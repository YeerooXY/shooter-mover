using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Runs.Session
{
    public sealed class ProductionRunStatInputResolutionV1
    {
        private readonly ReadOnlyCollection<DerivedStatModifierSourceV1>
            runSources;
        private readonly ReadOnlyCollection<string> activeConditionIds;

        public ProductionRunStatInputResolutionV1(
            DerivedCharacterStatInputV1 characterInput,
            IEnumerable<DerivedStatModifierSourceV1> runSources,
            IEnumerable<string> activeConditionIds)
        {
            CharacterInput = characterInput
                ?? throw new ArgumentNullException(nameof(characterInput));
            this.runSources = new ReadOnlyCollection<DerivedStatModifierSourceV1>(
                (runSources ?? Array.Empty<DerivedStatModifierSourceV1>())
                    .ToList());
            this.activeConditionIds = new ReadOnlyCollection<string>(
                (activeConditionIds ?? Array.Empty<string>())
                    .Select(value => (value ?? string.Empty).Trim())
                    .ToList());
            if (this.runSources.Any(source => source == null)
                || this.activeConditionIds.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Resolved run-stat sources and condition identities must be valid.");
            }
        }

        public DerivedCharacterStatInputV1 CharacterInput { get; }
        public IReadOnlyList<DerivedStatModifierSourceV1> RunSources
        {
            get { return runSources; }
        }
        public IReadOnlyList<string> ActiveConditionIds
        {
            get { return activeConditionIds; }
        }
    }

    public interface IProductionRunStatInputResolverV1
    {
        ProductionRunStatInputResolutionV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            ProductionCharacterRuntimeGraphV1 characterGraph,
            CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 currentRoutePayload,
            RankedSkillAllocationSnapshotV2 skillSnapshot,
            IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment);
    }

    /// <summary>
    /// Freezes one exact selected account-backed character graph into immutable run-start
    /// inputs. It never mutates the graph and resolves current Hub state on each new start.
    /// </summary>
    public sealed class ProductionCharacterRunSessionStartSourceV1 :
        IRunSessionStartSourceV1
    {
        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly IProductionRunStatInputResolverV1 statInputResolver;
        private readonly IDerivedCharacterStatComposerV1 statComposer;
        private readonly IRunSessionRuntimePortFactoryV1 runtimePortFactory;

        public ProductionCharacterRunSessionStartSourceV1(
            CharacterCompositionCoordinatorV1 composition,
            IProductionRunStatInputResolverV1 statInputResolver,
            IRunSessionRuntimePortFactoryV1 runtimePortFactory,
            IDerivedCharacterStatComposerV1 statComposer = null)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.statInputResolver = statInputResolver
                ?? throw new ArgumentNullException(nameof(statInputResolver));
            this.runtimePortFactory = runtimePortFactory
                ?? throw new ArgumentNullException(nameof(runtimePortFactory));
            this.statComposer = statComposer
                ?? new DefaultDerivedCharacterStatComposerV1();
        }

        public RunSessionStartMaterialV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId)
        {
            if (command == null || resolvedRunStableId == null)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-production-start-input-null");
            }
            ICharacterRuntimeGraphV1 selected = composition.ActiveRuntime;
            var graph = selected as ProductionCharacterRuntimeGraphV1;
            if (graph == null || graph.IsDisposed)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-selected-production-character-unavailable");
            }

            CharacterInstanceSnapshotV1 character = graph.Character;
            if (character == null
                || character.CharacterInstanceStableId
                    != command.SelectedCharacterInstanceStableId)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-selected-character-mismatch");
            }
            if (character.Revision != command.ExpectedCharacterRevision)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-selected-character-revision-stale");
            }
            if (!string.Equals(
                character.Fingerprint,
                command.ExpectedCharacterFingerprint,
                StringComparison.Ordinal))
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-selected-character-fingerprint-stale");
            }

            InventoryLoadoutAuthoritySnapshotV1 loadout =
                graph.LoadoutRuntime.LoadoutAuthority.ExportSnapshot();
            PlayerHoldingsSnapshotV1 holdings =
                graph.LoadoutRuntime.Holdings.ExportSnapshot();
            if (loadout == null
                || !loadout.HasValidFingerprint()
                || holdings == null)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-production-upstream-snapshot-invalid");
            }

            PlayerRouteProfilePayloadV1 currentRoute;
            List<FrozenRunEquipmentV1> frozenEquipment;
            string rejection;
            if (!TryFreezeEquipment(
                graph,
                loadout,
                holdings,
                out currentRoute,
                out frozenEquipment,
                out rejection))
            {
                return RunSessionStartMaterialV1.Reject(rejection);
            }

            RankedSkillAllocationSnapshotV2 skillSnapshot;
            try
            {
                skillSnapshot = graph.SkillAuthority.Get(graph.SkillProfileId);
            }
            catch (Exception exception)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-skill-snapshot-unavailable:"
                    + exception.GetType().Name);
            }
            if (skillSnapshot == null)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-skill-snapshot-null");
            }

            ProductionRunStatInputResolutionV1 resolvedStats;
            try
            {
                resolvedStats = statInputResolver.Resolve(
                    command,
                    resolvedRunStableId,
                    graph,
                    character,
                    currentRoute,
                    skillSnapshot,
                    frozenEquipment);
            }
            catch (Exception exception)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-stat-input-resolution-failed:"
                    + exception.GetType().Name);
            }
            if (resolvedStats == null
                || !string.Equals(
                    resolvedStats.CharacterInput.CharacterInstanceId,
                    character.CharacterInstanceStableId.ToString(),
                    StringComparison.Ordinal))
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-stat-input-character-mismatch");
            }

            DerivedCharacterStatsSnapshotV1 characterStats;
            RunCombatProfileV1 combatProfile;
            try
            {
                characterStats = statComposer.DeriveCharacter(
                    resolvedStats.CharacterInput);
                string runContextFingerprint = RunSessionFingerprintV1.Hash(
                    command.Fingerprint
                    + "|"
                    + currentRoute.Fingerprint
                    + "|"
                    + loadout.Fingerprint
                    + "|"
                    + holdings.Fingerprint
                    + "|"
                    + skillSnapshot.Fingerprint
                    + "|"
                    + string.Join(
                        ";",
                        frozenEquipment.Select(item => item.Fingerprint)));
                combatProfile = statComposer.BuildRunProfile(
                    new RunCombatProfileInputV1(
                        resolvedRunStableId.ToString(),
                        runContextFingerprint,
                        characterStats,
                        resolvedStats.RunSources,
                        resolvedStats.ActiveConditionIds,
                        resolvedStats.CharacterInput.Policy));
            }
            catch (Exception exception)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-stat-composition-failed:"
                    + exception.GetType().Name);
            }

            var frozenInputs = new FrozenCharacterRunInputsV1(
                character,
                currentRoute,
                loadout.Sequence,
                loadout.Fingerprint,
                graph.LoadoutRuntime.Holdings.Sequence,
                holdings.Fingerprint,
                skillSnapshot,
                characterStats,
                combatProfile,
                frozenEquipment,
                command.EventModifierContextFingerprint);

            RunSessionRuntimePortsV1 ports;
            try
            {
                ports = runtimePortFactory.Create(
                    command,
                    resolvedRunStableId,
                    frozenInputs);
            }
            catch (Exception exception)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-runtime-port-composition-failed:"
                    + exception.GetType().Name);
            }
            if (ports == null)
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-runtime-port-composition-null");
            }
            IReadOnlyList<StableId> frozenWeaponIds = frozenEquipment
                .Where(item => item.EquipmentDefinition.CategoryId
                    == EquipmentCategoryIds.Weapon)
                .Select(item => item.EquipmentInstanceStableId)
                .OrderBy(id => id)
                .ToList();
            if (ports.Weapons.FrozenEquipmentInstanceStableIds.Count
                    != frozenWeaponIds.Count
                || !ports.Weapons.FrozenEquipmentInstanceStableIds
                    .OrderBy(id => id)
                    .SequenceEqual(frozenWeaponIds))
            {
                return RunSessionStartMaterialV1.Reject(
                    "run-weapon-port-frozen-equipment-mismatch");
            }

            return RunSessionStartMaterialV1.Accept(frozenInputs, ports);
        }

        private static bool TryFreezeEquipment(
            ProductionCharacterRuntimeGraphV1 graph,
            InventoryLoadoutAuthoritySnapshotV1 loadout,
            PlayerHoldingsSnapshotV1 holdings,
            out PlayerRouteProfilePayloadV1 currentRoute,
            out List<FrozenRunEquipmentV1> frozenEquipment,
            out string rejectionCode)
        {
            currentRoute = null;
            frozenEquipment = new List<FrozenRunEquipmentV1>();
            rejectionCode = string.Empty;
            var routeEquipment = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);

            for (int index = 0;
                index < InventoryLoadoutSlotsV1.All.Count;
                index++)
            {
                InventoryLoadoutSlotDescriptorV1 descriptor =
                    InventoryLoadoutSlotsV1.All[index];
                InventoryLoadoutSlotBindingV1 binding =
                    loadout.GetBinding(descriptor.SlotStableId);
                if (descriptor.Kind == InventoryLoadoutSlotKindV1.Weapon)
                {
                    routeEquipment.Add(binding.EquipmentInstanceStableId);
                }
                if (binding.EquipmentInstanceStableId == null)
                {
                    continue;
                }

                EquipmentInstance instance = FindEquipment(
                    holdings,
                    binding.EquipmentInstanceStableId);
                if (instance == null)
                {
                    rejectionCode =
                        "run-equipped-instance-not-owned:"
                        + binding.EquipmentInstanceStableId;
                    return false;
                }
                EquipmentDefinition definition = graph.LoadoutRuntime
                    .EquipmentCatalog.FindEquipmentDefinition(
                        instance.DefinitionId);
                if (definition == null)
                {
                    rejectionCode =
                        "run-equipped-definition-unresolved:"
                        + instance.DefinitionId;
                    return false;
                }
                if (descriptor.Kind == InventoryLoadoutSlotKindV1.Weapon
                    && definition.RuntimeWeaponReferenceId == null)
                {
                    rejectionCode =
                        "run-equipped-weapon-runtime-unresolved:"
                        + instance.InstanceId;
                    return false;
                }
                frozenEquipment.Add(new FrozenRunEquipmentV1(
                    descriptor.SlotStableId,
                    instance,
                    definition));
            }

            if (routeEquipment.Count
                != PlayerRouteProfilePayloadV1.WeaponSlotCount)
            {
                rejectionCode = "run-loadout-weapon-slot-count-invalid";
                return false;
            }
            try
            {
                currentRoute = PlayerRouteProfilePayloadV1.Create(
                    graph.Character.CharacterInstanceStableId,
                    graph.RoutePayload.LoadoutProfileStableId,
                    routeEquipment);
            }
            catch (Exception exception)
            {
                rejectionCode = "run-current-route-invalid:"
                    + exception.GetType().Name;
                return false;
            }
            frozenEquipment.Sort();
            return true;
        }

        private static EquipmentInstance FindEquipment(
            PlayerHoldingsSnapshotV1 holdings,
            StableId instanceStableId)
        {
            for (int index = 0; index < holdings.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding =
                    holdings.UniqueHoldings[index];
                if (holding != null
                    && holding.RewardKind
                        == RewardGrantKindV1.EquipmentReference
                    && holding.InstanceStableId == instanceStableId
                    && holding.EquipmentInstance != null
                    && holding.EquipmentInstance.InstanceId
                        == instanceStableId)
                {
                    return holding.EquipmentInstance;
                }
            }
            return null;
        }
    }
}

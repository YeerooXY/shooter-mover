using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ShooterMover.Application.Flow.Game;
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
    public sealed class RunStatInputResolution
    {
        private readonly ReadOnlyCollection<DerivedStatModifierSource>
            runSources;
        private readonly ReadOnlyCollection<string> activeConditionIds;

        public RunStatInputResolution(
            DerivedCharacterStatInput characterInput,
            IEnumerable<DerivedStatModifierSource> runSources,
            IEnumerable<string> activeConditionIds)
        {
            CharacterInput = characterInput
                ?? throw new ArgumentNullException(nameof(characterInput));
            this.runSources = new ReadOnlyCollection<DerivedStatModifierSource>(
                (runSources ?? Array.Empty<DerivedStatModifierSource>())
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

        public DerivedCharacterStatInput CharacterInput { get; }
        public IReadOnlyList<DerivedStatModifierSource> RunSources
        {
            get { return runSources; }
        }
        public IReadOnlyList<string> ActiveConditionIds
        {
            get { return activeConditionIds; }
        }
    }

    public interface IRunStatInputResolver
    {
        RunStatInputResolution Resolve(
            StartRunSessionCommand command,
            StableId resolvedRunStableId,
            CharacterLiveGraph characterGraph,
            CharacterInstanceSnapshot character,
            PlayerRouteProfilePayload currentRoutePayload,
            RankedSkillAllocationSnapshot skillSnapshot,
            IReadOnlyList<FrozenRunEquipment> frozenEquipment);
    }

    /// <summary>
    /// Freezes one exact selected account-backed character graph into immutable run-start
    /// inputs. It never mutates the graph and resolves current Hub state on each new start.
    /// </summary>
    public sealed class CharacterRunSessionStartSource :
        IRunSessionStartSource
    {
        private readonly CharacterSetupFlow composition;
        private readonly IRunStatInputResolver statInputResolver;
        private readonly IDerivedCharacterStatComposer statComposer;
        private readonly IRunSessionLivePortFactory runtimePortFactory;

        public CharacterRunSessionStartSource(
            CharacterSetupFlow composition,
            IRunStatInputResolver statInputResolver,
            IRunSessionLivePortFactory runtimePortFactory,
            IDerivedCharacterStatComposer statComposer = null)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.statInputResolver = statInputResolver
                ?? throw new ArgumentNullException(nameof(statInputResolver));
            this.runtimePortFactory = runtimePortFactory
                ?? throw new ArgumentNullException(nameof(runtimePortFactory));
            this.statComposer = statComposer
                ?? new DefaultDerivedCharacterStatComposer();
        }

        public RunSessionStartMaterial Resolve(
            StartRunSessionCommand command,
            StableId resolvedRunStableId)
        {
            if (command == null || resolvedRunStableId == null)
            {
                return RunSessionStartMaterial.Reject(
                    "run-production-start-input-null");
            }
            ICharacterLiveGraph selected = composition.ActiveRuntime;
            var graph = selected as CharacterLiveGraph;
            if (graph == null || graph.IsDisposed)
            {
                return RunSessionStartMaterial.Reject(
                    "run-selected-production-character-unavailable");
            }

            CharacterInstanceSnapshot character = graph.Character;
            if (character == null
                || character.CharacterInstanceStableId
                    != command.SelectedCharacterInstanceStableId)
            {
                return RunSessionStartMaterial.Reject(
                    "run-selected-character-mismatch");
            }
            if (character.Revision != command.ExpectedCharacterRevision)
            {
                return RunSessionStartMaterial.Reject(
                    "run-selected-character-revision-stale");
            }
            if (!string.Equals(
                character.Fingerprint,
                command.ExpectedCharacterFingerprint,
                StringComparison.Ordinal))
            {
                return RunSessionStartMaterial.Reject(
                    "run-selected-character-fingerprint-stale");
            }

            LoadoutSnapshot loadout =
                graph.LoadoutRuntime.MountLoadoutAuthority.ExportSnapshot();
            PlayerHoldingsSnapshot holdings =
                graph.LoadoutRuntime.Holdings.ExportSnapshot();
            if (loadout == null
                || !loadout.HasValidFingerprint()
                || holdings == null)
            {
                return RunSessionStartMaterial.Reject(
                    "run-production-upstream-snapshot-invalid");
            }

            PlayerRouteProfilePayload currentRoute;
            List<FrozenRunEquipment> frozenEquipment;
            string rejection;
            if (!TryFreezeEquipment(
                graph,
                loadout,
                holdings,
                out currentRoute,
                out frozenEquipment,
                out rejection))
            {
                return RunSessionStartMaterial.Reject(rejection);
            }

            RankedSkillAllocationSnapshot skillSnapshot;
            try
            {
                skillSnapshot = graph.SkillAuthority.Get(graph.SkillProfileId);
            }
            catch (Exception exception)
            {
                return RunSessionStartMaterial.Reject(
                    "run-skill-snapshot-unavailable:"
                    + exception.GetType().Name);
            }
            if (skillSnapshot == null)
            {
                return RunSessionStartMaterial.Reject(
                    "run-skill-snapshot-null");
            }

            RunStatInputResolution resolvedStats;
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
                return RunSessionStartMaterial.Reject(
                    "run-stat-input-resolution-failed:"
                    + exception.GetType().Name);
            }
            if (resolvedStats == null
                || !string.Equals(
                    resolvedStats.CharacterInput.CharacterInstanceId,
                    character.CharacterInstanceStableId.ToString(),
                    StringComparison.Ordinal))
            {
                return RunSessionStartMaterial.Reject(
                    "run-stat-input-character-mismatch");
            }

            DerivedCharacterStatsSnapshot characterStats;
            RunCombatProfile combatProfile;
            try
            {
                characterStats = statComposer.DeriveCharacter(
                    resolvedStats.CharacterInput);
                string runContextFingerprint = RunSessionFingerprint.Hash(
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
                    new RunCombatProfileInput(
                        resolvedRunStableId.ToString(),
                        runContextFingerprint,
                        characterStats,
                        resolvedStats.RunSources,
                        resolvedStats.ActiveConditionIds,
                        resolvedStats.CharacterInput.Policy));
            }
            catch (Exception exception)
            {
                return RunSessionStartMaterial.Reject(
                    "run-stat-composition-failed:"
                    + exception.GetType().Name);
            }

            var frozenInputs = new FrozenCharacterRunInputs(
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

            RunSessionLivePorts ports;
            try
            {
                ports = runtimePortFactory.Create(
                    command,
                    resolvedRunStableId,
                    frozenInputs);
            }
            catch (Exception exception)
            {
                return RunSessionStartMaterial.Reject(
                    "run-runtime-port-composition-failed:"
                    + exception.GetType().Name);
            }
            if (ports == null)
            {
                return RunSessionStartMaterial.Reject(
                    "run-runtime-port-composition-null");
            }
            IReadOnlyList<StableId> frozenGunIds = frozenEquipment
                .Where(item => item.EquipmentDefinition.CategoryId
                    == EquipmentCategoryIds.Gun)
                .Select(item => item.EquipmentInstanceStableId)
                .OrderBy(id => id)
                .ToList();
            if (ports.Guns.FrozenEquipmentInstanceStableIds.Count
                    != frozenGunIds.Count
                || !ports.Guns.FrozenEquipmentInstanceStableIds
                    .OrderBy(id => id)
                    .SequenceEqual(frozenGunIds))
            {
                return RunSessionStartMaterial.Reject(
                    "run-gun-port-frozen-equipment-mismatch");
            }

            return RunSessionStartMaterial.Accept(frozenInputs, ports);
        }

        private static bool TryFreezeEquipment(
            CharacterLiveGraph graph,
            LoadoutSnapshot loadout,
            PlayerHoldingsSnapshot holdings,
            out PlayerRouteProfilePayload currentRoute,
            out List<FrozenRunEquipment> frozenEquipment,
            out string rejectionCode)
        {
            currentRoute = null;
            frozenEquipment = new List<FrozenRunEquipment>();
            rejectionCode = string.Empty;
            if (graph == null
                || graph.LoadoutRuntime == null
                || loadout == null
                || !loadout.HasValidFingerprint()
                || holdings == null)
            {
                rejectionCode = "run-production-upstream-snapshot-invalid";
                return false;
            }

            GunSlots layout = graph.LoadoutRuntime.MountLayout;
            if (layout == null
                || loadout.Bindings.Count != layout.PhysicalPositions.Count)
            {
                rejectionCode = "run-gun-mount-layout-mismatch";
                return false;
            }

            for (int index = 0;
                 index < layout.PhysicalPositions.Count;
                 index++)
            {
                GunSlot position = layout.PhysicalPositions[index];
                EquippedGun binding = loadout.Find(position.MountStableId);
                if (binding == null)
                {
                    rejectionCode = "run-gun-mount-binding-missing:"
                        + position.MountStableId;
                    return false;
                }
                if (!position.IsActive && binding.InstanceId != null)
                {
                    rejectionCode = "run-locked-gun-mount-is-bound:"
                        + position.MountStableId;
                    return false;
                }
                if (binding.InstanceId == null)
                {
                    continue;
                }

                EquipmentInstance instance = FindEquipment(
                    holdings,
                    binding.InstanceId);
                if (instance == null)
                {
                    rejectionCode = "run-equipped-instance-not-owned:"
                        + binding.InstanceId;
                    return false;
                }
                EquipmentDefinition definition = graph.LoadoutRuntime
                    .EquipmentCatalog.FindEquipmentDefinition(
                        instance.DefinitionId);
                if (definition == null)
                {
                    rejectionCode = "run-equipped-definition-unresolved:"
                        + instance.DefinitionId;
                    return false;
                }
                if (definition.RuntimeGunReferenceId == null)
                {
                    rejectionCode = "run-equipped-gun-runtime-unresolved:"
                        + instance.InstanceId;
                    return false;
                }
                frozenEquipment.Add(new FrozenRunEquipment(
                    position.LoadoutSlotStableId,
                    instance,
                    definition));
            }

            try
            {
                currentRoute = LoadoutView.Route(
                    graph.Character.CharacterInstanceStableId,
                    graph.RoutePayload.LoadoutProfileStableId,
                    layout,
                    loadout);
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
            PlayerHoldingsSnapshot holdings,
            StableId instanceStableId)
        {
            for (int index = 0; index < holdings.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshot holding =
                    holdings.UniqueHoldings[index];
                if (holding != null
                    && holding.RewardKind
                        == RewardGrantKind.EquipmentReference
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

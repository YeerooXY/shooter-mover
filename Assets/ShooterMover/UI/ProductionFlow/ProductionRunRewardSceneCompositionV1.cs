using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UI.LevelSelection;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    [DefaultExecutionOrder(20000)]
    [DisallowMultipleComponent]
    public sealed class ProductionRunRewardSceneCompositionV1 : MonoBehaviour
    {
        private ProductionRunRewardRuntimeV1 runtime;
        private string diagnostic = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            InstallForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    ProductionPlayableLevelCatalogV1.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            ProductionPlayableLevelControllerV1 controller = FindInScene<ProductionPlayableLevelControllerV1>(scene);
            if (controller != null
                && controller.GetComponent<ProductionRunRewardSceneCompositionV1>() == null)
            {
                controller.gameObject.AddComponent<ProductionRunRewardSceneCompositionV1>();
            }
        }

        private void Start()
        {
            try
            {
                Compose();
            }
            catch (Exception exception)
            {
                diagnostic = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void Compose()
        {
            ProductionPlayableLevelControllerV1 controller =
                GetComponent<ProductionPlayableLevelControllerV1>();
            if (controller == null || !controller.IsConfigured)
            {
                throw new InvalidOperationException(
                    "The production playable level must be configured before run rewards compose.");
            }

            PlayerRouteProfilePayloadV1 route;
            StableId modeId;
            StableId levelId;
            if (!LevelSelectionRouteContextV1.TryRead(out route, out modeId, out levelId)
                || levelId == null
                || controller.LevelStableId != levelId)
            {
                throw new InvalidOperationException(
                    "The selected level route is missing or does not match the configured scene.");
            }

            ProductionPlayableLevelDefinitionV1 level;
            if (!ProductionPlayableLevelCatalogV1.TryResolve(levelId, out level)
                || level == null)
            {
                throw new InvalidOperationException(
                    "The selected production level definition is unavailable: " + levelId);
            }

            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 profile;
            ShooterMover.Application.Persistence.Composition.CharacterCompositionCoordinatorV1 coordinator;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out profile,
                    out coordinator)
                || graph == null
                || profile == null
                || coordinator == null
                || graph.IsDisposed
                || route == null
                || !graph.RoutePayload.Equals(route))
            {
                throw new InvalidOperationException(
                    "The exact selected account-backed character graph is unavailable.");
            }

            JsonRoomRuntimeBootstrap2D roomBootstrap =
                FindInScene<JsonRoomRuntimeBootstrap2D>(gameObject.scene);
            RoomRuntimeComposition2D rooms =
                FindInScene<RoomRuntimeComposition2D>(gameObject.scene);
            RoomEnemySpawner2D spawner =
                FindInScene<RoomEnemySpawner2D>(gameObject.scene);
            if (roomBootstrap == null
                || !roomBootstrap.IsBuilt
                || roomBootstrap.ImportedBundle == null
                || rooms == null
                || !rooms.IsBuilt
                || spawner == null)
            {
                throw new InvalidOperationException(
                    "The authored room runtime is not ready for run/reward composition.");
            }

            StableId proofRoomId = StableId.Parse("room.level1-entry");
            List<RoomEnemyPlacementContentV1> proofRows = roomBootstrap.ImportedBundle
                .Enemies
                .Where(row => row != null && row.RoomStableId == proofRoomId)
                .ToList();
            if (levelId != ProductionPlayableLevelCatalogV1.FirstLevelStableId
                || proofRows.Count != 1
                || proofRows[0].InstanceStableId == null)
            {
                throw new InvalidOperationException(
                    "RUN-REWARD-COMPOSITION-001 requires exactly one stable proof enemy "
                    + "in the authored Level 1 entry room.");
            }

            EnemyCatalogAsset2D enemyAsset = Resources.Load<EnemyCatalogAsset2D>(
                level.EnemyCatalogResourcePath);
            if (enemyAsset == null)
            {
                throw new InvalidOperationException(
                    "The selected level enemy catalog asset is unavailable.");
            }
            EnemyCatalogImportResultV1 imported = enemyAsset.Import();
            if (imported == null || !imported.IsValid || imported.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The selected level enemy catalog did not import successfully.");
            }

            runtime = ProductionRunRewardRuntimeV1.Create(
                level,
                graph,
                coordinator,
                rooms,
                imported.Catalog,
                proofRoomId,
                proofRows[0].InstanceStableId);
            spawner.ConfigureRunDownstream(
                runtime.RunStableId,
                runtime.ExperienceConsumer,
                runtime.DropConsumer,
                runtime.KillStatisticsConsumer);
            if (!spawner.Synchronize())
            {
                throw new InvalidOperationException(
                    "The authored room enemy runtime rejected the selected-character run composition: "
                    + spawner.LastBuildError);
            }
        }

        private void OnGUI()
        {
            if (runtime == null) return;
            PendingRunRewardProjectionV1 projection = runtime.ExportPendingProjection();
            if (projection.AcceptedAdmissionCount < 1) return;

            GUI.Box(new Rect(16f, 16f, 190f, 108f), string.Empty);
            GUI.Label(new Rect(28f, 24f, 170f, 22f), "Pending rewards:");
            GUI.Label(new Rect(28f, 48f, 170f, 20f), "Cash: " + projection.Cash);
            GUI.Label(new Rect(28f, 68f, 170f, 20f), "Scrap: " + projection.Scrap);
            GUI.Label(new Rect(28f, 88f, 170f, 20f),
                "Strongboxes: " + projection.Strongboxes);
        }

        private void OnDestroy()
        {
            runtime = null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T value = roots[index].GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }
    }

    internal sealed class ProductionRunRewardRuntimeV1
    {
        private readonly PendingTerminalDropAdmissionAuthorityV1 pending;
        private readonly PendingAdmissionProjectionConsumerV1 projection;

        private ProductionRunRewardRuntimeV1(
            RunSessionAuthorityV1 authority,
            RunSessionAggregateV1 run,
            PendingTerminalDropAdmissionAuthorityV1 pending,
            PendingAdmissionProjectionConsumerV1 projection,
            IEnemyDropFactConsumerV1 dropConsumer)
        {
            RunSessions = authority;
            Run = run;
            this.pending = pending;
            this.projection = projection;
            DropConsumer = dropConsumer;
            ExperienceConsumer = new ExplicitNoOpExperienceConsumerV1();
            KillStatisticsConsumer = new ExplicitNoOpKillStatisticsConsumerV1();
        }

        public RunSessionAuthorityV1 RunSessions { get; }
        public RunSessionAggregateV1 Run { get; }
        public StableId RunStableId { get { return Run.RunStableId; } }
        public IEnemyExperienceFactConsumerV1 ExperienceConsumer { get; }
        public IEnemyDropFactConsumerV1 DropConsumer { get; }
        public IEnemyKillStatFactConsumerV1 KillStatisticsConsumer { get; }

        public PendingRunRewardProjectionV1 ExportPendingProjection()
        {
            return projection.Export(pending);
        }

        public static ProductionRunRewardRuntimeV1 Create(
            ProductionPlayableLevelDefinitionV1 level,
            ProductionCharacterRuntimeGraphV1 graph,
            ShooterMover.Application.Persistence.Composition.CharacterCompositionCoordinatorV1 coordinator,
            RoomRuntimeComposition2D rooms,
            ShooterMover.Domain.Enemies.Catalog.EnemyCatalogV1 enemyCatalog,
            StableId proofRoomId,
            StableId proofPlacementId)
        {
            string token = Guid.NewGuid().ToString("N");
            StableId runId = StableId.Create("run", "playable-level-" + token);
            long seed = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue;
            var source = new ProductionCharacterRunSessionStartSourceV1(
                coordinator,
                new ProductionPlayableLevelStatInputResolverV1(level),
                new ProductionPlayableLevelRuntimePortFactoryV1(rooms));
            var authority = new RunSessionAuthorityV1(source);
            var command = new StartRunSessionCommandV1(
                StableId.Create("operation", "start-playable-run-" + token),
                runId,
                "playable-level-run-v1|" + level.LevelStableId + "|"
                    + graph.Character.CharacterInstanceStableId + "|" + token,
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                level.LevelStableId,
                StableId.Parse("difficulty.normal"),
                seed,
                0L,
                RunSessionFingerprintV1.Hash(
                    "playable-level-event-context-v1|" + level.LevelStableId));
            RunSessionStartResultV1 start = authority.Start(command);
            RunSessionAggregateV1 run;
            if (start == null
                || start.Status != RunSessionStartStatusV1.Started
                || !authority.TryGetRun(runId, out run)
                || run == null
                || run.LifecycleState != RunSessionLifecycleStateV1.Active)
            {
                throw new InvalidOperationException(
                    "The selected-character production Run Session did not start: "
                    + (start == null ? "result-null" : start.RejectionCode));
            }

            run.ConfigureRewardEnvironment(new RunRewardEnvironmentSnapshotV1(
                StableId.Parse("game-mode.campaign"),
                Array.Empty<StableId>(),
                1000,
                1000,
                ProductionRunDropPacingCatalogV1.Default));

            var pending = new PendingTerminalDropAdmissionAuthorityV1();
            var projection = new PendingAdmissionProjectionConsumerV1();
            Func<RunSessionAggregateV1> runResolver = delegate { return run; };
            var binding = TerminalDropBindingCompositionV1.Create(
                enemyCatalog,
                new ExactRunEnemySourceContextResolverV1(runResolver),
                new PropCatalogV1(
                    PropCapabilityRegistryV1.CreateBuiltIns(),
                    Array.Empty<PropDefinitionV1>()),
                new UnsupportedPropSourceContextResolverV1(),
                new RunSessionTerminalDropContextResolverV1(
                    authority,
                    new SelectedCharacterProgressionContextProviderV1(graph),
                    1),
                null,
                null,
                pending,
                admissionConsumer: projection,
                participantResolver: new RunSessionTerminalRewardParticipantResolverV1(
                    runResolver,
                    new TerminalRewardEligibilityPolicyV1(true, false, false)),
                environmentResolver: new RunSessionTerminalRewardEnvironmentResolverV1(
                    runResolver),
                overrideResolver: new DeterministicProofRewardOverrideResolverV1(
                    runId,
                    proofRoomId,
                    proofPlacementId));
            return new ProductionRunRewardRuntimeV1(
                authority,
                run,
                pending,
                projection,
                binding.EnemyConsumer);
        }
    }

    internal sealed class PendingRunRewardProjectionV1
    {
        public PendingRunRewardProjectionV1(
            int acceptedAdmissionCount,
            long cash,
            long scrap,
            long strongboxes)
        {
            AcceptedAdmissionCount = acceptedAdmissionCount;
            Cash = cash;
            Scrap = scrap;
            Strongboxes = strongboxes;
        }
        public int AcceptedAdmissionCount { get; }
        public long Cash { get; }
        public long Scrap { get; }
        public long Strongboxes { get; }
    }

    internal sealed class PendingAdmissionProjectionConsumerV1 :
        IPendingTerminalDropAdmissionConsumerV1
    {
        private readonly HashSet<StableId> operations = new HashSet<StableId>();

        public void Consume(PendingTerminalDropAdmissionResultV1 admission)
        {
            if (admission == null || !admission.IsAccepted
                || admission.OperationStableId == null)
            {
                return;
            }
            operations.Add(admission.OperationStableId);
        }

        public PendingRunRewardProjectionV1 Export(
            PendingTerminalDropAdmissionAuthorityV1 authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            long cash = 0L;
            long scrap = 0L;
            long boxes = 0L;
            int accepted = 0;
            foreach (StableId operation in operations.OrderBy(value => value))
            {
                GeneratedTerminalDropResultV1 result;
                if (!authority.TryGetPending(operation, out result) || result == null)
                {
                    throw new InvalidOperationException(
                        "An observed pending reward operation is no longer authoritative: "
                        + operation);
                }
                accepted++;
                for (int index = 0; index < result.GeneratedRewards.Count; index++)
                {
                    GeneratedTerminalDropRewardV1 reward = result.GeneratedRewards[index];
                    if (reward.Kind == RewardGrantKindV1.Money) cash += reward.Quantity;
                    else if (reward.Kind == RewardGrantKindV1.Scrap) scrap += reward.Quantity;
                    else if (reward.Kind == RewardGrantKindV1.Strongbox) boxes += reward.Quantity;
                }
            }
            return new PendingRunRewardProjectionV1(accepted, cash, scrap, boxes);
        }
    }

    internal sealed class ExplicitNoOpExperienceConsumerV1 :
        IEnemyExperienceFactConsumerV1
    {
        public void Consume(EnemyDeathFactV1 fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }
    }

    internal sealed class ExplicitNoOpKillStatisticsConsumerV1 :
        IEnemyKillStatFactConsumerV1
    {
        public void Consume(EnemyDeathFactV1 fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }
    }

    internal sealed class ExactRunEnemySourceContextResolverV1 :
        IEnemyTerminalSourceContextResolverV1
    {
        private readonly Func<RunSessionAggregateV1> runResolver;
        public ExactRunEnemySourceContextResolverV1(Func<RunSessionAggregateV1> runResolver)
        {
            this.runResolver = runResolver ?? throw new ArgumentNullException(nameof(runResolver));
        }

        public bool TryResolve(
            EnemyDeathFactV1 fact,
            out EnemyTerminalSourceContextV1 context,
            out string diagnostic)
        {
            context = null;
            RunSessionAggregateV1 run = runResolver();
            if (fact == null || fact.Identity == null || run == null || run.IsEnded)
            {
                diagnostic = "enemy-source-run-context-unavailable";
                return false;
            }
            if (fact.Identity.RunStableId != run.RunStableId
                || fact.Identity.PlacementStableId == null
                || fact.Identity.EntityInstanceId == null)
            {
                diagnostic = "enemy-source-run-context-mismatch";
                return false;
            }
            context = new EnemyTerminalSourceContextV1(
                run.RunStableId,
                run.LifecycleGeneration,
                fact.Identity.EntityInstanceId,
                fact.Identity.PlacementStableId,
                fact.LifecycleGeneration,
                RunSessionFingerprintV1.Hash(
                    "enemy-source-context-v1|" + run.FrozenInputs.Fingerprint + "|"
                    + fact.Identity.RoomStableId + "|" + fact.Identity.PlacementStableId
                    + "|" + fact.Identity.EntityInstanceId + "|"
                    + fact.LifecycleGeneration.ToString(CultureInfo.InvariantCulture)));
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class UnsupportedPropSourceContextResolverV1 :
        IPropTerminalSourceContextResolverV1
    {
        public bool TryResolve(
            PropTerminalFactV1 terminalFact,
            out PropTerminalSourceContextV1 context,
            out string diagnostic)
        {
            context = null;
            diagnostic = "prop-rewards-not-composed";
            return false;
        }
    }

    internal sealed class SelectedCharacterProgressionContextProviderV1 :
        IRunRewardProgressionContextProviderV1
    {
        private readonly ProductionCharacterRuntimeGraphV1 graph;
        public SelectedCharacterProgressionContextProviderV1(
            ProductionCharacterRuntimeGraphV1 graph)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        public bool TryResolve(
            RunSessionAggregateV1 run,
            out ProgressionContext progressionContext,
            out string diagnostic)
        {
            progressionContext = null;
            if (run == null
                || graph.IsDisposed
                || run.FrozenInputs.Character.CharacterInstanceStableId
                    != graph.Character.CharacterInstanceStableId)
            {
                diagnostic = "run-progression-selected-character-mismatch";
                return false;
            }
            progressionContext = graph.ExperienceAuthority.CurrentContext;
            diagnostic = progressionContext == null
                ? "run-progression-context-unavailable"
                : string.Empty;
            return progressionContext != null;
        }
    }

    internal sealed class DeterministicProofRewardOverrideResolverV1 :
        ITerminalRewardOverrideResolverV1
    {
        private readonly StableId runId;
        private readonly StableId roomId;
        private readonly StableId placementId;
        private readonly RewardProfileOverrideV1 proofOverride;

        public DeterministicProofRewardOverrideResolverV1(
            StableId runId,
            StableId roomId,
            StableId placementId)
        {
            this.runId = runId ?? throw new ArgumentNullException(nameof(runId));
            this.roomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            this.placementId = placementId
                ?? throw new ArgumentNullException(nameof(placementId));
            RewardSourceProfileV1 profile = RewardSourceProfileV1.Create(
                StableId.Parse("drop-source.development-run-reward-proof"),
                ProductionStrongboxTierSelectionCatalogV1.LowSourceProfileId,
                new[]
                {
                    Guaranteed("cash", 0, RewardGrantKindV1.Money,
                        StableId.Parse("currency.money"), RewardBoxPacingModeV1.None),
                    Guaranteed("scrap", 1, RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"), RewardBoxPacingModeV1.None),
                    Guaranteed("strongbox", 2, RewardGrantKindV1.Strongbox,
                        ProductionStrongboxTierSelectionCatalogV1.LowSourceProfileId,
                        RewardBoxPacingModeV1.GuaranteedBox),
                });
            proofOverride = RewardProfileOverrideV1.Replace(
                StableId.Parse("drop-override.development-run-reward-proof"),
                profile);
        }

        public bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            TerminalRewardEnvironmentV1 environment,
            TerminalRewardPlacementContextV1 placement,
            out TerminalRewardOverrideSetV1 overrides,
            out string diagnostic)
        {
            overrides = TerminalRewardOverrideSetV1.Empty();
            if (source == null || runContext == null || environment == null || placement == null)
            {
                diagnostic = "proof-reward-context-missing";
                return false;
            }
            if (source.RunStableId != runId
                || runContext.RunStableId != runId
                || placement.RoomStableId != roomId
                || placement.PlacementStableId != placementId)
            {
                diagnostic = string.Empty;
                return true;
            }
            if (source.DeclaredDropProfileStableId == null)
            {
                diagnostic = "proof-enemy-declared-drop-profile-missing";
                return false;
            }
            overrides = new TerminalRewardOverrideSetV1(
                null,
                null,
                null,
                Array.Empty<RewardProfileOverrideV1>(),
                proofOverride);
            diagnostic = string.Empty;
            return true;
        }

        private static RewardRollGroupV1 Guaranteed(
            string slug,
            int ordinal,
            RewardGrantKindV1 kind,
            StableId content,
            RewardBoxPacingModeV1 pacing)
        {
            return RewardRollGroupV1.CreateGuaranteed(
                StableId.Create("drop-group", "development-proof-" + slug),
                ordinal,
                pacing,
                new[]
                {
                    RewardOutcomeV1.CreateGrant(
                        StableId.Create("drop-outcome", "development-proof-" + slug),
                        RewardGrantSpecificationV1.Create(
                            StableId.Create("drop-grant", "development-proof-" + slug),
                            kind,
                            content,
                            RewardQuantityRangeV1.Create(1L, 1L),
                            Array.Empty<RewardScalingInputDescriptorV1>()),
                        1UL),
                });
        }
    }

    internal sealed class ProductionPlayableLevelStatInputResolverV1 :
        IProductionRunStatInputResolverV1
    {
        private readonly ProductionPlayableLevelDefinitionV1 level;
        public ProductionPlayableLevelStatInputResolverV1(
            ProductionPlayableLevelDefinitionV1 level)
        {
            this.level = level ?? throw new ArgumentNullException(nameof(level));
        }

        public ProductionRunStatInputResolutionV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            ProductionCharacterRuntimeGraphV1 characterGraph,
            ShooterMover.Domain.Persistence.Accounts.CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 currentRoutePayload,
            RankedSkillAllocationSnapshotV2 skillSnapshot,
            IReadOnlyList<FrozenRunEquipmentV1> frozenEquipment)
        {
            if (level.LevelStableId != ProductionPlayableLevelCatalogV1.FirstLevelStableId)
            {
                throw new InvalidOperationException(
                    "No authored run-stat baseline exists for level " + level.LevelStableId);
            }
            var values = new Dictionary<string, decimal>
            {
                { DerivedStatTargetIdsV1.MaximumHealth, 100m },
                { DerivedStatTargetIdsV1.MovementSpeed, 6m },
            };
            return new ProductionRunStatInputResolutionV1(
                new DerivedCharacterStatInputV1(
                    character.CharacterInstanceStableId.ToString(),
                    new CharacterBaseStatProfileV1(
                        "base-profile.production-playable-level-1",
                        character.ClassDefinitionStableId.ToString(),
                        character.CharacterLevel,
                        RunSessionFingerprintV1.Hash(
                            "production-playable-level-1-base-v1|"
                            + character.ClassDefinitionStableId),
                        values),
                    Array.Empty<DerivedStatModifierSourceV1>(),
                    DerivedStatPolicyV1.CreateDefault()),
                Array.Empty<DerivedStatModifierSourceV1>(),
                Array.Empty<string>());
        }
    }

    internal sealed class ProductionPlayableLevelRuntimePortFactoryV1 :
        IRunSessionRuntimePortFactoryV1
    {
        private readonly RoomRuntimeComposition2D rooms;
        public ProductionPlayableLevelRuntimePortFactoryV1(RoomRuntimeComposition2D rooms)
        {
            this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        }

        public RunSessionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs)
        {
            const long generation = 1L;
            return new RunSessionRuntimePortsV1(
                new SnapshotPlayerRunPortV1(
                    generation,
                    resolvedRunStableId,
                    (double)frozenInputs.CombatProfile.MaximumHealth),
                new SnapshotWeaponRunPortV1(
                    generation,
                    frozenInputs.Equipment
                        .Where(item => item.EquipmentDefinition.CategoryId
                            == EquipmentCategoryIds.Weapon)
                        .Select(item => item.EquipmentInstanceStableId)),
                new SnapshotStatusRunPortV1(generation),
                new SnapshotConditionalRunPortV1(generation),
                new SnapshotAbilityRunPortV1(generation),
                new SnapshotRoomRunPortV1(generation, rooms),
                new UnsupportedMissionResultRunPortV1());
        }
    }

    internal abstract class ImmutableRunLifecyclePortV1 : IRunLifecycleRuntimePortV1
    {
        protected ImmutableRunLifecyclePortV1(string portId, long generation)
        {
            PortId = portId;
            LifecycleGeneration = generation;
        }
        public string PortId { get; }
        public long LifecycleGeneration { get; }
        public virtual string SnapshotFingerprint
        {
            get { return RunSessionFingerprintV1.Hash(PortId + "|" + LifecycleGeneration); }
        }
        public string ValidateRestart(long retiring, long replacement, long tick)
        {
            return "playable-run-restart-not-composed";
        }
        public RunRuntimePortRestartResultV1 Restart(
            StableId operation, long retiring, long replacement, long tick)
        {
            return new RunRuntimePortRestartResultV1(
                false,
                ValidateRestart(retiring, replacement, tick),
                LifecycleGeneration,
                SnapshotFingerprint);
        }
    }

    internal sealed class SnapshotPlayerRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunPlayerRuntimePortV1
    {
        private readonly StableId actorId;
        private readonly StableId participantId;
        private readonly double health;
        public SnapshotPlayerRunPortV1(long generation, StableId runId, double health)
            : base("production-playable-player-projection", generation)
        {
            actorId = StableId.Create("run-actor", runId.Value);
            participantId = StableId.Create("run-participant", runId.Value);
            this.health = health;
        }
        public RunPlayerRuntimeSnapshotV1 ExportSnapshot()
        {
            return new RunPlayerRuntimeSnapshotV1(
                actorId, participantId, LifecycleGeneration,
                health, health, 0d, 0d, 0L);
        }
        public override string SnapshotFingerprint { get { return ExportSnapshot().Fingerprint; } }
    }

    internal sealed class SnapshotWeaponRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunWeaponRuntimePortV1
    {
        private readonly IReadOnlyList<StableId> ids;
        public SnapshotWeaponRunPortV1(long generation, IEnumerable<StableId> ids)
            : base("production-playable-weapon-projection", generation)
        {
            this.ids = ids.OrderBy(value => value).ToList().AsReadOnly();
        }
        public IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds { get { return ids; } }
    }

    internal sealed class SnapshotStatusRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunStatusEffectRuntimePortV1
    {
        public SnapshotStatusRunPortV1(long generation)
            : base("production-playable-status-projection", generation) { }
        public int ActiveEffectCount { get { return 0; } }
    }

    internal sealed class SnapshotConditionalRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunConditionalFactRuntimePortV1
    {
        public SnapshotConditionalRunPortV1(long generation)
            : base("production-playable-condition-projection", generation) { }
    }

    internal sealed class SnapshotAbilityRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunActiveAbilityRuntimePortV1
    {
        public SnapshotAbilityRunPortV1(long generation)
            : base("production-playable-ability-projection", generation) { }
    }

    internal sealed class SnapshotRoomRunPortV1 : ImmutableRunLifecyclePortV1,
        IRunRoomRuntimePortV1
    {
        private readonly RoomRuntimeComposition2D rooms;
        public SnapshotRoomRunPortV1(long generation, RoomRuntimeComposition2D rooms)
            : base("production-playable-room-projection", generation)
        {
            this.rooms = rooms;
        }
        public StableId CurrentRoomStableId { get { return rooms.CurrentRoomStableId; } }
        public override string SnapshotFingerprint
        {
            get
            {
                return RunSessionFingerprintV1.Hash(
                    PortId + "|" + LifecycleGeneration + "|" + CurrentRoomStableId);
            }
        }
    }

    internal sealed class UnsupportedMissionResultRunPortV1 : IRunMissionResultPortV1
    {
        public long Sequence { get { return 0L; } }
        public bool TryGetRun(StableId runStableId, out MissionRunPayloadV1 runPayload)
        {
            runPayload = null;
            return false;
        }
        public MissionRunAuthorityResultV1 RecordCollectedStrongbox(
            RunStrongboxCollectionRequestV1 request,
            PlayerRouteProfilePayloadV1 routePayload)
        {
            return Invalid(
                request == null ? null : request.OperationStableId,
                request == null ? string.Empty : request.Fingerprint);
        }
        public MissionRunAuthorityResultV1 EndRun(
            EndRunSessionCommandV1 command,
            PlayerRouteProfilePayloadV1 routePayload)
        {
            return Invalid(
                command == null ? null : command.OperationStableId,
                command == null ? string.Empty : command.Fingerprint);
        }
        private static MissionRunAuthorityResultV1 Invalid(
            StableId operation,
            string fingerprint)
        {
            return new MissionRunAuthorityResultV1(
                MissionRunAuthorityStatusV1.InvalidRequest,
                0L,
                0L,
                operation,
                fingerprint,
                null,
                null,
                null,
                "run-results-not-composed");
        }
    }
}
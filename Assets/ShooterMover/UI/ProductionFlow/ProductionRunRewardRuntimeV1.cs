using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.UI.ProductionFlow
{
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
                ProductionRunFingerprintV1.Hash(
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
            TerminalDropBindingCompositionV1 binding = TerminalDropBindingCompositionV1.Create(
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
            if (fact == null
                || fact.Identity == null
                || run == null
                || run.LifecycleState == RunSessionLifecycleStateV1.Ended)
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
                ProductionRunFingerprintV1.Hash(
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

    internal static class ProductionRunFingerprintV1
    {
        public static string Hash(string material)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(
                    Encoding.UTF8.GetBytes(material ?? string.Empty));
                return BitConverter.ToString(digest)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
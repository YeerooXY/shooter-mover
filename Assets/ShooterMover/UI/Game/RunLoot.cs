using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.LootDropBinding;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.UI.Game
{
    internal sealed class RunLoot
    {
        private readonly PendingLootDropAdmissionState pending;
        private readonly PendingAdmissionViewConsumer projection;

        private RunLoot(
            RunSessionState authority,
            RunSessionAggregate run,
            PendingLootDropAdmissionState pending,
            PendingAdmissionViewConsumer projection,
            IEnemyDropFactConsumer dropConsumer)
        {
            RunSessions = authority;
            Run = run;
            this.pending = pending;
            this.projection = projection;
            DropConsumer = dropConsumer
                ?? throw new ArgumentNullException(nameof(dropConsumer));
            ExperienceConsumer = new ExplicitNoOpExperienceConsumer();
            KillStatisticsConsumer = new ExplicitNoOpKillStatisticsConsumer();
        }

        public RunSessionState RunSessions { get; }
        public RunSessionAggregate Run { get; }
        public StableId RunStableId { get { return Run.RunStableId; } }
        public IEnemyExperienceFactConsumer ExperienceConsumer { get; }
        public IEnemyDropFactConsumer DropConsumer { get; }
        public IEnemyKillStatFactConsumer KillStatisticsConsumer { get; }

        public PendingRunRewardView ExportPendingProjection()
        {
            return projection.Export(pending);
        }

        public static RunLoot Create(
            PlayableLevelDefinition level,
            StableId gameModeId,
            CharacterLiveGraph graph,
            ShooterMover.Application.Persistence.Composition.CharacterSetupFlow coordinator,
            LevelRooms rooms,
            ShooterMover.Domain.Enemies.Catalog.EnemyCatalog enemyCatalog,
            StableId proofRoomId,
            StableId proofPlacementId)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (gameModeId == null) throw new ArgumentNullException(nameof(gameModeId));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (coordinator == null) throw new ArgumentNullException(nameof(coordinator));
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (enemyCatalog == null) throw new ArgumentNullException(nameof(enemyCatalog));
            if (proofRoomId == null) throw new ArgumentNullException(nameof(proofRoomId));
            if (proofPlacementId == null)
                throw new ArgumentNullException(nameof(proofPlacementId));

            StableId difficultyId = StableId.Parse("difficulty.normal");
            ProgressionContext currentProgression =
                graph.ExperienceAuthority.CurrentContext;
            if (currentProgression == null || currentProgression.CharacterLevel < 1)
            {
                throw new InvalidOperationException(
                    "The selected character progression context is unavailable at run start.");
            }
            ProgressionContext frozenProgression = ProgressionContext.Create(
                currentProgression.CharacterLevel,
                currentProgression.RegionLevel,
                difficultyId,
                currentProgression.DifficultyValue,
                currentProgression.ProgressionTags);

            string token = Guid.NewGuid().ToString("N");
            StableId runId = StableId.Create("run", "playable-level-" + token);
            long seed = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0)
                & long.MaxValue;
            var source = new CharacterRunSessionStartSource(
                coordinator,
                new PlayableLevelStatInputResolver(
                    level,
                    frozenProgression),
                new PlayableLevelLivePortFactory(rooms));
            var authority = new RunSessionState(source);
            var command = new StartRunSessionCommand(
                StableId.Create("operation", "start-playable-run-" + token),
                runId,
                "playable-level-run-v1|" + level.LevelStableId + "|"
                    + graph.Character.CharacterInstanceStableId + "|" + token,
                graph.Character.CharacterInstanceStableId,
                graph.Character.Revision,
                graph.Character.Fingerprint,
                level.LevelStableId,
                difficultyId,
                seed,
                0L,
                RunFingerprint.Hash(
                    "playable-level-event-context-v1|" + level.LevelStableId + "|"
                    + gameModeId + "|" + frozenProgression.Fingerprint));
            RunSessionStartResult start = authority.Start(command);
            RunSessionAggregate run;
            if (start == null
                || start.Status != RunSessionStartStatus.Started
                || !authority.TryGetRun(runId, out run)
                || run == null
                || run.LifecycleState != RunSessionLifecycleState.Active)
            {
                throw new InvalidOperationException(
                    "The selected-character production Run Session did not start: "
                    + (start == null ? "result-null" : start.RejectionCode));
            }
            if (run.FrozenInputs.CharacterStats.Level
                    != frozenProgression.CharacterLevel
                || run.StartCommand.DifficultyStableId
                    != frozenProgression.DifficultyId)
            {
                throw new InvalidOperationException(
                    "The accepted Run Session did not preserve its frozen progression context.");
            }

            run.ConfigureRewardEnvironment(new RunRewardEnvironmentSnapshot(
                gameModeId,
                Array.Empty<StableId>(),
                1000,
                1000,
                RunDropPacingCatalog.Default));

            var pending = new PendingLootDropAdmissionState();
            var projection = new PendingAdmissionViewConsumer();
            Func<RunSessionAggregate> runResolver = delegate { return run; };
            var canonicalOverrides =
                new RunSessionTerminalRewardOverrideResolver(runResolver);
            var proofOverrides = new DeterministicProofRewardOverrideResolver(
                runId,
                proofRoomId,
                proofPlacementId);
            var composedOverrides = new ProofOverlayRewardOverrideResolver(
                canonicalOverrides,
                proofOverrides);
            var enemySourceContexts =
                new ExactRunEnemySourceContextResolver(runResolver);
            var propCatalog = new PropCatalog(
                PropCapabilityRegistry.CreateBuiltIns(),
                Array.Empty<PropDefinition>());
            var propSourceContexts = new UnsupportedPropSourceContextResolver();
            var runContexts = new RunSessionLootDropContextResolver(
                authority,
                new FrozenRunProgressionContextProvider(
                    graph.Character.CharacterInstanceStableId,
                    frozenProgression),
                1);
            var participantResolver =
                new RunSessionTerminalRewardParticipantResolver(
                    runResolver,
                    new TerminalRewardEligibilityPolicy(
                        true,
                        false,
                        false));
            var environmentResolver =
                new RunSessionTerminalRewardEnvironmentResolver(runResolver);

            Func<EnemyLootDropFactConsumer> consumerFactory = delegate
            {
                var personalGeneration = new PersonalRewardGenerationActions(
                    new ParticipantDropPacing(
                        new RunSessionParticipantDropPacingStateStore(run)));
                var deliveryOutbox =
                    new RunSessionPersonalRewardDeliveryOutbox(run);
                LootDropBindingSetup binding =
                    LootDropBindingSetup.Create(
                        enemyCatalog,
                        enemySourceContexts,
                        propCatalog,
                        propSourceContexts,
                        runContexts,
                        null,
                        null,
                        pending,
                        admissionConsumer: null,
                        personalGenerationService: personalGeneration,
                        participantResolver: participantResolver,
                        environmentResolver: environmentResolver,
                        overrideResolver: composedOverrides,
                        deliveryOutbox: deliveryOutbox,
                        requireAcceptedPublication: true);
                return binding.EnemyConsumer;
            };

            IEnemyDropFactConsumer transactionalConsumer =
                new TransactionalRunRewardEnemyConsumer(
                    run,
                    pending,
                    projection,
                    consumerFactory,
                    runId,
                    proofRoomId,
                    proofPlacementId);
            return new RunLoot(
                authority,
                run,
                pending,
                projection,
                transactionalConsumer);
        }
    }

    internal sealed class PendingRunRewardView
    {
        public PendingRunRewardView(
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

    internal sealed class PendingAdmissionViewConsumer :
        IPendingLootDropAdmissionConsumer
    {
        private readonly HashSet<StableId> operations = new HashSet<StableId>();

        public void Consume(PendingLootDropAdmissionResult admission)
        {
            if (admission == null || !admission.IsAccepted
                || admission.OperationStableId == null)
            {
                return;
            }
            operations.Add(admission.OperationStableId);
        }

        public void RollbackAccepted(
            PendingLootDropAdmissionResult admission)
        {
            if (admission == null
                || admission.Status
                    != PendingLootDropAdmissionStatus.Accepted
                || admission.OperationStableId == null)
            {
                return;
            }
            operations.Remove(admission.OperationStableId);
        }

        public PendingRunRewardView Export(
            PendingLootDropAdmissionState authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            long cash = 0L;
            long scrap = 0L;
            long boxes = 0L;
            int accepted = 0;
            foreach (StableId operation in operations.OrderBy(value => value))
            {
                GeneratedLootDropResult result;
                if (!authority.TryGetPending(operation, out result) || result == null)
                {
                    throw new InvalidOperationException(
                        "An observed pending reward operation is no longer authoritative: "
                        + operation);
                }
                accepted++;
                for (int index = 0; index < result.GeneratedRewards.Count; index++)
                {
                    GeneratedLootDropReward reward = result.GeneratedRewards[index];
                    if (reward.Kind == RewardGrantKind.Money) cash += reward.Quantity;
                    else if (reward.Kind == RewardGrantKind.Scrap) scrap += reward.Quantity;
                    else if (reward.Kind == RewardGrantKind.Strongbox) boxes += reward.Quantity;
                }
            }
            return new PendingRunRewardView(accepted, cash, scrap, boxes);
        }
    }

    internal sealed class ExplicitNoOpExperienceConsumer :
        IEnemyExperienceFactConsumer
    {
        public void Consume(EnemyDeathFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }
    }

    internal sealed class ExplicitNoOpKillStatisticsConsumer :
        IEnemyKillStatFactConsumer
    {
        public void Consume(EnemyDeathFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
        }
    }

    /// <summary>
    /// Coordinates run-owned pacing/outbox state with scene-local pending admission.
    /// A failed attempt restores the exact run reward snapshot, compensates only pending
    /// records created by that attempt, and discards the attempt's generation replay state.
    /// A successful attempt retains its consumer so exact redelivery reuses the same ledger.
    /// </summary>
    internal sealed class TransactionalRunRewardEnemyConsumer :
        IEnemyDropFactConsumer
    {
        private readonly object gate = new object();
        private readonly RunSessionAggregate run;
        private readonly PendingLootDropAdmissionState pending;
        private readonly PendingAdmissionViewConsumer projection;
        private readonly Func<EnemyLootDropFactConsumer> consumerFactory;
        private readonly StableId runId;
        private readonly StableId proofRoomId;
        private readonly StableId proofPlacementId;
        private readonly Dictionary<StableId, EnemyLootDropFactConsumer>
            committedByDeathEvent =
                new Dictionary<StableId, EnemyLootDropFactConsumer>();

        public TransactionalRunRewardEnemyConsumer(
            RunSessionAggregate run,
            PendingLootDropAdmissionState pending,
            PendingAdmissionViewConsumer projection,
            Func<EnemyLootDropFactConsumer> consumerFactory,
            StableId runId,
            StableId proofRoomId,
            StableId proofPlacementId)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.pending = pending ?? throw new ArgumentNullException(nameof(pending));
            this.projection = projection
                ?? throw new ArgumentNullException(nameof(projection));
            this.consumerFactory = consumerFactory
                ?? throw new ArgumentNullException(nameof(consumerFactory));
            this.runId = runId ?? throw new ArgumentNullException(nameof(runId));
            this.proofRoomId = proofRoomId
                ?? throw new ArgumentNullException(nameof(proofRoomId));
            this.proofPlacementId = proofPlacementId
                ?? throw new ArgumentNullException(nameof(proofPlacementId));
        }

        public void Consume(EnemyDeathFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            if (fact.DeathEventStableId == null)
            {
                throw new InvalidOperationException(
                    "A transactional enemy reward requires one stable death-event identity.");
            }

            lock (gate)
            {
                EnemyLootDropFactConsumer committed;
                if (committedByDeathEvent.TryGetValue(
                        fact.DeathEventStableId,
                        out committed))
                {
                    committed.Consume(fact);
                    ValidateProofIfRequired(fact, committed.LastAdmissions);
                    Publish(committed.LastAdmissions);
                    return;
                }

                RunLootSnapshot snapshot =
                    run.ExportRewardRuntimeSnapshot();
                EnemyLootDropFactConsumer attempt = null;
                try
                {
                    attempt = consumerFactory();
                    if (attempt == null)
                    {
                        throw new InvalidOperationException(
                            "The enemy reward attempt factory returned no consumer.");
                    }

                    attempt.Consume(fact);
                    ValidateProofIfRequired(fact, attempt.LastAdmissions);
                    Publish(attempt.LastAdmissions);
                    committedByDeathEvent.Add(
                        fact.DeathEventStableId,
                        attempt);
                }
                catch (Exception exception)
                {
                    Exception rollbackFailure = RollbackAttempt(
                        attempt == null ? null : attempt.LastAdmissions,
                        snapshot);
                    if (rollbackFailure != null && !IsFatal(exception))
                    {
                        throw new InvalidOperationException(
                            "The enemy reward attempt failed and compensation was incomplete.",
                            new AggregateException(exception, rollbackFailure));
                    }

                    ExceptionDispatchInfo.Capture(exception).Throw();
                    throw;
                }
            }
        }

        private Exception RollbackAttempt(
            IReadOnlyList<PendingLootDropAdmissionResult> admissions,
            RunLootSnapshot snapshot)
        {
            Exception failure = null;
            if (admissions != null)
            {
                for (int index = admissions.Count - 1; index >= 0; index--)
                {
                    PendingLootDropAdmissionResult admission =
                        admissions[index];
                    if (admission == null
                        || admission.Status
                            != PendingLootDropAdmissionStatus.Accepted)
                    {
                        continue;
                    }

                    projection.RollbackAccepted(admission);
                    string diagnostic;
                    try
                    {
                        if (!pending.TryRollbackAccepted(
                                admission,
                                out diagnostic))
                        {
                            failure = Combine(
                                failure,
                                new InvalidOperationException(
                                    "Pending reward compensation rejected: "
                                    + diagnostic));
                        }
                    }
                    catch (Exception exception)
                    {
                        if (IsFatal(exception))
                        {
                            ExceptionDispatchInfo.Capture(exception).Throw();
                        }
                        failure = Combine(failure, exception);
                    }
                }
            }

            try
            {
                run.RestoreRewardRuntimeSnapshot(snapshot);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }
                failure = Combine(failure, exception);
            }
            return failure;
        }

        private void Publish(
            IReadOnlyList<PendingLootDropAdmissionResult> admissions)
        {
            if (admissions == null) return;
            for (int index = 0; index < admissions.Count; index++)
            {
                projection.Consume(admissions[index]);
            }
        }

        private void ValidateProofIfRequired(
            EnemyDeathFact fact,
            IReadOnlyList<PendingLootDropAdmissionResult> admissions)
        {
            if (!IsProof(fact)) return;
            if (admissions == null
                || admissions.Count != 1
                || admissions[0] == null
                || !admissions[0].IsAccepted
                || admissions[0].PendingResult == null
                || !HasExactProofRewards(admissions[0].PendingResult))
            {
                string detail = admissions == null || admissions.Count == 0
                    ? "admission-missing"
                    : admissions[0] == null
                        ? "admission-null"
                        : admissions[0].Diagnostic;
                throw new InvalidOperationException(
                    "The deterministic proof reward was not admitted exactly once: "
                    + detail);
            }
        }

        private bool IsProof(EnemyDeathFact fact)
        {
            return fact.Identity != null
                && fact.Identity.RunStableId == runId
                && fact.Identity.RoomStableId == proofRoomId
                && fact.Identity.PlacementStableId == proofPlacementId;
        }

        private static bool HasExactProofRewards(
            GeneratedLootDropResult result)
        {
            long cash = 0L;
            long scrap = 0L;
            long boxes = 0L;
            for (int index = 0; index < result.GeneratedRewards.Count; index++)
            {
                GeneratedLootDropReward reward =
                    result.GeneratedRewards[index];
                if (reward.Kind == RewardGrantKind.Money)
                    cash += reward.Quantity;
                else if (reward.Kind == RewardGrantKind.Scrap)
                    scrap += reward.Quantity;
                else if (reward.Kind == RewardGrantKind.Strongbox)
                    boxes += reward.Quantity;
            }
            return cash == 1L && scrap == 1L && boxes == 1L;
        }

        private static Exception Combine(Exception current, Exception next)
        {
            if (current == null) return next;
            return new AggregateException(current, next);
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }

    internal sealed class ExactRunEnemySourceContextResolver :
        IEnemyTerminalSourceContextResolver
    {
        private readonly Func<RunSessionAggregate> runResolver;
        public ExactRunEnemySourceContextResolver(Func<RunSessionAggregate> runResolver)
        {
            this.runResolver = runResolver ?? throw new ArgumentNullException(nameof(runResolver));
        }

        public bool TryResolve(
            EnemyDeathFact fact,
            out EnemyTerminalSourceContext context,
            out string diagnostic)
        {
            context = null;
            RunSessionAggregate run = runResolver();
            if (fact == null
                || fact.Identity == null
                || run == null
                || run.LifecycleState == RunSessionLifecycleState.Ended)
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
            context = new EnemyTerminalSourceContext(
                run.RunStableId,
                run.LifecycleGeneration,
                fact.Identity.EntityInstanceId,
                fact.Identity.PlacementStableId,
                fact.LifecycleGeneration,
                RunFingerprint.Hash(
                    "enemy-source-context-v1|" + run.FrozenInputs.Fingerprint + "|"
                    + fact.Identity.RoomStableId + "|" + fact.Identity.PlacementStableId
                    + "|" + fact.Identity.EntityInstanceId + "|"
                    + fact.LifecycleGeneration.ToString(CultureInfo.InvariantCulture)));
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class UnsupportedPropSourceContextResolver :
        IPropTerminalSourceContextResolver
    {
        public bool TryResolve(
            PropTerminalFact terminalFact,
            out PropTerminalSourceContext context,
            out string diagnostic)
        {
            context = null;
            diagnostic = "prop-rewards-not-composed";
            return false;
        }
    }

    internal sealed class FrozenRunProgressionContextProvider :
        IRunRewardProgressionContextProvider
    {
        private readonly StableId characterId;
        private readonly ProgressionContext frozenProgression;

        public FrozenRunProgressionContextProvider(
            StableId characterId,
            ProgressionContext frozenProgression)
        {
            this.characterId = characterId
                ?? throw new ArgumentNullException(nameof(characterId));
            this.frozenProgression = frozenProgression
                ?? throw new ArgumentNullException(nameof(frozenProgression));
        }

        public bool TryResolve(
            RunSessionAggregate run,
            out ProgressionContext progressionContext,
            out string diagnostic)
        {
            progressionContext = null;
            if (run == null
                || run.FrozenInputs.Character.CharacterInstanceStableId != characterId
                || run.FrozenInputs.CharacterStats.Level
                    != frozenProgression.CharacterLevel
                || run.StartCommand.DifficultyStableId
                    != frozenProgression.DifficultyId)
            {
                diagnostic = "run-progression-frozen-context-mismatch";
                return false;
            }
            progressionContext = frozenProgression;
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class ProofOverlayRewardOverrideResolver :
        ITerminalRewardOverrideResolver
    {
        private readonly ITerminalRewardOverrideResolver production;
        private readonly ITerminalRewardOverrideResolver proof;

        public ProofOverlayRewardOverrideResolver(
            ITerminalRewardOverrideResolver production,
            ITerminalRewardOverrideResolver proof)
        {
            this.production = production
                ?? throw new ArgumentNullException(nameof(production));
            this.proof = proof ?? throw new ArgumentNullException(nameof(proof));
        }

        public bool TryResolve(
            LootDropSourceFact source,
            LootDropRunGenerationContext runContext,
            TerminalRewardEnvironment environment,
            TerminalRewardPlacementContext placement,
            out TerminalRewardOverrideSet overrides,
            out string diagnostic)
        {
            overrides = null;
            TerminalRewardOverrideSet productionSet;
            if (!production.TryResolve(
                    source,
                    runContext,
                    environment,
                    placement,
                    out productionSet,
                    out diagnostic)
                || productionSet == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "production-reward-overrides-unavailable"
                    : diagnostic;
                return false;
            }

            TerminalRewardOverrideSet proofSet;
            string proofDiagnostic;
            if (!proof.TryResolve(
                    source,
                    runContext,
                    environment,
                    placement,
                    out proofSet,
                    out proofDiagnostic)
                || proofSet == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(proofDiagnostic)
                    ? "proof-reward-overrides-unavailable"
                    : proofDiagnostic;
                return false;
            }

            overrides = new TerminalRewardOverrideSet(
                productionSet.GameModeOverride,
                productionSet.MissionOverride,
                productionSet.DifficultyOverride,
                productionSet.EventOverrides,
                proofSet.PlacementOverride ?? productionSet.PlacementOverride);
            diagnostic = string.Empty;
            return true;
        }
    }

    internal sealed class DeterministicProofRewardOverrideResolver :
        ITerminalRewardOverrideResolver
    {
        private readonly StableId runId;
        private readonly StableId roomId;
        private readonly StableId placementId;
        private readonly RewardProfileOverride proofOverride;

        public DeterministicProofRewardOverrideResolver(
            StableId runId,
            StableId roomId,
            StableId placementId)
        {
            this.runId = runId ?? throw new ArgumentNullException(nameof(runId));
            this.roomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            this.placementId = placementId
                ?? throw new ArgumentNullException(nameof(placementId));
            LootSourceProfile profile = LootSourceProfile.Create(
                StableId.Parse("drop-source.development-run-reward-proof"),
                StrongboxTierSelectionCatalog.LowSourceProfileId,
                new[]
                {
                    Guaranteed("cash", 0, RewardGrantKind.Money,
                        StableId.Parse("currency.money"), RewardBoxPacingMode.None),
                    Guaranteed("scrap", 1, RewardGrantKind.Scrap,
                        StableId.Parse("currency.scrap"), RewardBoxPacingMode.None),
                    Guaranteed("strongbox", 2, RewardGrantKind.Strongbox,
                        StrongboxTierSelectionCatalog.LowSourceProfileId,
                        RewardBoxPacingMode.GuaranteedBox),
                });
            proofOverride = RewardProfileOverride.Replace(
                StableId.Parse("drop-override.development-run-reward-proof"),
                profile);
        }

        public bool TryResolve(
            LootDropSourceFact source,
            LootDropRunGenerationContext runContext,
            TerminalRewardEnvironment environment,
            TerminalRewardPlacementContext placement,
            out TerminalRewardOverrideSet overrides,
            out string diagnostic)
        {
            overrides = TerminalRewardOverrideSet.Empty();
            if (source == null || runContext == null
                || environment == null || placement == null)
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
            overrides = new TerminalRewardOverrideSet(
                null,
                null,
                null,
                Array.Empty<RewardProfileOverride>(),
                proofOverride);
            diagnostic = string.Empty;
            return true;
        }

        private static RewardRollGroup Guaranteed(
            string slug,
            int ordinal,
            RewardGrantKind kind,
            StableId content,
            RewardBoxPacingMode pacing)
        {
            return RewardRollGroup.CreateGuaranteed(
                StableId.Create("drop-group", "development-proof-" + slug),
                ordinal,
                pacing,
                new[]
                {
                    RewardOutcome.CreateGrant(
                        StableId.Create("drop-outcome", "development-proof-" + slug),
                        RewardGrantSpecification.Create(
                            StableId.Create("drop-grant", "development-proof-" + slug),
                            kind,
                            content,
                            RewardQuantityRange.Create(1L, 1L),
                            Array.Empty<RewardScalingInputDescriptor>()),
                        1UL),
                });
        }
    }

    internal static class RunFingerprint
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

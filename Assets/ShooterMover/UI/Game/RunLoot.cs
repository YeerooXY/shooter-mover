using System;
using System.Collections.Generic;
using System.Globalization;
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
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.LootDropBinding;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Rewards.RunLoots;

namespace ShooterMover.UI.Game
{
    internal sealed class RunLoot
    {
        private RunLoot(
            RunSessionState authority,
            RunSessionAggregate run,
            IEnemyDropFactConsumer dropConsumer)
        {
            RunSessions = authority
                ?? throw new ArgumentNullException(nameof(authority));
            Run = run ?? throw new ArgumentNullException(nameof(run));
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

        public static RunLoot Create(
            PlayableLevelDefinition level,
            StableId gameModeId,
            CharacterLiveGraph graph,
            ShooterMover.Application.Persistence.Composition.CharacterSetupFlow coordinator,
            LevelRooms rooms,
            ShooterMover.Domain.Enemies.Catalog.EnemyCatalog enemyCatalog,
            PendingAdmissionPickupBridge pickupBridge)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (gameModeId == null) throw new ArgumentNullException(nameof(gameModeId));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (coordinator == null) throw new ArgumentNullException(nameof(coordinator));
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (enemyCatalog == null) throw new ArgumentNullException(nameof(enemyCatalog));
            if (pickupBridge == null) throw new ArgumentNullException(nameof(pickupBridge));

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
            Func<RunSessionAggregate> runResolver = delegate { return run; };
            var canonicalOverrides =
                new RunSessionTerminalRewardOverrideResolver(runResolver);
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
                        admissionConsumer: pickupBridge,
                        personalGenerationService: personalGeneration,
                        participantResolver: participantResolver,
                        environmentResolver: environmentResolver,
                        overrideResolver: canonicalOverrides,
                        deliveryOutbox: deliveryOutbox,
                        requireAcceptedPublication: true);
                return binding.EnemyConsumer;
            };

            IEnemyDropFactConsumer transactionalConsumer =
                new TransactionalRunRewardEnemyConsumer(
                    run,
                    pending,
                    pickupBridge,
                    consumerFactory);
            return new RunLoot(
                authority,
                run,
                transactionalConsumer);
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
    /// A failed attempt restores the exact run reward snapshot and compensates both the
    /// pending-generation authority and the retained physical-pickup delivery queue.
    /// A successful attempt retains its consumer so exact redelivery reuses the same ledger.
    /// </summary>
    internal sealed class TransactionalRunRewardEnemyConsumer :
        IEnemyDropFactConsumer
    {
        private readonly object gate = new object();
        private readonly RunSessionAggregate run;
        private readonly PendingLootDropAdmissionState pending;
        private readonly PendingAdmissionPickupBridge pickupBridge;
        private readonly Func<EnemyLootDropFactConsumer> consumerFactory;
        private readonly Dictionary<StableId, EnemyLootDropFactConsumer>
            committedByDeathEvent =
                new Dictionary<StableId, EnemyLootDropFactConsumer>();

        public TransactionalRunRewardEnemyConsumer(
            RunSessionAggregate run,
            PendingLootDropAdmissionState pending,
            PendingAdmissionPickupBridge pickupBridge,
            Func<EnemyLootDropFactConsumer> consumerFactory)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.pending = pending ?? throw new ArgumentNullException(nameof(pending));
            this.pickupBridge = pickupBridge
                ?? throw new ArgumentNullException(nameof(pickupBridge));
            this.consumerFactory = consumerFactory
                ?? throw new ArgumentNullException(nameof(consumerFactory));
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

                    string bridgeDiagnostic;
                    try
                    {
                        if (!pickupBridge.TryRollbackAccepted(
                                admission,
                                out bridgeDiagnostic))
                        {
                            failure = Combine(
                                failure,
                                new InvalidOperationException(
                                    "Pickup admission compensation rejected: "
                                    + bridgeDiagnostic));
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

                    string pendingDiagnostic;
                    try
                    {
                        if (!pending.TryRollbackAccepted(
                                admission,
                                out pendingDiagnostic))
                        {
                            failure = Combine(
                                failure,
                                new InvalidOperationException(
                                    "Pending reward compensation rejected: "
                                    + pendingDiagnostic));
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

using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyInstance
    {
        private int terminalConsequenceFailureCount;
        private string lastTerminalConsequenceFailure = string.Empty;

        public int TerminalConsequenceFailureCount
        {
            get { return terminalConsequenceFailureCount; }
        }

        public string LastTerminalConsequenceFailure
        {
            get { return lastTerminalConsequenceFailure; }
        }

        public EnemyMovementRealization RealizeMovement(
            EnemyPlacementDecision decision,
            EnemyMovementRealizationContext context)
        {
            IssuedDecisionRecord issued;
            EnemyLiveRejectionCode validation = ValidateDecisionCode(decision, out issued);
            if (validation != EnemyLiveRejectionCode.None)
                throw new InvalidOperationException("Enemy decision is not valid for this runtime: " + validation);
            if (!actorState.IsActive)
                throw new InvalidOperationException("Terminal enemies cannot realize movement.");
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.EntityInstanceId != Identity.EntityInstanceId)
                throw new ArgumentException("Movement context must target this enemy instance.", nameof(context));
            if (context.RoomStableId != RoomStableId)
                throw new ArgumentException("Movement context must target this enemy room.", nameof(context));

            EnemyMovementPolicyIntent intent = Movement.Policy.BuildIntent(
                issued.Decision.Evaluation,
                Movement.Configuration);
            var scaledContext = new EnemyMovementRealizationContext(
                context.EntityInstanceId,
                context.RoomStableId,
                context.CurrentPosition,
                context.CurrentFacing,
                context.SimulationTick,
                DifficultyScaling.MovementMultiplier,
                context.EnvironmentQuery);
            return Movement.Realizer.Realize(intent, scaledContext, Movement.Configuration);
        }

        [Obsolete("Pass observedTargetLifecycleGeneration explicitly.")]
        public EnemyPlayerDamagePortResult RoutePlayerImpact(
            EnemyAttackExecutionRequest execution,
            StableId hitEventStableId,
            StableId targetEntityStableId)
        {
            return RoutePlayerImpact(execution, hitEventStableId, targetEntityStableId, 1L);
        }

        public EnemyLiveDamageResult ApplyDamage(EnemyLiveDamageCommand command)
        {
            return ApplyDamage(command, 0d);
        }

        /// <summary>
        /// Applies one damage command at an authoritative run time. Health reaching zero commits the
        /// enemy death immediately. Collision shutdown, room reporting, attack cancellation, XP,
        /// drops, and kill statistics are target-owned death consequences; each is attempted once and
        /// cannot turn an already dead enemy back into a pending projectile operation.
        /// </summary>
        public EnemyLiveDamageResult ApplyDamage(
            EnemyLiveDamageCommand command,
            double occurredAtSeconds)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (double.IsNaN(occurredAtSeconds)
                || double.IsInfinity(occurredAtSeconds)
                || occurredAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(occurredAtSeconds));

            string signature = DamageSignature(command, occurredAtSeconds);
            DamageReplayRecord replay;
            if (damageReplay.TryGetValue(command.OperationStableId, out replay))
            {
                if (!string.Equals(replay.Signature, signature, StringComparison.Ordinal))
                {
                    return new EnemyLiveDamageResult(
                        EnemyLiveOperationStatus.Rejected,
                        EnemyLiveRejectionCode.ConflictingDuplicate,
                        Runtime,
                        publishedDeath);
                }
                return new EnemyLiveDamageResult(
                    EnemyLiveOperationStatus.ExactReplay,
                    replay.Result.Rejection,
                    Runtime,
                    replay.Result.DeathFact);
            }

            EnemyLiveDamageResult result;
            if (command.TargetEntityStableId != Identity.EntityInstanceId)
            {
                result = RejectedDamage(EnemyLiveRejectionCode.EntityMismatch);
            }
            else if (command.TargetLifecycleGeneration != LifecycleGeneration)
            {
                result = RejectedDamage(EnemyLiveRejectionCode.StaleLifecycle);
            }
            else if (!actorState.IsActive)
            {
                result = RejectedDamage(EnemyLiveRejectionCode.ActorTerminal);
            }
            else
            {
                EnemyActorStepResult stepped = EnemyActorStepper.Step(
                    actorState,
                    new[]
                    {
                        EnemyActorCommand.Damage(
                            command.Order,
                            command.OperationStableId,
                            command.SourceEntityStableId,
                            command.ChannelValue,
                            command.Amount),
                    });
                actorState = stepped.State;
                EnemyDestroyedNotification destroyed = FindDestroyed(stepped.Notifications);
                if (destroyed == null)
                {
                    result = new EnemyLiveDamageResult(
                        EnemyLiveOperationStatus.Applied,
                        EnemyLiveRejectionCode.None,
                        Runtime,
                        null);
                }
                else
                {
                    CreateDeathFactOnce(command, destroyed);
                    result = CompleteTerminalConsequences(occurredAtSeconds);
                }
            }

            if (result.Status != EnemyLiveOperationStatus.Rejected)
            {
                damageReplay.Add(
                    command.OperationStableId,
                    new DamageReplayRecord(signature, result));
            }
            return result;
        }

        public ReportRoomOccupantTerminalCommand BuildTerminalCommand(StableId operationStableId)
        {
            return new ReportRoomOccupantTerminalCommand(
                Identity.RoomRuntimeInstanceStableId,
                operationStableId,
                Request.RoomLifecycleGeneration,
                RoomStableId,
                SpawnStableId);
        }

        private EnemyLiveDamageResult CompleteTerminalConsequences(
            double occurredAtSeconds)
        {
            if (publishedDeath == null)
            {
                throw new InvalidOperationException(
                    "A terminal enemy requires one canonical death fact.");
            }

            StableId cancellationOperation = StableId.Create(
                "enemy-pattern-operation",
                "terminal-" + DeterministicEnemyLiveIdentityDeriver.Hash64(
                    Identity.EntityInstanceId
                    + "|"
                    + LifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + publishedDeath.DeathEventStableId));
            try
            {
                var cancellationCommand = new EnemyAttackLifecycleCancellationCommand(
                    cancellationOperation,
                    Identity.EntityInstanceId,
                    LifecycleGeneration,
                    occurredAtSeconds);
                EnemyAttackPatternCancellationResult cancellation =
                    CancelAttackPatterns(cancellationCommand);
                if (cancellation == null || !cancellation.IsAccepted)
                {
                    RecordTerminalConsequenceFailure(
                        "enemy-terminal-attack-cancellation-rejected",
                        null);
                }
            }
            catch (Exception exception)
            {
                if (IsFatalException(exception)) throw;
                RecordTerminalConsequenceFailure(
                    "enemy-terminal-attack-cancellation-failed",
                    exception);
            }

            AttemptTerminalConsequence(
                "enemy-terminal-collision-failed",
                () => downstream.TerminalCollision.SetTerminal(
                    new EnemyTerminalCollisionFact(
                        Identity.EntityInstanceId,
                        publishedDeath.DeathEventStableId,
                        LifecycleGeneration)));

            StableId roomOperation = StableId.Create(
                "room-operation",
                "enemy-terminal-" + DeterministicEnemyLiveIdentityDeriver.Hash64(
                    Identity.EntityInstanceId + "|" + publishedDeath.DeathEventStableId));
            AttemptTerminalConsequence(
                "enemy-terminal-room-report-failed",
                () => downstream.RoomTerminal.Report(
                    BuildTerminalCommand(roomOperation),
                    publishedDeath));
            AttemptTerminalConsequence(
                "enemy-terminal-experience-failed",
                () => downstream.Experience.Consume(publishedDeath));
            AttemptTerminalConsequence(
                "enemy-terminal-drop-failed",
                () => downstream.Drops.Consume(publishedDeath));
            AttemptTerminalConsequence(
                "enemy-terminal-kill-stat-failed",
                () => downstream.KillStats.Consume(publishedDeath));

            return new EnemyLiveDamageResult(
                EnemyLiveOperationStatus.Applied,
                EnemyLiveRejectionCode.None,
                Runtime,
                publishedDeath);
        }

        private void AttemptTerminalConsequence(string code, Action consequence)
        {
            if (consequence == null) throw new ArgumentNullException(nameof(consequence));
            try
            {
                consequence();
            }
            catch (Exception exception)
            {
                if (IsFatalException(exception)) throw;
                RecordTerminalConsequenceFailure(code, exception);
            }
        }

        private void RecordTerminalConsequenceFailure(string code, Exception exception)
        {
            terminalConsequenceFailureCount++;
            lastTerminalConsequenceFailure = code
                + (exception == null
                    ? string.Empty
                    : ":" + exception.GetType().Name + ":" + exception.Message);
        }

        private EnemyLiveRejectionCode ValidateDecisionCode(
            EnemyPlacementDecision decision,
            out IssuedDecisionRecord issued)
        {
            issued = null;
            if (decision == null) return EnemyLiveRejectionCode.InvalidCommand;
            if (decision.EntityInstanceId != Identity.EntityInstanceId)
                return EnemyLiveRejectionCode.EntityMismatch;
            if (decision.LifecycleGeneration != LifecycleGeneration)
                return EnemyLiveRejectionCode.StaleLifecycle;
            string fingerprint = EnemyLiveStateFingerprint.Decision(decision);
            if (!issuedDecisions.TryGetValue(fingerprint, out issued))
                return EnemyLiveRejectionCode.DecisionNotIssued;
            return EnemyLiveRejectionCode.None;
        }

        private StableId ResolveAttackItemInstance(StableId attackStableId)
        {
            if (ItemInstanceStableId != null) return ItemInstanceStableId;
            return StableId.Create(
                "equipment-instance",
                "enemy-" + DeterministicEnemyLiveIdentityDeriver.Hash64(
                    Identity.EntityInstanceId + "|" + attackStableId));
        }

        private EnemyDeathFact CreateDeathFactOnce(
            EnemyLiveDamageCommand command,
            EnemyDestroyedNotification destroyed)
        {
            if (publishedDeath != null) return publishedDeath;
            publishedDeath = new EnemyDeathFact(
                destroyed.EventId,
                command.OperationStableId,
                Identity,
                Definition.DefinitionId,
                Level,
                LifecycleGeneration,
                command.SourceEntityStableId,
                command.SourceRunParticipantStableId,
                Definition.ExperienceProfileId,
                Definition.DropProfileId,
                destroyed.DeathCause);
            return publishedDeath;
        }

        private static string DamageSignature(
            EnemyLiveDamageCommand command,
            double occurredAtSeconds)
        {
            return command.SourceEntityStableId
                + "|" + command.SourceRunParticipantStableId
                + "|" + command.TargetEntityStableId
                + "|" + command.TargetLifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + command.Order.ToString(CultureInfo.InvariantCulture)
                + "|" + command.ChannelValue.ToString(CultureInfo.InvariantCulture)
                + "|" + command.Amount.ToString("R", CultureInfo.InvariantCulture)
                + "|" + occurredAtSeconds.ToString("R", CultureInfo.InvariantCulture);
        }

        private static EnemyDestroyedNotification FindDestroyed(
            IReadOnlyList<EnemyActorNotification> notifications)
        {
            for (int index = 0; index < notifications.Count; index++)
            {
                EnemyDestroyedNotification destroyed = notifications[index] as EnemyDestroyedNotification;
                if (destroyed != null) return destroyed;
            }
            return null;
        }

        private static bool IsFatalException(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private EnemyAttackExecutionResult RejectedAttack(EnemyLiveRejectionCode rejection)
        {
            return new EnemyAttackExecutionResult(
                EnemyLiveOperationStatus.Rejected,
                rejection,
                null);
        }

        private EnemyLiveDamageResult RejectedDamage(EnemyLiveRejectionCode rejection)
        {
            return new EnemyLiveDamageResult(
                EnemyLiveOperationStatus.Rejected,
                rejection,
                Runtime,
                publishedDeath);
        }

        private static EnemyPlayerDamagePortResult RejectedPlayerImpact(
            EnemyLiveRejectionCode rejection)
        {
            return new EnemyPlayerDamagePortResult(
                EnemyLiveOperationStatus.Rejected,
                rejection);
        }
    }
}

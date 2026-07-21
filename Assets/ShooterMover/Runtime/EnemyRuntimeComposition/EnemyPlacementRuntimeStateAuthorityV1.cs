using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed partial class EnemyPlacementRuntimeInstanceV1
    {
        private StableId pendingTerminalDamageOperationStableId;
        private string pendingTerminalDamageSignature;
        private double pendingTerminalOccurredAtSeconds;

        public EnemyMovementRealizationV1 RealizeMovement(
            EnemyPlacementDecisionV1 decision,
            EnemyMovementRealizationContextV1 context)
        {
            IssuedDecisionRecord issued;
            EnemyRuntimeRejectionCodeV1 validation = ValidateDecisionCode(decision, out issued);
            if (validation != EnemyRuntimeRejectionCodeV1.None)
                throw new InvalidOperationException("Enemy decision is not valid for this runtime: " + validation);
            if (!actorState.IsActive)
                throw new InvalidOperationException("Terminal enemies cannot realize movement.");
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.EntityInstanceId != Identity.EntityInstanceId)
                throw new ArgumentException("Movement context must target this enemy instance.", nameof(context));
            if (context.RoomStableId != RoomStableId)
                throw new ArgumentException("Movement context must target this enemy room.", nameof(context));

            EnemyMovementPolicyIntentV1 intent = Movement.Policy.BuildIntent(
                issued.Decision.Evaluation,
                Movement.Configuration);
            var scaledContext = new EnemyMovementRealizationContextV1(
                context.EntityInstanceId,
                context.RoomStableId,
                context.CurrentPosition,
                context.CurrentFacing,
                context.SimulationTick,
                DifficultyScaling.MovementMultiplier,
                context.EnvironmentQuery);
            return Movement.Realizer.Realize(intent, scaledContext, Movement.Configuration);
        }

        // Compatibility bridge for pre-lifecycle call sites. New production adapters must always pass
        // the observed target lifecycle explicitly through the four-argument overload.
        [Obsolete("Pass observedTargetLifecycleGeneration explicitly.")]
        public EnemyPlayerDamagePortResultV1 RoutePlayerImpact(
            EnemyAttackExecutionRequestV1 execution,
            StableId hitEventStableId,
            StableId targetEntityStableId)
        {
            return RoutePlayerImpact(execution, hitEventStableId, targetEntityStableId, 1L);
        }

        public EnemyRuntimeDamageResultV1 ApplyDamage(EnemyRuntimeDamageCommandV1 command)
        {
            return ApplyDamage(command, 0d);
        }

        /// <summary>
        /// Applies damage at an authoritative run time. The timestamp is used only to determine
        /// which already-dispatched attack emissions remain live when this damage terminalizes
        /// the enemy. Existing callers use the compatibility overload and conservatively cancel
        /// every not-yet-processed scheduled emission from time zero.
        /// </summary>
        public EnemyRuntimeDamageResultV1 ApplyDamage(
            EnemyRuntimeDamageCommandV1 command,
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
                    return new EnemyRuntimeDamageResultV1(
                        EnemyRuntimeOperationStatusV1.Rejected,
                        EnemyRuntimeRejectionCodeV1.ConflictingDuplicate,
                        Runtime,
                        publishedDeath);
                }
                return new EnemyRuntimeDamageResultV1(
                    EnemyRuntimeOperationStatusV1.ExactReplay,
                    replay.Result.Rejection,
                    Runtime,
                    replay.Result.DeathFact);
            }

            if (pendingTerminalDamageOperationStableId != null)
            {
                if (command.OperationStableId != pendingTerminalDamageOperationStableId)
                    return RejectedDamage(EnemyRuntimeRejectionCodeV1.ActorTerminal);
                if (!string.Equals(
                    signature,
                    pendingTerminalDamageSignature,
                    StringComparison.Ordinal))
                {
                    return RejectedDamage(
                        EnemyRuntimeRejectionCodeV1.ConflictingDuplicate);
                }

                EnemyRuntimeDamageResultV1 pending = CompletePendingTerminalTransition();
                if (pending.Status == EnemyRuntimeOperationStatusV1.Applied)
                {
                    damageReplay.Add(
                        command.OperationStableId,
                        new DamageReplayRecord(signature, pending));
                    ClearPendingTerminalTransition();
                }
                return pending;
            }

            EnemyRuntimeDamageResultV1 result;
            if (command.TargetEntityStableId != Identity.EntityInstanceId)
            {
                result = RejectedDamage(EnemyRuntimeRejectionCodeV1.EntityMismatch);
            }
            else if (command.TargetLifecycleGeneration != LifecycleGeneration)
            {
                result = RejectedDamage(EnemyRuntimeRejectionCodeV1.StaleLifecycle);
            }
            else if (!actorState.IsActive)
            {
                result = RejectedDamage(EnemyRuntimeRejectionCodeV1.ActorTerminal);
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
                    result = new EnemyRuntimeDamageResultV1(
                        EnemyRuntimeOperationStatusV1.Applied,
                        EnemyRuntimeRejectionCodeV1.None,
                        Runtime,
                        null);
                }
                else
                {
                    CreateDeathFactOnce(command, destroyed);
                    pendingTerminalDamageOperationStableId = command.OperationStableId;
                    pendingTerminalDamageSignature = signature;
                    pendingTerminalOccurredAtSeconds = occurredAtSeconds;
                    result = CompletePendingTerminalTransition();
                    if (result.Status == EnemyRuntimeOperationStatusV1.Applied)
                        ClearPendingTerminalTransition();
                }
            }

            if (result.Status != EnemyRuntimeOperationStatusV1.Rejected)
            {
                damageReplay.Add(
                    command.OperationStableId,
                    new DamageReplayRecord(signature, result));
            }
            return result;
        }

        public ReportRoomOccupantTerminalCommandV1 BuildTerminalCommand(StableId operationStableId)
        {
            return new ReportRoomOccupantTerminalCommandV1(
                Identity.RoomRuntimeInstanceStableId,
                operationStableId,
                Request.RoomLifecycleGeneration,
                RoomStableId,
                SpawnStableId);
        }

        private EnemyRuntimeDamageResultV1 CompletePendingTerminalTransition()
        {
            if (publishedDeath == null
                || pendingTerminalDamageOperationStableId == null
                || string.IsNullOrWhiteSpace(pendingTerminalDamageSignature))
            {
                throw new InvalidOperationException(
                    "A pending terminal transition requires one canonical death fact.");
            }

            StableId cancellationOperation = StableId.Create(
                "enemy-pattern-operation",
                "terminal-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    Identity.EntityInstanceId
                    + "|"
                    + LifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + publishedDeath.DeathEventStableId));
            var cancellationCommand = new EnemyAttackLifecycleCancellationCommandV1(
                cancellationOperation,
                Identity.EntityInstanceId,
                LifecycleGeneration,
                pendingTerminalOccurredAtSeconds);
            EnemyAttackPatternCancellationResultV1 cancellation =
                CancelAttackPatterns(cancellationCommand);
            if (!cancellation.IsAccepted)
                return RejectedDamage(EnemyRuntimeRejectionCodeV1.InvalidCommand);

            downstream.TerminalCollision.SetTerminal(
                new EnemyTerminalCollisionFactV1(
                    Identity.EntityInstanceId,
                    publishedDeath.DeathEventStableId,
                    LifecycleGeneration));
            StableId roomOperation = StableId.Create(
                "room-operation",
                "enemy-terminal-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    Identity.EntityInstanceId + "|" + publishedDeath.DeathEventStableId));
            downstream.RoomTerminal.Report(
                BuildTerminalCommand(roomOperation),
                publishedDeath);
            downstream.Experience.Consume(publishedDeath);
            downstream.Drops.Consume(publishedDeath);
            downstream.KillStats.Consume(publishedDeath);
            return new EnemyRuntimeDamageResultV1(
                EnemyRuntimeOperationStatusV1.Applied,
                EnemyRuntimeRejectionCodeV1.None,
                Runtime,
                publishedDeath);
        }

        private void ClearPendingTerminalTransition()
        {
            pendingTerminalDamageOperationStableId = null;
            pendingTerminalDamageSignature = null;
            pendingTerminalOccurredAtSeconds = 0d;
        }

        private EnemyRuntimeRejectionCodeV1 ValidateDecisionCode(
            EnemyPlacementDecisionV1 decision,
            out IssuedDecisionRecord issued)
        {
            issued = null;
            if (decision == null) return EnemyRuntimeRejectionCodeV1.InvalidCommand;
            if (decision.EntityInstanceId != Identity.EntityInstanceId)
                return EnemyRuntimeRejectionCodeV1.EntityMismatch;
            if (decision.LifecycleGeneration != LifecycleGeneration)
                return EnemyRuntimeRejectionCodeV1.StaleLifecycle;
            string fingerprint = EnemyRuntimeAuthorityFingerprintV1.Decision(decision);
            if (!issuedDecisions.TryGetValue(fingerprint, out issued))
                return EnemyRuntimeRejectionCodeV1.DecisionNotIssued;
            return EnemyRuntimeRejectionCodeV1.None;
        }

        private StableId ResolveAttackItemInstance(StableId attackStableId)
        {
            if (ItemInstanceStableId != null) return ItemInstanceStableId;
            return StableId.Create(
                "equipment-instance",
                "enemy-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    Identity.EntityInstanceId + "|" + attackStableId));
        }

        private EnemyDeathFactV1 CreateDeathFactOnce(
            EnemyRuntimeDamageCommandV1 command,
            EnemyDestroyedNotification destroyed)
        {
            if (publishedDeath != null) return publishedDeath;
            publishedDeath = new EnemyDeathFactV1(
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
            EnemyRuntimeDamageCommandV1 command,
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

        private EnemyAttackExecutionResultV1 RejectedAttack(EnemyRuntimeRejectionCodeV1 rejection)
        {
            return new EnemyAttackExecutionResultV1(
                EnemyRuntimeOperationStatusV1.Rejected,
                rejection,
                null);
        }

        private EnemyRuntimeDamageResultV1 RejectedDamage(EnemyRuntimeRejectionCodeV1 rejection)
        {
            return new EnemyRuntimeDamageResultV1(
                EnemyRuntimeOperationStatusV1.Rejected,
                rejection,
                Runtime,
                publishedDeath);
        }

        private static EnemyPlayerDamagePortResultV1 RejectedPlayerImpact(
            EnemyRuntimeRejectionCodeV1 rejection)
        {
            return new EnemyPlayerDamagePortResultV1(
                EnemyRuntimeOperationStatusV1.Rejected,
                rejection);
        }
    }
}

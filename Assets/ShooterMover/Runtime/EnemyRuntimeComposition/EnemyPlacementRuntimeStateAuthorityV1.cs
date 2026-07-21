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
            if (command == null) throw new ArgumentNullException(nameof(command));
            string signature = DamageSignature(command);
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
                EnemyDeathFactV1 death = destroyed == null ? null : PublishDeathOnce(command, destroyed);
                result = new EnemyRuntimeDamageResultV1(
                    EnemyRuntimeOperationStatusV1.Applied,
                    EnemyRuntimeRejectionCodeV1.None,
                    Runtime,
                    death);
            }

            damageReplay.Add(command.OperationStableId, new DamageReplayRecord(signature, result));
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

        private EnemyDeathFactV1 PublishDeathOnce(
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

            downstream.TerminalCollision.SetTerminal(
                new EnemyTerminalCollisionFactV1(
                    Identity.EntityInstanceId,
                    destroyed.EventId,
                    LifecycleGeneration));
            StableId roomOperation = StableId.Create(
                "room-operation",
                "enemy-terminal-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    Identity.EntityInstanceId + "|" + destroyed.EventId));
            downstream.RoomTerminal.Report(BuildTerminalCommand(roomOperation), publishedDeath);
            downstream.Experience.Consume(publishedDeath);
            downstream.Drops.Consume(publishedDeath);
            downstream.KillStats.Consume(publishedDeath);
            return publishedDeath;
        }

        private static string DamageSignature(EnemyRuntimeDamageCommandV1 command)
        {
            return command.SourceEntityStableId
                + "|" + command.SourceRunParticipantStableId
                + "|" + command.TargetEntityStableId
                + "|" + command.TargetLifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                + "|" + command.Order.ToString(CultureInfo.InvariantCulture)
                + "|" + command.ChannelValue.ToString(CultureInfo.InvariantCulture)
                + "|" + command.Amount.ToString("R", CultureInfo.InvariantCulture);
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

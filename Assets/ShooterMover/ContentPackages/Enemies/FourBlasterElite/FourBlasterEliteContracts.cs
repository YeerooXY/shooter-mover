using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.ContentPackages.Enemies.FourBlasterElite
{
    /// <summary>
    /// Explicit package boundary for the accepted Combat Messages v1 and
    /// Encounter Lifecycle v1 contracts. It adapts contract envelopes into the
    /// package-owned session without taking encounter, reward, or registry authority.
    /// </summary>
    public static class FourBlasterEliteContracts
    {
        public static EncounterParticipantEntry CreateEncounterParticipantEntry(
            StableId entryId,
            StableId actorId,
            int order)
        {
            return FourBlasterElitePackage.CreateDescriptor()
                .CreateEncounterParticipantEntry(entryId, actorId, order);
        }

        public static EnemyActorStepResult ApplyHit(
            FourBlasterEliteSession session,
            HitMessage message,
            double amount,
            long order)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            EnemyActorState state = session.ActorState;
            if (state == null || message.TargetId != state.ActorId)
            {
                throw new ArgumentException(
                    "The combat message target must match the Four-Blaster Elite actor.",
                    nameof(message));
            }

            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Confirmed hit damage must be finite and positive.");
            }

            if (order < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            if (message.Result != HitResult.Confirmed)
            {
                return EnemyActorStepper.Step(
                    state,
                    new EnemyActorCommand[0]);
            }

            return session.ApplyDamage(
                message.EventId,
                message.SourceId,
                message.Channel,
                amount,
                order);
        }
    }
}

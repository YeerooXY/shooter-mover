using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies
{
    public sealed class EnemyActorStepResult
    {
        internal EnemyActorStepResult(
            EnemyActorState state,
            IList<EnemyActorNotification> notifications)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (notifications == null)
            {
                throw new ArgumentNullException(nameof(notifications));
            }

            State = state;
            Notifications = new ReadOnlyCollection<EnemyActorNotification>(
                new List<EnemyActorNotification>(notifications));
        }

        public EnemyActorState State { get; }

        public IReadOnlyList<EnemyActorNotification> Notifications { get; }
    }

    /// <summary>
    /// Pure deterministic transition function for one enemy actor.
    /// </summary>
    public static class EnemyActorStepper
    {
        public const int DamageAppliedResultValue = 1;
        public const int DamageBlockedResultValue = 2;
        public const int DamageDuplicateEventIgnoredResultValue = 3;
        public const int DamageTargetAlreadyDestroyedResultValue = 4;

        public const int VitalDestroyedResultValue = 2;

        public static EnemyActorStepResult Step(
            EnemyActorState state,
            IEnumerable<EnemyActorCommand> commands)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            List<EnemyActorCommand> ordered = new List<EnemyActorCommand>();
            foreach (EnemyActorCommand command in commands)
            {
                if (command == null)
                {
                    throw new ArgumentException(
                        "Enemy actor commands cannot contain null.",
                        nameof(commands));
                }

                ordered.Add(command);
            }

            ordered.Sort(CompareCommands);
            EnemyActorState current = state;
            List<EnemyActorNotification> notifications =
                new List<EnemyActorNotification>();

            foreach (EnemyActorCommand command in ordered)
            {
                if (command.Kind == EnemyActorCommandKind.Damage)
                {
                    current = ApplyDamage(current, command, notifications);
                }
                else if (command.Kind == EnemyActorCommandKind.Contact)
                {
                    current = ApplyContact(current, command, notifications);
                }
                else
                {
                    current = ApplyDespawn(current, command, notifications);
                }
            }

            return new EnemyActorStepResult(current, notifications);
        }

        private static EnemyActorState ApplyDamage(
            EnemyActorState state,
            EnemyActorCommand command,
            IList<EnemyActorNotification> notifications)
        {
            if (state.HasProcessed(command.EventId))
            {
                notifications.Add(
                    CreateDamageNotification(
                        state,
                        command,
                        DamageDuplicateEventIgnoredResultValue,
                        state.Health,
                        state.Health,
                        0d,
                        command.Amount));
                return state;
            }

            if (state.IsDestroyed)
            {
                notifications.Add(
                    CreateDamageNotification(
                        state,
                        command,
                        DamageTargetAlreadyDestroyedResultValue,
                        state.Health,
                        state.Health,
                        0d,
                        command.Amount));
                return state.Next(
                    state.Health,
                    state.ContactPolicy,
                    state.LifecyclePhase,
                    state.DeathCause,
                    state.DestroyedVitalEmitted,
                    state.EncounterResolutionEmitted,
                    command.EventId);
            }

            double before = state.Health;
            double applied = Math.Min(before, command.Amount);
            double after = before - applied;
            double unapplied = command.Amount - applied;

            notifications.Add(
                CreateDamageNotification(
                    state,
                    command,
                    DamageAppliedResultValue,
                    before,
                    after,
                    applied,
                    unapplied));

            if (after > 0d)
            {
                return state.Next(
                    after,
                    state.ContactPolicy,
                    EnemyActorLifecyclePhase.Active,
                    EnemyActorDeathCause.None,
                    false,
                    false,
                    command.EventId);
            }

            EnemyDestroyedNotification vital = CreateDestroyedNotification(
                state,
                command.EventId,
                command.OtherActorId,
                command.ChannelValue,
                EnemyActorDeathCause.IncomingDamage);
            notifications.Add(vital);
            notifications.Add(new EnemyEncounterResolutionNotification(vital));

            return state.Next(
                0d,
                state.ContactPolicy,
                EnemyActorLifecyclePhase.Destroyed,
                EnemyActorDeathCause.IncomingDamage,
                true,
                true,
                command.EventId);
        }

        private static EnemyActorState ApplyContact(
            EnemyActorState state,
            EnemyActorCommand command,
            IList<EnemyActorNotification> notifications)
        {
            int weightResult = EnemyContactPolicy.DetermineWeightResult(
                command.MoverWeightClassValue,
                state.WeightClassValue);

            if (state.HasProcessed(command.EventId))
            {
                notifications.Add(
                    new EnemyContactNotification(
                        command.EventId,
                        command.OtherActorId,
                        state.ActorId,
                        command.ContactClassificationValue,
                        EnemyContactPolicy.ContactDuplicateEventIgnoredResultValue,
                        weightResult,
                        EnemyContactDecision.DuplicateWithinSimultaneousWindow,
                        state.ContactPolicy.Mode,
                        false,
                        0d));
                return state;
            }

            if (state.IsDestroyed)
            {
                notifications.Add(
                    new EnemyContactNotification(
                        command.EventId,
                        command.OtherActorId,
                        state.ActorId,
                        command.ContactClassificationValue,
                        EnemyContactPolicy.ContactTargetAlreadyDestroyedResultValue,
                        weightResult,
                        EnemyContactDecision.ActorAlreadyDestroyed,
                        state.ContactPolicy.Mode,
                        false,
                        0d));
                return state.Next(
                    state.Health,
                    state.ContactPolicy,
                    state.LifecyclePhase,
                    state.DeathCause,
                    state.DestroyedVitalEmitted,
                    state.EncounterResolutionEmitted,
                    command.EventId);
            }

            EnemyContactResolution contact;
            EnemyContactPolicy nextPolicy = state.ContactPolicy.Register(
                command.OtherActorId,
                command.ObservedAtSeconds,
                command.MoverWeightClassValue,
                state.WeightClassValue,
                out contact);

            notifications.Add(
                new EnemyContactNotification(
                    command.EventId,
                    command.OtherActorId,
                    state.ActorId,
                    command.ContactClassificationValue,
                    contact.ContractResultValue,
                    contact.WeightResultValue,
                    contact.Decision,
                    state.ContactPolicy.Mode,
                    contact.RequestsMoverDamage,
                    contact.MoverDamageAmount));

            bool disposableDeath =
                contact.Decision == EnemyContactDecision.Accepted
                && state.ContactPolicy.Mode == EnemyContactMode.DisposableImpact;

            if (!disposableDeath)
            {
                return state.Next(
                    state.Health,
                    nextPolicy,
                    EnemyActorLifecyclePhase.Active,
                    EnemyActorDeathCause.None,
                    false,
                    false,
                    command.EventId);
            }

            EnemyDestroyedNotification vital = CreateDestroyedNotification(
                state,
                command.EventId,
                command.OtherActorId,
                EnemyContactPolicy.ContactChannelValue,
                EnemyActorDeathCause.DisposableImpact);
            notifications.Add(vital);
            notifications.Add(new EnemyEncounterResolutionNotification(vital));

            return state.Next(
                0d,
                nextPolicy,
                EnemyActorLifecyclePhase.Destroyed,
                EnemyActorDeathCause.DisposableImpact,
                true,
                true,
                command.EventId);
        }

        private static EnemyActorState ApplyDespawn(
            EnemyActorState state,
            EnemyActorCommand command,
            IList<EnemyActorNotification> notifications)
        {
            if (state.HasProcessed(command.EventId))
            {
                return state;
            }

            if (!state.IsDestroyed || state.IsDespawned)
            {
                return state.Next(
                    state.Health,
                    state.ContactPolicy,
                    state.LifecyclePhase,
                    state.DeathCause,
                    state.DestroyedVitalEmitted,
                    state.EncounterResolutionEmitted,
                    command.EventId);
            }

            notifications.Add(
                new EnemyDespawnedNotification(
                    command.EventId,
                    command.OtherActorId,
                    state.ActorId));

            return state.Next(
                0d,
                state.ContactPolicy,
                EnemyActorLifecyclePhase.Despawned,
                state.DeathCause,
                true,
                true,
                command.EventId);
        }

        private static EnemyDamageNotification CreateDamageNotification(
            EnemyActorState state,
            EnemyActorCommand command,
            int resultValue,
            double beforeHealth,
            double afterHealth,
            double applied,
            double unapplied)
        {
            return new EnemyDamageNotification(
                command.EventId,
                command.OtherActorId,
                state.ActorId,
                command.ChannelValue,
                command.Amount,
                resultValue,
                beforeHealth,
                afterHealth,
                state.MaximumHealth,
                applied,
                unapplied);
        }

        private static EnemyDestroyedNotification CreateDestroyedNotification(
            EnemyActorState state,
            StableId triggeringEventId,
            StableId sourceId,
            int channelValue,
            EnemyActorDeathCause cause)
        {
            return new EnemyDestroyedNotification(
                CreateVitalEventId(state.ActorId, triggeringEventId),
                sourceId,
                state.ActorId,
                channelValue,
                state.MaximumHealth,
                cause);
        }

        private static StableId CreateVitalEventId(
            StableId actorId,
            StableId triggeringEventId)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            string text = actorId + "|" + triggeringEventId + "|enemy-destroyed";
            ulong hash = offsetBasis;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }

            return StableId.Create(
                "event",
                "enemy-vital-" + hash.ToString("x16", CultureInfo.InvariantCulture));
        }

        private static int CompareCommands(
            EnemyActorCommand left,
            EnemyActorCommand right)
        {
            int orderComparison = left.Order.CompareTo(right.Order);
            if (orderComparison != 0)
            {
                return orderComparison;
            }

            int eventComparison = left.EventId.CompareTo(right.EventId);
            if (eventComparison != 0)
            {
                return eventComparison;
            }

            return ((int)left.Kind).CompareTo((int)right.Kind);
        }
    }
}

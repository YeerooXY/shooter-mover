using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Combat
{
    /// <summary>
    /// Common immutable envelope fields carried by every combat event message.
    /// </summary>
    public interface ICombatEventMessage
    {
        StableId EventId { get; }

        StableId SourceId { get; }

        StableId TargetId { get; }

        CombatChannel Channel { get; }
    }

    public enum CombatChannel
    {
        Kinetic = 1,
        Thermal = 2,
        Electrical = 3,
        Explosive = 4,
        Contact = 5,
        Environmental = 6,
        System = 7,
    }

    public enum DamageResult
    {
        Applied = 1,
        Blocked = 2,
        DuplicateEventIgnored = 3,
        TargetAlreadyDestroyed = 4,
    }

    public enum HitResult
    {
        Confirmed = 1,
        Blocked = 2,
        Missed = 3,
        DuplicateEventIgnored = 4,
        TargetAlreadyDestroyed = 5,
    }

    public enum VitalResult
    {
        Active = 1,
        Destroyed = 2,
    }

    public enum ContactClassification
    {
        BodyImpact = 1,
        SustainedBodyContact = 2,
        ProjectileImpact = 3,
        AreaOverlap = 4,
        HazardOverlap = 5,
    }

    public enum ContactResult
    {
        Accepted = 1,
        GracePeriodIgnored = 2,
        BlockedByWeight = 3,
        DuplicateEventIgnored = 4,
        TargetAlreadyDestroyed = 5,
    }

    public enum CombatWeightClass
    {
        Light = 1,
        Standard = 2,
        Heavy = 3,
        Immovable = 4,
    }

    public enum WeightResult
    {
        SourceLighter = 1,
        Equal = 2,
        SourceHeavier = 3,
        TargetImmovable = 4,
    }

    public enum CombatStatus
    {
        Stunned = 1,
        Slowed = 2,
        Burning = 3,
        Marked = 4,
    }

    public enum StatusResult
    {
        Applied = 1,
        Refreshed = 2,
        Removed = 3,
        Resisted = 4,
        DuplicateEventIgnored = 5,
        TargetAlreadyDestroyed = 6,
    }

    public enum CombatEventIdentityResult
    {
        Distinct = 1,
        Duplicate = 2,
        ConflictingDuplicate = 3,
    }

    /// <summary>
    /// Validated health and shield snapshot. Destruction is derived from zero health.
    /// </summary>
    public sealed class VitalState : IEquatable<VitalState>
    {
        public VitalState(
            double health,
            double maximumHealth,
            double shield,
            double maximumShield)
        {
            CombatMessageValidation.RequireFiniteNonNegative(health, nameof(health));
            CombatMessageValidation.RequireFinitePositive(maximumHealth, nameof(maximumHealth));
            CombatMessageValidation.RequireFiniteNonNegative(shield, nameof(shield));
            CombatMessageValidation.RequireFiniteNonNegative(maximumShield, nameof(maximumShield));

            if (health > maximumHealth)
            {
                throw new ArgumentException("Health cannot exceed maximum health.", nameof(health));
            }

            if (shield > maximumShield)
            {
                throw new ArgumentException("Shield cannot exceed maximum shield.", nameof(shield));
            }

            if (health == 0d && shield != 0d)
            {
                throw new ArgumentException(
                    "A destroyed vital state cannot retain shield in Combat Messages v1.",
                    nameof(shield));
            }

            Health = health;
            MaximumHealth = maximumHealth;
            Shield = shield;
            MaximumShield = maximumShield;
        }

        public double Health { get; }

        public double MaximumHealth { get; }

        public double Shield { get; }

        public double MaximumShield { get; }

        public bool IsDestroyed => Health == 0d;

        public bool Equals(VitalState other)
        {
            return !ReferenceEquals(other, null)
                && Health == other.Health
                && MaximumHealth == other.MaximumHealth
                && Shield == other.Shield
                && MaximumShield == other.MaximumShield;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as VitalState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Health.GetHashCode();
                hash = (hash * 31) + MaximumHealth.GetHashCode();
                hash = (hash * 31) + Shield.GetHashCode();
                hash = (hash * 31) + MaximumShield.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Immutable proposed-damage result. It validates a supplied result but never mutates vitals.
    /// </summary>
    public sealed class DamageMessage : ICombatEventMessage
    {
        public DamageMessage(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            CombatChannel channel,
            double requestedAmount,
            DamageResult result,
            VitalState before,
            VitalState after,
            double shieldDamageApplied,
            double shieldOverflowAmount,
            double healthDamageApplied,
            double unappliedAmount)
        {
            CombatMessageValidation.RequireEnvelope(eventId, sourceId, targetId, channel);
            if (channel == CombatChannel.System)
            {
                throw new ArgumentException(
                    "System is not a damage-bearing combat channel.",
                    nameof(channel));
            }

            CombatMessageValidation.RequireFinitePositive(requestedAmount, nameof(requestedAmount));
            CombatMessageValidation.RequireDefined(typeof(DamageResult), result, nameof(result));
            CombatMessageValidation.RequireNotNull(before, nameof(before));
            CombatMessageValidation.RequireNotNull(after, nameof(after));
            CombatMessageValidation.RequireFiniteNonNegative(
                shieldDamageApplied,
                nameof(shieldDamageApplied));
            CombatMessageValidation.RequireFiniteNonNegative(
                shieldOverflowAmount,
                nameof(shieldOverflowAmount));
            CombatMessageValidation.RequireFiniteNonNegative(
                healthDamageApplied,
                nameof(healthDamageApplied));
            CombatMessageValidation.RequireFiniteNonNegative(unappliedAmount, nameof(unappliedAmount));

            switch (result)
            {
                case DamageResult.Applied:
                    ValidateApplied(
                        requestedAmount,
                        before,
                        after,
                        shieldDamageApplied,
                        shieldOverflowAmount,
                        healthDamageApplied,
                        unappliedAmount);
                    break;
                case DamageResult.Blocked:
                    if (before.IsDestroyed)
                    {
                        throw new ArgumentException(
                            "A hit on an already destroyed target must use TargetAlreadyDestroyed.",
                            nameof(result));
                    }

                    ValidateNotApplied(
                        requestedAmount,
                        before,
                        after,
                        shieldDamageApplied,
                        shieldOverflowAmount,
                        healthDamageApplied,
                        unappliedAmount);
                    break;
                case DamageResult.DuplicateEventIgnored:
                    ValidateNotApplied(
                        requestedAmount,
                        before,
                        after,
                        shieldDamageApplied,
                        shieldOverflowAmount,
                        healthDamageApplied,
                        unappliedAmount);
                    break;
                case DamageResult.TargetAlreadyDestroyed:
                    if (!before.IsDestroyed)
                    {
                        throw new ArgumentException(
                            "TargetAlreadyDestroyed requires a destroyed before-state.",
                            nameof(result));
                    }

                    ValidateNotApplied(
                        requestedAmount,
                        before,
                        after,
                        shieldDamageApplied,
                        shieldOverflowAmount,
                        healthDamageApplied,
                        unappliedAmount);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result));
            }

            EventId = eventId;
            SourceId = sourceId;
            TargetId = targetId;
            Channel = channel;
            RequestedAmount = requestedAmount;
            Result = result;
            Before = before;
            After = after;
            ShieldDamageApplied = shieldDamageApplied;
            ShieldOverflowAmount = shieldOverflowAmount;
            HealthDamageApplied = healthDamageApplied;
            UnappliedAmount = unappliedAmount;
        }

        public StableId EventId { get; }

        public StableId SourceId { get; }

        public StableId TargetId { get; }

        public CombatChannel Channel { get; }

        public double RequestedAmount { get; }

        public DamageResult Result { get; }

        public VitalState Before { get; }

        public VitalState After { get; }

        public double ShieldDamageApplied { get; }

        public double ShieldOverflowAmount { get; }

        public double HealthDamageApplied { get; }

        public double UnappliedAmount { get; }

        private static void ValidateApplied(
            double requestedAmount,
            VitalState before,
            VitalState after,
            double shieldDamageApplied,
            double shieldOverflowAmount,
            double healthDamageApplied,
            double unappliedAmount)
        {
            if (before.IsDestroyed)
            {
                throw new ArgumentException(
                    "Applied damage cannot start from an already destroyed target.",
                    nameof(before));
            }

            CombatMessageValidation.RequireSameCapacities(before, after);

            if (after.Health > before.Health || after.Shield > before.Shield)
            {
                throw new ArgumentException(
                    "Applied damage cannot increase health or shield.",
                    nameof(after));
            }

            if (shieldDamageApplied != before.Shield - after.Shield)
            {
                throw new ArgumentException(
                    "Shield damage must equal the before/after shield difference.",
                    nameof(shieldDamageApplied));
            }

            if (healthDamageApplied != before.Health - after.Health)
            {
                throw new ArgumentException(
                    "Health damage must equal the before/after health difference.",
                    nameof(healthDamageApplied));
            }

            if (requestedAmount != shieldDamageApplied + shieldOverflowAmount)
            {
                throw new ArgumentException(
                    "Requested damage must equal shield damage plus shield overflow.",
                    nameof(shieldOverflowAmount));
            }

            if (shieldOverflowAmount != healthDamageApplied + unappliedAmount)
            {
                throw new ArgumentException(
                    "Shield overflow must equal health damage plus unapplied damage.",
                    nameof(shieldOverflowAmount));
            }

            if (shieldDamageApplied + healthDamageApplied == 0d)
            {
                throw new ArgumentException(
                    "Applied damage must change shield or health.",
                    nameof(healthDamageApplied));
            }

            if (shieldOverflowAmount > 0d && before.Shield > 0d && after.Shield != 0d)
            {
                throw new ArgumentException(
                    "Shield overflow is contradictory while shield remains.",
                    nameof(shieldOverflowAmount));
            }

            if (healthDamageApplied > 0d && after.Shield > 0d)
            {
                throw new ArgumentException(
                    "Health damage is contradictory while shield remains.",
                    nameof(healthDamageApplied));
            }
        }

        private static void ValidateNotApplied(
            double requestedAmount,
            VitalState before,
            VitalState after,
            double shieldDamageApplied,
            double shieldOverflowAmount,
            double healthDamageApplied,
            double unappliedAmount)
        {
            CombatMessageValidation.RequireUnchanged(before, after);

            if (shieldDamageApplied != 0d
                || shieldOverflowAmount != 0d
                || healthDamageApplied != 0d)
            {
                throw new ArgumentException(
                    "A non-applied result cannot report shield, overflow, or health damage.");
            }

            if (unappliedAmount != requestedAmount)
            {
                throw new ArgumentException(
                    "A non-applied result must report the entire request as unapplied.",
                    nameof(unappliedAmount));
            }
        }
    }

    public sealed class HitMessage : ICombatEventMessage
    {
        public HitMessage(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            CombatChannel channel,
            HitResult result)
        {
            CombatMessageValidation.RequireEnvelope(eventId, sourceId, targetId, channel);
            CombatMessageValidation.RequireDefined(typeof(HitResult), result, nameof(result));

            EventId = eventId;
            SourceId = sourceId;
            TargetId = targetId;
            Channel = channel;
            Result = result;
        }

        public StableId EventId { get; }

        public StableId SourceId { get; }

        public StableId TargetId { get; }

        public CombatChannel Channel { get; }

        public HitResult Result { get; }
    }

    public sealed class VitalMessage : ICombatEventMessage
    {
        public VitalMessage(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            CombatChannel channel,
            VitalResult result,
            VitalState state)
        {
            CombatMessageValidation.RequireEnvelope(eventId, sourceId, targetId, channel);
            CombatMessageValidation.RequireDefined(typeof(VitalResult), result, nameof(result));
            CombatMessageValidation.RequireNotNull(state, nameof(state));

            if (result == VitalResult.Active && state.IsDestroyed)
            {
                throw new ArgumentException(
                    "An Active vital result requires positive health.",
                    nameof(result));
            }

            if (result == VitalResult.Destroyed && !state.IsDestroyed)
            {
                throw new ArgumentException(
                    "A Destroyed vital result requires zero health.",
                    nameof(result));
            }

            EventId = eventId;
            SourceId = sourceId;
            TargetId = targetId;
            Channel = channel;
            Result = result;
            State = state;
        }

        public StableId EventId { get; }

        public StableId SourceId { get; }

        public StableId TargetId { get; }

        public CombatChannel Channel { get; }

        public VitalResult Result { get; }

        public VitalState State { get; }
    }

    public sealed class ContactMessage : ICombatEventMessage
    {
        public ContactMessage(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            CombatChannel channel,
            ContactClassification classification,
            ContactResult result)
        {
            CombatMessageValidation.RequireEnvelope(eventId, sourceId, targetId, channel);
            CombatMessageValidation.RequireDefined(
                typeof(ContactClassification),
                classification,
                nameof(classification));
            CombatMessageValidation.RequireDefined(typeof(ContactResult), result, nameof(result));

            EventId = eventId;
            SourceId = sourceId;
            TargetId = targetId;
            Channel = channel;
            Classification = classification;
            Result = result;
        }

        public StableId EventId { get; }

        public StableId SourceId { get; }

        public StableId TargetId { get; }

        public CombatChannel Channel { get; }

        public ContactClassification Classification { get; }

        public ContactResult Result { get; }
    }

    public sealed class WeightMessage : ICombatEventMessage
    {
        public WeightMessage(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            CombatChannel channel,
            CombatWeightClass sourceWeight,
            CombatWeightClass targetWeight,
            WeightResult result)
        {
            CombatMessageValidation.RequireEnvelope(eventId, sourceId, targetId, channel);
            if (channel != CombatChannel.Contact)
            {
                throw new ArgumentException(
                    "Weight comparisons must use the Contact channel.",
                    nameof(channel));
            }

            CombatMessageValidation.RequireDefined(
                typeof(CombatWeightClass),
                sourceWeight,
                nameof(sourceWeight));
            CombatMessageValidation.RequireDefined(
                typeof(CombatWeightClass),
                targetWeight,
                nameof(targetWeight));
            CombatMessageValidation.RequireDefined(typeof(WeightResult), result, nameof(result));

            WeightResult expected = DetermineResult(sourceWeight, targetWeight);
            if (result != expected)
            {
                throw new ArgumentException(
                    $"Weight result {result} contradicts {sourceWeight} versus {targetWeight}; expected {expected}.",
                    nameof(result));
            }

            EventId = eventId;
            SourceId = sourceId;
            TargetId = targetId;
            Channel = channel;
            SourceWeight = sourceWeight;
            TargetWeight = targetWeight;
            Result = result;
        }

        public StableId EventId { get; }

        public StableId SourceId { get; }

        public StableId TargetId { get; }

        public CombatChannel Channel { get; }

        public CombatWeightClass SourceWeight { get; }

        public CombatWeightClass TargetWeight { get; }

        public WeightResult Result { get; }

        public static WeightResult DetermineResult(
            CombatWeightClass sourceWeight,
            CombatWeightClass targetWeight)
        {
            CombatMessageValidation.RequireDefined(
                typeof(CombatWeightClass),
                sourceWeight,
                nameof(sourceWeight));
            CombatMessageValidation.RequireDefined(
                typeof(CombatWeightClass),
                targetWeight,
                nameof(targetWeight));

            if (targetWeight == CombatWeightClass.Immovable
                && sourceWeight != CombatWeightClass.Immovable)
            {
                return WeightResult.TargetImmovable;
            }

            if (sourceWeight < targetWeight)
            {
                return WeightResult.SourceLighter;
            }

            if (sourceWeight > targetWeight)
            {
                return WeightResult.SourceHeavier;
            }

            return WeightResult.Equal;
        }
    }

    public sealed class StatusMessage : ICombatEventMessage
    {
        public StatusMessage(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            CombatChannel channel,
            CombatStatus status,
            StatusResult result,
            double durationSeconds,
            double magnitude)
        {
            CombatMessageValidation.RequireEnvelope(eventId, sourceId, targetId, channel);
            CombatMessageValidation.RequireDefined(typeof(CombatStatus), status, nameof(status));
            CombatMessageValidation.RequireDefined(typeof(StatusResult), result, nameof(result));
            CombatMessageValidation.RequireFiniteNonNegative(
                durationSeconds,
                nameof(durationSeconds));
            CombatMessageValidation.RequireFiniteNonNegative(magnitude, nameof(magnitude));

            if (result == StatusResult.Applied || result == StatusResult.Refreshed)
            {
                if (durationSeconds == 0d)
                {
                    throw new ArgumentException(
                        "Applied or refreshed status requires a positive duration.",
                        nameof(durationSeconds));
                }
            }
            else if (durationSeconds != 0d || magnitude != 0d)
            {
                throw new ArgumentException(
                    "Removed, resisted, duplicate, and late status results carry zero duration and magnitude.");
            }

            EventId = eventId;
            SourceId = sourceId;
            TargetId = targetId;
            Channel = channel;
            Status = status;
            Result = result;
            DurationSeconds = durationSeconds;
            Magnitude = magnitude;
        }

        public StableId EventId { get; }

        public StableId SourceId { get; }

        public StableId TargetId { get; }

        public CombatChannel Channel { get; }

        public CombatStatus Status { get; }

        public StatusResult Result { get; }

        public double DurationSeconds { get; }

        public double Magnitude { get; }
    }

    /// <summary>
    /// Classifies whether two messages are distinct, exact retries, or conflicting reuse of one event ID.
    /// </summary>
    public static class CombatEventIdentity
    {
        public static CombatEventIdentityResult Classify(
            ICombatEventMessage first,
            ICombatEventMessage second)
        {
            CombatMessageValidation.RequireNotNull(first, nameof(first));
            CombatMessageValidation.RequireNotNull(second, nameof(second));

            if (first.EventId != second.EventId)
            {
                return CombatEventIdentityResult.Distinct;
            }

            bool sameEnvelope = first.SourceId == second.SourceId
                && first.TargetId == second.TargetId
                && first.Channel == second.Channel;

            return sameEnvelope
                ? CombatEventIdentityResult.Duplicate
                : CombatEventIdentityResult.ConflictingDuplicate;
        }
    }

    internal static class CombatMessageValidation
    {
        public static void RequireEnvelope(
            StableId eventId,
            StableId sourceId,
            StableId targetId,
            CombatChannel channel)
        {
            RequireNotNull(eventId, nameof(eventId));
            RequireNotNull(sourceId, nameof(sourceId));
            RequireNotNull(targetId, nameof(targetId));
            RequireDefined(typeof(CombatChannel), channel, nameof(channel));
        }

        public static void RequireDefined(Type enumType, object value, string parameterName)
        {
            if (!Enum.IsDefined(enumType, value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Unknown {enumType.Name} value.");
            }
        }

        public static void RequireFiniteNonNegative(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value cannot be negative.");
            }
        }

        public static void RequireFinitePositive(double value, string parameterName)
        {
            RequireFinite(value, parameterName);
            if (value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be positive.");
            }
        }

        public static void RequireNotNull(object value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        public static void RequireSameCapacities(VitalState before, VitalState after)
        {
            if (before.MaximumHealth != after.MaximumHealth
                || before.MaximumShield != after.MaximumShield)
            {
                throw new ArgumentException(
                    "One damage result cannot change maximum health or maximum shield.",
                    nameof(after));
            }
        }

        public static void RequireUnchanged(VitalState before, VitalState after)
        {
            if (!before.Equals(after))
            {
                throw new ArgumentException(
                    "This result requires identical before and after vital states.",
                    nameof(after));
            }
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite.");
            }
        }
    }
}

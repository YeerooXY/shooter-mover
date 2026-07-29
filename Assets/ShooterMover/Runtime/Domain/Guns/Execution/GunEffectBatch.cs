using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace ShooterMover.Domain.Guns.Execution
{
    public sealed class GunEffectIdentity
    {
        public GunEffectIdentity(
            GunActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            GunDefinitionId gunDefinitionId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long shotSequence,
            ProjectileOrdinal projectileOrdinal)
        {
            if (shotSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(shotSequence));
            }

            ActorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
            EquipmentInstanceId = equipmentInstanceId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
            GunDefinitionId = gunDefinitionId
                ?? throw new ArgumentNullException(nameof(gunDefinitionId));
            FireOperationId = fireOperationId
                ?? throw new ArgumentNullException(nameof(fireOperationId));
            LifecycleGeneration = lifecycleGeneration
                ?? throw new ArgumentNullException(nameof(lifecycleGeneration));
            ProjectileOrdinal = projectileOrdinal
                ?? throw new ArgumentNullException(nameof(projectileOrdinal));
            ShotSequence = shotSequence;
        }

        public GunActorInstanceId ActorId { get; }
        public RunParticipantId ParticipantId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public GunDefinitionId GunDefinitionId { get; }
        public FireOperationId FireOperationId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long ShotSequence { get; }
        public ProjectileOrdinal ProjectileOrdinal { get; }

        public string ToCanonicalString()
        {
            return ActorId + "|" + ParticipantId + "|" + EquipmentInstanceId + "|"
                + GunDefinitionId + "|" + FireOperationId + "|" + LifecycleGeneration + "|"
                + ShotSequence.ToString(CultureInfo.InvariantCulture) + "|" + ProjectileOrdinal;
        }
    }

    public enum GunEffectKind
    {
        DirectProjectile = 1,
        ExplosiveProjectile = 2,
        ChainArc = 3,
        DamageOverTimeProjectile = 4,
        CanonicalProjectileLaunch = 5,
    }

    public interface IGunEffectDescription
    {
        GunEffectKind Kind { get; }
        GunEffectIdentity Identity { get; }
        string ToCanonicalString();
    }

    public sealed class DirectProjectileEffect : IGunEffectDescription
    {
        public DirectProjectileEffect(
            GunEffectIdentity identity,
            GunVector2 origin,
            GunVector2 direction,
            double speed,
            double range,
            double directDamage,
            int pierce,
            double knockback,
            string damageType)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            Direction = direction == null ? null : direction.Normalized;
            Speed = speed;
            Range = range;
            DirectDamage = directDamage;
            Pierce = pierce;
            Knockback = knockback;
            DamageType = damageType ?? string.Empty;
        }

        public GunEffectKind Kind { get { return GunEffectKind.DirectProjectile; } }
        public GunEffectIdentity Identity { get; }
        public GunVector2 Origin { get; }
        public GunVector2 Direction { get; }
        public double Speed { get; }
        public double Range { get; }
        public double DirectDamage { get; }
        public int Pierce { get; }
        public double Knockback { get; }
        public string DamageType { get; }

        public string ToCanonicalString()
        {
            return "direct|" + Identity.ToCanonicalString() + "|" + Origin + "|" + Direction + "|"
                + Speed.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Range.ToString("R", CultureInfo.InvariantCulture) + "|"
                + DirectDamage.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Pierce.ToString(CultureInfo.InvariantCulture) + "|"
                + Knockback.ToString("R", CultureInfo.InvariantCulture) + "|" + DamageType;
        }
    }

    /// <summary>
    /// Canonical scheduler-to-projectile handoff retained inside the existing immutable batch
    /// envelope. All execution values are already baked into the request profile and initial state;
    /// downstream adapters must launch this state instead of rebuilding values from a blueprint.
    /// </summary>
    public sealed class ProjectileLaunchEffect : IGunEffectDescription
    {
        public ProjectileLaunchEffect(
            ProjectileLaunchRequest launchRequest,
            ProjectileLifecycleState initialState)
        {
            LaunchRequest = launchRequest
                ?? throw new ArgumentNullException(nameof(launchRequest));
            InitialState = initialState
                ?? throw new ArgumentNullException(nameof(initialState));
            if (launchRequest.Profile == null
                || !launchRequest.Profile.IsCanonical
                || initialState.Profile == null
                || !ReferenceEquals(initialState.Profile, launchRequest.Profile)
                || initialState.Lifecycle == null
                || !initialState.Lifecycle.Identity.Equals(
                    launchRequest.Lifecycle.Identity))
            {
                throw new ArgumentException(
                    "canonical-projectile-launch-state-mismatch",
                    nameof(initialState));
            }

            Identity = launchRequest.Lifecycle.Identity.SourceIdentity;
        }

        public GunEffectKind Kind
        {
            get { return GunEffectKind.CanonicalProjectileLaunch; }
        }
        public GunEffectIdentity Identity { get; }
        public ProjectileLaunchRequest LaunchRequest { get; }
        public ProjectileLifecycleState InitialState { get; }
        public ProjectileExecutionProfile Profile { get { return LaunchRequest.Profile; } }

        public string ToCanonicalString()
        {
            GunExplosionTriggerSpec trigger = Profile.Impact.ExplosionTrigger;
            GunRicochetSpec ricochet = Profile.Impact.Ricochet;
            GunExplosionEffect explosion = Profile.Effects.Explosion;
            GunDamageOverTimeStats dot = Profile.Damage.DamageOverTime;
            return string.Join(
                "|",
                new[]
                {
                    "canonical-projectile-launch",
                    Identity.ToCanonicalString(),
                    Profile.DefinitionId.ToString(),
                    Profile.EquipmentInstanceId.ToString(),
                    ((int)Profile.ExecutionMode).ToString(CultureInfo.InvariantCulture),
                    Profile.CanonicalDeliveryType.HasValue
                        ? ((int)Profile.CanonicalDeliveryType.Value).ToString(CultureInfo.InvariantCulture)
                        : "none",
                    ((int)Profile.Projectile.Kind).ToString(CultureInfo.InvariantCulture),
                    Format(Profile.Projectile.Speed),
                    Format(Profile.Projectile.Range),
                    Profile.Pierce.Tenths.ToString(CultureInfo.InvariantCulture),
                    Profile.Ricochet.Tenths.ToString(CultureInfo.InvariantCulture),
                    ((int)Profile.Projectile.TerminationBehavior).ToString(CultureInfo.InvariantCulture),
                    ((int)Profile.Guidance.Mode).ToString(CultureInfo.InvariantCulture),
                    Format(Profile.Guidance.AcquisitionRange),
                    Format(Profile.Guidance.TurnRateDegreesPerSecond),
                    Format(Profile.Guidance.ActivationDelaySeconds),
                    ((int)Profile.Guidance.TargetPolicy).ToString(CultureInfo.InvariantCulture),
                    ((int)Profile.Guidance.Reacquisition).ToString(CultureInfo.InvariantCulture),
                    Profile.Impact.HandlesEnemyImpact ? "1" : "0",
                    Profile.Impact.HandlesWallImpact ? "1" : "0",
                    Profile.Impact.HandlesRangeExpiry ? "1" : "0",
                    Profile.Impact.HandlesTermination ? "1" : "0",
                    ricochet == null ? "none" : Format(ricochet.RetainedSpeedPerRicochet),
                    ricochet == null ? "none" : Format(ricochet.RandomAngleDegrees),
                    ricochet == null ? "none" : Format(ricochet.PostBounceHomingPauseSeconds),
                    trigger == null ? "none" : (trigger.OnEnemyImpact ? "1" : "0"),
                    trigger == null ? "none" : (trigger.OnWallImpact ? "1" : "0"),
                    trigger == null ? "none" : (trigger.OnRangeExpiry ? "1" : "0"),
                    trigger == null ? "none" : (trigger.OnTermination ? "1" : "0"),
                    ((int)Profile.Damage.Category).ToString(CultureInfo.InvariantCulture),
                    Format(Profile.Damage.DirectDamage),
                    Format(Profile.Damage.AreaDamage),
                    dot == null ? "none" : Format(dot.DamagePerSecond),
                    dot == null ? "none" : Format(dot.DurationSeconds),
                    Format(Profile.Damage.Knockback),
                    explosion == null ? "none" : Format(explosion.Radius),
                    explosion == null ? "none" : Format(explosion.MinimumDamageMultiplier),
                    Format(Profile.MovementPenaltyPercent),
                    LaunchRequest.Origin.ToString(),
                    LaunchRequest.Direction.ToString(),
                    LaunchRequest.InitialTarget == null
                        ? "none"
                        : LaunchRequest.InitialTarget.ToCanonicalString(),
                    InitialState.Position.ToString(),
                    InitialState.Direction.ToString(),
                    Format(InitialState.Speed),
                    Format(InitialState.TravelledDistance),
                    InitialState.PierceState.AuthoredValue.Tenths.ToString(
                        CultureInfo.InvariantCulture),
                });
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed class ExplosiveProjectileEffect : IGunEffectDescription
    {
        public ExplosiveProjectileEffect(
            GunEffectIdentity identity,
            GunVector2 origin,
            GunVector2 direction,
            double speed,
            double range,
            double directDamage,
            double areaDamage,
            double explosionRadius,
            double knockback,
            string damageType)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            Direction = direction == null ? null : direction.Normalized;
            Speed = speed;
            Range = range;
            DirectDamage = directDamage;
            AreaDamage = areaDamage;
            ExplosionRadius = explosionRadius;
            Knockback = knockback;
            DamageType = damageType ?? string.Empty;
        }

        public GunEffectKind Kind { get { return GunEffectKind.ExplosiveProjectile; } }
        public GunEffectIdentity Identity { get; }
        public GunVector2 Origin { get; }
        public GunVector2 Direction { get; }
        public double Speed { get; }
        public double Range { get; }
        public double DirectDamage { get; }
        public double AreaDamage { get; }
        public double ExplosionRadius { get; }
        public double Knockback { get; }
        public string DamageType { get; }

        public string ToCanonicalString()
        {
            return "explosive|" + Identity.ToCanonicalString() + "|" + Origin + "|" + Direction + "|"
                + Speed.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Range.ToString("R", CultureInfo.InvariantCulture) + "|"
                + DirectDamage.ToString("R", CultureInfo.InvariantCulture) + "|"
                + AreaDamage.ToString("R", CultureInfo.InvariantCulture) + "|"
                + ExplosionRadius.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Knockback.ToString("R", CultureInfo.InvariantCulture) + "|" + DamageType;
        }
    }

    public sealed class DamageOverTimeProjectileEffect : IGunEffectDescription
    {
        public DamageOverTimeProjectileEffect(
            GunEffectIdentity identity,
            GunVector2 origin,
            GunVector2 direction,
            double speed,
            double range,
            double directDamage,
            int pierce,
            double dotDps,
            double dotDuration,
            double poolRadius,
            double poolDuration,
            double knockback,
            string damageType)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            Direction = direction == null ? null : direction.Normalized;
            Speed = speed;
            Range = range;
            DirectDamage = directDamage;
            Pierce = pierce;
            DotDps = dotDps;
            DotDuration = dotDuration;
            PoolRadius = poolRadius;
            PoolDuration = poolDuration;
            Knockback = knockback;
            DamageType = damageType ?? string.Empty;
        }

        public GunEffectKind Kind { get { return GunEffectKind.DamageOverTimeProjectile; } }
        public GunEffectIdentity Identity { get; }
        public GunVector2 Origin { get; }
        public GunVector2 Direction { get; }
        public double Speed { get; }
        public double Range { get; }
        public double DirectDamage { get; }
        public int Pierce { get; }
        public double DotDps { get; }
        public double DotDuration { get; }
        public double PoolRadius { get; }
        public double PoolDuration { get; }
        public double Knockback { get; }
        public string DamageType { get; }

        public string ToCanonicalString()
        {
            return "dot-projectile|" + Identity.ToCanonicalString() + "|" + Origin + "|" + Direction + "|"
                + Speed.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Range.ToString("R", CultureInfo.InvariantCulture) + "|"
                + DirectDamage.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Pierce.ToString(CultureInfo.InvariantCulture) + "|"
                + DotDps.ToString("R", CultureInfo.InvariantCulture) + "|"
                + DotDuration.ToString("R", CultureInfo.InvariantCulture) + "|"
                + PoolRadius.ToString("R", CultureInfo.InvariantCulture) + "|"
                + PoolDuration.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Knockback.ToString("R", CultureInfo.InvariantCulture) + "|" + DamageType;
        }
    }

    public sealed class ChainArcEffect : IGunEffectDescription
    {
        public ChainArcEffect(
            GunEffectIdentity identity,
            GunVector2 origin,
            GunVector2 direction,
            double damage,
            int maximumTargets,
            double maximumRange,
            double knockback,
            string damageType)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            Direction = direction == null ? null : direction.Normalized;
            Damage = damage;
            MaximumTargets = maximumTargets;
            MaximumRange = maximumRange;
            Knockback = knockback;
            DamageType = damageType ?? string.Empty;
        }

        public GunEffectKind Kind { get { return GunEffectKind.ChainArc; } }
        public GunEffectIdentity Identity { get; }
        public GunVector2 Origin { get; }
        public GunVector2 Direction { get; }
        public double Damage { get; }
        public int MaximumTargets { get; }
        public double MaximumRange { get; }
        public double Knockback { get; }
        public string DamageType { get; }

        public string ToCanonicalString()
        {
            return "chain|" + Identity.ToCanonicalString() + "|" + Origin + "|" + Direction + "|"
                + Damage.ToString("R", CultureInfo.InvariantCulture) + "|"
                + MaximumTargets.ToString(CultureInfo.InvariantCulture) + "|"
                + MaximumRange.ToString("R", CultureInfo.InvariantCulture) + "|"
                + Knockback.ToString("R", CultureInfo.InvariantCulture) + "|" + DamageType;
        }
    }

    public sealed class GunEffectBatch
    {
        private readonly ReadOnlyCollection<IGunEffectDescription> effects;

        public GunEffectBatch(IList<IGunEffectDescription> effectDescriptions)
        {
            if (effectDescriptions == null)
            {
                throw new ArgumentNullException(nameof(effectDescriptions));
            }

            if (effectDescriptions.Count < 1
                || effectDescriptions.Count > GunLiveFiringProfile.MaximumEffectsPerFire)
            {
                throw new ArgumentOutOfRangeException(nameof(effectDescriptions));
            }

            List<IGunEffectDescription> copy =
                new List<IGunEffectDescription>(effectDescriptions.Count);
            HashSet<int> ordinals = new HashSet<int>();
            GunEffectIdentity first = null;
            StringBuilder canonical = new StringBuilder();
            for (int index = 0; index < effectDescriptions.Count; index++)
            {
                IGunEffectDescription effect = effectDescriptions[index];
                if (effect == null || effect.Identity == null)
                {
                    throw new ArgumentException(
                        "Effect batches cannot contain null effects or identities.",
                        nameof(effectDescriptions));
                }

                if (first == null)
                {
                    first = effect.Identity;
                }
                else if (!SameFire(first, effect.Identity))
                {
                    throw new ArgumentException(
                        "Every effect in a batch must belong to the same fire operation.",
                        nameof(effectDescriptions));
                }

                if (!ordinals.Add(effect.Identity.ProjectileOrdinal.Value))
                {
                    throw new ArgumentException(
                        "Projectile ordinals must be unique inside one batch.",
                        nameof(effectDescriptions));
                }

                copy.Add(effect);
                canonical.Append(index.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(effect.ToCanonicalString())
                    .Append('\n');
            }

            effects = new ReadOnlyCollection<IGunEffectDescription>(copy);
            Identity = first;
            CanonicalText = canonical.ToString();
            Fingerprint = "fnv1a32:"
                + unchecked((uint)GunExecutionHash.Of(CanonicalText))
                    .ToString("x8", CultureInfo.InvariantCulture);
        }

        public GunEffectIdentity Identity { get; }
        public IReadOnlyList<IGunEffectDescription> Effects { get { return effects; } }
        public int EffectCount { get { return effects.Count; } }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        private static bool SameFire(GunEffectIdentity left, GunEffectIdentity right)
        {
            return left.ActorId.Equals(right.ActorId)
                && left.ParticipantId.Equals(right.ParticipantId)
                && left.EquipmentInstanceId.Equals(right.EquipmentInstanceId)
                && left.GunDefinitionId.Equals(right.GunDefinitionId)
                && left.FireOperationId.Equals(right.FireOperationId)
                && left.LifecycleGeneration.Equals(right.LifecycleGeneration)
                && left.ShotSequence == right.ShotSequence;
        }
    }
}

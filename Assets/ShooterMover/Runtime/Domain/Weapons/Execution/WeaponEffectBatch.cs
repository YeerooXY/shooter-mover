using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace ShooterMover.Domain.Weapons.Execution
{
    public sealed class WeaponEffectIdentity
    {
        public WeaponEffectIdentity(
            WeaponActorInstanceId actorId,
            RunParticipantId participantId,
            EquipmentInstanceId equipmentInstanceId,
            WeaponDefinitionId weaponDefinitionId,
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
            WeaponDefinitionId = weaponDefinitionId
                ?? throw new ArgumentNullException(nameof(weaponDefinitionId));
            FireOperationId = fireOperationId
                ?? throw new ArgumentNullException(nameof(fireOperationId));
            LifecycleGeneration = lifecycleGeneration
                ?? throw new ArgumentNullException(nameof(lifecycleGeneration));
            ProjectileOrdinal = projectileOrdinal
                ?? throw new ArgumentNullException(nameof(projectileOrdinal));
            ShotSequence = shotSequence;
        }

        public WeaponActorInstanceId ActorId { get; }
        public RunParticipantId ParticipantId { get; }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public WeaponDefinitionId WeaponDefinitionId { get; }
        public FireOperationId FireOperationId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long ShotSequence { get; }
        public ProjectileOrdinal ProjectileOrdinal { get; }

        public string ToCanonicalString()
        {
            return ActorId + "|" + ParticipantId + "|" + EquipmentInstanceId + "|"
                + WeaponDefinitionId + "|" + FireOperationId + "|" + LifecycleGeneration + "|"
                + ShotSequence.ToString(CultureInfo.InvariantCulture) + "|" + ProjectileOrdinal;
        }
    }

    public enum WeaponEffectKind
    {
        DirectProjectile = 1,
        ExplosiveProjectile = 2,
        ChainArc = 3,
        DamageOverTimeProjectile = 4,
    }

    public interface IWeaponEffectDescription
    {
        WeaponEffectKind Kind { get; }
        WeaponEffectIdentity Identity { get; }
        string ToCanonicalString();
    }

    public sealed class DirectProjectileEffect : IWeaponEffectDescription
    {
        public DirectProjectileEffect(
            WeaponEffectIdentity identity,
            WeaponVector2 origin,
            WeaponVector2 direction,
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

        public WeaponEffectKind Kind { get { return WeaponEffectKind.DirectProjectile; } }
        public WeaponEffectIdentity Identity { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 Direction { get; }
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

    public sealed class ExplosiveProjectileEffect : IWeaponEffectDescription
    {
        public ExplosiveProjectileEffect(
            WeaponEffectIdentity identity,
            WeaponVector2 origin,
            WeaponVector2 direction,
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

        public WeaponEffectKind Kind { get { return WeaponEffectKind.ExplosiveProjectile; } }
        public WeaponEffectIdentity Identity { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 Direction { get; }
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

    public sealed class DamageOverTimeProjectileEffect : IWeaponEffectDescription
    {
        public DamageOverTimeProjectileEffect(
            WeaponEffectIdentity identity,
            WeaponVector2 origin,
            WeaponVector2 direction,
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

        public WeaponEffectKind Kind { get { return WeaponEffectKind.DamageOverTimeProjectile; } }
        public WeaponEffectIdentity Identity { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 Direction { get; }
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

    public sealed class ChainArcEffect : IWeaponEffectDescription
    {
        public ChainArcEffect(
            WeaponEffectIdentity identity,
            WeaponVector2 origin,
            WeaponVector2 direction,
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

        public WeaponEffectKind Kind { get { return WeaponEffectKind.ChainArc; } }
        public WeaponEffectIdentity Identity { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 Direction { get; }
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

    public sealed class WeaponEffectBatch
    {
        private readonly ReadOnlyCollection<IWeaponEffectDescription> effects;

        public WeaponEffectBatch(IList<IWeaponEffectDescription> effectDescriptions)
        {
            if (effectDescriptions == null)
            {
                throw new ArgumentNullException(nameof(effectDescriptions));
            }

            if (effectDescriptions.Count < 1
                || effectDescriptions.Count > WeaponRuntimeFiringProfile.MaximumEffectsPerFire)
            {
                throw new ArgumentOutOfRangeException(nameof(effectDescriptions));
            }

            List<IWeaponEffectDescription> copy =
                new List<IWeaponEffectDescription>(effectDescriptions.Count);
            HashSet<int> ordinals = new HashSet<int>();
            WeaponEffectIdentity first = null;
            StringBuilder canonical = new StringBuilder();
            for (int index = 0; index < effectDescriptions.Count; index++)
            {
                IWeaponEffectDescription effect = effectDescriptions[index];
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

            effects = new ReadOnlyCollection<IWeaponEffectDescription>(copy);
            Identity = first;
            CanonicalText = canonical.ToString();
            Fingerprint = "fnv1a32:"
                + unchecked((uint)WeaponExecutionHash.Of(CanonicalText))
                    .ToString("x8", CultureInfo.InvariantCulture);
        }

        public WeaponEffectIdentity Identity { get; }
        public IReadOnlyList<IWeaponEffectDescription> Effects { get { return effects; } }
        public int EffectCount { get { return effects.Count; } }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        private static bool SameFire(WeaponEffectIdentity left, WeaponEffectIdentity right)
        {
            return left.ActorId.Equals(right.ActorId)
                && left.ParticipantId.Equals(right.ParticipantId)
                && left.EquipmentInstanceId.Equals(right.EquipmentInstanceId)
                && left.WeaponDefinitionId.Equals(right.WeaponDefinitionId)
                && left.FireOperationId.Equals(right.FireOperationId)
                && left.LifecycleGeneration.Equals(right.LifecycleGeneration)
                && left.ShotSequence == right.ShotSequence;
        }
    }
}

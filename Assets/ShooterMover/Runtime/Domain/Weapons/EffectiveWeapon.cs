using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Immutable derived weapon profile. It preserves canonical blueprint identity and
    /// equipment-instance identity while keeping item level as metadata rather than combat scaling.
    /// </summary>
    public sealed class EffectiveWeapon
    {
        private readonly ReadOnlyCollection<AugmentInstance> installedAugments;

        internal EffectiveWeapon(
            WeaponBlueprint blueprint,
            EquipmentInstanceId equipmentInstanceId,
            StableId equipmentDefinitionId,
            int itemLevel,
            StableId qualityId,
            IEnumerable<AugmentInstance> installedAugments,
            WeaponFireSettings fireSettings,
            WeaponShotPattern shotPattern,
            WeaponProjectileSpec projectile,
            WeaponGuidanceSpec guidance,
            WeaponImpactSpec impact,
            WeaponDamageSpec damage,
            WeaponEffects effects)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            EquipmentInstanceId = equipmentInstanceId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
            EquipmentDefinitionId = equipmentDefinitionId
                ?? throw new ArgumentNullException(nameof(equipmentDefinitionId));
            ItemLevel = itemLevel;
            QualityId = qualityId ?? throw new ArgumentNullException(nameof(qualityId));
            this.installedAugments = CopyAugments(installedAugments);
            FireSettings = fireSettings ?? throw new ArgumentNullException(nameof(fireSettings));
            ShotPattern = shotPattern ?? throw new ArgumentNullException(nameof(shotPattern));
            Projectile = projectile;
            Guidance = guidance ?? throw new ArgumentNullException(nameof(guidance));
            Impact = impact ?? throw new ArgumentNullException(nameof(impact));
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public WeaponBlueprint Blueprint { get; }
        public WeaponDefinitionId DefinitionId { get { return Blueprint.DefinitionId; } }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public StableId EquipmentDefinitionId { get; }
        public int ItemLevel { get; }
        public StableId QualityId { get; }
        public IReadOnlyList<AugmentInstance> InstalledAugments { get { return installedAugments; } }

        public WeaponFireSettings FireSettings { get; }
        public WeaponShotPattern ShotPattern { get; }
        public WeaponProjectileSpec Projectile { get; }
        public WeaponGuidanceSpec Guidance { get; }
        public WeaponImpactSpec Impact { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponEffects Effects { get; }

        private static ReadOnlyCollection<AugmentInstance> CopyAugments(
            IEnumerable<AugmentInstance> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<AugmentInstance> copy = new List<AugmentInstance>();
            foreach (AugmentInstance value in values)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Effective weapon augment snapshots cannot contain null values.",
                        nameof(values));
                }
                copy.Add(value);
            }
            return new ReadOnlyCollection<AugmentInstance>(copy);
        }
    }
}

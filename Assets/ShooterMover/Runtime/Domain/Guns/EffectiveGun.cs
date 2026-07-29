using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns
{
    /// <summary>
    /// Immutable derived gun profile. It preserves canonical definition and exact equipment
    /// instance identity while keeping item level as metadata rather than combat scaling. Existing
    /// evaluated contracts remain the final runtime values; canonical authored grouping is exposed
    /// without allowing spawned attacks to query inventory, augments, skills, or the catalogue.
    /// </summary>
    public sealed class EffectiveGun
    {
        private readonly ReadOnlyCollection<AugmentInstance> installedAugments;

        internal EffectiveGun(
            Gun blueprint,
            EquipmentInstanceId equipmentInstanceId,
            StableId equipmentDefinitionId,
            int itemLevel,
            StableId qualityId,
            IEnumerable<AugmentInstance> installedAugments,
            FireSettings fireSettings,
            GunShotPattern shotPattern,
            ProjectileSettings projectile,
            GunGuidanceSpec guidance,
            GunImpactSpec impact,
            GunDamageSpec damage,
            GunEffects effects,
            GunAttackDistance effectiveMaximumAttackDistance,
            PierceValue effectivePierce,
            RicochetValue effectiveRicochet,
            double effectiveMovementPenaltyPercent)
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
            EffectiveMaximumAttackDistance = effectiveMaximumAttackDistance;
            EffectivePierce = effectivePierce;
            EffectiveRicochet = effectiveRicochet;
            if (double.IsNaN(effectiveMovementPenaltyPercent)
                || double.IsInfinity(effectiveMovementPenaltyPercent)
                || effectiveMovementPenaltyPercent < 0d
                || effectiveMovementPenaltyPercent > 100d)
            {
                throw new ArgumentOutOfRangeException(nameof(effectiveMovementPenaltyPercent));
            }
            EffectiveMovementPenaltyPercent = effectiveMovementPenaltyPercent;
        }

        public Gun Blueprint { get; }
        public GunDefinitionId DefinitionId { get { return Blueprint.DefinitionId; } }
        public EquipmentInstanceId EquipmentInstanceId { get; }
        public StableId EquipmentDefinitionId { get; }
        public int ItemLevel { get; }
        public StableId QualityId { get; }
        public IReadOnlyList<AugmentInstance> InstalledAugments { get { return installedAugments; } }

        public FireSettings FireSettings { get; }
        public GunShotPattern ShotPattern { get; }
        public ProjectileSettings Projectile { get; }
        public GunGuidanceSpec Guidance { get; }
        public GunImpactSpec Impact { get; }
        public GunDamageSpec Damage { get; }
        public GunEffects Effects { get; }

        public bool UsesCanonicalAuthoredDefinition
        {
            get { return !Blueprint.IsTransitionalCatalogProjection; }
        }
        public ShotPattern AuthoredDelivery { get { return Blueprint.Delivery; } }
        public GunPresentation Presentation { get { return Blueprint.Presentation; } }
        public GunDropMetadata DropMetadata { get { return Blueprint.DropMetadata; } }

        /// <summary>
        /// Final immutable semantic values after compatible modifiers. These remain available for
        /// Laser and approved non-projectile Special deliveries and are not inferred from a
        /// ProjectileSettings.
        /// </summary>
        public GunAttackDistance EffectiveMaximumAttackDistance { get; }
        public PierceValue EffectivePierce { get; }
        public RicochetValue EffectiveRicochet { get; }
        public double EffectiveMovementPenaltyPercent { get; }

        public double MovementPenaltyPercent
        {
            get { return EffectiveMovementPenaltyPercent; }
        }

        public GunAttackDistance MaximumAttackDistance
        {
            get { return EffectiveMaximumAttackDistance; }
        }

        public PierceValue Pierce
        {
            get { return EffectivePierce; }
        }

        public RicochetValue AuthoredRicochet
        {
            get
            {
                return Blueprint.BaseStats == null
                    ? new RicochetValue(0)
                    : Blueprint.BaseStats.Ricochet;
            }
        }

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
                        "Effective gun augment snapshots cannot contain null values.",
                        nameof(values));
                }
                copy.Add(value);
            }
            return new ReadOnlyCollection<AugmentInstance>(copy);
        }
    }
}

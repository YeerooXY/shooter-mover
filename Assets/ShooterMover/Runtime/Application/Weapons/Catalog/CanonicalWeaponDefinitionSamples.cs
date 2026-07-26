using System;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Catalog
{
    /// <summary>
    /// Development-only representative authored content for the canonical model. These definitions
    /// are not registered into production, do not recreate retired Stage 1 content, and do not
    /// constitute a catalogue migration.
    /// </summary>
    public static class CanonicalWeaponDefinitionSamples
    {
        public static WeaponBlueprint PulseShotgun()
        {
            return WeaponBlueprint.CreateAuthored(
                new WeaponIdentity(
                    new WeaponDefinitionId("weapon.sample-pulse-shotgun"),
                    "Pulse Shotgun",
                    "weapon-family.sample-pulse"),
                WeaponFireSettings.SemiAutomatic(1.35d),
                WeaponShotPattern.Canonical(8, 28d),
                new WeaponBaseStats(
                    7.5d,
                    WeaponDamageCategory.Energy,
                    null,
                    new PierceValue(10),
                    new RicochetValue(0),
                    8d,
                    WeaponAttackDistance.Limited(18d)),
                WeaponDeliverySpec.Create(
                    WeaponDeliveryType.Normal,
                    new WeaponNormalDeliverySettings(28d, 0.14d),
                    null,
                    null,
                    null,
                    null,
                    WeaponGuidanceSpec.Unguided(),
                    StandardTravellingImpact(),
                    WeaponEffects.None()),
                Presentation("pulse-shotgun", "pellet-energy"),
                Drop(
                    "pulse-shotgun",
                    "rare",
                    24,
                    1.25d,
                    WeaponStrongboxEligibility.FromMinimumTier(3)));
        }

        public static WeaponBlueprint SeekingChemicalDotOrbLauncher()
        {
            var dotStats = new WeaponDamageOverTimeStats(6d, 5d);
            var effects = new WeaponEffects(
                null,
                new WeaponDamageOverTimeEffect(4d, 3, true),
                null);
            return WeaponBlueprint.CreateAuthored(
                new WeaponIdentity(
                    new WeaponDefinitionId("weapon.sample-seeking-chemical-dot-orb"),
                    "Seeking Chemical DoT Orb Launcher",
                    "weapon-family.sample-chemical-orb"),
                WeaponFireSettings.SemiAutomatic(0.9d),
                WeaponShotPattern.Canonical(1, 0d),
                new WeaponBaseStats(
                    18d,
                    WeaponDamageCategory.Chemical,
                    dotStats,
                    new PierceValue(12),
                    new RicochetValue(0),
                    12d,
                    WeaponAttackDistance.Limited(30d)),
                WeaponDeliverySpec.Create(
                    WeaponDeliveryType.Orb,
                    null,
                    new WeaponOrbDeliverySettings(10d, 0.42d),
                    null,
                    null,
                    null,
                    WeaponGuidanceSpec.Homing(
                        18d,
                        150d,
                        0.15d,
                        WeaponTargetPolicy.ClosestToAim,
                        WeaponReacquisitionMode.ReuseTargetPolicy),
                    StandardTravellingImpact(),
                    effects),
                Presentation("chemical-orb-launcher", "chemical-orb"),
                Drop(
                    "seeking-chemical-dot-orb",
                    "epic",
                    48,
                    0.8d,
                    WeaponStrongboxEligibility.FromMinimumTier(6)));
        }

        public static WeaponBlueprint ContactRocketLauncher()
        {
            var explosion = new WeaponExplosionEffect(2.5d, 0.35d);
            var impact = WeaponImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                new WeaponExplosionTriggerSpec(true, true, true, true));
            return WeaponBlueprint.CreateAuthored(
                new WeaponIdentity(
                    new WeaponDefinitionId("weapon.sample-contact-rocket"),
                    "Contact Rocket Launcher",
                    "weapon-family.sample-rocket"),
                WeaponFireSettings.SemiAutomatic(0.75d),
                WeaponShotPattern.Canonical(1, 0d),
                new WeaponBaseStats(
                    46d,
                    WeaponDamageCategory.Thermal,
                    null,
                    new PierceValue(27),
                    new RicochetValue(0),
                    16d,
                    WeaponAttackDistance.Limited(38d)),
                WeaponDeliverySpec.Create(
                    WeaponDeliveryType.Rocket,
                    null,
                    null,
                    new WeaponRocketDeliverySettings(18d, 0.28d),
                    null,
                    null,
                    WeaponGuidanceSpec.Unguided(),
                    impact,
                    new WeaponEffects(explosion, null, null)),
                Presentation("contact-rocket-launcher", "contact-rocket"),
                Drop(
                    "contact-rocket",
                    "legendary",
                    70,
                    0.45d,
                    WeaponStrongboxEligibility.FromMinimumTier(8)));
        }

        public static WeaponBlueprint AutomaticEnergyLaser()
        {
            return WeaponBlueprint.CreateAuthored(
                new WeaponIdentity(
                    new WeaponDefinitionId("weapon.sample-automatic-energy-laser"),
                    "Automatic Energy Laser",
                    "weapon-family.sample-laser"),
                WeaponFireSettings.Automatic(5.5d),
                WeaponShotPattern.Canonical(1, 0d),
                new WeaponBaseStats(
                    12d,
                    WeaponDamageCategory.Energy,
                    null,
                    new PierceValue(20),
                    new RicochetValue(0),
                    6d,
                    WeaponAttackDistance.Limited(32d)),
                WeaponDeliverySpec.Create(
                    WeaponDeliveryType.Laser,
                    null,
                    null,
                    null,
                    new WeaponLaserDeliverySettings(0.14d),
                    null,
                    WeaponGuidanceSpec.Unguided(),
                    WeaponImpactSpec.Create(
                        true,
                        true,
                        true,
                        true,
                        null,
                        null),
                    WeaponEffects.None()),
                Presentation("automatic-energy-laser", "energy-laser-beam"),
                Drop(
                    "automatic-energy-laser",
                    "epic",
                    58,
                    0.65d,
                    WeaponStrongboxEligibility.FromMinimumTier(7)));
        }

        /// <summary>
        /// The Pulse Shotgun's rolled capacity and shared level remain exact ownership state.
        /// The canonical definition above contains neither value and starts with no augments.
        /// </summary>
        public static CanonicalOwnedWeaponSample PulseShotgunOwnedInstance()
        {
            EquipmentInstance equipment = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.sample-pulse-shotgun"),
                StableId.Parse("equipment.sample-pulse-shotgun"),
                24,
                StableId.Parse("equipment-quality.rare"),
                Array.Empty<AugmentInstance>());
            return OwnedSample(equipment, 2, 5);
        }

        /// <summary>
        /// Installed Deadly, Overclocked, and DoT-enhancing augments belong to this exact owned
        /// instance. Capacity/shared level use the existing generated-signature contract.
        /// </summary>
        public static CanonicalOwnedWeaponSample ChemicalOrbOwnedInstance()
        {
            EquipmentInstance equipment = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.sample-chemical-orb"),
                StableId.Parse("equipment.sample-seeking-chemical-dot-orb"),
                48,
                StableId.Parse("equipment-quality.epic"),
                new[]
                {
                    AugmentInstance.Create(
                        StableId.Parse("augment-instance.sample-deadly"),
                        StableId.Parse("augment.deadly"),
                        1,
                        11),
                    AugmentInstance.Create(
                        StableId.Parse("augment-instance.sample-overclocked"),
                        StableId.Parse("augment.overclocked"),
                        1,
                        11),
                    AugmentInstance.Create(
                        StableId.Parse("augment-instance.sample-chemical-persistence"),
                        StableId.Parse("augment.chemical-persistence"),
                        1,
                        11),
                });
            return OwnedSample(equipment, 3, 11);
        }

        private static CanonicalOwnedWeaponSample OwnedSample(
            EquipmentInstance equipment,
            int capacity,
            int sharedLevel)
        {
            var signature = new GeneratedEquipmentAugmentSignatureV1(
                equipment.InstanceId,
                StableId.Parse("strongbox-instance.sample-authored-contract"),
                StableId.Parse("strongbox-policy.sample-authored-contract"),
                capacity,
                sharedLevel,
                "development-only-sample-policy-fingerprint",
                1);
            return new CanonicalOwnedWeaponSample(equipment, signature);
        }

        private static WeaponImpactSpec StandardTravellingImpact()
        {
            return WeaponImpactSpec.Create(
                true,
                true,
                true,
                true,
                null,
                null);
        }

        private static WeaponPresentation Presentation(
            string weaponKey,
            string deliveryKey)
        {
            return new WeaponPresentation(
                "weapon-art." + weaponKey + ".side-v1",
                "weapon-art." + weaponKey + ".mounted-top-v1",
                "weapon-delivery-art." + deliveryKey + ".v1",
                "weapon-trail-art." + deliveryKey + ".v1",
                "weapon-impact-art." + deliveryKey + ".v1",
                null);
        }

        private static WeaponDropMetadata Drop(
            string equipmentKey,
            string rarityKey,
            int peakLevel,
            double baseWeight,
            WeaponStrongboxEligibility eligibility)
        {
            return new WeaponDropMetadata(
                StableId.Parse("equipment.sample-" + equipmentKey),
                StableId.Parse("weapon-rarity." + rarityKey),
                WeaponDropAvailability.PreviewOnly,
                peakLevel,
                baseWeight,
                eligibility);
        }
    }

    public sealed class CanonicalOwnedWeaponSample
    {
        public CanonicalOwnedWeaponSample(
            EquipmentInstance equipment,
            GeneratedEquipmentAugmentSignatureV1 generatedAugmentSignature)
        {
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            GeneratedAugmentSignature = generatedAugmentSignature
                ?? throw new ArgumentNullException(nameof(generatedAugmentSignature));
        }

        public EquipmentInstance Equipment { get; }
        public GeneratedEquipmentAugmentSignatureV1 GeneratedAugmentSignature { get; }
    }
}

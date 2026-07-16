using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Equipment
{
    public sealed class ArmorAugmentCompatibilityTests
    {
        [Test]
        public void ArmorTargetedAugment_IsValidWithoutWeaponRuntimeReference()
        {
            StableId armorFamily = StableId.Parse("equipment-family.heavy-armor");
            StableId protectiveTag = StableId.Parse("equipment-tag.protective");
            EquipmentQualityTier quality = EquipmentQualityTier.Create(
                StableId.Parse("quality.common"),
                "Common",
                1);
            EquipmentDefinition armor = EquipmentDefinition.Create(
                StableId.Parse("equipment.armor-compatible"),
                EquipmentCategoryIds.Armor,
                armorFamily,
                "Compatible Armor",
                null,
                InclusiveIntRange.Create(1, 250),
                2,
                new[] { quality },
                new[] { protectiveTag });
            AugmentDefinition armorAugment = AugmentDefinition.Create(
                StableId.Parse("augment.armor-plating"),
                StableId.Parse("augment-family.armor-defense"),
                "Armor Plating",
                AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Armor },
                    new[] { armorFamily },
                    new[] { protectiveTag },
                    new StableId[0]),
                new StableId[0],
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 6),
                InclusiveIntRange.Create(1, 24));
            EquipmentCatalogBuildResult catalogResult = EquipmentCatalog.Build(
                new[] { armor },
                new[] { armorAugment });

            Assert.That(catalogResult.IsValid, Is.True);
            Assert.That(armor.RuntimeWeaponReferenceId, Is.Null);

            EquipmentInstance instance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.armor-compatible"),
                armor.DefinitionId,
                90,
                quality.QualityId,
                new[]
                {
                    AugmentInstance.Create(
                        StableId.Parse("augment-instance.armor-plating"),
                        armorAugment.DefinitionId,
                        5,
                        18),
                });

            EquipmentValidationResult validation = catalogResult.Catalog.ValidateInstance(instance);

            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Issues, Is.Empty);
        }
    }
}

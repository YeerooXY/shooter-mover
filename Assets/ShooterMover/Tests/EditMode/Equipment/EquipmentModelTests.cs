using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Equipment;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Equipment
{
    public sealed class EquipmentModelTests
    {
        private static readonly EquipmentQualityTier Common =
            EquipmentQualityTier.Create(StableId.Parse("quality.common"), "Common", 1);
        private static readonly EquipmentQualityTier Mythic =
            EquipmentQualityTier.Create(StableId.Parse("quality.mythic"), "Mythic", 7);
        private static readonly StableId EnergyTag = StableId.Parse("equipment-tag.energy");
        private static readonly StableId ExplosiveTag = StableId.Parse("equipment-tag.explosive");
        private static readonly StableId WeaponFamily = StableId.Parse("equipment-family.energy-rifle");
        private static readonly StableId ArmorFamily = StableId.Parse("equipment-family.heavy-armor");

        [Test]
        public void ExistingFiveWeaponIds_AreReferencedWithoutRuntimeBehaviorDuplication()
        {
            string[] weaponIds =
            {
                "weapon.blaster-machine-gun",
                "weapon.shotgun",
                "weapon.rocket-launcher",
                "weapon.arc-gun",
                "weapon.ricochet-gun",
            };

            List<EquipmentDefinition> definitions = new List<EquipmentDefinition>();
            for (int index = 0; index < weaponIds.Length; index++)
            {
                StableId runtimeId = StableId.Parse(weaponIds[index]);
                definitions.Add(CreateWeapon(
                    "equipment.stage1-weapon-" + index,
                    runtimeId,
                    index,
                    new[] { EnergyTag }));
            }

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                definitions,
                new AugmentDefinition[0]);

            Assert.That(result.IsValid, Is.True, CanonicalIssues(result.Issues));
            Assert.That(result.Catalog.EquipmentDefinitions, Has.Count.EqualTo(5));
            CollectionAssert.AreEquivalent(
                weaponIds,
                result.Catalog.EquipmentDefinitions
                    .Select(value => value.RuntimeWeaponReferenceId.ToString())
                    .ToArray());
            Assert.That(
                typeof(EquipmentDefinition).GetProperties()
                    .Any(property => property.Name.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0
                        || property.Name.IndexOf("Cadence", StringComparison.OrdinalIgnoreCase) >= 0
                        || property.Name.IndexOf("Projectile", StringComparison.OrdinalIgnoreCase) >= 0
                        || property.Name.IndexOf("Mount", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "Equipment metadata must not duplicate weapon-package behavior.");
        }

        [Test]
        public void ArmorFutureCategoriesAndZeroOneManySlots_AreValid()
        {
            EquipmentDefinition armor = CreateArmor("equipment.armor-zero", 0);
            EquipmentDefinition oneSlot = CreateArmor("equipment.armor-one", 1);
            EquipmentDefinition manySlots = CreateArmor("equipment.armor-many", 12);
            EquipmentDefinition futureCategory = EquipmentDefinition.Create(
                StableId.Parse("equipment.future-gadget"),
                StableId.Parse("equipment-category.gadget"),
                StableId.Parse("equipment-family.utility-gadget"),
                "Future Gadget",
                null,
                InclusiveIntRange.Create(1, 500),
                4,
                new[] { Common, Mythic },
                new[] { StableId.Parse("equipment-tag.utility") });

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { manySlots, armor, futureCategory, oneSlot },
                new AugmentDefinition[0]);

            Assert.That(result.IsValid, Is.True, CanonicalIssues(result.Issues));
            Assert.That(result.Catalog.FindEquipmentDefinition(armor.DefinitionId).MaximumAugmentSlots, Is.Zero);
            Assert.That(result.Catalog.FindEquipmentDefinition(oneSlot.DefinitionId).MaximumAugmentSlots, Is.EqualTo(1));
            Assert.That(result.Catalog.FindEquipmentDefinition(manySlots.DefinitionId).MaximumAugmentSlots, Is.EqualTo(12));
            Assert.That(result.Catalog.FindEquipmentDefinition(futureCategory.DefinitionId), Is.Not.Null);
            Assert.That(armor.RuntimeWeaponReferenceId, Is.Null);
        }

        [Test]
        public void ConfiguredMaximaBeyondThreeTiersAndTenLevels_AreAccepted()
        {
            EquipmentDefinition weapon = CreateWeapon(
                "equipment.high-range-weapon",
                StableId.Parse("weapon.arc-gun"),
                8,
                new[] { EnergyTag });
            AugmentDefinition augment = CreateAugment(
                "augment.high-range",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new[] { WeaponFamily },
                new[] { EnergyTag },
                new StableId[0],
                new StableId[0],
                1,
                9,
                1,
                40);
            EquipmentCatalog catalog = BuildCatalog(new[] { weapon }, new[] { augment });
            EquipmentInstance instance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.high-range"),
                weapon.DefinitionId,
                220,
                Mythic.QualityId,
                new[]
                {
                    AugmentInstance.Create(
                        StableId.Parse("augment-instance.high-range"),
                        augment.DefinitionId,
                        8,
                        37),
                });

            EquipmentValidationResult validation = catalog.ValidateInstance(instance);

            Assert.That(validation.IsValid, Is.True, CanonicalIssues(validation.Issues));
            Assert.That(instance.ItemLevel, Is.EqualTo(220));
            Assert.That(instance.Augments[0].Tier, Is.EqualTo(8));
            Assert.That(instance.Augments[0].Level, Is.EqualTo(37));
        }

        [Test]
        public void CategoryFamilyRequiredAndExcludedTagCompatibility_RejectsDeterministically()
        {
            EquipmentDefinition cleanWeapon = CreateWeapon(
                "equipment.clean-energy",
                StableId.Parse("weapon.blaster-machine-gun"),
                3,
                new[] { EnergyTag });
            EquipmentDefinition explosiveWeapon = CreateWeapon(
                "equipment.explosive-energy",
                StableId.Parse("weapon.rocket-launcher"),
                3,
                new[] { EnergyTag, ExplosiveTag });
            EquipmentDefinition armor = CreateArmor("equipment.compatibility-armor", 3);
            AugmentDefinition augment = CreateAugment(
                "augment.energy-focus",
                AugmentDuplicatePolicy.DisallowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new[] { WeaponFamily },
                new[] { EnergyTag },
                new[] { ExplosiveTag },
                new StableId[0],
                1,
                5,
                1,
                20);
            EquipmentCatalog catalog = BuildCatalog(
                new[] { cleanWeapon, explosiveWeapon, armor },
                new[] { augment });
            AugmentInstance installed = AugmentInstance.Create(
                StableId.Parse("augment-instance.compatibility"),
                augment.DefinitionId,
                2,
                4);

            EquipmentValidationResult armorResult = catalog.ValidateInstance(
                EquipmentInstance.Create(
                    StableId.Parse("equipment-instance.compatibility-armor"),
                    armor.DefinitionId,
                    10,
                    Common.QualityId,
                    new[] { installed }));
            EquipmentValidationResult explosiveResult = catalog.ValidateInstance(
                EquipmentInstance.Create(
                    StableId.Parse("equipment-instance.compatibility-explosive"),
                    explosiveWeapon.DefinitionId,
                    10,
                    Common.QualityId,
                    new[] { installed }));

            AssertIssue(armorResult, EquipmentModelIssueCode.IncompatibleAugmentCategory);
            AssertIssue(armorResult, EquipmentModelIssueCode.IncompatibleAugmentFamily);
            AssertIssue(armorResult, EquipmentModelIssueCode.MissingRequiredEquipmentTag);
            AssertIssue(explosiveResult, EquipmentModelIssueCode.ExcludedEquipmentTag);
        }

        [Test]
        public void DuplicatePolicyAndExclusionGroups_RejectImpossiblePairs()
        {
            EquipmentDefinition weapon = CreateWeapon(
                "equipment.duplicate-policy",
                StableId.Parse("weapon.shotgun"),
                4,
                new[] { EnergyTag });
            StableId exclusion = StableId.Parse("augment-exclusion.damage-channel");
            AugmentDefinition first = CreateAugment(
                "augment.damage-alpha",
                AugmentDuplicatePolicy.DisallowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new[] { exclusion },
                1,
                4,
                1,
                12);
            AugmentDefinition second = CreateAugment(
                "augment.damage-beta",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new[] { exclusion },
                1,
                4,
                1,
                12);
            EquipmentCatalog catalog = BuildCatalog(new[] { weapon }, new[] { first, second });
            EquipmentInstance instance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.duplicate-policy"),
                weapon.DefinitionId,
                25,
                Common.QualityId,
                new[]
                {
                    AugmentInstance.Create(StableId.Parse("augment-instance.alpha-one"), first.DefinitionId, 1, 1),
                    AugmentInstance.Create(StableId.Parse("augment-instance.alpha-two"), first.DefinitionId, 2, 2),
                    AugmentInstance.Create(StableId.Parse("augment-instance.beta"), second.DefinitionId, 1, 1),
                });

            EquipmentValidationResult result = catalog.ValidateInstance(instance);

            AssertIssue(result, EquipmentModelIssueCode.DuplicateAugmentNotAllowed);
            AssertIssue(result, EquipmentModelIssueCode.ExclusionGroupConflict);
        }

        [Test]
        public void CanonicalCatalogAndInstanceFingerprints_AreStableAcrossInputOrder()
        {
            EquipmentDefinition weaponA = CreateWeapon(
                "equipment.canonical-a",
                StableId.Parse("weapon.arc-gun"),
                3,
                new[] { ExplosiveTag, EnergyTag });
            EquipmentDefinition weaponB = CreateWeapon(
                "equipment.canonical-b",
                StableId.Parse("weapon.ricochet-gun"),
                3,
                new[] { EnergyTag });
            AugmentDefinition augmentA = CreateAugment(
                "augment.canonical-a",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                6,
                1,
                30);
            AugmentDefinition augmentB = CreateAugment(
                "augment.canonical-b",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                6,
                1,
                30);

            EquipmentCatalog firstCatalog = BuildCatalog(
                new[] { weaponB, weaponA },
                new[] { augmentB, augmentA });
            EquipmentCatalog secondCatalog = BuildCatalog(
                new[] { weaponA, weaponB },
                new[] { augmentA, augmentB });
            AugmentInstance firstAugment = AugmentInstance.Create(
                StableId.Parse("augment-instance.canonical-a"), augmentA.DefinitionId, 2, 7);
            AugmentInstance secondAugment = AugmentInstance.Create(
                StableId.Parse("augment-instance.canonical-b"), augmentB.DefinitionId, 3, 9);
            EquipmentInstance firstInstance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.canonical"),
                weaponA.DefinitionId,
                100,
                Mythic.QualityId,
                new[] { secondAugment, firstAugment });
            EquipmentInstance secondInstance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.canonical"),
                weaponA.DefinitionId,
                100,
                Mythic.QualityId,
                new[] { firstAugment, secondAugment });

            Assert.That(firstCatalog.Fingerprint, Is.EqualTo(secondCatalog.Fingerprint));
            Assert.That(firstCatalog.CanonicalText, Is.EqualTo(secondCatalog.CanonicalText));
            Assert.That(firstInstance.Fingerprint, Is.EqualTo(secondInstance.Fingerprint));
            Assert.That(firstInstance.ToCanonicalString(), Is.EqualTo(secondInstance.ToCanonicalString()));
            EquipmentCatalogSnapshot snapshot = EquipmentCatalogSnapshot.FromCatalog(firstCatalog);
            Assert.That(snapshot.Fingerprint, Is.EqualTo(firstCatalog.Fingerprint));
        }

        [Test]
        public void ImmutableAugmentReplacement_LeavesOriginalInstanceUntouched()
        {
            EquipmentDefinition weapon = CreateWeapon(
                "equipment.immutable-replacement",
                StableId.Parse("weapon.blaster-machine-gun"),
                2,
                new[] { EnergyTag });
            AugmentDefinition augment = CreateAugment(
                "augment.immutable-replacement",
                AugmentDuplicatePolicy.DisallowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                5,
                1,
                25);
            EquipmentCatalog catalog = BuildCatalog(new[] { weapon }, new[] { augment });
            AugmentInstance originalAugment = AugmentInstance.Create(
                StableId.Parse("augment-instance.immutable-replacement"),
                augment.DefinitionId,
                2,
                5);
            EquipmentInstance original = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.immutable-replacement"),
                weapon.DefinitionId,
                50,
                Common.QualityId,
                new[] { originalAugment });

            EquipmentInstance replacement = original.ReplaceAugment(originalAugment.WithLevel(6));

            Assert.That(original.Augments[0].Level, Is.EqualTo(5));
            Assert.That(replacement.Augments[0].Level, Is.EqualTo(6));
            Assert.That(replacement, Is.Not.SameAs(original));
            Assert.That(replacement.InstanceId, Is.EqualTo(original.InstanceId));
            Assert.That(replacement.Fingerprint, Is.Not.EqualTo(original.Fingerprint));
            Assert.That(catalog.ValidateInstance(original).IsValid, Is.True);
            Assert.That(catalog.ValidateInstance(replacement).IsValid, Is.True);
        }

        [Test]
        public void MalformedStableIdsDuplicateIdsAndInvalidRanges_Reject()
        {
            EquipmentDefinitionAsset asset = ScriptableObject.CreateInstance<EquipmentDefinitionAsset>();
            try
            {
                SetPrivate(asset, "definitionId", "Equipment.Bad");
                EquipmentAuthoringConversionResult<EquipmentDefinition> conversion = asset.BuildDefinition();
                Assert.That(conversion.IsValid, Is.False);
                Assert.That(conversion.Errors.Any(value => value.Contains("definition_id")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }

            EquipmentDefinition invalidRange = EquipmentDefinition.Create(
                StableId.Parse("equipment.invalid-range"),
                EquipmentCategoryIds.Armor,
                ArmorFamily,
                "Invalid Range",
                null,
                InclusiveIntRange.Create(10, 2),
                0,
                new[] { Common },
                new StableId[0]);
            EquipmentDefinition duplicateA = CreateArmor("equipment.duplicate-id", 0);
            EquipmentDefinition duplicateB = CreateArmor("equipment.duplicate-id", 1);
            AugmentDefinition duplicateAugmentA = CreateAugment(
                "augment.duplicate-id",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                2,
                1,
                2);
            AugmentDefinition duplicateAugmentB = CreateAugment(
                "augment.duplicate-id",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                2,
                1,
                2);

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { invalidRange, duplicateA, duplicateB },
                new[] { duplicateAugmentA, duplicateAugmentB });

            Assert.That(result.IsValid, Is.False);
            AssertIssue(result, EquipmentModelIssueCode.InvalidItemLevelRange);
            AssertIssue(result, EquipmentModelIssueCode.DuplicateEquipmentDefinitionId);
            AssertIssue(result, EquipmentModelIssueCode.DuplicateAugmentDefinitionId);
            StableId malformed;
            Assert.That(StableId.TryParse("weapon.bad_id", out malformed), Is.False);
        }

        [Test]
        public void ImpossibleSlotContents_RejectInCanonicalOrder()
        {
            EquipmentDefinition weapon = CreateWeapon(
                "equipment.impossible-slots",
                StableId.Parse("weapon.shotgun"),
                1,
                new[] { EnergyTag });
            AugmentDefinition augment = CreateAugment(
                "augment.impossible-slots",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                2,
                1,
                3);
            EquipmentCatalog catalog = BuildCatalog(new[] { weapon }, new[] { augment });
            StableId duplicateInstanceId = StableId.Parse("augment-instance.duplicate-slot");
            AugmentInstance known = AugmentInstance.Create(
                duplicateInstanceId,
                augment.DefinitionId,
                7,
                9);
            AugmentInstance unknown = AugmentInstance.Create(
                duplicateInstanceId,
                StableId.Parse("augment.unknown"),
                1,
                1);
            EquipmentInstance first = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.impossible-slots"),
                weapon.DefinitionId,
                20,
                Common.QualityId,
                new[] { unknown, known });
            EquipmentInstance second = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.impossible-slots"),
                weapon.DefinitionId,
                20,
                Common.QualityId,
                new[] { known, unknown });

            EquipmentValidationResult firstResult = catalog.ValidateInstance(first);
            EquipmentValidationResult secondResult = catalog.ValidateInstance(second);

            AssertIssue(firstResult, EquipmentModelIssueCode.AugmentSlotCapacityExceeded);
            AssertIssue(firstResult, EquipmentModelIssueCode.DuplicateAugmentInstanceId);
            AssertIssue(firstResult, EquipmentModelIssueCode.UnknownAugmentDefinition);
            AssertIssue(firstResult, EquipmentModelIssueCode.AugmentTierOutOfRange);
            AssertIssue(firstResult, EquipmentModelIssueCode.AugmentLevelOutOfRange);
            CollectionAssert.AreEqual(
                firstResult.Issues.Select(value => value.ToString()).ToArray(),
                secondResult.Issues.Select(value => value.ToString()).ToArray());
        }

        [Test]
        public void CatalogRejectsImpossibleCompatibilityBeforeGeneration()
        {
            EquipmentDefinition armor = CreateArmor("equipment.only-armor", 2);
            AugmentDefinition weaponOnly = CreateAugment(
                "augment.weapon-only-without-weapon",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Weapon },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                3,
                1,
                10);

            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { armor },
                new[] { weaponOnly });

            Assert.That(result.IsValid, Is.False);
            AssertIssue(result, EquipmentModelIssueCode.ImpossibleAugmentCompatibility);
        }

        private static EquipmentCatalog BuildCatalog(
            IEnumerable<EquipmentDefinition> equipment,
            IEnumerable<AugmentDefinition> augments)
        {
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(equipment, augments);
            Assert.That(result.IsValid, Is.True, CanonicalIssues(result.Issues));
            return result.Catalog;
        }

        private static EquipmentDefinition CreateWeapon(
            string definitionId,
            StableId runtimeWeaponId,
            int maximumSlots,
            IEnumerable<StableId> tags)
        {
            return EquipmentDefinition.Create(
                StableId.Parse(definitionId),
                EquipmentCategoryIds.Weapon,
                WeaponFamily,
                definitionId,
                runtimeWeaponId,
                InclusiveIntRange.Create(1, 300),
                maximumSlots,
                new[] { Mythic, Common },
                tags);
        }

        private static EquipmentDefinition CreateArmor(string definitionId, int maximumSlots)
        {
            return EquipmentDefinition.Create(
                StableId.Parse(definitionId),
                EquipmentCategoryIds.Armor,
                ArmorFamily,
                definitionId,
                null,
                InclusiveIntRange.Create(1, 300),
                maximumSlots,
                new[] { Common, Mythic },
                new[] { StableId.Parse("equipment-tag.protective") });
        }

        private static AugmentDefinition CreateAugment(
            string definitionId,
            AugmentDuplicatePolicy duplicatePolicy,
            IEnumerable<StableId> categories,
            IEnumerable<StableId> families,
            IEnumerable<StableId> requiredTags,
            IEnumerable<StableId> excludedTags,
            IEnumerable<StableId> exclusionGroups,
            int minimumTier,
            int maximumTier,
            int minimumLevel,
            int maximumLevel)
        {
            return AugmentDefinition.Create(
                StableId.Parse(definitionId),
                StableId.Parse("augment-family.general"),
                definitionId,
                AugmentCompatibility.Create(categories, families, requiredTags, excludedTags),
                exclusionGroups,
                duplicatePolicy,
                InclusiveIntRange.Create(minimumTier, maximumTier),
                InclusiveIntRange.Create(minimumLevel, maximumLevel));
        }

        private static void AssertIssue(EquipmentValidationResult result, EquipmentModelIssueCode code)
        {
            Assert.That(
                result.Issues.Any(value => value.Code == code),
                Is.True,
                "Missing issue " + code + ": " + CanonicalIssues(result.Issues));
        }

        private static void AssertIssue(EquipmentCatalogBuildResult result, EquipmentModelIssueCode code)
        {
            Assert.That(
                result.Issues.Any(value => value.Code == code),
                Is.True,
                "Missing issue " + code + ": " + CanonicalIssues(result.Issues));
        }

        private static string CanonicalIssues(IReadOnlyList<EquipmentModelIssue> issues)
        {
            return string.Join("\n", issues.Select(value => value.ToString()).ToArray());
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing serialized field " + fieldName);
            field.SetValue(target, value);
        }
    }
}

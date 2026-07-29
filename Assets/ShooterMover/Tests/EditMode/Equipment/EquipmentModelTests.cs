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
        private static readonly StableId GunFamily = StableId.Parse("equipment-family.energy-rifle");
        private static readonly StableId ArmorFamily = StableId.Parse("equipment-family.heavy-armor");

        [Test]
        public void ExistingFiveGunIds_AreReferencedWithoutRuntimeBehaviorDuplication()
        {
            string[] gunIds =
            {
                "gun.blaster-machine-gun",
                "gun.shotgun",
                "gun.rocket-launcher",
                "gun.arc-gun",
                "gun.ricochet-gun",
            };

            List<EquipmentDefinition> definitions = new List<EquipmentDefinition>();
            for (int index = 0; index < gunIds.Length; index++)
            {
                StableId runtimeId = StableId.Parse(gunIds[index]);
                definitions.Add(CreateGun(
                    "equipment.stage1-gun-" + index,
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
                gunIds,
                result.Catalog.EquipmentDefinitions
                    .Select(value => value.RuntimeGunReferenceId.ToString())
                    .ToArray());
            Assert.That(
                typeof(EquipmentDefinition).GetProperties()
                    .Any(property => property.Name.IndexOf("Damage", StringComparison.OrdinalIgnoreCase) >= 0
                        || property.Name.IndexOf("Cadence", StringComparison.OrdinalIgnoreCase) >= 0
                        || property.Name.IndexOf("Projectile", StringComparison.OrdinalIgnoreCase) >= 0
                        || property.Name.IndexOf("Mount", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "Equipment metadata must not duplicate gun-package behavior.");
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
            Assert.That(armor.RuntimeGunReferenceId, Is.Null);
        }

        [Test]
        public void ConfiguredMaximaBeyondThreeTiersAndTenLevels_AreAccepted()
        {
            EquipmentDefinition gun = CreateGun(
                "equipment.high-range-gun",
                StableId.Parse("gun.arc-gun"),
                8,
                new[] { EnergyTag });
            AugmentDefinition augment = CreateAugment(
                "augment.high-range",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Gun },
                new[] { GunFamily },
                new[] { EnergyTag },
                new StableId[0],
                new StableId[0],
                1,
                9,
                1,
                40);
            EquipmentCatalog catalog = BuildCatalog(new[] { gun }, new[] { augment });
            EquipmentInstance instance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.high-range"),
                gun.DefinitionId,
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
            EquipmentDefinition cleanGun = CreateGun(
                "equipment.clean-energy",
                StableId.Parse("gun.blaster-machine-gun"),
                3,
                new[] { EnergyTag });
            EquipmentDefinition explosiveGun = CreateGun(
                "equipment.explosive-energy",
                StableId.Parse("gun.rocket-launcher"),
                3,
                new[] { EnergyTag, ExplosiveTag });
            EquipmentDefinition armor = CreateArmor("equipment.compatibility-armor", 3);
            AugmentDefinition augment = CreateAugment(
                "augment.energy-focus",
                AugmentDuplicatePolicy.DisallowSameDefinition,
                new[] { EquipmentCategoryIds.Gun },
                new[] { GunFamily },
                new[] { EnergyTag },
                new[] { ExplosiveTag },
                new StableId[0],
                1,
                5,
                1,
                20);
            EquipmentCatalog catalog = BuildCatalog(
                new[] { cleanGun, explosiveGun, armor },
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
                    explosiveGun.DefinitionId,
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
            EquipmentDefinition gun = CreateGun(
                "equipment.duplicate-policy",
                StableId.Parse("gun.shotgun"),
                4,
                new[] { EnergyTag });
            StableId exclusion = StableId.Parse("augment-exclusion.damage-channel");
            AugmentDefinition first = CreateAugment(
                "augment.damage-alpha",
                AugmentDuplicatePolicy.DisallowSameDefinition,
                new[] { EquipmentCategoryIds.Gun },
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
                new[] { EquipmentCategoryIds.Gun },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new[] { exclusion },
                1,
                4,
                1,
                12);
            EquipmentCatalog catalog = BuildCatalog(new[] { gun }, new[] { first, second });
            EquipmentInstance instance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.duplicate-policy"),
                gun.DefinitionId,
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
            EquipmentDefinition gunA = CreateGun(
                "equipment.canonical-a",
                StableId.Parse("gun.arc-gun"),
                3,
                new[] { ExplosiveTag, EnergyTag });
            EquipmentDefinition gunB = CreateGun(
                "equipment.canonical-b",
                StableId.Parse("gun.ricochet-gun"),
                3,
                new[] { EnergyTag });
            AugmentDefinition augmentA = CreateAugment(
                "augment.canonical-a",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Gun },
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
                new[] { EquipmentCategoryIds.Gun },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                6,
                1,
                30);

            EquipmentCatalog firstCatalog = BuildCatalog(
                new[] { gunB, gunA },
                new[] { augmentB, augmentA });
            EquipmentCatalog secondCatalog = BuildCatalog(
                new[] { gunA, gunB },
                new[] { augmentA, augmentB });
            AugmentInstance firstAugment = AugmentInstance.Create(
                StableId.Parse("augment-instance.canonical-a"), augmentA.DefinitionId, 2, 7);
            AugmentInstance secondAugment = AugmentInstance.Create(
                StableId.Parse("augment-instance.canonical-b"), augmentB.DefinitionId, 3, 9);
            EquipmentInstance firstInstance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.canonical"),
                gunA.DefinitionId,
                100,
                Mythic.QualityId,
                new[] { secondAugment, firstAugment });
            EquipmentInstance secondInstance = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.canonical"),
                gunA.DefinitionId,
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
            EquipmentDefinition gun = CreateGun(
                "equipment.immutable-replacement",
                StableId.Parse("gun.blaster-machine-gun"),
                2,
                new[] { EnergyTag });
            AugmentDefinition augment = CreateAugment(
                "augment.immutable-replacement",
                AugmentDuplicatePolicy.DisallowSameDefinition,
                new[] { EquipmentCategoryIds.Gun },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                5,
                1,
                25);
            EquipmentCatalog catalog = BuildCatalog(new[] { gun }, new[] { augment });
            AugmentInstance originalAugment = AugmentInstance.Create(
                StableId.Parse("augment-instance.immutable-replacement"),
                augment.DefinitionId,
                2,
                5);
            EquipmentInstance original = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.immutable-replacement"),
                gun.DefinitionId,
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
            Assert.That(StableId.TryParse("gun.bad_id", out malformed), Is.False);
        }

        [Test]
        public void ImpossibleSlotContents_RejectInCanonicalOrder()
        {
            EquipmentDefinition gun = CreateGun(
                "equipment.impossible-slots",
                StableId.Parse("gun.shotgun"),
                1,
                new[] { EnergyTag });
            AugmentDefinition augment = CreateAugment(
                "augment.impossible-slots",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Gun },
                new StableId[0],
                new StableId[0],
                new StableId[0],
                new StableId[0],
                1,
                2,
                1,
                3);
            EquipmentCatalog catalog = BuildCatalog(new[] { gun }, new[] { augment });
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
                gun.DefinitionId,
                20,
                Common.QualityId,
                new[] { unknown, known });
            EquipmentInstance second = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.impossible-slots"),
                gun.DefinitionId,
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
            AugmentDefinition gunOnly = CreateAugment(
                "augment.gun-only-without-gun",
                AugmentDuplicatePolicy.AllowSameDefinition,
                new[] { EquipmentCategoryIds.Gun },
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
                new[] { gunOnly });

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

        private static EquipmentDefinition CreateGun(
            string definitionId,
            StableId runtimeGunId,
            int maximumSlots,
            IEnumerable<StableId> tags)
        {
            return EquipmentDefinition.Create(
                StableId.Parse(definitionId),
                EquipmentCategoryIds.Gun,
                GunFamily,
                definitionId,
                runtimeGunId,
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

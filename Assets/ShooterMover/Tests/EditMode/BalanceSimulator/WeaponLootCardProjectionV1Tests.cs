using System;
using System.Globalization;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed partial class LootboxSimulatorRuntimeV1Tests
    {
        [Test]
        public void GeneratedWeaponResolvesThroughExactEquipmentRuntimeReferenceChain()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            LootboxGeneratedItemV1 generated =
                runtime.Generate(5, 30, 71001UL, 0);

            WeaponLootCardProjectionV1 card =
                Project(runtime, generated.Equipment);
            EquipmentDefinition equipmentDefinition =
                runtime.EquipmentCatalog.FindEquipmentDefinition(
                    generated.Equipment.DefinitionId);

            Assert.That(
                card.EquipmentDefinitionId,
                Is.EqualTo(generated.Equipment.DefinitionId));
            Assert.That(equipmentDefinition, Is.Not.Null);
            Assert.That(
                card.RuntimeWeaponReferenceId,
                Is.EqualTo(
                    equipmentDefinition.RuntimeWeaponReferenceId));
            Assert.That(
                card.WeaponDefinitionId,
                Is.EqualTo(generated.SourceDefinitionId));
        }

        [Test]
        public void DisplayNameAndMarkAreComposedExactlyOnce()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            EquipmentInstance item = CreateImportedFixtureItem(
                "family_000.mk2",
                "equipment-instance.card-mark",
                2);

            WeaponLootCardProjectionV1 card =
                Project(runtime, item);

            Assert.That(
                card.DisplayName,
                Is.EqualTo("Family 000 MK II"));
            Assert.That(
                CountOccurrences(card.DisplayName, "MK"),
                Is.EqualTo(1));
        }

        [Test]
        public void ExactGeneratedQualityUsesCanonicalLabelNotRawStableId()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            LootboxGeneratedItemV1 generated =
                runtime.Generate(3, 30, 71002UL, 0);

            WeaponLootCardProjectionV1 card =
                Project(runtime, generated.Equipment);

            Assert.That(card.QualityLabel, Is.EqualTo("Common"));
            Assert.That(
                card.ToPrimaryCardText(),
                Does.Contain("COMMON"));
            Assert.That(
                card.ToPrimaryCardText(),
                Does.Not.Contain("quality.common"));
        }

        [Test]
        public void SingleProjectileDamageIsOneValueAndProjectileRowIsHidden()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            EquipmentInstance item = CreateImportedFixtureItem(
                "family_000.mk1",
                "equipment-instance.card-single",
                1);

            WeaponLootCardProjectionV1 card =
                Project(runtime, item);

            Assert.That(card.DamageText, Does.Not.Contain("×"));
            Assert.That(card.ShowsProjectileCount, Is.False);
            Assert.That(card.ProjectileCountText, Is.Empty);
            Assert.That(
                card.ToPrimaryCardText(),
                Does.Not.Contain("Projectiles:"));
        }

        [Test]
        public void MultiProjectileDamageShowsMultiplierAndProjectileRow()
        {
            EquipmentCatalog equipmentCatalog =
                ProductionStarterWeaponCatalogV1
                    .BuildEquipmentCatalog();
            WeaponCatalog weaponCatalog =
                ProductionStarterWeaponCatalogV1
                    .BuildWeaponCatalog();
            EquipmentInstance item = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.card-multi"),
                ProductionStarterWeaponCatalogV1
                    .ShotgunEquipmentDefinitionStableId,
                1,
                StableId.Parse("equipment-quality.common"),
                Array.Empty<AugmentInstance>());

            WeaponLootCardProjectionV1 card = Project(
                equipmentCatalog,
                weaponCatalog,
                item);

            Assert.That(card.DamageText, Is.EqualTo("3 × 7"));
            Assert.That(card.ShowsProjectileCount, Is.True);
            Assert.That(card.ProjectileCountText, Is.EqualTo("7"));
            Assert.That(
                card.ToPrimaryCardText(),
                Does.Contain("Projectiles: 7"));
        }

        [Test]
        public void PierceIsHiddenAtZeroAndShownAboveZero()
        {
            LootboxSimulatorRuntimeV1 withPierce =
                CreateRuntime();
            EquipmentInstance importedItem = CreateImportedFixtureItem(
                "family_000.mk1",
                "equipment-instance.card-pierce",
                1);
            WeaponLootCardProjectionV1 shown =
                Project(withPierce, importedItem);

            EquipmentCatalog starterEquipmentCatalog =
                ProductionStarterWeaponCatalogV1
                    .BuildEquipmentCatalog();
            WeaponCatalog starterWeaponCatalog =
                ProductionStarterWeaponCatalogV1
                    .BuildWeaponCatalog();
            EquipmentInstance shotgun = EquipmentInstance.Create(
                StableId.Parse(
                    "equipment-instance.card-no-pierce"),
                ProductionStarterWeaponCatalogV1
                    .ShotgunEquipmentDefinitionStableId,
                1,
                StableId.Parse("equipment-quality.common"),
                Array.Empty<AugmentInstance>());
            WeaponLootCardProjectionV1 hidden = Project(
                starterEquipmentCatalog,
                starterWeaponCatalog,
                shotgun);

            Assert.That(shown.ShowsPierce, Is.True);
            Assert.That(shown.PierceText, Is.EqualTo("1"));
            Assert.That(hidden.ShowsPierce, Is.False);
            Assert.That(hidden.PierceText, Is.Empty);
            Assert.That(
                hidden.ToPrimaryCardText(),
                Does.Not.Contain("Pierce:"));
        }

        [Test]
        public void ShotsPerSecondAndDpsComeFromWeaponDefinitionProjection()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            EquipmentInstance item = CreateImportedFixtureItem(
                "family_000.mk3",
                "equipment-instance.card-stats",
                3);
            WeaponLootCardProjectionV1 card =
                Project(runtime, item);

            WeaponDefinitionData weapon;
            Assert.That(
                runtime.WeaponCatalog.TryGetDefinition(
                    card.WeaponDefinitionId,
                    out weapon),
                Is.True);
            Assert.That(
                double.Parse(
                    card.ShotsPerSecondText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture),
                Is.EqualTo(weapon.FireRate));
            Assert.That(
                double.Parse(
                    card.DpsText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture),
                Is.EqualTo(weapon.TargetDps).Within(0.01d));
        }

        [Test]
        public void FreshStrongboxEquipmentHasNoInstalledAugmentsButShowsDefinitionCapacity()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            LootboxGeneratedItemV1 generated =
                runtime.Generate(11, 30, 71003UL, 0);
            WeaponLootCardProjectionV1 card =
                Project(runtime, generated.Equipment);
            EquipmentDefinition definition =
                runtime.EquipmentCatalog.FindEquipmentDefinition(
                    generated.Equipment.DefinitionId);

            Assert.That(
                generated.Equipment.Augments.Count,
                Is.Zero);
            Assert.That(definition, Is.Not.Null);
            Assert.That(
                card.AugmentCapacity,
                Is.EqualTo(definition.MaximumAugmentSlots));
            Assert.That(card.AugmentCapacity, Is.EqualTo(3));
            Assert.That(
                card.AugmentSymbols,
                Is.EqualTo("◇ ◇ ◇"));
        }

        [Test]
        public void ZeroCapacityWeaponShowsNoEmptyAugmentSymbols()
        {
            EquipmentCatalog equipmentCatalog =
                ProductionStarterWeaponCatalogV1
                    .BuildEquipmentCatalog();
            WeaponCatalog weaponCatalog =
                ProductionStarterWeaponCatalogV1
                    .BuildWeaponCatalog();
            EquipmentInstance item = EquipmentInstance.Create(
                StableId.Parse(
                    "equipment-instance.card-zero-capacity"),
                ProductionStarterWeaponCatalogV1
                    .BlasterEquipmentDefinitionStableId,
                1,
                StableId.Parse("equipment-quality.common"),
                Array.Empty<AugmentInstance>());

            WeaponLootCardProjectionV1 card = Project(
                equipmentCatalog,
                weaponCatalog,
                item);

            Assert.That(card.AugmentCapacity, Is.Zero);
            Assert.That(card.AugmentSymbols, Is.Empty);
            Assert.That(
                card.ToPrimaryCardText(),
                Does.Not.Contain("◇"));
        }

        [Test]
        public void PrimaryCardOmitsAugmentIdentityTierAndLevel()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            LootboxGeneratedItemV1 generated =
                runtime.Generate(8, 30, 71004UL, 0);
            WeaponLootCardProjectionV1 card =
                Project(runtime, generated.Equipment);
            string primary = card.ToPrimaryCardText();

            Assert.That(
                primary,
                Does.Not.Contain("augment.simulator"));
            Assert.That(primary, Does.Not.Contain("Tier"));
            Assert.That(primary, Does.Not.Contain("Level"));
            Assert.That(
                primary,
                Does.Not.Contain(
                    generated.Equipment.InstanceId.ToString()));
            Assert.That(primary, Does.Not.Contain("fingerprint"));
        }

        [Test]
        public void SameBoxContextProducesByteEquivalentGenerationAndCardProjection()
        {
            LootboxSimulatorRuntimeV1 left = CreateRuntime();
            LootboxSimulatorRuntimeV1 right = CreateRuntime();

            LootboxGeneratedItemV1 first =
                left.Generate(7, 30, 71005UL, 4);
            LootboxGeneratedItemV1 replay =
                right.Generate(7, 30, 71005UL, 4);
            WeaponLootCardProjectionV1 firstCard =
                Project(left, first.Equipment);
            WeaponLootCardProjectionV1 replayCard =
                Project(right, replay.Equipment);

            Assert.That(
                replay.Equipment.ToCanonicalString(),
                Is.EqualTo(
                    first.Equipment.ToCanonicalString()));
            Assert.That(
                replayCard.ToCanonicalString(),
                Is.EqualTo(firstCard.ToCanonicalString()));
            Assert.That(
                replayCard.Fingerprint,
                Is.EqualTo(firstCard.Fingerprint));
        }

        [Test]
        public void DifferentInstancesOfSameDefinitionRemainDistinctCardProjections()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            EquipmentInstance first = CreateImportedFixtureItem(
                "family_000.mk1",
                "equipment-instance.card-distinct-a",
                1);
            EquipmentInstance second = CreateImportedFixtureItem(
                "family_000.mk1",
                "equipment-instance.card-distinct-b",
                1);

            WeaponLootCardProjectionV1 firstCard =
                Project(runtime, first);
            WeaponLootCardProjectionV1 secondCard =
                Project(runtime, second);

            Assert.That(
                secondCard.WeaponDefinitionId,
                Is.EqualTo(firstCard.WeaponDefinitionId));
            Assert.That(
                secondCard.DisplayName,
                Is.EqualTo(firstCard.DisplayName));
            Assert.That(
                secondCard.EquipmentInstanceId,
                Is.Not.EqualTo(firstCard.EquipmentInstanceId));
            Assert.That(
                secondCard.Fingerprint,
                Is.Not.EqualTo(firstCard.Fingerprint));
        }

        [Test]
        public void AuthoritativeBoxGenRapResultProjectsWithoutBypassingAuthorities()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime =
                CreateAuthoritativeRuntime();
            AuthoritativeStrongboxPreparedOpenV1 box =
                runtime.PrepareBatch(
                    new[] { 6 },
                    30,
                    71006UL)[0];

            StrongboxOpeningResultRuntimeV1 result =
                runtime.OpenOrRetry(box);
            Assert.That(
                result.Status,
                Is.EqualTo(
                    StrongboxOpeningRuntimeStatusV1.Opened));
            EquipmentInstance equipment =
                runtime.EquipmentFrom(result)[0];
            Assert.That(equipment.Augments.Count, Is.Zero);

            WeaponLootCardProjectionV1 card;
            string diagnostic;
            Assert.That(
                WeaponLootCardProjectionV1.TryCreate(
                    equipment,
                    runtime.EquipmentCatalog,
                    runtime.WeaponCatalog,
                    out card,
                    out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(
                card.WeaponDefinitionId,
                Is.EqualTo(box.CommittedSourceDefinitionId));
        }

        [Test]
        public void UnresolvedEquipmentRuntimeReferenceFailsClosed()
        {
            LootboxSimulatorRuntimeV1 runtime = CreateRuntime();
            EquipmentQualityTier quality =
                EquipmentQualityTier.Create(
                    StableId.Parse("quality.common"),
                    "Common",
                    1);
            EquipmentDefinition definition =
                EquipmentDefinition.Create(
                    StableId.Parse(
                        "equipment.card-unresolved"),
                    EquipmentCategoryIds.Weapon,
                    StableId.Parse(
                        "equipment-family.card-unresolved"),
                    "Unresolved",
                    StableId.Parse("weapon.missing"),
                    InclusiveIntRange.Create(1, 1),
                    2,
                    new[] { quality },
                    Array.Empty<StableId>());
            EquipmentCatalogBuildResult build =
                EquipmentCatalog.Build(
                    new[] { definition },
                    Array.Empty<AugmentDefinition>());
            Assert.That(build.IsValid, Is.True);
            EquipmentInstance item = EquipmentInstance.Create(
                StableId.Parse(
                    "equipment-instance.card-unresolved"),
                definition.DefinitionId,
                1,
                quality.QualityId,
                Array.Empty<AugmentInstance>());

            WeaponLootCardProjectionV1 card;
            string diagnostic;
            Assert.That(
                WeaponLootCardProjectionV1.TryCreate(
                    item,
                    build.Catalog,
                    runtime.WeaponCatalog,
                    out card,
                    out diagnostic),
                Is.False);
            Assert.That(card, Is.Null);
            Assert.That(
                diagnostic,
                Does.StartWith(
                    "weapon-card-runtime-weapon-reference-unresolved:"));
        }

        private static WeaponLootCardProjectionV1 Project(
            LootboxSimulatorRuntimeV1 runtime,
            EquipmentInstance equipment)
        {
            return Project(
                runtime.EquipmentCatalog,
                runtime.WeaponCatalog,
                equipment);
        }

        private static WeaponLootCardProjectionV1 Project(
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            EquipmentInstance equipment)
        {
            WeaponLootCardProjectionV1 card;
            string diagnostic;
            Assert.That(
                WeaponLootCardProjectionV1.TryCreate(
                    equipment,
                    equipmentCatalog,
                    weaponCatalog,
                    out card,
                    out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(card, Is.Not.Null);
            return card;
        }

        private static EquipmentInstance CreateImportedFixtureItem(
            string weaponDefinitionId,
            string equipmentInstanceId,
            int itemLevel)
        {
            StableId equipmentDefinitionId =
                StrongboxCanonicalV1.DeriveId(
                    "weapondefinition",
                    weaponDefinitionId);
            return EquipmentInstance.Create(
                StableId.Parse(equipmentInstanceId),
                equipmentDefinitionId,
                itemLevel,
                StableId.Parse("quality.common"),
                Array.Empty<AugmentInstance>());
        }

        private static int CountOccurrences(
            string value,
            string token)
        {
            int count = 0;
            int start = 0;
            while (true)
            {
                int index = value.IndexOf(
                    token,
                    start,
                    StringComparison.Ordinal);
                if (index < 0)
                {
                    return count;
                }
                count++;
                start = index + token.Length;
            }
        }
    }
}

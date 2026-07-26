using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionExactWeaponInstanceLoadoutTests
    {
        [TestCase("plain-rattler", "augmented-rattler")]
        [TestCase("plain-rattler", "viper")]
        [TestCase("plain-rattler", "ironclad")]
        [TestCase("augmented-rattler", "ironclad")]
        [TestCase("augmented-rattler", "viper")]
        public void DistinctOwnedInstancesCanOccupyBothMounts(
            string leftKey,
            string rightKey)
        {
            Fixture fixture = Fixture.Create();
            InventoryLoadoutScreenServiceV1 service = fixture.Service();

            Assert.That(
                service.TrySelect(
                    InventoryLoadoutSlotIdsV1.WeaponOne,
                    fixture.InstanceId(leftKey)).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged)
                    .Or.EqualTo(InventoryLoadoutScreenStatusV1.NoChange));
            Assert.That(
                service.TrySelect(
                    InventoryLoadoutSlotIdsV1.WeaponFour,
                    fixture.InstanceId(rightKey)).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged)
                    .Or.EqualTo(InventoryLoadoutScreenStatusV1.NoChange));

            InventoryLoadoutScreenResultV1 confirmed = service.Confirm();
            Assert.That(
                confirmed.Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(
                confirmed.RoutePayload.WeaponSlots[0]
                    .EquipmentInstanceStableId,
                Is.EqualTo(fixture.InstanceId(leftKey)));
            Assert.That(
                confirmed.RoutePayload.WeaponSlots[3]
                    .EquipmentInstanceStableId,
                Is.EqualTo(fixture.InstanceId(rightKey)));

            ProductionWeaponMountSetV1 mounts =
                ProductionWeaponMountPolicyV1.BuildMountSet(
                    confirmed.RoutePayload);
            Assert.That(
                mounts.EnabledBindings[0].EquipmentInstanceStableId,
                Is.EqualTo(fixture.InstanceId(leftKey)));
            Assert.That(
                mounts.EnabledBindings[1].EquipmentInstanceStableId,
                Is.EqualTo(fixture.InstanceId(rightKey)));
            Assert.That(
                fixture.Holdings.ExportSnapshot().UniqueHoldings.Count,
                Is.EqualTo(4));
        }

        [Test]
        public void PlainAndAugmentedRattlersRemainDistinctAndCanSwapPositions()
        {
            Fixture fixture = Fixture.Create();
            InventoryLoadoutScreenServiceV1 first = fixture.Service();
            first.TrySelect(
                InventoryLoadoutSlotIdsV1.WeaponFour,
                fixture.AugmentedRattler.InstanceId);
            PlayerRouteProfilePayloadV1 firstPayload =
                first.Confirm().RoutePayload;

            var second = new InventoryLoadoutScreenServiceV1(
                firstPayload,
                fixture.Holdings,
                fixture.CatalogAdapter,
                fixture.Authority);
            Assert.That(
                second.TryUnequip(
                    InventoryLoadoutSlotIdsV1.WeaponFour).Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                second.TrySelect(
                    InventoryLoadoutSlotIdsV1.WeaponOne,
                    fixture.AugmentedRattler.InstanceId).Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                second.TrySelect(
                    InventoryLoadoutSlotIdsV1.WeaponFour,
                    fixture.PlainRattler.InstanceId).Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1.SelectionChanged));

            InventoryLoadoutScreenResultV1 swapped = second.Confirm();
            Assert.That(
                swapped.Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(
                swapped.RoutePayload.WeaponSlots[0]
                    .EquipmentInstanceStableId,
                Is.EqualTo(fixture.AugmentedRattler.InstanceId));
            Assert.That(
                swapped.RoutePayload.WeaponSlots[3]
                    .EquipmentInstanceStableId,
                Is.EqualTo(fixture.PlainRattler.InstanceId));
            Assert.That(
                fixture.PlainRattler.DefinitionId,
                Is.EqualTo(fixture.AugmentedRattler.DefinitionId));
            Assert.That(fixture.PlainRattler.Augments.Count, Is.EqualTo(0));
            Assert.That(
                fixture.AugmentedRattler.Augments.Count,
                Is.EqualTo(1));
            Assert.That(
                fixture.PlainRattler.Fingerprint,
                Is.Not.EqualTo(fixture.AugmentedRattler.Fingerprint));
        }

        [Test]
        public void SameConcreteInstanceCannotOccupyTwoMounts()
        {
            Fixture fixture = Fixture.Create();
            InventoryLoadoutScreenServiceV1 service = fixture.Service();

            InventoryLoadoutScreenResultV1 result = service.TrySelect(
                InventoryLoadoutSlotIdsV1.WeaponFour,
                fixture.PlainRattler.InstanceId);

            Assert.That(
                result.Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1
                        .DuplicateEquipmentInstance));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo(
                    "inventory-loadout-instance-already-selected"));
            Assert.That(
                service.Snapshot.GetSelection(
                    InventoryLoadoutSlotIdsV1.WeaponOne)
                    .EquipmentInstanceStableId,
                Is.EqualTo(fixture.PlainRattler.InstanceId));
        }

        private sealed class Fixture
        {
            private static readonly StableId Common =
                StableId.Parse("equipment-quality.common");
            private static readonly StableId RattlerDefinition =
                StableId.Parse("equipment.test-rattler");
            private static readonly StableId IroncladDefinition =
                StableId.Parse("equipment.test-ironclad");
            private static readonly StableId ViperDefinition =
                StableId.Parse("equipment.test-viper");
            private static readonly StableId AugmentDefinitionId =
                StableId.Parse("augment.test-calibrated");

            private readonly Dictionary<string, StableId> instanceIds;

            private Fixture(
                PlayerHoldingsService holdings,
                ProductionEquipmentCatalogAdapterV1 catalogAdapter,
                ProductionInventoryLoadoutAuthorityV1 authority,
                PlayerRouteProfilePayloadV1 route,
                EquipmentInstance plainRattler,
                EquipmentInstance augmentedRattler,
                EquipmentInstance ironclad,
                EquipmentInstance viper)
            {
                Holdings = holdings;
                CatalogAdapter = catalogAdapter;
                Authority = authority;
                Route = route;
                PlainRattler = plainRattler;
                AugmentedRattler = augmentedRattler;
                instanceIds = new Dictionary<string, StableId>(
                    StringComparer.Ordinal)
                {
                    { "plain-rattler", plainRattler.InstanceId },
                    { "augmented-rattler", augmentedRattler.InstanceId },
                    { "ironclad", ironclad.InstanceId },
                    { "viper", viper.InstanceId },
                };
            }

            public PlayerHoldingsService Holdings { get; }
            public ProductionEquipmentCatalogAdapterV1 CatalogAdapter
            {
                get;
            }
            public ProductionInventoryLoadoutAuthorityV1 Authority { get; }
            public PlayerRouteProfilePayloadV1 Route { get; }
            public EquipmentInstance PlainRattler { get; }
            public EquipmentInstance AugmentedRattler { get; }

            public StableId InstanceId(string key)
            {
                return instanceIds[key];
            }

            public InventoryLoadoutScreenServiceV1 Service()
            {
                return new InventoryLoadoutScreenServiceV1(
                    Route,
                    Holdings,
                    CatalogAdapter,
                    Authority);
            }

            public static Fixture Create()
            {
                EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                    new[]
                    {
                        Weapon(
                            RattlerDefinition,
                            "family.test-rattler",
                            "Rattler",
                            "weapon.test-rattler"),
                        Weapon(
                            IroncladDefinition,
                            "family.test-ironclad",
                            "Ironclad",
                            "weapon.test-ironclad"),
                        Weapon(
                            ViperDefinition,
                            "family.test-viper",
                            "Viper",
                            "weapon.test-viper"),
                    },
                    new[]
                    {
                        CreateAugmentDefinition(),
                    });
                Assert.That(build.IsValid, Is.True);
                var adapter = new ProductionEquipmentCatalogAdapterV1(
                    build.Catalog);
                var holdings = new PlayerHoldingsService(
                    StableId.Parse("authority.test-exact-loadout"),
                    999L,
                    adapter);

                EquipmentInstance plain = EquipmentInstance.Create(
                    StableId.Parse("equipment-instance.test-plain-rattler"),
                    RattlerDefinition,
                    1,
                    Common,
                    Array.Empty<AugmentInstance>());
                EquipmentInstance augmented = EquipmentInstance.Create(
                    StableId.Parse(
                        "equipment-instance.test-augmented-rattler"),
                    RattlerDefinition,
                    1,
                    Common,
                    new[]
                    {
                        AugmentInstance.Create(
                            StableId.Parse(
                                "augment-instance.test-calibrated"),
                            AugmentDefinitionId,
                            1,
                            1),
                    });
                EquipmentInstance ironclad = EquipmentInstance.Create(
                    StableId.Parse("equipment-instance.test-ironclad"),
                    IroncladDefinition,
                    1,
                    Common,
                    Array.Empty<AugmentInstance>());
                EquipmentInstance viper = EquipmentInstance.Create(
                    StableId.Parse("equipment-instance.test-viper"),
                    ViperDefinition,
                    1,
                    Common,
                    Array.Empty<AugmentInstance>());

                Add(holdings, plain, "plain-rattler");
                Add(holdings, augmented, "augmented-rattler");
                Add(holdings, ironclad, "ironclad");
                Add(holdings, viper, "viper");

                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        StableId.Parse("character.test-exact-loadout"),
                        StableId.Parse(
                            ProductionWeaponMountPolicyV1
                                .AggressiveLoadoutProfileId),
                        new StableId[]
                        {
                            plain.InstanceId,
                            null,
                            null,
                            ironclad.InstanceId,
                        });
                var authority =
                    new ProductionInventoryLoadoutAuthorityV1(
                        route,
                        holdings,
                        adapter);
                return new Fixture(
                    holdings,
                    adapter,
                    authority,
                    route,
                    plain,
                    augmented,
                    ironclad,
                    viper);
            }

            private static EquipmentDefinition Weapon(
                StableId definitionStableId,
                string family,
                string displayName,
                string runtimeWeapon)
            {
                return EquipmentDefinition.Create(
                    definitionStableId,
                    EquipmentCategoryIds.Weapon,
                    StableId.Parse(family),
                    displayName,
                    StableId.Parse(runtimeWeapon),
                    InclusiveIntRange.Create(1, 100),
                    2,
                    new[]
                    {
                        EquipmentQualityTier.Create(
                            Common,
                            "Common",
                            1),
                    },
                    Array.Empty<StableId>());
            }

            private static AugmentDefinition CreateAugmentDefinition()
            {
                return ShooterMover.Domain.Equipment.AugmentDefinition.Create(
                    AugmentDefinitionId,
                    StableId.Parse("augment-family.test-calibrated"),
                    "Calibrated",
                    AugmentCompatibility.Create(
                        new[] { EquipmentCategoryIds.Weapon },
                        Array.Empty<StableId>(),
                        Array.Empty<StableId>(),
                        Array.Empty<StableId>()),
                    Array.Empty<StableId>(),
                    AugmentDuplicatePolicy.DisallowSameDefinition,
                    InclusiveIntRange.Create(1, 10),
                    InclusiveIntRange.Create(1, 100));
            }

            private static void Add(
                PlayerHoldingsService holdings,
                EquipmentInstance instance,
                string token)
            {
                PlayerHoldingsMutationResultV1 result = holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        StableId.Parse("transaction.test-" + token),
                        StableId.Parse("operation.test-" + token),
                        holdings.AuthorityStableId,
                        instance,
                        HoldingProvenanceV1.Create(
                            StableId.Parse("grant.test-" + token),
                            StableId.Parse("source.test-loadout")),
                        holdings.Sequence));
                Assert.That(
                    result.Status,
                    Is.EqualTo(
                        PlayerHoldingsMutationStatusV1.Applied));
            }
        }
    }
}

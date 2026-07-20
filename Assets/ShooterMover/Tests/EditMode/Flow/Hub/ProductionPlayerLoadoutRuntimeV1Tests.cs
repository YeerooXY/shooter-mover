using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class ProductionPlayerLoadoutRuntimeV1Tests
    {
        [Test]
        public void StarterRuntimeOwnsFiveDistinctWeaponInstances()
        {
            PlayerRouteProfilePayloadV1 payload = Route("five-owned");
            var runtime = new ProductionPlayerLoadoutRuntimeV1(payload);
            var snapshot = runtime.Holdings.ExportSnapshot();
            var identities = new HashSet<StableId>();

            for (int index = 0;
                index < snapshot.UniqueHoldings.Count;
                index++)
            {
                if (snapshot.UniqueHoldings[index].RewardKind
                    == RewardGrantKindV1.EquipmentReference)
                {
                    identities.Add(
                        snapshot.UniqueHoldings[index]
                            .InstanceStableId);
                }
            }

            Assert.That(identities.Count, Is.EqualTo(5));
            for (int index = 0;
                index < payload.WeaponSlots.Count;
                index++)
            {
                Assert.That(
                    identities.Contains(
                        payload.WeaponSlots[index]
                            .EquipmentInstanceStableId),
                    Is.True);
            }
            Assert.That(
                identities.Contains(
                    runtime.RicochetEquipmentInstanceStableId),
                Is.True);
        }

        [Test]
        public void HubConfirmationEquipsReserveInstanceByExactIdentity()
        {
            PlayerRouteProfilePayloadV1 payload = Route("hub-confirm");
            var runtime = new ProductionPlayerLoadoutRuntimeV1(payload);
            var service = new InventoryLoadoutScreenServiceV1(
                payload,
                runtime.Holdings,
                runtime.CatalogAdapter,
                runtime.LoadoutAuthority);

            InventoryLoadoutScreenResultV1 selection =
                service.TrySelect(
                    InventoryLoadoutSlotIdsV1.WeaponFour,
                    runtime.RicochetEquipmentInstanceStableId);
            InventoryLoadoutScreenResultV1 confirmed =
                service.Confirm();

            Assert.That(
                selection.Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1
                        .SelectionChanged));
            Assert.That(
                confirmed.Status,
                Is.EqualTo(
                    InventoryLoadoutScreenStatusV1.Confirmed));
            Assert.That(
                confirmed.RoutePayload.WeaponSlots[3]
                    .EquipmentInstanceStableId,
                Is.EqualTo(
                    runtime.RicochetEquipmentInstanceStableId));
            Assert.That(
                runtime.LoadoutAuthority.ExportSnapshot()
                    .GetBinding(
                        InventoryLoadoutSlotIdsV1.WeaponFour)
                    .EquipmentInstanceStableId,
                Is.EqualTo(
                    runtime.RicochetEquipmentInstanceStableId));
            Assert.That(
                runtime.Holdings.ExportSnapshot()
                    .UniqueHoldings.Count,
                Is.EqualTo(5));
        }

        [Test]
        public void DirectDuplicateInstanceCommandRejectsWithoutMutation()
        {
            PlayerRouteProfilePayloadV1 payload = Route("duplicate");
            var runtime = new ProductionPlayerLoadoutRuntimeV1(payload);
            InventoryLoadoutAuthoritySnapshotV1 before =
                runtime.LoadoutAuthority.ExportSnapshot();
            var bindings = CopyBindings(before);
            bindings[1] = new InventoryLoadoutSlotBindingV1(
                InventoryLoadoutSlotIdsV1.WeaponTwo,
                bindings[0].EquipmentInstanceStableId);

            InventoryLoadoutAuthorityResultV1 result =
                runtime.LoadoutAuthority.Apply(
                    new InventoryLoadoutAuthorityCommandV1(
                        before.Sequence,
                        runtime.Holdings.Sequence,
                        bindings));

            Assert.That(
                result.Status,
                Is.EqualTo(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .Rejected));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo(
                    "production-loadout-instance-duplicate"));
            Assert.That(
                runtime.LoadoutAuthority.ExportSnapshot().Sequence,
                Is.EqualTo(before.Sequence));
        }

        [Test]
        public void ExactAcceptedCommandReplayDoesNotApplyTwice()
        {
            PlayerRouteProfilePayloadV1 payload = Route("replay");
            var runtime = new ProductionPlayerLoadoutRuntimeV1(payload);
            InventoryLoadoutAuthoritySnapshotV1 before =
                runtime.LoadoutAuthority.ExportSnapshot();
            var bindings = CopyBindings(before);
            bindings[3] = new InventoryLoadoutSlotBindingV1(
                InventoryLoadoutSlotIdsV1.WeaponFour,
                runtime.RicochetEquipmentInstanceStableId);
            var command = new InventoryLoadoutAuthorityCommandV1(
                before.Sequence,
                runtime.Holdings.Sequence,
                bindings);

            InventoryLoadoutAuthorityResultV1 first =
                runtime.LoadoutAuthority.Apply(command);
            InventoryLoadoutAuthorityResultV1 replay =
                runtime.LoadoutAuthority.Apply(command);

            Assert.That(
                first.Status,
                Is.EqualTo(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .Applied));
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .ExactRepeatNoChange));
            Assert.That(
                replay.Snapshot.Sequence,
                Is.EqualTo(first.Snapshot.Sequence));
        }

        [Test]
        public void HoldingsChangeMakesPreparedLoadoutCommandStale()
        {
            PlayerRouteProfilePayloadV1 payload = Route("stale");
            var runtime = new ProductionPlayerLoadoutRuntimeV1(payload);
            InventoryLoadoutAuthoritySnapshotV1 before =
                runtime.LoadoutAuthority.ExportSnapshot();
            var command = new InventoryLoadoutAuthorityCommandV1(
                before.Sequence,
                runtime.Holdings.Sequence,
                CopyBindings(before));

            AddExtraBlaster(runtime);
            InventoryLoadoutAuthorityResultV1 result =
                runtime.LoadoutAuthority.Apply(command);

            Assert.That(
                result.Status,
                Is.EqualTo(
                    InventoryLoadoutAuthorityMutationStatusV1
                        .StaleSnapshot));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo(
                    "production-loadout-holdings-stale"));
            Assert.That(
                result.Snapshot.Sequence,
                Is.EqualTo(before.Sequence));
        }

        private static List<InventoryLoadoutSlotBindingV1>
            CopyBindings(
                InventoryLoadoutAuthoritySnapshotV1 snapshot)
        {
            var bindings =
                new List<InventoryLoadoutSlotBindingV1>();
            for (int index = 0;
                index < snapshot.Bindings.Count;
                index++)
            {
                bindings.Add(
                    new InventoryLoadoutSlotBindingV1(
                        snapshot.Bindings[index].SlotStableId,
                        snapshot.Bindings[index]
                            .EquipmentInstanceStableId));
            }
            return bindings;
        }

        private static void AddExtraBlaster(
            ProductionPlayerLoadoutRuntimeV1 runtime)
        {
            EquipmentInstance instance = EquipmentInstance.Create(
                StableId.Parse(
                    "equipment-instance.production-extra-blaster"),
                ProductionStarterWeaponCatalogV1
                    .BlasterEquipmentDefinitionStableId,
                1,
                StableId.Parse("equipment-quality.common"),
                Array.Empty<AugmentInstance>());
            PlayerHoldingsMutationResultV1 result =
                runtime.Holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        StableId.Parse(
                            "transaction.production-extra-blaster"),
                        StableId.Parse(
                            "operation.production-extra-blaster"),
                        runtime.Holdings.AuthorityStableId,
                        instance,
                        HoldingProvenanceV1.Create(
                            StableId.Parse(
                                "grant.production-extra-blaster"),
                            StableId.Parse(
                                "source.production-loadout-test")),
                        runtime.Holdings.Sequence));
            Assert.That(
                result.Status,
                Is.EqualTo(
                    PlayerHoldingsMutationStatusV1.Applied));
        }

        private static PlayerRouteProfilePayloadV1 Route(
            string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character." + suffix),
                StableId.Parse("loadout-profile." + suffix),
                new[]
                {
                    StableId.Parse(
                        "equipment-instance." + suffix + "-1"),
                    StableId.Parse(
                        "equipment-instance." + suffix + "-2"),
                    StableId.Parse(
                        "equipment-instance." + suffix + "-3"),
                    StableId.Parse(
                        "equipment-instance." + suffix + "-4"),
                });
        }
    }
}

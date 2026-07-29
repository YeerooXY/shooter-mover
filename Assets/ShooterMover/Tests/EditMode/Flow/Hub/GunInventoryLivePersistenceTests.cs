using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class GunInventoryLivePersistenceTests
    {
        [Test]
        public void SerializedSchemaV2RestartPreservesExactFirstMountWithoutGranting()
        {
            var runtime = new PlayerLoadoutLive(Route(
                "restart-source",
                GunMountPolicy.HealerLoadoutProfileId));
            GunSlot firstPosition =
                runtime.MountLayout.Positions[0];
            GunSlot secondPosition =
                runtime.MountLayout.Positions[1];
            StableId firstSlot = firstPosition.LoadoutSlotStableId;
            StableId secondSlot = secondPosition.LoadoutSlotStableId;
            StableId replacement = runtime.MountLoadoutAuthority.ExportSnapshot()
                .Find(secondPosition.MountStableId).InstanceId;

            var inventory = Service(runtime);
            Assert.That(
                inventory.Unequip(secondSlot).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.SelectionChanged));
            inventory.SelectGun(replacement);
            Assert.That(
                inventory.EquipSelected(firstSlot).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.SelectionChanged));
            Assert.That(
                inventory.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));

            PlayerHoldingsSnapshot genericBefore =
                runtime.Holdings.ExportSnapshot();
            GunInventorySnapshot gunsBefore =
                runtime.GunInventory.ExportSnapshot();
            LoadoutSnapshot mountsBefore =
                runtime.MountLoadoutAuthority.ExportSnapshot();
            InventoryLoadoutStateSnapshot armorOnlyBefore =
                LoadoutView.ArmorOnly(
                    runtime.LoadoutAuthority.ExportSnapshot());

            string gunPayload = GunInventorySavePart.Codec.Encode(
                gunsBefore);
            string mountPayload = LoadoutSavePart.Codec.Encode(
                mountsBefore);
            string armorPayload = GameSaveFormats.ExactInstanceLoadout
                .Encode(armorOnlyBefore);

            GunInventorySnapshot decodedGuns;
            LoadoutSnapshot decodedMounts;
            InventoryLoadoutStateSnapshot decodedArmor;
            string rejectionCode;
            Assert.That(
                GunInventorySavePart.Codec.TryDecode(
                    gunPayload,
                    out decodedGuns,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                LoadoutSavePart.Codec.TryDecode(
                    mountPayload,
                    out decodedMounts,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                GameSaveFormats.ExactInstanceLoadout.TryDecode(
                    armorPayload,
                    out decodedArmor,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                decodedArmor.Bindings
                    .Where((item, index) =>
                        InventoryLoadoutSlots.All[index].Kind
                            == InventoryLoadoutSlotKind.Gun)
                    .All(item => item.EquipmentInstanceStableId == null),
                Is.True,
                "V2 saves must not persist gun truth in legacy generic slots.");

            PlayerLoadoutLive restored =
                PlayerLoadoutLive.Restore(
                    runtime.RoutePayload.SelectedCharacterStableId,
                    runtime.RoutePayload.LoadoutProfileStableId,
                    genericBefore,
                    decodedGuns,
                    decodedMounts,
                    decodedArmor);

            Assert.That(
                restored.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(genericBefore.Fingerprint));
            Assert.That(
                restored.GunInventory.ExportSnapshot().Fingerprint,
                Is.EqualTo(gunsBefore.Fingerprint));
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(mountsBefore.Fingerprint));
            Assert.That(
                restored.GunInventory.Count,
                Is.EqualTo(gunsBefore.Instances.Count));
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot()
                    .Find(firstPosition.MountStableId).InstanceId,
                Is.EqualTo(replacement));

            GunItem exact;
            Assert.That(
                restored.TryResolveFirstActiveEquippedGun(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact.InstanceId, Is.EqualTo(replacement));

            GunInventorySnapshot beforeOpen =
                restored.GunInventory.ExportSnapshot();
            LoadoutSnapshot mountsBeforeOpen =
                restored.MountLoadoutAuthority.ExportSnapshot();
            Service(restored).Refresh();
            Service(restored).Refresh();
            Assert.That(
                restored.GunInventory.ExportSnapshot().Fingerprint,
                Is.EqualTo(beforeOpen.Fingerprint),
                "Reopening Inventory after restore must never grant guns.");
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(mountsBeforeOpen.Fingerprint),
                "Reopening Inventory must not repair or reorder physical mounts.");
        }

        [Test]
        public void AccountAggregateAcceptsMountV2AndRequiresLegacyGunSlotsEmpty()
        {
            var runtime = new PlayerLoadoutLive(Route(
                "account-semantics",
                GunMountPolicy.DefensiveLoadoutProfileId));
            InventoryLoadoutStateSnapshot armorOnly =
                LoadoutView.ArmorOnly(
                    runtime.LoadoutAuthority.ExportSnapshot());

            SavePartSnapshot holdingsComponent =
                KnownSavePartAdapters.PlayerHoldings(
                    runtime.Holdings.ExportSnapshot,
                    GameSaveFormats.PlayerHoldings.Validate,
                    snapshot => SavePartApplyResult.Applied())
                    .ExportComponent();
            SavePartSnapshot gunsComponent =
                GunInventorySavePart.CreateAdapter(
                    runtime.GunInventory).ExportComponent();
            SavePartSnapshot mountsComponent =
                LoadoutSavePart.CreateAdapter(
                    runtime.MountLoadoutAuthority).ExportComponent();
            SavePartSnapshot armorComponent =
                KnownSavePartAdapters.ExactInstanceLoadout(
                    () => armorOnly,
                    GameSaveFormats.ExactInstanceLoadout.Validate,
                    snapshot => SavePartApplyResult.Applied())
                    .ExportComponent();

            var validCharacter = new CharacterInstanceSnapshot(
                runtime.RoutePayload.SelectedCharacterStableId,
                runtime.RoutePayload.LoadoutProfileStableId,
                0,
                "Canonical Mount Character",
                0L,
                new[]
                {
                    holdingsComponent,
                    gunsComponent,
                    mountsComponent,
                    armorComponent,
                });
            SavePartValidationResult valid =
                GameSaveRules.ValidateCharacter(
                    validCharacter);
            Assert.That(valid.Succeeded, Is.True, valid.RejectionCode);

            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = validCharacter;
            var account = new PlayerAccountSnapshot(
                StableId.Parse("account.gun-mount-v2"),
                0L,
                slots,
                null);
            string accountPayload = PlayerAccountAggregateCodec.Encode(account);
            PlayerAccountSnapshot decodedAccount;
            string accountError;
            Assert.That(
                PlayerAccountAggregateCodec.TryDecode(
                    accountPayload,
                    out decodedAccount,
                    out accountError),
                Is.True,
                accountError);
            SavePartValidationResult accountSemantics =
                GameSaveRules.Validate(decodedAccount);
            Assert.That(
                accountSemantics.Succeeded,
                Is.True,
                accountSemantics.RejectionCode);
            Assert.That(
                decodedAccount.CharacterAt(0).Fingerprint,
                Is.EqualTo(validCharacter.Fingerprint));

            InventoryLoadoutStateSnapshot invalidLegacyGuns =
                runtime.LoadoutAuthority.ExportSnapshot();
            SavePartSnapshot invalidLoadoutComponent =
                KnownSavePartAdapters.ExactInstanceLoadout(
                    () => invalidLegacyGuns,
                    GameSaveFormats.ExactInstanceLoadout.Validate,
                    snapshot => SavePartApplyResult.Applied())
                    .ExportComponent();
            var invalidCharacter = new CharacterInstanceSnapshot(
                runtime.RoutePayload.SelectedCharacterStableId,
                runtime.RoutePayload.LoadoutProfileStableId,
                0,
                "Legacy Gun Slot Conflict",
                0L,
                new[]
                {
                    holdingsComponent,
                    gunsComponent,
                    mountsComponent,
                    invalidLoadoutComponent,
                });
            SavePartValidationResult invalid =
                GameSaveRules.ValidateCharacter(
                    invalidCharacter);
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(
                invalid.RejectionCode,
                Does.StartWith(
                    "legacy-gun-slot-must-be-empty-when-mount-v2-present"));
        }

        private static InventoryMenuActions Service(
            PlayerLoadoutLive runtime)
        {
            LoadoutRegistry.Register(
                runtime.GunInventory,
                runtime.MountLoadoutAuthority);
            return new InventoryMenuActions(
                runtime.CurrentRoutePayload,
                runtime.Holdings,
                runtime.GunInventory,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.GunCatalog);
        }

        private static PlayerRouteProfilePayload Route(
            string suffix,
            string profileId)
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character." + suffix),
                StableId.Parse(profileId),
                new StableId[PlayerRouteProfilePayload.GunSlotCount]);
        }
    }
}

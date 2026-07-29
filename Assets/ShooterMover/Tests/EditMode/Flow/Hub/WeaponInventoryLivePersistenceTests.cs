using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class WeaponInventoryLivePersistenceTests
    {
        [Test]
        public void SerializedSchemaV2RestartPreservesExactFirstMountWithoutGranting()
        {
            var runtime = new PlayerLoadoutLive(Route(
                "restart-source",
                WeaponMountPolicy.HealerLoadoutProfileId));
            WeaponMountPosition firstPosition =
                runtime.MountLayout.Positions[0];
            WeaponMountPosition secondPosition =
                runtime.MountLayout.Positions[1];
            StableId firstSlot = firstPosition.LoadoutSlotStableId;
            StableId secondSlot = secondPosition.LoadoutSlotStableId;
            StableId replacement = runtime.MountLoadoutAuthority.ExportSnapshot()
                .Find(secondPosition.MountStableId).InstanceId;

            var inventory = Service(runtime);
            Assert.That(
                inventory.Unequip(secondSlot).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.SelectionChanged));
            inventory.SelectWeapon(replacement);
            Assert.That(
                inventory.EquipSelected(firstSlot).Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.SelectionChanged));
            Assert.That(
                inventory.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatus.Confirmed));

            PlayerHoldingsSnapshot genericBefore =
                runtime.Holdings.ExportSnapshot();
            WeaponHoldingsSnapshot weaponsBefore =
                runtime.WeaponHoldings.ExportSnapshot();
            WeaponMountLoadoutSnapshot mountsBefore =
                runtime.MountLoadoutAuthority.ExportSnapshot();
            InventoryLoadoutStateSnapshot armorOnlyBefore =
                WeaponMountLoadoutView.ArmorOnly(
                    runtime.LoadoutAuthority.ExportSnapshot());

            string weaponPayload = WeaponHoldingsSaveComponent.Codec.Encode(
                weaponsBefore);
            string mountPayload = WeaponMountLoadoutSaveComponent.Codec.Encode(
                mountsBefore);
            string armorPayload = KnownSaveComponentCodecs.ExactInstanceLoadout
                .Encode(armorOnlyBefore);

            WeaponHoldingsSnapshot decodedWeapons;
            WeaponMountLoadoutSnapshot decodedMounts;
            InventoryLoadoutStateSnapshot decodedArmor;
            string rejectionCode;
            Assert.That(
                WeaponHoldingsSaveComponent.Codec.TryDecode(
                    weaponPayload,
                    out decodedWeapons,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                WeaponMountLoadoutSaveComponent.Codec.TryDecode(
                    mountPayload,
                    out decodedMounts,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                KnownSaveComponentCodecs.ExactInstanceLoadout.TryDecode(
                    armorPayload,
                    out decodedArmor,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(
                decodedArmor.Bindings
                    .Where((item, index) =>
                        InventoryLoadoutSlots.All[index].Kind
                            == InventoryLoadoutSlotKind.Weapon)
                    .All(item => item.EquipmentInstanceStableId == null),
                Is.True,
                "V2 saves must not persist weapon truth in legacy generic slots.");

            PlayerLoadoutLive restored =
                PlayerLoadoutLive.Restore(
                    runtime.RoutePayload.SelectedCharacterStableId,
                    runtime.RoutePayload.LoadoutProfileStableId,
                    genericBefore,
                    decodedWeapons,
                    decodedMounts,
                    decodedArmor);

            Assert.That(
                restored.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(genericBefore.Fingerprint));
            Assert.That(
                restored.WeaponHoldings.ExportSnapshot().Fingerprint,
                Is.EqualTo(weaponsBefore.Fingerprint));
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(mountsBefore.Fingerprint));
            Assert.That(
                restored.WeaponHoldings.Count,
                Is.EqualTo(weaponsBefore.Instances.Count));
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot()
                    .Find(firstPosition.MountStableId).InstanceId,
                Is.EqualTo(replacement));

            WeaponEquipmentInstance exact;
            Assert.That(
                restored.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact.InstanceId, Is.EqualTo(replacement));

            WeaponHoldingsSnapshot beforeOpen =
                restored.WeaponHoldings.ExportSnapshot();
            WeaponMountLoadoutSnapshot mountsBeforeOpen =
                restored.MountLoadoutAuthority.ExportSnapshot();
            Service(restored).Refresh();
            Service(restored).Refresh();
            Assert.That(
                restored.WeaponHoldings.ExportSnapshot().Fingerprint,
                Is.EqualTo(beforeOpen.Fingerprint),
                "Reopening Inventory after restore must never grant weapons.");
            Assert.That(
                restored.MountLoadoutAuthority.ExportSnapshot().Fingerprint,
                Is.EqualTo(mountsBeforeOpen.Fingerprint),
                "Reopening Inventory must not repair or reorder physical mounts.");
        }

        [Test]
        public void AccountAggregateAcceptsMountV2AndRequiresLegacyWeaponSlotsEmpty()
        {
            var runtime = new PlayerLoadoutLive(Route(
                "account-semantics",
                WeaponMountPolicy.DefensiveLoadoutProfileId));
            InventoryLoadoutStateSnapshot armorOnly =
                WeaponMountLoadoutView.ArmorOnly(
                    runtime.LoadoutAuthority.ExportSnapshot());

            SaveComponentSnapshot holdingsComponent =
                KnownSaveComponentAdapters.PlayerHoldings(
                    runtime.Holdings.ExportSnapshot,
                    KnownSaveComponentCodecs.PlayerHoldings.Validate,
                    snapshot => SaveComponentApplyResult.Applied())
                    .ExportComponent();
            SaveComponentSnapshot weaponsComponent =
                WeaponHoldingsSaveComponent.CreateAdapter(
                    runtime.WeaponHoldings).ExportComponent();
            SaveComponentSnapshot mountsComponent =
                WeaponMountLoadoutSaveComponent.CreateAdapter(
                    runtime.MountLoadoutAuthority).ExportComponent();
            SaveComponentSnapshot armorComponent =
                KnownSaveComponentAdapters.ExactInstanceLoadout(
                    () => armorOnly,
                    KnownSaveComponentCodecs.ExactInstanceLoadout.Validate,
                    snapshot => SaveComponentApplyResult.Applied())
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
                    weaponsComponent,
                    mountsComponent,
                    armorComponent,
                });
            SaveComponentValidationResult valid =
                PlayerAccountComponentSemantics.ValidateCharacter(
                    validCharacter);
            Assert.That(valid.Succeeded, Is.True, valid.RejectionCode);

            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = validCharacter;
            var account = new PlayerAccountSnapshot(
                StableId.Parse("account.weapon-mount-v2"),
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
            SaveComponentValidationResult accountSemantics =
                PlayerAccountComponentSemantics.Validate(decodedAccount);
            Assert.That(
                accountSemantics.Succeeded,
                Is.True,
                accountSemantics.RejectionCode);
            Assert.That(
                decodedAccount.CharacterAt(0).Fingerprint,
                Is.EqualTo(validCharacter.Fingerprint));

            InventoryLoadoutStateSnapshot invalidLegacyWeapons =
                runtime.LoadoutAuthority.ExportSnapshot();
            SaveComponentSnapshot invalidLoadoutComponent =
                KnownSaveComponentAdapters.ExactInstanceLoadout(
                    () => invalidLegacyWeapons,
                    KnownSaveComponentCodecs.ExactInstanceLoadout.Validate,
                    snapshot => SaveComponentApplyResult.Applied())
                    .ExportComponent();
            var invalidCharacter = new CharacterInstanceSnapshot(
                runtime.RoutePayload.SelectedCharacterStableId,
                runtime.RoutePayload.LoadoutProfileStableId,
                0,
                "Legacy Weapon Slot Conflict",
                0L,
                new[]
                {
                    holdingsComponent,
                    weaponsComponent,
                    mountsComponent,
                    invalidLoadoutComponent,
                });
            SaveComponentValidationResult invalid =
                PlayerAccountComponentSemantics.ValidateCharacter(
                    invalidCharacter);
            Assert.That(invalid.Succeeded, Is.False);
            Assert.That(
                invalid.RejectionCode,
                Does.StartWith(
                    "legacy-weapon-slot-must-be-empty-when-mount-v2-present"));
        }

        private static WeaponInventoryScreenActions Service(
            PlayerLoadoutLive runtime)
        {
            WeaponMountLoadoutRegistry.Register(
                runtime.WeaponHoldings,
                runtime.MountLoadoutAuthority);
            return new WeaponInventoryScreenActions(
                runtime.CurrentRoutePayload,
                runtime.Holdings,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);
        }

        private static PlayerRouteProfilePayload Route(
            string suffix,
            string profileId)
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character." + suffix),
                StableId.Parse(profileId),
                new StableId[PlayerRouteProfilePayload.WeaponSlotCount]);
        }
    }
}

using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Live
{
    public sealed class CanonicalWeaponGameplayResolutionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ReplacedFirstMountResolvesTheExactCanonicalInstance()
        {
            var runtime = new ProductionPlayerLoadoutRuntimeV1(
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.playmode-exact-weapon"),
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .DefensiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayloadV1.WeaponSlotCount]));
            StableId firstMount = runtime.MountLayout.Positions[0]
                .LoadoutSlotStableId;
            StableId secondMount = runtime.MountLayout.Positions[1]
                .LoadoutSlotStableId;
            StableId replacement = runtime.LoadoutAuthority.ExportSnapshot()
                .GetBinding(secondMount).EquipmentInstanceStableId;
            var inventory = new CanonicalWeaponInventoryScreenServiceV2(
                runtime.CurrentRoutePayload,
                runtime.Holdings,
                runtime.WeaponHoldings,
                runtime.LoadoutAuthority,
                runtime.MountLayout,
                runtime.WeaponCatalog);

            Assert.That(
                inventory.Unequip(secondMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            inventory.SelectWeapon(replacement);
            Assert.That(
                inventory.EquipSelected(firstMount).Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.SelectionChanged));
            Assert.That(
                inventory.Confirm().Status,
                Is.EqualTo(InventoryLoadoutScreenStatusV1.Confirmed));

            WeaponEquipmentInstance exact;
            string rejectionCode;
            Assert.That(
                runtime.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(exact.InstanceId, Is.EqualTo(replacement));

            var lookup = new CanonicalWeaponEquipmentProjectionLookupV2(
                runtime.WeaponHoldings,
                runtime.EquipmentCatalog,
                runtime.Holdings);
            EquipmentInstance projected;
            Assert.That(
                lookup.TryResolve(
                    new EquipmentInstanceId(exact.InstanceId),
                    out projected),
                Is.True);
            Assert.That(projected.InstanceId, Is.EqualTo(exact.InstanceId));
            Assert.That(
                runtime.EquipmentCatalog.FindEquipmentDefinition(
                    projected.DefinitionId).RuntimeWeaponReferenceId.ToString(),
                Is.EqualTo(exact.WeaponDefinitionId.Value));

            EquipmentInstance missing;
            Assert.That(
                lookup.TryResolve(
                    new EquipmentInstanceId(
                        StableId.Parse("instance.not-owned-by-character")),
                    out missing),
                Is.False,
                "Gameplay must not fabricate a fallback weapon.");

            yield return null;
        }
    }
}

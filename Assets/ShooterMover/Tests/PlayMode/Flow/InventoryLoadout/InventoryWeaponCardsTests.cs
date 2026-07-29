using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.InventoryLoadout;
using ShooterMover.UI.ProductionFlow;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.InventoryLoadout
{
    public sealed class InventoryWeaponCardsTests
    {
        [Test]
        public void RattlerCardProjectsConfirmedCanonicalStatsAndTemporaryArt()
        {
            WeaponInventoryCardPresentation presentation;
            string rejectionCode;

            Assert.That(
                WeaponInventoryCardPresentation.TryCreate(
                    WeaponCatalogProvider.WeaponCatalog,
                    LegacyWeaponSetup.StarterWeaponDefinitionId,
                    out presentation,
                    out rejectionCode),
                Is.True,
                rejectionCode);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.DisplayName, Is.EqualTo("Rattler MK1"));
            Assert.That(presentation.DamagePerShot, Is.EqualTo(1d));
            Assert.That(presentation.ProjectilesPerShot, Is.EqualTo(1));
            Assert.That(presentation.RateOfFire, Is.EqualTo(4d));
            Assert.That(
                presentation.ImageResourceKey,
                Is.EqualTo("blaster_sp"));
            Assert.That(
                presentation.SideProfileArtReference,
                Is.EqualTo("weapon-art.rattler.mk1.side-v1"));
        }

        [Test]
        public void UnknownDefinitionFailsClosedWithoutFabricatedStats()
        {
            WeaponInventoryCardPresentation presentation;
            string rejectionCode;

            Assert.That(
                WeaponInventoryCardPresentation.TryCreate(
                    WeaponCatalogProvider.WeaponCatalog,
                    "weapon.unknown",
                    out presentation,
                    out rejectionCode),
                Is.False);
            Assert.That(presentation, Is.Null);
            Assert.That(
                rejectionCode,
                Is.EqualTo(
                    "inventory-weapon-card-definition-unknown:weapon.unknown"));
        }

        [UnityTest]
        public IEnumerator PresenterBindsAggressiveCanonicalInventoryWithoutFallback()
        {
            PlayerRouteProfilePayload draft =
                PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.inventory-card-test"),
                    StableId.Parse(
                        WeaponMountPolicy
                            .AggressiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayload.WeaponSlotCount]);
            var runtime = new PlayerLoadoutLive(draft);
            GameObject host = new GameObject(
                "Production inventory weapon-card presenter test");
            InventoryLoadoutScreenController controller =
                host.AddComponent<InventoryLoadoutScreenController>();
            controller.ConfigureDisconnected(
                delegate(PlayerRouteProfilePayload payload) { });
            controller.Present(
                HubRoute.Inventory,
                runtime.CurrentRoutePayload);
            InventoryWeaponCards presenter =
                host.AddComponent<InventoryWeaponCards>();

            Assert.That(
                presenter.BindForTests(controller, runtime),
                Is.True);
            Assert.That(presenter.IsBound, Is.True);
            Assert.That(controller.IsConfigured, Is.True);
            Assert.That(controller.CanonicalSnapshot, Is.Not.Null);
            Assert.That(
                controller.CanonicalSnapshot.OwnedWeapons.Count,
                Is.EqualTo(2));
            Assert.That(
                controller.CanonicalSnapshot.Mounts.Count,
                Is.EqualTo(3));
            Assert.That(
                controller.CanonicalSnapshot.Mounts[1]
                    .Position.IsLockedBySkill,
                Is.True);
            Assert.That(controller.enabled, Is.False);

            Object.Destroy(host);
            yield return null;
        }
    }
}

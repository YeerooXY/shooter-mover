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
    public sealed class ProductionInventoryWeaponCardsPresenterV1Tests
    {
        [Test]
        public void RattlerCardProjectsConfirmedCanonicalStatsAndTemporaryArt()
        {
            WeaponInventoryCardPresentationV1 presentation;
            string rejectionCode;

            Assert.That(
                WeaponInventoryCardPresentationV1.TryCreate(
                    ProductionWeaponCatalogProvider.WeaponCatalog,
                    ProductionWeaponOnboardingV1.StarterWeaponDefinitionId,
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
            WeaponInventoryCardPresentationV1 presentation;
            string rejectionCode;

            Assert.That(
                WeaponInventoryCardPresentationV1.TryCreate(
                    ProductionWeaponCatalogProvider.WeaponCatalog,
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
            PlayerRouteProfilePayloadV1 draft =
                PlayerRouteProfilePayloadV1.Create(
                    StableId.Parse("character.inventory-card-test"),
                    StableId.Parse(
                        ProductionWeaponMountPolicyV1
                            .AggressiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayloadV1.WeaponSlotCount]);
            var runtime = new ProductionPlayerLoadoutRuntimeV1(draft);
            GameObject host = new GameObject(
                "Production inventory weapon-card presenter test");
            InventoryLoadoutScreenControllerV1 controller =
                host.AddComponent<InventoryLoadoutScreenControllerV1>();
            controller.ConfigureDisconnected(delegate { });
            controller.Present(
                HubRouteV1.Inventory,
                runtime.CurrentRoutePayload);
            ProductionInventoryWeaponCardsPresenterV1 presenter =
                host.AddComponent<ProductionInventoryWeaponCardsPresenterV1>();

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

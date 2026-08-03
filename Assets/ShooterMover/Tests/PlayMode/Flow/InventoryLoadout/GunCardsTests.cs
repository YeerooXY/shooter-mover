using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UI.Game;
using ShooterMover.UI.InventoryLoadout;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.InventoryLoadout
{
    public sealed class GunCardsTests
    {
        [Test]
        public void RattlerCardProjectsConfirmedCanonicalStatsAndSideProfileArt()
        {
            GunInventoryCardPresentation presentation;
            string rejectionCode;

            Assert.That(
                GunInventoryCardPresentation.TryCreate(
                    GunCatalogProvider.GunCatalog,
                    StarterLoadout.StarterGunDefinitionId,
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
                presentation.SideProfileArtReference,
                Is.EqualTo("gun-art.rattler.mk1.side-v1"));
        }

        [Test]
        public void UnknownDefinitionFailsClosedWithoutFabricatedStats()
        {
            GunInventoryCardPresentation presentation;
            string rejectionCode;

            Assert.That(
                GunInventoryCardPresentation.TryCreate(
                    GunCatalogProvider.GunCatalog,
                    "gun.unknown",
                    out presentation,
                    out rejectionCode),
                Is.False);
            Assert.That(presentation, Is.Null);
            Assert.That(
                rejectionCode,
                Is.EqualTo(
                    "inventory-gun-card-definition-unknown:gun.unknown"));
        }

        [UnityTest]
        public IEnumerator PresenterBindsCanonicalPhysicalMountsAndExactInstances()
        {
            PlayerRouteProfilePayload draft =
                PlayerRouteProfilePayload.Create(
                    StableId.Parse("character.inventory-card-test"),
                    StableId.Parse(
                        GunMountPolicy.AggressiveLoadoutProfileId),
                    new StableId[
                        PlayerRouteProfilePayload.GunSlotCount]);
            var runtime = new PlayerLoadoutLive(draft);
            GameObject host = new GameObject(
                "Production inventory gun-card presenter test");
            InventoryMenu controller = host.AddComponent<InventoryMenu>();
            controller.ConfigureDisconnected(
                delegate(PlayerRouteProfilePayload payload) { });
            controller.Present(
                HubRoute.Inventory,
                runtime.CurrentRoutePayload);
            GunCards presenter = host.AddComponent<GunCards>();

            Assert.That(
                presenter.BindForTests(controller, runtime),
                Is.True);
            Assert.That(presenter.IsBound, Is.True);
            Assert.That(controller.IsConfigured, Is.True);
            Assert.That(controller.CanonicalSnapshot, Is.Not.Null);
            Assert.That(
                controller.CanonicalSnapshot.OwnedGuns.Count,
                Is.EqualTo(7),
                "Two starter Rattlers, one Sweeper, and four trial guns are owned.");
            Assert.That(
                controller.CanonicalSnapshot.Mounts.Count,
                Is.EqualTo(3));
            Assert.That(
                controller.CanonicalSnapshot.Mounts[0]
                    .Position.DisplayName,
                Is.EqualTo("Outer Left"));
            Assert.That(
                controller.CanonicalSnapshot.Mounts[1]
                    .Position.DisplayName,
                Is.EqualTo("Center"));
            Assert.That(
                controller.CanonicalSnapshot.Mounts[1]
                    .Position.IsLockedBySkill,
                Is.True);
            Assert.That(
                controller.CanonicalSnapshot.Mounts[1]
                    .EquippedInstanceId,
                Is.Null);
            Assert.That(
                controller.CanonicalSnapshot.Mounts[2]
                    .Position.DisplayName,
                Is.EqualTo("Outer Right"));

            var exactIds = new HashSet<StableId>();
            int rattlerCount = 0;
            for (int index = 0;
                 index < controller.CanonicalSnapshot.OwnedGuns.Count;
                 index++)
            {
                GunInventoryCard card =
                    controller.CanonicalSnapshot.OwnedGuns[index];
                Assert.That(
                    exactIds.Add(card.Instance.InstanceId),
                    Is.True,
                    "One exact instance must produce only one owned card.");
                if (card.Instance.GunDefinitionId.Value
                    == StarterLoadout.StarterGunDefinitionId)
                {
                    rattlerCount++;
                }
            }
            Assert.That(
                rattlerCount,
                Is.EqualTo(2),
                "The two shown Rattlers are separate exact owned instances.");
            Assert.That(controller.enabled, Is.False);

            Object.Destroy(host);
            yield return null;
        }
    }
}

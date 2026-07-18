using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed partial class LevelRunCoordinatorV1Tests
    {
        [Test]
        public void StarterProfileCreatesConcreteHoldingsAndExactRetryDoesNotGrantAgain()
        {
            Fixture fixture = Fixture.Create("starter-profile");
            var definitionIds = new List<StableId>();
            for (int index = 0; index < fixture.Catalog.EquipmentDefinitions.Count; index++)
            {
                definitionIds.Add(
                    fixture.Catalog.EquipmentDefinitions[index].DefinitionId);
            }

            var request = new StarterRouteProfileRequestV1(
                StableId.Parse("character.default-pilot"),
                StableId.Parse("loadout-profile.production-starter"),
                definitionIds,
                StableId.Parse("equipment-quality.common"),
                1);
            var factory = new StarterRouteProfileFactoryV1();
            long before = fixture.Holdings.Sequence;

            StarterRouteProfileResultV1 first = factory.CreateOrRestore(
                fixture.Holdings,
                fixture.Catalog,
                request);
            long afterFirst = fixture.Holdings.Sequence;
            StarterRouteProfileResultV1 retry = factory.CreateOrRestore(
                fixture.Holdings,
                fixture.Catalog,
                request);

            Assert.That(first.Status, Is.EqualTo(
                StarterRouteProfileStatusV1.Created));
            Assert.That(retry.Status, Is.EqualTo(
                StarterRouteProfileStatusV1.ExactRetry));
            Assert.That(afterFirst, Is.EqualTo(before + 4L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(afterFirst));
            Assert.That(first.RoutePayload.HasValidFingerprint(), Is.True);
            Assert.That(retry.RoutePayload.Fingerprint,
                Is.EqualTo(first.RoutePayload.Fingerprint));
            Assert.That(first.EquipmentInstances.Count, Is.EqualTo(4));

            for (int index = 0; index < first.EquipmentInstances.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding;
                Assert.That(
                    fixture.Holdings.TryGetUnique(
                        first.EquipmentInstances[index].InstanceId,
                        out holding),
                    Is.True);
                Assert.That(
                    first.RoutePayload.WeaponSlots[index]
                        .EquipmentInstanceStableId,
                    Is.EqualTo(first.EquipmentInstances[index].InstanceId));
            }
        }

        [Test]
        public void StarterProfilePrevalidationRejectsMissingDefinitionWithoutMutation()
        {
            Fixture fixture = Fixture.Create("starter-profile-missing");
            var definitionIds = new List<StableId>
            {
                fixture.Catalog.EquipmentDefinitions[0].DefinitionId,
                StableId.Parse("equipment.missing-starter-definition"),
                fixture.Catalog.EquipmentDefinitions[2].DefinitionId,
                fixture.Catalog.EquipmentDefinitions[3].DefinitionId,
            };
            var request = new StarterRouteProfileRequestV1(
                StableId.Parse("character.default-pilot"),
                StableId.Parse("loadout-profile.invalid-starter"),
                definitionIds,
                StableId.Parse("equipment-quality.common"),
                1);
            long before = fixture.Holdings.Sequence;

            StarterRouteProfileResultV1 result =
                new StarterRouteProfileFactoryV1().CreateOrRestore(
                    fixture.Holdings,
                    fixture.Catalog,
                    request);

            Assert.That(result.Status, Is.EqualTo(
                StarterRouteProfileStatusV1.MissingEquipmentDefinition));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RoutePayload, Is.Null);
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(before));
        }
    }
}

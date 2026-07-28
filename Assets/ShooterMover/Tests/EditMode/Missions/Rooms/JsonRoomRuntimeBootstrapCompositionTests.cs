using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Missions.Rooms
{
    public sealed class JsonRoomRuntimeBootstrapCompositionTests
    {
        [Test]
        public void BuildFromJson_RaisesAcceptedBundleSynchronouslyBeforeReturn()
        {
            GameObject owner = new GameObject("json-room-bootstrap-composition-test");
            owner.SetActive(false);
            try
            {
                JsonRoomRuntimeBootstrap2D bootstrap =
                    owner.AddComponent<JsonRoomRuntimeBootstrap2D>();
                RoomRuntimeComposition2D rooms =
                    owner.AddComponent<RoomRuntimeComposition2D>();
                JsonRoomContentDefinition2D content =
                    Resources.Load<JsonRoomContentDefinition2D>(
                        "ProductionLevels/Level1RoomContent");
                RoomPresentationCatalog2D presentations =
                    Resources.Load<RoomPresentationCatalog2D>(
                        "ProductionLevels/Level1PresentationCatalog");
                Assert.That(content, Is.Not.Null);
                Assert.That(presentations, Is.Not.Null);

                bootstrap.Configure(
                    content,
                    rooms,
                    presentations,
                    owner.transform,
                    "room-runtime-instance.run-reward-order-test");
                int acceptedCount = 0;
                RoomContentBundleV1 observed = null;
                bootstrap.BuildAccepted += delegate(RoomContentBundleV1 bundle)
                {
                    acceptedCount++;
                    observed = bundle;
                    Assert.That(bootstrap.IsBuilt, Is.True);
                    Assert.That(rooms.IsBuilt, Is.True);
                    Assert.That(
                        ReferenceEquals(bootstrap.ImportedBundle, bundle),
                        Is.True);
                };

                bool returned = bootstrap.BuildFromJson();

                Assert.That(returned, Is.True);
                Assert.That(acceptedCount, Is.EqualTo(1));
                Assert.That(observed, Is.Not.Null);
                Assert.That(
                    ReferenceEquals(observed, bootstrap.ImportedBundle),
                    Is.True);
                Assert.That(
                    observed.Enemies.Count(row => row != null
                        && string.Equals(
                            row.AuthoredId,
                            "run-reward-proof",
                            StringComparison.Ordinal)),
                    Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BuildFromJson_PropagatesAcceptedCompositionRejection()
        {
            GameObject owner = new GameObject("json-room-bootstrap-rejection-test");
            owner.SetActive(false);
            try
            {
                JsonRoomRuntimeBootstrap2D bootstrap =
                    owner.AddComponent<JsonRoomRuntimeBootstrap2D>();
                RoomRuntimeComposition2D rooms =
                    owner.AddComponent<RoomRuntimeComposition2D>();
                JsonRoomContentDefinition2D content =
                    Resources.Load<JsonRoomContentDefinition2D>(
                        "ProductionLevels/Level1RoomContent");
                RoomPresentationCatalog2D presentations =
                    Resources.Load<RoomPresentationCatalog2D>(
                        "ProductionLevels/Level1PresentationCatalog");
                Assert.That(content, Is.Not.Null);
                Assert.That(presentations, Is.Not.Null);

                bootstrap.Configure(
                    content,
                    rooms,
                    presentations,
                    owner.transform,
                    "room-runtime-instance.run-reward-rejection-test");
                bootstrap.BuildAccepted += delegate
                {
                    throw new InvalidOperationException(
                        "run-reward-composition-rejected-for-test");
                };

                InvalidOperationException error =
                    Assert.Throws<InvalidOperationException>(
                        () => bootstrap.BuildFromJson());

                StringAssert.Contains(
                    "run-reward-composition-rejected-for-test",
                    error.Message);
                Assert.That(bootstrap.IsBuilt, Is.True);
                Assert.That(rooms.IsBuilt, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}

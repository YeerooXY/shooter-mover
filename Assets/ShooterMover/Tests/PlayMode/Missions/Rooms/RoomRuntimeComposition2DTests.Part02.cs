#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Missions.Rooms
{
    public sealed partial class RoomRuntimeComposition2DTests
    {
[UnityTest]
        public IEnumerator RealEnemyAuthorities_DriveDoorsRetentionReturnFinalExitAndRestart()
        {
            Assert.That(MobileRuntimeType, Is.Not.Null);
            Assert.That(TurretPackageType, Is.Not.Null);
            GameObject droidTemplate = Track(TemplateWithComponent(
                "RealMovingDroidTemplate",
                MobileRuntimeType));
            GameObject turretTemplate = Track(TemplateWithComponent(
                "RealTurretTemplate",
                TurretPackageType));
            GameObject propTemplate = Track(Template("CoverTemplate"));
            GameObject doorTemplate = Track(Template("DoorTemplate"));
            doorTemplate.AddComponent<BoxCollider2D>();
            RoomPresentationCatalog2D catalog = Track(CreateCatalog(
                droidTemplate,
                turretTemplate,
                propTemplate,
                doorTemplate));
            PlayerFixture player = CreatePlayer();
            Component projectile = CreateProjectileTemplate();
            GameObject host = Track(new GameObject("RoomRuntimeRealContentHost"));
            RoomRuntimeComposition2D composition =
                host.AddComponent<RoomRuntimeComposition2D>();
            composition.ConfigureForTests(
                Level1AuthorableRoomDefinitionV1.Create(),
                catalog);
            composition.BuildSession(
                StableId.Parse("room-runtime-instance.playmode-real-content"));
            bool finalRaised = false;
            composition.FinalExitReached += () => finalRaised = true;
            yield return null;

            Assert.That(
                composition.CurrentRoomStableId,
                Is.EqualTo(Level1AuthorableRoomDefinitionV1.EntryRoomStableId));
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                    out RoomPlacedInstance2D droid),
                Is.True);
            ConfigureMobileDroid(droid, player, projectile);
            Assert.That(
                droid.GetComponent(MobileRuntimeType),
                Is.Not.Null,
                "Room 1 must instantiate the real moving-droid runtime component.");
            Assert.That(
                droid.GetComponent<RoomOccupantTerminalRelay2D>(),
                Is.Not.Null);
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                    out RoomDoorInstance2D forwardDoor),
                Is.True);
            Assert.That(forwardDoor.IsOpen, Is.False);

            ApplyLethalHit(
                droid.gameObject.GetComponent<EnemyTarget2DAdapter>(),
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                "room-live-droid-lethal");
            Assert.That(
                droid.GetComponent<RoomOccupantTerminalRelay2D>().PollNow(),
                Is.True,
                "The generic terminal relay must consume the real EN-002 destroyed state.");
            yield return null;
            Assert.That(forwardDoor == null || forwardDoor.IsOpen, Is.True);
            Assert.That(
                composition.Query.GetRoomProjection(
                    Level1AuthorableRoomDefinitionV1.EntryRoomStableId).IsCompleted,
                Is.True);

            composition.Traverse(
                Operation("real-forward"),
                Level1AuthorableRoomDefinitionV1.ForwardExitStableId);
            yield return null;
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                    out RoomPlacedInstance2D turret),
                Is.True);
            ConfigureTurret(turret, player, projectile);
            Assert.That(
                turret.GetComponent(TurretPackageType),
                Is.Not.Null,
                "Room 2 must instantiate the real turret package component.");
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.ReturnDoorStableId,
                    out RoomDoorInstance2D returnDoor),
                Is.True);
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.FinalDoorStableId,
                    out RoomDoorInstance2D finalDoor),
                Is.True);
            Assert.That(returnDoor.IsOpen, Is.True,
                "The authored entered-room condition opens only the return gate.");
            Assert.That(finalDoor.IsOpen, Is.False);

            ApplyLethalHit(
                turret.gameObject.GetComponent<EnemyTarget2DAdapter>(),
                Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                "room-live-turret-lethal");
            Assert.That(
                turret.GetComponent<RoomOccupantTerminalRelay2D>().PollNow(),
                Is.True);
            yield return null;
            Assert.That(finalDoor == null || finalDoor.IsOpen, Is.True);
            Assert.That(
                composition.Query.GetRoomProjection(
                    Level1AuthorableRoomDefinitionV1.TerminalRoomStableId).IsCompleted,
                Is.True);

            composition.Traverse(
                Operation("real-return"),
                Level1AuthorableRoomDefinitionV1.ReturnExitStableId);
            yield return null;
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                    out _),
                Is.False,
                "Returning must not respawn the defeated real droid instance.");
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                    out RoomDoorInstance2D retainedForward),
                Is.True);
            Assert.That(retainedForward.IsOpen, Is.True);

            retainedForward.TryTraverse(Operation("real-forward-again"));
            yield return null;
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.TurretInstanceStableId,
                    out _),
                Is.False,
                "Returning to Room 2 must not respawn the defeated real turret instance.");
            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.FinalDoorStableId,
                    out RoomDoorInstance2D retainedFinal),
                Is.True);
            Assert.That(retainedFinal.IsOpen, Is.True);
            RoomLiveOperationResultV1 final = retainedFinal.TryTraverse(
                Operation("real-final"));
            Assert.That(
                final.Status,
                Is.EqualTo(RoomLiveOperationStatusV1.FinalExitReached));
            Assert.That(finalRaised, Is.True);

            RoomLiveOperationResultV1 restart = composition.Restart(
                Operation("real-restart"));
            yield return null;
            Assert.That(restart.Status, Is.EqualTo(RoomLiveOperationStatusV1.Applied));
            Assert.That(composition.Query.CurrentProjection.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(
                composition.TryGetSpawnedPlacement(
                    Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId,
                    out RoomPlacedInstance2D restartedDroid),
                Is.True,
                "Restart must reconstruct the authored real droid presentation instance.");
            Assert.That(restartedDroid.GetComponent(MobileRuntimeType), Is.Not.Null);
        }
    }
}
#endif

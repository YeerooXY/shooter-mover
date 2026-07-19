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
        public IEnumerator ClosedConfiguredGate_FailsClosedBeforeRealTerminalFact()
        {
            GameObject droidTemplate = Track(TemplateWithComponent(
                "ClosedGateDroidTemplate",
                MobileRuntimeType));
            GameObject turretTemplate = Track(TemplateWithComponent(
                "ClosedGateTurretTemplate",
                TurretPackageType));
            GameObject propTemplate = Track(Template("ClosedGateCoverTemplate"));
            GameObject doorTemplate = Track(Template("ClosedGateDoorTemplate"));
            doorTemplate.AddComponent<BoxCollider2D>();
            RoomPresentationCatalog2D catalog = Track(CreateCatalog(
                droidTemplate,
                turretTemplate,
                propTemplate,
                doorTemplate));
            GameObject host = Track(new GameObject("RoomRuntimeClosedGateHost"));
            RoomRuntimeComposition2D composition =
                host.AddComponent<RoomRuntimeComposition2D>();
            composition.ConfigureForTests(
                Level1AuthorableRoomDefinitionV1.Create(),
                catalog);
            composition.BuildSession(
                StableId.Parse("room-runtime-instance.playmode-closed-gate"));
            yield return null;

            Assert.That(
                composition.TryGetSpawnedDoor(
                    Level1AuthorableRoomDefinitionV1.ForwardDoorStableId,
                    out RoomDoorInstance2D door),
                Is.True);
            Assert.That(door.IsOpen, Is.False);
            RoomLiveOperationResultV1 result = door.TryTraverse(
                Operation("closed-gate-attempt"));
            Assert.That(result.Status, Is.EqualTo(RoomLiveOperationStatusV1.Rejected));
            Assert.That(result.RejectionCode, Is.EqualTo("room-live-door-closed"));
        }

private PlayerFixture CreatePlayer()
        {
            GameObject player = Track(new GameObject("RoomLiveRealPlayer"));
            player.transform.position = new Vector3(10f, 0f, 0f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            CircleCollider2D collider = player.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            EnemyTarget2DAdapter target = player.AddComponent<EnemyTarget2DAdapter>();
            StableId playerId = StableId.Parse("actor.room-live-real-player");
            target.Configure(playerId, player.transform, collider);
            return new PlayerFixture(playerId, target, collider);
        }

private Component CreateProjectileTemplate()
        {
            GameObject projectilePrefab = Track(new GameObject(
                "RoomLiveAcceptedProjectilePrefab"));
            projectilePrefab.SetActive(false);
            Rigidbody2D body = projectilePrefab.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = projectilePrefab.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            return projectilePrefab.AddComponent(BoundedProjectileType);
        }

private void ConfigureMobileDroid(
            RoomPlacedInstance2D placement,
            PlayerFixture player,
            Component projectile)
        {
            Component runtime = placement.GetComponent(MobileRuntimeType);
            ScriptableObject definition = Track((ScriptableObject)InvokeStatic(
                MobileDefinitionType,
                "CreateRuntime",
                16d,
                2.5d,
                5d,
                0.5d,
                0.3d,
                0.8d,
                0.65d,
                4,
                0.55d,
                4d,
                0.2d));
            InvokeInstance(
                runtime,
                "ConfigureSession",
                definition,
                placement.InstanceStableId,
                player.Target,
                new Collider2D[] { player.Collider },
                player.PlayerStableId,
                CombatWeightClass.Standard,
                projectile);
        }

private void ConfigureTurret(
            RoomPlacedInstance2D placement,
            PlayerFixture player,
            Component projectile)
        {
            Component package = placement.GetComponent(TurretPackageType);
            ScriptableObject definition = Track((ScriptableObject)InvokeStatic(
                TurretDefinitionType,
                "CreateRuntime",
                120d,
                0.2d,
                1.5d,
                25d,
                0.5d,
                0.1d,
                0.2d,
                0.05d,
                16));
            InvokeInstance(
                package,
                "Configure",
                definition,
                player.Target,
                player.Collider,
                projectile,
                placement.InstanceStableId,
                player.PlayerStableId,
                CombatWeightClass.Standard,
                null);
        }

private static void ApplyLethalHit(
            EnemyTarget2DAdapter target,
            StableId targetStableId,
            string eventValue)
        {
            Assert.That(target, Is.Not.Null);
            HitMessage message = new HitMessage(
                StableId.Create("event", eventValue),
                StableId.Parse("actor.room-live-test-weapon"),
                targetStableId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
            EnemyTarget2DHitApplication result = target.ApplyHit(
                message,
                1000d,
                0L);
            Assert.That(result.Status, Is.EqualTo(EnemyTarget2DHitStatus.Applied));
        }

private static GameObject Template(string name)
        {
            var template = new GameObject(name);
            template.SetActive(false);
            return template;
        }

private static GameObject TemplateWithComponent(string name, Type type)
        {
            GameObject template = Template(name);
            template.AddComponent(type);
            return template;
        }

private static RoomPresentationCatalog2D CreateCatalog(
            GameObject droidPrefab,
            GameObject turretPrefab,
            GameObject propPrefab,
            GameObject doorPrefab)
        {
            RoomPresentationCatalog2D catalog =
                ScriptableObject.CreateInstance<RoomPresentationCatalog2D>();
            catalog.ConfigureForTests(
                Entry(
                    Level1AuthorableRoomDefinitionV1.MovingDroidPresentationStableId,
                    droidPrefab),
                Entry(
                    Level1AuthorableRoomDefinitionV1.TurretPresentationStableId,
                    turretPrefab),
                Entry(
                    Level1AuthorableRoomDefinitionV1.CoverPresentationStableId,
                    propPrefab),
                Entry(
                    Level1AuthorableRoomDefinitionV1.DoorPresentationStableId,
                    doorPrefab));
            return catalog;
        }

private static RoomPresentationCatalogEntry2D Entry(
            StableId stableId,
            GameObject prefab)
        {
            var entry = new RoomPresentationCatalogEntry2D();
            entry.ConfigureForTests(stableId.ToString(), prefab);
            return entry;
        }

private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(candidate =>
                    candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, type.FullName + "." + methodName);
            return Invoke(method, null, arguments);
        }
    }
}
#endif

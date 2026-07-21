#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1RoomEnemyPresentationRouteTests
    {
        private const string SceneName = "Stage1VisibleSlice";
        private const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        private const string ControllerTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController";
        private const string ProjectionTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1RoomEnemyAuthorityProjection2D";
        private const string PoolTypeName =
            "ShooterMover.UnityAdapters.CombatPresentation.CombatDeathVfxPool2D";

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    SceneManager.SetActiveScene(
                        SceneManager.CreateScene("Combat Presentation Route Cleanup"));
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator RoomProjection_LethalDamage_UsesDecoratedAuthority_AndReplaysVfxOnce()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            Type poolType = FindType(PoolTypeName);
            Component pool = null;
            float installationDeadline = Time.time + 3f;
            while (Time.time < installationDeadline && pool == null)
            {
                pool = UnityEngine.Object.FindFirstObjectByType(poolType) as Component;
                if (pool == null)
                {
                    yield return null;
                }
            }
            Assert.That(pool, Is.Not.Null, "Production combat presentation must install its pool.");

            Type projectionType = FindType(ProjectionTypeName);
            Component projection = FindProjection(
                projectionType,
                Level1AuthorableRoomDefinitionV1.MovingDroidInstanceStableId);
            Assert.That(
                projection,
                Is.Not.Null,
                "The moving-droid room projection must resolve through the generic authority map.");

            object droid = Read<object>(controller, "MobileBlasterDroid");
            EnemyActorState before = Read<EnemyActorState>(droid, "CurrentState");
            Assert.That(before.IsDestroyed, Is.False);

            int spawnCountBefore = Read<int>(pool, "TotalSpawnCount");
            EnemyActorCommand lethal = EnemyActorCommand.Damage(
                9001L,
                StableId.Parse("combat-event.combat-presentation-room-projection-lethal"),
                StableId.Parse("actor.combat-presentation-test-player"),
                (int)CombatChannel.Kinetic,
                before.MaximumHealth + 1d);

            object first = Invoke<object>(projection, "Apply", lethal);
            Assert.That(first, Is.Not.Null);
            yield return null;

            Assert.That(Read<EnemyActorState>(droid, "CurrentState").IsDestroyed, Is.True);
            Assert.That(
                Read<int>(pool, "TotalSpawnCount"),
                Is.EqualTo(spawnCountBefore + 1),
                "Lethal damage routed through the room projection must cross the decorated authority.");

            object replay = Invoke<object>(projection, "Apply", lethal);
            Assert.That(replay, Is.Not.Null);
            yield return null;

            Assert.That(
                Read<int>(pool, "TotalSpawnCount"),
                Is.EqualTo(spawnCountBefore + 1),
                "Exact terminal replay must not create a second presentation effect.");
        }

        private static Component FindProjection(Type projectionType, StableId instanceStableId)
        {
            RoomPlacedInstance2D[] markers =
                UnityEngine.Object.FindObjectsByType<RoomPlacedInstance2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < markers.Length; index++)
            {
                RoomPlacedInstance2D marker = markers[index];
                if (marker == null
                    || !marker.IsConfigured
                    || marker.InstanceStableId != instanceStableId)
                {
                    continue;
                }

                Component projection = marker.GetComponent(projectionType);
                if (projection != null)
                {
                    return projection;
                }
            }

            return null;
        }

        private static IEnumerator LoadController(Action<MonoBehaviour> assign)
        {
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
            Type type = FindType(ControllerTypeName);
            MonoBehaviour controller =
                UnityEngine.Object.FindFirstObjectByType(type) as MonoBehaviour;
            Assert.That(controller, Is.Not.Null);
            assign(controller);
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static T Invoke<T>(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            object result = method.Invoke(target, arguments);
            return result == null ? default(T) : (T)result;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException("Required type not found: " + fullName);
        }
    }
}
#endif

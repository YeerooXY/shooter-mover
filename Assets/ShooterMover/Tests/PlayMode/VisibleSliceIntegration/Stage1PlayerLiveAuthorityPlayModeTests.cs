#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1PlayerLiveAuthorityPlayModeTests
    {
        private const string SceneName = "Stage1VisibleSlice";
        private const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        private const string ControllerTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController";
        private const string LiveAdapterTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1PlayerLiveAuthorityAdapterV1";

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    SceneManager.SetActiveScene(
                        SceneManager.CreateScene("PLAYER-LIVE-001 Cleanup"));
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
        public IEnumerator VoidDamage_UsesLiveAuthorityAndRestartPreservesIdentity()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            Assert.That(Read<bool>(adapter, "IsInitialized"), Is.True);
            object beforeRuntime = Invoke<object>(adapter, "ExportSnapshot");
            object beforePlayer = Read<object>(beforeRuntime, "Player");
            StableId actorId = Read<StableId>(beforePlayer, "ActorInstanceId");
            long generation = Read<long>(beforePlayer, "LifecycleGeneration");

            Transform player = Read<Transform>(controller, "PlayerTransform");
            player.position = new Vector3(-1.5f, 4.2f, 0f);
            float deadline = Time.time + 1f;
            while (Time.time < deadline
                && Read<double>(
                    Invoke<object>(adapter, "ExportHudHealth"),
                    "CurrentHealth") == 100d)
            {
                yield return new WaitForFixedUpdate();
            }

            object damagedHud = Invoke<object>(adapter, "ExportHudHealth");
            Assert.That(Read<double>(damagedHud, "CurrentHealth"), Is.EqualTo(65d));
            Assert.That(Read<int>(controller, "PlayerHealth"), Is.EqualTo(65));
            Assert.That(Read<int>(controller, "VoidDamageCount"), Is.GreaterThanOrEqualTo(1));

            Invoke<object>(controller, "QuickRestart");
            yield return null;
            yield return null;

            object afterRuntime = Invoke<object>(adapter, "ExportSnapshot");
            object afterPlayer = Read<object>(afterRuntime, "Player");
            Assert.That(
                Read<StableId>(afterPlayer, "ActorInstanceId"),
                Is.EqualTo(actorId));
            Assert.That(
                Read<long>(afterPlayer, "LifecycleGeneration"),
                Is.EqualTo(generation + 1L));
            Assert.That(Read<double>(afterPlayer, "CurrentHealth"), Is.EqualTo(100d));
            Assert.That(Read<int>(controller, "PlayerHealth"), Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator TurretProjectile_DamagesLiveAuthorityAfterPhysicalContact()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);
            yield return ClearEntryAndEnterTerminal(controller);

            Assert.That(
                Read<double>(
                    Invoke<object>(adapter, "ExportHudHealth"),
                    "CurrentHealth"),
                Is.EqualTo(100d));

            yield return new WaitForSeconds(0.5f);
            Assert.That(
                Read<double>(
                    Invoke<object>(adapter, "ExportHudHealth"),
                    "CurrentHealth"),
                Is.EqualTo(100d),
                "A turret emission must not mutate health before its projectile contacts the player.");

            float deadline = Time.time + 1.5f;
            while (Time.time < deadline
                && Read<double>(
                    Invoke<object>(adapter, "ExportHudHealth"),
                    "CurrentHealth") == 100d)
            {
                yield return null;
            }

            Assert.That(
                Read<double>(
                    Invoke<object>(adapter, "ExportHudHealth"),
                    "CurrentHealth"),
                Is.EqualTo(90d));
            Assert.That(Read<int>(controller, "PlayerHealth"), Is.EqualTo(90));
            Assert.That(Read<int>(adapter, "DeathFactCount"), Is.Zero);
        }

        private static IEnumerator LoadComposition(
            Action<MonoBehaviour> assignController,
            Action<MonoBehaviour> assignAdapter)
        {
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
            MonoBehaviour controller =
                UnityEngine.Object.FindFirstObjectByType(FindType(ControllerTypeName))
                as MonoBehaviour;
            MonoBehaviour adapter =
                UnityEngine.Object.FindFirstObjectByType(FindType(LiveAdapterTypeName))
                as MonoBehaviour;
            Assert.That(controller, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            assignController(controller);
            assignAdapter(adapter);
        }

        private static IEnumerator ClearEntryAndEnterTerminal(
            MonoBehaviour controller)
        {
            object droid = Read<object>(controller, "MobileBlasterDroid");
            Invoke<object>(
                droid,
                "Apply",
                EnemyActorCommand.Damage(
                    10L,
                    StableId.Parse("combat-event.player-live-clear-entry"),
                    StableId.Parse("actor.player-live-test"),
                    (int)CombatChannel.Kinetic,
                    1000d));
            yield return new WaitForFixedUpdate();
            yield return null;

            Transform player = Read<Transform>(controller, "PlayerTransform");
            player.position = new Vector3(13.5f, 0f, 0f);
            yield return null;
            Assert.That(
                Read<StableId>(controller, "CurrentRoomStableId"),
                Is.EqualTo(StableId.Parse("room.level1-terminal")));
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static T Invoke<T>(
            object target,
            string methodName,
            params object[] arguments)
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

            throw new InvalidOperationException(
                "Required type not found: " + fullName);
        }
    }
}
#endif

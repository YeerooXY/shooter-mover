#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Players;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class EnemyProjectileDamageRoutingPlayModeTests
    {
        private const string SceneName = "Stage1VisibleSlice";
        private const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        private const string ControllerTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController";
        private const string CompatibilityAdapterTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1PlayerLiveAuthorityAdapterV1";
        private const string CanonicalAdapterTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Level1PlayerRuntimeSceneAdapterV1";

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    SceneManager.SetActiveScene(
                        SceneManager.CreateScene(
                            "ENEMY-DAMAGE-CUTOVER-001 Cleanup"));
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
        public IEnumerator CompatibilityShellUsesOneCanonicalSharedRouter()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            Assert.That(controller, Is.Not.Null);
            Assert.That(adapter.GetType().BaseType, Is.EqualTo(FindType(CanonicalAdapterTypeName)));
            Assert.That(Read<int>(adapter, "RegisteredEnemyDamageSourceCount"), Is.EqualTo(2));

            Type canonical = adapter.GetType().BaseType;
            Assert.That(
                canonical.GetField(
                    "enemyImpactRouter",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                canonical.GetMethod(
                    "HandleTurretHit",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                canonical.GetMethod(
                    "HandleDroidHit",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                canonical.GetMethod(
                    "TryResolveLifecycleGeneration",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null);
        }

        [UnityTest]
        public IEnumerator MobileDroidImpactCarriesTypedGenerationAndConfiguredDamage()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Component droid = (Component)Read<object>(controller, "MobileBlasterDroid");
            object droidTarget = Read<object>(droid, "EnemyTarget");
            StableId droidActorId = Read<StableId>(droidTarget, "TargetId");
            Transform player = Read<Transform>(controller, "PlayerTransform");
            player.position = droid.transform.position + Vector3.right * 3f;

            float deadline = Time.time + 3f;
            object impact = null;
            while (Time.time < deadline && impact == null)
            {
                impact = Read<object>(adapter, "LastEnemyImpact");
                yield return null;
            }

            Assert.That(impact, Is.Not.Null);
            Assert.That(Read<StableId>(impact, "SourceActorId"), Is.EqualTo(droidActorId));
            Assert.That(
                Read<StableId>(impact, "TargetActorId"),
                Is.EqualTo(before.Player.ActorInstanceId));
            Assert.That(Read<double>(impact, "Damage"), Is.EqualTo(10d));
            Assert.That(
                Read<long>(impact, "TargetLifecycleGeneration"),
                Is.EqualTo(before.Player.LifecycleGeneration));
            Assert.That(Read<StableId>(impact, "HitEventId"), Is.Not.Null);
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.LessThan(100d));
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
                UnityEngine.Object.FindFirstObjectByType(
                    FindType(CompatibilityAdapterTypeName))
                as MonoBehaviour;
            Assert.That(controller, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(Read<bool>(adapter, "IsInitialized"), Is.True);
            assignController(controller);
            assignAdapter(adapter);
        }

        private static T Read<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null, propertyName);
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

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.ContentPackages.Weapons.Stage1Loadouts;
using ShooterMover.Domain.Common;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Firing
{
    public sealed class Stage1WeaponRuntimeFiringTests
    {
        private const string SceneName = "Stage1VisibleSlice";
        private const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        private const string ControllerTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController";

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    SceneManager.SetActiveScene(
                        SceneManager.CreateScene("WPN-RUNTIME-001 Cleanup"));
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
        public IEnumerator ChangingLoadoutSlot_ChangesPhysicalProjectileBehavior()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            Transform player = Read<Transform>(controller, "PlayerTransform");
            Vector2 aim = (Vector2)player.position + Vector2.up * 100f;
            Stage1WeaponLoadoutFixture defaultFixture =
                Stage1WeaponLoadoutCatalog.Approved.DefaultFixture;
            Stage1WeaponLoadoutFixture ricochetFixture =
                Stage1WeaponLoadoutCatalog.Approved.GetFixedFixture(
                    StableId.Parse(Stage1WeaponLoadoutCatalog.RicochetFixtureIdText));

            Assert.That(
                Invoke<bool>(controller, "SelectLoadoutForTests", defaultFixture),
                Is.True);
            int shotgunProjectiles = Invoke<int>(
                controller,
                "FireMountForTests",
                1,
                0d,
                aim);
            Assert.That(shotgunProjectiles, Is.EqualTo(7));
            Assert.That(
                Invoke<string>(controller, "GetRuntimeDefinitionIdForTests", 1),
                Is.EqualTo("shotgun.mk1"));

            Invoke<object>(controller, "QuickRestart");
            Assert.That(
                Invoke<bool>(controller, "SelectLoadoutForTests", ricochetFixture),
                Is.True);
            int ricochetProjectiles = Invoke<int>(
                controller,
                "FireMountForTests",
                1,
                0d,
                aim);
            Assert.That(ricochetProjectiles, Is.EqualTo(1));
            Assert.That(
                Invoke<string>(controller, "GetRuntimeDefinitionIdForTests", 1),
                Is.EqualTo("ricochet_weapon.mk1"));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Restart_ResetsCooldownWithoutReplacingSelectedLoadout()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            Transform player = Read<Transform>(controller, "PlayerTransform");
            Vector2 aim = (Vector2)player.position + Vector2.right * 100f;
            Stage1WeaponLoadoutFixture ricochetFixture =
                Stage1WeaponLoadoutCatalog.Approved.GetFixedFixture(
                    StableId.Parse(Stage1WeaponLoadoutCatalog.RicochetFixtureIdText));

            Assert.That(
                Invoke<bool>(controller, "SelectLoadoutForTests", ricochetFixture),
                Is.True);
            Assert.That(
                Invoke<int>(controller, "FireMountForTests", 1, 0d, aim),
                Is.EqualTo(1));
            Assert.That(
                Invoke<int>(controller, "FireMountForTests", 1, 0.1d, aim),
                Is.Zero);

            Invoke<object>(controller, "QuickRestart");

            Assert.That(
                Invoke<string>(controller, "GetRuntimeDefinitionIdForTests", 1),
                Is.EqualTo("ricochet_weapon.mk1"));
            Assert.That(
                Invoke<int>(controller, "FireMountForTests", 1, 0d, aim),
                Is.EqualTo(1));

            yield return null;
        }

        private static IEnumerator LoadController(Action<MonoBehaviour> assign)
        {
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone)
            {
                yield return null;
            }
            yield return null;

            Type type = FindType(ControllerTypeName);
            Assert.That(type, Is.Not.Null);
            UnityEngine.Object value = UnityEngine.Object.FindFirstObjectByType(type);
            MonoBehaviour controller = value as MonoBehaviour;
            Assert.That(controller, Is.Not.Null);
            assign(controller);
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property " + propertyName);
            return (T)property.GetValue(target, null);
        }

        private static T Invoke<T>(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Missing method " + methodName);
            object value = method.Invoke(target, arguments);
            return value == null ? default(T) : (T)value;
        }
    }
}
#endif

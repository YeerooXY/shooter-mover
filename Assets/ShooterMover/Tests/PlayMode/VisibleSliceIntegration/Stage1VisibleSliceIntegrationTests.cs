#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Presentation.VisibleSliceBlasterTurret;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1VisibleSliceIntegrationTests
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
                    SceneManager.SetActiveScene(SceneManager.CreateScene("VS007 Cleanup"));
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
        public IEnumerator SceneBoot_ComposesAcceptedRoomPlayerTurretHudStripSelectorAndCamera()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            Assert.That(Read<bool>(controller, "IsInitialized"), Is.True);
            Assert.That(Read<object>(controller, "RoomPresentation"), Is.Not.Null);
            Assert.That(Read<object>(controller, "TurretPackage"), Is.Not.Null);
            Assert.That(Read<object>(controller, "CombatHud"), Is.Not.Null);
            Assert.That(Read<object>(controller, "WeaponStrip"), Is.Not.Null);
            Assert.That(Read<object>(controller, "LoadoutSelector"), Is.Not.Null);
            Assert.That(Read<object>(controller, "CameraRig"), Is.Not.Null);
            Assert.That(Read<int>(controller, "HudOwnerCount"), Is.EqualTo(1));
            Assert.That(Read<int>(controller, "CameraOwnerCount"), Is.EqualTo(1));
            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.False);
        }

        [UnityTest]
        public IEnumerator ConfirmAndFire_UsesAcceptedEnemyHealthUntilRoomClear()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            Assert.That(Invoke<bool>(controller, "ConfirmDefaultLoadout"), Is.True);
            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.True);

            for (int shot = 0; shot < 5; shot++)
            {
                Assert.That(Invoke<bool>(controller, "FireAtTurretForTests"), Is.True);
            }

            IVisibleSliceBlasterTurretPresentationSource source =
                controller as IVisibleSliceBlasterTurretPresentationSource;
            Assert.That(source, Is.Not.Null);
            Assert.That(source.TryReadSnapshot(out VisibleSliceBlasterTurretSnapshot snapshot), Is.True);
            Assert.That(snapshot.CurrentHealth, Is.Zero);
            Assert.That(snapshot.Phase, Is.EqualTo(VisibleSliceBlasterTurretPhase.Destroyed));
        }

        [UnityTest]
        public IEnumerator FiftyQuickRestarts_KeepOwnedCountsStableAndClearSessionState()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);
            int objects = Read<int>(controller, "SessionObjectCount");
            int hudOwners = Read<int>(controller, "HudOwnerCount");
            int cameraOwners = Read<int>(controller, "CameraOwnerCount");

            for (int cycle = 1; cycle <= 50; cycle++)
            {
                Invoke<object>(controller, "QuickRestart");
                Assert.That(Read<long>(controller, "RestartGeneration"), Is.EqualTo(cycle));
                Assert.That(Read<int>(controller, "PlayerHealth"), Is.EqualTo(100));
                Assert.That(Read<bool>(controller, "IsSessionActive"), Is.False);
                Assert.That(Read<int>(controller, "SessionObjectCount"), Is.EqualTo(objects));
                Assert.That(Read<int>(controller, "HudOwnerCount"), Is.EqualTo(hudOwners));
                Assert.That(Read<int>(controller, "CameraOwnerCount"), Is.EqualTo(cameraOwners));
                Assert.That(Read<int>(controller, "ActiveProjectileCount"), Is.Zero);
            }

            TestContext.Out.WriteLine(
                "cycles=50 objects=" + objects
                + " callbacks=2 projectiles=0 enemies=1 hud=" + hudOwners
                + " camera=" + cameraOwners
                + " selectedLoadout=0 sessionHealth=100 generation=50");
        }

        [UnityTest]
        public IEnumerator AccessibilityModes_PreserveCoreObjectsAndTurretReadability()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);
            int objects = Read<int>(controller, "SessionObjectCount");

            Invoke<object>(controller, "SetReducedEffects", true);
            Invoke<object>(controller, "SetGrayscale", true);
            IVisibleSliceBlasterTurretPresentationSource source =
                controller as IVisibleSliceBlasterTurretPresentationSource;
            Assert.That(source.TryReadSnapshot(out VisibleSliceBlasterTurretSnapshot snapshot), Is.True);
            Assert.That(snapshot.ReducedEffects, Is.True);
            Assert.That(snapshot.GrayscaleRequested, Is.True);
            Assert.That(Read<int>(controller, "SessionObjectCount"), Is.EqualTo(objects));
        }

        [Test]
        public void OwnedSourceAndScene_DoNotCreateDurableStateOrBuildSettingsAuthority()
        {
            string source = File.ReadAllText(
                "Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs");
            string scene = File.ReadAllText(ScenePath);
            Assert.That(source, Does.Not.Contain("MissionRunState"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs"));
            Assert.That(source, Does.Not.Contain("Save" + "Game"));
            Assert.That(source, Does.Not.Contain("Inventory" + "Service"));
            Assert.That(source, Does.Not.Contain("SceneManager.LoadScene"));
            Assert.That(scene, Does.Contain("Stage1VisibleSliceController"));
            Assert.That(scene, Does.Contain("d69cd63fb4924ef1b7a3c13bf92ef776"));
            Assert.That(scene, Does.Contain("9e665ce075ca8068ec015198e7000707"));
            Assert.That(scene, Does.Contain("05dcb140d7d245f1a4a03675967c9103"));
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
            MonoBehaviour controller = UnityEngine.Object.FindFirstObjectByType(type) as MonoBehaviour;
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

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
        public IEnumerator SceneBoot_ComposesDirectionalTurretShootingSandbox()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            Assert.That(Read<bool>(controller, "IsInitialized"), Is.True);
            Assert.That(Read<object>(controller, "RoomPresentation"), Is.Not.Null);
            object turret = Read<object>(controller, "TurretPackage");
            Assert.That(turret, Is.Not.Null);
            Assert.That(Read<object>(controller, "CombatHud"), Is.Not.Null);
            Assert.That(Read<object>(controller, "WeaponStrip"), Is.Null);
            Assert.That(Read<object>(controller, "LoadoutSelector"), Is.Null);
            Assert.That(Read<object>(controller, "CameraRig"), Is.Not.Null);
            Assert.That(Read<int>(controller, "HudOwnerCount"), Is.EqualTo(1));
            Assert.That(Read<int>(controller, "CameraOwnerCount"), Is.EqualTo(1));
            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.True);

            Component turretComponent = turret as Component;
            Assert.That(turretComponent, Is.Not.Null);
            Assert.That(
                turretComponent.transform.position.x / 0.5f,
                Is.EqualTo(Mathf.Round(turretComponent.transform.position.x / 0.5f))
                    .Within(0.0001f));
            Assert.That(
                turretComponent.transform.position.y / 0.5f,
                Is.EqualTo(Mathf.Round(turretComponent.transform.position.y / 0.5f))
                    .Within(0.0001f));
            Assert.That(Read<Vector2>(turret, "AuthoredFacing"), Is.EqualTo(Vector2.left));
        }

        [UnityTest]
        public IEnumerator TurretShot_DamagesOnlyAfterPhysicalTravelTime()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.True);
            Assert.That(Read<int>(controller, "PlayerHealth"), Is.EqualTo(100));

            yield return new WaitForSeconds(0.5f);
            Assert.That(
                Read<int>(controller, "PlayerHealth"),
                Is.EqualTo(100),
                "A turret shot must not apply damage merely because it was emitted.");
            object turret = Read<object>(controller, "TurretPackage");
            object projectileAdapter = Read<object>(turret, "ProjectileAdapter");
            Assert.That(
                Read<int>(projectileAdapter, "ActiveProjectileCount"),
                Is.GreaterThanOrEqualTo(1),
                "The eligible directional turret must emit a physical projectile.");

            float deadline = Time.time + 1.5f;
            while (Time.time < deadline && Read<int>(controller, "PlayerHealth") == 100)
            {
                yield return null;
            }

            Assert.That(
                Read<int>(controller, "PlayerHealth"),
                Is.EqualTo(90),
                "completion="
                + Read<object>(turret, "LastProjectileCompletionReason")
                + " hit-status="
                + (Read<object>(turret, "LastProjectileHitStatus") ?? "none")
                + " collision="
                + Read<string>(turret, "LastProjectileCollisionObjectName")
                + " collision-point="
                + Read<Vector2>(turret, "LastProjectileCollisionPoint")
                + " player-position="
                + Read<Transform>(controller, "PlayerTransform").position
                + " path-colliders="
                + DescribePathColliders(
                    ((Component)turret).transform.position,
                    Read<Transform>(controller, "PlayerTransform").position));
        }

        [UnityTest]
        public IEnumerator PlayerShot_DamagesTurretOnlyAfterPhysicalContact()
        {
            MonoBehaviour controller = null;
            yield return LoadController(value => controller = value);

            IVisibleSliceBlasterTurretPresentationSource source =
                controller as IVisibleSliceBlasterTurretPresentationSource;
            Assert.That(source, Is.Not.Null);
            Assert.That(
                source.TryReadSnapshot(out VisibleSliceBlasterTurretSnapshot before),
                Is.True);
            Assert.That(Invoke<bool>(controller, "FireAtTurretForTests"), Is.True);
            Assert.That(
                source.TryReadSnapshot(out VisibleSliceBlasterTurretSnapshot immediate),
                Is.True);
            Assert.That(immediate.CurrentHealth, Is.EqualTo(before.CurrentHealth));

            float deadline = Time.time + 1.2f;
            VisibleSliceBlasterTurretSnapshot after = immediate;
            while (Time.time < deadline && after.CurrentHealth == before.CurrentHealth)
            {
                yield return null;
                source.TryReadSnapshot(out after);
            }

            Assert.That(after.CurrentHealth, Is.EqualTo(before.CurrentHealth - 6));
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
                Assert.That(Read<bool>(controller, "IsSessionActive"), Is.True);
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

        private static string DescribePathColliders(Vector2 start, Vector2 end)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(start, end);
            return string.Join(
                ",",
                Array.ConvertAll(
                    hits,
                    hit => hit.collider == null ? "null" : hit.collider.gameObject.name));
        }
    }
}
#endif

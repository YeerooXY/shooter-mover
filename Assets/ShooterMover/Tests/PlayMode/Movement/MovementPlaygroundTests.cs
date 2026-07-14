#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Movement
{
    public sealed class MovementPlaygroundTests : InputTestFixture
    {
        private const string SceneName = "MovementPlayground";
        private const string ScenePath =
            "Assets/ShooterMover/Tests/PlayMode/Movement/Scenes/MovementPlayground.unity";
        private const string RootName = "Movement Playground";
        private const string HarnessTypeName =
            "ShooterMover.TestSupport.Movement.MovementPlaygroundHarness";
        private const string CleanupSceneName = "MT-013 Cleanup";
        private const float VelocityTolerance = 0.00001f;
        private const float PositionTolerance = 0.0001f;
        private const float ReferenceAspect = 16f / 9f;

        private readonly List<InputDevice> addedDevices = new List<InputDevice>();

        [UnityTearDown]
        public IEnumerator UnloadPlayground()
        {
            Scene playground = SceneManager.GetSceneByName(SceneName);
            if (playground.IsValid() && playground.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    Scene cleanup = SceneManager.CreateScene(CleanupSceneName);
                    SceneManager.SetActiveScene(cleanup);
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(playground);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            yield return null;
        }

        [TearDown]
        public override void TearDown()
        {
            try
            {
                for (int index = addedDevices.Count - 1; index >= 0; index--)
                {
                    InputDevice device = addedDevices[index];
                    if (device != null && device.added)
                    {
                        InputSystem.RemoveDevice(device);
                    }
                }

                addedDevices.Clear();
                InputSystem.Update();
            }
            finally
            {
                base.TearDown();
            }
        }

        [UnityTest]
        public IEnumerator SceneLoad_ConstructsOneExplicitPlayerRoomCameraAndLifecycle()
        {
            yield return LoadPlayground();

            GameObject root = RequireRoot();
            MonoBehaviour harness = RequireCompositionHarness(root);
            Rigidbody2D body = RequirePlayerBody(harness);
            MovementActorLifecycle lifecycle =
                RequireProperty<MovementActorLifecycle>(harness, "MovementLifecycle");
            Camera camera = RequireProperty<Camera>(harness, "FollowCamera");
            Vector2 roomSize = RequireProperty<Vector2>(harness, "RoomInteriorSize");

            Assert.That(RequireProperty<bool>(harness, "IsReady"), Is.True);
            Assert.That(lifecycle.IsConstructed, Is.True);
            Assert.That(lifecycle.IsRunning, Is.True);
            Assert.That(lifecycle.Actor, Is.Not.Null);
            Assert.That(body.bodyType, Is.EqualTo(RigidbodyType2D.Dynamic));
            Assert.That(body.gravityScale, Is.Zero);
            Assert.That(
                (body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0,
                Is.True);
            Assert.That(body.GetComponents<Rigidbody2D>().Length, Is.EqualTo(1));
            Assert.That(body.GetComponents<MovementActorLifecycle>().Length, Is.EqualTo(1));
            Assert.That(body.GetComponents<PlayerMovementIntentAdapter>().Length, Is.EqualTo(1));
            Assert.That(body.GetComponents<MovementContact2DAdapter>().Length, Is.EqualTo(1));

            PlayerMovementIntentAdapter input = body.GetComponent<PlayerMovementIntentAdapter>();
            MovementContact2DAdapter contact = body.GetComponent<MovementContact2DAdapter>();
            Assert.That(input.IsConfigured, Is.True);
            Assert.That(input.IsAcceptingInput, Is.True);
            Assert.That(contact.IsConfigured, Is.True);
            Assert.That(contact.enabled, Is.True);

            Assert.That(camera, Is.Not.Null);
            Assert.That(camera.orthographic, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(RequireProperty<int>(harness, "RoomWallCount"), Is.EqualTo(4));

            float viewHeight = camera.orthographicSize * 2f;
            float viewWidth = viewHeight * ReferenceAspect;
            Assert.That(roomSize.x / viewWidth, Is.InRange(2f, 3f));
            Assert.That(roomSize.y / viewHeight, Is.InRange(2f, 3f));
        }

        [UnityTest]
        public IEnumerator MovementInput_DrivesAcceptedLifecycleAndPlayerBody()
        {
            Keyboard keyboard = AddInputDevice<Keyboard>();
            yield return LoadPlayground();

            MonoBehaviour harness = RequireCompositionHarness(RequireRoot());
            Rigidbody2D body = RequirePlayerBody(harness);
            float startX = body.position.x;

            QueueKeyboard(keyboard, Key.D);
            Assert.That(Invoke<bool>(harness, "StepForTest", 0.1d), Is.True);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(body.linearVelocity.y), Is.LessThan(VelocityTolerance));

            yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.GreaterThan(startX));
        }

        [UnityTest]
        public IEnumerator ThrusterInput_ConsumesOneChargeAndShowsIndicator()
        {
            Keyboard keyboard = AddInputDevice<Keyboard>();
            yield return LoadPlayground();

            MonoBehaviour harness = RequireCompositionHarness(RequireRoot());
            MovementActorLifecycle lifecycle =
                RequireProperty<MovementActorLifecycle>(harness, "MovementLifecycle");

            int fullCharges = lifecycle.Actor.MaximumThrusterCharges;
            QueueKeyboard(keyboard, Key.W, Key.Space);
            Assert.That(Invoke<bool>(harness, "StepForTest", 0.02d), Is.True);
            Invoke<object>(harness, "RefreshPresentationForTests");

            Assert.That(lifecycle.Actor.CurrentPhase, Is.Not.EqualTo(ThrusterBurstPhase.Ready));
            Assert.That(lifecycle.Actor.AvailableThrusterCharges, Is.EqualTo(fullCharges - 1));
            Assert.That(RequireProperty<bool>(harness, "ThrusterVisualActive"), Is.True);
        }

        [UnityTest]
        public IEnumerator CameraFollow_RemainsCenteredOnPlayer()
        {
            yield return LoadPlayground();

            MonoBehaviour harness = RequireCompositionHarness(RequireRoot());
            Rigidbody2D body = RequirePlayerBody(harness);
            Camera camera = RequireProperty<Camera>(harness, "FollowCamera");
            float expectedDepth = camera.transform.position.z;

            body.position = new Vector2(6.25f, -4.5f);
            Physics2D.SyncTransforms();
            Invoke<object>(harness, "RefreshCameraForTests");

            Assert.That(camera.transform.position.x, Is.EqualTo(body.position.x).Within(PositionTolerance));
            Assert.That(camera.transform.position.y, Is.EqualTo(body.position.y).Within(PositionTolerance));
            Assert.That(camera.transform.position.z, Is.EqualTo(expectedDepth).Within(PositionTolerance));
        }

        [UnityTest]
        public IEnumerator RestartDisableAndReentry_ClearVelocityWithoutDuplicates()
        {
            Keyboard keyboard = AddInputDevice<Keyboard>();
            yield return LoadPlayground();

            GameObject root = RequireRoot();
            MonoBehaviour harness = RequireCompositionHarness(root);
            Rigidbody2D body = RequirePlayerBody(harness);
            MovementActorLifecycle lifecycle =
                RequireProperty<MovementActorLifecycle>(harness, "MovementLifecycle");
            int placeholderSpriteId = RequireProperty<int>(harness, "PlaceholderSpriteInstanceId");

            QueueKeyboard(keyboard, Key.D, Key.Space);
            Invoke<bool>(harness, "StepForTest", 0.02d);
            Assert.That(body.linearVelocity.sqrMagnitude, Is.GreaterThan(0f));

            long previousGeneration = lifecycle.Actor.Generation;
            Invoke<object>(harness, "RestartPlayground");
            Assert.That(lifecycle.Actor.Generation, Is.EqualTo(previousGeneration + 1L));
            AssertBodyZero(body);
            Assert.That(body.position.x, Is.Zero.Within(PositionTolerance));
            Assert.That(body.position.y, Is.Zero.Within(PositionTolerance));

            for (int cycle = 0; cycle < 5; cycle++)
            {
                QueueKeyboard(keyboard);
                Invoke<bool>(harness, "StepForTest", 0.02d);
                QueueKeyboard(keyboard, Key.D, Key.Space);
                Invoke<bool>(harness, "StepForTest", 0.02d);
                Assert.That(body.linearVelocity.sqrMagnitude, Is.GreaterThan(0f));

                harness.enabled = false;
                yield return null;
                Assert.That(lifecycle.IsRunning, Is.False);
                AssertBodyZero(body);

                harness.enabled = true;
                yield return null;
                Assert.That(lifecycle.IsRunning, Is.True);
                AssertBodyZero(body);
                Assert.That(
                    RequireProperty<int>(harness, "PlaceholderSpriteInstanceId"),
                    Is.EqualTo(placeholderSpriteId));
                Assert.That(body.GetComponents<Rigidbody2D>().Length, Is.EqualTo(1));
                Assert.That(body.GetComponents<MovementActorLifecycle>().Length, Is.EqualTo(1));
                Assert.That(body.GetComponents<MovementFixedStepDriver>().Length, Is.Zero);
                Assert.That(
                    UnityEngine.Object.FindObjectsByType<MovementActorLifecycle>(FindObjectsSortMode.None).Length,
                    Is.EqualTo(1));
                Assert.That(
                    UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
                    Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator FinalVelocityWriter_IsOnlyAcceptedLifecyclePath()
        {
            yield return LoadPlayground();

            MonoBehaviour harness = RequireCompositionHarness(RequireRoot());
            Rigidbody2D body = RequirePlayerBody(harness);
            Type harnessType = harness.GetType();

            Assert.That(RequireProperty<int>(harness, "FinalVelocityWriterCount"), Is.EqualTo(1));
            Assert.That(RequireProperty<int>(harness, "SecondaryDriverCount"), Is.Zero);
            Assert.That(body.GetComponents<Rigidbody2D>().Length, Is.EqualTo(1));
            Assert.That(body.GetComponents<MovementActorLifecycle>().Length, Is.EqualTo(1));
            Assert.That(body.GetComponents<MovementFixedStepDriver>().Length, Is.Zero);
            Assert.That(
                harnessType.GetMethod(
                    "FixedUpdate",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null,
                "The test harness must not add a second fixed-step movement writer.");
        }

        private static IEnumerator LoadPlayground()
        {
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(operation, Is.Not.Null, "Unity did not begin loading the MT-013 scene.");

            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;

            Scene scene = SceneManager.GetSceneByName(SceneName);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
        }

        private static GameObject RequireRoot()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject root = roots.SingleOrDefault(
                candidate => string.Equals(candidate.name, RootName, StringComparison.Ordinal));
            Assert.That(root, Is.Not.Null, "The MT-013 root object is missing.");
            Assert.That(roots.Length, Is.EqualTo(1));
            return root;
        }

        private static MonoBehaviour RequireCompositionHarness(GameObject root)
        {
            MonoBehaviour[] candidates = root.GetComponents<MonoBehaviour>();
            MonoBehaviour harness = candidates.SingleOrDefault(
                candidate => candidate != null
                    && string.Equals(
                        candidate.GetType().FullName,
                        HarnessTypeName,
                        StringComparison.Ordinal));
            Assert.That(harness, Is.Not.Null, "The MT-013 composition harness is missing.");
            Assert.That(RequireProperty<bool>(harness, "IsCompositionRoot"), Is.True);
            return harness;
        }

        private static Rigidbody2D RequirePlayerBody(MonoBehaviour harness)
        {
            Rigidbody2D body = RequireProperty<Rigidbody2D>(harness, "PlayerBody");
            Assert.That(body, Is.Not.Null);
            return body;
        }

        private TDevice AddInputDevice<TDevice>()
            where TDevice : InputDevice
        {
            TDevice device = InputSystem.AddDevice<TDevice>();
            addedDevices.Add(device);
            return device;
        }

        private static void QueueKeyboard(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private static void AssertBodyZero(Rigidbody2D body)
        {
            Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(VelocityTolerance));
            Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(VelocityTolerance));
            Assert.That(body.angularVelocity, Is.EqualTo(0f).Within(VelocityTolerance));
        }

        private static T RequireProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Missing harness property " + propertyName + ".");
            return (T)property.GetValue(target);
        }

        private static T Invoke<T>(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing harness method " + methodName + ".");

            try
            {
                object result = method.Invoke(target, arguments);
                return result == null ? default(T) : (T)result;
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
#endif

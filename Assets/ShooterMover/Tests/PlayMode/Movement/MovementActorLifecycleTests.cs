#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShooterMover.Tests.PlayMode.Movement
{
    public sealed class MovementActorLifecycleTests : InputTestFixture
    {
        private const string ActionAssetPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Input/ShooterMoverMovement.inputactions";
        private const float VelocityTolerance = 0.00001f;

        private readonly List<InputDevice> addedDevices = new List<InputDevice>();
        private readonly List<UnityEngine.Object> createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public override void TearDown()
        {
            try
            {
                for (int index = createdObjects.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object value = createdObjects[index];
                    if (value != null)
                    {
                        UnityEngine.Object.DestroyImmediate(value);
                    }
                }

                createdObjects.Clear();

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

        [Test]
        public void ConstructAndStart_AreExplicitIdempotentAndUseOneDriver()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Fixture fixture = CreateFixture();

            Assert.That(fixture.Lifecycle.IsConstructed, Is.True);
            Assert.That(fixture.Lifecycle.IsRunning, Is.False);
            Assert.That(fixture.Input.enabled, Is.False);
            Assert.That(fixture.Contact.enabled, Is.False);
            AssertBodyZero(fixture.Body);

            Assert.That(
                fixture.Lifecycle.Construct(
                    fixture.Body,
                    fixture.Input,
                    fixture.InputActions,
                    fixture.Contact,
                    fixture.Tuning),
                Is.False);

            Assert.That(fixture.GameObject.GetComponents<MovementActorLifecycle>().Length, Is.EqualTo(1));
            Assert.That(fixture.GameObject.GetComponents<MovementFixedStepDriver>().Length, Is.Zero);

            Assert.That(fixture.Lifecycle.StartActor(), Is.True);
            Assert.That(fixture.Lifecycle.StartActor(), Is.False);
            Assert.That(fixture.Lifecycle.IsRunning, Is.True);
            Assert.That(fixture.Input.IsAcceptingInput, Is.True);
            Assert.That(fixture.Contact.enabled, Is.True);

            QueueKeyboard(keyboard, Key.D);
            Assert.That(fixture.Lifecycle.ExecuteFixedStep(0.1d), Is.True);
            Assert.That(fixture.Body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(fixture.Lifecycle.Actor.FixedStepCount, Is.EqualTo(1L));
        }

        [Test]
        public void StopAndDispose_AreIdempotentAndClearEveryBoundary()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Fixture fixture = CreateFixture();
            fixture.Lifecycle.StartActor();

            QueueKeyboard(keyboard, Key.D, Key.Space);
            fixture.Lifecycle.ExecuteFixedStep(0.02d);
            Assert.That(fixture.Body.linearVelocity.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(
                fixture.Lifecycle.Actor.AvailableThrusterCharges,
                Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges - 1));

            Assert.That(fixture.Lifecycle.StopActor(), Is.True);
            Assert.That(fixture.Lifecycle.StopActor(), Is.False);
            Assert.That(fixture.Lifecycle.IsRunning, Is.False);
            Assert.That(fixture.Input.enabled, Is.False);
            Assert.That(fixture.Contact.enabled, Is.False);
            AssertBodyZero(fixture.Body);

            double velocityX;
            double velocityY;
            Assert.That(
                fixture.Lifecycle.Actor.TryReadVelocity(out velocityX, out velocityY),
                Is.False);
            Assert.That(velocityX, Is.Zero);
            Assert.That(velocityY, Is.Zero);

            Assert.That(fixture.Lifecycle.DisposeActor(), Is.True);
            Assert.That(fixture.Lifecycle.DisposeActor(), Is.False);
            Assert.That(fixture.Lifecycle.IsDisposed, Is.True);
            Assert.That(fixture.Lifecycle.Actor.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => fixture.Lifecycle.StartActor());
        }

        [Test]
        public void Restart_ClearsDomainBodyAndHeldInputUntilFreshNeutral()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Fixture fixture = CreateFixture();
            fixture.Lifecycle.StartActor();

            QueueKeyboard(keyboard, Key.D, Key.Space);
            fixture.Lifecycle.ExecuteFixedStep(0.02d);
            Assert.That(
                fixture.Lifecycle.Actor.AvailableThrusterCharges,
                Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges - 1));
            Assert.That(fixture.Body.linearVelocity.sqrMagnitude, Is.GreaterThan(0f));

            long previousGeneration = fixture.Lifecycle.Actor.Generation;
            Assert.That(fixture.Lifecycle.RestartActor(), Is.True);
            Assert.That(fixture.Lifecycle.Actor.Generation, Is.EqualTo(previousGeneration + 1L));
            Assert.That(fixture.Lifecycle.Actor.FixedStepCount, Is.Zero);
            Assert.That(
                fixture.Lifecycle.Actor.AvailableThrusterCharges,
                Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges));
            AssertBodyZero(fixture.Body);

            // The keyboard still reports the pre-restart held state. MT-007 must suppress it.
            fixture.Lifecycle.ExecuteFixedStep(0.02d);
            AssertBodyZero(fixture.Body);
            Assert.That(
                fixture.Lifecycle.Actor.AvailableThrusterCharges,
                Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges));

            QueueKeyboard(keyboard);
            fixture.Lifecycle.ExecuteFixedStep(0.02d);
            AssertBodyZero(fixture.Body);

            QueueKeyboard(keyboard, Key.D, Key.Space);
            fixture.Lifecycle.ExecuteFixedStep(0.02d);
            Assert.That(fixture.Body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(
                fixture.Lifecycle.Actor.AvailableThrusterCharges,
                Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges - 1));
        }

        [Test]
        public void FiftyRestartCycles_LeaveNoStaleInputCallbacksOrDrivers()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Fixture fixture = CreateFixture();
            fixture.Lifecycle.StartActor();

            for (int cycle = 0; cycle < 50; cycle++)
            {
                QueueKeyboard(keyboard, Key.D, Key.Space);
                Assert.That(fixture.Lifecycle.ExecuteFixedStep(0.02d), Is.True);
                Assert.That(
                    fixture.Lifecycle.Actor.AvailableThrusterCharges,
                    Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges - 1),
                    "Fresh activation must consume exactly one charge at cycle " + cycle + ".");

                fixture.Lifecycle.RestartActor();
                Assert.That(fixture.Lifecycle.Actor.Generation, Is.EqualTo(cycle + 2L));
                Assert.That(fixture.Lifecycle.Actor.FixedStepCount, Is.Zero);
                Assert.That(
                    fixture.Lifecycle.Actor.AvailableThrusterCharges,
                    Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges));
                AssertBodyZero(fixture.Body);

                // Held input from the prior generation cannot become a fresh press.
                Assert.That(fixture.Lifecycle.ExecuteFixedStep(0.02d), Is.True);
                Assert.That(fixture.Lifecycle.Actor.FixedStepCount, Is.EqualTo(1L));
                Assert.That(
                    fixture.Lifecycle.Actor.AvailableThrusterCharges,
                    Is.EqualTo(fixture.Lifecycle.Actor.MaximumThrusterCharges));
                AssertBodyZero(fixture.Body);

                QueueKeyboard(keyboard);
                Assert.That(fixture.Lifecycle.ExecuteFixedStep(0.02d), Is.True);
                Assert.That(fixture.Lifecycle.Actor.FixedStepCount, Is.EqualTo(2L));
                AssertBodyZero(fixture.Body);
            }

            Assert.That(fixture.Lifecycle.Actor.Generation, Is.EqualTo(51L));
            Assert.That(fixture.GameObject.GetComponents<MovementActorLifecycle>().Length, Is.EqualTo(1));
            Assert.That(fixture.GameObject.GetComponents<MovementFixedStepDriver>().Length, Is.Zero);
            Assert.That(fixture.Input.enabled, Is.True);
            Assert.That(fixture.Contact.enabled, Is.True);
        }

        [Test]
        public void ContactAndBodyIntegration_RejectsPendingRestartContactThenUsesFreshAuthority()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Fixture fixture = CreateFixture();
            fixture.Lifecycle.StartActor();
            Collider2D wall = CreateWallCollider();

            QueueKeyboard(keyboard, Key.A);
            fixture.Lifecycle.ExecuteFixedStep(0.1d);
            Assert.That(fixture.Lifecycle.Actor.CurrentVelocityX, Is.LessThan(0d));

            fixture.Contact.BeginFixedStep(1L);
            Assert.That(
                fixture.Contact.TryProcessContact(wall, Vector2.right, 1d),
                Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(fixture.Lifecycle.Actor.CurrentVelocityX, Is.GreaterThan(0d));
            Assert.That(fixture.Body.linearVelocity.x, Is.GreaterThan(0f));

            fixture.Lifecycle.RestartActor();
            fixture.Contact.BeginFixedStep(2L);
            Assert.That(
                fixture.Contact.TryProcessContact(wall, Vector2.right, 2d),
                Is.EqualTo(MovementContact2DProcessResult.AuthorityUnavailable));
            AssertBodyZero(fixture.Body);

            QueueKeyboard(keyboard);
            fixture.Lifecycle.ExecuteFixedStep(0.02d);
            QueueKeyboard(keyboard, Key.A);
            fixture.Lifecycle.ExecuteFixedStep(0.1d);
            Assert.That(fixture.Lifecycle.Actor.CurrentVelocityX, Is.LessThan(0d));

            fixture.Contact.BeginFixedStep(3L);
            Assert.That(
                fixture.Contact.TryProcessContact(wall, Vector2.right, 3d),
                Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(
                fixture.Body.linearVelocity.x,
                Is.EqualTo((float)fixture.Lifecycle.Actor.CurrentVelocityX)
                    .Within(VelocityTolerance));
        }

        private Fixture CreateFixture()
        {
            InputActionAsset imported =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);
            Assert.That(imported, Is.Not.Null, "The merged MT-007 action asset did not import.");

            InputActionAsset runtimeAsset = InputActionAsset.FromJson(imported.ToJson());
            createdObjects.Add(runtimeAsset);

            GameObject gameObject = new GameObject("MT-010 movement actor lifecycle");
            createdObjects.Add(gameObject);

            Rigidbody2D body = gameObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            PlayerMovementIntentAdapter input =
                gameObject.AddComponent<PlayerMovementIntentAdapter>();
            MovementContact2DAdapter contact =
                gameObject.AddComponent<MovementContact2DAdapter>();
            MovementActorLifecycle lifecycle =
                gameObject.AddComponent<MovementActorLifecycle>();
            MovementThrusterTuningProfile tuning = BuildTuning();

            Assert.That(
                lifecycle.Construct(body, input, runtimeAsset, contact, tuning),
                Is.True);

            return new Fixture(
                gameObject,
                body,
                input,
                runtimeAsset,
                contact,
                lifecycle,
                tuning);
        }

        private Collider2D CreateWallCollider()
        {
            GameObject wall = new GameObject("MT-010 explicit wall contact");
            createdObjects.Add(wall);
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            wall.AddComponent<MovementActorLifecycleWallContract>();
            return collider;
        }

        private TDevice AddDevice<TDevice>()
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
        }

        private static MovementThrusterTuningProfile BuildTuning()
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.mt-010-tests"),
                12d,
                50d,
                60d,
                90d,
                1.25d,
                2,
                1,
                1.75d,
                2.5d,
                0.3d,
                0.1d,
                0.05d,
                120d,
                0.04d,
                0.2d,
                0.75d,
                2d,
                0.8d,
                0.15d,
                5d,
                4,
                0.8d,
                0.9d,
                0.1d,
                0.5d,
                0.02d,
                128);
        }

        private sealed class Fixture
        {
            public Fixture(
                GameObject gameObject,
                Rigidbody2D body,
                PlayerMovementIntentAdapter input,
                InputActionAsset inputActions,
                MovementContact2DAdapter contact,
                MovementActorLifecycle lifecycle,
                MovementThrusterTuningProfile tuning)
            {
                GameObject = gameObject;
                Body = body;
                Input = input;
                InputActions = inputActions;
                Contact = contact;
                Lifecycle = lifecycle;
                Tuning = tuning;
            }

            public GameObject GameObject { get; }

            public Rigidbody2D Body { get; }

            public PlayerMovementIntentAdapter Input { get; }

            public InputActionAsset InputActions { get; }

            public MovementContact2DAdapter Contact { get; }

            public MovementActorLifecycle Lifecycle { get; }

            public MovementThrusterTuningProfile Tuning { get; }
        }
    }

    public sealed class MovementActorLifecycleWallContract :
        MonoBehaviour,
        IMovementContact2DContract
    {
        public bool TryDescribeMovementContact(out MovementContact2DDescriptor descriptor)
        {
            descriptor = MovementContact2DDescriptor.Wall();
            return true;
        }
    }
}
#endif

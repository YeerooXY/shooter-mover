#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Input;
using ShooterMover.UnityAdapters.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShooterMover.Tests.PlayMode.Movement
{
    // Virtual-device intent traces must run against Input System's isolated
    // test runtime rather than the host editor's native input state.
    public sealed class PlayerMovementIntentAdapterTests : InputTestFixture
    {
        private const string ActionAssetPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Input/ShooterMoverMovement.inputactions";

        private readonly List<InputDevice> addedDevices = new List<InputDevice>();
        private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

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
        public void ActionAsset_DefinesOnlyMovementAimAndThrusterForKeyboardMouseAndGamepad()
        {
            InputActionAsset asset = LoadImportedAsset();
            InputActionMap map = asset.FindActionMap("Movement", true);

            CollectionAssert.AreEquivalent(
                new[] { "Move", "Aim", "Thruster" },
                map.actions.Select(action => action.name).ToArray());

            AssertBinding(map, "Move", "<Keyboard>/w", "KeyboardMouse");
            AssertBinding(map, "Move", "<Keyboard>/s", "KeyboardMouse");
            AssertBinding(map, "Move", "<Keyboard>/a", "KeyboardMouse");
            AssertBinding(map, "Move", "<Keyboard>/d", "KeyboardMouse");
            AssertBinding(map, "Move", "<Gamepad>/leftStick", "Gamepad");
            AssertBinding(map, "Aim", "<Mouse>/delta", "KeyboardMouse");
            AssertBinding(map, "Aim", "<Gamepad>/rightStick", "Gamepad");
            AssertBinding(map, "Thruster", "<Keyboard>/space", "KeyboardMouse");
            AssertBinding(map, "Thruster", "<Gamepad>/buttonSouth", "Gamepad");

            CollectionAssert.AreEquivalent(
                new[] { "KeyboardMouse", "Gamepad" },
                asset.controlSchemes.Select(scheme => scheme.name).ToArray());

            Assert.That(
                map.bindings.Any(binding =>
                    binding.path.IndexOf("Touch", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
            Assert.That(
                map.actions.Any(action =>
                    action.name.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void ThrusterPhases_PreservePressHoldReleaseTapAndReleaseThenPress()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            PlayerMovementIntentAdapter adapter = CreateAdapter();

            QueueKeyboard(keyboard, Key.Space);
            PlayerIntentFrame pressed = adapter.ReadIntentFrame();
            Assert.That(pressed.Thruster, Is.EqualTo(ButtonIntent.Pressed));

            PlayerIntentFrame held = adapter.ReadIntentFrame();
            Assert.That(held.Thruster, Is.EqualTo(ButtonIntent.Held));

            QueueKeyboard(keyboard);
            PlayerIntentFrame released = adapter.ReadIntentFrame();
            Assert.That(released.Thruster, Is.EqualTo(ButtonIntent.Released));

            QueueKeyboard(keyboard, Key.Space);
            QueueKeyboard(keyboard);
            PlayerIntentFrame tap = adapter.ReadIntentFrame();
            Assert.That(tap.Thruster, Is.EqualTo(ButtonIntent.Tap));

            QueueKeyboard(keyboard, Key.Space);
            adapter.ReadIntentFrame();
            QueueKeyboard(keyboard);
            QueueKeyboard(keyboard, Key.Space);
            PlayerIntentFrame releaseThenPress = adapter.ReadIntentFrame();
            Assert.That(releaseThenPress.Thruster, Is.EqualTo(ButtonIntent.ReleaseThenPress));
        }

        [Test]
        public void KeyboardMouse_SimultaneousMoveAimAndThrusterProduceOneCs003Frame()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            PlayerMovementIntentAdapter adapter = CreateAdapter();

            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                new Vector2(-0.6f, 0.8f),
                Key.W,
                Key.D,
                Key.Space);

            PlayerIntentFrame frame = adapter.ReadIntentFrame();

            AssertVector(frame.Move, Mathf.Sqrt(0.5f), Mathf.Sqrt(0.5f));
            AssertVector(frame.Aim, -0.6f, 0.8f);
            Assert.That(frame.Thruster, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(frame.Fire, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.PowerModifier, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.Interact, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.Map, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.PauseMenu, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.UiNavigation, Is.EqualTo(NormalizedIntentVector2.Zero));
        }

        [Test]
        public void EquivalentKeyboardMouseAndGamepadInputProduceEquivalentIntents()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            Gamepad gamepad = AddDevice<Gamepad>();

            PlayerMovementIntentAdapter keyboardAdapter = CreateAdapter();
            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                new Vector2(-0.6f, 0.8f),
                Key.W,
                Key.D,
                Key.Space);
            PlayerIntentFrame keyboardFrame = keyboardAdapter.ReadIntentFrame();
            DestroyAdapter(keyboardAdapter);
            QueueKeyboard(keyboard);
            QueueMouse(mouse, Vector2.zero);

            PlayerMovementIntentAdapter gamepadAdapter = CreateAdapter();
            QueueGamepad(
                gamepad,
                new Vector2(Mathf.Sqrt(0.5f), Mathf.Sqrt(0.5f)),
                new Vector2(-0.6f, 0.8f),
                true);
            PlayerIntentFrame gamepadFrame = gamepadAdapter.ReadIntentFrame();

            AssertMovementEquivalent(keyboardFrame, gamepadFrame);
            TestContext.WriteLine("TRACE keyboard " + FormatMovementTrace(keyboardFrame));
            TestContext.WriteLine("TRACE gamepad " + FormatMovementTrace(gamepadFrame));
        }

        [Test]
        public void FocusLossWhileThrusterHeldEmitsOneReleaseBoundaryThenNeutral()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            PlayerMovementIntentAdapter adapter = CreateAdapter();

            QueueKeyboard(keyboard, Key.W, Key.Space);
            PlayerIntentFrame active = adapter.ReadIntentFrame();
            Assert.That(active.Thruster.IsHeld, Is.True);

            adapter.gameObject.SendMessage(
                "OnApplicationFocus",
                false,
                SendMessageOptions.RequireReceiver);

            PlayerIntentFrame focusBoundary = adapter.ReadIntentFrame();
            Assert.That(focusBoundary.WasFocusLost, Is.True);
            Assert.That(focusBoundary.Move, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(focusBoundary.Aim, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(focusBoundary.Thruster, Is.EqualTo(ButtonIntent.Released));

            PlayerIntentFrame unfocused = adapter.ReadIntentFrame();
            AssertNeutral(unfocused);

            adapter.gameObject.SendMessage(
                "OnApplicationFocus",
                true,
                SendMessageOptions.RequireReceiver);

            PlayerIntentFrame staleHeldSuppressed = adapter.ReadIntentFrame();
            AssertNeutral(staleHeldSuppressed);

            QueueKeyboard(keyboard);
            AssertNeutral(adapter.ReadIntentFrame());

            QueueKeyboard(keyboard, Key.Space);
            Assert.That(adapter.ReadIntentFrame().Thruster, Is.EqualTo(ButtonIntent.Pressed));
        }

        [Test]
        public void DisableClearsHeldStateAndRequiresFreshNeutralBeforeAnotherPress()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            PlayerMovementIntentAdapter adapter = CreateAdapter();

            QueueKeyboard(keyboard, Key.Space);
            Assert.That(adapter.ReadIntentFrame().Thruster, Is.EqualTo(ButtonIntent.Pressed));

            adapter.enabled = false;
            AssertNeutral(adapter.ReadIntentFrame());

            adapter.enabled = true;
            AssertNeutral(adapter.ReadIntentFrame());

            QueueKeyboard(keyboard);
            AssertNeutral(adapter.ReadIntentFrame());

            QueueKeyboard(keyboard, Key.Space);
            Assert.That(adapter.ReadIntentFrame().Thruster, Is.EqualTo(ButtonIntent.Pressed));
        }

        [Test]
        public void ContributingDeviceDisableClearsHeldStateWithoutPublishingDeviceIdentity()
        {
            Gamepad gamepad = AddDevice<Gamepad>();
            PlayerMovementIntentAdapter adapter = CreateAdapter();

            QueueGamepad(gamepad, Vector2.right, Vector2.up, true);
            PlayerIntentFrame active = adapter.ReadIntentFrame();
            Assert.That(active.Thruster.IsHeld, Is.True);

            InputSystem.DisableDevice(gamepad);
            InputSystem.Update();

            PlayerIntentFrame cleared = adapter.ReadIntentFrame();
            AssertNeutral(cleared);

            InputSystem.EnableDevice(gamepad);
            QueueGamepad(gamepad, Vector2.zero, Vector2.zero, false);
            AssertNeutral(adapter.ReadIntentFrame());

            QueueGamepad(gamepad, Vector2.right, Vector2.up, true);
            Assert.That(adapter.ReadIntentFrame().Thruster, Is.EqualTo(ButtonIntent.Pressed));

            Assert.That(
                PublicSurfaceExposesDeviceIdentifier(typeof(PlayerMovementIntentAdapter)),
                Is.False);
            Assert.That(
                typeof(PlayerMovementIntentAdapter)
                    .GetMethod(nameof(PlayerMovementIntentAdapter.ReadIntentFrame))
                    .ReturnType,
                Is.EqualTo(typeof(PlayerIntentFrame)));
        }

        [Test]
        public void Eh002KeyboardAndGamepadFixtureProjectsToTheSameMovementIntentTrace()
        {
            PlayerIntentFrame[] fixtureKeyboard = ResolveEvidenceFixture("ResolveKeyboardMouse");
            PlayerIntentFrame[] fixtureGamepad = ResolveEvidenceFixture("ResolveGamepad");

            Assert.That(fixtureKeyboard.Length, Is.EqualTo(fixtureGamepad.Length));
            for (int index = 0; index < fixtureKeyboard.Length; index++)
            {
                AssertMovementEquivalent(fixtureKeyboard[index], fixtureGamepad[index]);
            }

            PlayerIntentFrame[] actualKeyboard = DriveKeyboardFixtureTrace();
            PlayerIntentFrame[] actualGamepad = DriveGamepadFixtureTrace();

            Assert.That(actualKeyboard.Length, Is.EqualTo(fixtureKeyboard.Length));
            Assert.That(actualGamepad.Length, Is.EqualTo(fixtureGamepad.Length));

            for (int index = 0; index < fixtureKeyboard.Length; index++)
            {
                AssertMovementEquivalent(fixtureKeyboard[index], actualKeyboard[index]);
                AssertMovementEquivalent(fixtureGamepad[index], actualGamepad[index]);
                AssertMovementEquivalent(actualKeyboard[index], actualGamepad[index]);

                TestContext.WriteLine(
                    "TRACE index=" + index
                    + " keyboard=" + FormatMovementTrace(actualKeyboard[index])
                    + " gamepad=" + FormatMovementTrace(actualGamepad[index]));
            }
        }

        private PlayerIntentFrame[] DriveKeyboardFixtureTrace()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            PlayerMovementIntentAdapter adapter = CreateAdapter();

            PlayerIntentFrame[] frames = new PlayerIntentFrame[5];
            frames[0] = adapter.ReadIntentFrame();

            QueueKeyboardAndMouse(keyboard, mouse, Vector2.up, Key.D);
            frames[1] = adapter.ReadIntentFrame();

            QueueKeyboardAndMouse(keyboard, mouse, new Vector2(-1f, 1f), Key.W, Key.D, Key.Space);
            frames[2] = adapter.ReadIntentFrame();

            QueueKeyboardAndMouse(keyboard, mouse, Vector2.left, Key.W);
            frames[3] = adapter.ReadIntentFrame();

            QueueKeyboardAndMouse(keyboard, mouse, Vector2.zero);
            frames[4] = adapter.ReadIntentFrame();

            DestroyAdapter(adapter);
            QueueKeyboard(keyboard);
            QueueMouse(mouse, Vector2.zero);
            return frames;
        }

        private PlayerIntentFrame[] DriveGamepadFixtureTrace()
        {
            Gamepad gamepad = AddDevice<Gamepad>();
            PlayerMovementIntentAdapter adapter = CreateAdapter();

            PlayerIntentFrame[] frames = new PlayerIntentFrame[5];
            frames[0] = adapter.ReadIntentFrame();

            QueueGamepad(gamepad, Vector2.right, Vector2.up, false);
            frames[1] = adapter.ReadIntentFrame();

            QueueGamepad(gamepad, new Vector2(1f, 1f), new Vector2(-1f, 1f), true);
            frames[2] = adapter.ReadIntentFrame();

            QueueGamepad(gamepad, Vector2.up, Vector2.left, false);
            frames[3] = adapter.ReadIntentFrame();

            QueueGamepad(gamepad, Vector2.zero, Vector2.zero, false);
            frames[4] = adapter.ReadIntentFrame();

            return frames;
        }

        private PlayerMovementIntentAdapter CreateAdapter()
        {
            InputActionAsset imported = LoadImportedAsset();
            InputActionAsset runtimeAsset = InputActionAsset.FromJson(imported.ToJson());
            createdObjects.Add(runtimeAsset);

            GameObject gameObject = new GameObject("Player Movement Intent Adapter Test");
            createdObjects.Add(gameObject);

            PlayerMovementIntentAdapter adapter =
                gameObject.AddComponent<PlayerMovementIntentAdapter>();
            adapter.Configure(runtimeAsset);
            return adapter;
        }

        private void DestroyAdapter(PlayerMovementIntentAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }

            GameObject gameObject = adapter.gameObject;
            createdObjects.Remove(gameObject);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private TDevice AddDevice<TDevice>()
            where TDevice : InputDevice
        {
            TDevice device = InputSystem.AddDevice<TDevice>();
            addedDevices.Add(device);
            return device;
        }

        private static InputActionAsset LoadImportedAsset()
        {
            InputActionAsset asset =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);
            Assert.That(asset, Is.Not.Null, "The MT-007 action asset did not import.");
            return asset;
        }

        private static void QueueKeyboard(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private static void QueueMouse(Mouse mouse, Vector2 delta)
        {
            InputSystem.QueueDeltaStateEvent(mouse.delta, delta);
            InputSystem.Update();
        }

        private static void QueueKeyboardAndMouse(
            Keyboard keyboard,
            Mouse mouse,
            Vector2 aimDelta,
            params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.QueueDeltaStateEvent(mouse.delta, aimDelta);
            InputSystem.Update();
        }

        private static void QueueGamepad(
            Gamepad gamepad,
            Vector2 move,
            Vector2 aim,
            bool thruster)
        {
            GamepadState state = new GamepadState
            {
                leftStick = move,
                rightStick = aim,
            };

            if (thruster)
            {
                state = state.WithButton(GamepadButton.South);
            }

            InputSystem.QueueStateEvent(gamepad, state);
            InputSystem.Update();
        }

        private static void AssertBinding(
            InputActionMap map,
            string actionName,
            string path,
            string group)
        {
            InputAction action = map.FindAction(actionName, true);
            Assert.That(
                action.bindings.Any(binding =>
                    string.Equals(binding.path, path, StringComparison.Ordinal)
                    && BindingContainsGroup(binding.groups, group)),
                Is.True,
                actionName + " is missing binding " + path + " in group " + group + ".");
        }

        private static bool BindingContainsGroup(string groups, string expected)
        {
            if (string.IsNullOrEmpty(groups))
            {
                return false;
            }

            return groups.Split(';').Any(group =>
                string.Equals(group, expected, StringComparison.Ordinal));
        }

        private static void AssertMovementEquivalent(
            PlayerIntentFrame expected,
            PlayerIntentFrame actual)
        {
            AssertVector(actual.Move, expected.Move.X, expected.Move.Y);
            AssertVector(actual.Aim, expected.Aim.X, expected.Aim.Y);
            Assert.That(actual.Thruster, Is.EqualTo(expected.Thruster));
            Assert.That(actual.WasFocusLost, Is.EqualTo(expected.WasFocusLost));
        }

        private static void AssertVector(
            NormalizedIntentVector2 actual,
            float expectedX,
            float expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(0.0001f));
        }

        private static void AssertNeutral(PlayerIntentFrame frame)
        {
            Assert.That(frame.Move, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(frame.Aim, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(frame.Thruster, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.WasFocusLost, Is.False);
        }

        private static PlayerIntentFrame[] ResolveEvidenceFixture(string methodName)
        {
            const string typeName =
                "ShooterMover.TestSupport.EvidenceHarness.EvidenceIntentFixture";

            Type fixtureType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(type => type != null);

            Assert.That(
                fixtureType,
                Is.Not.Null,
                "EH-002 EvidenceIntentFixture was not found in the predefined assembly.");

            MethodInfo method = fixtureType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            object result = method.Invoke(null, new object[] { 1 });
            Assert.That(result, Is.TypeOf<PlayerIntentFrame[]>());
            return (PlayerIntentFrame[])result;
        }

        private static bool PublicSurfaceExposesDeviceIdentifier(Type adapterType)
        {
            foreach (PropertyInfo property in adapterType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (ContainsDeviceIdentifierType(property.PropertyType))
                {
                    return true;
                }
            }

            foreach (MethodInfo method in adapterType.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (ContainsDeviceIdentifierType(method.ReturnType)
                    || method.GetParameters().Any(parameter =>
                        ContainsDeviceIdentifierType(parameter.ParameterType)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDeviceIdentifierType(Type type)
        {
            return typeof(InputDevice).IsAssignableFrom(type)
                || typeof(InputControl).IsAssignableFrom(type);
        }

        private static string FormatMovementTrace(PlayerIntentFrame frame)
        {
            return "move=(" + frame.Move.X.ToString("0.###")
                + "," + frame.Move.Y.ToString("0.###")
                + ") aim=(" + frame.Aim.X.ToString("0.###")
                + "," + frame.Aim.Y.ToString("0.###")
                + ") thruster=" + frame.Thruster.IsHeld
                + "/" + frame.Thruster.WasPressed
                + "/" + frame.Thruster.WasReleased
                + " focus=" + frame.WasFocusLost;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Input;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShooterMover.Tests.PlayMode.Combat
{
    // Virtual-device traces must use Input System's isolated test runtime rather
    // than native editor input state.
    public sealed class PlayerCombatIntentAdapterTests : InputTestFixture
    {
        private const string CombatActionAssetPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Input/ShooterMoverCombat.inputactions";
        private const string MovementActionAssetPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Input/ShooterMoverMovement.inputactions";

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
        public void ActionAsset_DefinesOnlyCombatActionsForKeyboardMouseAndGamepad()
        {
            InputActionAsset asset = LoadImportedAsset(CombatActionAssetPath);
            InputActionMap map = asset.FindActionMap("Combat", true);

            CollectionAssert.AreEquivalent(
                new[] { "Aim", "Fire", "Power" },
                map.actions.Select(action => action.name).ToArray());

            AssertBinding(map, "Aim", "<Mouse>/delta", "KeyboardMouse");
            AssertBinding(map, "Aim", "<Gamepad>/rightStick", "Gamepad");
            AssertBinding(map, "Fire", "<Mouse>/leftButton", "KeyboardMouse");
            AssertBinding(map, "Fire", "<Keyboard>/leftCtrl", "KeyboardMouse");
            AssertBinding(map, "Fire", "<Gamepad>/rightTrigger", "Gamepad");
            AssertBinding(map, "Power", "<Mouse>/rightButton", "KeyboardMouse");
            AssertBinding(map, "Power", "<Keyboard>/leftShift", "KeyboardMouse");
            AssertBinding(map, "Power", "<Gamepad>/leftTrigger", "Gamepad");

            CollectionAssert.AreEquivalent(
                new[] { "KeyboardMouse", "Gamepad" },
                asset.controlSchemes.Select(scheme => scheme.name).ToArray());

            Assert.That(
                map.bindings.Any(binding =>
                    binding.path.IndexOf("Touch", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
            Assert.That(
                map.actions.Any(action =>
                    string.Equals(action.name, "Move", StringComparison.Ordinal)
                    || string.Equals(action.name, "Thruster", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void FireAndPowerPhases_PreservePressHoldReleaseTapAndReleaseThenPress()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            AddDevice<Mouse>();
            PlayerCombatIntentAdapter adapter = CreateCombatAdapter();

            QueueKeyboard(keyboard, Key.LeftCtrl, Key.LeftShift);
            PlayerIntentFrame pressed = adapter.ReadIntentFrame();
            Assert.That(pressed.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(pressed.PowerModifier, Is.EqualTo(ButtonIntent.Pressed));

            PlayerIntentFrame held = adapter.ReadIntentFrame();
            Assert.That(held.Fire, Is.EqualTo(ButtonIntent.Held));
            Assert.That(held.PowerModifier, Is.EqualTo(ButtonIntent.Held));

            QueueKeyboard(keyboard);
            PlayerIntentFrame released = adapter.ReadIntentFrame();
            Assert.That(released.Fire, Is.EqualTo(ButtonIntent.Released));
            Assert.That(released.PowerModifier, Is.EqualTo(ButtonIntent.Released));

            QueueKeyboard(keyboard, Key.LeftCtrl, Key.LeftShift);
            QueueKeyboard(keyboard);
            PlayerIntentFrame tap = adapter.ReadIntentFrame();
            Assert.That(tap.Fire, Is.EqualTo(ButtonIntent.Tap));
            Assert.That(tap.PowerModifier, Is.EqualTo(ButtonIntent.Tap));

            QueueKeyboard(keyboard, Key.LeftCtrl, Key.LeftShift);
            adapter.ReadIntentFrame();
            QueueKeyboard(keyboard);
            QueueKeyboard(keyboard, Key.LeftCtrl, Key.LeftShift);
            PlayerIntentFrame releaseThenPress = adapter.ReadIntentFrame();
            Assert.That(releaseThenPress.Fire, Is.EqualTo(ButtonIntent.ReleaseThenPress));
            Assert.That(
                releaseThenPress.PowerModifier,
                Is.EqualTo(ButtonIntent.ReleaseThenPress));
        }

        [Test]
        public void EquivalentKeyboardMouseAndGamepadInputProduceEquivalentCombatIntents()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            Gamepad gamepad = AddDevice<Gamepad>();

            PlayerCombatIntentAdapter keyboardAdapter = CreateCombatAdapter();
            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                new Vector2(-0.6f, 0.8f),
                Key.LeftCtrl,
                Key.LeftShift);
            PlayerIntentFrame keyboardFrame = keyboardAdapter.ReadIntentFrame();
            DestroyAdapter(keyboardAdapter);
            QueueKeyboardAndMouse(keyboard, mouse, Vector2.zero);

            PlayerCombatIntentAdapter gamepadAdapter = CreateCombatAdapter();
            QueueGamepad(gamepad, new Vector2(-0.6f, 0.8f), true, true);
            PlayerIntentFrame gamepadFrame = gamepadAdapter.ReadIntentFrame();

            AssertCombatEquivalent(keyboardFrame, gamepadFrame);
            AssertCombatOnly(keyboardFrame);
            AssertCombatOnly(gamepadFrame);
            TestContext.WriteLine("TRACE keyboard/mouse " + FormatCombatTrace(keyboardFrame));
            TestContext.WriteLine("TRACE gamepad " + FormatCombatTrace(gamepadFrame));
        }

        [Test]
        public void FocusLossWhileFireAndPowerHeldEmitsOneReleaseBoundaryThenNeutral()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            PlayerCombatIntentAdapter adapter = CreateCombatAdapter();

            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                Vector2.up,
                Key.LeftCtrl,
                Key.LeftShift);
            PlayerIntentFrame active = adapter.ReadIntentFrame();
            Assert.That(active.Fire.IsHeld, Is.True);
            Assert.That(active.PowerModifier.IsHeld, Is.True);

            adapter.gameObject.SendMessage(
                "OnApplicationFocus",
                false,
                SendMessageOptions.RequireReceiver);

            PlayerIntentFrame boundary = adapter.ReadIntentFrame();
            Assert.That(boundary.WasFocusLost, Is.True);
            Assert.That(boundary.Aim, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(boundary.Fire, Is.EqualTo(ButtonIntent.Released));
            Assert.That(boundary.PowerModifier, Is.EqualTo(ButtonIntent.Released));

            AssertNeutralCombat(adapter.ReadIntentFrame());

            adapter.gameObject.SendMessage(
                "OnApplicationFocus",
                true,
                SendMessageOptions.RequireReceiver);
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueKeyboardAndMouse(keyboard, mouse, Vector2.zero);
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                Vector2.right,
                Key.LeftCtrl,
                Key.LeftShift);
            PlayerIntentFrame freshPress = adapter.ReadIntentFrame();
            Assert.That(freshPress.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(freshPress.PowerModifier, Is.EqualTo(ButtonIntent.Pressed));
        }

        [Test]
        public void DisableClearsHeldStateAndRequiresFreshNeutralBeforeAnotherPress()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            PlayerCombatIntentAdapter adapter = CreateCombatAdapter();

            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                Vector2.right,
                Key.LeftCtrl,
                Key.LeftShift);
            Assert.That(adapter.ReadIntentFrame().Fire, Is.EqualTo(ButtonIntent.Pressed));

            adapter.enabled = false;
            AssertNeutralCombat(adapter.ReadIntentFrame());

            adapter.enabled = true;
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueKeyboardAndMouse(keyboard, mouse, Vector2.zero);
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                Vector2.up,
                Key.LeftCtrl,
                Key.LeftShift);
            PlayerIntentFrame freshPress = adapter.ReadIntentFrame();
            Assert.That(freshPress.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(freshPress.PowerModifier, Is.EqualTo(ButtonIntent.Pressed));
        }

        [Test]
        public void ContributingDeviceRemovalClearsHeldStateWithoutPublishingDeviceIdentity()
        {
            Gamepad gamepad = AddDevice<Gamepad>();
            PlayerCombatIntentAdapter adapter = CreateCombatAdapter();

            QueueGamepad(gamepad, Vector2.up, true, true);
            Assert.That(adapter.ReadIntentFrame().Fire.IsHeld, Is.True);

            InputSystem.RemoveDevice(gamepad);
            InputSystem.Update();
            AssertNeutralCombat(adapter.ReadIntentFrame());

            Gamepad replacement = AddDevice<Gamepad>();
            QueueGamepad(replacement, Vector2.zero, false, false);
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueGamepad(replacement, Vector2.right, true, true);
            PlayerIntentFrame freshPress = adapter.ReadIntentFrame();
            Assert.That(freshPress.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(freshPress.PowerModifier, Is.EqualTo(ButtonIntent.Pressed));

            Assert.That(
                PublicSurfaceExposesDeviceIdentifier(typeof(PlayerCombatIntentAdapter)),
                Is.False);
            Assert.That(
                typeof(PlayerCombatIntentAdapter)
                    .GetMethod(nameof(PlayerCombatIntentAdapter.ReadIntentFrame))
                    .ReturnType,
                Is.EqualTo(typeof(PlayerIntentFrame)));
        }

        [Test]
        public void DeviceSwitchWhileHeldClearsStateUntilAllCombatControlsReturnNeutral()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            Gamepad gamepad = AddDevice<Gamepad>();
            PlayerCombatIntentAdapter adapter = CreateCombatAdapter();

            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                Vector2.up,
                Key.LeftCtrl,
                Key.LeftShift);
            Assert.That(adapter.ReadIntentFrame().Fire.IsHeld, Is.True);

            QueueGamepad(gamepad, Vector2.right, true, true);
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueKeyboardAndMouse(keyboard, mouse, Vector2.zero);
            QueueGamepad(gamepad, Vector2.zero, false, false);
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueGamepad(gamepad, Vector2.left, true, true);
            PlayerIntentFrame gamepadFreshPress = adapter.ReadIntentFrame();
            AssertVector(gamepadFreshPress.Aim, -1f, 0f);
            Assert.That(gamepadFreshPress.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(
                gamepadFreshPress.PowerModifier,
                Is.EqualTo(ButtonIntent.Pressed));
        }

        [Test]
        public void MovementAndCombatMapsComposeWithoutSessionOwnershipOrConflictingState()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            PlayerMovementIntentAdapter movementAdapter = CreateMovementAdapter();
            PlayerCombatIntentAdapter combatAdapter = CreateCombatAdapter();

            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                new Vector2(-0.6f, 0.8f),
                Key.W,
                Key.D,
                Key.Space,
                Key.LeftCtrl,
                Key.LeftShift);

            PlayerIntentFrame movement = movementAdapter.ReadIntentFrame();
            PlayerIntentFrame combat = combatAdapter.ReadIntentFrame();

            AssertVector(movement.Move, Mathf.Sqrt(0.5f), Mathf.Sqrt(0.5f));
            AssertVector(movement.Aim, -0.6f, 0.8f);
            Assert.That(movement.Thruster, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(movement.Fire, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(movement.PowerModifier, Is.EqualTo(ButtonIntent.Inactive));

            AssertVector(combat.Aim, -0.6f, 0.8f);
            Assert.That(combat.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(combat.PowerModifier, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(combat.Move, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(combat.Thruster, Is.EqualTo(ButtonIntent.Inactive));

            combatAdapter.enabled = false;
            QueueKeyboardAndMouse(keyboard, mouse, Vector2.up, Key.W);
            Assert.That(movementAdapter.ReadIntentFrame().Move.Y, Is.GreaterThan(0f));
            AssertNeutralCombat(combatAdapter.ReadIntentFrame());

            Assert.That(DeclaresInputSessionAuthority(typeof(PlayerCombatIntentAdapter)), Is.False);
            Assert.That(movementAdapter.IsConfigured, Is.True);
            Assert.That(combatAdapter.IsConfigured, Is.True);
        }

        [Test]
        public void ReconfigureAndRepeatedEnableProduceOnlyOneConsumablePressAndReleaseEdge()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            AddDevice<Mouse>();
            InputActionAsset runtimeAsset = CreateRuntimeAsset(CombatActionAssetPath);

            GameObject gameObject = new GameObject("Player Combat Intent Duplicate Test");
            createdObjects.Add(gameObject);
            PlayerCombatIntentAdapter adapter =
                gameObject.AddComponent<PlayerCombatIntentAdapter>();

            adapter.Configure(runtimeAsset);
            adapter.Configure(runtimeAsset);
            adapter.enabled = false;
            adapter.enabled = true;

            QueueKeyboard(keyboard);
            AssertNeutralCombat(adapter.ReadIntentFrame());

            QueueKeyboard(keyboard, Key.LeftCtrl);
            Assert.That(adapter.ReadIntentFrame().Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(adapter.ReadIntentFrame().Fire, Is.EqualTo(ButtonIntent.Held));

            QueueKeyboard(keyboard);
            Assert.That(adapter.ReadIntentFrame().Fire, Is.EqualTo(ButtonIntent.Released));
            Assert.That(adapter.ReadIntentFrame().Fire, Is.EqualTo(ButtonIntent.Inactive));
        }

        [Test]
        public void Eh002KeyboardAndGamepadFixtureProjectsToTheSameCombatIntentTrace()
        {
            PlayerIntentFrame[] fixtureKeyboard = ResolveEvidenceFixture("ResolveKeyboardMouse");
            PlayerIntentFrame[] fixtureGamepad = ResolveEvidenceFixture("ResolveGamepad");
            PlayerIntentFrame[] actualKeyboard = DriveKeyboardFixtureTrace();
            PlayerIntentFrame[] actualGamepad = DriveGamepadFixtureTrace();

            Assert.That(fixtureKeyboard.Length, Is.EqualTo(fixtureGamepad.Length));
            Assert.That(actualKeyboard.Length, Is.EqualTo(fixtureKeyboard.Length));
            Assert.That(actualGamepad.Length, Is.EqualTo(fixtureGamepad.Length));

            for (int index = 0; index < fixtureKeyboard.Length; index++)
            {
                AssertCombatEquivalent(fixtureKeyboard[index], fixtureGamepad[index]);
                AssertCombatEquivalent(fixtureKeyboard[index], actualKeyboard[index]);
                AssertCombatEquivalent(fixtureGamepad[index], actualGamepad[index]);
                AssertCombatEquivalent(actualKeyboard[index], actualGamepad[index]);

                TestContext.WriteLine(
                    "TRACE index=" + index
                    + " keyboard/mouse=" + FormatCombatTrace(actualKeyboard[index])
                    + " gamepad=" + FormatCombatTrace(actualGamepad[index]));
            }
        }

        private PlayerIntentFrame[] DriveKeyboardFixtureTrace()
        {
            Keyboard keyboard = AddDevice<Keyboard>();
            Mouse mouse = AddDevice<Mouse>();
            PlayerCombatIntentAdapter adapter = CreateCombatAdapter();
            PlayerIntentFrame[] frames = new PlayerIntentFrame[5];

            frames[0] = adapter.ReadIntentFrame();
            QueueKeyboardAndMouse(keyboard, mouse, Vector2.up, Key.LeftCtrl);
            frames[1] = adapter.ReadIntentFrame();
            QueueKeyboardAndMouse(
                keyboard,
                mouse,
                new Vector2(-1f, 1f),
                Key.LeftCtrl,
                Key.LeftShift);
            frames[2] = adapter.ReadIntentFrame();
            QueueKeyboardAndMouse(keyboard, mouse, Vector2.left, Key.LeftShift);
            frames[3] = adapter.ReadIntentFrame();
            QueueKeyboardAndMouse(keyboard, mouse, Vector2.zero);
            frames[4] = adapter.ReadIntentFrame();

            DestroyAdapter(adapter);
            QueueKeyboardAndMouse(keyboard, mouse, Vector2.zero);
            return frames;
        }

        private PlayerIntentFrame[] DriveGamepadFixtureTrace()
        {
            Gamepad gamepad = AddDevice<Gamepad>();
            PlayerCombatIntentAdapter adapter = CreateCombatAdapter();
            PlayerIntentFrame[] frames = new PlayerIntentFrame[5];

            frames[0] = adapter.ReadIntentFrame();
            QueueGamepad(gamepad, Vector2.up, true, false);
            frames[1] = adapter.ReadIntentFrame();
            QueueGamepad(gamepad, new Vector2(-1f, 1f), true, true);
            frames[2] = adapter.ReadIntentFrame();
            QueueGamepad(gamepad, Vector2.left, false, true);
            frames[3] = adapter.ReadIntentFrame();
            QueueGamepad(gamepad, Vector2.zero, false, false);
            frames[4] = adapter.ReadIntentFrame();

            return frames;
        }

        private PlayerCombatIntentAdapter CreateCombatAdapter()
        {
            InputActionAsset runtimeAsset = CreateRuntimeAsset(CombatActionAssetPath);
            GameObject gameObject = new GameObject("Player Combat Intent Adapter Test");
            createdObjects.Add(gameObject);
            PlayerCombatIntentAdapter adapter =
                gameObject.AddComponent<PlayerCombatIntentAdapter>();
            adapter.Configure(runtimeAsset);
            return adapter;
        }

        private PlayerMovementIntentAdapter CreateMovementAdapter()
        {
            InputActionAsset runtimeAsset = CreateRuntimeAsset(MovementActionAssetPath);
            GameObject gameObject =
                new GameObject("Player Movement Intent Adapter Composition Test");
            createdObjects.Add(gameObject);
            PlayerMovementIntentAdapter adapter =
                gameObject.AddComponent<PlayerMovementIntentAdapter>();
            adapter.Configure(runtimeAsset);
            return adapter;
        }

        private InputActionAsset CreateRuntimeAsset(string path)
        {
            InputActionAsset imported = LoadImportedAsset(path);
            InputActionAsset runtimeAsset = InputActionAsset.FromJson(imported.ToJson());
            createdObjects.Add(runtimeAsset);
            return runtimeAsset;
        }

        private void DestroyAdapter(PlayerCombatIntentAdapter adapter)
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

        private static InputActionAsset LoadImportedAsset(string path)
        {
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            Assert.That(asset, Is.Not.Null, "Input action asset did not import: " + path);
            return asset;
        }

        private static void QueueKeyboard(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
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
            Vector2 aim,
            bool fire,
            bool power)
        {
            GamepadState state = new GamepadState
            {
                rightStick = aim,
                rightTrigger = fire ? 1f : 0f,
                leftTrigger = power ? 1f : 0f,
            };

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

        private static void AssertCombatEquivalent(
            PlayerIntentFrame expected,
            PlayerIntentFrame actual)
        {
            AssertVector(actual.Aim, expected.Aim.X, expected.Aim.Y);
            Assert.That(actual.Fire, Is.EqualTo(expected.Fire));
            Assert.That(actual.PowerModifier, Is.EqualTo(expected.PowerModifier));
            Assert.That(actual.WasFocusLost, Is.EqualTo(expected.WasFocusLost));
        }

        private static void AssertCombatOnly(PlayerIntentFrame frame)
        {
            Assert.That(frame.Move, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(frame.Thruster, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.Interact, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.Map, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.PauseMenu, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.UiNavigation, Is.EqualTo(NormalizedIntentVector2.Zero));
        }

        private static void AssertVector(
            NormalizedIntentVector2 actual,
            float expectedX,
            float expectedY)
        {
            Assert.That(actual.X, Is.EqualTo(expectedX).Within(0.0001f));
            Assert.That(actual.Y, Is.EqualTo(expectedY).Within(0.0001f));
        }

        private static void AssertNeutralCombat(PlayerIntentFrame frame)
        {
            Assert.That(frame.Aim, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(frame.Fire, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.PowerModifier, Is.EqualTo(ButtonIntent.Inactive));
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

        private static bool DeclaresInputSessionAuthority(Type adapterType)
        {
            return adapterType.GetFields(
                    BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.DeclaredOnly)
                .Any(field =>
                    typeof(PlayerInput).IsAssignableFrom(field.FieldType)
                    || string.Equals(
                        field.FieldType.FullName,
                        "UnityEngine.InputSystem.Users.InputUser",
                        StringComparison.Ordinal));
        }

        private static string FormatCombatTrace(PlayerIntentFrame frame)
        {
            return "aim=(" + frame.Aim.X.ToString("0.###")
                + "," + frame.Aim.Y.ToString("0.###")
                + ") fire=" + frame.Fire.IsHeld
                + "/" + frame.Fire.WasPressed
                + "/" + frame.Fire.WasReleased
                + " power=" + frame.PowerModifier.IsHeld
                + "/" + frame.PowerModifier.WasPressed
                + "/" + frame.PowerModifier.WasReleased
                + " focus=" + frame.WasFocusLost;
        }
    }
}
#endif

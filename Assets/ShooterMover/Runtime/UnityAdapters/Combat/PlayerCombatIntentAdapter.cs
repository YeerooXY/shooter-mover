using System;
using ShooterMover.Contracts.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UnityAdapters.Combat
{
    /// <summary>
    /// Unity Input System boundary for shared combat aim, fire, and power intent.
    /// Physical controls and lifecycle details remain inside this adapter; combat
    /// consumers receive only immutable CS-003 PlayerIntentFrame values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatIntentAdapter : MonoBehaviour
    {
        private const string CombatMapName = "Combat";
        private const string AimActionName = "Aim";
        private const string FireActionName = "Fire";
        private const string PowerActionName = "Power";
        private const float NeutralMagnitudeSquared = 0.000001f;
        private const float ButtonPressPoint = 0.5f;

        [SerializeField]
        private InputActionAsset inputActions;

        private InputActionMap combatMap;
        private InputAction aimAction;
        private InputAction fireAction;
        private InputAction powerAction;
        private InputDevice lastContributingDevice;
        private InputSourceKind activeInputSource;

        private bool isComponentActive;
        private bool isDeviceCallbackSubscribed;
        private bool acceptActionCallbacks;
        private bool hasInputFocus = true;
        private bool suppressUntilNeutral;
        private bool fireHeld;
        private bool firePressed;
        private bool fireReleased;
        private bool powerHeld;
        private bool powerPressed;
        private bool powerReleased;
        private bool hasPendingBoundaryFrame;
        private PlayerIntentFrame pendingBoundaryFrame;
        private PlayerIntentFrame lastPublishedFrame = PlayerIntentFrame.Neutral;

        private enum InputSourceKind
        {
            None = 0,
            KeyboardMouse = 1,
            Gamepad = 2,
        }

        public bool IsConfigured
        {
            get { return combatMap != null; }
        }

        public bool IsAcceptingInput
        {
            get
            {
                return isComponentActive
                    && hasInputFocus
                    && combatMap != null
                    && combatMap.enabled
                    && !suppressUntilNeutral;
            }
        }

        /// <summary>
        /// Assigns the imported combat action asset. This adapter enables only its
        /// own Combat map and does not pair devices, select a control scheme, or
        /// otherwise become an input-session authority.
        /// </summary>
        public void Configure(InputActionAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            DeactivateActions();
            UnbindActionCallbacks();

            inputActions = asset;
            combatMap = asset.FindActionMap(CombatMapName, true);
            aimAction = combatMap.FindAction(AimActionName, true);
            fireAction = combatMap.FindAction(FireActionName, true);
            powerAction = combatMap.FindAction(PowerActionName, true);

            BindActionCallbacks();
            ResetPublishedState();

            if (isComponentActive && hasInputFocus)
            {
                ActivateActions();
            }
        }

        /// <summary>
        /// Samples one immutable device-independent combat intent frame.
        /// Transient button edges are consumed by this call while held state remains.
        /// </summary>
        public PlayerIntentFrame ReadIntentFrame()
        {
            if (hasPendingBoundaryFrame)
            {
                hasPendingBoundaryFrame = false;
                lastPublishedFrame = pendingBoundaryFrame;
                return pendingBoundaryFrame;
            }

            if (!isComponentActive
                || !hasInputFocus
                || combatMap == null
                || !combatMap.enabled)
            {
                return PublishNeutral();
            }

            Vector2 rawAim = aimAction.ReadValue<Vector2>();

            if (suppressUntilNeutral)
            {
                if (!IsRawInputNeutral(rawAim))
                {
                    ClearTransientEdges();
                    return PublishNeutral();
                }

                suppressUntilNeutral = false;
                ResetButtonState();
                return PublishNeutral();
            }

            PlayerIntentFrame frame = new PlayerIntentFrame(
                NormalizedIntentVector2.Zero,
                NormalizedIntentVector2.Create(rawAim.x, rawAim.y),
                new ButtonIntent(fireHeld, firePressed, fireReleased),
                new ButtonIntent(powerHeld, powerPressed, powerReleased),
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);

            ClearTransientEdges();
            lastPublishedFrame = frame;
            return frame;
        }

        private void Awake()
        {
            if (inputActions != null && combatMap == null)
            {
                Configure(inputActions);
            }
        }

        private void OnEnable()
        {
            isComponentActive = true;
            SubscribeDeviceChanges();

            if (combatMap != null && hasInputFocus)
            {
                ActivateActions();
            }
        }

        private void OnDisable()
        {
            isComponentActive = false;
            UnsubscribeDeviceChanges();
            DeactivateActions();
            ResetPublishedState();
            suppressUntilNeutral = true;
        }

        private void OnDestroy()
        {
            UnsubscribeDeviceChanges();
            DeactivateActions();
            UnbindActionCallbacks();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus == hasInputFocus)
            {
                return;
            }

            if (!hasFocus)
            {
                HandleFocusLoss();
                return;
            }

            HandleFocusRegained();
        }

        private void HandleFocusLoss()
        {
            pendingBoundaryFrame = PlayerIntentFrame.FromFocusLoss(lastPublishedFrame);
            hasPendingBoundaryFrame = true;
            hasInputFocus = false;
            suppressUntilNeutral = true;

            DeactivateActions();
            ResetButtonState();
            lastContributingDevice = null;
            activeInputSource = InputSourceKind.None;
        }

        private void HandleFocusRegained()
        {
            hasInputFocus = true;
            suppressUntilNeutral = true;
            ResetButtonState();
            lastContributingDevice = null;
            activeInputSource = InputSourceKind.None;

            if (isComponentActive && combatMap != null)
            {
                ActivateActions();
            }
        }

        private void ActivateActions()
        {
            if (combatMap == null || combatMap.enabled)
            {
                return;
            }

            acceptActionCallbacks = true;
            combatMap.Enable();
        }

        private void DeactivateActions()
        {
            acceptActionCallbacks = false;
            if (combatMap != null && combatMap.enabled)
            {
                combatMap.Disable();
            }

            ResetButtonState();
            lastContributingDevice = null;
            activeInputSource = InputSourceKind.None;
        }

        private void BindActionCallbacks()
        {
            if (aimAction == null || fireAction == null || powerAction == null)
            {
                return;
            }

            aimAction.started += OnAimAction;
            aimAction.performed += OnAimAction;
            fireAction.started += OnFireStarted;
            fireAction.performed += OnFirePerformed;
            fireAction.canceled += OnFireCanceled;
            powerAction.started += OnPowerStarted;
            powerAction.performed += OnPowerPerformed;
            powerAction.canceled += OnPowerCanceled;
        }

        private void UnbindActionCallbacks()
        {
            if (aimAction != null)
            {
                aimAction.started -= OnAimAction;
                aimAction.performed -= OnAimAction;
            }

            if (fireAction != null)
            {
                fireAction.started -= OnFireStarted;
                fireAction.performed -= OnFirePerformed;
                fireAction.canceled -= OnFireCanceled;
            }

            if (powerAction != null)
            {
                powerAction.started -= OnPowerStarted;
                powerAction.performed -= OnPowerPerformed;
                powerAction.canceled -= OnPowerCanceled;
            }

            combatMap = null;
            aimAction = null;
            fireAction = null;
            powerAction = null;
        }

        private void OnAimAction(InputAction.CallbackContext context)
        {
            TryAcceptCallbackSource(context);
        }

        private void OnFireStarted(InputAction.CallbackContext context)
        {
            if (!TryAcceptCallbackSource(context))
            {
                return;
            }

            RegisterButtonPress(ref fireHeld, ref firePressed);
        }

        private void OnFirePerformed(InputAction.CallbackContext context)
        {
            if (!TryAcceptCallbackSource(context))
            {
                return;
            }

            RegisterButtonPress(ref fireHeld, ref firePressed);
        }

        private void OnFireCanceled(InputAction.CallbackContext context)
        {
            if (!TryAcceptCallbackSource(context))
            {
                return;
            }

            RegisterButtonRelease(ref fireHeld, ref firePressed, ref fireReleased);
        }

        private void OnPowerStarted(InputAction.CallbackContext context)
        {
            if (!TryAcceptCallbackSource(context))
            {
                return;
            }

            RegisterButtonPress(ref powerHeld, ref powerPressed);
        }

        private void OnPowerPerformed(InputAction.CallbackContext context)
        {
            if (!TryAcceptCallbackSource(context))
            {
                return;
            }

            RegisterButtonPress(ref powerHeld, ref powerPressed);
        }

        private void OnPowerCanceled(InputAction.CallbackContext context)
        {
            if (!TryAcceptCallbackSource(context))
            {
                return;
            }

            RegisterButtonRelease(ref powerHeld, ref powerPressed, ref powerReleased);
        }

        private bool TryAcceptCallbackSource(InputAction.CallbackContext context)
        {
            if (!acceptActionCallbacks || context.control == null)
            {
                return false;
            }

            InputDevice device = context.control.device;
            InputSourceKind source = ClassifyInputSource(device);
            if (source == InputSourceKind.None)
            {
                return false;
            }

            if (suppressUntilNeutral)
            {
                if (source == activeInputSource)
                {
                    lastContributingDevice = device;
                }

                return false;
            }

            if (activeInputSource != InputSourceKind.None
                && source != activeInputSource
                && HasActiveButtonState())
            {
                BeginDeviceSwitch(source, device);
                return false;
            }

            activeInputSource = source;
            lastContributingDevice = device;
            return true;
        }

        private void BeginDeviceSwitch(InputSourceKind source, InputDevice device)
        {
            hasPendingBoundaryFrame = false;
            suppressUntilNeutral = true;
            ResetButtonState();
            lastPublishedFrame = PlayerIntentFrame.Neutral;
            activeInputSource = source;
            lastContributingDevice = device;
        }

        private bool HasActiveButtonState()
        {
            return fireHeld
                || firePressed
                || fireReleased
                || powerHeld
                || powerPressed
                || powerReleased;
        }

        private static InputSourceKind ClassifyInputSource(InputDevice device)
        {
            if (device is Gamepad)
            {
                return InputSourceKind.Gamepad;
            }

            if (device is Keyboard || device is Mouse)
            {
                return InputSourceKind.KeyboardMouse;
            }

            return InputSourceKind.None;
        }

        private static void RegisterButtonPress(ref bool held, ref bool pressed)
        {
            if (!held)
            {
                pressed = true;
            }

            held = true;
        }

        private static void RegisterButtonRelease(
            ref bool held,
            ref bool pressed,
            ref bool released)
        {
            if (held || pressed)
            {
                released = true;
            }

            held = false;
        }

        private void SubscribeDeviceChanges()
        {
            if (isDeviceCallbackSubscribed)
            {
                return;
            }

            InputSystem.onDeviceChange += OnDeviceChange;
            isDeviceCallbackSubscribed = true;
        }

        private void UnsubscribeDeviceChanges()
        {
            if (!isDeviceCallbackSubscribed)
            {
                return;
            }

            InputSystem.onDeviceChange -= OnDeviceChange;
            isDeviceCallbackSubscribed = false;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change != InputDeviceChange.Disconnected
                && change != InputDeviceChange.Removed
                && change != InputDeviceChange.Disabled)
            {
                return;
            }

            if (!ReferenceEquals(device, lastContributingDevice)
                && !ActionUsesDevice(aimAction, device)
                && !ActionUsesDevice(fireAction, device)
                && !ActionUsesDevice(powerAction, device))
            {
                return;
            }

            ClearForLifecycleBoundary();
        }

        private void ClearForLifecycleBoundary()
        {
            hasPendingBoundaryFrame = false;
            suppressUntilNeutral = true;
            ResetButtonState();
            lastPublishedFrame = PlayerIntentFrame.Neutral;
            lastContributingDevice = null;
            activeInputSource = InputSourceKind.None;
        }

        private static bool ActionUsesDevice(InputAction action, InputDevice device)
        {
            return action != null
                && action.activeControl != null
                && ReferenceEquals(action.activeControl.device, device);
        }

        private bool IsRawInputNeutral(Vector2 rawAim)
        {
            return rawAim.sqrMagnitude <= NeutralMagnitudeSquared
                && fireAction.ReadValue<float>() <= ButtonPressPoint
                && powerAction.ReadValue<float>() <= ButtonPressPoint;
        }

        private PlayerIntentFrame PublishNeutral()
        {
            ClearTransientEdges();
            lastPublishedFrame = PlayerIntentFrame.Neutral;
            return lastPublishedFrame;
        }

        private void ResetPublishedState()
        {
            hasPendingBoundaryFrame = false;
            pendingBoundaryFrame = PlayerIntentFrame.Neutral;
            suppressUntilNeutral = false;
            ResetButtonState();
            lastPublishedFrame = PlayerIntentFrame.Neutral;
            lastContributingDevice = null;
            activeInputSource = InputSourceKind.None;
        }

        private void ResetButtonState()
        {
            fireHeld = false;
            firePressed = false;
            fireReleased = false;
            powerHeld = false;
            powerPressed = false;
            powerReleased = false;
        }

        private void ClearTransientEdges()
        {
            firePressed = false;
            fireReleased = false;
            powerPressed = false;
            powerReleased = false;
        }
    }
}

using System;
using ShooterMover.Contracts.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UnityAdapters.Input
{
    /// <summary>
    /// Unity Input System boundary for movement, aim, and thruster intent.
    /// Physical controls and devices remain inside this adapter; consumers receive
    /// only immutable CS-003 PlayerIntentFrame values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMovementIntentAdapter : MonoBehaviour
    {
        private const string MovementMapName = "Movement";
        private const string MoveActionName = "Move";
        private const string AimActionName = "Aim";
        private const string ThrusterActionName = "Thruster";
        private const float NeutralMagnitudeSquared = 0.000001f;

        [SerializeField]
        private InputActionAsset inputActions;

        private InputActionMap movementMap;
        private InputAction moveAction;
        private InputAction aimAction;
        private InputAction thrusterAction;
        private InputDevice lastContributingDevice;

        private bool isComponentActive;
        private bool isDeviceCallbackSubscribed;
        private bool acceptActionCallbacks;
        private bool hasInputFocus = true;
        private bool suppressUntilNeutral;
        private bool thrusterHeld;
        private bool thrusterPressed;
        private bool thrusterReleased;
        private bool hasPendingBoundaryFrame;
        private PlayerIntentFrame pendingBoundaryFrame;
        private PlayerIntentFrame lastPublishedFrame = PlayerIntentFrame.Neutral;

        public bool IsConfigured
        {
            get { return movementMap != null; }
        }

        public bool IsAcceptingInput
        {
            get
            {
                return isComponentActive
                    && hasInputFocus
                    && movementMap != null
                    && movementMap.enabled
                    && !suppressUntilNeutral;
            }
        }

        /// <summary>
        /// Assigns the imported movement action asset. This is an adapter-only
        /// configuration boundary; the asset and its binding/device details are
        /// never exposed through the published gameplay intent.
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
            movementMap = asset.FindActionMap(MovementMapName, true);
            moveAction = movementMap.FindAction(MoveActionName, true);
            aimAction = movementMap.FindAction(AimActionName, true);
            thrusterAction = movementMap.FindAction(ThrusterActionName, true);

            BindActionCallbacks();
            ResetPublishedState();

            if (isComponentActive && hasInputFocus)
            {
                ActivateActions();
            }
        }

        /// <summary>
        /// Samples one immutable device-independent movement intent frame.
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
                || movementMap == null
                || !movementMap.enabled)
            {
                return PublishNeutral();
            }

            Vector2 rawMove = moveAction.ReadValue<Vector2>();
            Vector2 rawAim = aimAction.ReadValue<Vector2>();

            if (suppressUntilNeutral)
            {
                if (!IsRawInputNeutral(rawMove, rawAim))
                {
                    ClearTransientEdges();
                    return PublishNeutral();
                }

                suppressUntilNeutral = false;
                ResetButtonState();
                return PublishNeutral();
            }

            PlayerIntentFrame frame = new PlayerIntentFrame(
                NormalizedIntentVector2.Create(rawMove.x, rawMove.y),
                NormalizedIntentVector2.Create(rawAim.x, rawAim.y),
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                new ButtonIntent(thrusterHeld, thrusterPressed, thrusterReleased),
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);

            ClearTransientEdges();
            lastPublishedFrame = frame;
            return frame;
        }

        private void OnEnable()
        {
            isComponentActive = true;
            SubscribeDeviceChanges();

            if (movementMap != null && hasInputFocus)
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
        }

        private void HandleFocusRegained()
        {
            hasInputFocus = true;
            suppressUntilNeutral = true;
            ResetButtonState();
            lastContributingDevice = null;

            if (isComponentActive && movementMap != null)
            {
                ActivateActions();
            }
        }

        private void ActivateActions()
        {
            if (movementMap == null || movementMap.enabled)
            {
                return;
            }

            acceptActionCallbacks = true;
            movementMap.Enable();
        }

        private void DeactivateActions()
        {
            acceptActionCallbacks = false;
            if (movementMap != null && movementMap.enabled)
            {
                movementMap.Disable();
            }

            ResetButtonState();
            lastContributingDevice = null;
        }

        private void BindActionCallbacks()
        {
            if (moveAction == null || aimAction == null || thrusterAction == null)
            {
                return;
            }

            moveAction.started += OnVectorAction;
            moveAction.performed += OnVectorAction;
            aimAction.started += OnVectorAction;
            aimAction.performed += OnVectorAction;
            thrusterAction.started += OnThrusterStarted;
            thrusterAction.performed += OnThrusterPerformed;
            thrusterAction.canceled += OnThrusterCanceled;
        }

        private void UnbindActionCallbacks()
        {
            if (moveAction != null)
            {
                moveAction.started -= OnVectorAction;
                moveAction.performed -= OnVectorAction;
            }

            if (aimAction != null)
            {
                aimAction.started -= OnVectorAction;
                aimAction.performed -= OnVectorAction;
            }

            if (thrusterAction != null)
            {
                thrusterAction.started -= OnThrusterStarted;
                thrusterAction.performed -= OnThrusterPerformed;
                thrusterAction.canceled -= OnThrusterCanceled;
            }

            movementMap = null;
            moveAction = null;
            aimAction = null;
            thrusterAction = null;
        }

        private void OnVectorAction(InputAction.CallbackContext context)
        {
            if (!acceptActionCallbacks)
            {
                return;
            }

            RecordContributingDevice(context);
        }

        private void OnThrusterStarted(InputAction.CallbackContext context)
        {
            if (!acceptActionCallbacks)
            {
                return;
            }

            RecordContributingDevice(context);
            RegisterThrusterPress();
        }

        private void OnThrusterPerformed(InputAction.CallbackContext context)
        {
            if (!acceptActionCallbacks)
            {
                return;
            }

            RecordContributingDevice(context);
            RegisterThrusterPress();
        }

        private void OnThrusterCanceled(InputAction.CallbackContext context)
        {
            if (!acceptActionCallbacks)
            {
                return;
            }

            RecordContributingDevice(context);
            if (thrusterHeld || thrusterPressed)
            {
                thrusterReleased = true;
            }

            thrusterHeld = false;
        }

        private void RegisterThrusterPress()
        {
            if (!thrusterHeld)
            {
                thrusterPressed = true;
            }

            thrusterHeld = true;
        }

        private void RecordContributingDevice(InputAction.CallbackContext context)
        {
            if (context.control != null)
            {
                lastContributingDevice = context.control.device;
            }
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
                && !ActionUsesDevice(moveAction, device)
                && !ActionUsesDevice(aimAction, device)
                && !ActionUsesDevice(thrusterAction, device))
            {
                return;
            }

            hasPendingBoundaryFrame = false;
            suppressUntilNeutral = true;
            ResetButtonState();
            lastPublishedFrame = PlayerIntentFrame.Neutral;
            lastContributingDevice = null;
        }

        private static bool ActionUsesDevice(InputAction action, InputDevice device)
        {
            return action != null
                && action.activeControl != null
                && ReferenceEquals(action.activeControl.device, device);
        }

        private bool IsRawInputNeutral(Vector2 rawMove, Vector2 rawAim)
        {
            return rawMove.sqrMagnitude <= NeutralMagnitudeSquared
                && rawAim.sqrMagnitude <= NeutralMagnitudeSquared
                && thrusterAction.ReadValue<float>() <= 0.5f;
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
        }

        private void ResetButtonState()
        {
            thrusterHeld = false;
            thrusterPressed = false;
            thrusterReleased = false;
        }

        private void ClearTransientEdges()
        {
            thrusterPressed = false;
            thrusterReleased = false;
        }
    }
}

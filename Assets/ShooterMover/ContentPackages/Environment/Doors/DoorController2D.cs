using System;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;
using UnityEngine.Events;

namespace ShooterMover.ContentPackages.Environment.Doors
{
    public enum DoorInitialState
    {
        Closed = 1,
        Open = 2,
    }

    public enum DoorRuntimeState
    {
        Closed = 1,
        Open = 2,
    }

    public enum DoorAuthoringValidationCode
    {
        Valid = 0,
        MissingPlacedObjectAuthoring = 1,
        PlacedObjectBindingFailed = 2,
        MissingConditionConfiguration = 3,
        InvalidConditionConfiguration = 4,
        MissingClosedCollider = 5,
        MissingPresentationRoot = 6,
        SharedPresentationRoot = 7,
        MissingConditionPort = 8,
        MissingTransitionSocket = 9,
        InvalidTransitionSocket = 10,
        RestartRegistrationFailed = 11,
        RebindFailed = 12,
    }

    public sealed class DoorAuthoringValidationResult
    {
        private DoorAuthoringValidationResult(
            DoorAuthoringValidationCode code,
            string diagnostic)
        {
            Code = code;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public DoorAuthoringValidationCode Code { get; }

        public string Diagnostic { get; }

        public bool IsValid
        {
            get { return Code == DoorAuthoringValidationCode.Valid; }
        }

        public static DoorAuthoringValidationResult Valid()
        {
            return new DoorAuthoringValidationResult(
                DoorAuthoringValidationCode.Valid,
                "Door authoring is valid.");
        }

        public static DoorAuthoringValidationResult Failed(
            DoorAuthoringValidationCode code,
            string diagnostic)
        {
            if (code == DoorAuthoringValidationCode.Valid)
            {
                throw new ArgumentException(
                    "A failed validation result cannot use the Valid code.",
                    nameof(code));
            }

            return new DoorAuthoringValidationResult(code, diagnostic);
        }
    }

    public sealed class DoorStateChange
    {
        public DoorStateChange(
            StableId doorPlacedInstanceId,
            DoorRuntimeState previousState,
            DoorRuntimeState currentState,
            bool restartProjection)
        {
            DoorPlacedInstanceId = doorPlacedInstanceId
                ?? throw new ArgumentNullException(nameof(doorPlacedInstanceId));
            PreviousState = previousState;
            CurrentState = currentState;
            IsRestartProjection = restartProjection;
        }

        public StableId DoorPlacedInstanceId { get; }

        public DoorRuntimeState PreviousState { get; }

        public DoorRuntimeState CurrentState { get; }

        public bool IsRestartProjection { get; }
    }

    /// <summary>
    /// Scene-independent reusable door projection. Placed identity, scope binding,
    /// and restart sequencing come from OBJ-001. Condition readers and transition
    /// authorization are typed read/command ports; this component owns no wallet,
    /// key inventory, encounter, room loading, or durable mission truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorController2D : MonoBehaviour, IRestartParticipant
    {
        [Header("Placed object")]
        [SerializeField] private PlacedObjectAuthoring2D placedObject;
        [SerializeField] private bool initializeOnEnable = true;

        [Header("Door conditions")]
        [SerializeField] private DoorConditionComposition conditionComposition =
            DoorConditionComposition.All;
        [SerializeField] private DoorConditionAuthoring[] conditions =
            Array.Empty<DoorConditionAuthoring>();
        [SerializeField] private MonoBehaviour encounterConditionReader;
        [SerializeField] private MonoBehaviour targetConditionReader;
        [SerializeField] private MonoBehaviour walletReadPort;
        [SerializeField] private MonoBehaviour keyReadPort;

        [Header("Collision and presentation")]
        [SerializeField] private DoorInitialState initialState =
            DoorInitialState.Closed;
        [SerializeField] private Collider2D[] closedColliders =
            Array.Empty<Collider2D>();
        [SerializeField] private GameObject closedPresentationRoot;
        [SerializeField] private GameObject openPresentationRoot;
        [SerializeField] private UnityEvent opened = new UnityEvent();
        [SerializeField] private UnityEvent closed = new UnityEvent();

        [Header("Traversal")]
        [SerializeField] private DoorOneWayPolicy oneWayPolicy =
            DoorOneWayPolicy.Bidirectional;
        [SerializeField] private bool transitionEnabled;
        [SerializeField] private DoorSocketAuthoring forwardSourceSocket;
        [SerializeField] private DoorSocketAuthoring forwardDestinationSocket;
        [SerializeField] private MonoBehaviour transitionAuthorizationPort;

        private DoorConditionSet conditionSet;
        private DoorConditionSet configuredConditionSetForTests;
        private DoorTransitionDefinition transitionDefinition;
        private IDoorEncounterConditionReader injectedEncounterReader;
        private IDoorTargetConditionReader injectedTargetReader;
        private IDoorWalletReadPort injectedWalletReader;
        private IDoorKeyReadPort injectedKeyReader;
        private IDoorTransitionAuthorizationPort injectedTransitionAuthorization;
        private GameplaySceneScope2D restartScope;
        private StableId doorPlacedInstanceId;
        private bool triggerEntered;
        private bool interactionRequested;
        private bool initialized;
        private bool restartRegistered;
        private DoorRuntimeState state;
        private DoorAuthoringValidationResult lastValidation =
            DoorAuthoringValidationResult.Failed(
                DoorAuthoringValidationCode.MissingPlacedObjectAuthoring,
                "Door has not been initialized.");
        private DoorConditionEvaluationResult lastConditionEvaluation;

        public event Action<DoorStateChange> StateChanged;

        public bool IsInitialized
        {
            get { return initialized; }
        }

        public bool IsOpen
        {
            get { return initialized && state == DoorRuntimeState.Open; }
        }

        public DoorRuntimeState State
        {
            get { return state; }
        }

        public StableId DoorPlacedInstanceId
        {
            get { return doorPlacedInstanceId; }
        }

        public StableId RestartParticipantId
        {
            get
            {
                if (doorPlacedInstanceId == null)
                {
                    throw new InvalidOperationException(
                        "Door must bind its placed identity before restart registration.");
                }

                return doorPlacedInstanceId;
            }
        }

        public DoorAuthoringValidationResult LastValidation
        {
            get { return lastValidation; }
        }

        public DoorConditionEvaluationResult LastConditionEvaluation
        {
            get { return lastConditionEvaluation; }
        }

        public DoorTransitionDefinition TransitionDefinition
        {
            get { return transitionDefinition; }
        }

        public int StateChangeCount { get; private set; }

        public int RestartCount { get; private set; }

        private void OnEnable()
        {
            if (initializeOnEnable)
            {
                TryInitialize();
            }
        }

        private void OnDisable()
        {
            ReleaseRegistrations();
        }

        private void OnDestroy()
        {
            ReleaseRegistrations();
            StateChanged = null;
        }

        public DoorAuthoringValidationResult TryInitialize()
        {
            if (initialized)
            {
                return lastValidation;
            }

            DoorAuthoringValidationResult localValidation = ValidateAndBuild();
            if (!localValidation.IsValid)
            {
                lastValidation = localValidation;
                return lastValidation;
            }

            SceneScopeBindingResult binding = placedObject.TryBind();
            if (!binding.IsBound)
            {
                lastValidation = DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.PlacedObjectBindingFailed,
                    binding.Diagnostic);
                return lastValidation;
            }

            doorPlacedInstanceId = placedObject.ResolvedIdentity.Value;
            restartScope = placedObject.BoundScope;
            RestartParticipantRegistrationResult restartResult =
                placedObject.RegisterRestartParticipant(
                    this,
                    this,
                    "door:" + doorPlacedInstanceId);
            if (!restartResult.IsAccepted)
            {
                placedObject.Unbind();
                restartScope = null;
                doorPlacedInstanceId = null;
                lastValidation = DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.RestartRegistrationFailed,
                    restartResult.Diagnostic);
                return lastValidation;
            }

            restartRegistered = true;
            DoorConditionEvaluationResult initialEvaluation =
                EvaluateConditionsInternal();
            if (!initialEvaluation.IsConfigurationValid)
            {
                ReleaseRegistrations();
                lastValidation = DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.MissingConditionPort,
                    BuildConditionFailureDiagnostic(initialEvaluation));
                return lastValidation;
            }

            initialized = true;
            ApplyState(
                initialState == DoorInitialState.Open
                    ? DoorRuntimeState.Open
                    : DoorRuntimeState.Closed,
                true,
                false);

            if (initialEvaluation.IsSatisfied)
            {
                ApplyState(DoorRuntimeState.Open, false, false);
            }

            lastValidation = DoorAuthoringValidationResult.Valid();
            return lastValidation;
        }

        public DoorConditionEvaluationResult EvaluateConditions()
        {
            lastConditionEvaluation = EvaluateConditionsInternal();
            return lastConditionEvaluation;
        }

        public bool ReevaluateAuthoritativeConditions()
        {
            if (!initialized)
            {
                return false;
            }

            DoorConditionEvaluationResult evaluation = EvaluateConditions();
            if (!evaluation.IsConfigurationValid || !evaluation.IsSatisfied)
            {
                return false;
            }

            ApplyState(DoorRuntimeState.Open, false, false);
            return true;
        }

        public bool NotifyTriggerEntered()
        {
            triggerEntered = true;
            return TryOpen();
        }

        public void NotifyTriggerExited()
        {
            triggerEntered = false;
        }

        public bool NotifyInteractionRequested()
        {
            interactionRequested = true;
            return TryOpen();
        }

        public void ClearInteractionRequest()
        {
            interactionRequested = false;
        }

        public bool TryOpen()
        {
            if (!initialized)
            {
                return false;
            }

            DoorConditionEvaluationResult evaluation = EvaluateConditions();
            if (!evaluation.IsConfigurationValid || !evaluation.IsSatisfied)
            {
                return false;
            }

            ApplyState(DoorRuntimeState.Open, false, false);
            return true;
        }

        public void Close()
        {
            if (initialized)
            {
                ApplyState(DoorRuntimeState.Closed, false, false);
            }
        }

        public DoorTransitionRequestResult TryRequestTransition(
            DoorTravelDirection direction)
        {
            if (!initialized || state != DoorRuntimeState.Open)
            {
                return new DoorTransitionRequestResult(
                    DoorTransitionRequestStatus.DoorClosed,
                    null,
                    null,
                    "Door must be open before traversal can be authorized.");
            }

            if (!IsDirectionAllowed(oneWayPolicy, direction))
            {
                return new DoorTransitionRequestResult(
                    DoorTransitionRequestStatus.RejectedByOneWayPolicy,
                    null,
                    null,
                    "Door one-way policy rejects the requested travel direction.");
            }

            if (!transitionEnabled || transitionDefinition == null)
            {
                return new DoorTransitionRequestResult(
                    DoorTransitionRequestStatus.MissingTransitionConfiguration,
                    null,
                    null,
                    "Door has no typed transition configuration.");
            }

            if (!transitionDefinition.Validation.IsValid)
            {
                return new DoorTransitionRequestResult(
                    DoorTransitionRequestStatus.InvalidTransitionSockets,
                    null,
                    null,
                    transitionDefinition.Validation.Diagnostic);
            }

            IDoorTransitionAuthorizationPort port =
                ResolveTransitionAuthorizationPort();
            if (port == null)
            {
                return new DoorTransitionRequestResult(
                    DoorTransitionRequestStatus.MissingAuthorizationPort,
                    null,
                    null,
                    "Transition door requires IDoorTransitionAuthorizationPort.");
            }

            DoorTransitionRequest request = new DoorTransitionRequest(
                doorPlacedInstanceId,
                direction,
                transitionDefinition.GetSource(direction),
                transitionDefinition.GetDestination(direction));
            DoorTransitionAuthorization authorization = port.Authorize(request);
            if (authorization == null || !authorization.IsAuthorized)
            {
                return new DoorTransitionRequestResult(
                    DoorTransitionRequestStatus.AuthorizationDenied,
                    request,
                    authorization,
                    authorization == null
                        ? "Transition authorization port returned no result."
                        : authorization.Diagnostic);
            }

            return new DoorTransitionRequestResult(
                DoorTransitionRequestStatus.Authorized,
                request,
                authorization,
                authorization.Diagnostic);
        }

        public void OnRestartPhase(
            RestartContext context,
            RestartLifecyclePhase phase)
        {
            if (!initialized || context == null)
            {
                return;
            }

            switch (phase)
            {
                case RestartLifecyclePhase.RetireAttempt:
                    triggerEntered = false;
                    interactionRequested = false;
                    placedObject.Unbind();
                    break;
                case RestartLifecyclePhase.ReleaseTransientResources:
                    break;
                case RestartLifecyclePhase.ApplyResetProjection:
                    ApplyState(
                        initialState == DoorInitialState.Open
                            ? DoorRuntimeState.Open
                            : DoorRuntimeState.Closed,
                        true,
                        true);
                    break;
                case RestartLifecyclePhase.CompleteRebind:
                    SceneScopeBindingResult rebound = placedObject.TryBind();
                    if (!rebound.IsBound)
                    {
                        lastValidation = DoorAuthoringValidationResult.Failed(
                            DoorAuthoringValidationCode.RebindFailed,
                            rebound.Diagnostic);
                        initialized = false;
                        return;
                    }

                    RestartCount++;
                    ReevaluateAuthoritativeConditions();
                    break;
            }
        }

        public void ConfigureForTests(
            PlacedObjectAuthoring2D configuredPlacedObject,
            DoorInitialState configuredInitialState,
            DoorConditionComposition configuredComposition,
            DoorConditionRequirement[] configuredRequirements,
            Collider2D[] configuredClosedColliders,
            GameObject configuredClosedPresentationRoot,
            GameObject configuredOpenPresentationRoot,
            DoorOneWayPolicy configuredOneWayPolicy,
            RoomSocket configuredForwardSource,
            RoomSocket configuredForwardDestination,
            bool configuredTransitionEnabled)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Cannot reconfigure an initialized door.");
            }

            placedObject = configuredPlacedObject;
            initialState = configuredInitialState;
            conditionComposition = configuredComposition;
            configuredConditionSetForTests = new DoorConditionSet(
                configuredComposition,
                configuredRequirements);
            closedColliders = configuredClosedColliders
                ?? Array.Empty<Collider2D>();
            closedPresentationRoot = configuredClosedPresentationRoot;
            openPresentationRoot = configuredOpenPresentationRoot;
            oneWayPolicy = configuredOneWayPolicy;
            transitionEnabled = configuredTransitionEnabled;
            transitionDefinition = configuredTransitionEnabled
                ? new DoorTransitionDefinition(
                    configuredForwardSource,
                    configuredForwardDestination)
                : null;
            initializeOnEnable = false;
        }

        public void SetConditionPortsForTests(
            IDoorEncounterConditionReader encounterReader,
            IDoorTargetConditionReader targetReader,
            IDoorWalletReadPort walletReader,
            IDoorKeyReadPort keyReader)
        {
            injectedEncounterReader = encounterReader;
            injectedTargetReader = targetReader;
            injectedWalletReader = walletReader;
            injectedKeyReader = keyReader;
        }

        public void SetTransitionAuthorizationForTests(
            IDoorTransitionAuthorizationPort authorizationPort)
        {
            injectedTransitionAuthorization = authorizationPort;
        }

        public static bool IsDirectionAllowed(
            DoorOneWayPolicy policy,
            DoorTravelDirection direction)
        {
            if (direction != DoorTravelDirection.Forward
                && direction != DoorTravelDirection.Reverse)
            {
                return false;
            }

            switch (policy)
            {
                case DoorOneWayPolicy.Bidirectional:
                    return true;
                case DoorOneWayPolicy.ForwardOnly:
                    return direction == DoorTravelDirection.Forward;
                case DoorOneWayPolicy.ReverseOnly:
                    return direction == DoorTravelDirection.Reverse;
                default:
                    return false;
            }
        }

        private DoorAuthoringValidationResult ValidateAndBuild()
        {
            if (placedObject == null)
            {
                return DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.MissingPlacedObjectAuthoring,
                    "Door requires PlacedObjectAuthoring2D.");
            }

            if (configuredConditionSetForTests != null)
            {
                conditionSet = configuredConditionSetForTests;
            }
            else
            {
                if (conditions == null || conditions.Length == 0)
                {
                    return DoorAuthoringValidationResult.Failed(
                        DoorAuthoringValidationCode.MissingConditionConfiguration,
                        "Door requires at least one typed condition.");
                }

                DoorConditionRequirement[] built =
                    new DoorConditionRequirement[conditions.Length];
                try
                {
                    for (int index = 0; index < conditions.Length; index++)
                    {
                        if (conditions[index] == null)
                        {
                            throw new InvalidOperationException(
                                "Condition at index " + index + " is null.");
                        }

                        built[index] = conditions[index].BuildRequirement();
                    }

                    conditionSet = new DoorConditionSet(
                        conditionComposition,
                        built);
                }
                catch (Exception exception)
                {
                    return DoorAuthoringValidationResult.Failed(
                        DoorAuthoringValidationCode.InvalidConditionConfiguration,
                        exception.Message);
                }
            }

            if (conditionSet.Requirements.Count == 0)
            {
                return DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.MissingConditionConfiguration,
                    "Door condition set cannot be empty.");
            }

            if (closedColliders == null || closedColliders.Length == 0)
            {
                return DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.MissingClosedCollider,
                    "Door requires at least one closed-state collider.");
            }

            for (int index = 0; index < closedColliders.Length; index++)
            {
                if (closedColliders[index] == null)
                {
                    return DoorAuthoringValidationResult.Failed(
                        DoorAuthoringValidationCode.MissingClosedCollider,
                        "Closed collider at index " + index + " is missing.");
                }
            }

            if (closedPresentationRoot == null || openPresentationRoot == null)
            {
                return DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.MissingPresentationRoot,
                    "Door requires distinct closed and open presentation roots.");
            }

            if (ReferenceEquals(closedPresentationRoot, openPresentationRoot))
            {
                return DoorAuthoringValidationResult.Failed(
                    DoorAuthoringValidationCode.SharedPresentationRoot,
                    "Closed and open presentation roots must be distinct.");
            }

            if (transitionEnabled)
            {
                if (transitionDefinition == null)
                {
                    if (forwardSourceSocket == null
                        || forwardDestinationSocket == null)
                    {
                        return DoorAuthoringValidationResult.Failed(
                            DoorAuthoringValidationCode.MissingTransitionSocket,
                            "Transition door requires both typed room sockets.");
                    }

                    try
                    {
                        transitionDefinition = new DoorTransitionDefinition(
                            forwardSourceSocket.BuildSocket(),
                            forwardDestinationSocket.BuildSocket());
                    }
                    catch (Exception exception)
                    {
                        return DoorAuthoringValidationResult.Failed(
                            DoorAuthoringValidationCode.InvalidTransitionSocket,
                            exception.Message);
                    }
                }

                if (!transitionDefinition.Validation.IsValid)
                {
                    return DoorAuthoringValidationResult.Failed(
                        DoorAuthoringValidationCode.InvalidTransitionSocket,
                        transitionDefinition.Validation.Diagnostic);
                }
            }

            return DoorAuthoringValidationResult.Valid();
        }

        private DoorConditionEvaluationResult EvaluateConditionsInternal()
        {
            if (conditionSet == null)
            {
                conditionSet = configuredConditionSetForTests
                    ?? new DoorConditionSet(
                        conditionComposition,
                        Array.Empty<DoorConditionRequirement>());
            }

            DoorConditionEvaluationContext context =
                new DoorConditionEvaluationContext(
                    triggerEntered,
                    interactionRequested,
                    injectedEncounterReader
                        ?? encounterConditionReader as IDoorEncounterConditionReader,
                    injectedTargetReader
                        ?? targetConditionReader as IDoorTargetConditionReader,
                    injectedWalletReader
                        ?? walletReadPort as IDoorWalletReadPort,
                    injectedKeyReader
                        ?? keyReadPort as IDoorKeyReadPort);
            lastConditionEvaluation = conditionSet.Evaluate(context);
            return lastConditionEvaluation;
        }

        private IDoorTransitionAuthorizationPort
            ResolveTransitionAuthorizationPort()
        {
            return injectedTransitionAuthorization
                ?? transitionAuthorizationPort as IDoorTransitionAuthorizationPort;
        }

        private void ApplyState(
            DoorRuntimeState nextState,
            bool force,
            bool restartProjection)
        {
            DoorRuntimeState previous = state;
            if (!force && previous == nextState)
            {
                return;
            }

            state = nextState;
            bool isClosed = nextState == DoorRuntimeState.Closed;
            for (int index = 0; index < closedColliders.Length; index++)
            {
                if (closedColliders[index] != null)
                {
                    closedColliders[index].enabled = isClosed;
                }
            }

            if (closedPresentationRoot != null)
            {
                closedPresentationRoot.SetActive(isClosed);
            }

            if (openPresentationRoot != null)
            {
                openPresentationRoot.SetActive(!isClosed);
            }

            if (!force || previous != nextState)
            {
                StateChangeCount++;
                DoorStateChange change = new DoorStateChange(
                    doorPlacedInstanceId,
                    previous,
                    nextState,
                    restartProjection);
                Action<DoorStateChange> handler = StateChanged;
                if (handler != null)
                {
                    foreach (Delegate subscriber in handler.GetInvocationList())
                    {
                        try
                        {
                            ((Action<DoorStateChange>)subscriber)(change);
                        }
                        catch (Exception)
                        {
                            // Optional presentation observers cannot change door authority.
                        }
                    }
                }

                UnityEvent unityEvent = isClosed ? closed : opened;
                if (unityEvent != null)
                {
                    unityEvent.Invoke();
                }
            }
        }

        private void ReleaseRegistrations()
        {
            if (restartRegistered
                && restartScope != null
                && doorPlacedInstanceId != null)
            {
                restartScope.UnregisterRestartParticipant(
                    doorPlacedInstanceId,
                    this);
            }

            restartRegistered = false;
            if (placedObject != null)
            {
                placedObject.Unbind();
            }

            restartScope = null;
            doorPlacedInstanceId = null;
            initialized = false;
        }

        private static string BuildConditionFailureDiagnostic(
            DoorConditionEvaluationResult evaluation)
        {
            if (evaluation == null)
            {
                return "Door condition evaluation did not return a result.";
            }

            for (int index = 0; index < evaluation.LeafResults.Count; index++)
            {
                DoorConditionLeafResult leaf = evaluation.LeafResults[index];
                if (!leaf.IsConfigurationValid)
                {
                    return "Condition "
                        + leaf.Index
                        + ": "
                        + leaf.Diagnostic;
                }
            }

            return "Door condition configuration is invalid.";
        }

        private void OnValidate()
        {
            if (conditions == null)
            {
                conditions = Array.Empty<DoorConditionAuthoring>();
            }

            if (closedColliders == null)
            {
                closedColliders = Array.Empty<Collider2D>();
            }
        }
    }
}

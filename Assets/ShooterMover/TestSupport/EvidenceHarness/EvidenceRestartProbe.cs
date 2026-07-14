using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Contracts.Input;
using UnityEngine;

namespace ShooterMover.TestSupport.EvidenceHarness
{
    /// <summary>
    /// PlayMode-only lifecycle probe that owns disposable attempt markers, one
    /// explicit subscription, and one CS-003 intent frame. It gives EH-006 tests
    /// observable leak sentinels without introducing a scene or gameplay runtime.
    /// </summary>
    public sealed class EvidenceRestartProbe : IDisposable
    {
        public const string TestObjectPrefix = "__EH006_";

        private static int activeProbeCount;
        private static int liveOwnedObjectCount;
        private static int liveSubscriptionCount;

        private readonly EvidenceSessionLifecycle lifecycle;
        private readonly List<GameObject> liveObjects = new List<GameObject>();
        private readonly List<GameObject> retiredObjects = new List<GameObject>();
        private readonly HashSet<string> markerIds =
            new HashSet<string>(StringComparer.Ordinal);

        private event Action<string> attemptSignal;
        private Action<string> currentSubscription;
        private PlayerIntentFrame activeIntent = PlayerIntentFrame.Neutral;
        private bool lastBoundaryWasFocusLoss;
        private bool lastBoundaryReleasedHeldAction;
        private bool disposed;
        private int observedSignalCount;
        private string lastObservedAttemptId;
        private string lastObservedSignal;

        private EvidenceRestartProbe(EvidenceSessionLifecycle lifecycle)
        {
            this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            activeProbeCount++;
        }

        public static int ActiveProbeCount
        {
            get { return activeProbeCount; }
        }

        public static int LiveOwnedObjectCount
        {
            get { return liveOwnedObjectCount; }
        }

        public static int LiveSubscriptionCount
        {
            get { return liveSubscriptionCount; }
        }

        public static int SceneTestObjectCount
        {
            get
            {
                return Resources.FindObjectsOfTypeAll<GameObject>()
                    .Count(candidate => candidate != null
                        && candidate.name.StartsWith(TestObjectPrefix, StringComparison.Ordinal));
            }
        }

        public string StateName
        {
            get { return lifecycle.State.ToString(); }
        }

        public string SessionId
        {
            get { return lifecycle.CurrentAttempt.SessionId.ToString(); }
        }

        public string CurrentAttemptId
        {
            get { return lifecycle.CurrentAttempt.AttemptId.ToString(); }
        }

        public string CurrentParentAttemptId
        {
            get
            {
                return lifecycle.CurrentAttempt.ParentAttemptId == null
                    ? string.Empty
                    : lifecycle.CurrentAttempt.ParentAttemptId.ToString();
            }
        }

        public string PendingAttemptId
        {
            get
            {
                return lifecycle.PendingAttempt == null
                    ? string.Empty
                    : lifecycle.PendingAttempt.AttemptId.ToString();
            }
        }

        public int CurrentAttemptOrdinal
        {
            get { return lifecycle.CurrentAttempt.Ordinal; }
        }

        public string CanonicalStartIdentity
        {
            get { return lifecycle.StartIdentity.ToCanonicalString(); }
        }

        public string ConfigurationFingerprint
        {
            get { return lifecycle.Configuration.Fingerprint; }
        }

        public int AuditCount
        {
            get { return lifecycle.AuditTrail.Count; }
        }

        public string AuditSnapshot
        {
            get { return lifecycle.CaptureAuditSnapshot(); }
        }

        public int MarkerCount
        {
            get { return markerIds.Count; }
        }

        public string[] CurrentMarkerIds
        {
            get
            {
                return markerIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        public int CurrentOwnedObjectCount
        {
            get { return liveObjects.Count; }
        }

        public int CurrentSubscriptionCount
        {
            get { return currentSubscription == null ? 0 : 1; }
        }

        public int RetiredObjectLeakCount
        {
            get { return retiredObjects.Count(candidate => candidate != null); }
        }

        public bool HasStaleIntent
        {
            get
            {
                return activeIntent.Move.MagnitudeSquared > 0f
                    || activeIntent.Aim.MagnitudeSquared > 0f
                    || activeIntent.UiNavigation.MagnitudeSquared > 0f
                    || IsActive(activeIntent.Fire)
                    || IsActive(activeIntent.PowerModifier)
                    || IsActive(activeIntent.Thruster)
                    || IsActive(activeIntent.Interact)
                    || IsActive(activeIntent.Map)
                    || IsActive(activeIntent.PauseMenu)
                    || activeIntent.WasFocusLost;
            }
        }

        public bool LastBoundaryWasFocusLoss
        {
            get { return lastBoundaryWasFocusLoss; }
        }

        public bool LastBoundaryReleasedHeldAction
        {
            get { return lastBoundaryReleasedHeldAction; }
        }

        public int ObservedSignalCount
        {
            get { return observedSignalCount; }
        }

        public string LastObservedAttemptId
        {
            get { return lastObservedAttemptId ?? string.Empty; }
        }

        public string LastObservedSignal
        {
            get { return lastObservedSignal ?? string.Empty; }
        }

        public int[] CurrentObjectInstanceIds
        {
            get
            {
                return liveObjects
                    .Where(candidate => candidate != null)
                    .Select(candidate => candidate.GetInstanceID())
                    .OrderBy(value => value)
                    .ToArray();
            }
        }

        public static EvidenceRestartProbe Create(
            string canonicalConfiguration,
            string sessionId,
            string initialAttemptId)
        {
            return new EvidenceRestartProbe(
                EvidenceSessionLifecycle.ConfigureFromCanonicalJson(
                    canonicalConfiguration,
                    sessionId,
                    initialAttemptId));
        }

        public string BeginStart()
        {
            RequireNotDisposed();
            return lifecycle.BeginStart().ToCanonicalString();
        }

        public string CompleteStart()
        {
            RequireNotDisposed();
            if (lifecycle.State == EvidenceSessionState.Starting)
            {
                ActivateAttempt(lifecycle.CurrentAttempt);
            }

            EvidenceSessionTransition transition = lifecycle.CompleteStart();
            if (transition.WasRejected)
            {
                CleanupAttemptResources();
            }

            return transition.ToCanonicalString();
        }

        public string BeginRestart(string nextAttemptId)
        {
            RequireNotDisposed();
            EvidenceSessionTransition transition = lifecycle.BeginRestart(nextAttemptId);
            if (transition.WasApplied)
            {
                CleanupAttemptResources();
            }
            else if (transition.WasRejected)
            {
                CleanupAttemptResources();
            }

            return transition.ToCanonicalString();
        }

        public string CompleteRestart()
        {
            RequireNotDisposed();
            if (lifecycle.State == EvidenceSessionState.Restarting
                && lifecycle.PendingAttempt != null)
            {
                ActivateAttempt(lifecycle.PendingAttempt);
            }

            EvidenceSessionTransition transition = lifecycle.CompleteRestart();
            if (transition.WasRejected)
            {
                CleanupAttemptResources();
            }

            return transition.ToCanonicalString();
        }

        public string QuickRestart(string nextAttemptId)
        {
            RequireNotDisposed();
            EvidenceSessionTransition begin = lifecycle.BeginRestart(nextAttemptId);
            if (!begin.WasApplied)
            {
                if (begin.WasRejected)
                {
                    CleanupAttemptResources();
                }

                return begin.ToCanonicalString();
            }

            CleanupAttemptResources();
            ActivateAttempt(lifecycle.PendingAttempt);
            EvidenceSessionTransition complete = lifecycle.CompleteRestart();
            if (complete.WasRejected)
            {
                CleanupAttemptResources();
            }

            return complete.ToCanonicalString();
        }

        public string Invalidate(string reasonCode)
        {
            RequireNotDisposed();
            EvidenceSessionTransition transition = lifecycle.Invalidate(reasonCode);
            CleanupAttemptResources();
            return transition.ToCanonicalString();
        }

        public string AbortRestartForTest()
        {
            return Invalidate("lifecycle.restart-interrupted");
        }

        public string BeginEnd()
        {
            RequireNotDisposed();
            EvidenceSessionTransition transition = lifecycle.BeginEnd();
            CleanupAttemptResources();
            return transition.ToCanonicalString();
        }

        public string CompleteEndCompleted()
        {
            RequireNotDisposed();
            return lifecycle.CompleteEnd(RunEndKind.Completed).ToCanonicalString();
        }

        public string CompleteEndAborted()
        {
            RequireNotDisposed();
            return lifecycle.CompleteEnd(RunEndKind.Aborted).ToCanonicalString();
        }

        public string EndCompleted()
        {
            RequireNotDisposed();
            lifecycle.BeginEnd();
            CleanupAttemptResources();
            return lifecycle.CompleteEnd(RunEndKind.Completed).ToCanonicalString();
        }

        public void SetHeldIntentForTest()
        {
            RequireNotDisposed();
            activeIntent = new PlayerIntentFrame(
                NormalizedIntentVector2.Create(0.75f, -0.25f),
                NormalizedIntentVector2.Create(0.4f, 0.9f),
                ButtonIntent.Held,
                ButtonIntent.Inactive,
                ButtonIntent.Held,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);
        }

        public void ClearIntentForFocusLoss()
        {
            RequireNotDisposed();
            ClearIntentAtBoundary();
        }

        public void EmitAttemptSignalForTest(string signal)
        {
            RequireNotDisposed();
            Action<string> callback = attemptSignal;
            if (callback != null)
            {
                callback(signal ?? string.Empty);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            CleanupAttemptResources();
            if (lifecycle.State != EvidenceSessionState.Ended)
            {
                lifecycle.BeginEnd();
                if (lifecycle.State == EvidenceSessionState.Ending)
                {
                    lifecycle.CompleteEnd(RunEndKind.Aborted);
                }
            }

            attemptSignal = null;
            disposed = true;
            activeProbeCount--;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeProbeCount = 0;
            liveOwnedObjectCount = 0;
            liveSubscriptionCount = 0;
        }

        private void ActivateAttempt(EvidenceSessionAttemptIdentity attempt)
        {
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

            if (liveObjects.Count != 0 || markerIds.Count != 0 || currentSubscription != null)
            {
                throw new InvalidOperationException(
                    "Attempt resources must be fully released before activation.");
            }

            string attemptId = attempt.AttemptId.ToString();
            GameObject root = CreateOwnedObject("root." + attemptId, null);
            string[] requiredMarkers =
            {
                "session.start",
                attempt.StartIdentity.RouteStartMarkerId,
                attempt.StartIdentity.ArenaStartMarkerId
            };

            for (int index = 0; index < requiredMarkers.Length; index++)
            {
                string markerId = requiredMarkers[index];
                if (!markerIds.Add(markerId))
                {
                    CleanupAttemptResources();
                    throw new InvalidOperationException(
                        "Duplicate evidence marker '" + markerId + "' was rejected.");
                }

                CreateOwnedObject("marker." + markerId + "." + attemptId, root.transform);
            }

            string capturedAttemptId = attemptId;
            currentSubscription = delegate(string signal)
            {
                observedSignalCount++;
                lastObservedAttemptId = capturedAttemptId;
                lastObservedSignal = signal;
            };
            attemptSignal += currentSubscription;
            liveSubscriptionCount++;
            activeIntent = PlayerIntentFrame.Neutral;
        }

        private GameObject CreateOwnedObject(string suffix, Transform parent)
        {
            var created = new GameObject(TestObjectPrefix + suffix);
            created.hideFlags = HideFlags.HideAndDontSave;
            if (parent != null)
            {
                created.transform.SetParent(parent, false);
            }

            liveObjects.Add(created);
            liveOwnedObjectCount++;
            return created;
        }

        private void CleanupAttemptResources()
        {
            ClearIntentAtBoundary();

            if (currentSubscription != null)
            {
                attemptSignal -= currentSubscription;
                currentSubscription = null;
                liveSubscriptionCount--;
            }

            for (int index = liveObjects.Count - 1; index >= 0; index--)
            {
                GameObject candidate = liveObjects[index];
                retiredObjects.Add(candidate);
                if (candidate != null)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }
            }

            liveOwnedObjectCount -= liveObjects.Count;
            liveObjects.Clear();
            markerIds.Clear();
        }

        private void ClearIntentAtBoundary()
        {
            PlayerIntentFrame boundary = PlayerIntentFrame.FromFocusLoss(activeIntent);
            lastBoundaryWasFocusLoss = boundary.WasFocusLost;
            lastBoundaryReleasedHeldAction =
                boundary.Fire.WasReleased || boundary.Thruster.WasReleased;
            activeIntent = PlayerIntentFrame.Neutral;
        }

        private void RequireNotDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(EvidenceRestartProbe));
            }
        }

        private static bool IsActive(ButtonIntent value)
        {
            return value.IsHeld || value.WasPressed || value.WasReleased;
        }
    }
}

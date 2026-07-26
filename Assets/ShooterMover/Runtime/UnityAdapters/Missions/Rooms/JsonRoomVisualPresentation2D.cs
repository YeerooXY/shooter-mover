using System;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Synchronizes imported JSON visual sidecars with the committed current-room
    /// presentation revision without becoming another room runtime authority.
    /// </summary>
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class JsonRoomVisualPresentation2D : MonoBehaviour
    {
        [SerializeField] private JsonRoomRuntimeBootstrap2D jsonRoomRuntimeBootstrap;
        [SerializeField] private RoomRuntimeComposition2D roomRuntimeComposition;
        [SerializeField] private RoomPresentationCatalog2D presentationCatalog;
        [SerializeField] private Transform presentationRoot;

        private readonly RoomVisualPresentationScene2D presentation =
            new RoomVisualPresentationScene2D();
        private bool isSubscribed;
        private long appliedPresentationRevision;
        private long lastAttemptedPresentationRevision;
        private long lastLoggedFailureRevision = long.MinValue;
        private string lastLoggedFailureMessage;
        private string lastBuildError;

        public bool IsSynchronized
        {
            get
            {
                return jsonRoomRuntimeBootstrap != null
                    && jsonRoomRuntimeBootstrap.IsBuilt
                    && jsonRoomRuntimeBootstrap.ImportedBundle != null
                    && roomRuntimeComposition != null
                    && roomRuntimeComposition.IsBuilt
                    && appliedPresentationRevision > 0L
                    && appliedPresentationRevision
                        == roomRuntimeComposition.PresentationRevision
                    && string.IsNullOrEmpty(lastBuildError);
            }
        }

        public long AppliedPresentationRevision
        {
            get { return appliedPresentationRevision; }
        }

        public int SpawnedVisualCount
        {
            get { return presentation.SpawnedVisualCount; }
        }

        public string LastBuildError
        {
            get { return lastBuildError; }
        }

        /// <summary>
        /// Explicitly retries the current committed revision. This remains safe after a
        /// catalogue or prefab repair because failed revisions are never marked as applied.
        /// </summary>
        public bool Synchronize()
        {
            return TrySynchronizeCurrentRevision(true);
        }

        private void OnEnable()
        {
            Subscribe();
            TrySynchronizeCurrentRevision(true);
        }

        private void Start()
        {
            TrySynchronizeCurrentRevision(false);
        }

        private void Update()
        {
            if (roomRuntimeComposition == null) return;

            long revision = roomRuntimeComposition.PresentationRevision;
            if (revision <= 0L
                || revision == appliedPresentationRevision
                || revision == lastAttemptedPresentationRevision)
            {
                return;
            }

            TrySynchronizeCurrentRevision(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            presentation.DestroyOwnedPresentation();
        }

        private void Subscribe()
        {
            if (isSubscribed || roomRuntimeComposition == null) return;
            roomRuntimeComposition.CurrentRoomPresentationRebuilt +=
                HandleCurrentRoomPresentationRebuilt;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed) return;
            if (roomRuntimeComposition != null)
            {
                roomRuntimeComposition.CurrentRoomPresentationRebuilt -=
                    HandleCurrentRoomPresentationRebuilt;
            }
            isSubscribed = false;
        }

        private void HandleCurrentRoomPresentationRebuilt()
        {
            TrySynchronizeCurrentRevision(false);
        }

        private bool TrySynchronizeCurrentRevision(bool forceRetry)
        {
            if (!TryGetReadyState(
                    out RoomContentBundleV1 bundle,
                    out long revision))
            {
                return false;
            }
            if (revision == appliedPresentationRevision)
            {
                return true;
            }
            if (!forceRetry && revision == lastAttemptedPresentationRevision)
            {
                return false;
            }

            lastAttemptedPresentationRevision = revision;
            try
            {
                presentation.BuildCurrentRoom(
                    bundle,
                    roomRuntimeComposition.CurrentRoomStableId,
                    presentationCatalog,
                    presentationRoot == null ? transform : presentationRoot);
                appliedPresentationRevision = revision;
                lastBuildError = null;
                lastLoggedFailureRevision = long.MinValue;
                lastLoggedFailureMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                if (IsFatalException(exception)) throw;

                lastBuildError = BuildFailureMessage(revision, exception);
                LogBuildFailureOnce(revision, lastBuildError);
                return false;
            }
        }

        private bool TryGetReadyState(
            out RoomContentBundleV1 bundle,
            out long revision)
        {
            bundle = null;
            revision = 0L;
            if (jsonRoomRuntimeBootstrap == null
                || roomRuntimeComposition == null
                || presentationCatalog == null)
            {
                return false;
            }
            if (!jsonRoomRuntimeBootstrap.IsBuilt
                || !roomRuntimeComposition.IsBuilt)
            {
                return false;
            }

            bundle = jsonRoomRuntimeBootstrap.ImportedBundle;
            revision = roomRuntimeComposition.PresentationRevision;
            return bundle != null
                && revision > 0L
                && roomRuntimeComposition.CurrentRoomStableId != null;
        }

        private string BuildFailureMessage(long revision, Exception exception)
        {
            string room = roomRuntimeComposition.CurrentRoomStableId == null
                ? "<unknown>"
                : roomRuntimeComposition.CurrentRoomStableId.ToString();
            return "JSON room visual presentation failed for room "
                + room
                + " at presentation revision "
                + revision
                + ": "
                + exception.GetType().Name
                + ": "
                + exception.Message;
        }

        private void LogBuildFailureOnce(long revision, string message)
        {
            if (lastLoggedFailureRevision == revision
                && string.Equals(
                    lastLoggedFailureMessage,
                    message,
                    StringComparison.Ordinal))
            {
                return;
            }

            lastLoggedFailureRevision = revision;
            lastLoggedFailureMessage = message;
            Debug.LogError(message, this);
        }

        private static bool IsFatalException(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }
}

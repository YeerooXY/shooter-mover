using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Imports authored JSON room content and hands its compiled runtime definition to the
    /// existing room runtime composition. The complete imported bundle remains available
    /// for later sidecar integrations without introducing another room authority.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class RoomLoader : MonoBehaviour
    {
        [SerializeField] private RoomFile roomContentDefinition;
        [SerializeField] private LevelRooms roomRuntimeComposition;
        [SerializeField] private RoomArt presentationCatalog;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private string runtimeInstanceStableId =
            "room-runtime-instance.json-bootstrap";

        private bool isBuilt;
        private RoomContentBundle importedBundle;
        private IReadOnlyList<RoomContentImportIssue> lastImportIssues =
            Array.Empty<RoomContentImportIssue>();

        /// <summary>
        /// Raised synchronously after the room authority and accepted imported bundle have
        /// committed, but before the production caller continues with dependent projections.
        /// Subscribers may compose required downstream ports; an exception fails the caller
        /// closed rather than allowing a partially connected gameplay scene.
        /// </summary>
        public event Action<RoomContentBundle> BuildAccepted;

        public bool IsBuilt
        {
            get { return isBuilt; }
        }

        public RoomContentBundle ImportedBundle
        {
            get { return importedBundle; }
        }

        public IReadOnlyList<RoomContentImportIssue> LastImportIssues
        {
            get { return lastImportIssues; }
        }

        public void Configure(
            RoomFile configuredRoomContentDefinition,
            LevelRooms configuredRoomRuntimeComposition,
            RoomArt configuredPresentationCatalog,
            Transform configuredPresentationRoot,
            string configuredRuntimeInstanceStableId)
        {
            if (isBuilt || (roomRuntimeComposition != null
                && roomRuntimeComposition.IsBuilt))
            {
                throw new InvalidOperationException(
                    "A built JSON room bootstrap cannot be reconfigured.");
            }

            roomContentDefinition = configuredRoomContentDefinition
                ?? throw new ArgumentNullException(
                    nameof(configuredRoomContentDefinition));
            roomRuntimeComposition = configuredRoomRuntimeComposition
                ?? throw new ArgumentNullException(
                    nameof(configuredRoomRuntimeComposition));
            presentationCatalog = configuredPresentationCatalog
                ?? throw new ArgumentNullException(
                    nameof(configuredPresentationCatalog));
            presentationRoot = configuredPresentationRoot
                ?? throw new ArgumentNullException(
                    nameof(configuredPresentationRoot));
            if (string.IsNullOrWhiteSpace(configuredRuntimeInstanceStableId))
            {
                throw new ArgumentException(
                    "A room runtime instance stable ID is required.",
                    nameof(configuredRuntimeInstanceStableId));
            }
            runtimeInstanceStableId = configuredRuntimeInstanceStableId.Trim();
            buildOnAwake = false;
        }

        public bool BuildFromJson()
        {
            ValidateBuildReferences();
            if (isBuilt || roomRuntimeComposition.IsBuilt)
            {
                throw new InvalidOperationException(
                    "The JSON room runtime bootstrap cannot build more than one room runtime.");
            }

            StableId stableRuntimeInstanceId = ParseRuntimeInstanceStableId();
            RoomContentImportResult importResult;
            try
            {
                importResult = roomContentDefinition.Import();
            }
            catch (Exception exception)
            {
                if (exception is OutOfMemoryException
                    || exception is StackOverflowException
                    || exception is AccessViolationException)
                {
                    throw;
                }

                RoomContentImportIssue issue = CreateImportExceptionIssue(exception);
                lastImportIssues = new[] { issue };
                LogImportFailure(issue, lastImportIssues.Count);
                return false;
            }

            if (importResult == null)
            {
                RoomContentImportIssue issue = new RoomContentImportIssue(
                    "room-content-import-result-missing",
                    "$",
                    "The JSON room importer returned no result.");
                lastImportIssues = new[] { issue };
                LogImportFailure(issue, lastImportIssues.Count);
                return false;
            }

            lastImportIssues = importResult.Issues;
            if (!importResult.IsValid)
            {
                RoomContentImportIssue issue = FirstIssueOrFallback(lastImportIssues);
                if (lastImportIssues.Count == 0)
                {
                    lastImportIssues = new[] { issue };
                }

                LogImportFailure(issue, lastImportIssues.Count);
                return false;
            }

            RoomContentBundle acceptedBundle = importResult.Bundle;
            roomRuntimeComposition.ConfigureDefinition(
                acceptedBundle.RuntimeDefinition,
                presentationCatalog,
                presentationRoot);
            roomRuntimeComposition.BuildSession(stableRuntimeInstanceId);

            importedBundle = acceptedBundle;
            isBuilt = true;
            Action<RoomContentBundle> accepted = BuildAccepted;
            if (accepted != null)
            {
                accepted(acceptedBundle);
            }
            return true;
        }

        private void Awake()
        {
            if (buildOnAwake)
            {
                BuildFromJson();
            }
        }

        private void ValidateBuildReferences()
        {
            if (roomContentDefinition == null)
            {
                throw new InvalidOperationException(
                    "A JSON room content definition is required.");
            }
            if (roomRuntimeComposition == null)
            {
                throw new InvalidOperationException(
                    "A room runtime composition is required.");
            }
            if (presentationCatalog == null)
            {
                throw new InvalidOperationException(
                    "A room presentation catalog is required.");
            }
            if (presentationRoot == null)
            {
                throw new InvalidOperationException(
                    "A room presentation root is required.");
            }
        }

        private StableId ParseRuntimeInstanceStableId()
        {
            if (string.IsNullOrWhiteSpace(runtimeInstanceStableId))
            {
                throw new InvalidOperationException(
                    "A room runtime instance stable ID is required.");
            }

            return StableId.Parse(runtimeInstanceStableId);
        }

        private static RoomContentImportIssue CreateImportExceptionIssue(
            Exception exception)
        {
            string message = exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? "The JSON room importer threw an unexpected exception."
                : exception.Message;
            return new RoomContentImportIssue(
                "room-content-import-exception",
                "$",
                message);
        }

        private static RoomContentImportIssue FirstIssueOrFallback(
            IReadOnlyList<RoomContentImportIssue> issues)
        {
            if (issues != null && issues.Count > 0 && issues[0] != null)
            {
                return issues[0];
            }

            return new RoomContentImportIssue(
                "room-content-import-invalid",
                "$",
                "The JSON room import failed without a structured issue.");
        }

        private void LogImportFailure(
            RoomContentImportIssue issue,
            int issueCount)
        {
            string suffix = issueCount > 1
                ? " (" + issueCount + " structured issues retained.)"
                : string.Empty;
            Debug.LogError(
                "JSON room import failed ["
                + issue.Code
                + "] at "
                + issue.Path
                + ": "
                + issue.Message
                + suffix,
                this);
        }
    }
}

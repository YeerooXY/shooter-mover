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
    public sealed class JsonRoomRuntimeBootstrap2D : MonoBehaviour
    {
        [SerializeField] private JsonRoomContentDefinition2D roomContentDefinition;
        [SerializeField] private RoomRuntimeComposition2D roomRuntimeComposition;
        [SerializeField] private RoomPresentationCatalog2D presentationCatalog;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private bool buildOnAwake = true;
        [SerializeField] private string runtimeInstanceStableId =
            "room-runtime-instance.json-bootstrap";

        private bool isBuilt;
        private RoomContentBundleV1 importedBundle;
        private IReadOnlyList<RoomContentImportIssueV1> lastImportIssues =
            Array.Empty<RoomContentImportIssueV1>();

        public bool IsBuilt
        {
            get { return isBuilt; }
        }

        public RoomContentBundleV1 ImportedBundle
        {
            get { return importedBundle; }
        }

        public IReadOnlyList<RoomContentImportIssueV1> LastImportIssues
        {
            get { return lastImportIssues; }
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
            RoomContentImportResultV1 importResult;
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

                RoomContentImportIssueV1 issue = CreateImportExceptionIssue(exception);
                lastImportIssues = new[] { issue };
                LogImportFailure(issue, lastImportIssues.Count);
                return false;
            }

            if (importResult == null)
            {
                RoomContentImportIssueV1 issue = new RoomContentImportIssueV1(
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
                RoomContentImportIssueV1 issue = FirstIssueOrFallback(lastImportIssues);
                if (lastImportIssues.Count == 0)
                {
                    lastImportIssues = new[] { issue };
                }

                LogImportFailure(issue, lastImportIssues.Count);
                return false;
            }

            RoomContentBundleV1 acceptedBundle = importResult.Bundle;
            roomRuntimeComposition.ConfigureDefinition(
                acceptedBundle.RuntimeDefinition,
                presentationCatalog,
                presentationRoot);
            roomRuntimeComposition.BuildSession(stableRuntimeInstanceId);

            importedBundle = acceptedBundle;
            isBuilt = true;
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

        private static RoomContentImportIssueV1 CreateImportExceptionIssue(
            Exception exception)
        {
            string message = exception == null || string.IsNullOrWhiteSpace(exception.Message)
                ? "The JSON room importer threw an unexpected exception."
                : exception.Message;
            return new RoomContentImportIssueV1(
                "room-content-import-exception",
                "$",
                message);
        }

        private static RoomContentImportIssueV1 FirstIssueOrFallback(
            IReadOnlyList<RoomContentImportIssueV1> issues)
        {
            if (issues != null && issues.Count > 0 && issues[0] != null)
            {
                return issues[0];
            }

            return new RoomContentImportIssueV1(
                "room-content-import-invalid",
                "$",
                "The JSON room import failed without a structured issue.");
        }

        private void LogImportFailure(
            RoomContentImportIssueV1 issue,
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

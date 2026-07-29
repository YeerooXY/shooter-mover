using System;
using ShooterMover.ContentPackages.Environment.Doors;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.ContentPackages.LevelDesign.Foundation
{
    /// <summary>
    /// Thin LEVELDES-001 composition seam over the existing DOOR-001 package.
    /// It contributes editor-facing metadata and preview only; Door
    /// remains the door runtime authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlacedObject))]
    [RequireComponent(typeof(Door))]
    [RequireComponent(typeof(DoorConnection))]
    public sealed class DoorMarker :
        MonoBehaviour,
        ILevelDoorPackageBridge
    {
        [Header("Existing package components")]
        [SerializeField] private PlacedObject placedObject;
        [SerializeField] private Door doorController;
        [SerializeField] private DoorConnection connection;

        [Header("Closed/open presentation")]
        [SerializeField] private GameObject closedPresentationRoot;
        [SerializeField] private GameObject openPresentationRoot;
        [SerializeField] private Sprite openDoorSprite;
        [SerializeField] private Collider2D[] closedColliders =
            Array.Empty<Collider2D>();

        [Header("Designer preview")]
        [SerializeField] private bool previewOpen;

        public bool HasDoorController
        {
            get { return doorController != null && placedObject != null; }
        }

        public bool HasClosedPresentation
        {
            get { return closedPresentationRoot != null; }
        }

        public bool HasOpenPresentation
        {
            get
            {
                return openPresentationRoot != null
                    && openPresentationRoot != closedPresentationRoot
                    && openDoorSprite != null;
            }
        }

        public Sprite OpenDoorSprite
        {
            get { return openDoorSprite; }
        }

        public bool HasClosedCollider
        {
            get
            {
                if (closedColliders == null || closedColliders.Length == 0)
                {
                    return false;
                }

                for (int index = 0; index < closedColliders.Length; index++)
                {
                    if (closedColliders[index] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public PlacedObject PlacedObject
        {
            get { return placedObject; }
        }

        public Door DoorController
        {
            get { return doorController; }
        }

        public DoorConnection Connection
        {
            get { return connection; }
        }

        public string ValidateComposition()
        {
            if (placedObject == null)
            {
                return "Configured door requires PlacedObject.";
            }

            if (doorController == null)
            {
                return "Configured door requires the existing DOOR-001 Door.";
            }

            if (connection == null)
            {
                return "Configured door requires DoorConnection.";
            }

            if (closedPresentationRoot == null
                || openPresentationRoot == null
                || closedPresentationRoot == openPresentationRoot)
            {
                return "Configured door requires distinct closed and open presentation roots.";
            }

            if (openDoorSprite == null)
            {
                return "Configured door requires the supplied open-door Sprite.";
            }

            if (!HasClosedCollider)
            {
                return "Configured door requires one or more assigned closed-state colliders.";
            }

            return string.Empty;
        }

        [ContextMenu("Preview Closed")]
        public void PreviewClosed()
        {
            previewOpen = false;
            ApplyPreview();
        }

        [ContextMenu("Preview Open")]
        public void PreviewOpen()
        {
            previewOpen = true;
            ApplyPreview();
        }

        public void ApplyPreview()
        {
            if (UnityEngine.Application.isPlaying)
            {
                return;
            }

            if (closedPresentationRoot != null)
            {
                closedPresentationRoot.SetActive(!previewOpen);
            }

            if (openPresentationRoot != null)
            {
                openPresentationRoot.SetActive(previewOpen);
            }

            if (closedColliders == null)
            {
                return;
            }

            for (int index = 0; index < closedColliders.Length; index++)
            {
                if (closedColliders[index] != null)
                {
                    closedColliders[index].enabled = !previewOpen;
                }
            }
        }

        public void ConfigureForTests(
            PlacedObject configuredPlacedObject,
            Door configuredDoorController,
            DoorConnection configuredConnection,
            GameObject configuredClosedPresentation,
            GameObject configuredOpenPresentation,
            Collider2D[] configuredClosedColliders)
        {
            placedObject = configuredPlacedObject;
            doorController = configuredDoorController;
            connection = configuredConnection;
            closedPresentationRoot = configuredClosedPresentation;
            openPresentationRoot = configuredOpenPresentation;
            closedColliders = configuredClosedColliders
                ?? Array.Empty<Collider2D>();
        }

        private void Reset()
        {
            ResolveSameObjectReferences();
        }

        private void OnValidate()
        {
            ResolveSameObjectReferences();
            ApplyPreview();
        }

        private void ResolveSameObjectReferences()
        {
            if (placedObject == null)
            {
                placedObject = GetComponent<PlacedObject>();
            }

            if (doorController == null)
            {
                doorController = GetComponent<Door>();
            }

            if (connection == null)
            {
                connection = GetComponent<DoorConnection>();
            }
        }
    }
}

using System;
using ShooterMover.ContentPackages.Environment.Doors;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;

namespace ShooterMover.ContentPackages.LevelDesign.Foundation
{
    /// <summary>
    /// Thin LEVELDES-001 composition seam over the existing DOOR-001 package.
    /// It contributes editor-facing metadata and preview only; DoorController2D
    /// remains the door runtime authority.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlacedObjectAuthoring2D))]
    [RequireComponent(typeof(DoorController2D))]
    [RequireComponent(typeof(LevelDoorConnectionAuthoring2D))]
    public sealed class ConfiguredDoorAuthoring2D :
        MonoBehaviour,
        ILevelDoorPackageAdapter
    {
        [Header("Existing package components")]
        [SerializeField] private PlacedObjectAuthoring2D placedObject;
        [SerializeField] private DoorController2D doorController;
        [SerializeField] private LevelDoorConnectionAuthoring2D connection;

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

        public PlacedObjectAuthoring2D PlacedObject
        {
            get { return placedObject; }
        }

        public DoorController2D DoorController
        {
            get { return doorController; }
        }

        public LevelDoorConnectionAuthoring2D Connection
        {
            get { return connection; }
        }

        public string ValidateComposition()
        {
            if (placedObject == null)
            {
                return "Configured door requires PlacedObjectAuthoring2D.";
            }

            if (doorController == null)
            {
                return "Configured door requires the existing DOOR-001 DoorController2D.";
            }

            if (connection == null)
            {
                return "Configured door requires LevelDoorConnectionAuthoring2D.";
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
            PlacedObjectAuthoring2D configuredPlacedObject,
            DoorController2D configuredDoorController,
            LevelDoorConnectionAuthoring2D configuredConnection,
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
                placedObject = GetComponent<PlacedObjectAuthoring2D>();
            }

            if (doorController == null)
            {
                doorController = GetComponent<DoorController2D>();
            }

            if (connection == null)
            {
                connection = GetComponent<LevelDoorConnectionAuthoring2D>();
            }
        }
    }
}

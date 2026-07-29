using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Unity-only owner for imported visual-sidecar presentation objects. It retains no
    /// room authority and rebuilds only the visuals belonging to one selected room.
    /// </summary>
    internal sealed class RoomDecor
    {
        private const string GeneratedRootName = "RoomVisuals";
        private const int BackgroundSortingOrder = -2000;
        private const int TileSortingOrder = -1000;
        private const int ForegroundSortingOrder = 1000;

        private readonly List<GameObject> spawnedVisuals = new List<GameObject>();
        private Transform configuredParent;
        private Transform generatedRoot;
        private Transform backgroundRoot;
        private Transform tileRoot;
        private Transform foregroundRoot;

        public int SpawnedVisualCount
        {
            get { return spawnedVisuals.Count; }
        }

        public void BuildCurrentRoom(
            RoomContentBundle bundle,
            StableId currentRoomStableId,
            RoomArt catalog,
            Transform presentationRoot)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (currentRoomStableId == null)
            {
                throw new ArgumentNullException(nameof(currentRoomStableId));
            }
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }

            EnsureLayerRoots(presentationRoot);
            Clear();

            var ordered = new List<RoomVisualPlacementContent>();
            for (int index = 0; index < bundle.Visuals.Count; index++)
            {
                RoomVisualPlacementContent visual = bundle.Visuals[index];
                if (visual.RoomStableId == currentRoomStableId)
                {
                    ordered.Add(visual);
                }
            }
            ordered.Sort(CompareVisuals);

            try
            {
                for (int index = 0; index < ordered.Count; index++)
                {
                    SpawnVisual(catalog, ordered[index]);
                }
            }
            catch (Exception exception)
            {
                CleanupAndRethrow(exception, Clear);
            }
        }

        public void Clear()
        {
            for (int index = spawnedVisuals.Count - 1; index >= 0; index--)
            {
                DestroyObject(spawnedVisuals[index]);
            }
            spawnedVisuals.Clear();
        }

        public void DestroyOwnedPresentation()
        {
            Clear();
            if (generatedRoot != null)
            {
                DestroyObject(generatedRoot.gameObject);
            }

            configuredParent = null;
            generatedRoot = null;
            backgroundRoot = null;
            tileRoot = null;
            foregroundRoot = null;
        }

        private void EnsureLayerRoots(Transform presentationRoot)
        {
            if (generatedRoot != null
                && backgroundRoot != null
                && tileRoot != null
                && foregroundRoot != null
                && configuredParent == presentationRoot)
            {
                return;
            }

            DestroyOwnedPresentation();
            configuredParent = presentationRoot;
            try
            {
                generatedRoot = CreateRoot(GeneratedRootName, presentationRoot, null, 0);
                backgroundRoot = CreateRoot(
                    "Background",
                    generatedRoot,
                    typeof(SortingGroup),
                    BackgroundSortingOrder);
                tileRoot = CreateRoot(
                    "Tiles",
                    generatedRoot,
                    typeof(SortingGroup),
                    TileSortingOrder);
                foregroundRoot = CreateRoot(
                    "Foreground",
                    generatedRoot,
                    typeof(SortingGroup),
                    ForegroundSortingOrder);
            }
            catch (Exception exception)
            {
                CleanupAndRethrow(exception, DestroyOwnedPresentation);
            }
        }

        private void SpawnVisual(
            RoomArt catalog,
            RoomVisualPlacementContent visual)
        {
            GameObject prefab;
            if (!catalog.TryResolve(visual.PresentationStableId, out prefab))
            {
                throw new InvalidOperationException(
                    "room-visual-presentation-missing:" + visual.PresentationStableId);
            }

            Transform layerRoot = GetLayerRoot(visual.Layer);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, layerRoot);
            spawnedVisuals.Add(instance);
            instance.name = visual.InstanceStableId.ToString();
            instance.transform.localPosition = new Vector3(
                (float)visual.LocalPosition.X,
                (float)visual.LocalPosition.Y,
                0f);
            instance.transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                (float)visual.LocalRotationDegrees);
            instance.SetActive(true);
        }

        private Transform GetLayerRoot(RoomContentVisualLayer layer)
        {
            switch (layer)
            {
                case RoomContentVisualLayer.Background:
                    return backgroundRoot;
                case RoomContentVisualLayer.Tile:
                    return tileRoot;
                case RoomContentVisualLayer.Foreground:
                    return foregroundRoot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private static Transform CreateRoot(
            string name,
            Transform parent,
            Type componentType,
            int sortingOrder)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            if (componentType != null)
            {
                var sortingGroup = (SortingGroup)root.AddComponent(componentType);
                sortingGroup.sortingOrder = sortingOrder;
            }
            return root.transform;
        }

        private static int CompareVisuals(
            RoomVisualPlacementContent left,
            RoomVisualPlacementContent right)
        {
            int layerComparison = LayerBuildOrder(left.Layer).CompareTo(
                LayerBuildOrder(right.Layer));
            return layerComparison != 0
                ? layerComparison
                : left.InstanceStableId.CompareTo(right.InstanceStableId);
        }

        private static int LayerBuildOrder(RoomContentVisualLayer layer)
        {
            switch (layer)
            {
                case RoomContentVisualLayer.Background:
                    return 0;
                case RoomContentVisualLayer.Tile:
                    return 1;
                case RoomContentVisualLayer.Foreground:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private static void CleanupAndRethrow(
            Exception constructionException,
            Action cleanup)
        {
            bool constructionWasFatal = IsFatalException(constructionException);
            try
            {
                cleanup();
            }
            catch (Exception cleanupException)
            {
                if (IsFatalException(cleanupException) && !constructionWasFatal)
                {
                    ExceptionDispatchInfo.Capture(cleanupException).Throw();
                }
                if (!IsFatalException(cleanupException))
                {
                    Debug.LogException(cleanupException);
                }
            }

            ExceptionDispatchInfo.Capture(constructionException).Throw();
        }

        private static bool IsFatalException(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private static void DestroyObject(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}

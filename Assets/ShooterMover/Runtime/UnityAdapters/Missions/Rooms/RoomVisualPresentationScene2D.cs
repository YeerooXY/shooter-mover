using System;
using System.Collections.Generic;
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
    internal sealed class RoomVisualPresentationScene2D
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
            RoomContentBundleV1 bundle,
            StableId currentRoomStableId,
            RoomPresentationCatalog2D catalog,
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

            var ordered = new List<RoomVisualPlacementContentV1>();
            for (int index = 0; index < bundle.Visuals.Count; index++)
            {
                RoomVisualPlacementContentV1 visual = bundle.Visuals[index];
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
            catch
            {
                Clear();
                throw;
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
            if (generatedRoot != null && configuredParent == presentationRoot)
            {
                return;
            }

            DestroyOwnedPresentation();
            configuredParent = presentationRoot;
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

        private void SpawnVisual(
            RoomPresentationCatalog2D catalog,
            RoomVisualPlacementContentV1 visual)
        {
            GameObject prefab;
            if (!catalog.TryResolve(visual.PresentationStableId, out prefab))
            {
                throw new InvalidOperationException(
                    "room-visual-presentation-missing:" + visual.PresentationStableId);
            }

            Transform layerRoot = GetLayerRoot(visual.Layer);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, layerRoot);
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
            spawnedVisuals.Add(instance);
        }

        private Transform GetLayerRoot(RoomContentVisualLayerV1 layer)
        {
            switch (layer)
            {
                case RoomContentVisualLayerV1.Background:
                    return backgroundRoot;
                case RoomContentVisualLayerV1.Tile:
                    return tileRoot;
                case RoomContentVisualLayerV1.Foreground:
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
            RoomVisualPlacementContentV1 left,
            RoomVisualPlacementContentV1 right)
        {
            int layerComparison = LayerBuildOrder(left.Layer).CompareTo(
                LayerBuildOrder(right.Layer));
            return layerComparison != 0
                ? layerComparison
                : left.InstanceStableId.CompareTo(right.InstanceStableId);
        }

        private static int LayerBuildOrder(RoomContentVisualLayerV1 layer)
        {
            switch (layer)
            {
                case RoomContentVisualLayerV1.Background:
                    return 0;
                case RoomContentVisualLayerV1.Tile:
                    return 1;
                case RoomContentVisualLayerV1.Foreground:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer));
            }
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

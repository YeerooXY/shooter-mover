#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Refreshes diagnostics after authoring changes. This observer is deliberately read-only with
    /// respect to room topology and edge-managed door placement: it reports stale/misaligned state
    /// but never reflows or migrates the scene behind the designer's back.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelGridAuthoringV2LiveValidation
    {
        private static readonly HashSet<LevelDesignSceneAuthoringRoot2D> PendingRoots =
            new HashSet<LevelDesignSceneAuthoringRoot2D>();
        private static readonly Dictionary<int, int> HierarchySignatureByRoot =
            new Dictionary<int, int>();
        private static bool refreshScheduled;

        static LevelGridAuthoringV2LiveValidation()
        {
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.delayCall += QueueAllLoadedRootsInitially;
        }

        internal static void ValidateNow(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridValidationPurposeV2 purpose,
            bool reflow = false,
            bool notifyWindows = true)
        {
            if (!IsSceneRoot(root))
            {
                return;
            }

            // The compatibility parameter remains so existing callers compile, but validation is
            // intentionally read-only. Explicit reflow belongs to LevelGridEditorOperationsV2.
            root.ValidateHierarchy();
            root.ValidateGridAuthoring(purpose);
            MarkSynchronouslyValidated(root);
            if (notifyWindows)
            {
                NotifyOpenWindows();
            }
        }

        internal static void MarkSynchronouslyValidated(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                return;
            }

            PendingRoots.Remove(root);
            UpdateHierarchySignature(root);
        }

        private static UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            var capturedFixedDoors = new HashSet<LevelDoorEndpointAuthoring2D>();
            for (int index = 0; index < modifications.Length; index++)
            {
                UndoPropertyModification modification = modifications[index];
                UnityEngine.Object target = modification.currentValue.target;
                GameObject gameObject = target as GameObject;
                Component component = target as Component;
                if (gameObject == null && component != null)
                {
                    gameObject = component.gameObject;
                }

                Transform movedTransform = target as Transform;
                if (movedTransform != null
                    && IsPositionModification(modification.currentValue.propertyPath))
                {
                    LevelDoorEndpointAuthoring2D fixedDoor =
                        movedTransform.GetComponent<LevelDoorEndpointAuthoring2D>();
                    if (fixedDoor != null
                        && fixedDoor.PlacementMode == LevelDoorPlacementModeV2.Fixed
                        && capturedFixedDoors.Add(fixedDoor))
                    {
                        // The designer explicitly moved a fixed door. Capture that authored value;
                        // do not move the transform or alter topology.
                        CaptureFixedDoorPositionWithUndo(fixedDoor);
                    }
                }

                LevelDesignSceneAuthoringRoot2D root = gameObject == null
                    ? null
                    : gameObject.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
                QueueRoot(root);
            }

            return modifications;
        }

        private static void OnUndoRedoPerformed()
        {
            QueueAllLoadedRoots(true);
        }

        private static void OnHierarchyChanged()
        {
            QueueAllLoadedRoots(false);
        }

        private static void QueueAllLoadedRootsInitially()
        {
            QueueAllLoadedRoots(true);
        }

        private static void QueueAllLoadedRoots(bool force)
        {
            LevelDesignSceneAuthoringRoot2D[] roots =
                Resources.FindObjectsOfTypeAll<LevelDesignSceneAuthoringRoot2D>();
            for (int index = 0; index < roots.Length; index++)
            {
                LevelDesignSceneAuthoringRoot2D root = roots[index];
                if (!IsSceneRoot(root))
                {
                    continue;
                }

                if (!force && !HasHierarchyChanged(root))
                {
                    continue;
                }
                QueueRoot(root);
            }
        }

        private static void QueueRoot(LevelDesignSceneAuthoringRoot2D root)
        {
            if (!IsSceneRoot(root))
            {
                return;
            }

            PendingRoots.Add(root);
            ScheduleRefresh();
        }

        private static void ScheduleRefresh()
        {
            if (refreshScheduled)
            {
                return;
            }

            refreshScheduled = true;
            EditorApplication.delayCall += RefreshPendingRoots;
        }

        private static void RefreshPendingRoots()
        {
            EditorApplication.delayCall -= RefreshPendingRoots;
            refreshScheduled = false;
            LevelDesignSceneAuthoringRoot2D[] roots =
                new LevelDesignSceneAuthoringRoot2D[PendingRoots.Count];
            PendingRoots.CopyTo(roots);
            PendingRoots.Clear();

            for (int index = 0; index < roots.Length; index++)
            {
                LevelDesignSceneAuthoringRoot2D root = roots[index];
                if (!IsSceneRoot(root))
                {
                    continue;
                }

                LevelGridValidationPurposeV2 purpose =
                    root.LastGridValidation.Purpose
                        == LevelGridValidationPurposeV2.ProductionPublish
                    ? LevelGridValidationPurposeV2.ProductionPublish
                    : LevelGridValidationPurposeV2.Draft;
                ValidateNow(root, purpose, false, false);
            }
            NotifyOpenWindows();
        }

        private static void NotifyOpenWindows()
        {
            LevelGridProblemsWindowV2[] windows =
                Resources.FindObjectsOfTypeAll<LevelGridProblemsWindowV2>();
            for (int index = 0; index < windows.Length; index++)
            {
                windows[index].Repaint();
            }

            LevelGridEditorWindowV2[] gridEditors =
                Resources.FindObjectsOfTypeAll<LevelGridEditorWindowV2>();
            for (int index = 0; index < gridEditors.Length; index++)
            {
                gridEditors[index].ReconcileSelectedDiagnosticsAfterValidation();
                gridEditors[index].RefreshAfterExternalValidation();
            }
            SceneView.RepaintAll();
        }

        private static void CaptureFixedDoorPositionWithUndo(
            LevelDoorEndpointAuthoring2D door)
        {
            if (door == null
                || door.PlacementMode != LevelDoorPlacementModeV2.Fixed)
            {
                return;
            }

            Undo.RecordObject(door, "Move Fixed Level Door");
            door.CaptureCurrentFixedPosition();
            EditorUtility.SetDirty(door);
        }

        private static bool HasHierarchyChanged(
            LevelDesignSceneAuthoringRoot2D root)
        {
            int rootId = root.GetInstanceID();
            int current = ComputeHierarchySignature(root);
            int previous;
            if (!HierarchySignatureByRoot.TryGetValue(rootId, out previous)
                || previous != current)
            {
                HierarchySignatureByRoot[rootId] = current;
                return true;
            }
            return false;
        }

        private static void UpdateHierarchySignature(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (!IsSceneRoot(root))
            {
                return;
            }
            HierarchySignatureByRoot[root.GetInstanceID()] =
                ComputeHierarchySignature(root);
        }

        private static int ComputeHierarchySignature(
            LevelDesignSceneAuthoringRoot2D root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            Array.Sort(
                components,
                delegate(Component left, Component right)
                {
                    int leftId = left == null ? 0 : left.GetInstanceID();
                    int rightId = right == null ? 0 : right.GetInstanceID();
                    return leftId.CompareTo(rightId);
                });

            unchecked
            {
                int hash = 17;
                for (int index = 0; index < components.Length; index++)
                {
                    Component component = components[index];
                    if (component == null)
                    {
                        continue;
                    }
                    hash = hash * 31 + component.GetInstanceID();
                    Transform parent = component.transform.parent;
                    hash = hash * 31 + (parent == null ? 0 : parent.GetInstanceID());
                }
                return hash;
            }
        }

        private static bool IsSceneRoot(LevelDesignSceneAuthoringRoot2D root)
        {
            return root != null
                && root.gameObject.scene.IsValid()
                && !EditorUtility.IsPersistent(root);
        }

        private static bool IsPositionModification(string propertyPath)
        {
            return !string.IsNullOrEmpty(propertyPath)
                && propertyPath.StartsWith("m_LocalPosition");
        }
    }
}
#endif

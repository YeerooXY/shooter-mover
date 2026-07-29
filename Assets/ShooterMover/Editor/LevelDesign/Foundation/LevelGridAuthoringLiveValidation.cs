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
    public static class LevelGridAuthoringLiveValidation
    {
        private static readonly HashSet<LevelDraft> PendingRoots =
            new HashSet<LevelDraft>();
        private static readonly Dictionary<int, int> HierarchySignatureByRoot =
            new Dictionary<int, int>();
        private static bool refreshScheduled;

        static LevelGridAuthoringLiveValidation()
        {
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.delayCall += QueueAllLoadedRootsInitially;
        }

        internal static void ValidateNow(
            LevelDraft root,
            LevelGridValidationPurpose purpose,
            bool reflow = false,
            bool notifyWindows = true)
        {
            if (!IsSceneRoot(root))
            {
                return;
            }

            // The compatibility parameter remains so existing callers compile, but validation is
            // intentionally read-only. Explicit reflow belongs to LevelGridEditorOperations.
            root.ValidateHierarchy();
            root.ValidateGridAuthoring(purpose);
            MarkSynchronouslyValidated(root);
            if (notifyWindows)
            {
                NotifyOpenWindows();
            }
        }

        internal static void MarkSynchronouslyValidated(
            LevelDraft root)
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
            var capturedFixedDoors = new HashSet<DoorEndpoint>();
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
                    DoorEndpoint fixedDoor =
                        movedTransform.GetComponent<DoorEndpoint>();
                    if (fixedDoor != null
                        && fixedDoor.PlacementMode == LevelDoorPlacementMode.Fixed
                        && capturedFixedDoors.Add(fixedDoor))
                    {
                        // The designer explicitly moved a fixed door. Capture that authored value;
                        // do not move the transform or alter topology.
                        CaptureFixedDoorPositionWithUndo(fixedDoor);
                    }
                }

                LevelDraft root = gameObject == null
                    ? null
                    : gameObject.GetComponentInParent<LevelDraft>();
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
            LevelDraft[] roots =
                Resources.FindObjectsOfTypeAll<LevelDraft>();
            for (int index = 0; index < roots.Length; index++)
            {
                LevelDraft root = roots[index];
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

        private static void QueueRoot(LevelDraft root)
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
            LevelDraft[] roots =
                new LevelDraft[PendingRoots.Count];
            PendingRoots.CopyTo(roots);
            PendingRoots.Clear();

            for (int index = 0; index < roots.Length; index++)
            {
                LevelDraft root = roots[index];
                if (!IsSceneRoot(root))
                {
                    continue;
                }

                LevelGridValidationPurpose purpose =
                    root.LastGridValidation.Purpose
                        == LevelGridValidationPurpose.ProductionPublish
                    ? LevelGridValidationPurpose.ProductionPublish
                    : LevelGridValidationPurpose.Draft;
                ValidateNow(root, purpose, false, false);
            }
            NotifyOpenWindows();
        }

        private static void NotifyOpenWindows()
        {
            LevelGridProblemsWindow[] windows =
                Resources.FindObjectsOfTypeAll<LevelGridProblemsWindow>();
            for (int index = 0; index < windows.Length; index++)
            {
                windows[index].Repaint();
            }

            LevelGridEditorWindow[] gridEditors =
                Resources.FindObjectsOfTypeAll<LevelGridEditorWindow>();
            for (int index = 0; index < gridEditors.Length; index++)
            {
                gridEditors[index].ReconcileSelectedDiagnosticsAfterValidation();
                gridEditors[index].RefreshAfterExternalValidation();
            }
            SceneView.RepaintAll();
        }

        private static void CaptureFixedDoorPositionWithUndo(
            DoorEndpoint door)
        {
            if (door == null
                || door.PlacementMode != LevelDoorPlacementMode.Fixed)
            {
                return;
            }

            Undo.RecordObject(door, "Move Fixed Level Door");
            door.CaptureCurrentFixedPosition();
            EditorUtility.SetDirty(door);
        }

        private static bool HasHierarchyChanged(
            LevelDraft root)
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
            LevelDraft root)
        {
            if (!IsSceneRoot(root))
            {
                return;
            }
            HierarchySignatureByRoot[root.GetInstanceID()] =
                ComputeHierarchySignature(root);
        }

        private static int ComputeHierarchySignature(
            LevelDraft root)
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

        private static bool IsSceneRoot(LevelDraft root)
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

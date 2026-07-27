#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    [InitializeOnLoad]
    public static class LevelGridAuthoringV2LiveValidation
    {
        private static readonly HashSet<LevelDesignSceneAuthoringRoot2D> PendingRoots =
            new HashSet<LevelDesignSceneAuthoringRoot2D>();
        private static readonly HashSet<LevelDesignSceneAuthoringRoot2D> ReflowRoots =
            new HashSet<LevelDesignSceneAuthoringRoot2D>();
        private static readonly Dictionary<int, int> HierarchySignatureByRoot =
            new Dictionary<int, int>();
        private static readonly FieldInfo SelectedGridProblemField =
            typeof(LevelGridEditorWindowV2).GetField(
                "selectedProblem",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SelectedFoundationIssueField =
            typeof(LevelGridEditorWindowV2).GetField(
                "selectedFoundationIssue",
                BindingFlags.Instance | BindingFlags.NonPublic);
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
            bool reflow = true,
            bool notifyWindows = true)
        {
            if (!IsSceneRoot(root))
            {
                return;
            }

            MigrateLegacyFixedDoorPositions(root);
            if (reflow)
            {
                LevelGridDoorOperationsV2.ReflowAll(root);
            }
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
            ReflowRoots.Remove(root);
            UpdateHierarchySignature(root);
        }

        private static UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            HashSet<LevelDoorEndpointAuthoring2D> capturedFixedDoors =
                new HashSet<LevelDoorEndpointAuthoring2D>();
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
                        CaptureFixedDoorPositionWithUndo(fixedDoor);
                    }
                }

                LevelDesignSceneAuthoringRoot2D root = gameObject == null
                    ? null
                    : gameObject.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
                QueueRoot(root, true);
            }

            return modifications;
        }

        private static void OnUndoRedoPerformed()
        {
            QueueAllLoadedRoots(true, false);
        }

        private static void OnHierarchyChanged()
        {
            QueueAllLoadedRoots(false, true);
        }

        private static void QueueAllLoadedRootsInitially()
        {
            QueueAllLoadedRoots(true, true);
        }

        private static void QueueAllLoadedRoots(bool force, bool reflow)
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
                QueueRoot(root, reflow);
            }
        }

        private static void QueueRoot(
            LevelDesignSceneAuthoringRoot2D root,
            bool reflow)
        {
            if (!IsSceneRoot(root))
            {
                return;
            }

            PendingRoots.Add(root);
            if (reflow)
            {
                ReflowRoots.Add(root);
            }
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

                bool reflow = ReflowRoots.Remove(root);
                LevelGridValidationPurposeV2 purpose =
                    root.LastGridValidation.Purpose
                        == LevelGridValidationPurposeV2.ProductionPublish
                    ? LevelGridValidationPurposeV2.ProductionPublish
                    : LevelGridValidationPurposeV2.Draft;
                ValidateNow(root, purpose, reflow, false);
            }
            ReflowRoots.Clear();
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
                RefreshSelectedDiagnostic(gridEditors[index]);
                gridEditors[index].RefreshAfterExternalValidation();
            }
            SceneView.RepaintAll();
        }

        private static void RefreshSelectedDiagnostic(
            LevelGridEditorWindowV2 window)
        {
            if (window == null)
            {
                return;
            }

            LevelDesignSceneAuthoringRoot2D root = window.ActiveRoot;
            if (SelectedGridProblemField != null)
            {
                LevelGridProblemV2 selected =
                    SelectedGridProblemField.GetValue(window) as LevelGridProblemV2;
                SelectedGridProblemField.SetValue(
                    window,
                    FindCurrentGridProblem(root, selected));
            }
            if (SelectedFoundationIssueField != null)
            {
                LevelDesignValidationIssue selected =
                    SelectedFoundationIssueField.GetValue(window)
                        as LevelDesignValidationIssue;
                SelectedFoundationIssueField.SetValue(
                    window,
                    FindCurrentFoundationIssue(root, selected));
            }
        }

        private static LevelGridProblemV2 FindCurrentGridProblem(
            LevelDesignSceneAuthoringRoot2D root,
            LevelGridProblemV2 selected)
        {
            if (root == null || selected == null)
            {
                return null;
            }

            IReadOnlyList<LevelGridProblemV2> problems =
                root.LastGridValidation.Problems;
            for (int index = 0; index < problems.Count; index++)
            {
                LevelGridProblemV2 candidate = problems[index];
                if (candidate.Code == selected.Code
                    && string.Equals(
                        candidate.AuthoredId,
                        selected.AuthoredId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.DiagnosticLocation,
                        selected.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static LevelDesignValidationIssue FindCurrentFoundationIssue(
            LevelDesignSceneAuthoringRoot2D root,
            LevelDesignValidationIssue selected)
        {
            if (root == null || selected == null)
            {
                return null;
            }

            IReadOnlyList<LevelDesignValidationIssue> issues =
                root.LastValidation.Issues;
            for (int index = 0; index < issues.Count; index++)
            {
                LevelDesignValidationIssue candidate = issues[index];
                if (candidate.Code == selected.Code
                    && string.Equals(
                        candidate.AuthoredId,
                        selected.AuthoredId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.DiagnosticLocation,
                        selected.DiagnosticLocation,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return null;
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

        private static int MigrateLegacyFixedDoorPositions(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (!IsSceneRoot(root) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return 0;
            }

            LevelDoorEndpointAuthoring2D[] doors =
                root.GetComponentsInChildren<LevelDoorEndpointAuthoring2D>(true);
            int changed = 0;
            int undoGroup = -1;
            for (int index = 0; index < doors.Length; index++)
            {
                LevelDoorEndpointAuthoring2D door = doors[index];
                if (door == null
                    || door.PlacementMode != LevelDoorPlacementModeV2.Fixed
                    || door.UsesOwningRoomFixedPositionSpace
                    || door.OwningRoom == null)
                {
                    continue;
                }

                if (undoGroup < 0)
                {
                    Undo.IncrementCurrentGroup();
                    undoGroup = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Migrate Fixed Door Position Space");
                }
                Undo.RecordObject(door, "Migrate Fixed Door Position Space");
                if (!door.MigrateFixedPositionSpaceForAuthoring())
                {
                    continue;
                }

                EditorUtility.SetDirty(door);
                changed++;
            }

            if (undoGroup >= 0)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }
            return changed;
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

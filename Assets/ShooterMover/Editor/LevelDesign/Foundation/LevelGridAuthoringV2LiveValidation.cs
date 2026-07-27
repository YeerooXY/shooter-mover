#if UNITY_EDITOR
using System.Collections.Generic;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    [InitializeOnLoad]
    public static class LevelGridAuthoringV2LiveValidation
    {
        private static readonly HashSet<LevelDesignSceneAuthoringRoot2D> PendingRoots =
            new HashSet<LevelDesignSceneAuthoringRoot2D>();
        private static bool refreshScheduled;

        static LevelGridAuthoringV2LiveValidation()
        {
            Undo.postprocessModifications += OnPostprocessModifications;
        }

        private static UndoPropertyModification[] OnPostprocessModifications(
            UndoPropertyModification[] modifications)
        {
            for (int index = 0; index < modifications.Length; index++)
            {
                Object target = modifications[index].currentValue.target;
                GameObject gameObject = target as GameObject;
                Component component = target as Component;
                if (gameObject == null && component != null)
                {
                    gameObject = component.gameObject;
                }

                LevelDesignSceneAuthoringRoot2D root = gameObject == null
                    ? null
                    : gameObject.GetComponentInParent<LevelDesignSceneAuthoringRoot2D>();
                if (root != null)
                {
                    PendingRoots.Add(root);
                }
            }

            if (PendingRoots.Count > 0 && !refreshScheduled)
            {
                refreshScheduled = true;
                EditorApplication.delayCall += RefreshPendingRoots;
            }

            return modifications;
        }

        private static void RefreshPendingRoots()
        {
            refreshScheduled = false;
            foreach (LevelDesignSceneAuthoringRoot2D root in PendingRoots)
            {
                if (root == null)
                {
                    continue;
                }

                root.ValidateGridAuthoring(root.LastGridValidation.Purpose);
            }
            PendingRoots.Clear();

            LevelGridProblemsWindowV2[] windows =
                Resources.FindObjectsOfTypeAll<LevelGridProblemsWindowV2>();
            for (int index = 0; index < windows.Length; index++)
            {
                windows[index].Repaint();
            }
            SceneView.RepaintAll();
        }
    }
}
#endif

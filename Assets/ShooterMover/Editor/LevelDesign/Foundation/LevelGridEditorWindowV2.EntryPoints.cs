#if UNITY_EDITOR
using System;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public sealed partial class LevelGridEditorWindowV2
    {
        /// <summary>
        /// Opens the canonical Level Grid editor focused on the exact selected authoring root.
        /// Compatibility menus and inspectors use this entry point rather than retaining their own
        /// topology workflows.
        /// </summary>
        public static LevelGridEditorWindowV2 OpenForRoot(
            LevelDesignSceneAuthoringRoot2D root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            LevelGridEditorWindowV2 window = GetWindow<LevelGridEditorWindowV2>();
            window.titleContent = new GUIContent("Level Grid Editor");
            window.minSize = new Vector2(780f, 520f);
            window.Show();
            window.SetActiveRoot(root);
            window.Repaint();
            return window;
        }
    }
}
#endif

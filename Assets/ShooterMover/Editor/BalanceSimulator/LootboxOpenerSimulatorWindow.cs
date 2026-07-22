using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Compatibility entry point retained for the established menu command. The former
    /// direct-generation window duplicated box selection and odds behavior outside the
    /// live BOX authority. Opening this tool now delegates to the authoritative hybrid
    /// strongbox window, which consumes the same resolver and catalogs as gameplay.
    /// </summary>
    public sealed class LootboxOpenerSimulatorWindow : EditorWindow
    {
        [MenuItem("Tools/Shooter Mover/Lootbox Opener Simulator")]
        public static void Open()
        {
            AuthoritativeStrongboxSimulatorWindow.Open();
        }

        private void OnEnable()
        {
            EditorApplication.delayCall += RedirectAndClose;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RedirectAndClose;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "The Lootbox Opener now runs through the authoritative hybrid BOX wiring. Redirecting...",
                MessageType.Info);
            if (GUILayout.Button("Open Authoritative Hybrid Opener"))
            {
                RedirectAndClose();
            }
        }

        private void RedirectAndClose()
        {
            EditorApplication.delayCall -= RedirectAndClose;
            AuthoritativeStrongboxSimulatorWindow.Open();
            Close();
        }
    }
}

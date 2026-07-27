#if UNITY_EDITOR
using UnityEditor;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Keeps Phase-1 and arbitrary-path utilities available to retained migration/test code while
    /// preventing them from appearing as usable production workflows. The supported designer route
    /// is the exact-root Level Grid editor and its Validate / Build actions.
    /// </summary>
    internal static class LevelGridLegacySurfaceGuardsV2
    {
        [MenuItem(
            "Tools/Shooter Mover/Level Design/Create Three-Room Starter Example",
            true)]
        private static bool DisableThreeRoomStarter()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Export Grid V2 Draft Folder...",
            true)]
        private static bool DisablePhaseOneDraftExport()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Publish Grid V2 Validated Authoring Folder...",
            true)]
        private static bool DisablePhaseOneValidatedExport()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Export Compiler-Ready Grid V2 Package...",
            true)]
        private static bool DisableArbitraryPlayableExport()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Compile Tracked Combat Loop Grid V2",
            true)]
        private static bool DisableTrackedCompilerShortcut()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Compile Grid V2 Folder...",
            true)]
        private static bool DisableArbitraryCompilerShortcut()
        {
            return false;
        }
    }
}
#endif

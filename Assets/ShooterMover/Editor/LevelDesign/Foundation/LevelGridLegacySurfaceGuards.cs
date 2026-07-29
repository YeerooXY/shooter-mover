#if UNITY_EDITOR
using UnityEditor;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    /// <summary>
    /// Keeps Phase-1 and arbitrary-path utilities available to retained migration/test code while
    /// preventing them from appearing as usable production workflows. The supported designer route
    /// is the exact-root Level Grid editor and its Validate / Build actions.
    /// </summary>
    internal static class LevelGridLegacySurfaceGuards
    {
        [MenuItem(
            "Tools/Shooter Mover/Level Design/Create Three-Room Starter Example",
            true)]
        private static bool DisableThreeRoomStarter()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Export Level Draft Folder...",
            true)]
        private static bool DisablePhaseOneDraftExport()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Publish Level Validated Authoring Folder...",
            true)]
        private static bool DisablePhaseOneValidatedExport()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Export Compiler-Ready Level Package...",
            true)]
        private static bool DisableArbitraryPlayableExport()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Compile Tracked Combat Loop Level",
            true)]
        private static bool DisableTrackedCompilerShortcut()
        {
            return false;
        }

        [MenuItem(
            "Tools/Shooter Mover/Level Design/Compile Level Folder...",
            true)]
        private static bool DisableArbitraryCompilerShortcut()
        {
            return false;
        }
    }
}
#endif

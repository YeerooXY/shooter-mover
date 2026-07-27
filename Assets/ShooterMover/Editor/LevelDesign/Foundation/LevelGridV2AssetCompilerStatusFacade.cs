#if UNITY_EDITOR
using System;
using ShooterMover.Application.Missions.Rooms.Content;

namespace ShooterMover.Editor.LevelDesign.Foundation
{
    public static partial class LevelGridV2AssetCompiler
    {
        /// <summary>
        /// Returns the exact content-addressed version ID used by transactional publication.
        /// Status code consumes this instead of defining a second fingerprint algorithm.
        /// </summary>
        internal static string ComputePublishedVersionIdForStatus(
            RoomContentJsonPackageV1 package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            return ComputePackageVersionId(package);
        }
    }
}
#endif
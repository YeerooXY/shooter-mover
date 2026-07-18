using System;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.UnityAdapters.Progression.Skills.Content
{
    /// <summary>
    /// Stable public entry point for JSON-driven ranked skill content. The parser builds
    /// first-class combined-rank requirements directly into the V2 domain catalog.
    /// </summary>
    public static class RankedSkillJsonCanonicalV2
    {
        public static RankedSkillJsonImportResultV1 Import(string json) => RankedSkillJsonImporterV1.Import(json);

        public static SkillEffectSnapshotV2 ProjectEffects(
            RankedSkillJsonImportResultV1 import,
            RankedSkillAllocationSnapshotV2 allocation)
        {
            if (import == null || !import.Success)
                throw new ArgumentException("A successful canonical import is required.", nameof(import));
            if (allocation == null) throw new ArgumentNullException(nameof(allocation));
            return new SkillEffectProjectorV2().Project(import.Catalog, allocation);
        }
    }
}

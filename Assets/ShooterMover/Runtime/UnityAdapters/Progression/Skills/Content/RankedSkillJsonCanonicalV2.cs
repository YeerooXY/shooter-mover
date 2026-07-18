using System;
using System.Linq;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.UnityAdapters.Progression.Skills.Content
{
    /// <summary>
    /// Canonical JSON import surface after combined-rank requirements became first-class
    /// SKILL-FOUNDATION-003 domain data. The V1 parser remains a compatibility parser;
    /// this adapter upgrades its parsed authoring metadata into one complete runtime catalog.
    /// </summary>
    public static class RankedSkillJsonCanonicalV2
    {
        public static RankedSkillJsonImportResultV1 Import(string json)
        {
            RankedSkillJsonImportResultV1 parsed = RankedSkillJsonImporterV1.Import(json);
            if (!parsed.Success) return parsed;

            ImportedSkillSynergyV1[] imported = parsed.ImportedSynergies
                .Select(x => new ImportedSkillSynergyV1(
                    new SkillSynergyDefinitionV2(
                        x.Definition.Id,
                        x.Definition.Requirements,
                        x.Definition.Effects,
                        x.CombinedRequirements.Select(r =>
                            new SkillCombinedRankRequirementV2(r.SkillIds, r.MinimumCombinedRanks))),
                    x.DisplayName,
                    x.Description,
                    x.EligibleClassIds,
                    x.CombinedRequirements))
                .ToArray();

            var catalog = new RankedSkillCatalogV2(
                parsed.Catalog.SchemaVersion,
                parsed.Catalog.ContentVersion,
                parsed.Catalog.Skills,
                imported.Select(x => x.Definition));

            return new RankedSkillJsonImportResultV1(
                catalog,
                imported,
                parsed.ClassIds,
                parsed.Diagnostics,
                parsed.NormalizedFingerprint);
        }

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

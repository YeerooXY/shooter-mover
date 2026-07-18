using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ShooterMover.Domain.Progression.Skills;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Progression.Skills.Content
{
    public enum SkillCatalogDiagnosticSeverityV1 { Warning, Error }

    public sealed class SkillCatalogDiagnosticV1
    {
        public SkillCatalogDiagnosticV1(string jsonPath, string definitionId, string errorCode, string message, SkillCatalogDiagnosticSeverityV1 severity)
        {
            JsonPath = jsonPath ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
        }
        public string JsonPath { get; }
        public string DefinitionId { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public SkillCatalogDiagnosticSeverityV1 Severity { get; }
    }

    public sealed class CombinedSkillRankRequirementV1
    {
        public CombinedSkillRankRequirementV1(IEnumerable<string> skillIds, int minimumCombinedRanks)
        {
            var ids = (skillIds ?? throw new ArgumentNullException(nameof(skillIds))).Select(x => (x ?? string.Empty).Trim()).ToList();
            if (ids.Count < 2 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                throw new ArgumentException("Combined-rank gates require at least two unique skill ids.", nameof(skillIds));
            if (minimumCombinedRanks < 2) throw new ArgumentOutOfRangeException(nameof(minimumCombinedRanks));
            SkillIds = new ReadOnlyCollection<string>(ids);
            MinimumCombinedRanks = minimumCombinedRanks;
        }
        public IReadOnlyList<string> SkillIds { get; }
        public int MinimumCombinedRanks { get; }
        public bool IsSatisfied(RankedSkillAllocationSnapshotV2 allocation) => SkillIds.Sum(allocation.RankOf) >= MinimumCombinedRanks;
        public string Canonical => string.Join(",", SkillIds.OrderBy(x => x, StringComparer.Ordinal)) + ">=" + MinimumCombinedRanks;
    }

    public sealed class ImportedSkillSynergyV1
    {
        public ImportedSkillSynergyV1(SkillSynergyDefinitionV2 definition, string displayName, string description, IEnumerable<string> eligibleClassIds, IEnumerable<CombinedSkillRankRequirementV1> combinedRequirements)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            EligibleClassIds = new ReadOnlyCollection<string>((eligibleClassIds ?? Array.Empty<string>()).ToList());
            CombinedRequirements = new ReadOnlyCollection<CombinedSkillRankRequirementV1>((combinedRequirements ?? Array.Empty<CombinedSkillRankRequirementV1>()).ToList());
        }
        public SkillSynergyDefinitionV2 Definition { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> EligibleClassIds { get; }
        public IReadOnlyList<CombinedSkillRankRequirementV1> CombinedRequirements { get; }
        public bool HasCombinedRequirements => CombinedRequirements.Count != 0;
        public bool IsSatisfied(RankedSkillAllocationSnapshotV2 allocation)
        {
            if (EligibleClassIds.Count != 0 && !EligibleClassIds.Contains(allocation.ClassId, StringComparer.Ordinal)) return false;
            return Definition.IsSatisfied(allocation);
        }
    }

    public sealed class RankedSkillCatalogSummaryV1
    {
        public RankedSkillCatalogSummaryV1(RankedSkillCatalogV2 catalog, IEnumerable<string> classIds)
        {
            TotalSkills = catalog.Skills.Count;
            TotalPurchasableRanks = catalog.Skills.Sum(x => x.MaximumRank);
            MilestoneCount = catalog.Skills.Sum(x => x.Milestones.Count);
            SynergyCount = catalog.Synergies.Count;
            SkillsByCategory = new ReadOnlyDictionary<string, int>(catalog.Skills.GroupBy(x => x.CategoryId, StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal));
            MaximumRanksByClass = new ReadOnlyDictionary<string, int>((classIds ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal).ToDictionary(x => x, x => catalog.Skills.Where(s => s.IsEligible(x)).Sum(s => s.EffectiveMaximumRank(x)), StringComparer.Ordinal));
        }
        public int TotalSkills { get; }
        public int TotalPurchasableRanks { get; }
        public int MilestoneCount { get; }
        public int SynergyCount { get; }
        public IReadOnlyDictionary<string, int> SkillsByCategory { get; }
        public IReadOnlyDictionary<string, int> MaximumRanksByClass { get; }
    }

    public sealed class RankedSkillJsonImportResultV1
    {
        public RankedSkillJsonImportResultV1(RankedSkillCatalogV2 catalog, IEnumerable<ImportedSkillSynergyV1> importedSynergies, IEnumerable<string> classIds, IEnumerable<SkillCatalogDiagnosticV1> diagnostics, string normalizedFingerprint)
        {
            Catalog = catalog;
            ImportedSynergies = new ReadOnlyCollection<ImportedSkillSynergyV1>((importedSynergies ?? Array.Empty<ImportedSkillSynergyV1>()).ToList());
            ClassIds = new ReadOnlyCollection<string>((classIds ?? Array.Empty<string>()).ToList());
            Diagnostics = new ReadOnlyCollection<SkillCatalogDiagnosticV1>((diagnostics ?? Array.Empty<SkillCatalogDiagnosticV1>()).ToList());
            NormalizedFingerprint = normalizedFingerprint ?? string.Empty;
            Summary = catalog == null ? null : new RankedSkillCatalogSummaryV1(catalog, ClassIds);
        }
        public RankedSkillCatalogV2 Catalog { get; }
        public IReadOnlyList<ImportedSkillSynergyV1> ImportedSynergies { get; }
        public IReadOnlyList<string> ClassIds { get; }
        public IReadOnlyList<SkillCatalogDiagnosticV1> Diagnostics { get; }
        public string NormalizedFingerprint { get; }
        public RankedSkillCatalogSummaryV1 Summary { get; }
        public bool Success => Catalog != null && Diagnostics.All(x => x.Severity != SkillCatalogDiagnosticSeverityV1.Error);
    }

    public static class RankedSkillJsonImporterV1
    {
        public const string SupportedSchemaVersion = "ranked-skills-schema-v1";
        private const decimal MaximumLegendaryRelativeBonus = 0.20m;

        public static RankedSkillJsonImportResultV1 Import(string json)
        {
            var diagnostics = new List<SkillCatalogDiagnosticV1>();
            if (string.IsNullOrWhiteSpace(json)) { Error(diagnostics, "$", string.Empty, "skill-json-empty", "Skill catalog JSON is empty."); return Failure(diagnostics); }
            CatalogDto dto;
            try { dto = JsonUtility.FromJson<CatalogDto>(json); }
            catch (Exception ex) { Error(diagnostics, "$", string.Empty, "skill-json-invalid", ex.Message); return Failure(diagnostics); }
            if (dto == null) { Error(diagnostics, "$", string.Empty, "skill-json-null", "JSON produced no catalog object."); return Failure(diagnostics); }
            if (!string.Equals(dto.schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal)) Error(diagnostics, "$.schemaVersion", string.Empty, "skill-schema-unsupported", "Unsupported schema version: " + dto.schemaVersion);
            if (string.IsNullOrWhiteSpace(dto.contentVersion)) Error(diagnostics, "$.contentVersion", string.Empty, "skill-content-version-missing", "Content version is required.");

            var classIds = UniqueIds(dto.classes, "$.classes", "class", diagnostics);
            var categoryIds = UniqueIds(dto.categories, "$.categories", "category", diagnostics);
            var effectKinds = BuildEffectRegistry(dto.effects, diagnostics);
            var skillDtos = dto.skills ?? Array.Empty<SkillDto>();
            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < skillDtos.Length; i++)
            {
                string id = Clean(skillDtos[i]?.id);
                if (string.IsNullOrEmpty(id)) Error(diagnostics, "$.skills[" + i + "].id", id, "skill-id-missing", "Skill id is required.");
                else if (!skillIds.Add(id)) Error(diagnostics, "$.skills[" + i + "].id", id, "skill-id-duplicate", "Duplicate skill id.");
            }
            var skills = new List<RankedSkillDefinitionV2>();
            for (int i = 0; i < skillDtos.Length; i++)
            {
                RankedSkillDefinitionV2 skill = BuildSkill(skillDtos[i], i, classIds, categoryIds, effectKinds, skillIds, diagnostics);
                if (skill != null) skills.Add(skill);
            }
            var importedSynergies = BuildSynergies(dto.synergies, skills, classIds, effectKinds, diagnostics);
            if (diagnostics.Any(x => x.Severity == SkillCatalogDiagnosticSeverityV1.Error)) return Failure(diagnostics, classIds);
            try
            {
                var catalog = new RankedSkillCatalogV2(dto.schemaVersion, dto.contentVersion, skills, importedSynergies.Select(x => x.Definition));
                string normalized = BuildCanonical(dto, skills, importedSynergies, classIds, categoryIds, effectKinds.Keys);
                return new RankedSkillJsonImportResultV1(catalog, importedSynergies, classIds, diagnostics, SkillFingerprintV2.Hash(normalized));
            }
            catch (Exception ex) { Error(diagnostics, "$", string.Empty, "skill-catalog-build-failed", ex.Message); return Failure(diagnostics, classIds); }
        }

        public static SkillEffectSnapshotV2 ProjectEffects(RankedSkillJsonImportResultV1 import, RankedSkillAllocationSnapshotV2 allocation)
        {
            if (import == null || !import.Success) throw new ArgumentException("A successful import is required.", nameof(import));
            if (allocation == null) throw new ArgumentNullException(nameof(allocation));
            return new SkillEffectProjectorV2().Project(import.Catalog, allocation);
        }

        private static RankedSkillDefinitionV2 BuildSkill(SkillDto dto, int index, HashSet<string> classIds, HashSet<string> categoryIds, Dictionary<string, HashSet<SkillModifierKindV2>> effectKinds, HashSet<string> skillIds, List<SkillCatalogDiagnosticV1> diagnostics)
        {
            if (dto == null) { Error(diagnostics, "$.skills[" + index + "]", string.Empty, "skill-null", "Skill entry is null."); return null; }
            string path = "$.skills[" + index + "]";
            string id = Clean(dto.id);
            if (!categoryIds.Contains(Clean(dto.categoryId))) Error(diagnostics, path + ".categoryId", id, "skill-category-unknown", "Unknown category id: " + dto.categoryId);
            if (dto.maximumRank < 1 || dto.maximumRank > 99) Error(diagnostics, path + ".maximumRank", id, "skill-rank-invalid", "Maximum rank must be 1..99.");
            if (dto.pointCostPerRank != 1) Error(diagnostics, path + ".pointCostPerRank", id, "skill-point-cost-invalid", "Point cost per rank must currently equal one.");
            ValidateReferences(dto.eligibleClassIds, classIds, path + ".eligibleClassIds", id, "skill-class-unknown", diagnostics);
            decimal[] baseCurve = ExpandCurve(dto.rankValues, dto.maximumRank, path + ".rankValues", id, diagnostics);
            var overrides = new List<SkillClassOverrideV2>();
            var overrideClasses = new HashSet<string>(StringComparer.Ordinal);
            ClassOverrideDto[] overrideDtos = dto.classOverrides ?? Array.Empty<ClassOverrideDto>();
            for (int i = 0; i < overrideDtos.Length; i++)
            {
                ClassOverrideDto item = overrideDtos[i]; string itemPath = path + ".classOverrides[" + i + "]";
                if (item == null) { Error(diagnostics, itemPath, id, "skill-class-override-null", "Class override is null."); continue; }
                string classId = Clean(item.classId);
                if (!classIds.Contains(classId)) Error(diagnostics, itemPath + ".classId", id, "skill-class-unknown", "Unknown class id: " + item.classId);
                if (!overrideClasses.Add(classId)) Error(diagnostics, itemPath + ".classId", id, "skill-class-override-duplicate", "Duplicate class override.");
                decimal[] values = item.rankValues == null ? Array.Empty<decimal>() : ExpandCurve(item.rankValues, item.maximumRank, itemPath + ".rankValues", id, diagnostics);
                if (item.maximumRank > 0 && (values.Length == 0 || values.Length == item.maximumRank)) try { overrides.Add(new SkillClassOverrideV2(classId, item.maximumRank, values)); } catch (Exception ex) { Error(diagnostics, itemPath, id, "skill-class-override-invalid", ex.Message); }
            }
            var prerequisites = new List<SkillPrerequisiteV1>();
            PrerequisiteDto[] prerequisiteDtos = dto.prerequisites ?? Array.Empty<PrerequisiteDto>();
            for (int i = 0; i < prerequisiteDtos.Length; i++)
            {
                PrerequisiteDto item = prerequisiteDtos[i];
                if (item == null || !skillIds.Contains(Clean(item.skillId))) Error(diagnostics, path + ".prerequisites[" + i + "]", id, "skill-prerequisite-unknown", "Unknown prerequisite skill.");
                else if (item.requiredRank < 1) Error(diagnostics, path + ".prerequisites[" + i + "].requiredRank", id, "skill-prerequisite-rank-invalid", "Required rank must be positive.");
                else prerequisites.Add(new SkillPrerequisiteV1(Clean(item.skillId), item.requiredRank));
            }
            var gates = new List<SkillCategoryInvestmentRequirementV1>();
            CategoryGateDto[] gateDtos = dto.categoryGates ?? Array.Empty<CategoryGateDto>();
            for (int i = 0; i < gateDtos.Length; i++)
            {
                CategoryGateDto item = gateDtos[i];
                if (item == null || !categoryIds.Contains(Clean(item.categoryId))) Error(diagnostics, path + ".categoryGates[" + i + "]", id, "skill-category-gate-invalid", "Category gate references an unknown category.");
                else if (item.requiredPoints < 1) Error(diagnostics, path + ".categoryGates[" + i + "].requiredPoints", id, "skill-category-gate-points-invalid", "Required points must be positive.");
                else gates.Add(new SkillCategoryInvestmentRequirementV1(Clean(item.treeId), Clean(item.categoryId), item.requiredPoints));
            }
            var perRankEffects = BuildEffects(dto.perRankEffects, path + ".perRankEffects", id, effectKinds, true, diagnostics);
            var milestones = new List<SkillRankMilestoneV2>();
            int maximumEffective = Math.Max(dto.maximumRank, overrides.Count == 0 ? dto.maximumRank : overrides.Max(x => x.MaximumRank));
            MilestoneDto[] milestoneDtos = dto.milestones ?? Array.Empty<MilestoneDto>();
            for (int i = 0; i < milestoneDtos.Length; i++)
            {
                MilestoneDto item = milestoneDtos[i];
                if (item == null || item.rank < 1 || item.rank > maximumEffective) Error(diagnostics, path + ".milestones[" + i + "]", id, "skill-milestone-rank-invalid", "Milestone exceeds the effective maximum rank.");
                else { var effects = BuildEffects(item.effects, path + ".milestones[" + i + "].effects", id, effectKinds, false, diagnostics); if (effects.Count != 0) milestones.Add(new SkillRankMilestoneV2(item.rank, effects)); }
            }
            if (diagnostics.Any(x => x.DefinitionId == id && x.Severity == SkillCatalogDiagnosticSeverityV1.Error)) return null;
            try { return new RankedSkillDefinitionV2(id, Clean(dto.categoryId), dto.maximumRank, dto.eligibleClassIds, prerequisites, gates, overrides, baseCurve, perRankEffects, milestones); }
            catch (Exception ex) { Error(diagnostics, path, id, "skill-definition-invalid", ex.Message); return null; }
        }

        private static List<ImportedSkillSynergyV1> BuildSynergies(SynergyDto[] source, IReadOnlyList<RankedSkillDefinitionV2> skills, HashSet<string> classIds, Dictionary<string, HashSet<SkillModifierKindV2>> effectKinds, List<SkillCatalogDiagnosticV1> diagnostics)
        {
            var output = new List<ImportedSkillSynergyV1>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var skillMap = skills.ToDictionary(x => x.Id, StringComparer.Ordinal);
            SynergyDto[] items = source ?? Array.Empty<SynergyDto>();
            for (int i = 0; i < items.Length; i++)
            {
                SynergyDto dto = items[i]; string path = "$.synergies[" + i + "]";
                if (dto == null) { Error(diagnostics, path, string.Empty, "skill-synergy-null", "Synergy is null."); continue; }
                string id = Clean(dto.id);
                if (!ids.Add(id)) Error(diagnostics, path + ".id", id, "skill-synergy-duplicate", "Duplicate synergy id.");
                ValidateReferences(dto.eligibleClassIds, classIds, path + ".eligibleClassIds", id, "skill-class-unknown", diagnostics);
                var requirements = new List<SkillSynergyRequirementV2>();
                foreach (RequirementDto item in dto.requirements ?? Array.Empty<RequirementDto>())
                {
                    RankedSkillDefinitionV2 skill;
                    if (item == null || !skillMap.TryGetValue(Clean(item.skillId), out skill)) Error(diagnostics, path + ".requirements", id, "skill-synergy-requirement-unknown", "Unknown synergy skill.");
                    else if (item.minimumRank < 1 || item.minimumRank > MaxRank(skill)) Error(diagnostics, path + ".requirements", id, "skill-synergy-requirement-impossible", "Synergy minimum rank is impossible.");
                    else requirements.Add(new SkillSynergyRequirementV2(skill.Id, item.minimumRank));
                }
                var combinedAuthoring = new List<CombinedSkillRankRequirementV1>();
                var combinedDomain = new List<SkillCombinedRankRequirementV2>();
                foreach (CombinedRequirementDto item in dto.combinedRankRequirements ?? Array.Empty<CombinedRequirementDto>())
                {
                    if (item == null || item.skillIds == null || item.skillIds.Length < 2 || item.skillIds.Any(x => !skillMap.ContainsKey(Clean(x)))) Error(diagnostics, path + ".combinedRankRequirements", id, "skill-synergy-combined-invalid", "Combined-rank requirement references missing skills.");
                    else if (item.minimumCombinedRanks < 2 || item.minimumCombinedRanks > item.skillIds.Sum(x => MaxRank(skillMap[Clean(x)]))) Error(diagnostics, path + ".combinedRankRequirements", id, "skill-synergy-combined-impossible", "Combined-rank requirement is impossible.");
                    else
                    {
                        var authoring = new CombinedSkillRankRequirementV1(item.skillIds.Select(Clean), item.minimumCombinedRanks);
                        combinedAuthoring.Add(authoring);
                        combinedDomain.Add(new SkillCombinedRankRequirementV2(authoring.SkillIds, authoring.MinimumCombinedRanks));
                    }
                }
                var effects = BuildEffects(dto.effects, path + ".effects", id, effectKinds, false, diagnostics);
                foreach (SkillEffectDescriptorV2 effect in effects.Where(x => string.Equals(x.StatId, "reward.legendary_definition_weight_multiplier", StringComparison.Ordinal)))
                    if (effect.Kind != SkillModifierKindV2.Percentage || effect.Value < 0m || effect.Value > MaximumLegendaryRelativeBonus) Error(diagnostics, path + ".effects", id, "skill-legendary-relative-bonus-invalid", "Legendary definition weight bonus must be a relative Percentage from 0 through 0.20.");
                if (diagnostics.Any(x => x.DefinitionId == id && x.Severity == SkillCatalogDiagnosticSeverityV1.Error)) continue;
                try
                {
                    var definition = new SkillSynergyDefinitionV2(id, requirements, effects, combinedDomain);
                    output.Add(new ImportedSkillSynergyV1(definition, dto.displayName, dto.description, dto.eligibleClassIds, combinedAuthoring));
                }
                catch (Exception ex) { Error(diagnostics, path, id, "skill-synergy-invalid", ex.Message); }
            }
            return output;
        }

        private static List<SkillEffectDescriptorV2> BuildEffects(EffectDto[] source, string path, string definitionId, Dictionary<string, HashSet<SkillModifierKindV2>> registry, bool requireRankValueSource, List<SkillCatalogDiagnosticV1> diagnostics)
        {
            var output = new List<SkillEffectDescriptorV2>();
            EffectDto[] items = source ?? Array.Empty<EffectDto>();
            for (int i = 0; i < items.Length; i++)
            {
                EffectDto dto = items[i]; string itemPath = path + "[" + i + "]";
                if (dto == null) { Error(diagnostics, itemPath, definitionId, "skill-effect-null", "Effect is null."); continue; }
                SkillModifierKindV2 kind;
                if (!Enum.TryParse(dto.kind, false, out kind)) { Error(diagnostics, itemPath + ".kind", definitionId, "skill-effect-kind-unknown", "Unknown modifier kind: " + dto.kind); continue; }
                HashSet<SkillModifierKindV2> allowed;
                if (!registry.TryGetValue(Clean(dto.statId), out allowed)) { Error(diagnostics, itemPath + ".statId", definitionId, "skill-effect-id-unknown", "Unknown effect id: " + dto.statId); continue; }
                if (!allowed.Contains(kind)) { Error(diagnostics, itemPath + ".kind", definitionId, "skill-effect-kind-disallowed", "Modifier kind is not allowed for this effect id."); continue; }
                if (requireRankValueSource && !string.Equals(dto.valueSource, "rankValue", StringComparison.Ordinal)) { Error(diagnostics, itemPath + ".valueSource", definitionId, "skill-effect-rank-source-required", "Per-rank effects must use rankValue."); continue; }
                decimal value = requireRankValueSource ? 1m : Convert.ToDecimal(dto.value, CultureInfo.InvariantCulture);
                try { output.Add(new SkillEffectDescriptorV2(Clean(dto.statId), kind, value, Clean(dto.conditionId))); }
                catch (Exception ex) { Error(diagnostics, itemPath, definitionId, "skill-effect-invalid", ex.Message); }
            }
            return output;
        }

        private static decimal[] ExpandCurve(RankCurveDto dto, int expectedCount, string path, string id, List<SkillCatalogDiagnosticV1> diagnostics)
        {
            if (dto == null || dto.count != expectedCount) { Error(diagnostics, path, id, "skill-rank-curve-count-invalid", "Rank curve count must equal maximumRank."); return Array.Empty<decimal>(); }
            decimal start = Convert.ToDecimal(dto.start, CultureInfo.InvariantCulture);
            decimal step = Convert.ToDecimal(dto.step, CultureInfo.InvariantCulture);
            return Enumerable.Range(0, dto.count).Select(x => start + step * x).ToArray();
        }

        private static Dictionary<string, HashSet<SkillModifierKindV2>> BuildEffectRegistry(EffectRegistryDto[] source, List<SkillCatalogDiagnosticV1> diagnostics)
        {
            var output = new Dictionary<string, HashSet<SkillModifierKindV2>>(StringComparer.Ordinal);
            EffectRegistryDto[] items = source ?? Array.Empty<EffectRegistryDto>();
            for (int i = 0; i < items.Length; i++)
            {
                EffectRegistryDto dto = items[i]; string id = Clean(dto?.id);
                if (string.IsNullOrEmpty(id) || output.ContainsKey(id)) { Error(diagnostics, "$.effects[" + i + "].id", id, "skill-effect-registry-invalid", "Effect ids must be non-empty and unique."); continue; }
                var kinds = new HashSet<SkillModifierKindV2>();
                foreach (string value in dto.allowedKinds ?? Array.Empty<string>()) { SkillModifierKindV2 kind; if (Enum.TryParse(value, false, out kind)) kinds.Add(kind); else Error(diagnostics, "$.effects[" + i + "].allowedKinds", id, "skill-effect-kind-unknown", "Unknown modifier kind: " + value); }
                if (kinds.Count == 0) Error(diagnostics, "$.effects[" + i + "].allowedKinds", id, "skill-effect-kind-missing", "At least one allowed kind is required.");
                output.Add(id, kinds);
            }
            return output;
        }

        private static HashSet<string> UniqueIds(NamedIdDto[] source, string path, string kind, List<SkillCatalogDiagnosticV1> diagnostics)
        {
            var output = new HashSet<string>(StringComparer.Ordinal); NamedIdDto[] items = source ?? Array.Empty<NamedIdDto>();
            for (int i = 0; i < items.Length; i++) { string id = Clean(items[i]?.id); if (string.IsNullOrEmpty(id) || !output.Add(id)) Error(diagnostics, path + "[" + i + "].id", id, "skill-" + kind + "-id-invalid", kind + " ids must be non-empty and unique."); }
            return output;
        }

        private static void ValidateReferences(string[] source, HashSet<string> known, string path, string id, string code, List<SkillCatalogDiagnosticV1> diagnostics)
        { foreach (string value in source ?? Array.Empty<string>()) if (!known.Contains(Clean(value))) Error(diagnostics, path, id, code, "Unknown reference: " + value); }

        private static string BuildCanonical(CatalogDto dto, IEnumerable<RankedSkillDefinitionV2> skills, IEnumerable<ImportedSkillSynergyV1> synergies, IEnumerable<string> classes, IEnumerable<string> categories, IEnumerable<string> effects)
        {
            var lines = new List<string> { dto.schemaVersion ?? string.Empty, dto.contentVersion ?? string.Empty, dto.status ?? string.Empty, "classes=" + string.Join(",", classes.OrderBy(x => x, StringComparer.Ordinal)), "categories=" + string.Join(",", categories.OrderBy(x => x, StringComparer.Ordinal)), "effects=" + string.Join(",", effects.OrderBy(x => x, StringComparer.Ordinal)) };
            lines.AddRange(skills.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => "skill=" + x.Id + "|" + x.CategoryId + "|" + x.MaximumRank + "|" + string.Join(",", x.RankValues.Select(v => v.ToString(CultureInfo.InvariantCulture)))));
            lines.AddRange(synergies.OrderBy(x => x.Definition.Id, StringComparer.Ordinal).Select(x => "synergy=" + x.Definition.Canonical));
            return string.Join("\n", lines);
        }

        private static int MaxRank(RankedSkillDefinitionV2 skill) => Math.Max(skill.MaximumRank, skill.ClassOverrides.Count == 0 ? skill.MaximumRank : skill.ClassOverrides.Max(x => x.MaximumRank));
        private static string Clean(string value) => (value ?? string.Empty).Trim();
        private static void Error(List<SkillCatalogDiagnosticV1> list, string path, string id, string code, string message) => list.Add(new SkillCatalogDiagnosticV1(path, id, code, message, SkillCatalogDiagnosticSeverityV1.Error));
        private static RankedSkillJsonImportResultV1 Failure(IEnumerable<SkillCatalogDiagnosticV1> diagnostics, IEnumerable<string> classIds = null) => new RankedSkillJsonImportResultV1(null, null, classIds, diagnostics, string.Empty);

        [Serializable] private sealed class CatalogDto { public string schemaVersion; public string contentVersion; public string status; public NamedIdDto[] classes; public NamedIdDto[] categories; public EffectRegistryDto[] effects; public SkillDto[] skills; public SynergyDto[] synergies; }
        [Serializable] private sealed class NamedIdDto { public string id; public string displayName; }
        [Serializable] private sealed class EffectRegistryDto { public string id; public string[] allowedKinds; }
        [Serializable] private sealed class RankCurveDto { public double start; public double step; public int count; }
        [Serializable] private sealed class SkillDto { public string id; public string categoryId; public string displayName; public string shortDescription; public string longDescription; public int maximumRank; public int pointCostPerRank; public string[] eligibleClassIds; public RankCurveDto rankValues; public ClassOverrideDto[] classOverrides; public PrerequisiteDto[] prerequisites; public CategoryGateDto[] categoryGates; public EffectDto[] perRankEffects; public MilestoneDto[] milestones; public string designStatus; public string balanceNotes; }
        [Serializable] private sealed class ClassOverrideDto { public string classId; public int maximumRank; public RankCurveDto rankValues; }
        [Serializable] private sealed class PrerequisiteDto { public string skillId; public int requiredRank; }
        [Serializable] private sealed class CategoryGateDto { public string treeId; public string categoryId; public int requiredPoints; }
        [Serializable] private sealed class EffectDto { public string statId; public string kind; public double value; public string valueSource; public string conditionId; }
        [Serializable] private sealed class MilestoneDto { public int rank; public EffectDto[] effects; }
        [Serializable] private sealed class SynergyDto { public string id; public string displayName; public string description; public string[] eligibleClassIds; public RequirementDto[] requirements; public CombinedRequirementDto[] combinedRankRequirements; public EffectDto[] effects; public string designStatus; public string balanceNotes; }
        [Serializable] private sealed class RequirementDto { public string skillId; public int minimumRank; }
        [Serializable] private sealed class CombinedRequirementDto { public string[] skillIds; public int minimumCombinedRanks; }
    }
}

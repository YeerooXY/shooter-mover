using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.Domain.Progression.Skills
{
    public enum SkillModifierKindV2 { Flat, Percentage, Multiplicative, IntegerCapacity }

    public sealed class SkillEffectDescriptorV2
    {
        public SkillEffectDescriptorV2(string statId, SkillModifierKindV2 kind, decimal value, string conditionId = "")
        {
            if (string.IsNullOrWhiteSpace(statId)) throw new ArgumentException("Stat id is required.", nameof(statId));
            if (kind == SkillModifierKindV2.Multiplicative && value <= 0m) throw new ArgumentOutOfRangeException(nameof(value));
            StatId = statId.Trim(); Kind = kind; Value = value; ConditionId = (conditionId ?? string.Empty).Trim();
        }
        public string StatId { get; }
        public SkillModifierKindV2 Kind { get; }
        public decimal Value { get; }
        public string ConditionId { get; }
        public string Canonical => StatId + ":" + Kind + ":" + Value.ToString(CultureInfo.InvariantCulture) + ":" + ConditionId;
    }

    public sealed class SkillRankMilestoneV2
    {
        public SkillRankMilestoneV2(int rank, IEnumerable<SkillEffectDescriptorV2> effects)
        {
            if (rank < 1) throw new ArgumentOutOfRangeException(nameof(rank));
            Rank = rank; Effects = Freeze(effects, nameof(effects));
        }
        public int Rank { get; }
        public IReadOnlyList<SkillEffectDescriptorV2> Effects { get; }
        private static IReadOnlyList<SkillEffectDescriptorV2> Freeze(IEnumerable<SkillEffectDescriptorV2> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var list = source.ToList();
            if (list.Any(x => x == null)) throw new ArgumentException("Effects must be non-null.", name);
            return new ReadOnlyCollection<SkillEffectDescriptorV2>(list);
        }
    }

    public sealed class SkillClassOverrideV2
    {
        public SkillClassOverrideV2(string classId, int maximumRank, IEnumerable<decimal> rankValues)
        {
            if (string.IsNullOrWhiteSpace(classId)) throw new ArgumentException("Class id is required.", nameof(classId));
            if (maximumRank < 1) throw new ArgumentOutOfRangeException(nameof(maximumRank));
            var values = rankValues == null ? new List<decimal>() : rankValues.ToList();
            if (values.Count != 0 && values.Count != maximumRank) throw new ArgumentException("Class value curve must contain exactly one value per rank.", nameof(rankValues));
            ClassId = classId.Trim(); MaximumRank = maximumRank; RankValues = new ReadOnlyCollection<decimal>(values);
        }
        public string ClassId { get; }
        public int MaximumRank { get; }
        public IReadOnlyList<decimal> RankValues { get; }
    }

    public sealed class RankedSkillDefinitionV2
    {
        public RankedSkillDefinitionV2(string id, string categoryId, int maximumRank, IEnumerable<string> eligibleClassIds,
            IEnumerable<SkillPrerequisiteV1> prerequisites, IEnumerable<SkillCategoryInvestmentRequirementV1> categoryGates,
            IEnumerable<SkillClassOverrideV2> classOverrides, IEnumerable<decimal> rankValues,
            IEnumerable<SkillEffectDescriptorV2> perRankEffects, IEnumerable<SkillRankMilestoneV2> milestones)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Skill id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category id is required.", nameof(categoryId));
            if (maximumRank < 1 || maximumRank > 99) throw new ArgumentOutOfRangeException(nameof(maximumRank));
            Id = id.Trim(); CategoryId = categoryId.Trim(); MaximumRank = maximumRank;
            EligibleClassIds = FreezeStrings(eligibleClassIds);
            Prerequisites = new ReadOnlyCollection<SkillPrerequisiteV1>((prerequisites ?? Array.Empty<SkillPrerequisiteV1>()).ToList());
            CategoryGates = new ReadOnlyCollection<SkillCategoryInvestmentRequirementV1>((categoryGates ?? Array.Empty<SkillCategoryInvestmentRequirementV1>()).ToList());
            ClassOverrides = new ReadOnlyCollection<SkillClassOverrideV2>((classOverrides ?? Array.Empty<SkillClassOverrideV2>()).ToList());
            RankValues = new ReadOnlyCollection<decimal>((rankValues ?? Array.Empty<decimal>()).ToList());
            if (RankValues.Count != 0 && RankValues.Count != maximumRank) throw new ArgumentException("Base value curve must contain exactly one value per rank.", nameof(rankValues));
            PerRankEffects = new ReadOnlyCollection<SkillEffectDescriptorV2>((perRankEffects ?? Array.Empty<SkillEffectDescriptorV2>()).ToList());
            Milestones = new ReadOnlyCollection<SkillRankMilestoneV2>((milestones ?? Array.Empty<SkillRankMilestoneV2>()).OrderBy(x => x.Rank).ToList());
        }
        public string Id { get; }
        public string CategoryId { get; }
        public int MaximumRank { get; }
        public IReadOnlyList<string> EligibleClassIds { get; }
        public IReadOnlyList<SkillPrerequisiteV1> Prerequisites { get; }
        public IReadOnlyList<SkillCategoryInvestmentRequirementV1> CategoryGates { get; }
        public IReadOnlyList<SkillClassOverrideV2> ClassOverrides { get; }
        public IReadOnlyList<decimal> RankValues { get; }
        public IReadOnlyList<SkillEffectDescriptorV2> PerRankEffects { get; }
        public IReadOnlyList<SkillRankMilestoneV2> Milestones { get; }
        public bool IsEligible(string classId) => EligibleClassIds.Count == 0 || EligibleClassIds.Contains(classId, StringComparer.Ordinal);
        public int EffectiveMaximumRank(string classId)
        {
            var item = ClassOverrides.FirstOrDefault(x => string.Equals(x.ClassId, classId, StringComparison.Ordinal));
            return item == null ? MaximumRank : item.MaximumRank;
        }
        public decimal RankValue(string classId, int rank)
        {
            if (rank < 1 || rank > EffectiveMaximumRank(classId)) throw new ArgumentOutOfRangeException(nameof(rank));
            var item = ClassOverrides.FirstOrDefault(x => string.Equals(x.ClassId, classId, StringComparison.Ordinal));
            if (item != null && item.RankValues.Count != 0) return item.RankValues[rank - 1];
            return RankValues.Count == 0 ? rank : RankValues[Math.Min(rank, RankValues.Count) - 1];
        }
        private static IReadOnlyList<string> FreezeStrings(IEnumerable<string> source)
        {
            var list = (source ?? Array.Empty<string>()).Select(x => (x ?? string.Empty).Trim()).ToList();
            if (list.Any(string.IsNullOrWhiteSpace) || list.Distinct(StringComparer.Ordinal).Count() != list.Count) throw new ArgumentException("Class ids must be non-empty and unique.", nameof(source));
            return new ReadOnlyCollection<string>(list);
        }
    }

    public sealed class SkillSynergyRequirementV2
    {
        public SkillSynergyRequirementV2(string skillId, int minimumRank)
        {
            if (string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("Skill id is required.", nameof(skillId));
            if (minimumRank < 1) throw new ArgumentOutOfRangeException(nameof(minimumRank));
            SkillId = skillId.Trim(); MinimumRank = minimumRank;
        }
        public string SkillId { get; }
        public int MinimumRank { get; }
    }

    public sealed class SkillCombinedRankRequirementV2
    {
        public SkillCombinedRankRequirementV2(IEnumerable<string> skillIds, int minimumCombinedRank)
        {
            var ids = (skillIds ?? throw new ArgumentNullException(nameof(skillIds))).Select(x => (x ?? string.Empty).Trim()).ToList();
            if (ids.Count < 2 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                throw new ArgumentException("Combined-rank requirements need at least two unique skill ids.", nameof(skillIds));
            if (minimumCombinedRank < 1) throw new ArgumentOutOfRangeException(nameof(minimumCombinedRank));
            SkillIds = new ReadOnlyCollection<string>(ids);
            MinimumCombinedRank = minimumCombinedRank;
        }
        public IReadOnlyList<string> SkillIds { get; }
        public int MinimumCombinedRank { get; }
        public int CurrentCombinedRank(RankedSkillAllocationSnapshotV2 allocation) => SkillIds.Sum(allocation.RankOf);
        public bool IsSatisfied(RankedSkillAllocationSnapshotV2 allocation) => CurrentCombinedRank(allocation) >= MinimumCombinedRank;
        public string Canonical => string.Join(",", SkillIds.OrderBy(x => x, StringComparer.Ordinal)) + ">=" + MinimumCombinedRank;
    }

    public sealed class SkillSynergyDefinitionV2
    {
        public SkillSynergyDefinitionV2(string id, IEnumerable<SkillSynergyRequirementV2> requirements, IEnumerable<SkillEffectDescriptorV2> effects,
            IEnumerable<SkillCombinedRankRequirementV2> combinedRankRequirements = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Synergy id is required.", nameof(id));
            Id = id.Trim();
            Requirements = new ReadOnlyCollection<SkillSynergyRequirementV2>((requirements ?? throw new ArgumentNullException(nameof(requirements))).ToList());
            CombinedRankRequirements = new ReadOnlyCollection<SkillCombinedRankRequirementV2>((combinedRankRequirements ?? Array.Empty<SkillCombinedRankRequirementV2>()).ToList());
            Effects = new ReadOnlyCollection<SkillEffectDescriptorV2>((effects ?? throw new ArgumentNullException(nameof(effects))).ToList());
            if (Requirements.Count + CombinedRankRequirements.Count < 1 || Effects.Count < 1) throw new ArgumentException("A synergy requires at least one requirement and one effect.");
            if (Requirements.Any(x => x == null) || CombinedRankRequirements.Any(x => x == null) || Effects.Any(x => x == null)) throw new ArgumentException("Synergy entries must be non-null.");
        }
        public string Id { get; }
        public IReadOnlyList<SkillSynergyRequirementV2> Requirements { get; }
        public IReadOnlyList<SkillCombinedRankRequirementV2> CombinedRankRequirements { get; }
        public IReadOnlyList<SkillEffectDescriptorV2> Effects { get; }
        public bool IsSatisfied(RankedSkillAllocationSnapshotV2 allocation) =>
            Requirements.All(r => allocation.RankOf(r.SkillId) >= r.MinimumRank) && CombinedRankRequirements.All(r => r.IsSatisfied(allocation));
        public string Canonical => Id + "|" + string.Join(";", Requirements.OrderBy(x => x.SkillId, StringComparer.Ordinal).Select(x => x.SkillId + ">=" + x.MinimumRank)) + "|" + string.Join(";", CombinedRankRequirements.Select(x => x.Canonical));
    }

    public sealed class RankedSkillCatalogV2
    {
        private readonly IReadOnlyDictionary<string, RankedSkillDefinitionV2> byId;
        public RankedSkillCatalogV2(string schemaVersion, string contentVersion, IEnumerable<RankedSkillDefinitionV2> skills, IEnumerable<SkillSynergyDefinitionV2> synergies)
        {
            if (string.IsNullOrWhiteSpace(schemaVersion) || string.IsNullOrWhiteSpace(contentVersion)) throw new ArgumentException("Versions are required.");
            SchemaVersion = schemaVersion.Trim(); ContentVersion = contentVersion.Trim();
            var list = (skills ?? throw new ArgumentNullException(nameof(skills))).ToList();
            var synergyList = (synergies ?? Array.Empty<SkillSynergyDefinitionV2>()).ToList();
            Validate(list, synergyList);
            Skills = new ReadOnlyCollection<RankedSkillDefinitionV2>(list.OrderBy(x => x.Id, StringComparer.Ordinal).ToList());
            Synergies = new ReadOnlyCollection<SkillSynergyDefinitionV2>(synergyList.OrderBy(x => x.Id, StringComparer.Ordinal).ToList());
            byId = new ReadOnlyDictionary<string, RankedSkillDefinitionV2>(Skills.ToDictionary(x => x.Id, StringComparer.Ordinal));
            Fingerprint = SkillFingerprintV2.Hash(ToCanonicalString());
        }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<RankedSkillDefinitionV2> Skills { get; }
        public IReadOnlyList<SkillSynergyDefinitionV2> Synergies { get; }
        public string Fingerprint { get; }
        public bool TryGet(string id, out RankedSkillDefinitionV2 skill) => byId.TryGetValue(id ?? string.Empty, out skill);
        public string ToCanonicalString() => SchemaVersion + "|" + ContentVersion + "|" + string.Join(";", Skills.Select(x => x.Id + ":" + x.MaximumRank)) + "|" + string.Join(";", Synergies.Select(x => x.Canonical));
        private static void Validate(IReadOnlyList<RankedSkillDefinitionV2> skills, IReadOnlyList<SkillSynergyDefinitionV2> synergies)
        {
            if (skills.Count == 0 || skills.Any(x => x == null)) throw new ArgumentException("At least one non-null skill is required.");
            if (skills.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != skills.Count) throw new ArgumentException("Duplicate skill ids.");
            var map = skills.ToDictionary(x => x.Id, StringComparer.Ordinal);
            Func<RankedSkillDefinitionV2, int> maxRank = s => Math.Max(s.MaximumRank, s.ClassOverrides.Count == 0 ? s.MaximumRank : s.ClassOverrides.Max(x => x.MaximumRank));
            foreach (var s in skills)
            {
                if (s.ClassOverrides.Select(x => x.ClassId).Distinct(StringComparer.Ordinal).Count() != s.ClassOverrides.Count) throw new ArgumentException("Duplicate class override.");
                if (s.Milestones.Any(x => x.Rank > maxRank(s))) throw new ArgumentException("Milestone exceeds effective maximum.");
                foreach (var p in s.Prerequisites) if (!map.ContainsKey(p.SkillId)) throw new ArgumentException("Missing prerequisite: " + p.SkillId);
            }
            var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
            Func<string, bool> cycle = null;
            cycle = id => { if (visiting.Contains(id)) return true; if (visited.Contains(id)) return false; visiting.Add(id); foreach (var p in map[id].Prerequisites) if (cycle(p.SkillId)) return true; visiting.Remove(id); visited.Add(id); return false; };
            foreach (var id in map.Keys) if (cycle(id)) throw new ArgumentException("Circular prerequisites.");
            if (synergies.Any(x => x == null) || synergies.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != synergies.Count) throw new ArgumentException("Invalid synergy ids.");
            foreach (var synergy in synergies)
            {
                foreach (var requirement in synergy.Requirements)
                {
                    RankedSkillDefinitionV2 skill;
                    if (!map.TryGetValue(requirement.SkillId, out skill) || requirement.MinimumRank > maxRank(skill)) throw new ArgumentException("Unsatisfiable synergy requirement.");
                }
                foreach (var combined in synergy.CombinedRankRequirements)
                {
                    int possible = 0;
                    foreach (string skillId in combined.SkillIds)
                    {
                        RankedSkillDefinitionV2 skill;
                        if (!map.TryGetValue(skillId, out skill)) throw new ArgumentException("Missing combined-rank skill: " + skillId);
                        possible = checked(possible + maxRank(skill));
                    }
                    if (combined.MinimumCombinedRank > possible) throw new ArgumentException("Unsatisfiable combined-rank synergy requirement.");
                }
            }
        }
    }

    public static class SkillFingerprintV2
    {
        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            { var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)); return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant(); }
        }
    }

    public sealed class RankedSkillAllocationSnapshotV2
    {
        public RankedSkillAllocationSnapshotV2(string profileId, string classId, long version, string schemaVersion, string contentVersion, IDictionary<string, int> ranks)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(classId)) throw new ArgumentException("Profile and class identities are required.");
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            ProfileId = profileId.Trim(); ClassId = classId.Trim(); Version = version; SchemaVersion = schemaVersion ?? string.Empty; ContentVersion = contentVersion ?? string.Empty;
            var copy = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in ranks ?? new Dictionary<string, int>()) { if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1) throw new ArgumentException("Ranks must be positive."); copy.Add(pair.Key.Trim(), pair.Value); }
            Ranks = new ReadOnlyDictionary<string, int>(copy); AllocatedPoints = copy.Values.Sum(); Fingerprint = SkillFingerprintV2.Hash(ToCanonicalString());
        }
        public string ProfileId { get; }
        public string ClassId { get; }
        public long Version { get; }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public IReadOnlyDictionary<string, int> Ranks { get; }
        public int AllocatedPoints { get; }
        public string Fingerprint { get; }
        public int RankOf(string id) { int value; return Ranks.TryGetValue(id ?? string.Empty, out value) ? value : 0; }
        public string ToCanonicalString() => ProfileId + "|" + ClassId + "|" + Version + "|" + SchemaVersion + "|" + ContentVersion + "|" + string.Join(";", Ranks.Select(x => x.Key + "=" + x.Value));
        public static RankedSkillAllocationSnapshotV2 Empty(string profileId, string classId, RankedSkillCatalogV2 catalog) => new RankedSkillAllocationSnapshotV2(profileId, classId, 0, catalog.SchemaVersion, catalog.ContentVersion, null);
    }

    public sealed class SkillEffectContributionV2
    {
        public SkillEffectContributionV2(string sourceId, SkillEffectDescriptorV2 effect) { SourceId = sourceId; Effect = effect; }
        public string SourceId { get; }
        public SkillEffectDescriptorV2 Effect { get; }
    }

    public sealed class SkillEffectSnapshotV2
    {
        public SkillEffectSnapshotV2(RankedSkillAllocationSnapshotV2 allocation, IEnumerable<SkillEffectContributionV2> contributions)
        {
            AllocationFingerprint = allocation.Fingerprint; Contributions = new ReadOnlyCollection<SkillEffectContributionV2>(contributions.OrderBy(x => x.Effect.StatId).ThenBy(x => x.Effect.Kind).ThenBy(x => x.SourceId, StringComparer.Ordinal).ToList());
            Fingerprint = SkillFingerprintV2.Hash(AllocationFingerprint + "|" + string.Join(";", Contributions.Select(x => x.SourceId + ":" + x.Effect.Canonical)));
        }
        public string AllocationFingerprint { get; }
        public IReadOnlyList<SkillEffectContributionV2> Contributions { get; }
        public string Fingerprint { get; }
        public decimal Apply(string statId, decimal baseValue)
        {
            var items = Contributions.Where(x => string.Equals(x.Effect.StatId, statId, StringComparison.Ordinal) && string.IsNullOrEmpty(x.Effect.ConditionId)).Select(x => x.Effect).ToList();
            decimal flat = items.Where(x => x.Kind == SkillModifierKindV2.Flat || x.Kind == SkillModifierKindV2.IntegerCapacity).Sum(x => x.Value);
            decimal pct = items.Where(x => x.Kind == SkillModifierKindV2.Percentage).Sum(x => x.Value);
            decimal mult = items.Where(x => x.Kind == SkillModifierKindV2.Multiplicative).Aggregate(1m, (a, x) => a * x.Value);
            return (baseValue + flat) * (1m + pct) * mult;
        }
    }

    public sealed class SkillEffectProjectorV2
    {
        public SkillEffectSnapshotV2 Project(RankedSkillCatalogV2 catalog, RankedSkillAllocationSnapshotV2 allocation)
        {
            var output = new List<SkillEffectContributionV2>();
            foreach (var pair in allocation.Ranks)
            {
                RankedSkillDefinitionV2 skill; if (!catalog.TryGet(pair.Key, out skill)) continue;
                for (int rank = 1; rank <= pair.Value; rank++) foreach (var effect in skill.PerRankEffects)
                    output.Add(new SkillEffectContributionV2(skill.Id + "#" + rank, new SkillEffectDescriptorV2(effect.StatId, effect.Kind, effect.Value * skill.RankValue(allocation.ClassId, rank), effect.ConditionId)));
                foreach (var milestone in skill.Milestones.Where(x => x.Rank <= pair.Value)) foreach (var effect in milestone.Effects) output.Add(new SkillEffectContributionV2(skill.Id + "@" + milestone.Rank, effect));
            }
            foreach (var synergy in catalog.Synergies.Where(x => x.IsSatisfied(allocation))) foreach (var effect in synergy.Effects) output.Add(new SkillEffectContributionV2(synergy.Id, effect));
            return new SkillEffectSnapshotV2(allocation, output);
        }
    }

    public sealed class RankedSkillSampleCatalogV2
    {
        public static RankedSkillCatalogV2 Create()
        {
            Func<decimal, decimal[]> fifteen = step => Enumerable.Range(1, 15).Select(x => x * step).ToArray();
            var armor = new RankedSkillDefinitionV2("generic.armor", "defense", 6, null, null, null,
                new[] { new SkillClassOverrideV2("juggernaut", 18, Enumerable.Range(1, 18).Select(x => x * 0.01m)) }, Enumerable.Range(1, 6).Select(x => x * 0.01m),
                new[] { new SkillEffectDescriptorV2("character.armor", SkillModifierKindV2.Percentage, 1m) }, null);
            var speed = new RankedSkillDefinitionV2("generic.movement_speed", "mobility", 18, null, null, null,
                new[] { new SkillClassOverrideV2("combat_medic", 6, Enumerable.Range(1, 6).Select(x => x * 0.01m)), new SkillClassOverrideV2("juggernaut", 9, Enumerable.Range(1, 9).Select(x => x * 0.01m)) }, Enumerable.Range(1, 18).Select(x => x * 0.01m),
                new[] { new SkillEffectDescriptorV2("movement.speed", SkillModifierKindV2.Percentage, 1m) }, null);
            var recovery = new RankedSkillDefinitionV2("striker.thruster_recovery", "mobility", 15, new[] { "striker" }, null, null, null, fifteen(0.01m), new[] { new SkillEffectDescriptorV2("movement.thruster_recovery", SkillModifierKindV2.Percentage, 1m) }, new[] { new SkillRankMilestoneV2(5, new[] { new SkillEffectDescriptorV2("movement.recovery_delay", SkillModifierKindV2.Flat, -0.1m) }) });
            var efficiency = new RankedSkillDefinitionV2("striker.movement_efficiency", "mobility", 15, new[] { "striker" }, new[] { new SkillPrerequisiteV1("generic.movement_speed", 3) }, null, null, fifteen(0.01m), new[] { new SkillEffectDescriptorV2("movement.energy_efficiency", SkillModifierKindV2.Percentage, 1m) }, null);
            var synergy = new SkillSynergyDefinitionV2("striker.third_movement_charge", new[] { new SkillSynergyRequirementV2(recovery.Id, 8), new SkillSynergyRequirementV2(efficiency.Id, 8) }, new[] { new SkillEffectDescriptorV2("movement.maximum_charges", SkillModifierKindV2.IntegerCapacity, 1m) });
            return new RankedSkillCatalogV2("skills.schema.v2", "fixture.003", new[] { armor, speed, recovery, efficiency }, new[] { synergy });
        }
    }
}

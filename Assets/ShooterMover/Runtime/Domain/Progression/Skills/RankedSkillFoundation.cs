using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.Domain.Progression.Skills
{
    public enum SkillModifierKind { Flat, Percentage, Multiplicative, IntegerCapacity }

    public sealed class SkillEffectDescriptor
    {
        public SkillEffectDescriptor(string statId, SkillModifierKind kind, decimal value, string conditionId = "")
        {
            if (string.IsNullOrWhiteSpace(statId)) throw new ArgumentException("Stat id is required.", nameof(statId));
            if (kind == SkillModifierKind.Multiplicative && value <= 0m) throw new ArgumentOutOfRangeException(nameof(value));
            StatId = statId.Trim(); Kind = kind; Value = value; ConditionId = (conditionId ?? string.Empty).Trim();
        }
        public string StatId { get; }
        public SkillModifierKind Kind { get; }
        public decimal Value { get; }
        public string ConditionId { get; }
        public string Canonical => StatId + ":" + Kind + ":" + Value.ToString(CultureInfo.InvariantCulture) + ":" + ConditionId;
    }

    public sealed class SkillRankMilestone
    {
        public SkillRankMilestone(int rank, IEnumerable<SkillEffectDescriptor> effects)
        {
            if (rank < 1) throw new ArgumentOutOfRangeException(nameof(rank));
            Rank = rank; Effects = Freeze(effects, nameof(effects));
        }
        public int Rank { get; }
        public IReadOnlyList<SkillEffectDescriptor> Effects { get; }
        private static IReadOnlyList<SkillEffectDescriptor> Freeze(IEnumerable<SkillEffectDescriptor> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var list = source.ToList(); if (list.Any(x => x == null)) throw new ArgumentException("Effects must be non-null.", name);
            return new ReadOnlyCollection<SkillEffectDescriptor>(list);
        }
    }

    public sealed class SkillClassOverride
    {
        public SkillClassOverride(string classId, int maximumRank, IEnumerable<decimal> rankValues)
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

    public sealed class RankedSkillDefinition
    {
        public RankedSkillDefinition(string id, string categoryId, int maximumRank, IEnumerable<string> eligibleClassIds,
            IEnumerable<SkillPrerequisite> prerequisites, IEnumerable<SkillCategoryInvestmentRequirement> categoryGates,
            IEnumerable<SkillClassOverride> classOverrides, IEnumerable<decimal> rankValues,
            IEnumerable<SkillEffectDescriptor> perRankEffects, IEnumerable<SkillRankMilestone> milestones)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Skill id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category id is required.", nameof(categoryId));
            if (maximumRank < 1 || maximumRank > 99) throw new ArgumentOutOfRangeException(nameof(maximumRank));
            Id = id.Trim(); CategoryId = categoryId.Trim(); MaximumRank = maximumRank;
            EligibleClassIds = FreezeStrings(eligibleClassIds);
            Prerequisites = new ReadOnlyCollection<SkillPrerequisite>((prerequisites ?? Array.Empty<SkillPrerequisite>()).ToList());
            CategoryGates = new ReadOnlyCollection<SkillCategoryInvestmentRequirement>((categoryGates ?? Array.Empty<SkillCategoryInvestmentRequirement>()).ToList());
            ClassOverrides = new ReadOnlyCollection<SkillClassOverride>((classOverrides ?? Array.Empty<SkillClassOverride>()).ToList());
            RankValues = new ReadOnlyCollection<decimal>((rankValues ?? Array.Empty<decimal>()).ToList());
            if (RankValues.Count != 0 && RankValues.Count != maximumRank) throw new ArgumentException("Base value curve must contain exactly one value per rank.", nameof(rankValues));
            PerRankEffects = new ReadOnlyCollection<SkillEffectDescriptor>((perRankEffects ?? Array.Empty<SkillEffectDescriptor>()).ToList());
            Milestones = new ReadOnlyCollection<SkillRankMilestone>((milestones ?? Array.Empty<SkillRankMilestone>()).OrderBy(x => x.Rank).ToList());
        }
        public string Id { get; }
        public string CategoryId { get; }
        public int MaximumRank { get; }
        public IReadOnlyList<string> EligibleClassIds { get; }
        public IReadOnlyList<SkillPrerequisite> Prerequisites { get; }
        public IReadOnlyList<SkillCategoryInvestmentRequirement> CategoryGates { get; }
        public IReadOnlyList<SkillClassOverride> ClassOverrides { get; }
        public IReadOnlyList<decimal> RankValues { get; }
        public IReadOnlyList<SkillEffectDescriptor> PerRankEffects { get; }
        public IReadOnlyList<SkillRankMilestone> Milestones { get; }
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

    public sealed class SkillSynergyRequirement
    {
        public SkillSynergyRequirement(string skillId, int minimumRank)
        { if (string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("Skill id is required.", nameof(skillId)); if (minimumRank < 1) throw new ArgumentOutOfRangeException(nameof(minimumRank)); SkillId = skillId.Trim(); MinimumRank = minimumRank; }
        public string SkillId { get; }
        public int MinimumRank { get; }
    }

    public sealed class SkillSynergyDefinition
    {
        public SkillSynergyDefinition(string id, IEnumerable<SkillSynergyRequirement> requirements, IEnumerable<SkillEffectDescriptor> effects)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Synergy id is required.", nameof(id));
            Id = id.Trim(); Requirements = new ReadOnlyCollection<SkillSynergyRequirement>((requirements ?? throw new ArgumentNullException(nameof(requirements))).ToList());
            Effects = new ReadOnlyCollection<SkillEffectDescriptor>((effects ?? throw new ArgumentNullException(nameof(effects))).ToList());
            if (Requirements.Count < 2 || Effects.Count < 1) throw new ArgumentException("A synergy requires at least two skills and one effect.");
        }
        public string Id { get; }
        public IReadOnlyList<SkillSynergyRequirement> Requirements { get; }
        public IReadOnlyList<SkillEffectDescriptor> Effects { get; }
    }

    public sealed class RankedSkillCatalog
    {
        private readonly IReadOnlyDictionary<string, RankedSkillDefinition> byId;
        public RankedSkillCatalog(string schemaVersion, string contentVersion, IEnumerable<RankedSkillDefinition> skills, IEnumerable<SkillSynergyDefinition> synergies)
        {
            if (string.IsNullOrWhiteSpace(schemaVersion) || string.IsNullOrWhiteSpace(contentVersion)) throw new ArgumentException("Versions are required.");
            SchemaVersion = schemaVersion.Trim(); ContentVersion = contentVersion.Trim();
            var list = (skills ?? throw new ArgumentNullException(nameof(skills))).ToList();
            var synergyList = (synergies ?? Array.Empty<SkillSynergyDefinition>()).ToList();
            Validate(list, synergyList);
            Skills = new ReadOnlyCollection<RankedSkillDefinition>(list.OrderBy(x => x.Id, StringComparer.Ordinal).ToList());
            Synergies = new ReadOnlyCollection<SkillSynergyDefinition>(synergyList.OrderBy(x => x.Id, StringComparer.Ordinal).ToList());
            byId = new ReadOnlyDictionary<string, RankedSkillDefinition>(Skills.ToDictionary(x => x.Id, StringComparer.Ordinal));
            Fingerprint = SkillFingerprint.Hash(ToCanonicalString());
        }
        public string SchemaVersion { get; }
        public string ContentVersion { get; }
        public IReadOnlyList<RankedSkillDefinition> Skills { get; }
        public IReadOnlyList<SkillSynergyDefinition> Synergies { get; }
        public string Fingerprint { get; }
        public bool TryGet(string id, out RankedSkillDefinition skill) => byId.TryGetValue(id ?? string.Empty, out skill);
        public string ToCanonicalString() => SchemaVersion + "|" + ContentVersion + "|" + string.Join(";", Skills.Select(x => x.Id + ":" + x.MaximumRank)) + "|" + string.Join(";", Synergies.Select(x => x.Id));
        private static void Validate(IReadOnlyList<RankedSkillDefinition> skills, IReadOnlyList<SkillSynergyDefinition> synergies)
        {
            if (skills.Count == 0 || skills.Any(x => x == null)) throw new ArgumentException("At least one non-null skill is required.");
            if (skills.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != skills.Count) throw new ArgumentException("Duplicate skill ids.");
            var map = skills.ToDictionary(x => x.Id, StringComparer.Ordinal);
            foreach (var s in skills)
            {
                if (s.ClassOverrides.Select(x => x.ClassId).Distinct(StringComparer.Ordinal).Count() != s.ClassOverrides.Count) throw new ArgumentException("Duplicate class override.");
                if (s.Milestones.Any(x => x.Rank > Math.Max(s.MaximumRank, s.ClassOverrides.Count == 0 ? s.MaximumRank : s.ClassOverrides.Max(y => y.MaximumRank)))) throw new ArgumentException("Milestone exceeds effective maximum.");
                foreach (var p in s.Prerequisites) if (!map.ContainsKey(p.SkillId)) throw new ArgumentException("Missing prerequisite: " + p.SkillId);
            }
            var visiting = new HashSet<string>(StringComparer.Ordinal); var visited = new HashSet<string>(StringComparer.Ordinal);
            Func<string, bool> cycle = null; cycle = id => { if (visiting.Contains(id)) return true; if (visited.Contains(id)) return false; visiting.Add(id); foreach (var p in map[id].Prerequisites) if (cycle(p.SkillId)) return true; visiting.Remove(id); visited.Add(id); return false; };
            foreach (var id in map.Keys) if (cycle(id)) throw new ArgumentException("Circular prerequisites.");
            if (synergies.Any(x => x == null) || synergies.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != synergies.Count) throw new ArgumentException("Invalid synergy ids.");
            foreach (var synergy in synergies) foreach (var requirement in synergy.Requirements)
            { RankedSkillDefinition s; if (!map.TryGetValue(requirement.SkillId, out s) || requirement.MinimumRank > Math.Max(s.MaximumRank, s.ClassOverrides.Count == 0 ? s.MaximumRank : s.ClassOverrides.Max(x => x.MaximumRank))) throw new ArgumentException("Unsatisfiable synergy requirement."); }
        }
    }

    public static class SkillFingerprint
    {
        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            { var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)); return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant(); }
        }
    }

    public sealed class RankedSkillAllocationSnapshot
    {
        public RankedSkillAllocationSnapshot(string profileId, string classId, long version, string schemaVersion, string contentVersion, IDictionary<string, int> ranks)
        {
            if (string.IsNullOrWhiteSpace(profileId) || string.IsNullOrWhiteSpace(classId)) throw new ArgumentException("Profile and class identities are required.");
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            ProfileId = profileId.Trim(); ClassId = classId.Trim(); Version = version; SchemaVersion = schemaVersion ?? string.Empty; ContentVersion = contentVersion ?? string.Empty;
            var copy = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in ranks ?? new Dictionary<string, int>()) { if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1) throw new ArgumentException("Ranks must be positive."); copy.Add(pair.Key.Trim(), pair.Value); }
            Ranks = new ReadOnlyDictionary<string, int>(copy); AllocatedPoints = copy.Values.Sum(); Fingerprint = SkillFingerprint.Hash(ToCanonicalString());
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
        public static RankedSkillAllocationSnapshot Empty(string profileId, string classId, RankedSkillCatalog catalog) => new RankedSkillAllocationSnapshot(profileId, classId, 0, catalog.SchemaVersion, catalog.ContentVersion, null);
    }

    public sealed class SkillEffectContribution
    {
        public SkillEffectContribution(string sourceId, SkillEffectDescriptor effect) { SourceId = sourceId; Effect = effect; }
        public string SourceId { get; }
        public SkillEffectDescriptor Effect { get; }
    }

    public sealed class SkillEffectSnapshot
    {
        public SkillEffectSnapshot(RankedSkillAllocationSnapshot allocation, IEnumerable<SkillEffectContribution> contributions)
        {
            AllocationFingerprint = allocation.Fingerprint; Contributions = new ReadOnlyCollection<SkillEffectContribution>(contributions.OrderBy(x => x.Effect.StatId).ThenBy(x => x.Effect.Kind).ThenBy(x => x.SourceId, StringComparer.Ordinal).ToList());
            Fingerprint = SkillFingerprint.Hash(AllocationFingerprint + "|" + string.Join(";", Contributions.Select(x => x.SourceId + ":" + x.Effect.Canonical)));
        }
        public string AllocationFingerprint { get; }
        public IReadOnlyList<SkillEffectContribution> Contributions { get; }
        public string Fingerprint { get; }
        public decimal Apply(string statId, decimal baseValue)
        {
            var items = Contributions.Where(x => string.Equals(x.Effect.StatId, statId, StringComparison.Ordinal) && string.IsNullOrEmpty(x.Effect.ConditionId)).Select(x => x.Effect).ToList();
            decimal flat = items.Where(x => x.Kind == SkillModifierKind.Flat || x.Kind == SkillModifierKind.IntegerCapacity).Sum(x => x.Value);
            decimal pct = items.Where(x => x.Kind == SkillModifierKind.Percentage).Sum(x => x.Value);
            decimal mult = items.Where(x => x.Kind == SkillModifierKind.Multiplicative).Aggregate(1m, (a, x) => a * x.Value);
            return (baseValue + flat) * (1m + pct) * mult;
        }
    }

    public sealed class SkillEffectProjector
    {
        public SkillEffectSnapshot Project(RankedSkillCatalog catalog, RankedSkillAllocationSnapshot allocation)
        {
            var output = new List<SkillEffectContribution>();
            foreach (var pair in allocation.Ranks)
            {
                RankedSkillDefinition skill; if (!catalog.TryGet(pair.Key, out skill)) continue;
                for (int rank = 1; rank <= pair.Value; rank++) foreach (var effect in skill.PerRankEffects)
                    output.Add(new SkillEffectContribution(skill.Id + "#" + rank, new SkillEffectDescriptor(effect.StatId, effect.Kind, effect.Value * skill.RankValue(allocation.ClassId, rank), effect.ConditionId)));
                foreach (var milestone in skill.Milestones.Where(x => x.Rank <= pair.Value)) foreach (var effect in milestone.Effects) output.Add(new SkillEffectContribution(skill.Id + "@" + milestone.Rank, effect));
            }
            foreach (var synergy in catalog.Synergies.Where(x => x.Requirements.All(r => allocation.RankOf(r.SkillId) >= r.MinimumRank))) foreach (var effect in synergy.Effects) output.Add(new SkillEffectContribution(synergy.Id, effect));
            return new SkillEffectSnapshot(allocation, output);
        }
    }

    public sealed class RankedSkillSampleCatalog
    {
        public static RankedSkillCatalog Create()
        {
            Func<decimal, decimal[]> fifteen = step => Enumerable.Range(1, 15).Select(x => x * step).ToArray();
            var armor = new RankedSkillDefinition("generic.armor", "defense", 6, null, null, null,
                new[] { new SkillClassOverride("juggernaut", 18, Enumerable.Range(1, 18).Select(x => x * 0.01m)) }, Enumerable.Range(1, 6).Select(x => x * 0.01m),
                new[] { new SkillEffectDescriptor("character.armor", SkillModifierKind.Percentage, 1m) }, null);
            var speed = new RankedSkillDefinition("generic.movement_speed", "mobility", 18, null, null, null,
                new[] { new SkillClassOverride("combat_medic", 6, Enumerable.Range(1, 6).Select(x => x * 0.01m)), new SkillClassOverride("juggernaut", 9, Enumerable.Range(1, 9).Select(x => x * 0.01m)) }, Enumerable.Range(1, 18).Select(x => x * 0.01m),
                new[] { new SkillEffectDescriptor("movement.speed", SkillModifierKind.Percentage, 1m) }, null);
            var recovery = new RankedSkillDefinition("striker.thruster_recovery", "mobility", 15, new[] { "striker" }, null, null, null, fifteen(0.01m), new[] { new SkillEffectDescriptor("movement.thruster_recovery", SkillModifierKind.Percentage, 1m) }, new[] { new SkillRankMilestone(5, new[] { new SkillEffectDescriptor("movement.recovery_delay", SkillModifierKind.Flat, -0.1m) }) });
            var efficiency = new RankedSkillDefinition("striker.movement_efficiency", "mobility", 15, new[] { "striker" }, new[] { new SkillPrerequisite("generic.movement_speed", 3) }, null, null, fifteen(0.01m), new[] { new SkillEffectDescriptor("movement.energy_efficiency", SkillModifierKind.Percentage, 1m) }, null);
            var synergy = new SkillSynergyDefinition("striker.third_movement_charge", new[] { new SkillSynergyRequirement(recovery.Id, 8), new SkillSynergyRequirement(efficiency.Id, 8) }, new[] { new SkillEffectDescriptor("movement.maximum_charges", SkillModifierKind.IntegerCapacity, 1m) });
            return new RankedSkillCatalog("skills.schema.v2", "fixture.003", new[] { armor, speed, recovery, efficiency }, new[] { synergy });
        }
    }
}

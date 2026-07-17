using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShooterMover.Domain.Progression.Skills
{
    public sealed class SkillDefinitionV1
    {
        public SkillDefinitionV1(string id, string displayName, string description, int maxRank, string prerequisiteId = "", int prerequisiteRank = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Skill id is required.", nameof(id));
            if (maxRank < 1) throw new ArgumentOutOfRangeException(nameof(maxRank));
            if (prerequisiteRank < 0) throw new ArgumentOutOfRangeException(nameof(prerequisiteRank));
            Id = id.Trim();
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            MaxRank = maxRank;
            PrerequisiteId = prerequisiteId ?? string.Empty;
            PrerequisiteRank = prerequisiteRank;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int MaxRank { get; }
        public string PrerequisiteId { get; }
        public int PrerequisiteRank { get; }
    }

    public sealed class SkillCatalogV1
    {
        private readonly IReadOnlyList<SkillDefinitionV1> definitions;
        private readonly IReadOnlyDictionary<string, SkillDefinitionV1> byId;

        public SkillCatalogV1(IEnumerable<SkillDefinitionV1> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var list = definitions.ToList();
            if (list.Count != 20) throw new ArgumentException("SKILL-001 catalogs contain exactly 20 skills.", nameof(definitions));
            var map = new Dictionary<string, SkillDefinitionV1>(StringComparer.Ordinal);
            foreach (var definition in list)
            {
                if (definition == null || map.ContainsKey(definition.Id)) throw new ArgumentException("Skill definitions must be non-null and unique.", nameof(definitions));
                map.Add(definition.Id, definition);
            }
            foreach (var definition in list)
            {
                if (definition.PrerequisiteId.Length > 0 && !map.ContainsKey(definition.PrerequisiteId))
                    throw new ArgumentException("Unknown prerequisite: " + definition.PrerequisiteId, nameof(definitions));
            }
            this.definitions = new ReadOnlyCollection<SkillDefinitionV1>(list);
            byId = new ReadOnlyDictionary<string, SkillDefinitionV1>(map);
        }

        public IReadOnlyList<SkillDefinitionV1> Definitions => definitions;
        public bool TryGet(string id, out SkillDefinitionV1 definition) => byId.TryGetValue(id ?? string.Empty, out definition);

        public static SkillCatalogV1 CreateDefault()
        {
            var list = new List<SkillDefinitionV1>();
            for (int branch = 0; branch < 4; branch++)
            {
                string prefix = new[] { "offense", "defense", "mobility", "utility" }[branch];
                for (int tier = 1; tier <= 5; tier++)
                {
                    string id = prefix + "." + tier;
                    string prerequisite = tier == 1 ? string.Empty : prefix + "." + (tier - 1);
                    list.Add(new SkillDefinitionV1(id, char.ToUpperInvariant(prefix[0]) + prefix.Substring(1) + " " + tier,
                        "Authorable " + prefix + " skill tier " + tier + ".", 5, prerequisite, tier == 1 ? 0 : 1));
                }
            }
            return new SkillCatalogV1(list);
        }
    }

    public sealed class SkillProgressionSnapshotV1
    {
        public SkillProgressionSnapshotV1(int playerLevel, long sequence, IReadOnlyDictionary<string, int> ranks, IReadOnlyCollection<string> appliedOperationIds)
        {
            PlayerLevel = playerLevel;
            Sequence = sequence;
            Ranks = ranks;
            AppliedOperationIds = appliedOperationIds;
        }
        public int PlayerLevel { get; }
        public long Sequence { get; }
        public IReadOnlyDictionary<string, int> Ranks { get; }
        public IReadOnlyCollection<string> AppliedOperationIds { get; }
        public int SpentPoints => Ranks.Values.Sum();
        public int AvailablePoints => Math.Max(0, PlayerLevel - SpentPoints);
    }

    public enum SkillMutationStatusV1 { Applied, DuplicateNoChange, InvalidRequest, UnknownSkill, RankCapped, PrerequisiteMissing, InsufficientPoints }

    public sealed class SkillMutationFactV1
    {
        public SkillMutationFactV1(SkillMutationStatusV1 status, string skillId, int previousRank, int currentRank, SkillProgressionSnapshotV1 snapshot, string rejectionCode = "")
        { Status = status; SkillId = skillId ?? string.Empty; PreviousRank = previousRank; CurrentRank = currentRank; Snapshot = snapshot; RejectionCode = rejectionCode ?? string.Empty; }
        public SkillMutationStatusV1 Status { get; }
        public string SkillId { get; }
        public int PreviousRank { get; }
        public int CurrentRank { get; }
        public SkillProgressionSnapshotV1 Snapshot { get; }
        public string RejectionCode { get; }
    }
}
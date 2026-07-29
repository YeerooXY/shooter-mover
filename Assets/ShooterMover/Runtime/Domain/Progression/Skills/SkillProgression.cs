using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShooterMover.Domain.Progression.Skills
{
    public sealed class SkillPrerequisite
    {
        public SkillPrerequisite(string skillId, int requiredRank)
        {
            if (string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("Prerequisite skill id is required.", nameof(skillId));
            if (requiredRank < 0) throw new ArgumentOutOfRangeException(nameof(requiredRank));
            SkillId = skillId.Trim();
            RequiredRank = requiredRank;
        }

        public string SkillId { get; }
        public int RequiredRank { get; }
    }

    public sealed class SkillCategoryInvestmentRequirement
    {
        public SkillCategoryInvestmentRequirement(string treeId, string categoryId, int requiredPoints)
        {
            if (string.IsNullOrWhiteSpace(treeId)) throw new ArgumentException("Tree id is required.", nameof(treeId));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category id is required.", nameof(categoryId));
            if (requiredPoints < 1) throw new ArgumentOutOfRangeException(nameof(requiredPoints));
            TreeId = treeId.Trim();
            CategoryId = categoryId.Trim();
            RequiredPoints = requiredPoints;
        }

        public string TreeId { get; }
        public string CategoryId { get; }
        public int RequiredPoints { get; }
        public string StableId => TreeId + "/" + CategoryId;
    }

    public sealed class SkillDefinition
    {
        public SkillDefinition(string id, string displayName, string description, int maxRank, string prerequisiteId = "", int prerequisiteRank = 0)
            : this(
                id,
                "legacy",
                InferCategoryId(id),
                displayName,
                description,
                maxRank,
                CreateLegacyPrerequisites(prerequisiteId, prerequisiteRank),
                null)
        {
        }

        public SkillDefinition(
            string id,
            string treeId,
            string categoryId,
            string displayName,
            string description,
            int maxRank,
            IEnumerable<SkillPrerequisite> prerequisites = null,
            IEnumerable<SkillCategoryInvestmentRequirement> categoryInvestmentRequirements = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Skill id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(treeId)) throw new ArgumentException("Tree id is required.", nameof(treeId));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category id is required.", nameof(categoryId));
            if (maxRank < 1) throw new ArgumentOutOfRangeException(nameof(maxRank));

            Id = id.Trim();
            TreeId = treeId.Trim();
            CategoryId = categoryId.Trim();
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            MaxRank = maxRank;
            Prerequisites = CopyPrerequisites(Id, prerequisites);
            CategoryInvestmentRequirements = CopyCategoryRequirements(categoryInvestmentRequirements);
        }

        public string Id { get; }
        public string TreeId { get; }
        public string CategoryId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int MaxRank { get; }
        public IReadOnlyList<SkillPrerequisite> Prerequisites { get; }
        public IReadOnlyList<SkillCategoryInvestmentRequirement> CategoryInvestmentRequirements { get; }

        public string PrerequisiteId => Prerequisites.Count == 0 ? string.Empty : Prerequisites[0].SkillId;
        public int PrerequisiteRank => Prerequisites.Count == 0 ? 0 : Prerequisites[0].RequiredRank;

        private static IReadOnlyList<SkillPrerequisite> CopyPrerequisites(string skillId, IEnumerable<SkillPrerequisite> prerequisites)
        {
            var list = prerequisites == null ? new List<SkillPrerequisite>() : prerequisites.ToList();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prerequisite in list)
            {
                if (prerequisite == null) throw new ArgumentException("Prerequisites must be non-null.", nameof(prerequisites));
                if (string.Equals(prerequisite.SkillId, skillId, StringComparison.Ordinal))
                    throw new ArgumentException("A skill cannot require itself.", nameof(prerequisites));
                if (!ids.Add(prerequisite.SkillId))
                    throw new ArgumentException("Prerequisite skill ids must be unique per skill.", nameof(prerequisites));
            }
            return new ReadOnlyCollection<SkillPrerequisite>(list);
        }

        private static IReadOnlyList<SkillCategoryInvestmentRequirement> CopyCategoryRequirements(
            IEnumerable<SkillCategoryInvestmentRequirement> requirements)
        {
            var list = requirements == null ? new List<SkillCategoryInvestmentRequirement>() : requirements.ToList();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var requirement in list)
            {
                if (requirement == null) throw new ArgumentException("Category requirements must be non-null.", nameof(requirements));
                if (!ids.Add(requirement.StableId))
                    throw new ArgumentException("Category requirements must be unique per tree/category pair.", nameof(requirements));
            }
            return new ReadOnlyCollection<SkillCategoryInvestmentRequirement>(list);
        }

        private static IEnumerable<SkillPrerequisite> CreateLegacyPrerequisites(string prerequisiteId, int prerequisiteRank)
        {
            if (prerequisiteRank < 0) throw new ArgumentOutOfRangeException(nameof(prerequisiteRank));
            if (string.IsNullOrWhiteSpace(prerequisiteId)) return Array.Empty<SkillPrerequisite>();
            return new[] { new SkillPrerequisite(prerequisiteId, prerequisiteRank) };
        }

        private static string InferCategoryId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "uncategorized";
            string value = id.Trim();
            int separator = value.IndexOf('.');
            return separator > 0 ? value.Substring(0, separator) : "uncategorized";
        }
    }

    public sealed class SkillTreeDefinition
    {
        private readonly IReadOnlyList<SkillDefinition> definitions;

        public SkillTreeDefinition(string id, IEnumerable<SkillDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Tree id is required.", nameof(id));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            Id = id.Trim();
            var list = definitions.ToList();
            if (list.Count < 1) throw new ArgumentException("A skill tree must contain at least one skill.", nameof(definitions));
            foreach (var definition in list)
            {
                if (definition == null) throw new ArgumentException("Skill definitions must be non-null.", nameof(definitions));
                if (!string.Equals(definition.TreeId, Id, StringComparison.Ordinal))
                    throw new ArgumentException("Every skill must reference its containing tree id.", nameof(definitions));
            }
            this.definitions = new ReadOnlyCollection<SkillDefinition>(list);
        }

        public string Id { get; }
        public IReadOnlyList<SkillDefinition> Definitions => definitions;
        public int SkillCount => definitions.Count;
    }

    public sealed class SkillCategoryKey
    {
        public SkillCategoryKey(string treeId, string categoryId)
        {
            if (string.IsNullOrWhiteSpace(treeId)) throw new ArgumentException("Tree id is required.", nameof(treeId));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category id is required.", nameof(categoryId));
            TreeId = treeId.Trim();
            CategoryId = categoryId.Trim();
        }

        public string TreeId { get; }
        public string CategoryId { get; }
        public string StableId => TreeId + "/" + CategoryId;
    }

    public sealed class SkillCatalog
    {
        private readonly IReadOnlyList<SkillTreeDefinition> trees;
        private readonly IReadOnlyList<SkillDefinition> definitions;
        private readonly IReadOnlyList<SkillCategoryKey> categories;
        private readonly IReadOnlyDictionary<string, SkillDefinition> byId;
        private readonly IReadOnlyDictionary<string, SkillTreeDefinition> treesById;
        private readonly HashSet<string> categoryIds;

        public SkillCatalog(IEnumerable<SkillDefinition> definitions)
            : this(GroupByTree(definitions))
        {
        }

        public SkillCatalog(IEnumerable<SkillTreeDefinition> trees)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            var treeList = trees.ToList();
            if (treeList.Count < 1) throw new ArgumentException("A catalog must contain at least one skill tree.", nameof(trees));

            var treeMap = new Dictionary<string, SkillTreeDefinition>(StringComparer.Ordinal);
            var definitionList = new List<SkillDefinition>();
            var skillMap = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            var categoryList = new List<SkillCategoryKey>();
            categoryIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var tree in treeList)
            {
                if (tree == null || treeMap.ContainsKey(tree.Id))
                    throw new ArgumentException("Skill trees must be non-null and uniquely identified.", nameof(trees));
                treeMap.Add(tree.Id, tree);
                foreach (var definition in tree.Definitions)
                {
                    if (skillMap.ContainsKey(definition.Id))
                        throw new ArgumentException("Skill ids must be globally unique within a catalog.", nameof(trees));
                    skillMap.Add(definition.Id, definition);
                    definitionList.Add(definition);
                    string categoryStableId = definition.TreeId + "/" + definition.CategoryId;
                    if (categoryIds.Add(categoryStableId))
                        categoryList.Add(new SkillCategoryKey(definition.TreeId, definition.CategoryId));
                }
            }

            ValidateReferences(definitionList, skillMap, categoryIds);
            ValidatePrerequisiteCycles(definitionList, skillMap);

            this.trees = new ReadOnlyCollection<SkillTreeDefinition>(treeList);
            definitions = new ReadOnlyCollection<SkillDefinition>(definitionList);
            categories = new ReadOnlyCollection<SkillCategoryKey>(categoryList);
            byId = new ReadOnlyDictionary<string, SkillDefinition>(skillMap);
            treesById = new ReadOnlyDictionary<string, SkillTreeDefinition>(treeMap);
        }

        public IReadOnlyList<SkillTreeDefinition> Trees => trees;
        public IReadOnlyList<SkillDefinition> Definitions => definitions;
        public IReadOnlyList<SkillCategoryKey> Categories => categories;
        public bool TryGet(string id, out SkillDefinition definition) => byId.TryGetValue(id ?? string.Empty, out definition);
        public bool TryGetTree(string id, out SkillTreeDefinition tree) => treesById.TryGetValue(id ?? string.Empty, out tree);
        public bool ContainsCategory(string treeId, string categoryId) => categoryIds.Contains((treeId ?? string.Empty) + "/" + (categoryId ?? string.Empty));

        public static SkillCatalog CreateDefault()
        {
            return new SkillCatalog(new[] { CreateLinearTree("default", new[] { "offense", "defense", "utility" }, 5, false) });
        }

        public static SkillCatalog CreateSpecializedFiveSkillCatalog(string treeId, string categoryId)
        {
            return new SkillCatalog(new[] { CreateLinearTree(treeId, new[] { categoryId }, 5, false) });
        }

        public static SkillCatalog CreateMixedTreeFixture()
        {
            return new SkillCatalog(new[]
            {
                CreateLinearTree("default", new[] { "offense", "defense", "utility" }, 5, false),
                CreateLinearTree("medic.specialized", new[] { "healing" }, 5, false)
            });
        }

        public static SkillCatalog CreateCompatibilityTwentySkillCatalog()
        {
            return new SkillCatalog(new[]
            {
                CreateLinearTree("compatibility.20", new[] { "offense", "defense", "mobility", "utility" }, 5, true)
            });
        }

        private static SkillTreeDefinition CreateLinearTree(string treeId, IReadOnlyList<string> categoryNames, int skillsPerCategory, bool legacySkillIds)
        {
            if (string.IsNullOrWhiteSpace(treeId)) throw new ArgumentException("Tree id is required.", nameof(treeId));
            if (categoryNames == null || categoryNames.Count < 1) throw new ArgumentException("At least one category is required.", nameof(categoryNames));
            if (skillsPerCategory < 1) throw new ArgumentOutOfRangeException(nameof(skillsPerCategory));

            var list = new List<SkillDefinition>();
            foreach (var categoryName in categoryNames)
            {
                if (string.IsNullOrWhiteSpace(categoryName)) throw new ArgumentException("Category ids are required.", nameof(categoryNames));
                string categoryId = categoryName.Trim();
                for (int tier = 1; tier <= skillsPerCategory; tier++)
                {
                    string id = legacySkillIds ? categoryId + "." + tier : treeId + "." + categoryId + "." + tier;
                    string prerequisite = tier == 1
                        ? string.Empty
                        : (legacySkillIds ? categoryId + "." + (tier - 1) : treeId + "." + categoryId + "." + (tier - 1));
                    var prerequisites = tier == 1
                        ? Array.Empty<SkillPrerequisite>()
                        : new[] { new SkillPrerequisite(prerequisite, 1) };
                    string displayCategory = char.ToUpperInvariant(categoryId[0]) + categoryId.Substring(1);
                    list.Add(new SkillDefinition(
                        id,
                        treeId,
                        categoryId,
                        displayCategory + " " + tier,
                        "Authorable " + categoryId + " skill tier " + tier + ".",
                        5,
                        prerequisites));
                }
            }
            return new SkillTreeDefinition(treeId, list);
        }

        private static IEnumerable<SkillTreeDefinition> GroupByTree(IEnumerable<SkillDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var orderedTreeIds = new List<string>();
            var grouped = new Dictionary<string, List<SkillDefinition>>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Skill definitions must be non-null.", nameof(definitions));
                List<SkillDefinition> treeDefinitions;
                if (!grouped.TryGetValue(definition.TreeId, out treeDefinitions))
                {
                    treeDefinitions = new List<SkillDefinition>();
                    grouped.Add(definition.TreeId, treeDefinitions);
                    orderedTreeIds.Add(definition.TreeId);
                }
                treeDefinitions.Add(definition);
            }
            if (orderedTreeIds.Count < 1) throw new ArgumentException("A catalog must contain at least one skill.", nameof(definitions));
            return orderedTreeIds.Select(treeId => new SkillTreeDefinition(treeId, grouped[treeId])).ToList();
        }

        private static void ValidateReferences(
            IEnumerable<SkillDefinition> definitions,
            IReadOnlyDictionary<string, SkillDefinition> skillMap,
            ISet<string> knownCategoryIds)
        {
            foreach (var definition in definitions)
            {
                foreach (var prerequisite in definition.Prerequisites)
                {
                    if (!skillMap.ContainsKey(prerequisite.SkillId))
                        throw new ArgumentException("Unknown prerequisite: " + prerequisite.SkillId, nameof(definitions));
                }
                foreach (var requirement in definition.CategoryInvestmentRequirements)
                {
                    if (!knownCategoryIds.Contains(requirement.StableId))
                        throw new ArgumentException("Unknown category requirement: " + requirement.StableId, nameof(definitions));
                }
            }
        }

        private static void ValidatePrerequisiteCycles(
            IEnumerable<SkillDefinition> definitions,
            IReadOnlyDictionary<string, SkillDefinition> skillMap)
        {
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var definition in definitions) Visit(definition, skillMap, state);
        }

        private static void Visit(
            SkillDefinition definition,
            IReadOnlyDictionary<string, SkillDefinition> skillMap,
            IDictionary<string, int> state)
        {
            int currentState;
            if (state.TryGetValue(definition.Id, out currentState))
            {
                if (currentState == 1) throw new ArgumentException("Skill prerequisite graph contains a cycle at " + definition.Id + ".");
                if (currentState == 2) return;
            }

            state[definition.Id] = 1;
            foreach (var prerequisite in definition.Prerequisites) Visit(skillMap[prerequisite.SkillId], skillMap, state);
            state[definition.Id] = 2;
        }
    }

    public sealed class SkillCategoryInvestment
    {
        public SkillCategoryInvestment(string treeId, string categoryId, int investedPoints)
        {
            if (string.IsNullOrWhiteSpace(treeId)) throw new ArgumentException("Tree id is required.", nameof(treeId));
            if (string.IsNullOrWhiteSpace(categoryId)) throw new ArgumentException("Category id is required.", nameof(categoryId));
            if (investedPoints < 0) throw new ArgumentOutOfRangeException(nameof(investedPoints));
            TreeId = treeId.Trim();
            CategoryId = categoryId.Trim();
            InvestedPoints = investedPoints;
        }

        public string TreeId { get; }
        public string CategoryId { get; }
        public int InvestedPoints { get; }
        public string StableId => TreeId + "/" + CategoryId;
    }

    public sealed class SkillProgressionSnapshot
    {
        public SkillProgressionSnapshot(int playerLevel, long sequence, IReadOnlyDictionary<string, int> ranks, IReadOnlyCollection<string> appliedOperationIds)
            : this(playerLevel, sequence, ranks, appliedOperationIds, Array.Empty<SkillCategoryInvestment>())
        {
        }

        public SkillProgressionSnapshot(
            int playerLevel,
            long sequence,
            IReadOnlyDictionary<string, int> ranks,
            IReadOnlyCollection<string> appliedOperationIds,
            IReadOnlyList<SkillCategoryInvestment> categoryInvestments)
        {
            PlayerLevel = playerLevel;
            Sequence = sequence;
            Ranks = ranks ?? throw new ArgumentNullException(nameof(ranks));
            AppliedOperationIds = appliedOperationIds ?? throw new ArgumentNullException(nameof(appliedOperationIds));
            CategoryInvestments = categoryInvestments ?? throw new ArgumentNullException(nameof(categoryInvestments));
        }

        public int PlayerLevel { get; }
        public long Sequence { get; }
        public IReadOnlyDictionary<string, int> Ranks { get; }
        public IReadOnlyCollection<string> AppliedOperationIds { get; }
        public IReadOnlyList<SkillCategoryInvestment> CategoryInvestments { get; }
        public int SpentPoints => Ranks.Values.Sum();
        public int AvailablePoints => Math.Max(0, PlayerLevel - SpentPoints);

        public int GetInvestedPoints(string treeId, string categoryId)
        {
            foreach (var investment in CategoryInvestments)
            {
                if (string.Equals(investment.TreeId, treeId, StringComparison.Ordinal) &&
                    string.Equals(investment.CategoryId, categoryId, StringComparison.Ordinal))
                    return investment.InvestedPoints;
            }
            return 0;
        }
    }

    public enum SkillMutationStatus
    {
        Applied,
        DuplicateNoChange,
        InvalidRequest,
        UnknownSkill,
        RankCapped,
        PrerequisiteMissing,
        CategoryInvestmentMissing,
        InsufficientPoints
    }

    public sealed class SkillRejectionReason
    {
        public static readonly SkillRejectionReason None = new SkillRejectionReason(string.Empty);

        public SkillRejectionReason(string code, string relatedId = "", int requiredValue = 0, int actualValue = 0)
        {
            Code = code ?? string.Empty;
            RelatedId = relatedId ?? string.Empty;
            RequiredValue = requiredValue;
            ActualValue = actualValue;
        }

        public string Code { get; }
        public string RelatedId { get; }
        public int RequiredValue { get; }
        public int ActualValue { get; }
    }

    public sealed class SkillMutationFact
    {
        public SkillMutationFact(
            SkillMutationStatus status,
            string skillId,
            int previousRank,
            int currentRank,
            SkillProgressionSnapshot snapshot,
            string rejectionCode = "")
            : this(status, skillId, previousRank, currentRank, snapshot, new SkillRejectionReason(rejectionCode))
        {
        }

        public SkillMutationFact(
            SkillMutationStatus status,
            string skillId,
            int previousRank,
            int currentRank,
            SkillProgressionSnapshot snapshot,
            SkillRejectionReason rejectionReason)
        {
            Status = status;
            SkillId = skillId ?? string.Empty;
            PreviousRank = previousRank;
            CurrentRank = currentRank;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            RejectionReason = rejectionReason ?? SkillRejectionReason.None;
        }

        public SkillMutationStatus Status { get; }
        public string SkillId { get; }
        public int PreviousRank { get; }
        public int CurrentRank { get; }
        public SkillProgressionSnapshot Snapshot { get; }
        public SkillRejectionReason RejectionReason { get; }
        public string RejectionCode => RejectionReason.Code;
    }
}

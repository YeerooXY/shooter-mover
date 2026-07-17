using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShooterMover.Domain.Progression.Skills
{
    public sealed class SkillPrerequisiteV1
    {
        public SkillPrerequisiteV1(string skillId, int requiredRank)
        {
            if (string.IsNullOrWhiteSpace(skillId)) throw new ArgumentException("Prerequisite skill id is required.", nameof(skillId));
            if (requiredRank < 0) throw new ArgumentOutOfRangeException(nameof(requiredRank));
            SkillId = skillId.Trim();
            RequiredRank = requiredRank;
        }

        public string SkillId { get; }
        public int RequiredRank { get; }
    }

    public sealed class SkillCategoryInvestmentRequirementV1
    {
        public SkillCategoryInvestmentRequirementV1(string treeId, string categoryId, int requiredPoints)
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

    public sealed class SkillDefinitionV1
    {
        public SkillDefinitionV1(string id, string displayName, string description, int maxRank, string prerequisiteId = "", int prerequisiteRank = 0)
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

        public SkillDefinitionV1(
            string id,
            string treeId,
            string categoryId,
            string displayName,
            string description,
            int maxRank,
            IEnumerable<SkillPrerequisiteV1> prerequisites = null,
            IEnumerable<SkillCategoryInvestmentRequirementV1> categoryInvestmentRequirements = null)
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
        public IReadOnlyList<SkillPrerequisiteV1> Prerequisites { get; }
        public IReadOnlyList<SkillCategoryInvestmentRequirementV1> CategoryInvestmentRequirements { get; }

        public string PrerequisiteId => Prerequisites.Count == 0 ? string.Empty : Prerequisites[0].SkillId;
        public int PrerequisiteRank => Prerequisites.Count == 0 ? 0 : Prerequisites[0].RequiredRank;

        private static IReadOnlyList<SkillPrerequisiteV1> CopyPrerequisites(string skillId, IEnumerable<SkillPrerequisiteV1> prerequisites)
        {
            var list = prerequisites == null ? new List<SkillPrerequisiteV1>() : prerequisites.ToList();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prerequisite in list)
            {
                if (prerequisite == null) throw new ArgumentException("Prerequisites must be non-null.", nameof(prerequisites));
                if (string.Equals(prerequisite.SkillId, skillId, StringComparison.Ordinal))
                    throw new ArgumentException("A skill cannot require itself.", nameof(prerequisites));
                if (!ids.Add(prerequisite.SkillId))
                    throw new ArgumentException("Prerequisite skill ids must be unique per skill.", nameof(prerequisites));
            }
            return new ReadOnlyCollection<SkillPrerequisiteV1>(list);
        }

        private static IReadOnlyList<SkillCategoryInvestmentRequirementV1> CopyCategoryRequirements(
            IEnumerable<SkillCategoryInvestmentRequirementV1> requirements)
        {
            var list = requirements == null ? new List<SkillCategoryInvestmentRequirementV1>() : requirements.ToList();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var requirement in list)
            {
                if (requirement == null) throw new ArgumentException("Category requirements must be non-null.", nameof(requirements));
                if (!ids.Add(requirement.StableId))
                    throw new ArgumentException("Category requirements must be unique per tree/category pair.", nameof(requirements));
            }
            return new ReadOnlyCollection<SkillCategoryInvestmentRequirementV1>(list);
        }

        private static IEnumerable<SkillPrerequisiteV1> CreateLegacyPrerequisites(string prerequisiteId, int prerequisiteRank)
        {
            if (prerequisiteRank < 0) throw new ArgumentOutOfRangeException(nameof(prerequisiteRank));
            if (string.IsNullOrWhiteSpace(prerequisiteId)) return Array.Empty<SkillPrerequisiteV1>();
            return new[] { new SkillPrerequisiteV1(prerequisiteId, prerequisiteRank) };
        }

        private static string InferCategoryId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "uncategorized";
            string value = id.Trim();
            int separator = value.IndexOf('.');
            return separator > 0 ? value.Substring(0, separator) : "uncategorized";
        }
    }

    public sealed class SkillTreeDefinitionV1
    {
        private readonly IReadOnlyList<SkillDefinitionV1> definitions;

        public SkillTreeDefinitionV1(string id, IEnumerable<SkillDefinitionV1> definitions)
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
            this.definitions = new ReadOnlyCollection<SkillDefinitionV1>(list);
        }

        public string Id { get; }
        public IReadOnlyList<SkillDefinitionV1> Definitions => definitions;
        public int SkillCount => definitions.Count;
    }

    public sealed class SkillCategoryKeyV1
    {
        public SkillCategoryKeyV1(string treeId, string categoryId)
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

    public sealed class SkillCatalogV1
    {
        private readonly IReadOnlyList<SkillTreeDefinitionV1> trees;
        private readonly IReadOnlyList<SkillDefinitionV1> definitions;
        private readonly IReadOnlyList<SkillCategoryKeyV1> categories;
        private readonly IReadOnlyDictionary<string, SkillDefinitionV1> byId;
        private readonly IReadOnlyDictionary<string, SkillTreeDefinitionV1> treesById;
        private readonly HashSet<string> categoryIds;

        public SkillCatalogV1(IEnumerable<SkillDefinitionV1> definitions)
            : this(GroupByTree(definitions))
        {
        }

        public SkillCatalogV1(IEnumerable<SkillTreeDefinitionV1> trees)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            var treeList = trees.ToList();
            if (treeList.Count < 1) throw new ArgumentException("A catalog must contain at least one skill tree.", nameof(trees));

            var treeMap = new Dictionary<string, SkillTreeDefinitionV1>(StringComparer.Ordinal);
            var definitionList = new List<SkillDefinitionV1>();
            var skillMap = new Dictionary<string, SkillDefinitionV1>(StringComparer.Ordinal);
            var categoryList = new List<SkillCategoryKeyV1>();
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
                        categoryList.Add(new SkillCategoryKeyV1(definition.TreeId, definition.CategoryId));
                }
            }

            ValidateReferences(definitionList, skillMap, categoryIds);
            ValidatePrerequisiteCycles(definitionList, skillMap);

            this.trees = new ReadOnlyCollection<SkillTreeDefinitionV1>(treeList);
            definitions = new ReadOnlyCollection<SkillDefinitionV1>(definitionList);
            categories = new ReadOnlyCollection<SkillCategoryKeyV1>(categoryList);
            byId = new ReadOnlyDictionary<string, SkillDefinitionV1>(skillMap);
            treesById = new ReadOnlyDictionary<string, SkillTreeDefinitionV1>(treeMap);
        }

        public IReadOnlyList<SkillTreeDefinitionV1> Trees => trees;
        public IReadOnlyList<SkillDefinitionV1> Definitions => definitions;
        public IReadOnlyList<SkillCategoryKeyV1> Categories => categories;
        public bool TryGet(string id, out SkillDefinitionV1 definition) => byId.TryGetValue(id ?? string.Empty, out definition);
        public bool TryGetTree(string id, out SkillTreeDefinitionV1 tree) => treesById.TryGetValue(id ?? string.Empty, out tree);
        public bool ContainsCategory(string treeId, string categoryId) => categoryIds.Contains((treeId ?? string.Empty) + "/" + (categoryId ?? string.Empty));

        public static SkillCatalogV1 CreateDefault()
        {
            return new SkillCatalogV1(new[] { CreateLinearTree("default", new[] { "offense", "defense", "utility" }, 5, false) });
        }

        public static SkillCatalogV1 CreateSpecializedFiveSkillCatalog(string treeId, string categoryId)
        {
            return new SkillCatalogV1(new[] { CreateLinearTree(treeId, new[] { categoryId }, 5, false) });
        }

        public static SkillCatalogV1 CreateMixedTreeFixture()
        {
            return new SkillCatalogV1(new[]
            {
                CreateLinearTree("default", new[] { "offense", "defense", "utility" }, 5, false),
                CreateLinearTree("medic.specialized", new[] { "healing" }, 5, false)
            });
        }

        public static SkillCatalogV1 CreateCompatibilityTwentySkillCatalog()
        {
            return new SkillCatalogV1(new[]
            {
                CreateLinearTree("compatibility.20", new[] { "offense", "defense", "mobility", "utility" }, 5, true)
            });
        }

        private static SkillTreeDefinitionV1 CreateLinearTree(string treeId, IReadOnlyList<string> categoryNames, int skillsPerCategory, bool legacySkillIds)
        {
            if (string.IsNullOrWhiteSpace(treeId)) throw new ArgumentException("Tree id is required.", nameof(treeId));
            if (categoryNames == null || categoryNames.Count < 1) throw new ArgumentException("At least one category is required.", nameof(categoryNames));
            if (skillsPerCategory < 1) throw new ArgumentOutOfRangeException(nameof(skillsPerCategory));

            var list = new List<SkillDefinitionV1>();
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
                        ? Array.Empty<SkillPrerequisiteV1>()
                        : new[] { new SkillPrerequisiteV1(prerequisite, 1) };
                    string displayCategory = char.ToUpperInvariant(categoryId[0]) + categoryId.Substring(1);
                    list.Add(new SkillDefinitionV1(
                        id,
                        treeId,
                        categoryId,
                        displayCategory + " " + tier,
                        "Authorable " + categoryId + " skill tier " + tier + ".",
                        5,
                        prerequisites));
                }
            }
            return new SkillTreeDefinitionV1(treeId, list);
        }

        private static IEnumerable<SkillTreeDefinitionV1> GroupByTree(IEnumerable<SkillDefinitionV1> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var orderedTreeIds = new List<string>();
            var grouped = new Dictionary<string, List<SkillDefinitionV1>>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Skill definitions must be non-null.", nameof(definitions));
                List<SkillDefinitionV1> treeDefinitions;
                if (!grouped.TryGetValue(definition.TreeId, out treeDefinitions))
                {
                    treeDefinitions = new List<SkillDefinitionV1>();
                    grouped.Add(definition.TreeId, treeDefinitions);
                    orderedTreeIds.Add(definition.TreeId);
                }
                treeDefinitions.Add(definition);
            }
            if (orderedTreeIds.Count < 1) throw new ArgumentException("A catalog must contain at least one skill.", nameof(definitions));
            return orderedTreeIds.Select(treeId => new SkillTreeDefinitionV1(treeId, grouped[treeId])).ToList();
        }

        private static void ValidateReferences(
            IEnumerable<SkillDefinitionV1> definitions,
            IReadOnlyDictionary<string, SkillDefinitionV1> skillMap,
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
            IEnumerable<SkillDefinitionV1> definitions,
            IReadOnlyDictionary<string, SkillDefinitionV1> skillMap)
        {
            var state = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var definition in definitions) Visit(definition, skillMap, state);
        }

        private static void Visit(
            SkillDefinitionV1 definition,
            IReadOnlyDictionary<string, SkillDefinitionV1> skillMap,
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

    public sealed class SkillCategoryInvestmentV1
    {
        public SkillCategoryInvestmentV1(string treeId, string categoryId, int investedPoints)
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

    public sealed class SkillProgressionSnapshotV1
    {
        public SkillProgressionSnapshotV1(int playerLevel, long sequence, IReadOnlyDictionary<string, int> ranks, IReadOnlyCollection<string> appliedOperationIds)
            : this(playerLevel, sequence, ranks, appliedOperationIds, Array.Empty<SkillCategoryInvestmentV1>())
        {
        }

        public SkillProgressionSnapshotV1(
            int playerLevel,
            long sequence,
            IReadOnlyDictionary<string, int> ranks,
            IReadOnlyCollection<string> appliedOperationIds,
            IReadOnlyList<SkillCategoryInvestmentV1> categoryInvestments)
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
        public IReadOnlyList<SkillCategoryInvestmentV1> CategoryInvestments { get; }
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

    public enum SkillMutationStatusV1
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

    public sealed class SkillRejectionReasonV1
    {
        public static readonly SkillRejectionReasonV1 None = new SkillRejectionReasonV1(string.Empty);

        public SkillRejectionReasonV1(string code, string relatedId = "", int requiredValue = 0, int actualValue = 0)
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

    public sealed class SkillMutationFactV1
    {
        public SkillMutationFactV1(
            SkillMutationStatusV1 status,
            string skillId,
            int previousRank,
            int currentRank,
            SkillProgressionSnapshotV1 snapshot,
            string rejectionCode = "")
            : this(status, skillId, previousRank, currentRank, snapshot, new SkillRejectionReasonV1(rejectionCode))
        {
        }

        public SkillMutationFactV1(
            SkillMutationStatusV1 status,
            string skillId,
            int previousRank,
            int currentRank,
            SkillProgressionSnapshotV1 snapshot,
            SkillRejectionReasonV1 rejectionReason)
        {
            Status = status;
            SkillId = skillId ?? string.Empty;
            PreviousRank = previousRank;
            CurrentRank = currentRank;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            RejectionReason = rejectionReason ?? SkillRejectionReasonV1.None;
        }

        public SkillMutationStatusV1 Status { get; }
        public string SkillId { get; }
        public int PreviousRank { get; }
        public int CurrentRank { get; }
        public SkillProgressionSnapshotV1 Snapshot { get; }
        public SkillRejectionReasonV1 RejectionReason { get; }
        public string RejectionCode => RejectionReason.Code;
    }
}

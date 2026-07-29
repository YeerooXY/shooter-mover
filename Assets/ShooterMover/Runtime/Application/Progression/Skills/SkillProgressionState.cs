using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Progression.Skills
{
    public sealed class SkillProgressionState
    {
        private readonly object syncRoot = new object();
        private readonly SkillCatalog catalog;
        private readonly Dictionary<string, int> ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> appliedOperations = new HashSet<string>(StringComparer.Ordinal);
        private int playerLevel;
        private long sequence;

        public SkillProgressionState(SkillCatalog catalog, int playerLevel)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            SetPlayerLevel(playerLevel);
            foreach (var definition in catalog.Definitions) ranks.Add(definition.Id, 0);
        }

        public SkillCatalog Catalog => catalog;
        public SkillProgressionSnapshot CurrentSnapshot { get { lock (syncRoot) return BuildSnapshot(); } }

        public void SetPlayerLevel(int value)
        {
            if (value < 1 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
            lock (syncRoot) playerLevel = value;
        }

        public SkillMutationFact Allocate(string operationId, string skillId)
        {
            lock (syncRoot)
            {
                if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(skillId))
                    return Fact(
                        SkillMutationStatus.InvalidRequest,
                        skillId,
                        0,
                        0,
                        new SkillRejectionReason("skill-request-invalid"));

                if (appliedOperations.Contains(operationId))
                {
                    int duplicateRank = ranks.ContainsKey(skillId) ? ranks[skillId] : 0;
                    return Fact(SkillMutationStatus.DuplicateNoChange, skillId, duplicateRank, duplicateRank);
                }

                SkillDefinition definition;
                if (!catalog.TryGet(skillId, out definition))
                    return Fact(
                        SkillMutationStatus.UnknownSkill,
                        skillId,
                        0,
                        0,
                        new SkillRejectionReason("skill-unknown", skillId));

                int previousRank = ranks[definition.Id];
                if (previousRank >= definition.MaxRank)
                    return Fact(
                        SkillMutationStatus.RankCapped,
                        definition.Id,
                        previousRank,
                        previousRank,
                        new SkillRejectionReason("skill-rank-capped", definition.Id, definition.MaxRank, previousRank));

                foreach (var prerequisite in definition.Prerequisites)
                {
                    int actualRank = ranks[prerequisite.SkillId];
                    if (actualRank < prerequisite.RequiredRank)
                        return Fact(
                            SkillMutationStatus.PrerequisiteMissing,
                            definition.Id,
                            previousRank,
                            previousRank,
                            new SkillRejectionReason(
                                "skill-prerequisite-missing",
                                prerequisite.SkillId,
                                prerequisite.RequiredRank,
                                actualRank));
                }

                foreach (var requirement in definition.CategoryInvestmentRequirements)
                {
                    int actualPoints = GetInvestedPoints(requirement.TreeId, requirement.CategoryId);
                    if (actualPoints < requirement.RequiredPoints)
                        return Fact(
                            SkillMutationStatus.CategoryInvestmentMissing,
                            definition.Id,
                            previousRank,
                            previousRank,
                            new SkillRejectionReason(
                                "skill-category-investment-missing",
                                requirement.StableId,
                                requirement.RequiredPoints,
                                actualPoints));
                }

                int availablePoints = Math.Max(0, playerLevel - GetSpentPoints());
                if (availablePoints < 1)
                    return Fact(
                        SkillMutationStatus.InsufficientPoints,
                        definition.Id,
                        previousRank,
                        previousRank,
                        new SkillRejectionReason("skill-points-insufficient", string.Empty, 1, availablePoints));

                ranks[definition.Id] = previousRank + 1;
                appliedOperations.Add(operationId);
                sequence = checked(sequence + 1L);
                return Fact(SkillMutationStatus.Applied, definition.Id, previousRank, previousRank + 1);
            }
        }

        public SkillProgressionSnapshot ExportSnapshot()
        {
            lock (syncRoot) return BuildSnapshot();
        }

        private SkillMutationFact Fact(
            SkillMutationStatus status,
            string skillId,
            int previousRank,
            int currentRank,
            SkillRejectionReason rejectionReason = null)
        {
            return new SkillMutationFact(
                status,
                skillId,
                previousRank,
                currentRank,
                BuildSnapshot(),
                rejectionReason ?? SkillRejectionReason.None);
        }

        private SkillProgressionSnapshot BuildSnapshot()
        {
            var operationIds = new List<string>(appliedOperations);
            operationIds.Sort(StringComparer.Ordinal);

            var categoryInvestments = new List<SkillCategoryInvestment>();
            foreach (var category in catalog.Categories)
            {
                categoryInvestments.Add(new SkillCategoryInvestment(
                    category.TreeId,
                    category.CategoryId,
                    GetInvestedPoints(category.TreeId, category.CategoryId)));
            }

            return new SkillProgressionSnapshot(
                playerLevel,
                sequence,
                new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(ranks, StringComparer.Ordinal)),
                new ReadOnlyCollection<string>(operationIds),
                new ReadOnlyCollection<SkillCategoryInvestment>(categoryInvestments));
        }

        private int GetSpentPoints()
        {
            int spentPoints = 0;
            foreach (var rank in ranks.Values) spentPoints = checked(spentPoints + rank);
            return spentPoints;
        }

        private int GetInvestedPoints(string treeId, string categoryId)
        {
            int investedPoints = 0;
            foreach (var definition in catalog.Definitions)
            {
                if (string.Equals(definition.TreeId, treeId, StringComparison.Ordinal) &&
                    string.Equals(definition.CategoryId, categoryId, StringComparison.Ordinal))
                    investedPoints = checked(investedPoints + ranks[definition.Id]);
            }
            return investedPoints;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Progression.Skills
{
    public sealed class SkillProgressionAuthorityV1
    {
        private readonly object syncRoot = new object();
        private readonly SkillCatalogV1 catalog;
        private readonly Dictionary<string, int> ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> appliedOperations = new HashSet<string>(StringComparer.Ordinal);
        private int playerLevel;
        private long sequence;

        public SkillProgressionAuthorityV1(SkillCatalogV1 catalog, int playerLevel)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            SetPlayerLevel(playerLevel);
            foreach (var definition in catalog.Definitions) ranks.Add(definition.Id, 0);
        }

        public SkillCatalogV1 Catalog => catalog;
        public SkillProgressionSnapshotV1 CurrentSnapshot { get { lock (syncRoot) return BuildSnapshot(); } }

        public void SetPlayerLevel(int value)
        {
            if (value < 1 || value > 100) throw new ArgumentOutOfRangeException(nameof(value));
            lock (syncRoot) playerLevel = value;
        }

        public SkillMutationFactV1 Allocate(string operationId, string skillId)
        {
            lock (syncRoot)
            {
                if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(skillId))
                    return Fact(
                        SkillMutationStatusV1.InvalidRequest,
                        skillId,
                        0,
                        0,
                        new SkillRejectionReasonV1("skill-request-invalid"));

                if (appliedOperations.Contains(operationId))
                {
                    int duplicateRank = ranks.ContainsKey(skillId) ? ranks[skillId] : 0;
                    return Fact(SkillMutationStatusV1.DuplicateNoChange, skillId, duplicateRank, duplicateRank);
                }

                SkillDefinitionV1 definition;
                if (!catalog.TryGet(skillId, out definition))
                    return Fact(
                        SkillMutationStatusV1.UnknownSkill,
                        skillId,
                        0,
                        0,
                        new SkillRejectionReasonV1("skill-unknown", skillId));

                int previousRank = ranks[definition.Id];
                if (previousRank >= definition.MaxRank)
                    return Fact(
                        SkillMutationStatusV1.RankCapped,
                        definition.Id,
                        previousRank,
                        previousRank,
                        new SkillRejectionReasonV1("skill-rank-capped", definition.Id, definition.MaxRank, previousRank));

                foreach (var prerequisite in definition.Prerequisites)
                {
                    int actualRank = ranks[prerequisite.SkillId];
                    if (actualRank < prerequisite.RequiredRank)
                        return Fact(
                            SkillMutationStatusV1.PrerequisiteMissing,
                            definition.Id,
                            previousRank,
                            previousRank,
                            new SkillRejectionReasonV1(
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
                            SkillMutationStatusV1.CategoryInvestmentMissing,
                            definition.Id,
                            previousRank,
                            previousRank,
                            new SkillRejectionReasonV1(
                                "skill-category-investment-missing",
                                requirement.StableId,
                                requirement.RequiredPoints,
                                actualPoints));
                }

                int availablePoints = Math.Max(0, playerLevel - GetSpentPoints());
                if (availablePoints < 1)
                    return Fact(
                        SkillMutationStatusV1.InsufficientPoints,
                        definition.Id,
                        previousRank,
                        previousRank,
                        new SkillRejectionReasonV1("skill-points-insufficient", string.Empty, 1, availablePoints));

                ranks[definition.Id] = previousRank + 1;
                appliedOperations.Add(operationId);
                sequence = checked(sequence + 1L);
                return Fact(SkillMutationStatusV1.Applied, definition.Id, previousRank, previousRank + 1);
            }
        }

        public SkillProgressionSnapshotV1 ExportSnapshot()
        {
            lock (syncRoot) return BuildSnapshot();
        }

        private SkillMutationFactV1 Fact(
            SkillMutationStatusV1 status,
            string skillId,
            int previousRank,
            int currentRank,
            SkillRejectionReasonV1 rejectionReason = null)
        {
            return new SkillMutationFactV1(
                status,
                skillId,
                previousRank,
                currentRank,
                BuildSnapshot(),
                rejectionReason ?? SkillRejectionReasonV1.None);
        }

        private SkillProgressionSnapshotV1 BuildSnapshot()
        {
            var operationIds = new List<string>(appliedOperations);
            operationIds.Sort(StringComparer.Ordinal);

            var categoryInvestments = new List<SkillCategoryInvestmentV1>();
            foreach (var category in catalog.Categories)
            {
                categoryInvestments.Add(new SkillCategoryInvestmentV1(
                    category.TreeId,
                    category.CategoryId,
                    GetInvestedPoints(category.TreeId, category.CategoryId)));
            }

            return new SkillProgressionSnapshotV1(
                playerLevel,
                sequence,
                new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(ranks, StringComparer.Ordinal)),
                new ReadOnlyCollection<string>(operationIds),
                new ReadOnlyCollection<SkillCategoryInvestmentV1>(categoryInvestments));
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

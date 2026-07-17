using System;
using NUnit.Framework;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Progression.Skills
{
    public sealed class SkillProgressionAuthorityTests
    {
        [Test]
        public void DefaultCatalogContainsOneFifteenSkillTree()
        {
            var catalog = SkillCatalogV1.CreateDefault();

            Assert.That(catalog.Trees.Count, Is.EqualTo(1));
            Assert.That(catalog.Trees[0].Id, Is.EqualTo("default"));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(15));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(15));
        }

        [Test]
        public void SpecializedFactoryContainsFiveSkills()
        {
            var catalog = SkillCatalogV1.CreateSpecializedFiveSkillCatalog("medic", "healing");

            Assert.That(catalog.Trees.Count, Is.EqualTo(1));
            Assert.That(catalog.Trees[0].Id, Is.EqualTo("medic"));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(5));
            Assert.That(catalog.Definitions[0].TreeId, Is.EqualTo("medic"));
            Assert.That(catalog.Definitions[0].CategoryId, Is.EqualTo("healing"));
        }

        [Test]
        public void MixedFixtureSupportsDifferentTreeSizes()
        {
            var catalog = SkillCatalogV1.CreateMixedTreeFixture();

            Assert.That(catalog.Trees.Count, Is.EqualTo(2));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(15));
            Assert.That(catalog.Trees[1].SkillCount, Is.EqualTo(5));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(20));
        }

        [Test]
        public void ArbitraryPositiveSkillCountsAreAccepted()
        {
            var skill = Definition("solo", "solo-tree", "utility", 1);
            var catalog = new SkillCatalogV1(new[] { new SkillTreeDefinitionV1("solo-tree", new[] { skill }) });

            Assert.That(catalog.Definitions.Count, Is.EqualTo(1));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(1));
        }

        [Test]
        public void EmptySkillTreeIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new SkillTreeDefinitionV1("empty", Array.Empty<SkillDefinitionV1>()));
        }

        [Test]
        public void CompatibilityCatalogPreservesOriginalTwentySkillIds()
        {
            var catalog = SkillCatalogV1.CreateCompatibilityTwentySkillCatalog();
            SkillDefinitionV1 offense;
            SkillDefinitionV1 utility;

            Assert.That(catalog.Definitions.Count, Is.EqualTo(20));
            Assert.That(catalog.TryGet("offense.1", out offense), Is.True);
            Assert.That(catalog.TryGet("utility.5", out utility), Is.True);
            Assert.That(offense.TreeId, Is.EqualTo("compatibility.20"));
            Assert.That(utility.CategoryId, Is.EqualTo("utility"));
        }

        [Test]
        public void LegacyConstructorRetainsSinglePrerequisiteProjection()
        {
            var skill = new SkillDefinitionV1("offense.2", "Offense 2", "", 5, "offense.1", 1);

            Assert.That(skill.TreeId, Is.EqualTo("legacy"));
            Assert.That(skill.CategoryId, Is.EqualTo("offense"));
            Assert.That(skill.Prerequisites.Count, Is.EqualTo(1));
            Assert.That(skill.PrerequisiteId, Is.EqualTo("offense.1"));
            Assert.That(skill.PrerequisiteRank, Is.EqualTo(1));
        }

        [Test]
        public void LevelOneStartsWithOneSpendablePoint()
        {
            var authority = new SkillProgressionAuthorityV1(SkillCatalogV1.CreateDefault(), 1);

            Assert.That(authority.CurrentSnapshot.AvailablePoints, Is.EqualTo(1));
            Assert.That(authority.Allocate("op-1", "default.offense.1").Status, Is.EqualTo(SkillMutationStatusV1.Applied));
            Assert.That(authority.CurrentSnapshot.AvailablePoints, Is.Zero);
        }

        [Test]
        public void MultiplePrerequisitesAreRejectedInDeclaredOrder()
        {
            var first = Definition("first", "tree", "offense", 1);
            var second = Definition("second", "tree", "offense", 1);
            var target = Definition(
                "target",
                "tree",
                "utility",
                1,
                new[]
                {
                    new SkillPrerequisiteV1("second", 1),
                    new SkillPrerequisiteV1("first", 1)
                });
            var authority = Authority(10, first, second, target);

            var result = authority.Allocate("target-op", "target");

            Assert.That(result.Status, Is.EqualTo(SkillMutationStatusV1.PrerequisiteMissing));
            Assert.That(result.RejectionCode, Is.EqualTo("skill-prerequisite-missing"));
            Assert.That(result.RejectionReason.RelatedId, Is.EqualTo("second"));
            Assert.That(result.RejectionReason.RequiredValue, Is.EqualTo(1));
            Assert.That(result.RejectionReason.ActualValue, Is.Zero);
        }

        [Test]
        public void RejectedOperationCanBeRetriedAfterPrerequisiteIsMet()
        {
            var prerequisite = Definition("prerequisite", "tree", "offense", 1);
            var target = Definition(
                "target",
                "tree",
                "utility",
                1,
                new[] { new SkillPrerequisiteV1("prerequisite", 1) });
            var authority = Authority(2, prerequisite, target);

            Assert.That(authority.Allocate("retryable", "target").Status, Is.EqualTo(SkillMutationStatusV1.PrerequisiteMissing));
            Assert.That(authority.Allocate("unlock", "prerequisite").Status, Is.EqualTo(SkillMutationStatusV1.Applied));
            Assert.That(authority.Allocate("retryable", "target").Status, Is.EqualTo(SkillMutationStatusV1.Applied));
            Assert.That(authority.CurrentSnapshot.AppliedOperationIds, Is.EquivalentTo(new[] { "retryable", "unlock" }));
        }

        [Test]
        public void CategoryInvestmentGateRequiresEightOffensePoints()
        {
            var offense = Definition("offense.pool", "tree", "offense", 8);
            var target = Definition(
                "offense.capstone",
                "tree",
                "offense",
                1,
                null,
                new[] { new SkillCategoryInvestmentRequirementV1("tree", "offense", 8) });
            var authority = Authority(9, offense, target);

            for (int point = 1; point <= 7; point++)
                Assert.That(authority.Allocate("offense-" + point, "offense.pool").Status, Is.EqualTo(SkillMutationStatusV1.Applied));

            var rejected = authority.Allocate("capstone-op", "offense.capstone");
            Assert.That(rejected.Status, Is.EqualTo(SkillMutationStatusV1.CategoryInvestmentMissing));
            Assert.That(rejected.RejectionCode, Is.EqualTo("skill-category-investment-missing"));
            Assert.That(rejected.RejectionReason.RelatedId, Is.EqualTo("tree/offense"));
            Assert.That(rejected.RejectionReason.RequiredValue, Is.EqualTo(8));
            Assert.That(rejected.RejectionReason.ActualValue, Is.EqualTo(7));

            Assert.That(authority.Allocate("offense-8", "offense.pool").Status, Is.EqualTo(SkillMutationStatusV1.Applied));
            Assert.That(authority.Allocate("capstone-op", "offense.capstone").Status, Is.EqualTo(SkillMutationStatusV1.Applied));
        }

        [Test]
        public void SnapshotProjectsInvestedPointsByExplicitTreeAndCategory()
        {
            var offense = Definition("offense.pool", "tree", "offense", 3);
            var utility = Definition("utility.pool", "tree", "utility", 1);
            var authority = Authority(4, offense, utility);

            authority.Allocate("offense-1", "offense.pool");
            authority.Allocate("offense-2", "offense.pool");
            authority.Allocate("utility-1", "utility.pool");

            Assert.That(authority.CurrentSnapshot.GetInvestedPoints("tree", "offense"), Is.EqualTo(2));
            Assert.That(authority.CurrentSnapshot.GetInvestedPoints("tree", "utility"), Is.EqualTo(1));
            Assert.That(authority.CurrentSnapshot.GetInvestedPoints("other", "offense"), Is.Zero);
        }

        [Test]
        public void DuplicateAppliedOperationDoesNotSpendTwice()
        {
            var authority = new SkillProgressionAuthorityV1(SkillCatalogV1.CreateDefault(), 10);
            authority.Allocate("same", "default.offense.1");

            var duplicate = authority.Allocate("same", "default.offense.1");

            Assert.That(duplicate.Status, Is.EqualTo(SkillMutationStatusV1.DuplicateNoChange));
            Assert.That(authority.CurrentSnapshot.Ranks["default.offense.1"], Is.EqualTo(1));
            Assert.That(authority.CurrentSnapshot.SpentPoints, Is.EqualTo(1));
        }

        [Test]
        public void UnknownCategoryRequirementIsRejectedAtCatalogConstruction()
        {
            var target = Definition(
                "target",
                "tree",
                "utility",
                1,
                null,
                new[] { new SkillCategoryInvestmentRequirementV1("tree", "missing", 1) });

            Assert.Throws<ArgumentException>(() => new SkillCatalogV1(new[] { target }));
        }

        [Test]
        public void PrerequisiteCyclesAreRejectedDeterministically()
        {
            var first = Definition("first", "tree", "offense", 1, new[] { new SkillPrerequisiteV1("second", 1) });
            var second = Definition("second", "tree", "offense", 1, new[] { new SkillPrerequisiteV1("first", 1) });

            var exception = Assert.Throws<ArgumentException>(() => new SkillCatalogV1(new[] { first, second }));
            Assert.That(exception.Message, Does.Contain("cycle"));
        }

        private static SkillDefinitionV1 Definition(
            string id,
            string treeId,
            string categoryId,
            int maxRank,
            SkillPrerequisiteV1[] prerequisites = null,
            SkillCategoryInvestmentRequirementV1[] requirements = null)
        {
            return new SkillDefinitionV1(
                id,
                treeId,
                categoryId,
                id,
                string.Empty,
                maxRank,
                prerequisites,
                requirements);
        }

        private static SkillProgressionAuthorityV1 Authority(int playerLevel, params SkillDefinitionV1[] definitions)
        {
            return new SkillProgressionAuthorityV1(new SkillCatalogV1(definitions), playerLevel);
        }
    }
}

using System;
using NUnit.Framework;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Progression.Skills
{
    public sealed class SkillProgressionStateTests
    {
        [Test]
        public void DefaultCatalogContainsOneFifteenSkillTree()
        {
            var catalog = SkillCatalog.CreateDefault();

            Assert.That(catalog.Trees.Count, Is.EqualTo(1));
            Assert.That(catalog.Trees[0].Id, Is.EqualTo("default"));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(15));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(15));
        }

        [Test]
        public void SpecializedFactoryContainsFiveSkills()
        {
            var catalog = SkillCatalog.CreateSpecializedFiveSkillCatalog("medic", "healing");

            Assert.That(catalog.Trees.Count, Is.EqualTo(1));
            Assert.That(catalog.Trees[0].Id, Is.EqualTo("medic"));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(5));
            Assert.That(catalog.Definitions[0].TreeId, Is.EqualTo("medic"));
            Assert.That(catalog.Definitions[0].CategoryId, Is.EqualTo("healing"));
        }

        [Test]
        public void MixedFixtureSupportsDifferentTreeSizes()
        {
            var catalog = SkillCatalog.CreateMixedTreeFixture();

            Assert.That(catalog.Trees.Count, Is.EqualTo(2));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(15));
            Assert.That(catalog.Trees[1].SkillCount, Is.EqualTo(5));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(20));
        }

        [Test]
        public void ArbitraryPositiveSkillCountsAreAccepted()
        {
            var skill = Definition("solo", "solo-tree", "utility", 1);
            var catalog = new SkillCatalog(new[] { new SkillTreeDefinition("solo-tree", new[] { skill }) });

            Assert.That(catalog.Definitions.Count, Is.EqualTo(1));
            Assert.That(catalog.Trees[0].SkillCount, Is.EqualTo(1));
        }

        [Test]
        public void EmptySkillTreeIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new SkillTreeDefinition("empty", Array.Empty<SkillDefinition>()));
        }

        [Test]
        public void CompatibilityCatalogPreservesOriginalTwentySkillIds()
        {
            var catalog = SkillCatalog.CreateCompatibilityTwentySkillCatalog();
            SkillDefinition offense;
            SkillDefinition utility;

            Assert.That(catalog.Definitions.Count, Is.EqualTo(20));
            Assert.That(catalog.TryGet("offense.1", out offense), Is.True);
            Assert.That(catalog.TryGet("utility.5", out utility), Is.True);
            Assert.That(offense.TreeId, Is.EqualTo("compatibility.20"));
            Assert.That(utility.CategoryId, Is.EqualTo("utility"));
        }

        [Test]
        public void LegacyConstructorRetainsSinglePrerequisiteProjection()
        {
            var skill = new SkillDefinition("offense.2", "Offense 2", "", 5, "offense.1", 1);

            Assert.That(skill.TreeId, Is.EqualTo("legacy"));
            Assert.That(skill.CategoryId, Is.EqualTo("offense"));
            Assert.That(skill.Prerequisites.Count, Is.EqualTo(1));
            Assert.That(skill.PrerequisiteId, Is.EqualTo("offense.1"));
            Assert.That(skill.PrerequisiteRank, Is.EqualTo(1));
        }

        [Test]
        public void LevelOneStartsWithOneSpendablePoint()
        {
            var authority = new SkillProgressionState(SkillCatalog.CreateDefault(), 1);

            Assert.That(authority.CurrentSnapshot.AvailablePoints, Is.EqualTo(1));
            Assert.That(authority.Allocate("op-1", "default.offense.1").Status, Is.EqualTo(SkillMutationStatus.Applied));
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
                    new SkillPrerequisite("second", 1),
                    new SkillPrerequisite("first", 1)
                });
            var authority = Authority(10, first, second, target);

            var result = authority.Allocate("target-op", "target");

            Assert.That(result.Status, Is.EqualTo(SkillMutationStatus.PrerequisiteMissing));
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
                new[] { new SkillPrerequisite("prerequisite", 1) });
            var authority = Authority(2, prerequisite, target);

            Assert.That(authority.Allocate("retryable", "target").Status, Is.EqualTo(SkillMutationStatus.PrerequisiteMissing));
            Assert.That(authority.Allocate("unlock", "prerequisite").Status, Is.EqualTo(SkillMutationStatus.Applied));
            Assert.That(authority.Allocate("retryable", "target").Status, Is.EqualTo(SkillMutationStatus.Applied));
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
                new[] { new SkillCategoryInvestmentRequirement("tree", "offense", 8) });
            var authority = Authority(9, offense, target);

            for (int point = 1; point <= 7; point++)
                Assert.That(authority.Allocate("offense-" + point, "offense.pool").Status, Is.EqualTo(SkillMutationStatus.Applied));

            var rejected = authority.Allocate("capstone-op", "offense.capstone");
            Assert.That(rejected.Status, Is.EqualTo(SkillMutationStatus.CategoryInvestmentMissing));
            Assert.That(rejected.RejectionCode, Is.EqualTo("skill-category-investment-missing"));
            Assert.That(rejected.RejectionReason.RelatedId, Is.EqualTo("tree/offense"));
            Assert.That(rejected.RejectionReason.RequiredValue, Is.EqualTo(8));
            Assert.That(rejected.RejectionReason.ActualValue, Is.EqualTo(7));

            Assert.That(authority.Allocate("offense-8", "offense.pool").Status, Is.EqualTo(SkillMutationStatus.Applied));
            Assert.That(authority.Allocate("capstone-op", "offense.capstone").Status, Is.EqualTo(SkillMutationStatus.Applied));
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
            var authority = new SkillProgressionState(SkillCatalog.CreateDefault(), 10);
            authority.Allocate("same", "default.offense.1");

            var duplicate = authority.Allocate("same", "default.offense.1");

            Assert.That(duplicate.Status, Is.EqualTo(SkillMutationStatus.DuplicateNoChange));
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
                new[] { new SkillCategoryInvestmentRequirement("tree", "missing", 1) });

            Assert.Throws<ArgumentException>(() => new SkillCatalog(new[] { target }));
        }

        [Test]
        public void PrerequisiteCyclesAreRejectedDeterministically()
        {
            var first = Definition("first", "tree", "offense", 1, new[] { new SkillPrerequisite("second", 1) });
            var second = Definition("second", "tree", "offense", 1, new[] { new SkillPrerequisite("first", 1) });

            var exception = Assert.Throws<ArgumentException>(() => new SkillCatalog(new[] { first, second }));
            Assert.That(exception.Message, Does.Contain("cycle"));
        }

        private static SkillDefinition Definition(
            string id,
            string treeId,
            string categoryId,
            int maxRank,
            SkillPrerequisite[] prerequisites = null,
            SkillCategoryInvestmentRequirement[] requirements = null)
        {
            return new SkillDefinition(
                id,
                treeId,
                categoryId,
                id,
                string.Empty,
                maxRank,
                prerequisites,
                requirements);
        }

        private static SkillProgressionState Authority(int playerLevel, params SkillDefinition[] definitions)
        {
            return new SkillProgressionState(new SkillCatalog(definitions), playerLevel);
        }
    }
}

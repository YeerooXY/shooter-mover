using NUnit.Framework;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Progression.Skills
{
    public sealed class SkillProgressionAuthorityTests
    {
        [Test]
        public void DefaultCatalogContainsExactlyTwentySkills()
        {
            Assert.That(SkillCatalogV1.CreateDefault().Definitions.Count, Is.EqualTo(20));
        }

        [Test]
        public void LevelOneStartsWithOneSpendablePoint()
        {
            var authority = new SkillProgressionAuthorityV1(SkillCatalogV1.CreateDefault(), 1);
            Assert.That(authority.CurrentSnapshot.AvailablePoints, Is.EqualTo(1));
            Assert.That(authority.Allocate("op-1", "offense.1").Status, Is.EqualTo(SkillMutationStatusV1.Applied));
            Assert.That(authority.CurrentSnapshot.AvailablePoints, Is.Zero);
        }

        [Test]
        public void DuplicateOperationDoesNotSpendTwice()
        {
            var authority = new SkillProgressionAuthorityV1(SkillCatalogV1.CreateDefault(), 10);
            authority.Allocate("same", "offense.1");
            var duplicate = authority.Allocate("same", "offense.1");
            Assert.That(duplicate.Status, Is.EqualTo(SkillMutationStatusV1.DuplicateNoChange));
            Assert.That(authority.CurrentSnapshot.Ranks["offense.1"], Is.EqualTo(1));
        }

        [Test]
        public void PrerequisiteIsEnforced()
        {
            var authority = new SkillProgressionAuthorityV1(SkillCatalogV1.CreateDefault(), 10);
            Assert.That(authority.Allocate("op", "offense.2").Status, Is.EqualTo(SkillMutationStatusV1.PrerequisiteMissing));
        }
    }
}
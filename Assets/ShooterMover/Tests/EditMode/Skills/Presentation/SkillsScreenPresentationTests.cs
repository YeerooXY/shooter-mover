using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Skills.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Skills.Presentation
{
    public sealed class SkillsScreenPresentationTests
    {
        [Test]
        public void Projection_DisplaysXpTotalsAndAllDefinitionFields()
        {
            PlayerExperience experience = CreateExperience(7);
            var skills = new SkillProgressionState(
                SkillCatalog.CreateDefault(),
                1);
            PlayerRouteProfilePayload route = CreateRoute();
            var session = new SkillsScreenSession(route, experience, skills);

            SkillsScreenView projection = session.CurrentProjection;
            SkillsScreenSkillView offenseOne;

            Assert.That(projection.RoutePayload, Is.SameAs(route));
            Assert.That(projection.PlayerLevel, Is.EqualTo(7));
            Assert.That(projection.TotalSkillPoints, Is.EqualTo(7));
            Assert.That(projection.SpentSkillPoints, Is.Zero);
            Assert.That(projection.AvailableSkillPoints, Is.EqualTo(7));
            Assert.That(projection.Skills.Count, Is.EqualTo(20));
            Assert.That(projection.TryGetSkill("offense.1", out offenseOne), Is.True);
            Assert.That(offenseOne.SkillId, Is.EqualTo("offense.1"));
            Assert.That(offenseOne.DisplayName, Is.Not.Empty);
            Assert.That(offenseOne.Description, Is.Not.Empty);
            Assert.That(offenseOne.PrerequisiteLabel, Is.EqualTo("None"));
            Assert.That(offenseOne.CurrentRank, Is.Zero);
            Assert.That(offenseOne.MaximumRank, Is.EqualTo(5));
            Assert.That(offenseOne.State, Is.EqualTo(SkillsScreenSkillState.Available));
            Assert.That(offenseOne.CanAllocate, Is.True);
        }

        [Test]
        public void Allocation_UsesRealAuthorityAndDuplicateOperationDoesNotSpendTwice()
        {
            PlayerExperience experience = CreateExperience(4);
            var skills = new SkillProgressionState(
                SkillCatalog.CreateDefault(),
                experience.CurrentState.Level);
            var session = new SkillsScreenSession(CreateRoute(), experience, skills);

            SkillsScreenAllocationResult applied = session.Allocate(
                "skill-operation.same",
                "offense.1");
            SkillsScreenAllocationResult duplicate = session.Allocate(
                "skill-operation.same",
                "offense.1");
            SkillsScreenSkillView projected;

            Assert.That(applied.MutationFact.Status, Is.EqualTo(SkillMutationStatus.Applied));
            Assert.That(duplicate.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.DuplicateNoChange));
            Assert.That(duplicate.Projection.SpentSkillPoints, Is.EqualTo(1));
            Assert.That(duplicate.Projection.AvailableSkillPoints, Is.EqualTo(3));
            Assert.That(duplicate.Projection.SkillAuthoritySequence, Is.EqualTo(1L));
            Assert.That(duplicate.Projection.TryGetSkill("offense.1", out projected), Is.True);
            Assert.That(projected.CurrentRank, Is.EqualTo(1));
            Assert.That(projected.State, Is.EqualTo(SkillsScreenSkillState.Purchased));
        }

        [Test]
        public void Projection_ReportsLockedAvailablePurchasedAndCappedStates()
        {
            PlayerExperience experience = CreateExperience(10);
            var skills = new SkillProgressionState(
                SkillCatalog.CreateDefault(),
                experience.CurrentState.Level);
            var session = new SkillsScreenSession(CreateRoute(), experience, skills);

            SkillsScreenSkillView offenseOne;
            SkillsScreenSkillView offenseTwo;
            Assert.That(session.CurrentProjection.TryGetSkill("offense.1", out offenseOne), Is.True);
            Assert.That(session.CurrentProjection.TryGetSkill("offense.2", out offenseTwo), Is.True);
            Assert.That(offenseOne.State, Is.EqualTo(SkillsScreenSkillState.Available));
            Assert.That(offenseTwo.State, Is.EqualTo(SkillsScreenSkillState.Locked));

            session.Allocate("skill-operation.offense-1-rank-1", "offense.1");
            Assert.That(session.CurrentProjection.TryGetSkill("offense.1", out offenseOne), Is.True);
            Assert.That(session.CurrentProjection.TryGetSkill("offense.2", out offenseTwo), Is.True);
            Assert.That(offenseOne.State, Is.EqualTo(SkillsScreenSkillState.Purchased));
            Assert.That(offenseTwo.State, Is.EqualTo(SkillsScreenSkillState.Available));
            Assert.That(offenseTwo.PrerequisiteSatisfied, Is.True);

            for (int rank = 2; rank <= 5; rank++)
            {
                session.Allocate("skill-operation.offense-1-rank-" + rank, "offense.1");
            }

            Assert.That(session.CurrentProjection.TryGetSkill("offense.1", out offenseOne), Is.True);
            Assert.That(offenseOne.State, Is.EqualTo(SkillsScreenSkillState.Capped));
            Assert.That(offenseOne.CanAllocate, Is.False);
            Assert.That(offenseOne.AllocationBlockCode, Is.EqualTo("skill-rank-capped"));
        }

        [Test]
        public void Allocation_ReportsMissingPrerequisite()
        {
            var session = new SkillsScreenSession(
                CreateRoute(),
                CreateExperience(5),
                new SkillProgressionState(SkillCatalog.CreateDefault(), 5));

            SkillsScreenAllocationResult result = session.Allocate(
                "skill-operation.missing-prerequisite",
                "offense.2");

            Assert.That(result.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.PrerequisiteMissing));
            Assert.That(result.MutationFact.RejectionCode, Is.EqualTo(
                "skill-prerequisite-missing"));
            Assert.That(result.Projection.SpentSkillPoints, Is.Zero);
        }

        [Test]
        public void Allocation_ReportsInsufficientPoints()
        {
            PlayerExperience experience = CreateExperience(1);
            var session = new SkillsScreenSession(
                CreateRoute(),
                experience,
                new SkillProgressionState(SkillCatalog.CreateDefault(), 1));

            session.Allocate("skill-operation.first-point", "defense.1");
            SkillsScreenAllocationResult result = session.Allocate(
                "skill-operation.no-points",
                "offense.1");
            SkillsScreenSkillView offenseOne;

            Assert.That(result.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.InsufficientPoints));
            Assert.That(result.Projection.AvailableSkillPoints, Is.Zero);
            Assert.That(result.Projection.TryGetSkill("offense.1", out offenseOne), Is.True);
            Assert.That(offenseOne.State, Is.EqualTo(SkillsScreenSkillState.Available));
            Assert.That(offenseOne.CanAllocate, Is.False);
            Assert.That(offenseOne.AllocationBlockCode, Is.EqualTo(
                "skill-points-insufficient"));
        }

        [Test]
        public void Allocation_ReportsMaxRankWithoutChangingSequence()
        {
            PlayerExperience experience = CreateExperience(10);
            var session = new SkillsScreenSession(
                CreateRoute(),
                experience,
                new SkillProgressionState(SkillCatalog.CreateDefault(), 10));

            for (int rank = 1; rank <= 5; rank++)
            {
                session.Allocate("skill-operation.cap-" + rank, "utility.1");
            }

            SkillsScreenAllocationResult capped = session.Allocate(
                "skill-operation.cap-rejected",
                "utility.1");

            Assert.That(capped.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.RankCapped));
            Assert.That(capped.MutationFact.PreviousRank, Is.EqualTo(5));
            Assert.That(capped.MutationFact.CurrentRank, Is.EqualTo(5));
            Assert.That(capped.Projection.SkillAuthoritySequence, Is.EqualTo(5L));
        }

        [Test]
        public void BackAndRevisit_PreserveExactPayloadAndAuthorityState()
        {
            PlayerExperience experience = CreateExperience(3);
            var skills = new SkillProgressionState(SkillCatalog.CreateDefault(), 3);
            PlayerRouteProfilePayload route = CreateRoute();
            string originalFingerprint = route.Fingerprint;
            var firstVisit = new SkillsScreenSession(route, experience, skills);

            firstVisit.Allocate("skill-operation.visit-one", "mobility.1");
            SkillsScreenBackResult back = firstVisit.Back();
            var revisit = new SkillsScreenSession(route, experience, skills);
            SkillsScreenSkillView mobilityOne;

            Assert.That(back.RoutePayload, Is.SameAs(route));
            Assert.That(back.Projection.RoutePayload, Is.SameAs(route));
            Assert.That(route.Fingerprint, Is.EqualTo(originalFingerprint));
            Assert.That(revisit.CurrentProjection.RoutePayload, Is.SameAs(route));
            Assert.That(revisit.CurrentProjection.TryGetSkill("mobility.1", out mobilityOne), Is.True);
            Assert.That(mobilityOne.CurrentRank, Is.EqualTo(1));
            Assert.That(revisit.CurrentProjection.AvailableSkillPoints, Is.EqualTo(2));
        }

        private static PlayerExperience CreateExperience(int level)
        {
            var curve = new PlayerExperienceCurve(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
            var authority = new PlayerExperience(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.skills-screen-tests"),
                    1,
                    new List<StableId>()));
            if (level > 1)
            {
                authority.Grant(new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.skills-screen-level-" + level),
                    (level - 1L) * 100L));
            }

            return authority;
        }

        private static PlayerRouteProfilePayload CreateRoute()
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character.skills-screen-tests"),
                StableId.Parse("loadout-profile.skills-screen-tests"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.skills-screen-1"),
                    StableId.Parse("equipment-instance.skills-screen-2"),
                    StableId.Parse("equipment-instance.skills-screen-3"),
                    StableId.Parse("equipment-instance.skills-screen-4"),
                });
        }
    }
}

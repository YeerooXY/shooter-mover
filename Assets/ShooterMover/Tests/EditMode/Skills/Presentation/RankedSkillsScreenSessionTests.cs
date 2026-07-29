using System;
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
    public sealed class RankedSkillsScreenSessionTests
    {
        [Test]
        public void Projection_UsesXpAwardedPointsAndRankedV2State()
        {
            PlayerExperienceState experience = CreateExperience(4);
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            RankedSkillAllocationState authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence();

            RankedSkillsScreenSession session = CreateSession(
                experience,
                authority,
                persistence);
            SkillsScreenSkillView gated;

            Assert.That(session.CurrentProjection.PlayerLevel, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.TotalSkillPoints, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.AvailableSkillPoints, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.SpentSkillPoints, Is.Zero);
            Assert.That(session.CurrentProjection.Skills.Count, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.TryGetSkill(
                "striker.movement_efficiency",
                out gated),
                Is.True);
            Assert.That(gated.State, Is.EqualTo(SkillsScreenSkillState.Locked));
            Assert.That(gated.AllocationBlockCode, Is.EqualTo(
                "skill-prerequisite-missing"));
            Assert.That(persistence.CallCount, Is.Zero);
        }

        [Test]
        public void AcceptedAllocation_PersistsRefreshesAndRestoresOnRevisit()
        {
            PlayerExperienceState experience = CreateExperience(3);
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            RankedSkillAllocationState authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence();
            RankedSkillsScreenSession session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResult applied = session.Allocate(
                "generic.movement_speed");
            SkillsScreenSkillView projected;

            Assert.That(applied.Changed, Is.True);
            Assert.That(applied.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.Applied));
            Assert.That(persistence.CallCount, Is.EqualTo(1));
            Assert.That(session.CurrentProjection.TryGetSkill(
                "generic.movement_speed",
                out projected),
                Is.True);
            Assert.That(projected.CurrentRank, Is.EqualTo(1));
            Assert.That(session.CurrentProjection.AvailableSkillPoints, Is.EqualTo(2));

            RankedSkillsScreenSession revisit = CreateSession(
                experience,
                authority,
                persistence);
            Assert.That(revisit.CurrentProjection.TryGetSkill(
                "generic.movement_speed",
                out projected),
                Is.True);
            Assert.That(projected.CurrentRank, Is.EqualTo(1));

            var restoredAuthority = new RankedSkillAllocationState(catalog);
            restoredAuthority.Seed(authority.Get("profile.skills-v2-tests"));
            RankedSkillsScreenSession restarted = CreateSession(
                experience,
                restoredAuthority,
                new RecordingPersistence());
            Assert.That(restarted.CurrentProjection.TryGetSkill(
                "generic.movement_speed",
                out projected),
                Is.True);
            Assert.That(projected.CurrentRank, Is.EqualTo(1));
        }

        [Test]
        public void InvalidPrerequisiteAndCap_AreRejectedWithoutPersistence()
        {
            PlayerExperienceState experience = CreateExperience(10);
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            RankedSkillAllocationState authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence();
            RankedSkillsScreenSession session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResult prerequisite = session.Allocate(
                "striker.movement_efficiency");
            Assert.That(prerequisite.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.PrerequisiteMissing));
            Assert.That(prerequisite.MutationFact.RejectionCode, Is.EqualTo(
                "skill-prerequisite-missing"));
            Assert.That(persistence.CallCount, Is.Zero);

            for (int rank = 1; rank <= 6; rank++)
            {
                Assert.That(session.Allocate("generic.armor").Changed, Is.True);
            }
            SkillsScreenAllocationResult capped = session.Allocate(
                "generic.armor");
            Assert.That(capped.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.RankCapped));
            Assert.That(capped.MutationFact.RejectionCode, Is.EqualTo(
                "skill-rank-capped"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.armor"), Is.EqualTo(6));
            Assert.That(persistence.CallCount, Is.EqualTo(6));
        }

        [Test]
        public void RejectedPersistence_RollsBackReceiptAndRetryAppliesOnce()
        {
            PlayerExperienceState experience = CreateExperience(2);
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            RankedSkillAllocationState authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence
            {
                RejectNext = true,
            };
            RankedSkillsScreenSession session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResult rejected = session.Allocate(
                "generic.movement_speed");
            Assert.That(rejected.Changed, Is.False);
            Assert.That(rejected.MutationFact.RejectionCode, Does.StartWith(
                "skills-v2-persistence-rejected:"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.movement_speed"), Is.Zero);

            SkillsScreenAllocationResult retried = session.Allocate(
                "generic.movement_speed");
            Assert.That(retried.Changed, Is.True);
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.movement_speed"), Is.EqualTo(1));
            Assert.That(persistence.CallCount, Is.EqualTo(2));
        }

        [Test]
        public void ThrowingPersistence_RollsBackAuthoritativeRank()
        {
            PlayerExperienceState experience = CreateExperience(2);
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            RankedSkillAllocationState authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence
            {
                ThrowNext = true,
            };
            RankedSkillsScreenSession session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResult rejected = session.Allocate(
                "generic.movement_speed");

            Assert.That(rejected.Changed, Is.False);
            Assert.That(rejected.MutationFact.RejectionCode, Does.StartWith(
                "skills-v2-persistence-threw:"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.movement_speed"), Is.Zero);
        }

        [Test]
        public void UnverifiedCommit_DoesNotRollBackOrRetryInSameSession()
        {
            PlayerExperienceState experience = CreateExperience(2);
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            RankedSkillAllocationState authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence
            {
                ReturnUnverifiedCommitNext = true,
            };
            RankedSkillsScreenSession session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResult uncertain = session.Allocate(
                "generic.movement_speed");

            Assert.That(uncertain.Changed, Is.False);
            Assert.That(uncertain.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.InvalidRequest));
            Assert.That(uncertain.MutationFact.RejectionCode, Does.Contain(
                "skills-v2-persistence-commit-unverified"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.movement_speed"), Is.EqualTo(1));
            Assert.That(session.MutationBlocked, Is.True);

            SkillsScreenAllocationResult blocked = session.Allocate(
                "generic.armor");
            Assert.That(blocked.Changed, Is.False);
            Assert.That(blocked.MutationFact.RejectionCode, Does.Contain(
                "skills-v2-persistence-commit-unverified"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.armor"), Is.Zero);
            Assert.That(persistence.CallCount, Is.EqualTo(1));
            Assert.That(authority.IsCommitUnverified(
                "profile.skills-v2-tests"), Is.True);
            SkillAllocationResult direct = authority.Allocate(
                new AllocateSkillRankCommand(
                    "operation.skills-v2-quarantine-proof",
                    "profile.skills-v2-tests",
                    "generic.armor",
                    authority.Get("profile.skills-v2-tests").Version,
                    experience.CurrentState.TotalSkillPointsAwarded));
            Assert.That(direct.Accepted, Is.False);
            Assert.That(direct.Rejection, Is.EqualTo(
                SkillAllocationRejection.CommitUnverified));

            RankedSkillsScreenSession reopened;
            string reopenCode;
            Assert.That(RankedSkillsScreenSession.TryCreate(
                CreateRoute(),
                experience,
                authority,
                "profile.skills-v2-tests",
                new RecordingPersistence(),
                out reopened,
                out reopenCode),
                Is.False);
            Assert.That(reopened, Is.Null);
            Assert.That(reopenCode, Is.EqualTo(
                "skills-v2-persistence-commit-unverified"));

            authority.Seed(authority.Get("profile.skills-v2-tests"));
            Assert.That(authority.IsCommitUnverified(
                "profile.skills-v2-tests"), Is.False);
            Assert.That(CreateSession(
                experience,
                authority,
                new RecordingPersistence()), Is.Not.Null);
        }

        [Test]
        public void StaleCatalogVersion_FailsClosedWithoutSession()
        {
            PlayerExperienceState experience = CreateExperience(2);
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            var authority = new RankedSkillAllocationState(catalog);
            authority.Seed(new RankedSkillAllocationSnapshot(
                "profile.skills-v2-tests",
                "striker",
                0L,
                "stale-schema",
                "stale-content",
                null));

            RankedSkillsScreenSession session;
            string rejectionCode;
            bool created = RankedSkillsScreenSession.TryCreate(
                CreateRoute(),
                experience,
                authority,
                "profile.skills-v2-tests",
                new RecordingPersistence(),
                out session,
                out rejectionCode);

            Assert.That(created, Is.False);
            Assert.That(session, Is.Null);
            Assert.That(rejectionCode, Is.EqualTo(
                "skills-v2-definition-version-stale"));
        }

        private static RankedSkillsScreenSession CreateSession(
            PlayerExperienceState experience,
            RankedSkillAllocationState authority,
            IRankedSkillsPersistencePort persistence)
        {
            RankedSkillsScreenSession session;
            string rejectionCode;
            Assert.That(RankedSkillsScreenSession.TryCreate(
                CreateRoute(),
                experience,
                authority,
                "profile.skills-v2-tests",
                persistence,
                out session,
                out rejectionCode),
                Is.True,
                rejectionCode);
            return session;
        }

        private static RankedSkillAllocationState CreateAuthority(
            RankedSkillCatalog catalog)
        {
            var authority = new RankedSkillAllocationState(catalog);
            authority.Seed(RankedSkillAllocationSnapshot.Empty(
                "profile.skills-v2-tests",
                "striker",
                catalog));
            return authority;
        }

        private static PlayerExperienceState CreateExperience(int level)
        {
            var curve = new PlayerExperienceCurve(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
            var authority = new PlayerExperienceState(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.skills-v2-tests"),
                    1,
                    new List<StableId>()));
            if (level > 1)
            {
                authority.Grant(new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.skills-v2-level-" + level),
                    (level - 1L) * 100L));
            }
            return authority;
        }

        private static PlayerRouteProfilePayload CreateRoute()
        {
            return PlayerRouteProfilePayload.Create(
                StableId.Parse("character.skills-v2-tests"),
                StableId.Parse("class.skills-v2-tests"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.skills-v2-1"),
                    StableId.Parse("equipment-instance.skills-v2-2"),
                    StableId.Parse("equipment-instance.skills-v2-3"),
                    StableId.Parse("equipment-instance.skills-v2-4"),
                });
        }

        private sealed class RecordingPersistence :
            IRankedSkillsPersistencePort
        {
            public int CallCount { get; private set; }
            public bool RejectNext { get; set; }
            public bool ThrowNext { get; set; }
            public bool ReturnUnverifiedCommitNext { get; set; }

            public RankedSkillsPersistenceResult Persist(
                string mutationScope,
                string immutableMutationFingerprint)
            {
                CallCount++;
                if (ThrowNext)
                {
                    ThrowNext = false;
                    throw new InvalidOperationException("simulated-store-failure");
                }
                if (ReturnUnverifiedCommitNext)
                {
                    ReturnUnverifiedCommitNext = false;
                    return new RankedSkillsPersistenceResult(
                        false,
                        "simulated-post-commit-verification-failure",
                        false);
                }
                if (RejectNext)
                {
                    RejectNext = false;
                    return new RankedSkillsPersistenceResult(
                        false,
                        "simulated-store-rejection");
                }
                return new RankedSkillsPersistenceResult(true, string.Empty);
            }
        }
    }
}

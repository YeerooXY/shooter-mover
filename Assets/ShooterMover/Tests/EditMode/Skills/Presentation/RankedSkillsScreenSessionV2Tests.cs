using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Skills.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Skills.Presentation
{
    public sealed class RankedSkillsScreenSessionV2Tests
    {
        [Test]
        public void Projection_UsesXpAwardedPointsAndRankedV2State()
        {
            PlayerExperienceAuthorityV1 experience = CreateExperience(4);
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            RankedSkillAllocationAuthorityV2 authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence();

            RankedSkillsScreenSessionV2 session = CreateSession(
                experience,
                authority,
                persistence);
            SkillsScreenSkillProjectionV1 gated;

            Assert.That(session.CurrentProjection.PlayerLevel, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.TotalSkillPoints, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.AvailableSkillPoints, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.SpentSkillPoints, Is.Zero);
            Assert.That(session.CurrentProjection.Skills.Count, Is.EqualTo(4));
            Assert.That(session.CurrentProjection.TryGetSkill(
                "striker.movement_efficiency",
                out gated),
                Is.True);
            Assert.That(gated.State, Is.EqualTo(SkillsScreenSkillStateV1.Locked));
            Assert.That(gated.AllocationBlockCode, Is.EqualTo(
                "skill-prerequisite-missing"));
            Assert.That(persistence.CallCount, Is.Zero);
        }

        [Test]
        public void AcceptedAllocation_PersistsRefreshesAndRestoresOnRevisit()
        {
            PlayerExperienceAuthorityV1 experience = CreateExperience(3);
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            RankedSkillAllocationAuthorityV2 authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence();
            RankedSkillsScreenSessionV2 session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResultV1 applied = session.Allocate(
                "generic.movement_speed");
            SkillsScreenSkillProjectionV1 projected;

            Assert.That(applied.Changed, Is.True);
            Assert.That(applied.MutationFact.Status, Is.EqualTo(
                SkillMutationStatusV1.Applied));
            Assert.That(persistence.CallCount, Is.EqualTo(1));
            Assert.That(session.CurrentProjection.TryGetSkill(
                "generic.movement_speed",
                out projected),
                Is.True);
            Assert.That(projected.CurrentRank, Is.EqualTo(1));
            Assert.That(session.CurrentProjection.AvailableSkillPoints, Is.EqualTo(2));

            RankedSkillsScreenSessionV2 revisit = CreateSession(
                experience,
                authority,
                persistence);
            Assert.That(revisit.CurrentProjection.TryGetSkill(
                "generic.movement_speed",
                out projected),
                Is.True);
            Assert.That(projected.CurrentRank, Is.EqualTo(1));

            var restoredAuthority = new RankedSkillAllocationAuthorityV2(catalog);
            restoredAuthority.Seed(authority.Get("profile.skills-v2-tests"));
            RankedSkillsScreenSessionV2 restarted = CreateSession(
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
            PlayerExperienceAuthorityV1 experience = CreateExperience(10);
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            RankedSkillAllocationAuthorityV2 authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence();
            RankedSkillsScreenSessionV2 session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResultV1 prerequisite = session.Allocate(
                "striker.movement_efficiency");
            Assert.That(prerequisite.MutationFact.Status, Is.EqualTo(
                SkillMutationStatusV1.PrerequisiteMissing));
            Assert.That(prerequisite.MutationFact.RejectionCode, Is.EqualTo(
                "skill-prerequisite-missing"));
            Assert.That(persistence.CallCount, Is.Zero);

            for (int rank = 1; rank <= 6; rank++)
            {
                Assert.That(session.Allocate("generic.armor").Changed, Is.True);
            }
            SkillsScreenAllocationResultV1 capped = session.Allocate(
                "generic.armor");
            Assert.That(capped.MutationFact.Status, Is.EqualTo(
                SkillMutationStatusV1.RankCapped));
            Assert.That(capped.MutationFact.RejectionCode, Is.EqualTo(
                "skill-rank-capped"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.armor"), Is.EqualTo(6));
            Assert.That(persistence.CallCount, Is.EqualTo(6));
        }

        [Test]
        public void RejectedPersistence_RollsBackReceiptAndRetryAppliesOnce()
        {
            PlayerExperienceAuthorityV1 experience = CreateExperience(2);
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            RankedSkillAllocationAuthorityV2 authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence
            {
                RejectNext = true,
            };
            RankedSkillsScreenSessionV2 session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResultV1 rejected = session.Allocate(
                "generic.movement_speed");
            Assert.That(rejected.Changed, Is.False);
            Assert.That(rejected.MutationFact.RejectionCode, Does.StartWith(
                "skills-v2-persistence-rejected:"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.movement_speed"), Is.Zero);

            SkillsScreenAllocationResultV1 retried = session.Allocate(
                "generic.movement_speed");
            Assert.That(retried.Changed, Is.True);
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.movement_speed"), Is.EqualTo(1));
            Assert.That(persistence.CallCount, Is.EqualTo(2));
        }

        [Test]
        public void ThrowingPersistence_RollsBackAuthoritativeRank()
        {
            PlayerExperienceAuthorityV1 experience = CreateExperience(2);
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            RankedSkillAllocationAuthorityV2 authority = CreateAuthority(catalog);
            var persistence = new RecordingPersistence
            {
                ThrowNext = true,
            };
            RankedSkillsScreenSessionV2 session = CreateSession(
                experience,
                authority,
                persistence);

            SkillsScreenAllocationResultV1 rejected = session.Allocate(
                "generic.movement_speed");

            Assert.That(rejected.Changed, Is.False);
            Assert.That(rejected.MutationFact.RejectionCode, Does.StartWith(
                "skills-v2-persistence-threw:"));
            Assert.That(authority.Get("profile.skills-v2-tests")
                .RankOf("generic.movement_speed"), Is.Zero);
        }

        [Test]
        public void StaleCatalogVersion_FailsClosedWithoutSession()
        {
            PlayerExperienceAuthorityV1 experience = CreateExperience(2);
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            var authority = new RankedSkillAllocationAuthorityV2(catalog);
            authority.Seed(new RankedSkillAllocationSnapshotV2(
                "profile.skills-v2-tests",
                "striker",
                0L,
                "stale-schema",
                "stale-content",
                null));

            RankedSkillsScreenSessionV2 session;
            string rejectionCode;
            bool created = RankedSkillsScreenSessionV2.TryCreate(
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

        private static RankedSkillsScreenSessionV2 CreateSession(
            PlayerExperienceAuthorityV1 experience,
            RankedSkillAllocationAuthorityV2 authority,
            IRankedSkillsPersistencePortV2 persistence)
        {
            RankedSkillsScreenSessionV2 session;
            string rejectionCode;
            Assert.That(RankedSkillsScreenSessionV2.TryCreate(
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

        private static RankedSkillAllocationAuthorityV2 CreateAuthority(
            RankedSkillCatalogV2 catalog)
        {
            var authority = new RankedSkillAllocationAuthorityV2(catalog);
            authority.Seed(RankedSkillAllocationSnapshotV2.Empty(
                "profile.skills-v2-tests",
                "striker",
                catalog));
            return authority;
        }

        private static PlayerExperienceAuthorityV1 CreateExperience(int level)
        {
            var curve = new PlayerExperienceCurveV1(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
            var authority = new PlayerExperienceAuthorityV1(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.skills-v2-tests"),
                    1,
                    new List<StableId>()));
            if (level > 1)
            {
                authority.Grant(new PlayerExperienceGrantRequestV1(
                    StableId.Parse("xp-source.skills-v2-level-" + level),
                    (level - 1L) * 100L));
            }
            return authority;
        }

        private static PlayerRouteProfilePayloadV1 CreateRoute()
        {
            return PlayerRouteProfilePayloadV1.Create(
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
            IRankedSkillsPersistencePortV2
        {
            public int CallCount { get; private set; }
            public bool RejectNext { get; set; }
            public bool ThrowNext { get; set; }

            public RankedSkillsPersistenceResultV2 Persist(
                string mutationScope,
                string immutableMutationFingerprint)
            {
                CallCount++;
                if (ThrowNext)
                {
                    ThrowNext = false;
                    throw new InvalidOperationException("simulated-store-failure");
                }
                if (RejectNext)
                {
                    RejectNext = false;
                    return new RankedSkillsPersistenceResultV2(
                        false,
                        "simulated-store-rejection");
                }
                return new RankedSkillsPersistenceResultV2(true, string.Empty);
            }
        }
    }
}

using System.Collections;
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
using ShooterMover.UI.Skills;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.Skills
{
    public sealed class RankedSkillsSceneControllerV2Tests
    {
        [UnityTest]
        public IEnumerator Controller_UsesRankedSessionAndSurfacesRejectedPrerequisite()
        {
            GameObject host = new GameObject("SKILLS-V2-LIVE-001 controller test");
            SkillsSceneController controller = host.AddComponent<SkillsSceneController>();
            RankedSkillCatalogV2 catalog = RankedSkillSampleCatalogV2.Create();
            var authority = new RankedSkillAllocationAuthorityV2(catalog);
            authority.Seed(RankedSkillAllocationSnapshotV2.Empty(
                "profile.skills-v2-controller",
                "striker",
                catalog));
            RankedSkillsScreenSessionV2 session;
            string rejectionCode;
            Assert.That(RankedSkillsScreenSessionV2.TryCreate(
                CreateRoute(),
                CreateExperience(3),
                authority,
                "profile.skills-v2-controller",
                new SuccessfulPersistence(),
                out session,
                out rejectionCode),
                Is.True,
                rejectionCode);
            var navigation = new CaptureNavigationPort();
            controller.ConfigureRankedV2ForTests(session, navigation);

            SkillsScreenAllocationResultV1 rejected =
                controller.AllocateRankedSkill("striker.movement_efficiency");
            SkillsScreenAllocationResultV1 applied =
                controller.AllocateRankedSkill("generic.movement_speed");

            Assert.That(controller.IsRankedV2Connected, Is.True);
            Assert.That(rejected.MutationFact.Status, Is.EqualTo(
                SkillMutationStatusV1.PrerequisiteMissing));
            Assert.That(rejected.MutationFact.RejectionCode, Is.EqualTo(
                "skill-prerequisite-missing"));
            Assert.That(applied.Changed, Is.True);
            Assert.That(controller.CurrentProjection.SpentSkillPoints, Is.EqualTo(1));
            Assert.That(navigation.ReturnCount, Is.Zero);

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnavailableState_HasNoProjectionAndReturnsExactPayload()
        {
            GameObject host = new GameObject("SKILLS-V2-LIVE-001 unavailable test");
            SkillsSceneController controller = host.AddComponent<SkillsSceneController>();
            PlayerRouteProfilePayloadV1 route = CreateRoute();
            var navigation = new CaptureNavigationPort();

            controller.ShowUnavailable(
                route,
                navigation,
                "skills-v2-active-character-graph-unavailable");

            Assert.That(controller.IsDisconnected, Is.True);
            Assert.That(controller.CurrentProjection, Is.Null);
            Assert.That(controller.UnavailableReason, Is.EqualTo(
                "skills-v2-active-character-graph-unavailable"));
            Assert.That(controller.Back(), Is.True);
            Assert.That(navigation.LastPayload, Is.SameAs(route));

            Object.Destroy(host);
            yield return null;
        }

        private sealed class SuccessfulPersistence :
            IRankedSkillsPersistencePortV2
        {
            public RankedSkillsPersistenceResultV2 Persist(
                string mutationScope,
                string immutableMutationFingerprint)
            {
                return new RankedSkillsPersistenceResultV2(true, string.Empty);
            }
        }

        private sealed class CaptureNavigationPort :
            ISkillsScreenNavigationPortV1
        {
            public int ReturnCount { get; private set; }
            public PlayerRouteProfilePayloadV1 LastPayload { get; private set; }

            public void ReturnToHub(PlayerRouteProfilePayloadV1 routePayload)
            {
                ReturnCount++;
                LastPayload = routePayload;
            }
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
                    StableId.Parse("difficulty.skills-v2-controller"),
                    1,
                    new List<StableId>()));
            if (level > 1)
            {
                authority.Grant(new PlayerExperienceGrantRequestV1(
                    StableId.Parse("xp-source.skills-v2-controller-" + level),
                    (level - 1L) * 100L));
            }
            return authority;
        }

        private static PlayerRouteProfilePayloadV1 CreateRoute()
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.skills-v2-controller"),
                StableId.Parse("class.skills-v2-controller"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.skills-v2-controller-1"),
                    StableId.Parse("equipment-instance.skills-v2-controller-2"),
                    StableId.Parse("equipment-instance.skills-v2-controller-3"),
                    StableId.Parse("equipment-instance.skills-v2-controller-4"),
                });
        }
    }
}

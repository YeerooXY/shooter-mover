using System.Collections;
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
using ShooterMover.UI.Skills;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Flow.Skills
{
    public sealed class RankedSkillsMenuTests
    {
        [UnityTest]
        public IEnumerator Controller_UsesRankedSessionAndSurfacesRejectedPrerequisite()
        {
            GameObject host = new GameObject("SKILLS-V2-LIVE-001 controller test");
            SkillsMenu controller = host.AddComponent<SkillsMenu>();
            RankedSkillCatalog catalog = RankedSkillSampleCatalog.Create();
            var authority = new RankedSkillAllocationState(catalog);
            authority.Seed(RankedSkillAllocationSnapshot.Empty(
                "profile.skills-v2-controller",
                "striker",
                catalog));
            RankedSkillsScreenSession session;
            string rejectionCode;
            Assert.That(RankedSkillsScreenSession.TryCreate(
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

            SkillsScreenAllocationResult rejected =
                controller.AllocateRankedSkill("striker.movement_efficiency");
            SkillsScreenAllocationResult applied =
                controller.AllocateRankedSkill("generic.movement_speed");

            Assert.That(controller.IsRankedV2Connected, Is.True);
            Assert.That(rejected.MutationFact.Status, Is.EqualTo(
                SkillMutationStatus.PrerequisiteMissing));
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
            SkillsMenu controller = host.AddComponent<SkillsMenu>();
            PlayerRouteProfilePayload route = CreateRoute();
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

        [UnityTest]
        public IEnumerator InvalidRouteUnavailableState_UsesSanitizedNullPayload()
        {
            GameObject host = new GameObject(
                "SKILLS-V2-LIVE-001 invalid route unavailable test");
            SkillsMenu controller = host.AddComponent<SkillsMenu>();
            var navigation = new CaptureNavigationPort();

            Assert.DoesNotThrow(() => controller.ShowUnavailable(
                null,
                navigation,
                "skills-v2-route-invalid"));

            Assert.That(controller.IsDisconnected, Is.True);
            Assert.That(controller.CurrentProjection, Is.Null);
            Assert.That(controller.UnavailableReason, Is.EqualTo(
                "skills-v2-route-invalid"));
            Assert.That(controller.Back(), Is.True);
            Assert.That(navigation.ReturnCount, Is.EqualTo(1));
            Assert.That(navigation.LastPayload, Is.Null);

            Object.Destroy(host);
            yield return null;
        }

        private sealed class SuccessfulPersistence :
            IRankedSkillsPersistencePort
        {
            public RankedSkillsPersistenceResult Persist(
                string mutationScope,
                string immutableMutationFingerprint)
            {
                return new RankedSkillsPersistenceResult(true, string.Empty);
            }
        }

        private sealed class CaptureNavigationPort :
            ISkillsScreenNavigationPort
        {
            public int ReturnCount { get; private set; }
            public PlayerRouteProfilePayload LastPayload { get; private set; }

            public void ReturnToHub(PlayerRouteProfilePayload routePayload)
            {
                ReturnCount++;
                LastPayload = routePayload;
            }
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
                    StableId.Parse("difficulty.skills-v2-controller"),
                    1,
                    new List<StableId>()));
            if (level > 1)
            {
                authority.Grant(new PlayerExperienceGrantRequest(
                    StableId.Parse("xp-source.skills-v2-controller-" + level),
                    (level - 1L) * 100L));
            }
            return authority;
        }

        private static PlayerRouteProfilePayload CreateRoute()
        {
            return PlayerRouteProfilePayload.Create(
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

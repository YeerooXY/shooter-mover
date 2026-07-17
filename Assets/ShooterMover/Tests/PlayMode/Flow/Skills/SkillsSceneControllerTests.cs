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
    public sealed class SkillsSceneControllerTests
    {
        [UnityTest]
        public IEnumerator Controller_AllocatesAndReportsDuplicateOperation()
        {
            GameObject host = new GameObject("SKILLUI-001 controller test");
            SkillsSceneController controller = host.AddComponent<SkillsSceneController>();
            PlayerExperienceAuthorityV1 experience = CreateExperience(4);
            var skills = new SkillProgressionAuthorityV1(
                SkillCatalogV1.CreateDefault(),
                4);
            var navigation = new CaptureNavigationPort();
            controller.ConfigureForTests(
                new SkillsScreenSessionV1(CreateRoute(), experience, skills),
                navigation);

            SkillsScreenAllocationResultV1 applied = controller.AllocateSkill(
                "defense.1",
                "skills-controller-operation.same");
            SkillsScreenAllocationResultV1 duplicate = controller.AllocateSkill(
                "defense.1",
                "skills-controller-operation.same");

            Assert.That(applied.MutationFact.Status, Is.EqualTo(
                SkillMutationStatusV1.Applied));
            Assert.That(duplicate.MutationFact.Status, Is.EqualTo(
                SkillMutationStatusV1.DuplicateNoChange));
            Assert.That(controller.CurrentProjection.SpentSkillPoints, Is.EqualTo(1));
            Assert.That(navigation.ReturnCount, Is.Zero);

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Back_DispatchesOnceWithExactIncomingPayload()
        {
            GameObject host = new GameObject("SKILLUI-001 back test");
            SkillsSceneController controller = host.AddComponent<SkillsSceneController>();
            PlayerRouteProfilePayloadV1 route = CreateRoute();
            var navigation = new CaptureNavigationPort();
            controller.ConfigureForTests(
                new SkillsScreenSessionV1(
                    route,
                    CreateExperience(2),
                    new SkillProgressionAuthorityV1(
                        SkillCatalogV1.CreateDefault(),
                        2)),
                navigation);

            bool first = controller.Back();
            bool repeated = controller.Back();

            Assert.That(first, Is.True);
            Assert.That(repeated, Is.False);
            Assert.That(navigation.ReturnCount, Is.EqualTo(1));
            Assert.That(navigation.LastPayload, Is.SameAs(route));
            Assert.That(controller.IsVisible, Is.False);

            Object.Destroy(host);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HubAdapter_RevisitProjectsPersistedAuthorityRank()
        {
            GameObject host = new GameObject("SKILLUI-001 hub adapter test");
            SkillsSceneController controller = host.AddComponent<SkillsSceneController>();
            PlayerExperienceAuthorityV1 experience = CreateExperience(3);
            var skills = new SkillProgressionAuthorityV1(
                SkillCatalogV1.CreateDefault(),
                3);
            PlayerRouteProfilePayloadV1 route = CreateRoute();
            var navigation = new CaptureNavigationPort();
            var adapter = new SkillsHubDestinationAdapterV1(
                experience,
                skills,
                controller,
                navigation);

            adapter.Present(HubRouteV1.Skills, route);
            controller.AllocateSkill(
                "utility.1",
                "skills-controller-operation.revisit");
            adapter.Present(HubRouteV1.InventoryLoadoutHub, route);
            Assert.That(controller.IsVisible, Is.False);

            adapter.Present(HubRouteV1.Skills, route);
            SkillsScreenSkillProjectionV1 utilityOne;
            Assert.That(controller.IsVisible, Is.True);
            Assert.That(controller.CurrentProjection.RoutePayload, Is.SameAs(route));
            Assert.That(controller.CurrentProjection.TryGetSkill(
                "utility.1",
                out utilityOne),
                Is.True);
            Assert.That(utilityOne.CurrentRank, Is.EqualTo(1));
            Assert.That(controller.CurrentProjection.AvailableSkillPoints, Is.EqualTo(2));

            Object.Destroy(host);
            yield return null;
        }

        private sealed class CaptureNavigationPort : ISkillsScreenNavigationPortV1
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
                    StableId.Parse("difficulty.skills-controller-tests"),
                    1,
                    new List<StableId>()));
            if (level > 1)
            {
                authority.Grant(new PlayerExperienceGrantRequestV1(
                    StableId.Parse("xp-source.skills-controller-level-" + level),
                    (level - 1L) * 100L));
            }

            return authority;
        }

        private static PlayerRouteProfilePayloadV1 CreateRoute()
        {
            return PlayerRouteProfilePayloadV1.Create(
                StableId.Parse("character.skills-controller-tests"),
                StableId.Parse("loadout-profile.skills-controller-tests"),
                new List<StableId>
                {
                    StableId.Parse("equipment-instance.skills-controller-1"),
                    StableId.Parse("equipment-instance.skills-controller-2"),
                    StableId.Parse("equipment-instance.skills-controller-3"),
                    StableId.Parse("equipment-instance.skills-controller-4"),
                });
        }
    }
}

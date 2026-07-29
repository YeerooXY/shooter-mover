using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UI.StrongboxOpening;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Rewards.Strongboxes
{
    public sealed class StrongboxOpeningSceneTests
    {
        [Test]
        public void RuntimePortCachesTerminalResultAndDoesNotSubmitAnotherOpening()
        {
            int calls = 0;
            StrongboxOpeningResultLive terminal = new StrongboxOpeningResultLive(
                StrongboxOpeningLiveStatus.Opened,
                StableId.Parse("opening.scene"),
                0L,
                1L,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            StrongboxOpeningLivePort port = new StrongboxOpeningLivePort(delegate
            {
                calls++;
                return terminal;
            });

            Assert.That(port.OpenOrContinue(), Is.SameAs(terminal));
            Assert.That(port.OpenOrContinue(), Is.SameAs(terminal));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(port.AuthorityInvocationCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimePortRetriesOnlyPendingResultWithSameBoundOperation()
        {
            int calls = 0;
            StrongboxOpeningLivePort port = new StrongboxOpeningLivePort(delegate
            {
                calls++;
                return new StrongboxOpeningResultLive(
                    calls == 1
                        ? StrongboxOpeningLiveStatus.ClaimedPendingApplication
                        : StrongboxOpeningLiveStatus.Opened,
                    StableId.Parse("opening.retry"),
                    0L,
                    calls == 1 ? 0L : 1L,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            });

            Assert.That(port.OpenOrContinue().Status, Is.EqualTo(StrongboxOpeningLiveStatus.ClaimedPendingApplication));
            Assert.That(port.OpenOrContinue().Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(port.OpenOrContinue().Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void SessionUsesClosedOpeningRevealContinueStagesAndOneUserOpenRequest()
        {
            int opens = 0;
            StrongboxOpeningPreviewConfiguration configuration = Configuration();
            StrongboxOpeningSceneSession session = new StrongboxOpeningSceneSession(
                configuration,
                delegate
                {
                    opens++;
                    return StrongboxOpeningPresentationResult.Success(
                        new[]
                        {
                            Item(StrongboxRewardPresentationKind.Money, "Money", null),
                            Item(StrongboxRewardPresentationKind.Scrap, "Scrap", null),
                            Item(StrongboxRewardPresentationKind.Equipment, "Weapon", "instance.weapon"),
                        },
                        false,
                        false,
                        "Opened");
                });

            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStage.BoxClosed));
            Assert.That(session.RequestOpen(), Is.True);
            Assert.That(session.RequestOpen(), Is.False);
            Assert.That(opens, Is.EqualTo(1));
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStage.OpeningAnimation));

            session.Advance(configuration.OpeningDurationSeconds);
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStage.RewardReveal));
            Assert.That(session.VisibleRewardCount, Is.EqualTo(1));

            session.Advance(configuration.RevealIntervalSeconds * 2f + configuration.RevealCompleteHoldSeconds);
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStage.ContinueOrBack));
            Assert.That(session.VisibleRewardCount, Is.EqualTo(3));
            Assert.That(session.RequestContinue(), Is.True);
            Assert.That(session.RequestContinue(), Is.False);
        }

        [Test]
        public void ProjectorDisplaysEveryRequiredCategoryAndKeepsDuplicateDefinitionsSeparate()
        {
            EquipmentCatalog catalog = BuildCatalog();
            StableId weaponDefinition = StableId.Parse("equipment.rifle");
            EquipmentInstance weaponA = EquipmentInstance.Create(
                StableId.Parse("instance.rifle-a"), weaponDefinition, 25, StableId.Parse("quality.common"), Array.Empty<AugmentInstance>());
            EquipmentInstance weaponB = EquipmentInstance.Create(
                StableId.Parse("instance.rifle-b"), weaponDefinition, 26, StableId.Parse("quality.common"), Array.Empty<AugmentInstance>());
            EquipmentInstance armor = EquipmentInstance.Create(
                StableId.Parse("instance.armor-a"), StableId.Parse("equipment.armor"), 24, StableId.Parse("quality.common"), Array.Empty<AugmentInstance>());

            List<RewardGrantApplicationPayload> payloads = new List<RewardGrantApplicationPayload>
            {
                RewardGrantApplicationPayload.ForEquipment(
                    RewardGrant.Create(StableId.Parse("grant.weapons"), RewardGrantKind.EquipmentReference, weaponDefinition, 2L),
                    new[] { weaponA, weaponB }),
                RewardGrantApplicationPayload.ForEquipment(
                    RewardGrant.Create(StableId.Parse("grant.armor"), RewardGrantKind.EquipmentReference, StableId.Parse("equipment.armor"), 1L),
                    new[] { armor }),
                RewardGrantApplicationPayload.ForValue(
                    RewardGrant.Create(StableId.Parse("grant.money"), RewardGrantKind.Money, StableId.Parse("currency.money"), 250L)),
                RewardGrantApplicationPayload.ForValue(
                    RewardGrant.Create(StableId.Parse("grant.scrap"), RewardGrantKind.Scrap, StableId.Parse("currency.scrap"), 40L)),
                RewardGrantApplicationPayload.ForValue(
                    RewardGrant.Create(StableId.Parse("grant.misc"), RewardGrantKind.Miscellaneous, StableId.Parse("item.token"), 2L)),
            };

            IReadOnlyList<StrongboxRewardRevealItem> items =
                StrongboxRewardRevealProjector.ProjectPayloads(payloads, catalog);

            Assert.That(Count(items, StrongboxRewardPresentationKind.Equipment), Is.EqualTo(2));
            Assert.That(Count(items, StrongboxRewardPresentationKind.Armor), Is.EqualTo(1));
            Assert.That(Count(items, StrongboxRewardPresentationKind.Money), Is.EqualTo(1));
            Assert.That(Count(items, StrongboxRewardPresentationKind.Scrap), Is.EqualTo(1));
            Assert.That(Count(items, StrongboxRewardPresentationKind.Miscellaneous), Is.EqualTo(1));

            List<StrongboxRewardRevealItem> weapons = Find(items, StrongboxRewardPresentationKind.Equipment);
            Assert.That(weapons[0].ContentStableId, Is.EqualTo(weapons[1].ContentStableId));
            Assert.That(weapons[0].InstanceStableId, Is.Not.EqualTo(weapons[1].InstanceStableId));
        }

        [Test]
        public void PreviewConfigurationExposesStableSeedAndCanonicalTiming()
        {
            StrongboxOpeningPreviewConfiguration left = Configuration();
            StrongboxOpeningPreviewConfiguration right = Configuration();

            Assert.That(left.DeterministicSeed, Is.EqualTo(123456UL));
            Assert.That(left, Is.EqualTo(right));
            Assert.That(left.ToCanonicalString(), Does.Contain("seed=123456"));
            Assert.That(left.ToCanonicalString(), Does.Contain("tier=strongbox-tier.test"));
        }

        [Test]
        public void ControllerDoesNotRepeatUserOpenCallback()
        {
            GameObject gameObject = new GameObject("StrongboxOpeningControllerTest");
            try
            {
                StrongboxOpeningController controller = gameObject.AddComponent<StrongboxOpeningController>();
                int calls = 0;
                controller.ConfigureForTests(Configuration(), delegate
                {
                    calls++;
                    return StrongboxOpeningPresentationResult.Success(
                        new[] { Item(StrongboxRewardPresentationKind.Scrap, "Scrap", null) },
                        false,
                        false,
                        "Opened");
                });

                Assert.That(controller.RequestOpen(), Is.True);
                Assert.That(controller.RequestOpen(), Is.False);
                Assert.That(calls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ProductionSourceDelegatesToBoxAndContainsNoDirectAuthorityMutation()
        {
            string sourcePath = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains("service.Open(command)", source);
            StringAssert.DoesNotContain("MoneyWalletActions", source);
            StringAssert.DoesNotContain("ScrapWalletActions", source);
            StringAssert.DoesNotContain("PlayerHoldingsActions", source);
            StringAssert.DoesNotContain("RewardApplicationActions", source);
            StringAssert.DoesNotContain("holdings.Apply", source);
            StringAssert.DoesNotContain("rewardApplication.", source);
        }

        [Test]
        public void StandaloneSceneContainsOnlyTheStrongboxPresentationController()
        {
            string scenePath = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/Scenes/StrongboxOpening/StrongboxOpening.unity");
            string scene = File.ReadAllText(scenePath);

            StringAssert.Contains("m_Name: StrongboxOpening", scene);
            StringAssert.Contains("guid: 6e8f7d8229f545b08157a8aa32c28e02", scene);
            StringAssert.DoesNotContain("Stage1VisibleSliceController", scene);
        }

        private static StrongboxOpeningPreviewConfiguration Configuration()
        {
            return new StrongboxOpeningPreviewConfiguration(
                "strongbox-tier.test",
                "TEST TIER",
                123456UL,
                1f,
                0.25f,
                0.5f);
        }

        private static StrongboxRewardRevealItem Item(
            StrongboxRewardPresentationKind kind,
            string title,
            string instanceId)
        {
            return new StrongboxRewardRevealItem(kind, title, "content.test", instanceId, 1L, string.Empty);
        }

        private static int Count(
            IEnumerable<StrongboxRewardRevealItem> items,
            StrongboxRewardPresentationKind kind)
        {
            int count = 0;
            foreach (StrongboxRewardRevealItem item in items)
            {
                if (item.Kind == kind) { count++; }
            }
            return count;
        }

        private static List<StrongboxRewardRevealItem> Find(
            IEnumerable<StrongboxRewardRevealItem> items,
            StrongboxRewardPresentationKind kind)
        {
            List<StrongboxRewardRevealItem> result = new List<StrongboxRewardRevealItem>();
            foreach (StrongboxRewardRevealItem item in items)
            {
                if (item.Kind == kind) { result.Add(item); }
            }
            return result;
        }

        private static EquipmentCatalog BuildCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                StableId.Parse("quality.common"), "Common", 1);
            EquipmentDefinition weapon = EquipmentDefinition.Create(
                StableId.Parse("equipment.rifle"),
                EquipmentCategoryIds.Weapon,
                StableId.Parse("family.rifle"),
                "Blaster Rifle",
                StableId.Parse("weapon.blaster-machine-gun"),
                InclusiveIntRange.Create(1, 100),
                3,
                new[] { common },
                Array.Empty<StableId>());
            EquipmentDefinition armor = EquipmentDefinition.Create(
                StableId.Parse("equipment.armor"),
                EquipmentCategoryIds.Armor,
                StableId.Parse("family.armor"),
                "Field Armor",
                null,
                InclusiveIntRange.Create(1, 100),
                3,
                new[] { common },
                Array.Empty<StableId>());
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[] { weapon, armor },
                Array.Empty<AugmentDefinition>());
            Assert.That(result.IsValid, Is.True, "Reference equipment catalog should be valid.");
            return result.Catalog;
        }
    }
}

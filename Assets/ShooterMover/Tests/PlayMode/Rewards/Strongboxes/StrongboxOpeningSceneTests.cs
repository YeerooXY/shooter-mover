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
            StrongboxOpeningResultRuntimeV1 terminal = new StrongboxOpeningResultRuntimeV1(
                StrongboxOpeningRuntimeStatusV1.Opened,
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
            StrongboxOpeningRuntimePortV1 port = new StrongboxOpeningRuntimePortV1(delegate
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
            StrongboxOpeningRuntimePortV1 port = new StrongboxOpeningRuntimePortV1(delegate
            {
                calls++;
                return new StrongboxOpeningResultRuntimeV1(
                    calls == 1
                        ? StrongboxOpeningRuntimeStatusV1.ClaimedPendingApplication
                        : StrongboxOpeningRuntimeStatusV1.Opened,
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

            Assert.That(port.OpenOrContinue().Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ClaimedPendingApplication));
            Assert.That(port.OpenOrContinue().Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(port.OpenOrContinue().Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void SessionUsesClosedOpeningRevealContinueStagesAndOneUserOpenRequest()
        {
            int opens = 0;
            StrongboxOpeningPreviewConfigurationV1 configuration = Configuration();
            StrongboxOpeningSceneSessionV1 session = new StrongboxOpeningSceneSessionV1(
                configuration,
                delegate
                {
                    opens++;
                    return StrongboxOpeningPresentationResultV1.Success(
                        new[]
                        {
                            Item(StrongboxRewardPresentationKindV1.Money, "Money", null),
                            Item(StrongboxRewardPresentationKindV1.Scrap, "Scrap", null),
                            Item(StrongboxRewardPresentationKindV1.Equipment, "Weapon", "instance.weapon"),
                        },
                        false,
                        false,
                        "Opened");
                });

            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStageV1.BoxClosed));
            Assert.That(session.RequestOpen(), Is.True);
            Assert.That(session.RequestOpen(), Is.False);
            Assert.That(opens, Is.EqualTo(1));
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStageV1.OpeningAnimation));

            session.Advance(configuration.OpeningDurationSeconds);
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStageV1.RewardReveal));
            Assert.That(session.VisibleRewardCount, Is.EqualTo(1));

            session.Advance(configuration.RevealIntervalSeconds * 2f + configuration.RevealCompleteHoldSeconds);
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStageV1.ContinueOrBack));
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

            List<RewardGrantApplicationPayloadV1> payloads = new List<RewardGrantApplicationPayloadV1>
            {
                RewardGrantApplicationPayloadV1.ForEquipment(
                    RewardGrantV1.Create(StableId.Parse("grant.weapons"), RewardGrantKindV1.EquipmentReference, weaponDefinition, 2L),
                    new[] { weaponA, weaponB }),
                RewardGrantApplicationPayloadV1.ForEquipment(
                    RewardGrantV1.Create(StableId.Parse("grant.armor"), RewardGrantKindV1.EquipmentReference, StableId.Parse("equipment.armor"), 1L),
                    new[] { armor }),
                RewardGrantApplicationPayloadV1.ForValue(
                    RewardGrantV1.Create(StableId.Parse("grant.money"), RewardGrantKindV1.Money, StableId.Parse("currency.money"), 250L)),
                RewardGrantApplicationPayloadV1.ForValue(
                    RewardGrantV1.Create(StableId.Parse("grant.scrap"), RewardGrantKindV1.Scrap, StableId.Parse("currency.scrap"), 40L)),
                RewardGrantApplicationPayloadV1.ForValue(
                    RewardGrantV1.Create(StableId.Parse("grant.misc"), RewardGrantKindV1.Miscellaneous, StableId.Parse("item.token"), 2L)),
            };

            IReadOnlyList<StrongboxRewardRevealItemV1> items =
                StrongboxRewardRevealProjectorV1.ProjectPayloads(payloads, catalog);

            Assert.That(Count(items, StrongboxRewardPresentationKindV1.Equipment), Is.EqualTo(2));
            Assert.That(Count(items, StrongboxRewardPresentationKindV1.Armor), Is.EqualTo(1));
            Assert.That(Count(items, StrongboxRewardPresentationKindV1.Money), Is.EqualTo(1));
            Assert.That(Count(items, StrongboxRewardPresentationKindV1.Scrap), Is.EqualTo(1));
            Assert.That(Count(items, StrongboxRewardPresentationKindV1.Miscellaneous), Is.EqualTo(1));

            List<StrongboxRewardRevealItemV1> weapons = Find(items, StrongboxRewardPresentationKindV1.Equipment);
            Assert.That(weapons[0].ContentStableId, Is.EqualTo(weapons[1].ContentStableId));
            Assert.That(weapons[0].InstanceStableId, Is.Not.EqualTo(weapons[1].InstanceStableId));
        }

        [Test]
        public void PreviewConfigurationExposesStableSeedAndCanonicalTiming()
        {
            StrongboxOpeningPreviewConfigurationV1 left = Configuration();
            StrongboxOpeningPreviewConfigurationV1 right = Configuration();

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
                    return StrongboxOpeningPresentationResultV1.Success(
                        new[] { Item(StrongboxRewardPresentationKindV1.Scrap, "Scrap", null) },
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
            StringAssert.DoesNotContain("MoneyWalletService", source);
            StringAssert.DoesNotContain("ScrapWalletServiceV1", source);
            StringAssert.DoesNotContain("PlayerHoldingsService", source);
            StringAssert.DoesNotContain("RewardApplicationServiceV1", source);
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

        private static StrongboxOpeningPreviewConfigurationV1 Configuration()
        {
            return new StrongboxOpeningPreviewConfigurationV1(
                "strongbox-tier.test",
                "TEST TIER",
                123456UL,
                1f,
                0.25f,
                0.5f);
        }

        private static StrongboxRewardRevealItemV1 Item(
            StrongboxRewardPresentationKindV1 kind,
            string title,
            string instanceId)
        {
            return new StrongboxRewardRevealItemV1(kind, title, "content.test", instanceId, 1L, string.Empty);
        }

        private static int Count(
            IEnumerable<StrongboxRewardRevealItemV1> items,
            StrongboxRewardPresentationKindV1 kind)
        {
            int count = 0;
            foreach (StrongboxRewardRevealItemV1 item in items)
            {
                if (item.Kind == kind) { count++; }
            }
            return count;
        }

        private static List<StrongboxRewardRevealItemV1> Find(
            IEnumerable<StrongboxRewardRevealItemV1> items,
            StrongboxRewardPresentationKindV1 kind)
        {
            List<StrongboxRewardRevealItemV1> result = new List<StrongboxRewardRevealItemV1>();
            foreach (StrongboxRewardRevealItemV1 item in items)
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

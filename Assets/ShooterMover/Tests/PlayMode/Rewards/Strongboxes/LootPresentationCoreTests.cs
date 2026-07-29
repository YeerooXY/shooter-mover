using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UI.StrongboxOpening;

namespace ShooterMover.Tests.PlayMode.Rewards.Strongboxes
{
    public sealed class LootPresentationCoreTests
    {
        [Test]
        public void GroupingPreservesExactIdentitiesAndRejectsDuplicates()
        {
            StrongboxTier steel = StrongboxCatalog.GetByNumber(1);
            var instances = new List<OwnedStrongboxInstancePresentation>();
            for (int index = 1; index <= 10; index++)
            {
                instances.Add(CreateInstance(steel, index));
            }

            IReadOnlyList<OwnedStrongboxGroupPresentation> groups;
            string diagnostic;
            Assert.That(
                StrongboxGroupingProjector.TryProject(instances, out groups, out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].Quantity, Is.EqualTo(10));
            Assert.That(groups[0].Instances[4].InstanceStableId, Is.EqualTo(instances[4].InstanceStableId));

            instances.Add(instances[0]);
            Assert.That(
                StrongboxGroupingProjector.TryProject(instances, out groups, out diagnostic),
                Is.False);
            Assert.That(diagnostic, Does.Contain("duplicate"));
        }

        [Test]
        public void ExactSelectionResolvesOpenFiveWithoutMutatingQuantity()
        {
            StrongboxTier steel = StrongboxCatalog.GetByNumber(1);
            var instances = new List<OwnedStrongboxInstancePresentation>();
            for (int index = 1; index <= 10; index++)
            {
                instances.Add(CreateInstance(steel, index));
            }

            IReadOnlyList<OwnedStrongboxGroupPresentation> groups;
            string diagnostic;
            Assert.That(
                StrongboxGroupingProjector.TryProject(instances, out groups, out diagnostic),
                Is.True,
                diagnostic);
            var selection = new ExactStrongboxSelection(groups);
            Assert.That(selection.TrySelectExact(instances[4].InstanceStableId, out diagnostic), Is.True, diagnostic);

            IReadOnlyList<StableId> batch = selection.ResolveBatch(5);
            Assert.That(batch.Count, Is.EqualTo(5));
            Assert.That(batch[0], Is.EqualTo(instances[4].InstanceStableId));
            Assert.That(new HashSet<StableId>(batch).Count, Is.EqualTo(5));
            Assert.That(groups[0].Quantity, Is.EqualTo(10));
        }

        [Test]
        public void RunHudTotalsProjectCanonicalFactsAndRejectDuplicateOperations()
        {
            RunSessionCollectedReward money = CollectedReward(
                "money", RewardGrantKind.Money, StableId.Parse("currency.money"), 125L, 1L);
            RunSessionCollectedReward scrap = CollectedReward(
                "scrap", RewardGrantKind.Scrap, StableId.Parse("currency.scrap"), 18L, 2L);
            RunSessionCollectedReward box = CollectedReward(
                "box",
                RewardGrantKind.Strongbox,
                StrongboxCatalog.GetByNumber(1).TierStableId,
                1L,
                3L);

            RunLootTotalsPresentation totals;
            string diagnostic;
            Assert.That(
                RunLootTotalsProjector.TryProject(
                    new[] { money, scrap, box }, out totals, out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(totals.Credits, Is.EqualTo(125L));
            Assert.That(totals.Scrap, Is.EqualTo(18L));
            Assert.That(totals.Strongboxes, Is.EqualTo(1L));

            Assert.That(
                RunLootTotalsProjector.TryProject(
                    new[] { money, money }, out totals, out diagnostic),
                Is.False);
            Assert.That(diagnostic, Does.Contain("duplicate-operation"));
        }

        [Test]
        public void MissionResultProjectionRejectsOpenedStrongboxes()
        {
            StrongboxTier steel = StrongboxCatalog.GetByNumber(1);
            MissionRunStrongboxResult first = RunStrongboxResult(steel, "01", false);
            MissionRunStrongboxResult second = RunStrongboxResult(steel, "02", false);

            IReadOnlyList<OwnedStrongboxGroupPresentation> groups;
            string diagnostic;
            Assert.That(
                StrongboxGroupingProjector.TryProjectUnopened(
                    new[] { first, second }, out groups, out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].Quantity, Is.EqualTo(2));
            Assert.That(groups[0].Instances[0].InstanceStableId, Is.EqualTo(first.InstanceStableId));

            MissionRunStrongboxResult opened = RunStrongboxResult(steel, "03", true);
            Assert.That(
                StrongboxGroupingProjector.TryProjectUnopened(
                    new[] { opened }, out groups, out diagnostic),
                Is.False);
            Assert.That(diagnostic, Does.Contain("already-opened"));
        }

        [Test]
        public void UnknownTierFailsClosed()
        {
            OwnedStrongboxInstancePresentation instance;
            string diagnostic;
            Assert.That(
                OwnedStrongboxInstancePresentation.TryCreate(
                    StableId.Parse("development-strongbox.unknown"),
                    StableId.Parse("strongbox-tier.not-authored"),
                    out instance,
                    out diagnostic),
                Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(diagnostic, Does.Contain("unknown"));
        }

        [Test]
        public void SkipReusesCommittedPresentationResult()
        {
            int calls = 0;
            StrongboxOpeningPresentationResult frozen = StrongboxOpeningPresentationResult.Success(
                new[]
                {
                    new StrongboxRewardRevealItem(
                        StrongboxRewardPresentationKind.Money,
                        "CREDITS",
                        "currency.money",
                        null,
                        10L,
                        string.Empty),
                },
                false,
                true,
                "FROZEN");
            var session = new StrongboxOpeningSceneSession(
                new StrongboxOpeningPreviewConfiguration(
                    "strongbox-tier.steel",
                    "Steel",
                    9001001UL,
                    1f,
                    0.25f,
                    0.5f),
                delegate
                {
                    calls++;
                    return frozen;
                });

            Assert.That(session.RequestOpen(), Is.True);
            Assert.That(StrongboxPresentationPlayback.SkipToComplete(session), Is.True);
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStage.ContinueOrBack));
            Assert.That(session.Result, Is.SameAs(frozen));
            Assert.That(calls, Is.EqualTo(1));
        }


        private static RunSessionCollectedReward CollectedReward(
            string suffix,
            RewardGrantKind kind,
            StableId contentStableId,
            long quantity,
            long order)
        {
            return new RunSessionCollectedReward(
                StableId.Create("pickup", suffix),
                StableId.Create("generated-child", suffix),
                StableId.Create("grant", suffix),
                StableId.Create("drop-operation", suffix),
                StableId.Create("terminal-event", suffix),
                null,
                StableId.Parse("run.loot-presentation-test"),
                0L,
                StableId.Create("source-entity", suffix),
                null,
                0L,
                StableId.Create("source-definition", suffix),
                StableId.Parse("participant.player"),
                kind,
                contentStableId,
                quantity,
                "generated-batch-fingerprint-" + suffix,
                "generated-reward-fingerprint-" + suffix,
                StableId.Parse("room.loot-presentation-test"),
                1d,
                2d,
                "world-spawn-fingerprint-" + suffix,
                "available-pickup-fingerprint-" + suffix,
                StableId.Parse("actor.player"),
                StableId.Parse("participant.player"),
                StableId.Create("collection-operation", suffix),
                order,
                order);
        }

        private static MissionRunStrongboxResult RunStrongboxResult(
            StrongboxTier tier,
            string suffix,
            bool opened)
        {
            const string fingerprint =
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var collection = new MissionRunStrongboxCollection(
                tier.TierStableId,
                StableId.Create("strongbox-instance", suffix),
                StableId.Create("strongbox-grant", suffix),
                StableId.Create("reward-source", suffix),
                StableId.Create("collection-operation", suffix),
                1L,
                fingerprint);
            return new MissionRunStrongboxResult(
                collection,
                opened ? MissionRunStrongboxState.Opened : MissionRunStrongboxState.Unopened,
                opened ? StableId.Create("opening", suffix) : null,
                opened ? fingerprint : null);
        }

        private static OwnedStrongboxInstancePresentation CreateInstance(
            StrongboxTier tier,
            int index)
        {
            OwnedStrongboxInstancePresentation instance;
            string diagnostic;
            Assert.That(
                OwnedStrongboxInstancePresentation.TryCreate(
                    StableId.Create("development-strongbox", tier.Slug + "-" + index.ToString("00")),
                    tier.TierStableId,
                    out instance,
                    out diagnostic),
                Is.True,
                diagnostic);
            return instance;
        }
    }
}

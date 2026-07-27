using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Common;
using ShooterMover.UI.StrongboxOpening;

namespace ShooterMover.Tests.PlayMode.Rewards.Strongboxes
{
    public sealed class LootPresentationCoreTests
    {
        [Test]
        public void GroupingPreservesExactIdentitiesAndRejectsDuplicates()
        {
            ProductionStrongboxTierV1 steel = ProductionStrongboxCatalogV1.GetByNumber(1);
            var instances = new List<OwnedStrongboxInstancePresentationV1>();
            for (int index = 1; index <= 10; index++)
            {
                instances.Add(CreateInstance(steel, index));
            }

            IReadOnlyList<OwnedStrongboxGroupPresentationV1> groups;
            string diagnostic;
            Assert.That(
                StrongboxGroupingProjectorV1.TryProject(instances, out groups, out diagnostic),
                Is.True,
                diagnostic);
            Assert.That(groups.Count, Is.EqualTo(1));
            Assert.That(groups[0].Quantity, Is.EqualTo(10));
            Assert.That(groups[0].Instances[4].InstanceStableId, Is.EqualTo(instances[4].InstanceStableId));

            instances.Add(instances[0]);
            Assert.That(
                StrongboxGroupingProjectorV1.TryProject(instances, out groups, out diagnostic),
                Is.False);
            Assert.That(diagnostic, Does.Contain("duplicate"));
        }

        [Test]
        public void ExactSelectionResolvesOpenFiveWithoutMutatingQuantity()
        {
            ProductionStrongboxTierV1 steel = ProductionStrongboxCatalogV1.GetByNumber(1);
            var instances = new List<OwnedStrongboxInstancePresentationV1>();
            for (int index = 1; index <= 10; index++)
            {
                instances.Add(CreateInstance(steel, index));
            }

            IReadOnlyList<OwnedStrongboxGroupPresentationV1> groups;
            string diagnostic;
            Assert.That(
                StrongboxGroupingProjectorV1.TryProject(instances, out groups, out diagnostic),
                Is.True,
                diagnostic);
            var selection = new ExactStrongboxSelectionV1(groups);
            Assert.That(selection.TrySelectExact(instances[4].InstanceStableId, out diagnostic), Is.True, diagnostic);

            IReadOnlyList<StableId> batch = selection.ResolveBatch(5);
            Assert.That(batch.Count, Is.EqualTo(5));
            Assert.That(batch[0], Is.EqualTo(instances[4].InstanceStableId));
            Assert.That(new HashSet<StableId>(batch).Count, Is.EqualTo(5));
            Assert.That(groups[0].Quantity, Is.EqualTo(10));
        }

        [Test]
        public void UnknownTierFailsClosed()
        {
            OwnedStrongboxInstancePresentationV1 instance;
            string diagnostic;
            Assert.That(
                OwnedStrongboxInstancePresentationV1.TryCreate(
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
            StrongboxOpeningPresentationResultV1 frozen = StrongboxOpeningPresentationResultV1.Success(
                new[]
                {
                    new StrongboxRewardRevealItemV1(
                        StrongboxRewardPresentationKindV1.Money,
                        "CREDITS",
                        "currency.money",
                        null,
                        10L,
                        string.Empty),
                },
                false,
                true,
                "FROZEN");
            var session = new StrongboxOpeningSceneSessionV1(
                new StrongboxOpeningPreviewConfigurationV1(
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
            Assert.That(StrongboxPresentationPlaybackV1.SkipToComplete(session), Is.True);
            Assert.That(session.Stage, Is.EqualTo(StrongboxRevealStageV1.ContinueOrBack));
            Assert.That(session.Result, Is.SameAs(frozen));
            Assert.That(calls, Is.EqualTo(1));
        }

        private static OwnedStrongboxInstancePresentationV1 CreateInstance(
            ProductionStrongboxTierV1 tier,
            int index)
        {
            OwnedStrongboxInstancePresentationV1 instance;
            string diagnostic;
            Assert.That(
                OwnedStrongboxInstancePresentationV1.TryCreate(
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

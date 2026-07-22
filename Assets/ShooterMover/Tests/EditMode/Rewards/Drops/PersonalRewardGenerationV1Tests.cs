using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.Drops
{
    public sealed class PersonalRewardGenerationV1Tests
    {
        private static readonly StableId ParticipantA = StableId.Parse("participant.test-a");
        private static readonly StableId ParticipantB = StableId.Parse("participant.test-b");
        private static readonly StableId ParticipantC = StableId.Parse("participant.test-c");
        private static readonly StableId ParticipantD = StableId.Parse("participant.test-d");
        private static readonly StableId RoomId = StableId.Parse("room.test-rewards");
        private static readonly StableId PlacementId = StableId.Parse("placement.test-rewards");
        private static readonly StableId DifficultyId = StableId.Parse("difficulty.normal");
        private static readonly StableId GameModeId = StableId.Parse("game-mode.campaign");

        [Test]
        public void IdenticalInputsAndSeedProduceByteEquivalentResults()
        {
            PersonalRewardRollContextV1 context = Context(
                ProductionRewardSourceCatalogV1.NormalEnemyId,
                ParticipantA,
                7UL,
                1,
                1);
            string left = NewService().Generate(context).ToCanonicalString();
            string right = NewService().Generate(context).ToCanonicalString();
            Assert.That(right, Is.EqualTo(left));
        }

        [Test]
        public void DifferentParticipantIdentityProducesIndependentDeterministicRoll()
        {
            PersonalRewardGenerationServiceV1 service = NewService();
            PersonalRewardGenerationResultV1 left = service.Generate(Context(
                ProductionRewardSourceCatalogV1.NormalEnemyId,
                ParticipantA,
                113UL,
                1,
                1));
            PersonalRewardGenerationResultV1 right = service.Generate(Context(
                ProductionRewardSourceCatalogV1.NormalEnemyId,
                ParticipantB,
                113UL,
                1,
                1));
            Assert.That(right.Context.OperationStableId, Is.Not.EqualTo(left.Context.OperationStableId));
            Assert.That(right.Context.Fingerprint, Is.Not.EqualTo(left.Context.Fingerprint));
            Assert.That(
                NewService().Generate(right.Context).ToCanonicalString(),
                Is.EqualTo(right.ToCanonicalString()));
        }

        [Test]
        public void ExactReplayReturnsExactResultAndChangedFingerprintRejects()
        {
            ParticipantDropPacingAuthorityV1 authority =
                new ParticipantDropPacingAuthorityV1();
            PersonalRewardGenerationServiceV1 service =
                new PersonalRewardGenerationServiceV1(authority);
            PersonalRewardRollContextV1 original = Context(
                ProductionRewardSourceCatalogV1.NormalEnemyId,
                ParticipantA,
                42UL,
                1,
                1);
            PersonalRewardGenerationResultV1 first = service.Generate(original);
            PersonalRewardGenerationResultV1 replay = service.Generate(original);
            Assert.That(replay, Is.SameAs(first));

            PersonalRewardRollContextV1 conflict = Context(
                ProductionRewardSourceCatalogV1.NormalEnemyId,
                ParticipantA,
                43UL,
                1,
                1);
            Assert.That(conflict.OperationStableId, Is.EqualTo(original.OperationStableId));
            PersonalRewardGenerationResultV1 rejected = service.Generate(conflict);
            Assert.That(
                rejected.Status,
                Is.EqualTo(PersonalRewardGenerationStatusV1.ConflictingReplay));
            Assert.That(rejected.Grants, Is.Empty);
        }

        [Test]
        public void ProductionAliasesShareTheSameProfileFingerprint()
        {
            RewardSourceProfileV1 normal = ProductionRewardSourceCatalogV1.Get(
                ProductionRewardSourceCatalogV1.NormalEnemyId);
            Assert.That(
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.NormalPropId),
                Is.SameAs(normal));
            Assert.That(
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.NormalHiddenTreasureId),
                Is.SameAs(normal));

            RewardSourceProfileV1 large = ProductionRewardSourceCatalogV1.Get(
                ProductionRewardSourceCatalogV1.LargeEnemyId);
            Assert.That(
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.RarePropId),
                Is.SameAs(large));
            Assert.That(
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.LargeTreasureLootId),
                Is.SameAs(ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.ExtraRarePropId)));
        }

        [Test]
        public void NormalEnemyApproximatesNinetyEightTwoBeforePacing()
        {
            Distribution distribution = Simulate(
                ProductionRewardSourceCatalogV1.NormalEnemyId,
                20000);
            Assert.That(distribution.Money / 20000d, Is.InRange(0.88d, 0.92d));
            Assert.That(distribution.Scrap / 20000d, Is.InRange(0.07d, 0.09d));
            Assert.That(distribution.Strongboxes / 20000d, Is.InRange(0.015d, 0.025d));
            Assert.That(distribution.NoDrop, Is.EqualTo(0));
        }

        [Test]
        public void LargeEnemyApproximatesSixtyEightThirtyTwoBeforePacing()
        {
            Distribution distribution = Simulate(
                ProductionRewardSourceCatalogV1.LargeEnemyId,
                20000);
            Assert.That(distribution.Money / 20000d, Is.InRange(0.58d, 0.62d));
            Assert.That(distribution.Scrap / 20000d, Is.InRange(0.07d, 0.09d));
            Assert.That(distribution.Strongboxes / 20000d, Is.InRange(0.30d, 0.34d));
        }

        [Test]
        public void ExtraBossCountApproximatesSeventyTwentyFiveFive()
        {
            CountDistribution distribution = SimulateBoxCounts(
                ProductionRewardSourceCatalogV1.ExtraBossEnemyId,
                20000);
            Assert.That(distribution.Counts[3] / 20000d, Is.InRange(0.68d, 0.72d));
            Assert.That(distribution.Counts[4] / 20000d, Is.InRange(0.23d, 0.27d));
            Assert.That(distribution.Counts[5] / 20000d, Is.InRange(0.04d, 0.06d));
        }

        [Test]
        public void ExtraRarePropCountApproximatesSeventyTwentyFiveFive()
        {
            CountDistribution distribution = SimulateBoxCounts(
                ProductionRewardSourceCatalogV1.ExtraRarePropId,
                20000);
            Assert.That(distribution.Counts[2] / 20000d, Is.InRange(0.68d, 0.72d));
            Assert.That(distribution.Counts[3] / 20000d, Is.InRange(0.23d, 0.27d));
            Assert.That(distribution.Counts[4] / 20000d, Is.InRange(0.04d, 0.06d));
        }

        [Test]
        public void EveryEligibleParticipantGetsDefaultBossGuarantee()
        {
            PersonalRewardGenerationServiceV1 service = NewService();
            IReadOnlyList<PersonalRewardGenerationResultV1> results =
                service.GenerateForParticipants(new[]
                {
                    Context(ProductionRewardSourceCatalogV1.BossEnemyId, ParticipantA, 99UL, 1, 1),
                    Context(ProductionRewardSourceCatalogV1.BossEnemyId, ParticipantB, 99UL, 1, 1),
                    Context(ProductionRewardSourceCatalogV1.BossEnemyId, ParticipantC, 99UL, 1, 1),
                    Context(ProductionRewardSourceCatalogV1.BossEnemyId, ParticipantD, 99UL, 1, 1),
                });
            Assert.That(results, Has.Count.EqualTo(4));
            var exactBoxIds = new HashSet<StableId>();
            for (int index = 0; index < results.Count; index++)
            {
                Assert.That(CountBoxes(results[index]), Is.EqualTo(1));
                for (int grantIndex = 0; grantIndex < results[index].Grants.Count; grantIndex++)
                {
                    RewardGrantV1 grant = results[index].Grants[grantIndex];
                    if (grant.Kind == RewardGrantKindV1.Strongbox)
                        Assert.That(exactBoxIds.Add(grant.GrantStableId), Is.True);
                }
            }
        }

        [Test]
        public void ModeReplacementCanRemoveBossStrongboxCompletely()
        {
            RewardSourceProfileV1 moneyOnly = RewardSourceProfileV1.Create(
                StableId.Parse("drop-source.test-survival-boss"),
                null,
                new[]
                {
                    RewardRollGroupV1.CreateGuaranteed(
                        StableId.Parse("drop-group.test-survival-boss-money"),
                        0,
                        RewardBoxPacingModeV1.None,
                        new[]
                        {
                            RewardOutcomeV1.CreateGrant(
                                StableId.Parse("drop-outcome.test-survival-boss-money"),
                                RewardGrantSpecificationV1.Create(
                                    StableId.Parse("drop-grant.test-survival-boss-money"),
                                    RewardGrantKindV1.Money,
                                    StableId.Parse("currency.money"),
                                    RewardQuantityRangeV1.Fixed(50),
                                    Array.Empty<RewardScalingInputDescriptorV1>()),
                                1),
                        }),
                });
            RewardProfileResolutionV1 resolution = new RewardProfileResolverV1().Resolve(
                ProductionRewardSourceCatalogV1.BossEnemyId,
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.BossEnemyId),
                RewardProfileOverrideV1.Replace(
                    StableId.Parse("game-mode.survival-boss-override"),
                    moneyOnly),
                null,
                null,
                null,
                null);
            PersonalRewardGenerationResultV1 result = NewService().Generate(Context(
                resolution,
                ParticipantA,
                5UL,
                1,
                1));
            Assert.That(CountBoxes(result), Is.EqualTo(0));
            Assert.That(result.Grants, Has.Count.EqualTo(1));
            Assert.That(result.Grants[0].Kind, Is.EqualTo(RewardGrantKindV1.Money));
        }

        [Test]
        public void OnePlayersRandomBoxDoesNotChangeAnotherPlayersPacing()
        {
            for (ulong seed = 1UL; seed < 1000UL; seed++)
            {
                ParticipantDropPacingAuthorityV1 authority =
                    new ParticipantDropPacingAuthorityV1();
                PersonalRewardGenerationServiceV1 service =
                    new PersonalRewardGenerationServiceV1(authority);
                PersonalRewardGenerationResultV1 playerA = service.Generate(Context(
                    ProductionRewardSourceCatalogV1.LargeEnemyId,
                    ParticipantA,
                    seed,
                    1,
                    1));
                if (CountBoxes(playerA) == 0)
                    continue;

                PersonalRewardGenerationResultV1 playerB = service.Generate(Context(
                    ProductionRewardSourceCatalogV1.LargeEnemyId,
                    ParticipantB,
                    seed,
                    1,
                    1));
                Assert.That(playerB.PacingBefore.RandomBoxesInRun, Is.EqualTo(0));
                Assert.That(playerB.PacingBefore.RandomBoxesInCurrentRoom, Is.EqualTo(0));
                return;
            }
            Assert.Fail("No deterministic seed produced the expected player-A box fixture.");
        }

        [Test]
        public void ReconnectRestorePreservesPityAndSaturationState()
        {
            ParticipantDropPacingAuthorityV1 uninterruptedAuthority =
                new ParticipantDropPacingAuthorityV1();
            PersonalRewardGenerationServiceV1 uninterrupted =
                new PersonalRewardGenerationServiceV1(uninterruptedAuthority);
            for (int ordinal = 1; ordinal <= 15; ordinal++)
            {
                uninterrupted.Generate(Context(
                    ProductionRewardSourceCatalogV1.SmallEnemyId,
                    ParticipantA,
                    1UL,
                    ordinal,
                    1));
            }

            ParticipantDropPacingStateV1 snapshot;
            Assert.That(uninterruptedAuthority.TryExport(
                StableId.Parse("run.test-rewards"),
                1,
                ParticipantA,
                out snapshot), Is.True);

            ParticipantDropPacingAuthorityV1 restoredAuthority =
                new ParticipantDropPacingAuthorityV1();
            restoredAuthority.Restore(snapshot);
            PersonalRewardGenerationServiceV1 restored =
                new PersonalRewardGenerationServiceV1(restoredAuthority);
            PersonalRewardRollContextV1 next = Context(
                ProductionRewardSourceCatalogV1.SmallEnemyId,
                ParticipantA,
                1UL,
                16,
                1);
            PersonalRewardGenerationResultV1 uninterruptedNext =
                uninterrupted.Generate(next);
            PersonalRewardGenerationResultV1 restoredNext = restored.Generate(next);
            Assert.That(
                restoredNext.PacingBefore.ToCanonicalString(),
                Is.EqualTo(uninterruptedNext.PacingBefore.ToCanonicalString()));
            Assert.That(
                restoredNext.ToCanonicalString(),
                Is.EqualTo(uninterruptedNext.ToCanonicalString()));
        }

        [Test]
        public void RunMinimumCreatesAtMostTheMissingCount()
        {
            ParticipantDropPacingAuthorityV1 authority =
                new ParticipantDropPacingAuthorityV1();
            PersonalRewardGenerationServiceV1 service =
                new PersonalRewardGenerationServiceV1(authority);
            RewardProfileResolutionV1 noDrop = Resolution(
                ProductionRewardSourceCatalogV1.ExplicitNoDropId);
            PersonalRewardGenerationResultV1 first = service.GenerateRunMinimum(
                Context(noDrop, ParticipantA, 77UL, 1001, 1));
            Assert.That(CountBoxes(first), Is.EqualTo(1));

            PersonalRewardGenerationResultV1 second = service.GenerateRunMinimum(
                Context(noDrop, ParticipantA, 77UL, 1002, 1));
            Assert.That(CountBoxes(second), Is.EqualTo(0));
            Assert.That(second.PacingAfter.TotalBoxesInRun, Is.EqualTo(1));
        }

        [Test]
        public void TenThousandSourcesNeverDuplicateExactGrantIdentity()
        {
            PersonalRewardGenerationServiceV1 service = NewService();
            var identities = new HashSet<StableId>();
            for (int ordinal = 1; ordinal <= 10000; ordinal++)
            {
                PersonalRewardGenerationResultV1 result = service.Generate(Context(
                    ProductionRewardSourceCatalogV1.NormalEnemyId,
                    ParticipantA,
                    1234UL,
                    ordinal,
                    1));
                for (int index = 0; index < result.Grants.Count; index++)
                {
                    Assert.That(
                        identities.Add(result.Grants[index].GrantStableId),
                        Is.True,
                        "Duplicate exact grant at source ordinal " + ordinal + ".");
                }
            }
        }

        private static PersonalRewardGenerationServiceV1 NewService()
        {
            return new PersonalRewardGenerationServiceV1(
                new ParticipantDropPacingAuthorityV1());
        }

        private static Distribution Simulate(StableId profileId, int samples)
        {
            PersonalRewardGenerationServiceV1 service = NewService();
            var distribution = new Distribution();
            for (int ordinal = 1; ordinal <= samples; ordinal++)
            {
                PersonalRewardGenerationResultV1 result = service.Generate(Context(
                    profileId,
                    ParticipantA,
                    923UL,
                    ordinal,
                    1));
                if (result.Grants.Count == 0)
                {
                    distribution.NoDrop++;
                    continue;
                }
                RewardGrantKindV1 kind = result.Grants[0].Kind;
                if (kind == RewardGrantKindV1.Money) distribution.Money++;
                else if (kind == RewardGrantKindV1.Scrap) distribution.Scrap++;
                else if (kind == RewardGrantKindV1.Strongbox) distribution.Strongboxes++;
            }
            return distribution;
        }

        private static CountDistribution SimulateBoxCounts(
            StableId profileId,
            int samples)
        {
            PersonalRewardGenerationServiceV1 service = NewService();
            var distribution = new CountDistribution();
            for (int ordinal = 1; ordinal <= samples; ordinal++)
            {
                int count = CountBoxes(service.Generate(Context(
                    profileId,
                    ParticipantA,
                    810UL,
                    ordinal,
                    1)));
                distribution.Counts[count] = distribution.Counts.ContainsKey(count)
                    ? distribution.Counts[count] + 1
                    : 1;
            }
            return distribution;
        }

        private static int CountBoxes(PersonalRewardGenerationResultV1 result)
        {
            int count = 0;
            for (int index = 0; index < result.Grants.Count; index++)
            {
                if (result.Grants[index].Kind == RewardGrantKindV1.Strongbox)
                    count += checked((int)result.Grants[index].Quantity);
            }
            return count;
        }

        private static PersonalRewardRollContextV1 Context(
            StableId profileReferenceId,
            StableId participantId,
            ulong seed,
            int sourceOrdinal,
            int roomLifecycle)
        {
            return Context(
                Resolution(profileReferenceId),
                participantId,
                seed,
                sourceOrdinal,
                roomLifecycle);
        }

        private static PersonalRewardRollContextV1 Context(
            RewardProfileResolutionV1 resolution,
            StableId participantId,
            ulong seed,
            int sourceOrdinal,
            int roomLifecycle)
        {
            return new PersonalRewardRollContextV1(
                StableId.Parse("run.test-rewards"),
                1,
                StableId.Create("terminal-event", "test-" + sourceOrdinal),
                1,
                RoomId,
                roomLifecycle,
                StableId.Create("placement", "test-" + sourceOrdinal),
                participantId,
                true,
                30,
                30,
                DifficultyId,
                GameModeId,
                Array.Empty<StableId>(),
                1000,
                1000,
                resolution,
                NoPacingPolicy(),
                RewardGenerationFingerprintV1.Compute(
                    "terminal-fact-test-" + sourceOrdinal),
                seed,
                1);
        }

        private static RewardProfileResolutionV1 Resolution(
            StableId profileReferenceId)
        {
            RewardSourceProfileV1 profile =
                ProductionRewardSourceCatalogV1.Get(profileReferenceId);
            return new RewardProfileResolutionV1(
                profileReferenceId,
                profile,
                profile,
                Array.Empty<StableId>());
        }

        private static RunDropPacingPolicyV1 NoPacingPolicy()
        {
            return new RunDropPacingPolicyV1(
                StableId.Parse("drop-pacing.test-disabled"),
                1,
                int.MaxValue,
                0,
                0,
                false,
                new[] { new DropSaturationBandV1(0, 1000000) },
                new[] { new DropSaturationBandV1(0, 1000000) });
        }

        private sealed class Distribution
        {
            public int NoDrop;
            public int Money;
            public int Scrap;
            public int Strongboxes;
        }

        private sealed class CountDistribution
        {
            public readonly Dictionary<int, int> Counts =
                new Dictionary<int, int>();
        }
    }
}

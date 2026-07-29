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
    public sealed class PersonalRewardGenerationTests
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
            PersonalRewardRollContext context = Context(
                RewardSourceCatalog.NormalEnemyId,
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
            PersonalRewardGenerationActions service = NewService();
            PersonalRewardGenerationResult left = service.Generate(Context(
                RewardSourceCatalog.NormalEnemyId,
                ParticipantA,
                113UL,
                1,
                1));
            PersonalRewardGenerationResult right = service.Generate(Context(
                RewardSourceCatalog.NormalEnemyId,
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
            ParticipantDropPacing authority =
                new ParticipantDropPacing();
            PersonalRewardGenerationActions service =
                new PersonalRewardGenerationActions(authority);
            PersonalRewardRollContext original = Context(
                RewardSourceCatalog.NormalEnemyId,
                ParticipantA,
                42UL,
                1,
                1);
            PersonalRewardGenerationResult first = service.Generate(original);
            PersonalRewardGenerationResult replay = service.Generate(original);
            Assert.That(replay, Is.SameAs(first));

            PersonalRewardRollContext conflict = Context(
                RewardSourceCatalog.NormalEnemyId,
                ParticipantA,
                43UL,
                1,
                1);
            Assert.That(conflict.OperationStableId, Is.EqualTo(original.OperationStableId));
            PersonalRewardGenerationResult rejected = service.Generate(conflict);
            Assert.That(
                rejected.Status,
                Is.EqualTo(PersonalRewardGenerationStatus.ConflictingReplay));
            Assert.That(rejected.Grants, Is.Empty);
        }

        [Test]
        public void ProductionAliasesShareTheSameProfileFingerprint()
        {
            RewardSourceProfile normal = RewardSourceCatalog.Get(
                RewardSourceCatalog.NormalEnemyId);
            Assert.That(
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.NormalPropId),
                Is.SameAs(normal));
            Assert.That(
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.NormalHiddenTreasureId),
                Is.SameAs(normal));

            RewardSourceProfile large = RewardSourceCatalog.Get(
                RewardSourceCatalog.LargeEnemyId);
            Assert.That(
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.RarePropId),
                Is.SameAs(large));
            Assert.That(
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.LargeTreasureLootId),
                Is.SameAs(RewardSourceCatalog.Get(
                    RewardSourceCatalog.ExtraRarePropId)));
        }

        [Test]
        public void NormalEnemyApproximatesNinetyEightTwoBeforePacing()
        {
            Distribution distribution = Simulate(
                RewardSourceCatalog.NormalEnemyId,
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
                RewardSourceCatalog.LargeEnemyId,
                20000);
            Assert.That(distribution.Money / 20000d, Is.InRange(0.58d, 0.62d));
            Assert.That(distribution.Scrap / 20000d, Is.InRange(0.07d, 0.09d));
            Assert.That(distribution.Strongboxes / 20000d, Is.InRange(0.30d, 0.34d));
        }

        [Test]
        public void ExtraBossCountApproximatesSeventyTwentyFiveFive()
        {
            CountDistribution distribution = SimulateBoxCounts(
                RewardSourceCatalog.ExtraBossEnemyId,
                20000);
            Assert.That(distribution.Counts[3] / 20000d, Is.InRange(0.68d, 0.72d));
            Assert.That(distribution.Counts[4] / 20000d, Is.InRange(0.23d, 0.27d));
            Assert.That(distribution.Counts[5] / 20000d, Is.InRange(0.04d, 0.06d));
        }

        [Test]
        public void ExtraRarePropCountApproximatesSeventyTwentyFiveFive()
        {
            CountDistribution distribution = SimulateBoxCounts(
                RewardSourceCatalog.ExtraRarePropId,
                20000);
            Assert.That(distribution.Counts[2] / 20000d, Is.InRange(0.68d, 0.72d));
            Assert.That(distribution.Counts[3] / 20000d, Is.InRange(0.23d, 0.27d));
            Assert.That(distribution.Counts[4] / 20000d, Is.InRange(0.04d, 0.06d));
        }

        [Test]
        public void EveryEligibleParticipantGetsDefaultBossGuarantee()
        {
            PersonalRewardGenerationActions service = NewService();
            IReadOnlyList<PersonalRewardGenerationResult> results =
                service.GenerateForParticipants(new[]
                {
                    Context(RewardSourceCatalog.BossEnemyId, ParticipantA, 99UL, 1, 1),
                    Context(RewardSourceCatalog.BossEnemyId, ParticipantB, 99UL, 1, 1),
                    Context(RewardSourceCatalog.BossEnemyId, ParticipantC, 99UL, 1, 1),
                    Context(RewardSourceCatalog.BossEnemyId, ParticipantD, 99UL, 1, 1),
                });
            Assert.That(results, Has.Count.EqualTo(4));
            var exactBoxIds = new HashSet<StableId>();
            for (int index = 0; index < results.Count; index++)
            {
                Assert.That(CountBoxes(results[index]), Is.EqualTo(1));
                for (int grantIndex = 0; grantIndex < results[index].Grants.Count; grantIndex++)
                {
                    RewardGrant grant = results[index].Grants[grantIndex];
                    if (grant.Kind == RewardGrantKind.Strongbox)
                        Assert.That(exactBoxIds.Add(grant.GrantStableId), Is.True);
                }
            }
        }

        [Test]
        public void ModeReplacementCanRemoveBossStrongboxCompletely()
        {
            RewardSourceProfile moneyOnly = RewardSourceProfile.Create(
                StableId.Parse("drop-source.test-survival-boss"),
                null,
                new[]
                {
                    RewardRollGroup.CreateGuaranteed(
                        StableId.Parse("drop-group.test-survival-boss-money"),
                        0,
                        RewardBoxPacingMode.None,
                        new[]
                        {
                            RewardOutcome.CreateGrant(
                                StableId.Parse("drop-outcome.test-survival-boss-money"),
                                RewardGrantSpecification.Create(
                                    StableId.Parse("drop-grant.test-survival-boss-money"),
                                    RewardGrantKind.Money,
                                    StableId.Parse("currency.money"),
                                    RewardQuantityRange.Fixed(50),
                                    Array.Empty<RewardScalingInputDescriptor>()),
                                1),
                        }),
                });
            RewardProfileResolution resolution = new RewardProfileResolver().Resolve(
                RewardSourceCatalog.BossEnemyId,
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.BossEnemyId),
                RewardProfileOverride.Replace(
                    StableId.Parse("game-mode.survival-boss-override"),
                    moneyOnly),
                null,
                null,
                null,
                null);
            PersonalRewardGenerationResult result = NewService().Generate(Context(
                resolution,
                ParticipantA,
                5UL,
                1,
                1));
            Assert.That(CountBoxes(result), Is.EqualTo(0));
            Assert.That(result.Grants, Has.Count.EqualTo(1));
            Assert.That(result.Grants[0].Kind, Is.EqualTo(RewardGrantKind.Money));
        }

        [Test]
        public void OnePlayersRandomBoxDoesNotChangeAnotherPlayersPacing()
        {
            for (ulong seed = 1UL; seed < 1000UL; seed++)
            {
                ParticipantDropPacing authority =
                    new ParticipantDropPacing();
                PersonalRewardGenerationActions service =
                    new PersonalRewardGenerationActions(authority);
                PersonalRewardGenerationResult playerA = service.Generate(Context(
                    RewardSourceCatalog.LargeEnemyId,
                    ParticipantA,
                    seed,
                    1,
                    1));
                if (CountBoxes(playerA) == 0)
                    continue;

                PersonalRewardGenerationResult playerB = service.Generate(Context(
                    RewardSourceCatalog.LargeEnemyId,
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
            ParticipantDropPacing uninterruptedAuthority =
                new ParticipantDropPacing();
            PersonalRewardGenerationActions uninterrupted =
                new PersonalRewardGenerationActions(uninterruptedAuthority);
            for (int ordinal = 1; ordinal <= 15; ordinal++)
            {
                uninterrupted.Generate(Context(
                    RewardSourceCatalog.SmallEnemyId,
                    ParticipantA,
                    1UL,
                    ordinal,
                    1));
            }

            ParticipantDropPacingState snapshot;
            Assert.That(uninterruptedAuthority.TryExport(
                StableId.Parse("run.test-rewards"),
                1,
                ParticipantA,
                out snapshot), Is.True);

            ParticipantDropPacing restoredAuthority =
                new ParticipantDropPacing();
            restoredAuthority.Restore(snapshot);
            PersonalRewardGenerationActions restored =
                new PersonalRewardGenerationActions(restoredAuthority);
            PersonalRewardRollContext next = Context(
                RewardSourceCatalog.SmallEnemyId,
                ParticipantA,
                1UL,
                16,
                1);
            PersonalRewardGenerationResult uninterruptedNext =
                uninterrupted.Generate(next);
            PersonalRewardGenerationResult restoredNext = restored.Generate(next);
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
            ParticipantDropPacing authority =
                new ParticipantDropPacing();
            PersonalRewardGenerationActions service =
                new PersonalRewardGenerationActions(authority);
            RewardProfileResolution noDrop = Resolution(
                RewardSourceCatalog.ExplicitNoDropId);
            PersonalRewardGenerationResult first = service.GenerateRunMinimum(
                Context(noDrop, ParticipantA, 77UL, 1001, 1));
            Assert.That(CountBoxes(first), Is.EqualTo(1));

            PersonalRewardGenerationResult second = service.GenerateRunMinimum(
                Context(noDrop, ParticipantA, 77UL, 1002, 1));
            Assert.That(CountBoxes(second), Is.EqualTo(0));
            Assert.That(second.PacingAfter.TotalBoxesInRun, Is.EqualTo(1));
        }

        [Test]
        public void TenThousandSourcesNeverDuplicateExactGrantIdentity()
        {
            PersonalRewardGenerationActions service = NewService();
            var identities = new HashSet<StableId>();
            for (int ordinal = 1; ordinal <= 10000; ordinal++)
            {
                PersonalRewardGenerationResult result = service.Generate(Context(
                    RewardSourceCatalog.NormalEnemyId,
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

        private static PersonalRewardGenerationActions NewService()
        {
            return new PersonalRewardGenerationActions(
                new ParticipantDropPacing());
        }

        private static Distribution Simulate(StableId profileId, int samples)
        {
            PersonalRewardGenerationActions service = NewService();
            var distribution = new Distribution();
            for (int ordinal = 1; ordinal <= samples; ordinal++)
            {
                PersonalRewardGenerationResult result = service.Generate(Context(
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
                RewardGrantKind kind = result.Grants[0].Kind;
                if (kind == RewardGrantKind.Money) distribution.Money++;
                else if (kind == RewardGrantKind.Scrap) distribution.Scrap++;
                else if (kind == RewardGrantKind.Strongbox) distribution.Strongboxes++;
            }
            return distribution;
        }

        private static CountDistribution SimulateBoxCounts(
            StableId profileId,
            int samples)
        {
            PersonalRewardGenerationActions service = NewService();
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

        private static int CountBoxes(PersonalRewardGenerationResult result)
        {
            int count = 0;
            for (int index = 0; index < result.Grants.Count; index++)
            {
                if (result.Grants[index].Kind == RewardGrantKind.Strongbox)
                    count += checked((int)result.Grants[index].Quantity);
            }
            return count;
        }

        private static PersonalRewardRollContext Context(
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

        private static PersonalRewardRollContext Context(
            RewardProfileResolution resolution,
            StableId participantId,
            ulong seed,
            int sourceOrdinal,
            int roomLifecycle)
        {
            return new PersonalRewardRollContext(
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
                RewardGenerationFingerprint.Compute(
                    "terminal-fact-test-" + sourceOrdinal),
                seed,
                1);
        }

        private static RewardProfileResolution Resolution(
            StableId profileReferenceId)
        {
            RewardSourceProfile profile =
                RewardSourceCatalog.Get(profileReferenceId);
            return new RewardProfileResolution(
                profileReferenceId,
                profile,
                profile,
                Array.Empty<StableId>());
        }

        private static RunDropPacingPolicy NoPacingPolicy()
        {
            return new RunDropPacingPolicy(
                StableId.Parse("drop-pacing.test-disabled"),
                1,
                int.MaxValue,
                0,
                0,
                false,
                new[] { new DropSaturationBand(0, 1000000) },
                new[] { new DropSaturationBand(0, 1000000) });
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

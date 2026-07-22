using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Tests.EditMode.Rewards.Drops
{
    public sealed class RunRewardRuntimeSnapshotV1Tests
    {
        [Test]
        public void SnapshotPreservesPacingAndPendingPersonalDeliveryDeterministically()
        {
            StableId runId = StableId.Parse("run.reward-snapshot-test");
            StableId participantId = StableId.Parse("participant.reward-snapshot-test");
            StableId modeId = StableId.Parse("game-mode.campaign");
            RunDropPacingPolicyV1 pacingPolicy =
                ProductionRunDropPacingCatalogV1.Resolve(modeId, null);
            RewardSourceProfileV1 profile =
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.NormalEnemyId);
            RewardProfileResolutionV1 resolution =
                new RewardProfileResolverV1().Resolve(
                    ProductionRewardSourceCatalogV1.NormalEnemyId,
                    profile,
                    null,
                    null,
                    null,
                    Array.Empty<RewardProfileOverrideV1>(),
                    null);
            var context = new PersonalRewardRollContextV1(
                runId,
                1,
                StableId.Parse("terminal-source.reward-snapshot-test"),
                1,
                StableId.Parse("room.reward-snapshot-test"),
                1,
                StableId.Parse("placement.reward-snapshot-test"),
                participantId,
                true,
                30,
                30,
                StableId.Parse("difficulty.normal"),
                modeId,
                Array.Empty<StableId>(),
                1000,
                1000,
                resolution,
                pacingPolicy,
                "terminal-fact-fingerprint-reward-snapshot-test",
                123456UL,
                1);
            PersonalRewardGenerationResultV1 generated =
                new PersonalRewardGenerationServiceV1(
                    new ParticipantDropPacingAuthorityV1())
                    .Generate(context);
            Assert.That(generated.IsSuccess, Is.True);

            var environment = new RunRewardEnvironmentSnapshotV1(
                modeId,
                Array.Empty<StableId>(),
                1000,
                1000,
                pacingPolicy);
            var participant = new RunRewardParticipantStateV1(
                participantId,
                30,
                true,
                true,
                true,
                true,
                false);
            var delivery = new PersonalRewardDeliveryEnvelopeV1(
                generated,
                PersonalRewardDeliveryStateV1.Pending,
                string.Empty);

            var first = new RunRewardRuntimeSnapshotV1(
                runId,
                1,
                environment,
                new[] { participant },
                new[] { generated.PacingAfter },
                new[] { delivery });
            var second = new RunRewardRuntimeSnapshotV1(
                runId,
                1,
                environment,
                new[] { participant },
                new[] { generated.PacingAfter },
                new[] { delivery });

            Assert.That(first.PacingStates.Count, Is.EqualTo(1));
            Assert.That(first.Deliveries.Count, Is.EqualTo(1));
            Assert.That(
                first.Deliveries[0].State,
                Is.EqualTo(PersonalRewardDeliveryStateV1.Pending));
            Assert.That(
                first.Deliveries[0].Result.Context.ParticipantStableId,
                Is.EqualTo(participantId));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(
                second.ToCanonicalString(),
                Is.EqualTo(first.ToCanonicalString()));
        }

        [Test]
        public void DeliveredEnvelopeRejectsMissingDeliveryFingerprint()
        {
            StableId runId = StableId.Parse("run.reward-delivery-validation");
            StableId participantId = StableId.Parse("participant.reward-delivery-validation");
            StableId modeId = StableId.Parse("game-mode.campaign");
            RunDropPacingPolicyV1 pacingPolicy =
                ProductionRunDropPacingCatalogV1.Resolve(modeId, null);
            RewardSourceProfileV1 profile =
                ProductionRewardSourceCatalogV1.Get(
                    ProductionRewardSourceCatalogV1.ExplicitNoDropId);
            RewardProfileResolutionV1 resolution =
                new RewardProfileResolverV1().Resolve(
                    ProductionRewardSourceCatalogV1.ExplicitNoDropId,
                    profile,
                    null,
                    null,
                    null,
                    Array.Empty<RewardProfileOverrideV1>(),
                    null);
            var context = new PersonalRewardRollContextV1(
                runId,
                1,
                StableId.Parse("terminal-source.reward-delivery-validation"),
                1,
                StableId.Parse("room.reward-delivery-validation"),
                1,
                StableId.Parse("placement.reward-delivery-validation"),
                participantId,
                true,
                1,
                1,
                StableId.Parse("difficulty.normal"),
                modeId,
                Array.Empty<StableId>(),
                1000,
                1000,
                resolution,
                pacingPolicy,
                "terminal-fact-fingerprint-reward-delivery-validation",
                7UL,
                1);
            PersonalRewardGenerationResultV1 generated =
                new PersonalRewardGenerationServiceV1(
                    new ParticipantDropPacingAuthorityV1())
                    .Generate(context);

            Assert.Throws<ArgumentException>(delegate
            {
                new PersonalRewardDeliveryEnvelopeV1(
                    generated,
                    PersonalRewardDeliveryStateV1.Delivered,
                    string.Empty);
            });
        }
    }
}

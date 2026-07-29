using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Tests.EditMode.Rewards.Drops
{
    public sealed class RunRewardLiveSnapshotTests
    {
        [Test]
        public void SnapshotPreservesPacingAndPendingPersonalDeliveryDeterministically()
        {
            StableId runId = StableId.Parse("run.reward-snapshot-test");
            StableId participantId = StableId.Parse("participant.reward-snapshot-test");
            StableId modeId = StableId.Parse("game-mode.campaign");
            RunDropPacingPolicy pacingPolicy =
                RunDropPacingCatalog.Resolve(modeId, null);
            RewardSourceProfile profile =
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.NormalEnemyId);
            RewardProfileResolution resolution =
                new RewardProfileResolver().Resolve(
                    RewardSourceCatalog.NormalEnemyId,
                    profile,
                    null,
                    null,
                    null,
                    Array.Empty<RewardProfileOverride>(),
                    null);
            var context = new PersonalRewardRollContext(
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
            PersonalRewardGenerationResult generated =
                new PersonalRewardGenerationActions(
                    new ParticipantDropPacing())
                    .Generate(context);
            Assert.That(generated.IsSuccess, Is.True);

            var environment = new RunRewardEnvironmentSnapshot(
                modeId,
                Array.Empty<StableId>(),
                1000,
                1000,
                pacingPolicy);
            var participant = new RunRewardParticipantState(
                participantId,
                30,
                true,
                true,
                true,
                true,
                false);
            var delivery = new PersonalRewardDeliveryEnvelope(
                generated,
                PersonalRewardDeliveryState.Pending,
                string.Empty);

            var first = new RunRewardLiveSnapshot(
                runId,
                1,
                environment,
                new[] { participant },
                new[] { generated.PacingAfter },
                new[] { delivery });
            var second = new RunRewardLiveSnapshot(
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
                Is.EqualTo(PersonalRewardDeliveryState.Pending));
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
            RunDropPacingPolicy pacingPolicy =
                RunDropPacingCatalog.Resolve(modeId, null);
            RewardSourceProfile profile =
                RewardSourceCatalog.Get(
                    RewardSourceCatalog.ExplicitNoDropId);
            RewardProfileResolution resolution =
                new RewardProfileResolver().Resolve(
                    RewardSourceCatalog.ExplicitNoDropId,
                    profile,
                    null,
                    null,
                    null,
                    Array.Empty<RewardProfileOverride>(),
                    null);
            var context = new PersonalRewardRollContext(
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
            PersonalRewardGenerationResult generated =
                new PersonalRewardGenerationActions(
                    new ParticipantDropPacing())
                    .Generate(context);

            Assert.Throws<ArgumentException>(delegate
            {
                new PersonalRewardDeliveryEnvelope(
                    generated,
                    PersonalRewardDeliveryState.Delivered,
                    string.Empty);
            });
        }
    }
}

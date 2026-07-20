using NUnit.Framework;
using ShooterMover.Application.Modifiers.Events;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.Events;

namespace ShooterMover.Tests.EditMode.Modifiers.Events
{
    public sealed class EventStampedCommandEnvelopeV1Tests
    {
        [Test]
        public void RewardDropAndOpeningCommands_RecordExactEventSnapshotFingerprint()
        {
            FrozenEventModifierContextV1 context = FrozenContext(150L);

            EventStampedCommandEnvelopeV1 reward =
                EventStampedCommandEnvelopeV1.ForRewardGeneration(
                    "reward-command-fingerprint",
                    context);
            EventStampedCommandEnvelopeV1 drop =
                EventStampedCommandEnvelopeV1.ForDropGeneration(
                    "drop-command-fingerprint",
                    context);
            EventStampedCommandEnvelopeV1 opening =
                EventStampedCommandEnvelopeV1.ForStrongboxOpening(
                    "opening-command-fingerprint",
                    context);

            Assert.That(
                reward.ActiveEventSnapshotFingerprint,
                Is.EqualTo(context.ActiveEventSnapshotFingerprint));
            Assert.That(
                drop.ActiveEventSnapshotFingerprint,
                Is.EqualTo(context.ActiveEventSnapshotFingerprint));
            Assert.That(
                opening.ActiveEventSnapshotFingerprint,
                Is.EqualTo(context.ActiveEventSnapshotFingerprint));
            Assert.That(reward.CommandKind, Is.EqualTo(
                EventStampedCommandKindV1.RewardGeneration));
            Assert.That(drop.CommandKind, Is.EqualTo(
                EventStampedCommandKindV1.DropGeneration));
            Assert.That(opening.CommandKind, Is.EqualTo(
                EventStampedCommandKindV1.StrongboxOpening));
        }

        [Test]
        public void SameCommandAndFrozenContext_ProduceIdenticalEnvelopeFingerprint()
        {
            FrozenEventModifierContextV1 context = FrozenContext(150L);

            EventStampedCommandEnvelopeV1 first =
                EventStampedCommandEnvelopeV1.ForStrongboxOpening(
                    "opening-command-fingerprint",
                    context);
            EventStampedCommandEnvelopeV1 second =
                EventStampedCommandEnvelopeV1.ForStrongboxOpening(
                    "opening-command-fingerprint",
                    context);

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(
                first.ToCanonicalString(),
                Is.EqualTo(second.ToCanonicalString()));
        }

        [Test]
        public void ChangedEventSnapshot_ChangesStampedCommandFingerprint()
        {
            EventStampedCommandEnvelopeV1 active =
                EventStampedCommandEnvelopeV1.ForRewardGeneration(
                    "reward-command-fingerprint",
                    FrozenContext(150L));
            EventStampedCommandEnvelopeV1 expired =
                EventStampedCommandEnvelopeV1.ForRewardGeneration(
                    "reward-command-fingerprint",
                    FrozenContext(250L));

            Assert.That(
                active.ActiveEventSnapshotFingerprint,
                Is.Not.EqualTo(expired.ActiveEventSnapshotFingerprint));
            Assert.That(active.Fingerprint, Is.Not.EqualTo(expired.Fingerprint));
        }

        private static FrozenEventModifierContextV1 FrozenContext(
            long unixSeconds)
        {
            var definition = new SpecialEventDefinitionV1(
                SpecialEventDefinitionV1.CurrentSchemaVersion,
                "content.fixture.v1",
                "event.double-drops",
                new EventActivationWindowV1(100L, 200L),
                10,
                SpecialEventOverlapModeV1.Combine,
                new[]
                {
                    new EventModifierDescriptorV1(
                        EventModifierTargetIdsV1.RewardStrongboxWeight,
                        RuntimeModifierOperationV1.Multiplicative,
                        2m),
                });
            var catalog = new SpecialEventCatalogV1(
                "events.fixture.v1",
                new[] { definition });
            var service = new ActiveEventModifierProjectionServiceV1(
                catalog,
                new FixedClockV1(unixSeconds));
            return service.ProjectActiveEvents().Snapshot.FreezeForCommand();
        }

        private sealed class FixedClockV1 : IAuthoritativeEventClockV1
        {
            private readonly long unixSeconds;

            public FixedClockV1(long unixSeconds)
            {
                this.unixSeconds = unixSeconds;
            }

            public long GetCurrentUnixTimeSeconds()
            {
                return unixSeconds;
            }
        }
    }
}

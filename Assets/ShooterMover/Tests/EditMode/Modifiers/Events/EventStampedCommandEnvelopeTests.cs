using NUnit.Framework;
using ShooterMover.Application.Modifiers.Events;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.Events;

namespace ShooterMover.Tests.EditMode.Modifiers.Events
{
    public sealed class EventStampedCommandEnvelopeTests
    {
        [Test]
        public void RewardDropAndOpeningCommands_RecordExactEventSnapshotFingerprint()
        {
            FrozenEventModifierContext context = FrozenContext(150L);

            EventStampedCommandEnvelope reward =
                EventStampedCommandEnvelope.ForRewardGeneration(
                    "reward-command-fingerprint",
                    context);
            EventStampedCommandEnvelope drop =
                EventStampedCommandEnvelope.ForDropGeneration(
                    "drop-command-fingerprint",
                    context);
            EventStampedCommandEnvelope opening =
                EventStampedCommandEnvelope.ForStrongboxOpening(
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
                EventStampedCommandKind.RewardGeneration));
            Assert.That(drop.CommandKind, Is.EqualTo(
                EventStampedCommandKind.DropGeneration));
            Assert.That(opening.CommandKind, Is.EqualTo(
                EventStampedCommandKind.StrongboxOpening));
        }

        [Test]
        public void SameCommandAndFrozenContext_ProduceIdenticalEnvelopeFingerprint()
        {
            FrozenEventModifierContext context = FrozenContext(150L);

            EventStampedCommandEnvelope first =
                EventStampedCommandEnvelope.ForStrongboxOpening(
                    "opening-command-fingerprint",
                    context);
            EventStampedCommandEnvelope second =
                EventStampedCommandEnvelope.ForStrongboxOpening(
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
            EventStampedCommandEnvelope active =
                EventStampedCommandEnvelope.ForRewardGeneration(
                    "reward-command-fingerprint",
                    FrozenContext(150L));
            EventStampedCommandEnvelope expired =
                EventStampedCommandEnvelope.ForRewardGeneration(
                    "reward-command-fingerprint",
                    FrozenContext(250L));

            Assert.That(
                active.ActiveEventSnapshotFingerprint,
                Is.Not.EqualTo(expired.ActiveEventSnapshotFingerprint));
            Assert.That(active.Fingerprint, Is.Not.EqualTo(expired.Fingerprint));
        }

        private static FrozenEventModifierContext FrozenContext(
            long unixSeconds)
        {
            var definition = new SpecialEventDefinition(
                SpecialEventDefinition.CurrentSchemaVersion,
                "content.fixture.v1",
                "event.double-drops",
                new EventActivationWindow(100L, 200L),
                10,
                SpecialEventOverlapMode.Combine,
                new[]
                {
                    new EventModifierDescriptor(
                        EventModifierTargetIds.RewardStrongboxWeight,
                        LiveModifierOperation.Multiplicative,
                        2m),
                });
            var catalog = new SpecialEventCatalog(
                "events.fixture.v1",
                new[] { definition });
            var service = new ActiveEventModifierViewActions(
                catalog,
                new FixedClock(unixSeconds));
            return service.ProjectActiveEvents().Snapshot.FreezeForCommand();
        }

        private sealed class FixedClock : IAuthoritativeEventClock
        {
            private readonly long unixSeconds;

            public FixedClock(long unixSeconds)
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

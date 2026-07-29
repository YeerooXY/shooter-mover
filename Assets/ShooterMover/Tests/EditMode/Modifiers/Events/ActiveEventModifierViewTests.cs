using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Modifiers.Events;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.Events;

namespace ShooterMover.Tests.EditMode.Modifiers.Events
{
    public sealed class ActiveEventModifierViewTests
    {
        [Test]
        public void Event_IsInactiveBeforeStart_ActiveInside_AndInactiveAtEnd()
        {
            var clock = new MutableClock(99L);
            var service = new ActiveEventModifierViewActions(
                Catalog(Event(
                    "event.double-drops",
                    100L,
                    200L,
                    EventModifierTargetIds.RewardStrongboxWeight,
                    2m)),
                clock);

            ActiveEventViewResult before =
                service.ProjectActiveEvents();
            clock.UnixSeconds = 100L;
            ActiveEventViewResult start =
                service.ProjectActiveEvents();
            clock.UnixSeconds = 199L;
            ActiveEventViewResult inside =
                service.ProjectActiveEvents();
            clock.UnixSeconds = 200L;
            ActiveEventViewResult after =
                service.ProjectActiveEvents();

            Assert.That(before.Succeeded, Is.True);
            Assert.That(before.Snapshot.ActiveEvents, Is.Empty);
            Assert.That(start.Snapshot.ActiveEvents.Count, Is.EqualTo(1));
            Assert.That(inside.Snapshot.ActiveEvents.Count, Is.EqualTo(1));
            Assert.That(after.Snapshot.ActiveEvents, Is.Empty);
        }

        [Test]
        public void SameClockAndCatalog_ProduceIdenticalSnapshotFingerprint()
        {
            var clock = new MutableClock(150L);
            SpecialEventCatalog catalog = Catalog(
                Event(
                    "event.money",
                    100L,
                    200L,
                    EventModifierTargetIds.MoneyQuantity,
                    1.5m,
                    priority: 5),
                Event(
                    "event.xp",
                    100L,
                    200L,
                    EventModifierTargetIds.ExperienceQuantity,
                    2m,
                    priority: 10));
            var service = new ActiveEventModifierViewActions(
                catalog,
                clock);

            ActiveEventViewResult first =
                service.ProjectActiveEvents();
            ActiveEventViewResult second =
                service.ProjectActiveEvents();

            Assert.That(first.Snapshot.Fingerprint, Is.EqualTo(
                second.Snapshot.Fingerprint));
            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(
                first.Snapshot.ActiveEvents.Select(item => item.EventId),
                Is.EqualTo(new[] { "event.xp", "event.money" }));
        }

        [Test]
        public void DoubleDropEvent_MultipliesOnlyStrongboxWeight()
        {
            var service = new ActiveEventModifierViewActions(
                Catalog(Event(
                    "event.double-drops",
                    100L,
                    200L,
                    EventModifierTargetIds.RewardStrongboxWeight,
                    2m)),
                new MutableClock(150L));

            ActiveEventModifierSnapshot snapshot =
                service.ProjectActiveEvents().Snapshot;

            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIds.RewardStrongboxWeight,
                    100m).FinalValue,
                Is.EqualTo(200m));
            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIds.MoneyQuantity,
                    100m).FinalValue,
                Is.EqualTo(100m));
            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIds.ExperienceQuantity,
                    100m).FinalValue,
                Is.EqualTo(100m));
        }

        [Test]
        public void FrozenRewardContext_RemainsUnchangedAfterEventExpires()
        {
            var clock = new MutableClock(150L);
            var service = new ActiveEventModifierViewActions(
                Catalog(Event(
                    "event.double-money",
                    100L,
                    200L,
                    EventModifierTargetIds.MoneyQuantity,
                    2m)),
                clock);

            ActiveEventModifierSnapshot activeSnapshot =
                service.ProjectActiveEvents().Snapshot;
            FrozenEventModifierContext frozen =
                activeSnapshot.FreezeForCommand();
            string recordedSnapshotFingerprint =
                frozen.ActiveEventSnapshotFingerprint;

            clock.UnixSeconds = 250L;
            ActiveEventModifierSnapshot expiredSnapshot =
                service.ProjectActiveEvents().Snapshot;

            Assert.That(
                expiredSnapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIds.MoneyQuantity,
                    10m).FinalValue,
                Is.EqualTo(10m));
            Assert.That(
                frozen.Evaluate(
                    EventModifierTargetIds.MoneyQuantity,
                    10m).FinalValue,
                Is.EqualTo(20m));
            Assert.That(
                frozen.ActiveEventSnapshotFingerprint,
                Is.EqualTo(recordedSnapshotFingerprint));
            Assert.That(
                frozen.ActiveEventSnapshotFingerprint,
                Is.Not.EqualTo(expiredSnapshot.Fingerprint));
        }

        [Test]
        public void ExclusiveOverlap_RejectsDeterministically()
        {
            SpecialEventDefinition first = Event(
                "event.alpha",
                100L,
                200L,
                EventModifierTargetIds.MoneyQuantity,
                2m,
                overlapMode: SpecialEventOverlapMode.Exclusive);
            SpecialEventDefinition second = Event(
                "event.beta",
                100L,
                200L,
                EventModifierTargetIds.ExperienceQuantity,
                2m);
            var clock = new MutableClock(150L);

            ActiveEventViewResult left =
                new ActiveEventModifierViewActions(
                    Catalog(first, second),
                    clock).ProjectActiveEvents();
            ActiveEventViewResult right =
                new ActiveEventModifierViewActions(
                    Catalog(second, first),
                    clock).ProjectActiveEvents();

            Assert.That(left.Status, Is.EqualTo(
                ActiveEventViewStatus.ConflictingActiveEvents));
            Assert.That(left.Snapshot, Is.Null);
            Assert.That(left.Conflicts.Count, Is.EqualTo(1));
            Assert.That(left.Conflicts[0].ReasonCode, Is.EqualTo(
                "exclusive-overlap"));
            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
        }

        [Test]
        public void ExplicitExclusion_RejectsEvenWhenBothEventsAllowCombining()
        {
            SpecialEventDefinition first = Event(
                "event.alpha",
                100L,
                200L,
                EventModifierTargetIds.MoneyQuantity,
                2m,
                excludedEventIds: new[] { "event.beta" });
            SpecialEventDefinition second = Event(
                "event.beta",
                100L,
                200L,
                EventModifierTargetIds.ExperienceQuantity,
                2m);

            ActiveEventViewResult result =
                new ActiveEventModifierViewActions(
                    Catalog(first, second),
                    new MutableClock(150L)).ProjectActiveEvents();

            Assert.That(result.Status, Is.EqualTo(
                ActiveEventViewStatus.ConflictingActiveEvents));
            Assert.That(result.Conflicts.Single().ReasonCode, Is.EqualTo(
                "explicit-exclusion"));
        }

        [Test]
        public void CombiningEvents_ApplyThroughMergedModifierLanguage()
        {
            var service = new ActiveEventModifierViewActions(
                Catalog(
                    Event(
                        "event.money-a",
                        100L,
                        200L,
                        EventModifierTargetIds.MoneyQuantity,
                        1.5m),
                    Event(
                        "event.money-b",
                        100L,
                        200L,
                        EventModifierTargetIds.MoneyQuantity,
                        2m)),
                new MutableClock(150L));

            decimal result = service.ProjectActiveEvents()
                .Snapshot
                .ModifierSnapshot
                .Evaluate(EventModifierTargetIds.MoneyQuantity, 10m)
                .FinalValue;

            Assert.That(result, Is.EqualTo(30m));
        }

        [Test]
        public void UnknownTarget_RemainsRepresentableUntilAConsumerRequestsIt()
        {
            const string futureTarget = "future.rewards.mystery-scale";
            ActiveEventModifierSnapshot snapshot =
                new ActiveEventModifierViewActions(
                    Catalog(Event(
                        "event.future-target",
                        100L,
                        200L,
                        futureTarget,
                        1.5m)),
                    new MutableClock(150L))
                .ProjectActiveEvents()
                .Snapshot;

            Assert.That(
                snapshot.ModifierSnapshot.Modifiers.Single().TargetId,
                Is.EqualTo(futureTarget));
            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIds.MoneyQuantity,
                    10m).FinalValue,
                Is.EqualTo(10m));
            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    futureTarget,
                    10m).FinalValue,
                Is.EqualTo(15m));
        }

        [Test]
        public void CatalogFingerprint_IsIndependentOfDefinitionInputOrder()
        {
            SpecialEventDefinition first = Event(
                "event.alpha",
                100L,
                200L,
                EventModifierTargetIds.MoneyQuantity,
                2m);
            SpecialEventDefinition second = Event(
                "event.beta",
                100L,
                200L,
                EventModifierTargetIds.ExperienceQuantity,
                2m);

            Assert.That(
                Catalog(first, second).Fingerprint,
                Is.EqualTo(Catalog(second, first).Fingerprint));
        }

        private static SpecialEventCatalog Catalog(
            params SpecialEventDefinition[] definitions)
        {
            return new SpecialEventCatalog(
                "events.fixture.v1",
                definitions);
        }

        private static SpecialEventDefinition Event(
            string eventId,
            long start,
            long end,
            string targetId,
            decimal multiplier,
            int priority = 0,
            SpecialEventOverlapMode overlapMode =
                SpecialEventOverlapMode.Combine,
            IEnumerable<string> excludedEventIds = null)
        {
            return new SpecialEventDefinition(
                SpecialEventDefinition.CurrentSchemaVersion,
                "content.fixture.v1",
                eventId,
                new EventActivationWindow(start, end),
                priority,
                overlapMode,
                new[]
                {
                    new EventModifierDescriptor(
                        targetId,
                        LiveModifierOperation.Multiplicative,
                        multiplier),
                },
                excludedEventIds);
        }

        private sealed class MutableClock : IAuthoritativeEventClock
        {
            public MutableClock(long unixSeconds)
            {
                UnixSeconds = unixSeconds;
            }

            public long UnixSeconds { get; set; }

            public long GetCurrentUnixTimeSeconds()
            {
                return UnixSeconds;
            }
        }
    }
}

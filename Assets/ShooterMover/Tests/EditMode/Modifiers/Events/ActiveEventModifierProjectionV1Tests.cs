using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Modifiers.Events;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.Events;

namespace ShooterMover.Tests.EditMode.Modifiers.Events
{
    public sealed class ActiveEventModifierProjectionV1Tests
    {
        [Test]
        public void Event_IsInactiveBeforeStart_ActiveInside_AndInactiveAtEnd()
        {
            var clock = new MutableClockV1(99L);
            var service = new ActiveEventModifierProjectionServiceV1(
                Catalog(Event(
                    "event.double-drops",
                    100L,
                    200L,
                    EventModifierTargetIdsV1.RewardStrongboxWeight,
                    2m)),
                clock);

            ActiveEventProjectionResultV1 before =
                service.ProjectActiveEvents();
            clock.UnixSeconds = 100L;
            ActiveEventProjectionResultV1 start =
                service.ProjectActiveEvents();
            clock.UnixSeconds = 199L;
            ActiveEventProjectionResultV1 inside =
                service.ProjectActiveEvents();
            clock.UnixSeconds = 200L;
            ActiveEventProjectionResultV1 after =
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
            var clock = new MutableClockV1(150L);
            SpecialEventCatalogV1 catalog = Catalog(
                Event(
                    "event.money",
                    100L,
                    200L,
                    EventModifierTargetIdsV1.MoneyQuantity,
                    1.5m,
                    priority: 5),
                Event(
                    "event.xp",
                    100L,
                    200L,
                    EventModifierTargetIdsV1.ExperienceQuantity,
                    2m,
                    priority: 10));
            var service = new ActiveEventModifierProjectionServiceV1(
                catalog,
                clock);

            ActiveEventProjectionResultV1 first =
                service.ProjectActiveEvents();
            ActiveEventProjectionResultV1 second =
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
            var service = new ActiveEventModifierProjectionServiceV1(
                Catalog(Event(
                    "event.double-drops",
                    100L,
                    200L,
                    EventModifierTargetIdsV1.RewardStrongboxWeight,
                    2m)),
                new MutableClockV1(150L));

            ActiveEventModifierSnapshotV1 snapshot =
                service.ProjectActiveEvents().Snapshot;

            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIdsV1.RewardStrongboxWeight,
                    100m).FinalValue,
                Is.EqualTo(200m));
            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIdsV1.MoneyQuantity,
                    100m).FinalValue,
                Is.EqualTo(100m));
            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIdsV1.ExperienceQuantity,
                    100m).FinalValue,
                Is.EqualTo(100m));
        }

        [Test]
        public void FrozenRewardContext_RemainsUnchangedAfterEventExpires()
        {
            var clock = new MutableClockV1(150L);
            var service = new ActiveEventModifierProjectionServiceV1(
                Catalog(Event(
                    "event.double-money",
                    100L,
                    200L,
                    EventModifierTargetIdsV1.MoneyQuantity,
                    2m)),
                clock);

            ActiveEventModifierSnapshotV1 activeSnapshot =
                service.ProjectActiveEvents().Snapshot;
            FrozenEventModifierContextV1 frozen =
                activeSnapshot.FreezeForCommand();
            string recordedSnapshotFingerprint =
                frozen.ActiveEventSnapshotFingerprint;

            clock.UnixSeconds = 250L;
            ActiveEventModifierSnapshotV1 expiredSnapshot =
                service.ProjectActiveEvents().Snapshot;

            Assert.That(
                expiredSnapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIdsV1.MoneyQuantity,
                    10m).FinalValue,
                Is.EqualTo(10m));
            Assert.That(
                frozen.Evaluate(
                    EventModifierTargetIdsV1.MoneyQuantity,
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
            SpecialEventDefinitionV1 first = Event(
                "event.alpha",
                100L,
                200L,
                EventModifierTargetIdsV1.MoneyQuantity,
                2m,
                overlapMode: SpecialEventOverlapModeV1.Exclusive);
            SpecialEventDefinitionV1 second = Event(
                "event.beta",
                100L,
                200L,
                EventModifierTargetIdsV1.ExperienceQuantity,
                2m);
            var clock = new MutableClockV1(150L);

            ActiveEventProjectionResultV1 left =
                new ActiveEventModifierProjectionServiceV1(
                    Catalog(first, second),
                    clock).ProjectActiveEvents();
            ActiveEventProjectionResultV1 right =
                new ActiveEventModifierProjectionServiceV1(
                    Catalog(second, first),
                    clock).ProjectActiveEvents();

            Assert.That(left.Status, Is.EqualTo(
                ActiveEventProjectionStatusV1.ConflictingActiveEvents));
            Assert.That(left.Snapshot, Is.Null);
            Assert.That(left.Conflicts.Count, Is.EqualTo(1));
            Assert.That(left.Conflicts[0].ReasonCode, Is.EqualTo(
                "exclusive-overlap"));
            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
        }

        [Test]
        public void ExplicitExclusion_RejectsEvenWhenBothEventsAllowCombining()
        {
            SpecialEventDefinitionV1 first = Event(
                "event.alpha",
                100L,
                200L,
                EventModifierTargetIdsV1.MoneyQuantity,
                2m,
                excludedEventIds: new[] { "event.beta" });
            SpecialEventDefinitionV1 second = Event(
                "event.beta",
                100L,
                200L,
                EventModifierTargetIdsV1.ExperienceQuantity,
                2m);

            ActiveEventProjectionResultV1 result =
                new ActiveEventModifierProjectionServiceV1(
                    Catalog(first, second),
                    new MutableClockV1(150L)).ProjectActiveEvents();

            Assert.That(result.Status, Is.EqualTo(
                ActiveEventProjectionStatusV1.ConflictingActiveEvents));
            Assert.That(result.Conflicts.Single().ReasonCode, Is.EqualTo(
                "explicit-exclusion"));
        }

        [Test]
        public void CombiningEvents_ApplyThroughMergedModifierLanguage()
        {
            var service = new ActiveEventModifierProjectionServiceV1(
                Catalog(
                    Event(
                        "event.money-a",
                        100L,
                        200L,
                        EventModifierTargetIdsV1.MoneyQuantity,
                        1.5m),
                    Event(
                        "event.money-b",
                        100L,
                        200L,
                        EventModifierTargetIdsV1.MoneyQuantity,
                        2m)),
                new MutableClockV1(150L));

            decimal result = service.ProjectActiveEvents()
                .Snapshot
                .ModifierSnapshot
                .Evaluate(EventModifierTargetIdsV1.MoneyQuantity, 10m)
                .FinalValue;

            Assert.That(result, Is.EqualTo(30m));
        }

        [Test]
        public void UnknownTarget_RemainsRepresentableUntilAConsumerRequestsIt()
        {
            const string futureTarget = "future.rewards.mystery-scale";
            ActiveEventModifierSnapshotV1 snapshot =
                new ActiveEventModifierProjectionServiceV1(
                    Catalog(Event(
                        "event.future-target",
                        100L,
                        200L,
                        futureTarget,
                        1.5m)),
                    new MutableClockV1(150L))
                .ProjectActiveEvents()
                .Snapshot;

            Assert.That(
                snapshot.ModifierSnapshot.Modifiers.Single().TargetId,
                Is.EqualTo(futureTarget));
            Assert.That(
                snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIdsV1.MoneyQuantity,
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
            SpecialEventDefinitionV1 first = Event(
                "event.alpha",
                100L,
                200L,
                EventModifierTargetIdsV1.MoneyQuantity,
                2m);
            SpecialEventDefinitionV1 second = Event(
                "event.beta",
                100L,
                200L,
                EventModifierTargetIdsV1.ExperienceQuantity,
                2m);

            Assert.That(
                Catalog(first, second).Fingerprint,
                Is.EqualTo(Catalog(second, first).Fingerprint));
        }

        private static SpecialEventCatalogV1 Catalog(
            params SpecialEventDefinitionV1[] definitions)
        {
            return new SpecialEventCatalogV1(
                "events.fixture.v1",
                definitions);
        }

        private static SpecialEventDefinitionV1 Event(
            string eventId,
            long start,
            long end,
            string targetId,
            decimal multiplier,
            int priority = 0,
            SpecialEventOverlapModeV1 overlapMode =
                SpecialEventOverlapModeV1.Combine,
            IEnumerable<string> excludedEventIds = null)
        {
            return new SpecialEventDefinitionV1(
                SpecialEventDefinitionV1.CurrentSchemaVersion,
                "content.fixture.v1",
                eventId,
                new EventActivationWindowV1(start, end),
                priority,
                overlapMode,
                new[]
                {
                    new EventModifierDescriptorV1(
                        targetId,
                        RuntimeModifierOperationV1.Multiplicative,
                        multiplier),
                },
                excludedEventIds);
        }

        private sealed class MutableClockV1 : IAuthoritativeEventClockV1
        {
            public MutableClockV1(long unixSeconds)
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

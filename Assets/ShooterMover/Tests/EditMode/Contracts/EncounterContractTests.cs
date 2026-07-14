using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Contracts.Identity;
using ShooterMover.Contracts.Mission;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class EncounterContractTests
    {
        private const string DefinitionFingerprint =
            "sha256:8c1e3a5f7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a";

        [Test]
        public void Start_IsDeterministicIdempotentAndRejectsConflictingRepeat()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterStartMessage start = Start(
                identity,
                "encounter-message.start-0001",
                Entry("entry.initial-0", "actor.pursuer-01", "enemy.pursuer-drone", 0));
            EncounterLifecycle ready = EncounterLifecycle.Create(identity);

            EncounterLifecycleTransition applied = ready.Start(start);
            Assert.That(applied.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(applied.Next.Phase, Is.EqualTo(EncounterLifecyclePhase.Active));
            Assert.That(applied.Next.ActiveParticipantCount, Is.EqualTo(1));

            EncounterLifecycleTransition repeated = applied.Next.Start(start);
            Assert.That(repeated.Kind, Is.EqualTo(EncounterTransitionKind.NoChange));
            Assert.That(repeated.Next, Is.SameAs(applied.Next));

            EncounterStartMessage conflicting = Start(
                identity,
                "encounter-message.start-0002",
                Entry("entry.initial-0", "actor.pursuer-01", "enemy.pursuer-drone", 0));
            EncounterLifecycleTransition conflict = applied.Next.Start(conflicting);
            Assert.That(
                conflict.Rejection,
                Is.EqualTo(EncounterTransitionRejection.AlreadyStarted));
            Assert.That(conflict.Next, Is.SameAs(applied.Next));
        }

        [Test]
        public void Reinforcements_AreEntryOrderedWaveOrderedAndRetrySafe()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterLifecycle active = EncounterLifecycle.Create(identity)
                .Start(
                    Start(
                        identity,
                        "encounter-message.start-0001",
                        Entry(
                            "entry.initial-0",
                            "actor.pursuer-01",
                            "enemy.pursuer-drone",
                            0)))
                .Next;

            EncounterReinforcementMessage waveZero = new EncounterReinforcementMessage(
                identity,
                Id("encounter-message.reinforcement-0000"),
                0L,
                new[]
                {
                    Entry(
                        "entry.reinforcement-0-1",
                        "actor.blaster-01",
                        "enemy.mobile-blaster-droid",
                        1),
                    Entry(
                        "entry.reinforcement-0-0",
                        "actor.ram-01",
                        "enemy.ram-droid",
                        0),
                });
            EncounterReinforcementMessage waveOne = new EncounterReinforcementMessage(
                identity,
                Id("encounter-message.reinforcement-0001"),
                1L,
                new[]
                {
                    Entry(
                        "entry.reinforcement-1-0",
                        "actor.turret-01",
                        "enemy.blaster-turret",
                        0),
                });

            Assert.That(waveZero.Entries[0].ActorId, Is.EqualTo(Id("actor.ram-01")));
            Assert.That(waveZero.Entries[1].ActorId, Is.EqualTo(Id("actor.blaster-01")));

            EncounterLifecycleTransition outOfOrder = active.AddReinforcement(waveOne);
            Assert.That(
                outOfOrder.Rejection,
                Is.EqualTo(EncounterTransitionRejection.ReinforcementOutOfOrder));

            EncounterLifecycleTransition first = active.AddReinforcement(waveZero);
            Assert.That(first.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(first.Next.ActiveParticipantCount, Is.EqualTo(3));

            EncounterLifecycleTransition repeated = first.Next.AddReinforcement(waveZero);
            Assert.That(repeated.Kind, Is.EqualTo(EncounterTransitionKind.NoChange));
            Assert.That(repeated.Next, Is.SameAs(first.Next));

            EncounterReinforcementMessage conflictingZero =
                new EncounterReinforcementMessage(
                    identity,
                    Id("encounter-message.reinforcement-conflict"),
                    0L,
                    new[]
                    {
                        Entry(
                            "entry.reinforcement-conflict-0",
                            "actor.elite-01",
                            "enemy.four-blaster-elite",
                            0),
                    });
            Assert.That(
                first.Next.AddReinforcement(conflictingZero).Rejection,
                Is.EqualTo(EncounterTransitionRejection.ReinforcementConflict));

            EncounterLifecycleTransition second = first.Next.AddReinforcement(waveOne);
            Assert.That(second.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(second.Next.Reinforcements.Count, Is.EqualTo(2));
            Assert.That(second.Next.ActiveParticipantCount, Is.EqualTo(4));
        }

        [Test]
        public void RetreatAndWithdrawal_RespectLockdownAndRemainIdempotent()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterLifecycle active = EncounterLifecycle.Create(identity)
                .Start(
                    Start(
                        identity,
                        "encounter-message.start-0001",
                        Entry(
                            "entry.initial-0",
                            "actor.pursuer-01",
                            "enemy.pursuer-drone",
                            0)))
                .Next;
            EncounterLockdownMessage engaged = new EncounterLockdownMessage(
                identity,
                Id("encounter-message.lockdown-engaged"),
                EncounterLockdownState.Engaged,
                EncounterLockdownReason.EncounterRule);
            EncounterLifecycle locked = active.ApplyLockdown(engaged).Next;
            EncounterRetreatMessage retreat = new EncounterRetreatMessage(
                identity,
                Id("encounter-message.retreat-0001"),
                Id("encounter-controller.benchmark"),
                EncounterRetreatReason.TacticalWithdrawal);
            EncounterWithdrawalMessage withdrawal = new EncounterWithdrawalMessage(
                identity,
                Id("encounter-message.withdrawal-0001"),
                Id("actor.pursuer-01"),
                EncounterWithdrawalReason.Retreat);

            Assert.That(
                locked.BeginRetreat(retreat).Rejection,
                Is.EqualTo(EncounterTransitionRejection.LockdownActive));
            Assert.That(
                locked.RecordWithdrawal(withdrawal).Rejection,
                Is.EqualTo(EncounterTransitionRejection.LockdownActive));

            EncounterLockdownMessage released = new EncounterLockdownMessage(
                identity,
                Id("encounter-message.lockdown-released"),
                EncounterLockdownState.Released,
                EncounterLockdownReason.RouteControl);
            EncounterLifecycle unlocked = locked.ApplyLockdown(released).Next;
            EncounterLifecycleTransition retreatApplied = unlocked.BeginRetreat(retreat);
            Assert.That(retreatApplied.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(
                retreatApplied.Next.Phase,
                Is.EqualTo(EncounterLifecyclePhase.Retreating));

            EncounterLifecycleTransition withdrew =
                retreatApplied.Next.RecordWithdrawal(withdrawal);
            Assert.That(withdrew.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(withdrew.Next.ActiveParticipantCount, Is.EqualTo(0));

            EncounterLifecycleTransition repeated =
                withdrew.Next.RecordWithdrawal(withdrawal);
            Assert.That(repeated.Kind, Is.EqualTo(EncounterTransitionKind.NoChange));
            Assert.That(repeated.Next, Is.SameAs(withdrew.Next));
        }

        [Test]
        public void Withdrawal_CanResolveAnActorAddedByReinforcement()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterLifecycle active = EncounterLifecycle.Create(identity)
                .Start(
                    Start(
                        identity,
                        "encounter-message.start-0001",
                        Entry(
                            "entry.initial-0",
                            "actor.pursuer-01",
                            "enemy.pursuer-drone",
                            0)))
                .Next;
            EncounterReinforcementMessage reinforcement =
                new EncounterReinforcementMessage(
                    identity,
                    Id("encounter-message.reinforcement-0000"),
                    0L,
                    new[]
                    {
                        Entry(
                            "entry.reinforcement-0-0",
                            "actor.elite-01",
                            "enemy.four-blaster-elite",
                            0),
                    });
            EncounterLifecycle reinforced = active.AddReinforcement(reinforcement).Next;
            EncounterWithdrawalMessage leavesDuringWave = new EncounterWithdrawalMessage(
                identity,
                Id("encounter-message.withdrawal-elite"),
                Id("actor.elite-01"),
                EncounterWithdrawalReason.RouteExit);

            EncounterLifecycleTransition result =
                reinforced.RecordWithdrawal(leavesDuringWave);

            Assert.That(result.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(result.Next.Resolutions.Single().ActorId, Is.EqualTo(Id("actor.elite-01")));
            Assert.That(result.Next.ActiveParticipantCount, Is.EqualTo(1));
        }

        [Test]
        public void CombatResolution_ConsumesCombatV1AndCompletionIsDurableOnce()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterLifecycle active = EncounterLifecycle.Create(identity)
                .Start(
                    Start(
                        identity,
                        "encounter-message.start-0001",
                        Entry(
                            "entry.initial-0",
                            "actor.pursuer-01",
                            "enemy.pursuer-drone",
                            0)))
                .Next;
            VitalMessage destroyed = new VitalMessage(
                Id("combat-event.pursuer-destroyed"),
                Id("actor.player"),
                Id("actor.pursuer-01"),
                CombatChannel.Kinetic,
                VitalResult.Destroyed,
                new VitalState(0d, 100d, 0d, 0d));
            EncounterCombatResolutionMessage combat =
                new EncounterCombatResolutionMessage(identity, destroyed);

            EncounterLifecycleTransition resolved =
                active.RecordCombatResolution(combat);
            Assert.That(resolved.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(resolved.Next.ActiveParticipantCount, Is.EqualTo(0));
            Assert.That(
                resolved.Next.Resolutions.Single().Kind,
                Is.EqualTo(EncounterActorResolutionKind.Destroyed));

            EncounterCompletionMessage completion = Completion(
                identity,
                "mission-event.room-cleared-0001",
                "command.room-clear-0001",
                5L);
            EncounterLifecycleTransition completed = resolved.Next.Complete(completion);
            Assert.That(completed.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(completed.Next.IsCompleted, Is.True);
            Assert.That(
                completed.Next.CompletionMessage.DurableEvent.EventType,
                Is.EqualTo(MissionEventType.RoomCleared));

            EncounterLifecycleTransition repeated = completed.Next.Complete(completion);
            Assert.That(repeated.Kind, Is.EqualTo(EncounterTransitionKind.NoChange));
            Assert.That(repeated.Next, Is.SameAs(completed.Next));

            EncounterCompletionMessage conflicting = Completion(
                identity,
                "mission-event.room-cleared-0002",
                "command.room-clear-0002",
                6L);
            Assert.That(
                completed.Next.Complete(conflicting).Rejection,
                Is.EqualTo(EncounterTransitionRejection.AlreadyCompleted));
        }

        [Test]
        public void Completion_RequiresEveryKnownParticipantToResolve()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterLifecycle active = EncounterLifecycle.Create(identity)
                .Start(
                    Start(
                        identity,
                        "encounter-message.start-0001",
                        Entry(
                            "entry.initial-0",
                            "actor.pursuer-01",
                            "enemy.pursuer-drone",
                            0),
                        Entry(
                            "entry.initial-1",
                            "actor.ram-01",
                            "enemy.ram-droid",
                            1)))
                .Next;
            EncounterWithdrawalMessage firstLeaves = new EncounterWithdrawalMessage(
                identity,
                Id("encounter-message.withdrawal-0001"),
                Id("actor.pursuer-01"),
                EncounterWithdrawalReason.RouteExit);
            EncounterLifecycle oneRemaining = active.RecordWithdrawal(firstLeaves).Next;

            EncounterLifecycleTransition completion = oneRemaining.Complete(
                Completion(
                    identity,
                    "mission-event.room-cleared-0001",
                    "command.room-clear-0001",
                    5L));

            Assert.That(
                completion.Rejection,
                Is.EqualTo(EncounterTransitionRejection.ParticipantsRemain));
            Assert.That(completion.Next, Is.SameAs(oneRemaining));
        }

        [Test]
        public void CompletionMessage_RejectsMismatchedMissionRunRoomOrEncounter()
        {
            EncounterRuntimeIdentity identity = Identity();
            MissionPayloadVersion version = CreateVersion();

            Assert.Throws<ArgumentException>(
                () => new EncounterCompletionMessage(
                    identity,
                    new MissionEventEnvelope(
                        Id("mission-event.room-cleared-0001"),
                        Id("command.room-clear-0001"),
                        Id("run.other-run"),
                        version,
                        new MissionSequence(5L),
                        new RoomClearedEvent(
                            identity.Room.RoomId,
                            identity.EncounterId))));
            Assert.Throws<ArgumentException>(
                () => new EncounterCompletionMessage(
                    identity,
                    new MissionEventEnvelope(
                        Id("mission-event.room-cleared-0001"),
                        Id("command.room-clear-0001"),
                        identity.RunId,
                        version,
                        new MissionSequence(5L),
                        new RoomClearedEvent(
                            Id("room.other-room"),
                            identity.EncounterId))));
            Assert.Throws<ArgumentException>(
                () => new EncounterCompletionMessage(
                    identity,
                    new MissionEventEnvelope(
                        Id("mission-event.room-cleared-0001"),
                        Id("command.room-clear-0001"),
                        identity.RunId,
                        version,
                        new MissionSequence(5L),
                        new RoomClearedEvent(
                            identity.Room.RoomId,
                            Id("encounter.other-encounter")))));
        }

        [Test]
        public void BudgetMessages_ValidateAndReportViolationsInCanonicalOrder()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterPerformanceBudget budget =
                new EncounterPerformanceBudget(4, 2, 12, 16.667d);
            EncounterBudgetSample sample = new EncounterBudgetSample(
                identity,
                Id("budget-sample.frame-0042"),
                5,
                3,
                13,
                20d);

            EncounterBudgetEvaluation evaluation =
                EncounterBudgetEvaluation.Evaluate(budget, sample);

            Assert.That(evaluation.IsWithinBudget, Is.False);
            Assert.That(
                evaluation.Violations,
                Is.EqualTo(
                    new[]
                    {
                        EncounterBudgetViolation.ConcurrentParticipantsExceeded,
                        EncounterBudgetViolation.PendingReinforcementEntriesExceeded,
                        EncounterBudgetViolation.CombatMessagesPerTickExceeded,
                        EncounterBudgetViolation.FrameTimeExceeded,
                    }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterPerformanceBudget(0, 0, 1, 16d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterPerformanceBudget(1, -1, 1, 16d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterPerformanceBudget(1, 0, 0, 16d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterPerformanceBudget(1, 0, 1, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EncounterBudgetSample(
                    identity,
                    Id("budget-sample.bad"),
                    0,
                    0,
                    0,
                    -1d));
        }

        [Test]
        public void Lifecycle_RejectsReinforcementThatWouldExceedBudget()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterPerformanceBudget tightBudget =
                new EncounterPerformanceBudget(2, 1, 12, 16.667d);
            EncounterStartMessage start = new EncounterStartMessage(
                identity,
                Id("encounter-message.start-0001"),
                tightBudget,
                new[]
                {
                    Entry(
                        "entry.initial-0",
                        "actor.pursuer-01",
                        "enemy.pursuer-drone",
                        0),
                    Entry(
                        "entry.initial-1",
                        "actor.ram-01",
                        "enemy.ram-droid",
                        1),
                });
            EncounterLifecycle active = EncounterLifecycle.Create(identity).Start(start).Next;
            EncounterReinforcementMessage reinforcement =
                new EncounterReinforcementMessage(
                    identity,
                    Id("encounter-message.reinforcement-0000"),
                    0L,
                    new[]
                    {
                        Entry(
                            "entry.reinforcement-0-0",
                            "actor.elite-01",
                            "enemy.four-blaster-elite",
                            0),
                    });

            Assert.That(
                active.AddReinforcement(reinforcement).Rejection,
                Is.EqualTo(EncounterTransitionRejection.BudgetExceeded));
        }

        [Test]
        public void GenericEntries_MapAllStageOneEnemyRolesWithoutSpecialEnvelopes()
        {
            EncounterRuntimeIdentity identity = Identity();
            EncounterStartMessage start = Start(
                identity,
                "encounter-message.start-role-map",
                Entry(
                    "entry.role-0",
                    "actor.pursuer-01",
                    "enemy.pursuer-drone",
                    0),
                Entry(
                    "entry.role-1",
                    "actor.ram-01",
                    "enemy.ram-droid",
                    1),
                Entry(
                    "entry.role-2",
                    "actor.blaster-01",
                    "enemy.mobile-blaster-droid",
                    2),
                Entry(
                    "entry.role-3",
                    "actor.turret-01",
                    "enemy.blaster-turret",
                    3),
                Entry(
                    "entry.role-4",
                    "actor.elite-01",
                    "enemy.four-blaster-elite",
                    4));

            Assert.That(
                start.Entries.Select(entry => entry.GetType()).Distinct().ToArray(),
                Is.EqualTo(new[] { typeof(EncounterParticipantEntry) }));
            Assert.That(
                start.Entries.Select(entry => entry.RoleId.ToString()).ToArray(),
                Is.EqualTo(
                    new[]
                    {
                        "enemy.pursuer-drone",
                        "enemy.ram-droid",
                        "enemy.mobile-blaster-droid",
                        "enemy.blaster-turret",
                        "enemy.four-blaster-elite",
                    }));
        }

        [Test]
        public void EncounterContracts_AreImmutableAndUnityFree()
        {
            Type[] immutableTypes =
            {
                typeof(EncounterRuntimeIdentity),
                typeof(EncounterParticipantEntry),
                typeof(EncounterPerformanceBudget),
                typeof(EncounterBudgetSample),
                typeof(EncounterBudgetEvaluation),
                typeof(EncounterStartMessage),
                typeof(EncounterReinforcementMessage),
                typeof(EncounterRetreatMessage),
                typeof(EncounterLockdownMessage),
                typeof(EncounterWithdrawalMessage),
                typeof(EncounterCombatResolutionMessage),
                typeof(EncounterCompletionMessage),
                typeof(EncounterActorResolution),
                typeof(EncounterLifecycle),
                typeof(EncounterLifecycleTransition),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(type.IsSealed, Is.True, type.FullName + " must be sealed.");
                foreach (PropertyInfo property in type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public))
                {
                    Assert.That(
                        property.CanWrite,
                        Is.False,
                        type.FullName + "." + property.Name + " must not be settable.");
                }
            }

            Assert.That(
                typeof(EncounterRuntimeIdentity).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static EncounterRuntimeIdentity Identity()
        {
            return new EncounterRuntimeIdentity(
                Id("encounter.stage1-benchmark"),
                Id("encounter-runtime.stage1-benchmark-a"),
                Id("run.stage1-run-0001"),
                new RoomProjectionIdentity(
                    Id("room.stage1-benchmark"),
                    Id("projection.stage1-benchmark-a")));
        }

        private static EncounterStartMessage Start(
            EncounterRuntimeIdentity identity,
            string messageId,
            params EncounterParticipantEntry[] entries)
        {
            return new EncounterStartMessage(
                identity,
                Id(messageId),
                new EncounterPerformanceBudget(8, 4, 32, 16.667d),
                entries);
        }

        private static EncounterParticipantEntry Entry(
            string entryId,
            string actorId,
            string roleId,
            int order)
        {
            return new EncounterParticipantEntry(
                Id(entryId),
                Id(actorId),
                Id(roleId),
                order);
        }

        private static EncounterCompletionMessage Completion(
            EncounterRuntimeIdentity identity,
            string eventId,
            string commandId,
            long sequence)
        {
            return new EncounterCompletionMessage(
                identity,
                new MissionEventEnvelope(
                    Id(eventId),
                    Id(commandId),
                    identity.RunId,
                    CreateVersion(),
                    new MissionSequence(sequence),
                    new RoomClearedEvent(
                        identity.Room.RoomId,
                        identity.EncounterId)));
        }

        private static MissionPayloadVersion CreateVersion()
        {
            return new MissionPayloadVersion(
                1,
                ContentVersion.Create(1, DefinitionFingerprint));
        }

        private static StableId Id(string text)
        {
            return StableId.Parse(text);
        }
    }
}

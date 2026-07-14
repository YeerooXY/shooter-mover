using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyActorStepperTests
    {
        [Test]
        public void DamageOrdering_IsDeterministicAcrossCallerEnumeration()
        {
            EnemyActorState initial = BuildState(
                EnemyContactMode.OrdinaryDamage,
                maximumHealth: 100d);
            EnemyActorCommand first = EnemyActorCommand.Damage(
                0L,
                Id("event.damage-first"),
                Id("weapon.first"),
                (int)CombatChannel.Kinetic,
                20d);
            EnemyActorCommand second = EnemyActorCommand.Damage(
                1L,
                Id("event.damage-second"),
                Id("weapon.second"),
                (int)CombatChannel.Thermal,
                30d);

            EnemyActorStepResult forward =
                EnemyActorStepper.Step(initial, new[] { first, second });
            EnemyActorStepResult reversed =
                EnemyActorStepper.Step(initial, new[] { second, first });

            Assert.That(forward.State, Is.EqualTo(reversed.State));
            Assert.That(forward.State.Health, Is.EqualTo(50d));
            Assert.That(
                forward.Notifications.Cast<EnemyDamageNotification>()
                    .Select(item => item.EventId),
                Is.EqualTo(new[] { first.EventId, second.EventId }));
            Assert.That(
                reversed.Notifications.Cast<EnemyDamageNotification>()
                    .Select(item => item.EventId),
                Is.EqualTo(new[] { first.EventId, second.EventId }));
        }

        [Test]
        public void Overkill_ProducesValidDamageVitalAndEncounterResolutionOnce()
        {
            EnemyActorState initial = BuildState(
                EnemyContactMode.OrdinaryDamage,
                maximumHealth: 100d);
            EnemyActorCommand lethal = EnemyActorCommand.Damage(
                0L,
                Id("event.overkill"),
                Id("weapon.rocket"),
                (int)CombatChannel.Explosive,
                150d);

            EnemyActorStepResult result =
                EnemyActorStepper.Step(initial, new[] { lethal });

            EnemyDamageNotification damage =
                result.Notifications.OfType<EnemyDamageNotification>().Single();
            Assert.That(damage.BeforeHealth, Is.EqualTo(100d));
            Assert.That(damage.AfterHealth, Is.Zero);
            Assert.That(damage.HealthDamageApplied, Is.EqualTo(100d));
            Assert.That(damage.UnappliedAmount, Is.EqualTo(50d));
            Assert.That(
                damage.ResultValue,
                Is.EqualTo((int)DamageResult.Applied));
            Assert.DoesNotThrow(() => ToDamageMessage(damage));

            EnemyDestroyedNotification vitalFact =
                result.Notifications.OfType<EnemyDestroyedNotification>().Single();
            VitalMessage vital = ToVitalMessage(vitalFact);
            Assert.That(vital.Result, Is.EqualTo(VitalResult.Destroyed));

            EnemyEncounterResolutionNotification encounterFact =
                result.Notifications
                    .OfType<EnemyEncounterResolutionNotification>()
                    .Single();
            Assert.That(encounterFact.Vital, Is.SameAs(vitalFact));

            EncounterRuntimeIdentity encounter = BuildEncounterIdentity();
            EncounterCombatResolutionMessage resolution =
                new EncounterCombatResolutionMessage(encounter, vital);
            Assert.That(resolution.ActorId, Is.EqualTo(initial.ActorId));

            EncounterLifecycle lifecycle = EncounterLifecycle.Create(encounter)
                .Start(
                    new EncounterStartMessage(
                        encounter,
                        Id("encounter-message.start"),
                        new EncounterPerformanceBudget(1, 0, 16, 16.667d),
                        new[]
                        {
                            new EncounterParticipantEntry(
                                Id("entry.enemy"),
                                initial.ActorId,
                                initial.RoleId,
                                0),
                        }))
                .Next;
            EncounterLifecycleTransition applied =
                lifecycle.RecordCombatResolution(resolution);
            Assert.That(applied.Kind, Is.EqualTo(EncounterTransitionKind.Applied));
            Assert.That(applied.Next.ActiveParticipantCount, Is.Zero);

            Assert.That(
                result.State.LifecyclePhase,
                Is.EqualTo(EnemyActorLifecyclePhase.Destroyed));
            Assert.That(
                result.State.DeathCause,
                Is.EqualTo(EnemyActorDeathCause.IncomingDamage));
            Assert.That(result.State.DestroyedVitalEmitted, Is.True);
            Assert.That(result.State.EncounterResolutionEmitted, Is.True);
        }

        [Test]
        public void ExactZeroHealthAndRepeatedDamage_EmitTerminalFactsOnlyOnce()
        {
            EnemyActorState initial = BuildState(
                EnemyContactMode.OrdinaryDamage,
                maximumHealth: 40d);
            EnemyActorCommand lethal = EnemyActorCommand.Damage(
                0L,
                Id("event.exact-zero"),
                Id("weapon.blaster"),
                (int)CombatChannel.Kinetic,
                40d);

            EnemyActorStepResult first =
                EnemyActorStepper.Step(initial, new[] { lethal });
            EnemyActorStepResult duplicate =
                EnemyActorStepper.Step(first.State, new[] { lethal });
            EnemyActorStepResult late =
                EnemyActorStepper.Step(
                    duplicate.State,
                    new[]
                    {
                        EnemyActorCommand.Damage(
                            1L,
                            Id("event.late-hit"),
                            Id("weapon.blaster"),
                            (int)CombatChannel.Kinetic,
                            10d),
                    });

            Assert.That(first.State.Health, Is.Zero);
            Assert.That(
                first.Notifications.OfType<EnemyDestroyedNotification>().Count(),
                Is.EqualTo(1));
            Assert.That(
                first.Notifications
                    .OfType<EnemyEncounterResolutionNotification>()
                    .Count(),
                Is.EqualTo(1));
            Assert.That(
                duplicate.Notifications.OfType<EnemyDestroyedNotification>(),
                Is.Empty);
            Assert.That(
                duplicate.Notifications
                    .OfType<EnemyEncounterResolutionNotification>(),
                Is.Empty);
            Assert.That(
                duplicate.Notifications
                    .OfType<EnemyDamageNotification>()
                    .Single()
                    .ResultValue,
                Is.EqualTo((int)DamageResult.DuplicateEventIgnored));
            Assert.That(
                late.Notifications
                    .OfType<EnemyDamageNotification>()
                    .Single()
                    .ResultValue,
                Is.EqualTo((int)DamageResult.TargetAlreadyDestroyed));
            Assert.That(
                late.Notifications.OfType<EnemyDestroyedNotification>(),
                Is.Empty);
        }

        [Test]
        public void OrdinaryContact_UsesPerMoverGraceAndReportsAcceptedWeightResult()
        {
            EnemyActorState state = BuildState(
                EnemyContactMode.OrdinaryDamage,
                maximumHealth: 75d,
                actorWeight: CombatWeightClass.Light,
                moverDamage: 12d,
                graceSeconds: 0.5d,
                simultaneousSeconds: 0.02d);
            StableId mover = Id("actor.player");

            EnemyActorStepResult first = EnemyActorStepper.Step(
                state,
                new[]
                {
                    Contact(
                        0L,
                        "event.contact-first",
                        mover,
                        1d,
                        CombatWeightClass.Standard),
                });
            EnemyActorStepResult simultaneous = EnemyActorStepper.Step(
                first.State,
                new[]
                {
                    Contact(
                        1L,
                        "event.contact-simultaneous",
                        mover,
                        1.02d,
                        CombatWeightClass.Standard),
                });
            EnemyActorStepResult grace = EnemyActorStepper.Step(
                simultaneous.State,
                new[]
                {
                    Contact(
                        2L,
                        "event.contact-grace",
                        mover,
                        1.2d,
                        CombatWeightClass.Standard),
                });
            EnemyActorStepResult boundary = EnemyActorStepper.Step(
                grace.State,
                new[]
                {
                    Contact(
                        3L,
                        "event.contact-boundary",
                        mover,
                        1.5d,
                        CombatWeightClass.Standard),
                });

            EnemyContactNotification accepted =
                first.Notifications.OfType<EnemyContactNotification>().Single();
            Assert.That(accepted.ResultValue, Is.EqualTo((int)ContactResult.Accepted));
            Assert.That(accepted.RequestsMoverDamage, Is.True);
            Assert.That(accepted.MoverDamageAmount, Is.EqualTo(12d));
            Assert.That(
                accepted.WeightResultValue,
                Is.EqualTo((int)WeightResult.SourceHeavier));
            Assert.DoesNotThrow(() => ToContactMessage(accepted));

            Assert.That(
                simultaneous.Notifications
                    .OfType<EnemyContactNotification>()
                    .Single()
                    .ResultValue,
                Is.EqualTo((int)ContactResult.GracePeriodIgnored));
            Assert.That(
                grace.Notifications
                    .OfType<EnemyContactNotification>()
                    .Single()
                    .ResultValue,
                Is.EqualTo((int)ContactResult.GracePeriodIgnored));
            Assert.That(
                boundary.Notifications
                    .OfType<EnemyContactNotification>()
                    .Single()
                    .ResultValue,
                Is.EqualTo((int)ContactResult.Accepted));
            Assert.That(boundary.State.Health, Is.EqualTo(75d));
            Assert.That(boundary.State.IsActive, Is.True);
        }

        [Test]
        public void ContactCapacity_IsHardBoundedWithoutEviction()
        {
            EnemyActorState state = BuildState(
                EnemyContactMode.OrdinaryDamage,
                maximumHealth: 50d,
                contactCapacity: 1);
            StableId firstMover = Id("actor.alpha");
            StableId secondMover = Id("actor.beta");

            EnemyActorStepResult result = EnemyActorStepper.Step(
                state,
                new[]
                {
                    Contact(
                        0L,
                        "event.contact-alpha",
                        firstMover,
                        0d,
                        CombatWeightClass.Standard),
                    Contact(
                        1L,
                        "event.contact-beta",
                        secondMover,
                        0d,
                        CombatWeightClass.Standard),
                });

            EnemyContactNotification[] contacts =
                result.Notifications.OfType<EnemyContactNotification>().ToArray();
            Assert.That(contacts[0].Decision, Is.EqualTo(EnemyContactDecision.Accepted));
            Assert.That(
                contacts[1].Decision,
                Is.EqualTo(EnemyContactDecision.CapacityRejected));
            Assert.That(
                contacts[1].ResultValue,
                Is.EqualTo((int)ContactResult.GracePeriodIgnored));
            Assert.That(contacts[1].RequestsMoverDamage, Is.False);
            Assert.That(result.State.ContactPolicy.TrackedMoverIds.Count, Is.EqualTo(1));
            Assert.That(result.State.ContactPolicy.TrackedMoverIds[0], Is.EqualTo(firstMover));
        }

        [Test]
        public void DisposableImpact_IsDistinctFromOrdinaryContactDamage()
        {
            EnemyActorState disposable = BuildState(
                EnemyContactMode.DisposableImpact,
                maximumHealth: 25d,
                moverDamage: 20d);
            EnemyActorStepResult result = EnemyActorStepper.Step(
                disposable,
                new[]
                {
                    Contact(
                        0L,
                        "event.ram-impact",
                        Id("actor.player"),
                        0d,
                        CombatWeightClass.Standard),
                });

            EnemyContactNotification contact =
                result.Notifications.OfType<EnemyContactNotification>().Single();
            Assert.That(contact.RequestsMoverDamage, Is.True);
            Assert.That(contact.MoverDamageAmount, Is.EqualTo(20d));
            Assert.That(contact.Mode, Is.EqualTo(EnemyContactMode.DisposableImpact));
            Assert.That(result.State.Health, Is.Zero);
            Assert.That(
                result.State.DeathCause,
                Is.EqualTo(EnemyActorDeathCause.DisposableImpact));
            Assert.That(
                result.Notifications.OfType<EnemyDestroyedNotification>().Count(),
                Is.EqualTo(1));
            Assert.That(
                result.Notifications
                    .OfType<EnemyEncounterResolutionNotification>()
                    .Count(),
                Is.EqualTo(1));

            EnemyActorState ordinary = BuildState(
                EnemyContactMode.OrdinaryDamage,
                maximumHealth: 25d,
                moverDamage: 20d);
            EnemyActorStepResult ordinaryResult = EnemyActorStepper.Step(
                ordinary,
                new[]
                {
                    Contact(
                        0L,
                        "event.ordinary-impact",
                        Id("actor.player"),
                        0d,
                        CombatWeightClass.Standard),
                });

            Assert.That(ordinaryResult.State.Health, Is.EqualTo(25d));
            Assert.That(ordinaryResult.State.IsActive, Is.True);
            Assert.That(
                ordinaryResult.Notifications.OfType<EnemyDestroyedNotification>(),
                Is.Empty);
        }

        [Test]
        public void DisposalAfterDeath_IsIdempotentAndDoesNotRepeatEncounterFact()
        {
            EnemyActorState initial = BuildState(
                EnemyContactMode.OrdinaryDamage,
                maximumHealth: 10d);
            EnemyActorStepResult dead = EnemyActorStepper.Step(
                initial,
                new[]
                {
                    EnemyActorCommand.Damage(
                        0L,
                        Id("event.kill"),
                        Id("weapon.blaster"),
                        (int)CombatChannel.Kinetic,
                        10d),
                });
            EnemyActorCommand despawn = EnemyActorCommand.Despawn(
                1L,
                Id("event.despawn"),
                Id("system.enemy-adapter"));

            EnemyActorStepResult first =
                EnemyActorStepper.Step(dead.State, new[] { despawn });
            EnemyActorStepResult repeated =
                EnemyActorStepper.Step(first.State, new[] { despawn });

            Assert.That(first.State.IsDespawned, Is.True);
            Assert.That(
                first.Notifications.OfType<EnemyDespawnedNotification>().Count(),
                Is.EqualTo(1));
            Assert.That(
                first.Notifications.OfType<EnemyDestroyedNotification>(),
                Is.Empty);
            Assert.That(
                first.Notifications
                    .OfType<EnemyEncounterResolutionNotification>(),
                Is.Empty);
            Assert.That(repeated.Notifications, Is.Empty);
            Assert.That(repeated.State, Is.SameAs(first.State));
        }

        [Test]
        public void StateAndPolicies_AreImmutableUnityFreeAndOwnNoMissionOrVelocitySurface()
        {
            Type[] immutableTypes =
            {
                typeof(EnemyActorState),
                typeof(EnemyActorCommand),
                typeof(EnemyContactPolicy),
                typeof(EnemyContactResolution),
                typeof(EnemyDamageNotification),
                typeof(EnemyContactNotification),
                typeof(EnemyDestroyedNotification),
                typeof(EnemyEncounterResolutionNotification),
                typeof(EnemyDespawnedNotification),
                typeof(EnemyActorStepResult),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(type.IsSealed || type.IsAbstract, Is.True, type.FullName);
                Assert.That(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(property => property.CanWrite),
                    Is.Empty,
                    type.FullName);
            }

            string[] references = typeof(EnemyActorState).Assembly
                .GetReferencedAssemblies()
                .Select(item => item.Name)
                .ToArray();
            Assert.That(references, Does.Not.Contain("UnityEngine"));
            Assert.That(references, Does.Not.Contain("ShooterMover.Contracts"));

            string[] propertyNames = typeof(EnemyActorState)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();
            Assert.That(propertyNames.Any(name => name.Contains("Velocity")), Is.False);
            Assert.That(propertyNames.Any(name => name.Contains("Mission")), Is.False);
            Assert.That(
                propertyNames.Any(name => name.Contains("EncounterLifecycle")),
                Is.False);
        }

        [Test]
        public void ContractNumericBoundaries_MatchCombatMessagesV1()
        {
            Assert.That(
                EnemyActorStepper.DamageAppliedResultValue,
                Is.EqualTo((int)DamageResult.Applied));
            Assert.That(
                EnemyActorStepper.DamageDuplicateEventIgnoredResultValue,
                Is.EqualTo((int)DamageResult.DuplicateEventIgnored));
            Assert.That(
                EnemyActorStepper.DamageTargetAlreadyDestroyedResultValue,
                Is.EqualTo((int)DamageResult.TargetAlreadyDestroyed));
            Assert.That(
                EnemyContactPolicy.ContactAcceptedResultValue,
                Is.EqualTo((int)ContactResult.Accepted));
            Assert.That(
                EnemyContactPolicy.ContactGracePeriodIgnoredResultValue,
                Is.EqualTo((int)ContactResult.GracePeriodIgnored));
            Assert.That(
                EnemyContactPolicy.ContactDuplicateEventIgnoredResultValue,
                Is.EqualTo((int)ContactResult.DuplicateEventIgnored));
            Assert.That(
                EnemyContactPolicy.DetermineWeightResult(
                    (int)CombatWeightClass.Standard,
                    (int)CombatWeightClass.Light),
                Is.EqualTo(
                    (int)WeightMessage.DetermineResult(
                        CombatWeightClass.Standard,
                        CombatWeightClass.Light)));
            Assert.That(
                EnemyActorStepper.VitalDestroyedResultValue,
                Is.EqualTo((int)VitalResult.Destroyed));
        }

        private static EnemyActorState BuildState(
            EnemyContactMode mode,
            double maximumHealth,
            CombatWeightClass actorWeight = CombatWeightClass.Standard,
            double moverDamage = 10d,
            double graceSeconds = 0.5d,
            double simultaneousSeconds = 0.02d,
            int contactCapacity = 4)
        {
            return EnemyActorState.Create(
                Id("actor.enemy-test"),
                Id("enemy.pursuer-drone"),
                maximumHealth,
                (int)actorWeight,
                EnemyContactPolicy.Create(
                    mode,
                    moverDamage,
                    graceSeconds,
                    simultaneousSeconds,
                    contactCapacity));
        }

        private static EnemyActorCommand Contact(
            long order,
            string eventId,
            StableId moverId,
            double time,
            CombatWeightClass moverWeight)
        {
            return EnemyActorCommand.Contact(
                order,
                Id(eventId),
                moverId,
                time,
                (int)ContactClassification.BodyImpact,
                (int)moverWeight);
        }

        private static DamageMessage ToDamageMessage(EnemyDamageNotification fact)
        {
            VitalState before = new VitalState(
                fact.BeforeHealth,
                fact.MaximumHealth,
                0d,
                0d);
            VitalState after = new VitalState(
                fact.AfterHealth,
                fact.MaximumHealth,
                0d,
                0d);

            return new DamageMessage(
                fact.EventId,
                fact.SourceId,
                fact.TargetId,
                (CombatChannel)fact.ChannelValue,
                fact.RequestedAmount,
                (DamageResult)fact.ResultValue,
                before,
                after,
                0d,
                fact.ResultValue == (int)DamageResult.Applied
                    ? fact.RequestedAmount
                    : 0d,
                fact.HealthDamageApplied,
                fact.UnappliedAmount);
        }

        private static ContactMessage ToContactMessage(EnemyContactNotification fact)
        {
            return new ContactMessage(
                fact.EventId,
                fact.SourceId,
                fact.TargetId,
                (CombatChannel)fact.ChannelValue,
                (ContactClassification)fact.ContactClassificationValue,
                (ContactResult)fact.ResultValue);
        }

        private static VitalMessage ToVitalMessage(EnemyDestroyedNotification fact)
        {
            return new VitalMessage(
                fact.EventId,
                fact.SourceId,
                fact.TargetId,
                (CombatChannel)fact.ChannelValue,
                (VitalResult)EnemyActorStepper.VitalDestroyedResultValue,
                new VitalState(0d, fact.MaximumHealth, 0d, 0d));
        }

        private static EncounterRuntimeIdentity BuildEncounterIdentity()
        {
            return new EncounterRuntimeIdentity(
                Id("encounter.enemy-test"),
                Id("encounter-runtime.enemy-test-a"),
                Id("run.enemy-test"),
                new RoomProjectionIdentity(
                    Id("room.enemy-test"),
                    Id("projection.enemy-test-a")));
        }

        private static StableId Id(string text)
        {
            return StableId.Parse(text);
        }
    }
}

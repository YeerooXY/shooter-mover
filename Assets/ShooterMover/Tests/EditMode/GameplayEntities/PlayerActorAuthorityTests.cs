using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Tests.EditMode.GameplayEntities
{
    public sealed class PlayerActorAuthorityTests
    {
        [Test]
        public void ValidConstruction_ExportsExactIdentityAtFullHealth()
        {
            PlayerActorAuthority actor = CreateActor(
                Id("actor", "player-a"),
                Id("participant", "local-a"),
                Id("character", "striker"),
                Id("faction", "players"),
                125d,
                3L);

            PlayerActorSnapshot snapshot = actor.ExportSnapshot();

            Assert.That(snapshot.ActorInstanceId, Is.EqualTo(Id("actor", "player-a")));
            Assert.That(snapshot.RunParticipantId, Is.EqualTo(Id("participant", "local-a")));
            Assert.That(snapshot.CharacterId, Is.EqualTo(Id("character", "striker")));
            Assert.That(snapshot.FactionId, Is.EqualTo(Id("faction", "players")));
            Assert.That(snapshot.MaximumHealth, Is.EqualTo(125d));
            Assert.That(snapshot.CurrentHealth, Is.EqualTo(125d));
            Assert.That(snapshot.LifecycleGeneration, Is.EqualTo(3L));
            Assert.That(snapshot.AcceptedSequence, Is.EqualTo(0L));
            Assert.That(snapshot.IsAlive, Is.True);
            Assert.That(snapshot.VitalState.Health, Is.EqualTo(125d));
        }

        [Test]
        public void InvalidIdentityInputs_FailClosedWithDeterministicCodes()
        {
            StableId actorId = Id("actor", "player-a");
            StableId participantId = Id("participant", "local-a");
            StableId characterId = Id("character", "striker");
            StableId factionId = Id("faction", "players");

            AssertRejected(
                new PlayerActorDefinition(null, participantId, characterId, factionId, 100d, 0L),
                PlayerActorCreationRejectionCode.MissingActorInstanceId);
            AssertRejected(
                new PlayerActorDefinition(actorId, null, characterId, factionId, 100d, 0L),
                PlayerActorCreationRejectionCode.MissingRunParticipantId);
            AssertRejected(
                new PlayerActorDefinition(actorId, participantId, null, factionId, 100d, 0L),
                PlayerActorCreationRejectionCode.MissingCharacterId);
            AssertRejected(
                new PlayerActorDefinition(actorId, participantId, characterId, null, 100d, 0L),
                PlayerActorCreationRejectionCode.MissingFactionId);
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void NonPositiveOrNonFiniteMaximumHealth_FailsClosed(double maximumHealth)
        {
            AssertRejected(
                Definition(maximumHealth, 0L),
                PlayerActorCreationRejectionCode.InvalidMaximumHealth);
        }

        [Test]
        public void NegativeInitialGeneration_FailsClosed()
        {
            AssertRejected(
                Definition(100d, -1L),
                PlayerActorCreationRejectionCode.InvalidInitialGeneration);
        }

        [Test]
        public void Damage_ReducesHealthDeterministicallyAndProjectsCombatMessage()
        {
            PlayerActorAuthority actor = CreateActor();

            DamageReceiverResult result = actor.ApplyDamage(Damage("damage-a", 35d, 0L));

            Assert.That(result.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(result.RejectionCode, Is.EqualTo(DamageReceiverRejectionCode.None));
            Assert.That(result.DamageMessage, Is.Not.Null);
            Assert.That(result.DeathFact, Is.Null);
            Assert.That(actor.ExportSnapshot().CurrentHealth, Is.EqualTo(65d));
            Assert.That(actor.ExportSnapshot().AcceptedSequence, Is.EqualTo(1L));
        }

        [Test]
        public void Overkill_ClampsToZeroAndDeathIsProducedExactlyOnce()
        {
            PlayerActorAuthority actor = CreateActor();
            DamageReceiverCommand lethal = Damage("lethal", 150d, 0L);

            DamageReceiverResult first = actor.ApplyDamage(lethal);
            DamageReceiverResult replay = actor.ApplyDamage(lethal);
            DamageReceiverResult late = actor.ApplyDamage(Damage("late", 1d, 0L));

            Assert.That(first.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(first.DeathFact, Is.Not.Null);
            Assert.That(first.DeathFact.AppliedAmount, Is.EqualTo(100d));
            Assert.That(first.DeathFact.RequestedAmount, Is.EqualTo(150d));
            Assert.That(actor.ExportSnapshot().CurrentHealth, Is.EqualTo(0d));
            Assert.That(actor.ExportSnapshot().IsDead, Is.True);

            Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
            Assert.That(replay.DeathFact, Is.Null);
            Assert.That(late.Status, Is.EqualTo(DamageReceiverStatus.RejectedByLifecycle));
            Assert.That(late.RejectionCode, Is.EqualTo(DamageReceiverRejectionCode.ActorDead));
            Assert.That(late.DeathFact, Is.Null);
            Assert.That(actor.ExportSnapshot().AcceptedSequence, Is.EqualTo(1L));
        }

        [Test]
        public void ConflictingDamageReplay_IsRejectedWithoutMutation()
        {
            PlayerActorAuthority actor = CreateActor();
            actor.ApplyDamage(Damage("same-id", 10d, 0L));

            DamageReceiverResult conflict = actor.ApplyDamage(Damage("same-id", 20d, 0L));

            Assert.That(conflict.Status, Is.EqualTo(DamageReceiverStatus.RejectedInvalid));
            Assert.That(
                conflict.RejectionCode,
                Is.EqualTo(DamageReceiverRejectionCode.ConflictingDuplicate));
            Assert.That(actor.ExportSnapshot().CurrentHealth, Is.EqualTo(90d));
            Assert.That(actor.ExportSnapshot().AcceptedSequence, Is.EqualTo(1L));
        }

        [Test]
        public void Healing_ClampsToMaximumAndDeduplicatesAcceptedOperation()
        {
            PlayerActorAuthority actor = CreateActor();
            actor.ApplyDamage(Damage("damage-a", 40d, 0L));
            PlayerActorHealingCommand heal = Healing("heal-a", 75d, 0L);

            PlayerActorHealingResult first = actor.ApplyHealing(heal);
            PlayerActorHealingResult replay = actor.ApplyHealing(heal);

            Assert.That(first.Status, Is.EqualTo(PlayerActorOperationStatus.Applied));
            Assert.That(first.AppliedAmount, Is.EqualTo(40d));
            Assert.That(first.Snapshot.CurrentHealth, Is.EqualTo(100d));
            Assert.That(replay.Status, Is.EqualTo(PlayerActorOperationStatus.Duplicate));
            Assert.That(replay.AppliedAmount, Is.EqualTo(0d));
            Assert.That(actor.ExportSnapshot().AcceptedSequence, Is.EqualTo(2L));
        }

        [Test]
        public void HealingDeadActor_FailsClosed()
        {
            PlayerActorAuthority actor = CreateActor();
            actor.ApplyDamage(Damage("lethal", 100d, 0L));

            PlayerActorHealingResult result = actor.ApplyHealing(Healing("heal-dead", 20d, 0L));

            Assert.That(result.Status, Is.EqualTo(PlayerActorOperationStatus.RejectedByLifecycle));
            Assert.That(result.RejectionCode, Is.EqualTo(PlayerActorOperationRejectionCode.ActorDead));
            Assert.That(result.Snapshot.CurrentHealth, Is.EqualTo(0d));
            Assert.That(result.Snapshot.AcceptedSequence, Is.EqualTo(1L));
        }

        [Test]
        public void DamageForWrongTarget_IsRejectedWithoutRecordingEvent()
        {
            PlayerActorAuthority actor = CreateActor();
            DamageReceiverCommand wrongTarget = new DamageReceiverCommand(
                Id("event", "wrong-target"),
                Id("actor", "enemy-a"),
                null,
                Id("actor", "player-b"),
                10d,
                CombatChannel.Kinetic,
                0L);

            DamageReceiverResult result = actor.ApplyDamage(wrongTarget);

            Assert.That(result.Status, Is.EqualTo(DamageReceiverStatus.RejectedInvalid));
            Assert.That(result.RejectionCode, Is.EqualTo(DamageReceiverRejectionCode.TargetMismatch));
            Assert.That(actor.ExportSnapshot().CurrentHealth, Is.EqualTo(100d));
            Assert.That(actor.ExportSnapshot().AcceptedSequence, Is.EqualTo(0L));
        }

        [Test]
        public void Restart_RestoresHealthPreservesIdentityAndRejectsStaleCombat()
        {
            PlayerActorAuthority actor = CreateActor();
            PlayerActorSnapshot original = actor.ExportSnapshot();
            actor.ApplyDamage(Damage("damage-a", 60d, 0L));

            PlayerActorRestartResult restart = actor.Restart(
                new PlayerActorRestartCommand(
                    Id("operation", "restart-a"),
                    original.ActorInstanceId,
                    0L,
                    1L));
            DamageReceiverResult stale = actor.ApplyDamage(Damage("stale", 10d, 0L));

            Assert.That(restart.Status, Is.EqualTo(PlayerActorOperationStatus.Applied));
            Assert.That(restart.Snapshot.CurrentHealth, Is.EqualTo(100d));
            Assert.That(restart.Snapshot.IsAlive, Is.True);
            Assert.That(restart.Snapshot.LifecycleGeneration, Is.EqualTo(1L));
            Assert.That(restart.Snapshot.ActorInstanceId, Is.EqualTo(original.ActorInstanceId));
            Assert.That(restart.Snapshot.RunParticipantId, Is.EqualTo(original.RunParticipantId));
            Assert.That(restart.Snapshot.CharacterId, Is.EqualTo(original.CharacterId));
            Assert.That(restart.Snapshot.FactionId, Is.EqualTo(original.FactionId));
            Assert.That(stale.Status, Is.EqualTo(DamageReceiverStatus.RejectedByLifecycle));
            Assert.That(stale.RejectionCode, Is.EqualTo(DamageReceiverRejectionCode.StaleGeneration));
            Assert.That(actor.ExportSnapshot().CurrentHealth, Is.EqualTo(100d));
        }

        [Test]
        public void SameEventId_MayBeReusedAfterGenerationRestart()
        {
            PlayerActorAuthority actor = CreateActor();
            DamageReceiverResult generationZero = actor.ApplyDamage(Damage("repeatable", 10d, 0L));
            actor.Restart(
                new PlayerActorRestartCommand(
                    Id("operation", "restart-a"),
                    Id("actor", "player-a"),
                    0L,
                    1L));
            DamageReceiverResult generationOne = actor.ApplyDamage(Damage("repeatable", 25d, 1L));

            Assert.That(generationZero.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(generationOne.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(actor.ExportSnapshot().CurrentHealth, Is.EqualTo(75d));
            Assert.That(actor.ExportSnapshot().LifecycleGeneration, Is.EqualTo(1L));
        }

        [Test]
        public void ExactRestartReplay_IsIdempotent()
        {
            PlayerActorAuthority actor = CreateActor();
            PlayerActorRestartCommand command = new PlayerActorRestartCommand(
                Id("operation", "restart-a"),
                Id("actor", "player-a"),
                0L,
                1L);

            PlayerActorRestartResult first = actor.Restart(command);
            PlayerActorRestartResult replay = actor.Restart(command);

            Assert.That(first.Status, Is.EqualTo(PlayerActorOperationStatus.Applied));
            Assert.That(replay.Status, Is.EqualTo(PlayerActorOperationStatus.Duplicate));
            Assert.That(replay.Snapshot.LifecycleGeneration, Is.EqualTo(1L));
            Assert.That(replay.Snapshot.AcceptedSequence, Is.EqualTo(1L));
        }

        [Test]
        public void EquivalentAuthoredData_ProducesIndependentActorInstances()
        {
            PlayerActorDefinition definition = Definition(100d, 0L);
            PlayerActorAuthority first = PlayerActorAuthority.TryCreate(definition).Authority;
            PlayerActorAuthority second = PlayerActorAuthority.TryCreate(definition).Authority;

            first.ApplyDamage(Damage("first-only", 30d, 0L));

            Assert.That(first.ExportSnapshot().CurrentHealth, Is.EqualTo(70d));
            Assert.That(second.ExportSnapshot().CurrentHealth, Is.EqualTo(100d));
            Assert.That(second.ExportSnapshot().AcceptedSequence, Is.EqualTo(0L));
        }

        [Test]
        public void TwoRunParticipants_RemainDistinctInDeathAttribution()
        {
            PlayerActorAuthority firstTarget = CreateActor(
                Id("actor", "target-a"),
                Id("participant", "target-participant-a"),
                Id("character", "striker"),
                Id("faction", "players"),
                50d,
                0L);
            PlayerActorAuthority secondTarget = CreateActor(
                Id("actor", "target-b"),
                Id("participant", "target-participant-b"),
                Id("character", "striker"),
                Id("faction", "players"),
                50d,
                0L);

            GameplayEntityDeathFact firstDeath = firstTarget.ApplyDamage(
                DamageFor(
                    "lethal-a",
                    Id("actor", "target-a"),
                    Id("participant", "attacker-a"),
                    50d,
                    0L)).DeathFact;
            GameplayEntityDeathFact secondDeath = secondTarget.ApplyDamage(
                DamageFor(
                    "lethal-b",
                    Id("actor", "target-b"),
                    Id("participant", "attacker-b"),
                    50d,
                    0L)).DeathFact;

            Assert.That(firstDeath.SourceRunParticipantId, Is.EqualTo(Id("participant", "attacker-a")));
            Assert.That(secondDeath.SourceRunParticipantId, Is.EqualTo(Id("participant", "attacker-b")));
            Assert.That(firstDeath.SourceRunParticipantId, Is.Not.EqualTo(secondDeath.SourceRunParticipantId));
        }

        [Test]
        public void NeutralEntityIdentity_DoesNotInventParticipantOrCharacter()
        {
            GameplayEntityIdentity identity = new GameplayEntityIdentity(
                Id("actor", "neutral-prop"),
                GameplayEntityOwnership.None(),
                Id("faction", "neutral"),
                0L);

            Assert.That(identity.Ownership.HasRunParticipant, Is.False);
            Assert.That(identity.Ownership.HasSourceCharacter, Is.False);
        }

        [Test]
        public void PublicApi_IsEngineNeutralAndExposesNoUnrelatedAuthority()
        {
            Assembly assembly = typeof(PlayerActorAuthority).Assembly;
            Assert.That(
                assembly.GetReferencedAssemblies().Any(
                    reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);

            string[] forbiddenNames =
            {
                "Inventory",
                "Experience",
                "Xp",
                "Kill",
                "Money",
                "Scrap",
                "Scene",
                "Navigation",
                "Hud",
                "Weapon",
                "Enemy",
            };

            foreach (Type type in assembly.GetExportedTypes()
                .Where(candidate => candidate.Namespace == "ShooterMover.GameplayEntities"))
            {
                Assert.That(IsUnityType(type), Is.False, type.FullName);

                foreach (PropertyInfo property in type.GetProperties())
                {
                    Assert.That(property.CanWrite, Is.False, type.FullName + "." + property.Name);
                    Assert.That(IsUnityType(property.PropertyType), Is.False, property.Name);
                    AssertNoForbiddenName(property.Name, forbiddenNames);
                }

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    Assert.That(IsUnityType(method.ReturnType), Is.False, method.Name);
                    AssertNoForbiddenName(method.Name, forbiddenNames);
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        Assert.That(IsUnityType(parameter.ParameterType), Is.False, method.Name);
                    }
                }
            }
        }

        private static PlayerActorDefinition Definition(double maximumHealth, long generation)
        {
            return new PlayerActorDefinition(
                Id("actor", "player-a"),
                Id("participant", "local-a"),
                Id("character", "striker"),
                Id("faction", "players"),
                maximumHealth,
                generation);
        }

        private static PlayerActorAuthority CreateActor()
        {
            return CreateActor(
                Id("actor", "player-a"),
                Id("participant", "local-a"),
                Id("character", "striker"),
                Id("faction", "players"),
                100d,
                0L);
        }

        private static PlayerActorAuthority CreateActor(
            StableId actorId,
            StableId participantId,
            StableId characterId,
            StableId factionId,
            double maximumHealth,
            long generation)
        {
            PlayerActorCreationResult result = PlayerActorAuthority.TryCreate(
                new PlayerActorDefinition(
                    actorId,
                    participantId,
                    characterId,
                    factionId,
                    maximumHealth,
                    generation));
            Assert.That(result.Status, Is.EqualTo(PlayerActorCreationStatus.Created));
            return result.Authority;
        }

        private static DamageReceiverCommand Damage(string eventValue, double amount, long generation)
        {
            return DamageFor(eventValue, Id("actor", "player-a"), null, amount, generation);
        }

        private static DamageReceiverCommand DamageFor(
            string eventValue,
            StableId targetActorId,
            StableId sourceParticipantId,
            double amount,
            long generation)
        {
            return new DamageReceiverCommand(
                Id("event", eventValue),
                Id("actor", "enemy-a"),
                sourceParticipantId,
                targetActorId,
                amount,
                CombatChannel.Kinetic,
                generation);
        }

        private static PlayerActorHealingCommand Healing(
            string operationValue,
            double amount,
            long generation)
        {
            return new PlayerActorHealingCommand(
                Id("operation", operationValue),
                Id("actor", "medic-a"),
                Id("actor", "player-a"),
                amount,
                generation);
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        private static void AssertRejected(
            PlayerActorDefinition definition,
            PlayerActorCreationRejectionCode expected)
        {
            PlayerActorCreationResult result = PlayerActorAuthority.TryCreate(definition);
            Assert.That(result.Status, Is.EqualTo(PlayerActorCreationStatus.RejectedInvalid));
            Assert.That(result.RejectionCode, Is.EqualTo(expected));
            Assert.That(result.Authority, Is.Null);
        }

        private static bool IsUnityType(Type type)
        {
            string namespaceName = type == null ? null : type.Namespace;
            return namespaceName != null
                && namespaceName.StartsWith("UnityEngine", StringComparison.Ordinal);
        }

        private static void AssertNoForbiddenName(string name, string[] forbiddenNames)
        {
            foreach (string forbidden in forbiddenNames)
            {
                Assert.That(
                    name.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0),
                    name + " exposes forbidden responsibility token " + forbidden);
            }
        }
    }
}

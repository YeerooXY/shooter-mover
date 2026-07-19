using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.Tests.EditMode.PlayerRuntime
{
    public sealed class PlayerLiveAuthorityTests
    {
        [Test]
        public void Damage_ChangesAuthorityStateExactlyOnce()
        {
            using (Fixture fixture = new Fixture("player-a", "participant-a"))
            {
                PlayerRuntimeComposition runtime = fixture.Construct();
                PlayerDamageRequest request = fixture.Damage("damage-once", 25d);

                DamageReceiverResult first = runtime.ApplyDamage(request);
                DamageReceiverResult replay = runtime.ApplyDamage(request);

                Assert.That(first.Status, Is.EqualTo(DamageReceiverStatus.Applied));
                Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
                Assert.That(runtime.ExportSnapshot().Player.CurrentHealth, Is.EqualTo(75d));
                Assert.That(runtime.ExportSnapshot().Player.AcceptedSequence, Is.EqualTo(1L));
            }
        }

        [Test]
        public void ConflictingDuplicateDamage_IsRejectedWithoutMutation()
        {
            using (Fixture fixture = new Fixture("player-a", "participant-a"))
            {
                PlayerRuntimeComposition runtime = fixture.Construct();
                runtime.ApplyDamage(fixture.Damage("damage-conflict", 20d));

                DamageReceiverResult conflict = runtime.ApplyDamage(
                    fixture.Damage("damage-conflict", 35d));

                Assert.That(
                    conflict.Status,
                    Is.EqualTo(DamageReceiverStatus.RejectedInvalid));
                Assert.That(
                    conflict.RejectionCode,
                    Is.EqualTo(DamageReceiverRejectionCode.ConflictingDuplicate));
                Assert.That(runtime.ExportSnapshot().Player.CurrentHealth, Is.EqualTo(80d));
                Assert.That(runtime.ExportSnapshot().Player.AcceptedSequence, Is.EqualTo(1L));
            }
        }

        [Test]
        public void LethalDamage_EmitsDeathOnceAcrossReplay()
        {
            using (Fixture fixture = new Fixture("player-a", "participant-a"))
            {
                PlayerRuntimeComposition runtime = fixture.Construct();
                PlayerDamageRequest lethal = fixture.Damage("lethal", 150d);

                DamageReceiverResult first = runtime.ApplyDamage(lethal);
                DamageReceiverResult replay = runtime.ApplyDamage(lethal);

                Assert.That(first.DeathFact, Is.Not.Null);
                Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
                Assert.That(replay.DeathFact, Is.Null);
                Assert.That(fixture.RunCoordinator.DeathFacts.Count, Is.EqualTo(1));
                Assert.That(runtime.ExportSnapshot().Player.IsDead, Is.True);
            }
        }

        [Test]
        public void HealingAtFullHealth_IsAcceptedNoEffect()
        {
            using (Fixture fixture = new Fixture("player-a", "participant-a"))
            {
                PlayerRuntimeComposition runtime = fixture.Construct();

                PlayerActorHealingResult result = runtime.ApplyHealing(
                    new PlayerHealingRequest(
                        Id("operation", "heal-full"),
                        Id("actor", "medic"),
                        null,
                        fixture.ActorId,
                        15d,
                        0L));

                Assert.That(
                    result.Status,
                    Is.EqualTo(PlayerActorOperationStatus.AcceptedNoEffect));
                Assert.That(result.AppliedAmount, Is.Zero);
                Assert.That(result.StateChanged, Is.False);
                Assert.That(runtime.ExportSnapshot().Player.CurrentHealth, Is.EqualTo(100d));
            }
        }

        [Test]
        public void Restart_RestoresHealthAndPreservesEntityIdentity()
        {
            using (Fixture fixture = new Fixture("player-a", "participant-a"))
            {
                PlayerRuntimeComposition runtime = fixture.Construct();
                StableId identity = runtime.ExportSnapshot().Player.ActorInstanceId;
                runtime.ApplyDamage(fixture.Damage("before-restart", 70d));

                PlayerRuntimeRestartResult result = runtime.Restart(
                    new PlayerRuntimeRestartCommand(
                        Id("operation", "restart-g1"),
                        fixture.ActorId,
                        0L,
                        1L));

                Assert.That(result.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
                Assert.That(result.Snapshot.Player.ActorInstanceId, Is.EqualTo(identity));
                Assert.That(result.Snapshot.Player.LifecycleGeneration, Is.EqualTo(1L));
                Assert.That(result.Snapshot.Movement.Generation, Is.EqualTo(1L));
                Assert.That(result.Snapshot.Player.CurrentHealth, Is.EqualTo(100d));
                Assert.That(fixture.Movement.RestartCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void SeparatePlayersAndRunParticipants_DoNotShareAuthorityState()
        {
            using (Fixture first = new Fixture("player-a", "participant-a"))
            using (Fixture second = new Fixture("player-b", "participant-b"))
            {
                PlayerRuntimeComposition firstRuntime = first.Construct();
                PlayerRuntimeComposition secondRuntime = second.Construct();

                firstRuntime.ApplyDamage(first.Damage("first-only", 30d));

                PlayerActorSnapshot firstPlayer = firstRuntime.ExportSnapshot().Player;
                PlayerActorSnapshot secondPlayer = secondRuntime.ExportSnapshot().Player;
                Assert.That(firstPlayer.ActorInstanceId, Is.Not.EqualTo(secondPlayer.ActorInstanceId));
                Assert.That(firstPlayer.RunParticipantId, Is.Not.EqualTo(secondPlayer.RunParticipantId));
                Assert.That(firstPlayer.CurrentHealth, Is.EqualTo(70d));
                Assert.That(secondPlayer.CurrentHealth, Is.EqualTo(100d));
                Assert.That(secondPlayer.AcceptedSequence, Is.Zero);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture(string actorValue, string participantValue)
            {
                ActorId = Id("actor", actorValue);
                Root = new PlayerRuntimeCompositionRoot();
                Movement = new FakeMovement(0L);
                Input = new FakeInput();
                RunCoordinator = new FakeRunCoordinator();
                Configuration = new PlayerRuntimeConfiguration(
                    new PlayerActorDefinition(
                        ActorId,
                        Id("participant", participantValue),
                        Id("character", "striker"),
                        Id("faction", "player"),
                        100d,
                        0L));
                Attachments = new PlayerRuntimeAttachments(
                    Movement,
                    new FakePresentation(),
                    Input,
                    new NullAttributionResolver(),
                    RunCoordinator);
            }

            public StableId ActorId { get; }
            public PlayerRuntimeCompositionRoot Root { get; }
            public FakeMovement Movement { get; }
            public FakeInput Input { get; }
            public FakeRunCoordinator RunCoordinator { get; }
            public PlayerRuntimeConfiguration Configuration { get; }
            public PlayerRuntimeAttachments Attachments { get; }

            public PlayerRuntimeComposition Construct()
            {
                PlayerRuntimeConstructionResult result =
                    Root.TryConstruct(Configuration, Attachments);
                Assert.That(result.IsConstructed, Is.True);
                return result.Runtime;
            }

            public PlayerDamageRequest Damage(string eventValue, double amount)
            {
                return new PlayerDamageRequest(
                    Id("event", eventValue),
                    Id("actor", "enemy"),
                    null,
                    ActorId,
                    amount,
                    CombatChannel.Kinetic,
                    0L);
            }

            public void Dispose()
            {
                Root.Dispose();
            }
        }

        private sealed class FakeMovement : IPlayerMovementRuntime
        {
            private long generation;

            public FakeMovement(long generation)
            {
                this.generation = generation;
            }

            public bool IsDisposed { get; private set; }
            public int RestartCount { get; private set; }

            public PlayerMovementSnapshot ExportSnapshot()
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name);
                }

                return new PlayerMovementSnapshot(
                    generation,
                    0d,
                    0d,
                    0d,
                    0d,
                    Thruster(generation));
            }

            public bool TryRestart(
                long retiringGeneration,
                long replacementGeneration)
            {
                if (IsDisposed
                    || retiringGeneration != generation
                    || retiringGeneration == long.MaxValue
                    || replacementGeneration != retiringGeneration + 1L)
                {
                    return false;
                }

                RestartCount++;
                generation = replacementGeneration;
                return true;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class FakePresentation : IPlayerPresentationRuntime
        {
            public void RefreshContinuousBoost(
                PlayerMovementSnapshot movementSnapshot)
            {
            }

            public void Restart(PlayerRuntimeSnapshot runtimeSnapshot)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class FakeInput : IPlayerInputRuntime
        {
            private PlayerInputOwnership ownership;

            public bool TryAcquire(PlayerInputOwnership requested)
            {
                if (requested == null || ownership != null)
                {
                    return false;
                }

                ownership = requested;
                return true;
            }

            public bool Release(PlayerInputOwnership requested)
            {
                if (requested == null
                    || ownership == null
                    || !ownership.Equals(requested))
                {
                    return false;
                }

                ownership = null;
                return true;
            }

            public void Dispose()
            {
                ownership = null;
            }
        }

        private sealed class NullAttributionResolver :
            ITrustedPlayerAttributionResolver
        {
            public StableId ResolveSourceRunParticipant(StableId sourceActorId)
            {
                return null;
            }
        }

        private sealed class FakeRunCoordinator : IPlayerRunCoordinator
        {
            public FakeRunCoordinator()
            {
                DeathFacts = new List<GameplayEntityDeathFact>();
            }

            public List<GameplayEntityDeathFact> DeathFacts { get; }

            public void ObservePlayerDeath(GameplayEntityDeathFact deathFact)
            {
                DeathFacts.Add(deathFact);
            }
        }

        private static ThrusterStatusSnapshot Thruster(long generation)
        {
            return new ThrusterStatusSnapshot(
                ThrusterStatusState.Ready,
                Id("movement-tuning", "player-live-test"),
                generation,
                2,
                2,
                0,
                1d,
                ThrusterBurstPhase.Ready,
                1d,
                2d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                1d,
                0d);
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.Tests.EditMode.PlayerRuntime
{
    public sealed class PlayerRuntimeCompositionTests
    {
        [Test]
        public void Construction_CreatesExactlyOneAuthorityAndAcquiresInputOnce()
        {
            Fixture fixture = new Fixture();

            PlayerRuntimeConstructionResult result = fixture.Root.TryConstruct(
                fixture.Configuration,
                fixture.Attachments);

            Assert.That(result.Status, Is.EqualTo(PlayerRuntimeConstructionStatus.Constructed));
            Assert.That(result.Runtime, Is.SameAs(fixture.Root.Runtime));
            Assert.That(result.Runtime.ExportSnapshot().Player.ActorInstanceId,
                Is.EqualTo(Id("actor", "player-a")));
            Assert.That(fixture.Input.AcquireCount, Is.EqualTo(1));
            Assert.That(fixture.Movement.ExportCount, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateConstruction_IsRejectedWithoutSecondInputOwnership()
        {
            Fixture fixture = new Fixture();
            fixture.Construct();

            PlayerRuntimeConstructionResult duplicate = fixture.Root.TryConstruct(
                fixture.Configuration,
                fixture.Attachments);

            Assert.That(duplicate.Status,
                Is.EqualTo(PlayerRuntimeConstructionStatus.RejectedDuplicate));
            Assert.That(duplicate.RejectionCode,
                Is.EqualTo(PlayerRuntimeConstructionRejectionCode.AlreadyConstructed));
            Assert.That(fixture.Input.AcquireCount, Is.EqualTo(1));
        }

        [Test]
        public void DamageAndHealing_AreForwardedToPlayerActorAuthority()
        {
            Fixture fixture = new Fixture();
            PlayerRuntimeComposition runtime = fixture.Construct();

            DamageReceiverResult damage = runtime.ApplyDamage(new PlayerDamageRequest(
                Id("event", "damage-a"),
                Id("actor", "enemy-a"),
                Id("participant", "untrusted-client-claim"),
                Id("actor", "player-a"),
                25d,
                CombatChannel.Kinetic,
                1L));
            PlayerActorHealingResult healing = runtime.ApplyHealing(
                new PlayerHealingRequest(
                    Id("operation", "heal-a"),
                    Id("actor", "medic-a"),
                    Id("participant", "untrusted-medic-claim"),
                    Id("actor", "player-a"),
                    10d,
                    1L));

            Assert.That(damage.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(damage.Command.EventId, Is.EqualTo(Id("event", "damage-a")));
            Assert.That(damage.Command.SourceActorId, Is.EqualTo(Id("actor", "enemy-a")));
            Assert.That(damage.Command.SourceRunParticipantId,
                Is.EqualTo(Id("participant", "trusted-enemy-owner")));
            Assert.That(damage.Command.SourceRunParticipantId,
                Is.Not.EqualTo(Id("participant", "untrusted-client-claim")));
            Assert.That(healing.Status, Is.EqualTo(PlayerActorOperationStatus.Applied));
            Assert.That(healing.AppliedAmount, Is.EqualTo(10d));
            Assert.That(runtime.ExportSnapshot().Player.CurrentHealth, Is.EqualTo(85d));
        }

        [Test]
        public void FullHealthHealing_RemainsAcceptedNoEffect()
        {
            Fixture fixture = new Fixture();
            PlayerRuntimeComposition runtime = fixture.Construct();

            PlayerActorHealingResult result = runtime.ApplyHealing(
                new PlayerHealingRequest(
                    Id("operation", "heal-full"),
                    Id("actor", "medic-a"),
                    Id("participant", "untrusted-medic-claim"),
                    Id("actor", "player-a"),
                    10d,
                    1L));

            Assert.That(result.Status, Is.EqualTo(PlayerActorOperationStatus.AcceptedNoEffect));
            Assert.That(result.AppliedAmount, Is.EqualTo(0d));
            Assert.That(result.StateChanged, Is.False);
            Assert.That(runtime.ExportSnapshot().Player.CurrentHealth, Is.EqualTo(100d));
        }

        [Test]
        public void LethalDamage_ProjectsOneDeathFactToRunCoordinator()
        {
            Fixture fixture = new Fixture();
            PlayerRuntimeComposition runtime = fixture.Construct();
            PlayerDamageRequest lethal = new PlayerDamageRequest(
                Id("event", "lethal"),
                Id("actor", "enemy-a"),
                null,
                Id("actor", "player-a"),
                150d,
                CombatChannel.Kinetic,
                1L);

            DamageReceiverResult first = runtime.ApplyDamage(lethal);
            DamageReceiverResult replay = runtime.ApplyDamage(lethal);

            Assert.That(first.DeathFact, Is.Not.Null);
            Assert.That(first.DeathFact.SourceRunParticipantId,
                Is.EqualTo(Id("participant", "trusted-enemy-owner")));
            Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
            Assert.That(fixture.RunCoordinator.DeathFacts.Count, Is.EqualTo(1));
            Assert.That(fixture.RunCoordinator.DeathFacts[0], Is.SameAs(first.DeathFact));
        }

        [Test]
        public void Restart_AdvancesPlayerAndMovementGenerationTogether()
        {
            Fixture fixture = new Fixture();
            PlayerRuntimeComposition runtime = fixture.Construct();
            runtime.ApplyDamage(new PlayerDamageRequest(
                Id("event", "damage-before-restart"),
                Id("actor", "enemy-a"),
                null,
                Id("actor", "player-a"),
                60d,
                CombatChannel.Kinetic,
                1L));
            PlayerRuntimeRestartCommand command = new PlayerRuntimeRestartCommand(
                Id("operation", "restart-a"),
                Id("actor", "player-a"),
                1L,
                2L);

            PlayerRuntimeRestartResult result = runtime.Restart(command);
            PlayerRuntimeRestartResult replay = runtime.Restart(command);

            Assert.That(result.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            Assert.That(result.Snapshot.Player.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(result.Snapshot.Movement.Generation, Is.EqualTo(2L));
            Assert.That(result.Snapshot.Player.CurrentHealth, Is.EqualTo(100d));
            Assert.That(fixture.Movement.RestartCount, Is.EqualTo(1));
            Assert.That(fixture.Presentation.RestartCount, Is.EqualTo(1));
            Assert.That(replay.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Duplicate));
            Assert.That(fixture.Movement.RestartCount, Is.EqualTo(1));
        }

        [Test]
        public void StaleRestartGeneration_IsRejectedBeforeEitherAuthorityMoves()
        {
            Fixture fixture = new Fixture();
            PlayerRuntimeComposition runtime = fixture.Construct();

            PlayerRuntimeRestartResult result = runtime.Restart(
                new PlayerRuntimeRestartCommand(
                    Id("operation", "restart-stale"),
                    Id("actor", "player-a"),
                    0L,
                    1L));

            Assert.That(result.Status,
                Is.EqualTo(PlayerRuntimeRestartStatus.RejectedByLifecycle));
            Assert.That(result.RejectionCode,
                Is.EqualTo(PlayerRuntimeRestartRejectionCode.StaleGeneration));
            Assert.That(runtime.ExportSnapshot().Player.LifecycleGeneration, Is.EqualTo(1L));
            Assert.That(runtime.ExportSnapshot().Movement.Generation, Is.EqualTo(1L));
            Assert.That(fixture.Movement.RestartCount, Is.EqualTo(0));
            Assert.That(fixture.Presentation.RestartCount, Is.EqualTo(0));
        }

        [Test]
        public void Disposal_IsIdempotentAcrossOwnedResources()
        {
            Fixture fixture = new Fixture();
            fixture.Construct();

            fixture.Root.Dispose();
            fixture.Root.Dispose();

            Assert.That(fixture.Input.ReleaseCount, Is.EqualTo(1));
            Assert.That(fixture.Input.DisposeCount, Is.EqualTo(1));
            Assert.That(fixture.Presentation.DisposeCount, Is.EqualTo(1));
            Assert.That(fixture.Movement.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void SharedInputAdapter_CannotBeOwnedByTwoPlayerRuntimes()
        {
            FakeInput input = new FakeInput();
            Fixture first = new Fixture(input);
            Fixture second = new Fixture(input, "player-b", "participant-b");
            first.Construct();

            PlayerRuntimeConstructionResult rejected = second.Root.TryConstruct(
                second.Configuration,
                second.Attachments);

            Assert.That(rejected.Status,
                Is.EqualTo(PlayerRuntimeConstructionStatus.RejectedOwnership));
            Assert.That(rejected.RejectionCode,
                Is.EqualTo(PlayerRuntimeConstructionRejectionCode.InputOwnershipUnavailable));
            Assert.That(input.AcquireCount, Is.EqualTo(2));
            Assert.That(input.CurrentOwnership.ActorInstanceId,
                Is.EqualTo(Id("actor", "player-a")));
        }

        [Test]
        public void HudProjection_IsGetterOnlyAndCannotMutateAuthority()
        {
            Fixture fixture = new Fixture();
            PlayerRuntimeComposition runtime = fixture.Construct();
            runtime.ApplyDamage(new PlayerDamageRequest(
                Id("event", "hud-damage"),
                Id("actor", "enemy-a"),
                null,
                Id("actor", "player-a"),
                40d,
                CombatChannel.Kinetic,
                1L));
            PlayerActorSnapshot before = runtime.ExportSnapshot().Player;

            PlayerHudHealthSnapshot hud = runtime.ExportHudHealth();
            PropertyInfo[] publicProperties = typeof(PlayerHudHealthSnapshot)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            Assert.That(hud.CurrentHealth, Is.EqualTo(60d));
            Assert.That(hud.MaximumHealth, Is.EqualTo(100d));
            Assert.That(hud.NormalizedHealth, Is.EqualTo(0.6d));
            Assert.That(publicProperties.All(property => !property.CanWrite), Is.True);
            Assert.That(runtime.ExportSnapshot().Player, Is.EqualTo(before));
        }

        [Test]
        public void ContinuousBoostRefresh_ReceivesDetachedMovementSnapshot()
        {
            Fixture fixture = new Fixture();
            PlayerRuntimeComposition runtime = fixture.Construct();

            bool refreshed = runtime.RefreshContinuousPresentation();

            Assert.That(refreshed, Is.True);
            Assert.That(fixture.Presentation.RefreshCount, Is.EqualTo(1));
            Assert.That(fixture.Presentation.LastMovementSnapshot, Is.Not.Null);
            Assert.That(fixture.Presentation.LastMovementSnapshot.Generation, Is.EqualTo(1L));
        }

        private sealed class Fixture
        {
            public Fixture(
                FakeInput sharedInput = null,
                string actorValue = "player-a",
                string participantValue = "player-a")
            {
                Root = new PlayerRuntimeCompositionRoot();
                Movement = new FakeMovement(1L);
                Presentation = new FakePresentation();
                Input = sharedInput ?? new FakeInput();
                Attribution = new FakeAttributionResolver();
                RunCoordinator = new FakeRunCoordinator();
                Configuration = new PlayerRuntimeConfiguration(
                    new PlayerActorDefinition(
                        Id("actor", actorValue),
                        Id("participant", participantValue),
                        Id("character", "striker"),
                        Id("faction", "players"),
                        100d,
                        1L));
                Attachments = new PlayerRuntimeAttachments(
                    Movement,
                    Presentation,
                    Input,
                    Attribution,
                    RunCoordinator);
            }

            public PlayerRuntimeCompositionRoot Root { get; }
            public FakeMovement Movement { get; }
            public FakePresentation Presentation { get; }
            public FakeInput Input { get; }
            public FakeAttributionResolver Attribution { get; }
            public FakeRunCoordinator RunCoordinator { get; }
            public PlayerRuntimeConfiguration Configuration { get; }
            public PlayerRuntimeAttachments Attachments { get; }

            public PlayerRuntimeComposition Construct()
            {
                PlayerRuntimeConstructionResult result = Root.TryConstruct(
                    Configuration,
                    Attachments);
                Assert.That(result.Status, Is.EqualTo(PlayerRuntimeConstructionStatus.Constructed));
                return result.Runtime;
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
            public int ExportCount { get; private set; }
            public int RestartCount { get; private set; }
            public int DisposeCount { get; private set; }

            public PlayerMovementSnapshot ExportSnapshot()
            {
                ExportCount++;
                return new PlayerMovementSnapshot(
                    generation,
                    4d,
                    5d,
                    1d,
                    2d,
                    Thruster(generation));
            }

            public bool TryRestart(long retiringGeneration, long replacementGeneration)
            {
                RestartCount++;
                if (retiringGeneration != generation || replacementGeneration != generation + 1L)
                {
                    return false;
                }

                generation = replacementGeneration;
                return true;
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                DisposeCount++;
            }
        }

        private sealed class FakePresentation : IPlayerPresentationRuntime
        {
            private bool disposed;

            public int RefreshCount { get; private set; }
            public int RestartCount { get; private set; }
            public int DisposeCount { get; private set; }
            public PlayerMovementSnapshot LastMovementSnapshot { get; private set; }

            public void RefreshContinuousBoost(PlayerMovementSnapshot movementSnapshot)
            {
                RefreshCount++;
                LastMovementSnapshot = movementSnapshot;
            }

            public void Restart(PlayerRuntimeSnapshot runtimeSnapshot)
            {
                RestartCount++;
                LastMovementSnapshot = runtimeSnapshot.Movement;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                DisposeCount++;
            }
        }

        private sealed class FakeInput : IPlayerInputRuntime
        {
            private bool disposed;

            public int AcquireCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public int DisposeCount { get; private set; }
            public PlayerInputOwnership CurrentOwnership { get; private set; }

            public bool TryAcquire(PlayerInputOwnership ownership)
            {
                AcquireCount++;
                if (disposed || CurrentOwnership != null)
                {
                    return false;
                }

                CurrentOwnership = ownership;
                return true;
            }

            public bool Release(PlayerInputOwnership ownership)
            {
                if (CurrentOwnership == null || !CurrentOwnership.Equals(ownership))
                {
                    return false;
                }

                ReleaseCount++;
                CurrentOwnership = null;
                return true;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                DisposeCount++;
            }
        }

        private sealed class FakeAttributionResolver : ITrustedPlayerAttributionResolver
        {
            public StableId ResolveSourceRunParticipant(StableId sourceActorId)
            {
                if (sourceActorId == Id("actor", "enemy-a"))
                {
                    return Id("participant", "trusted-enemy-owner");
                }

                return sourceActorId == Id("actor", "medic-a")
                    ? Id("participant", "trusted-medic-owner")
                    : null;
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
                Id("movement-tuning", "test"),
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

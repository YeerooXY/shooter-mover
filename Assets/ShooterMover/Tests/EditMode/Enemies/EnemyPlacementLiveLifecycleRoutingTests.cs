using System;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyFactoryTestsLifecycleRouting
    {
        [Test]
        public void ProjectileObservedAgainstPreviousPlayerGeneration_RejectsAtDamagePort()
        {
            var fixture = new Fixture("player-generation", 5L);
            EnemyAttackExecutionRequest execution = fixture.Execute(1L);

            EnemyPlayerDamagePortResult stale = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "player-generation-four"),
                Id("entity", "player"),
                4L);
            EnemyPlayerDamagePortResult current = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "player-generation-five"),
                Id("entity", "player"),
                5L);

            Assert.That(stale.Status, Is.EqualTo(EnemyLiveOperationStatus.Rejected));
            Assert.That(stale.Rejection, Is.EqualTo(EnemyLiveRejectionCode.StaleLifecycle));
            Assert.That(current.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(fixture.Ports.RouteCount, Is.EqualTo(2));
            Assert.That(fixture.Ports.LastRequest.ObservedTargetLifecycleGeneration, Is.EqualTo(5L));
        }

        [Test]
        public void DeadEnemy_CannotRealizeMovementFromNewlyEvaluatedDecision()
        {
            var fixture = new Fixture("terminal-movement", 1L);
            fixture.Kill();
            EnemyPlacementDecision terminalDecision = fixture.Runtime.Evaluate(Perception(2L));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                fixture.Runtime.RealizeMovement(
                    terminalDecision,
                    new EnemyMovementRealizationContext(
                        fixture.Runtime.SpawnStableId,
                        fixture.Runtime.RoomStableId,
                        new EnemyVector2(0d, 0d),
                        new EnemyVector2(1d, 0d),
                        2L,
                        1d,
                        null)));

            Assert.That(error.Message, Does.Contain("Terminal enemies"));
            Assert.That(fixture.Runtime.ActorState.IsActive, Is.False);
        }

        private sealed class Fixture
        {
            public Fixture(string name, long currentPlayerGeneration)
            {
                Name = name;
                Ports = new LifecycleAwarePorts(currentPlayerGeneration);
                EnemyDefinition definition = Definition(name);
                var factory = new EnemyFactory(
                    new RoomContentObjectCatalog(new[]
                    {
                        new RoomContentObjectDefinition(
                            Id("room-object", name),
                            RoomContentObjectKind.Enemy,
                            definition.DefinitionId,
                            definition.PresentationId),
                    }),
                    new EnemyCatalog(
                        1,
                        Id("enemy-catalog", name + "-v1"),
                        new[] { definition }),
                    BuiltInEnemyRules.Create(),
                    new DeterministicEnemyLiveIdentityDeriver(),
                    new EnemyDifficultyLiveRegistration(
                        new EnemyDifficultyScalingConfiguration(
                            Id("enemy-difficulty", "fixture"),
                            1d,
                            0.5d,
                            0.2d,
                            0.15d),
                        new ScalarEnemyDifficultyScalingPolicy()),
                    new EnemyPerceptionLiveRegistration(
                        new EnemyPerceptionPolicyConfiguration(
                            Id("enemy-perception", "fixture")),
                        new ValidatedEnemyPerceptionLiveBridge()),
                    Ports.Bundle);
                Runtime = factory.Create(
                    new EnemyPlacementLiveRequest(
                        new RoomEnemyPlacementContent(
                            Id("enemy-placement", name),
                            Id("room", "fixture"),
                            Id("room-object", name),
                            1,
                            new RoomVector2(0d, 0d),
                            0d,
                            name),
                        Id("run", "fixture"),
                        Id("room-runtime", "fixture"),
                        null,
                        1L,
                        1L,
                        new EnemyDifficultyContext(
                            Id("difficulty", "fixture"),
                            1d))).Runtime;
            }

            public string Name { get; }
            public LifecycleAwarePorts Ports { get; }
            public EnemyInstance Runtime { get; }

            public EnemyAttackExecutionRequest Execute(long tick)
            {
                EnemyAttackExecutionResult result = Runtime.TryExecuteAttack(
                    Runtime.Evaluate(Perception(tick)),
                    Id("enemy-operation", Name + "-attack"),
                    1d);
                Assert.That(result.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
                return result.Request;
            }

            public void Kill()
            {
                EnemyLiveDamageResult result = Runtime.ApplyDamage(
                    new EnemyLiveDamageCommand(
                        Id("enemy-damage", Name + "-kill"),
                        Id("entity", "player"),
                        Id("run-participant", "player"),
                        Runtime.SpawnStableId,
                        Runtime.LifecycleGeneration,
                        0L,
                        1,
                        10000d));
                Assert.That(result.DeathFact, Is.Not.Null);
            }
        }

        private sealed class LifecycleAwarePorts :
            IEnemyAttackEffectPort,
            IEnemyPlayerDamagePort,
            IEnemyRoomTerminalPort,
            IEnemyExperienceFactConsumer,
            IEnemyDropFactConsumer,
            IEnemyKillStatFactConsumer,
            IEnemyTerminalCollisionBridge
        {
            private readonly long currentPlayerGeneration;

            public LifecycleAwarePorts(long currentPlayerGeneration)
            {
                this.currentPlayerGeneration = currentPlayerGeneration;
                Bundle = new EnemyLiveDownstreamPorts(
                    this, this, this, this, this, this, this);
            }

            public EnemyLiveDownstreamPorts Bundle { get; }
            public int RouteCount { get; private set; }
            public EnemyPlayerDamageRequest LastRequest { get; private set; }

            public void Emit(EnemyAttackExecutionRequest request) { }

            public EnemyPlayerDamagePortResult Route(EnemyPlayerDamageRequest request)
            {
                RouteCount++;
                LastRequest = request;
                return request.ObservedTargetLifecycleGeneration == currentPlayerGeneration
                    ? new EnemyPlayerDamagePortResult(
                        EnemyLiveOperationStatus.Applied,
                        EnemyLiveRejectionCode.None)
                    : new EnemyPlayerDamagePortResult(
                        EnemyLiveOperationStatus.Rejected,
                        EnemyLiveRejectionCode.StaleLifecycle);
            }

            public void Report(
                ReportRoomOccupantTerminalCommand command,
                EnemyDeathFact deathFact) { }
            void IEnemyExperienceFactConsumer.Consume(EnemyDeathFact fact) { }
            void IEnemyDropFactConsumer.Consume(EnemyDeathFact fact) { }
            void IEnemyKillStatFactConsumer.Consume(EnemyDeathFact fact) { }
            public void SetTerminal(EnemyTerminalCollisionFact fact) { }
        }

        private static EnemyDefinition Definition(string name)
        {
            return new EnemyDefinition(
                Id("enemy", name),
                Id("presentation", name),
                20d,
                new EnemyLevelScalingProfile(1, 100, 2d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                360d,
                Id("enemy-movement", "mobile-positioning"),
                Id("enemy-decision", "ranged-standard"),
                new[]
                {
                    new EnemyAttackCapabilityDescriptor(
                        Id("enemy-attack-profile", name),
                        Id("enemy-attack", "ranged-projectile"),
                        10,
                        120d,
                        0d,
                        3d,
                        6d,
                        1d,
                        4d,
                        Id("damage", "kinetic"),
                        new EnemyProjectileAttackParameters(
                            Id("projectile", name), 1, 12d, 16d, 0.15d, 0d, 0),
                        null,
                        null),
                },
                Id("xp", "standard"),
                Id("drop", "standard"),
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyPerceptionSnapshot Perception(long tick)
        {
            return new EnemyPerceptionSnapshot(
                new EnemyVector2(0d, 0d),
                new EnemyVector2(1d, 0d),
                new[]
                {
                    new EnemyPerceivedTarget(
                        Id("entity", "player"),
                        Id("faction", "players"),
                        EnemyTargetRelationship.Hostile,
                        new EnemyVector2(3d, 0d),
                        new EnemyVector2(0d, 0d),
                        3d,
                        new EnemyVector2(1d, 0d),
                        true,
                        true,
                        true),
                },
                tick);
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}

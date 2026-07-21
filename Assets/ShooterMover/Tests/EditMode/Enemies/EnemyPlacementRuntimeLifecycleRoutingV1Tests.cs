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
    public sealed class EnemyPlacementRuntimeFactoryV1Tests_LifecycleRouting
    {
        [Test]
        public void ProjectileObservedAgainstPreviousPlayerGeneration_RejectsAtDamagePort()
        {
            var fixture = new Fixture("player-generation", 5L);
            EnemyAttackExecutionRequestV1 execution = fixture.Execute(1L);

            EnemyPlayerDamagePortResultV1 stale = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "player-generation-four"),
                Id("entity", "player"),
                4L);
            EnemyPlayerDamagePortResultV1 current = fixture.Runtime.RoutePlayerImpact(
                execution,
                Id("enemy-hit", "player-generation-five"),
                Id("entity", "player"),
                5L);

            Assert.That(stale.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Rejected));
            Assert.That(stale.Rejection, Is.EqualTo(EnemyRuntimeRejectionCodeV1.StaleLifecycle));
            Assert.That(current.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(fixture.Ports.RouteCount, Is.EqualTo(2));
            Assert.That(fixture.Ports.LastRequest.ObservedTargetLifecycleGeneration, Is.EqualTo(5L));
        }

        [Test]
        public void DeadEnemy_CannotRealizeMovementFromNewlyEvaluatedDecision()
        {
            var fixture = new Fixture("terminal-movement", 1L);
            fixture.Kill();
            EnemyPlacementDecisionV1 terminalDecision = fixture.Runtime.Evaluate(Perception(2L));

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                fixture.Runtime.RealizeMovement(
                    terminalDecision,
                    new EnemyMovementRealizationContextV1(
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
                EnemyDefinitionV1 definition = Definition(name);
                var factory = new EnemyPlacementRuntimeFactoryV1(
                    new RoomContentObjectCatalogV1(new[]
                    {
                        new RoomContentObjectDefinitionV1(
                            Id("room-object", name),
                            RoomContentObjectKindV1.Enemy,
                            definition.DefinitionId,
                            definition.PresentationId),
                    }),
                    new EnemyCatalogV1(
                        1,
                        Id("enemy-catalog", name + "-v1"),
                        new[] { definition }),
                    BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                    new DeterministicEnemyRuntimeIdentityDeriverV1(),
                    new EnemyDifficultyRuntimeRegistrationV1(
                        new EnemyDifficultyScalingConfigurationV1(
                            Id("enemy-difficulty", "fixture"),
                            1d,
                            0.5d,
                            0.2d,
                            0.15d),
                        new ScalarEnemyDifficultyScalingPolicyV1()),
                    new EnemyPerceptionRuntimeRegistrationV1(
                        new EnemyPerceptionPolicyConfigurationV1(
                            Id("enemy-perception", "fixture")),
                        new ValidatedEnemyPerceptionRuntimeAdapterV1()),
                    Ports.Bundle);
                Runtime = factory.Create(
                    new EnemyPlacementRuntimeRequestV1(
                        new RoomEnemyPlacementContentV1(
                            Id("enemy-placement", name),
                            Id("room", "fixture"),
                            Id("room-object", name),
                            1,
                            new RoomVector2V1(0d, 0d),
                            0d,
                            name),
                        Id("run", "fixture"),
                        Id("room-runtime", "fixture"),
                        null,
                        1L,
                        1L,
                        new EnemyDifficultyContextV1(
                            Id("difficulty", "fixture"),
                            1d))).Runtime;
            }

            public string Name { get; }
            public LifecycleAwarePorts Ports { get; }
            public EnemyPlacementRuntimeInstanceV1 Runtime { get; }

            public EnemyAttackExecutionRequestV1 Execute(long tick)
            {
                EnemyAttackExecutionResultV1 result = Runtime.TryExecuteAttack(
                    Runtime.Evaluate(Perception(tick)),
                    Id("enemy-operation", Name + "-attack"),
                    1d);
                Assert.That(result.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
                return result.Request;
            }

            public void Kill()
            {
                EnemyRuntimeDamageResultV1 result = Runtime.ApplyDamage(
                    new EnemyRuntimeDamageCommandV1(
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
            IEnemyAttackEffectPortV1,
            IEnemyPlayerDamagePortV1,
            IEnemyRoomTerminalPortV1,
            IEnemyExperienceFactConsumerV1,
            IEnemyDropFactConsumerV1,
            IEnemyKillStatFactConsumerV1,
            IEnemyTerminalCollisionAdapterV1
        {
            private readonly long currentPlayerGeneration;

            public LifecycleAwarePorts(long currentPlayerGeneration)
            {
                this.currentPlayerGeneration = currentPlayerGeneration;
                Bundle = new EnemyRuntimeDownstreamPortsV1(
                    this, this, this, this, this, this, this);
            }

            public EnemyRuntimeDownstreamPortsV1 Bundle { get; }
            public int RouteCount { get; private set; }
            public EnemyPlayerDamageRequestV1 LastRequest { get; private set; }

            public void Emit(EnemyAttackExecutionRequestV1 request) { }

            public EnemyPlayerDamagePortResultV1 Route(EnemyPlayerDamageRequestV1 request)
            {
                RouteCount++;
                LastRequest = request;
                return request.ObservedTargetLifecycleGeneration == currentPlayerGeneration
                    ? new EnemyPlayerDamagePortResultV1(
                        EnemyRuntimeOperationStatusV1.Applied,
                        EnemyRuntimeRejectionCodeV1.None)
                    : new EnemyPlayerDamagePortResultV1(
                        EnemyRuntimeOperationStatusV1.Rejected,
                        EnemyRuntimeRejectionCodeV1.StaleLifecycle);
            }

            public void Report(
                ReportRoomOccupantTerminalCommandV1 command,
                EnemyDeathFactV1 deathFact) { }
            void IEnemyExperienceFactConsumerV1.Consume(EnemyDeathFactV1 fact) { }
            void IEnemyDropFactConsumerV1.Consume(EnemyDeathFactV1 fact) { }
            void IEnemyKillStatFactConsumerV1.Consume(EnemyDeathFactV1 fact) { }
            public void SetTerminal(EnemyTerminalCollisionFactV1 fact) { }
        }

        private static EnemyDefinitionV1 Definition(string name)
        {
            return new EnemyDefinitionV1(
                Id("enemy", name),
                Id("presentation", name),
                20d,
                new EnemyLevelScalingProfileV1(1, 100, 2d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                360d,
                Id("enemy-movement", "mobile-positioning"),
                Id("enemy-decision", "ranged-standard"),
                new[]
                {
                    new EnemyAttackCapabilityDescriptorV1(
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
                        new EnemyProjectileAttackParametersV1(
                            Id("projectile", name), 1, 12d, 16d, 0.15d, 0d, 0),
                        null,
                        null),
                },
                Id("xp", "standard"),
                Id("drop", "standard"),
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
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

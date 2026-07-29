using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyFactoryTests
    {
        [Test]
        public void TenRepeatedDefinitions_DeriveDistinctIndependentActorAndParticipantIdentities()
        {
            EnemyDefinition definition = Definition(
                "mobile",
                "mobile-positioning",
                "ranged-standard",
                "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy,
                false,
                360d,
                120d);
            EnemyFactory factory = Factory(
                new[] { definition },
                new[] { Object("mobile", definition) },
                BuiltInEnemyRules.Create(),
                EnemyLiveDownstreamPorts.None());
            var requests = new List<EnemyPlacementLiveRequest>();
            for (int index = 0; index < 10; index++)
                requests.Add(Request("mobile-" + index, "mobile", 3, 1L, 1d));

            EnemyRoomPlacementSetupResult result = factory.CreateRoom(requests);

            Assert.That(result.IsCreated, Is.True, result.Diagnostic);
            Assert.That(result.Runtimes.Count, Is.EqualTo(10));
            Assert.That(result.Occupants.Count, Is.EqualTo(10));
            var actors = new HashSet<StableId>();
            var participants = new HashSet<StableId>();
            for (int index = 0; index < result.Runtimes.Count; index++)
            {
                EnemyInstance runtime = result.Runtimes[index];
                Assert.That(actors.Add(runtime.SpawnStableId), Is.True);
                Assert.That(participants.Add(runtime.RunParticipantStableId), Is.True);
                Assert.That(runtime.Runtime.ActorState.Health,
                    Is.EqualTo(definition.ResolveHealth(3)).Within(0.000001d));
                Assert.That(runtime.RoomOccupant.EntityStableId, Is.EqualTo(runtime.SpawnStableId));
                Assert.That(runtime.PresentationStableId, Is.EqualTo(definition.PresentationId));
            }
        }

        [Test]
        public void RangedTurretPursuitAndMelee_ExecuteThroughRegisteredCapabilitiesWithoutRuntimeSubclasses()
        {
            EnemyDefinition ranged = Definition(
                "ranged", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            EnemyDefinition turret = Definition(
                "turret", "stationary", "turret-standard", "projectile-area",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            EnemyDefinition pursuit = Definition(
                "pursuit", "pursuit", "contact-standard", "contact",
                EnemyCatalogRoomClearRole.RequiredEnemy, true, 360d, 160d);
            EnemyDefinition melee = Definition(
                "melee", "pursuit", "pounce-standard", "pounce",
                EnemyCatalogRoomClearRole.RequiredEnemy, true, 360d, 160d);
            var ports = new RecordingPorts();
            EnemyFactory factory = Factory(
                new[] { ranged, turret, pursuit, melee },
                new[]
                {
                    Object("ranged", ranged), Object("turret", turret),
                    Object("pursuit", pursuit), Object("melee", melee),
                },
                BuiltInEnemyRules.Create(),
                ports.Bundle);
            string[] objects = { "ranged", "turret", "pursuit", "melee" };
            EnemyAttackExecutionKind[] expected =
            {
                EnemyAttackExecutionKind.Projectile,
                EnemyAttackExecutionKind.Area,
                EnemyAttackExecutionKind.Contact,
                EnemyAttackExecutionKind.Pounce,
            };

            for (int index = 0; index < objects.Length; index++)
            {
                EnemyInstance runtime = factory.Create(
                    Request(objects[index] + "-a", objects[index], 1, 1L, 1d)).Runtime;
                EnemyPerceptionSnapshot perception = Perception(
                    index < 2 ? 3d : 0.5d,
                    20L + index,
                    new EnemyVector2(1d, 0d),
                    true,
                    true);
                EnemyPlacementDecision decision = runtime.Evaluate(perception);
                EnemyAttackExecutionResult attack = runtime.TryExecuteAttack(
                    decision,
                    new EnemyTargetingAimContext(perception, 1d),
                    Id("enemy-operation", "attack-" + index),
                    10d + index);

                Assert.That(runtime.GetType(), Is.EqualTo(typeof(EnemyInstance)));
                Assert.That(attack.IsAccepted, Is.True, attack.Rejection.ToString());
                Assert.That(attack.Request.ExecutionKind, Is.EqualTo(expected[index]));
                Assert.That(attack.Request.ItemInstanceStableId, Is.Not.Null);
                Assert.That(attack.Request.CommittedIntent.CommittedDirection,
                    Is.EqualTo(decision.Evaluation.Decision.RequestedAttack.CommittedDirection));
                Assert.That(attack.Request.CommittedIntent.CommittedTargetPoint,
                    Is.EqualTo(decision.Evaluation.Decision.RequestedAttack.CommittedTargetPoint));
            }
            Assert.That(ports.AttackEffectCount, Is.EqualTo(4));
        }

        [Test]
        public void MovementRealization_ConsumesPolicyIntentThroughRegisteredTypedBoundary()
        {
            EnemyDefinition definition = Definition(
                "mover", "obstacle-ready", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            var recording = new RecordingMovementRealizer();
            EnemyRules policies = CustomPolicies(
                definition,
                new EnemyMovementPolicyRegistration(
                    new EnemyMovementPolicyConfiguration(
                        definition.MovementPolicyId, 4.5d, 12d, 240d, true),
                    new DecisionMovementLivePolicy(),
                    recording),
                new LockedEnemyTargetingAimPolicy(),
                new RequestEnemyAttackCapabilityBridge());
            EnemyFactory factory = Factory(
                new[] { definition },
                new[] { Object("mover", definition) },
                policies,
                EnemyLiveDownstreamPorts.None());
            EnemyInstance runtime = factory.Create(
                Request("mover-a", "mover", 1, 1L, 1.5d)).Runtime;
            EnemyPerceptionSnapshot perception = Perception(
                10d, 11L, new EnemyVector2(1d, 0d), true, true);
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            var callerContext = new EnemyMovementRealizationContext(
                runtime.SpawnStableId,
                runtime.RoomStableId,
                perception.ObserverPosition,
                perception.ObserverFacing,
                perception.SimulationTick,
                99d,
                null);

            EnemyMovementRealization realization = runtime.RealizeMovement(
                decision,
                callerContext);

            Assert.That(decision.Evaluation.Decision.MovementKind,
                Is.EqualTo(EnemyMovementIntentKind.Approach));
            Assert.That(recording.CallCount, Is.EqualTo(1));
            Assert.That(recording.LastIntent.DesiredDirection,
                Is.EqualTo(decision.Evaluation.Decision.DesiredMovement));
            Assert.That(recording.LastContext.EntityInstanceId, Is.EqualTo(runtime.SpawnStableId));
            Assert.That(recording.LastContext.RoomStableId, Is.EqualTo(runtime.RoomStableId));
            Assert.That(recording.LastContext.SpeedScalar,
                Is.EqualTo(runtime.DifficultyScaling.MovementMultiplier));
            Assert.That(recording.LastConfiguration.PolicyId,
                Is.EqualTo(definition.MovementPolicyId));
            Assert.That(realization.PolicyId, Is.EqualTo(definition.MovementPolicyId));
        }

        [Test]
        public void AimAndAttackRegistrations_AreIndependentCooldownAndReplaySafe()
        {
            EnemyDefinition definition = Definition(
                "attacker", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            var aim = new RecordingAimPolicy();
            var capability = new RecordingAttackCapability();
            EnemyRules policies = CustomPolicies(
                definition,
                MovementRegistration(definition),
                aim,
                capability);
            EnemyFactory factory = Factory(
                new[] { definition },
                new[] { Object("attacker", definition) },
                policies,
                EnemyLiveDownstreamPorts.None());
            EnemyInstance runtime = factory.Create(
                Request("attacker-a", "attacker", 1, 1L, 1.4d)).Runtime;
            EnemyPerceptionSnapshot perception = Perception(
                3d, 22L, new EnemyVector2(1d, 0d), true, true);
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            var aimContext = new EnemyTargetingAimContext(perception, 1.4d);
            StableId operation = Id("enemy-operation", "attack-replay");

            EnemyAttackExecutionResult first = runtime.TryExecuteAttack(
                decision, aimContext, operation, 5d);
            EnemyAttackExecutionResult replay = runtime.TryExecuteAttack(
                decision, aimContext, operation, 5d);
            EnemyAttackExecutionResult conflict = runtime.TryExecuteAttack(
                decision, aimContext, operation, 8d);
            EnemyAttackExecutionResult cooldown = runtime.TryExecuteAttack(
                decision,
                aimContext,
                Id("enemy-operation", "attack-cooldown"),
                5.1d);

            Assert.That(first.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
            Assert.That(replay.Request, Is.SameAs(first.Request));
            Assert.That(conflict.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.ConflictingDuplicate));
            Assert.That(cooldown.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.CooldownActive));
            Assert.That(aim.CallCount, Is.EqualTo(1));
            Assert.That(capability.CallCount, Is.EqualTo(1));
            Assert.That(aim.LastContext.DifficultyScalar, Is.EqualTo(1.4d));
            Assert.That(capability.LastContext.LifecycleGeneration,
                Is.EqualTo(runtime.LifecycleGeneration));
            Assert.That(first.Request.ResolvedDamage,
                Is.EqualTo(definition.Attacks[0].Damage
                    * runtime.DifficultyScaling.DamageMultiplier).Within(0.000001d));
        }

        [Test]
        public void VisionAndAttackArcs_RemainIndependent()
        {
            EnemyDefinition definition = Definition(
                "arc", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 60d);
            EnemyFactory factory = Factory(
                new[] { definition },
                new[] { Object("arc", definition) },
                BuiltInEnemyRules.Create(),
                EnemyLiveDownstreamPorts.None());
            EnemyInstance runtime = factory.Create(
                Request("arc-a", "arc", 1, 1L, 1d)).Runtime;
            EnemyPerceptionSnapshot perception = Perception(
                3d,
                30L,
                new EnemyVector2(0d, 1d),
                true,
                true);

            EnemyPlacementDecision decision = runtime.Evaluate(perception);

            Assert.That(definition.VisionArcDegrees, Is.EqualTo(360d));
            Assert.That(definition.Attacks[0].AttackArcDegrees, Is.EqualTo(60d));
            Assert.That(decision.Evaluation.Debug.SelectedTargetWithinVisionArc, Is.True);
            Assert.That(decision.Evaluation.Debug.SelectedTargetWithinAttackArc, Is.False);
            Assert.That(decision.Evaluation.Decision.RequestedAttack, Is.Null);
        }

        [Test]
        public void MissingCapabilityRegistration_RejectsRoomAtomically()
        {
            EnemyDefinition valid = Definition(
                "valid", "pursuit", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            EnemyDefinition invalid = Definition(
                "invalid", "pursuit", "ranged-standard", "unregistered-capability",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            EnemyFactory factory = Factory(
                new[] { valid, invalid },
                new[] { Object("valid", valid), Object("invalid", invalid) },
                CustomPolicies(
                    valid,
                    MovementRegistration(valid),
                    new LockedEnemyTargetingAimPolicy(),
                    new RequestEnemyAttackCapabilityBridge()),
                EnemyLiveDownstreamPorts.None());

            EnemyRoomPlacementSetupResult result = factory.CreateRoom(new[]
            {
                Request("valid-a", "valid", 1, 1L, 1d),
                Request("invalid-a", "invalid", 1, 1L, 1d),
            });

            Assert.That(result.IsCreated, Is.False);
            Assert.That(result.Rejection,
                Is.EqualTo(EnemyFactoryRejection.AttackCapabilityNotRegistered));
            Assert.That(result.Runtimes, Is.Empty);
            Assert.That(result.Occupants, Is.Empty);
        }

        [Test]
        public void LethalDamage_EmitsAttributedTerminalFactsAndDownstreamConsumersOnce()
        {
            EnemyDefinition required = Definition(
                "required", "pursuit", "contact-standard", "contact",
                EnemyCatalogRoomClearRole.RequiredEnemy, true, 360d, 160d);
            EnemyDefinition optional = Definition(
                "optional", "pursuit", "contact-standard", "contact",
                EnemyCatalogRoomClearRole.OptionalEnemy, true, 360d, 160d);
            var ports = new RecordingPorts();
            EnemyFactory factory = Factory(
                new[] { required, optional },
                new[] { Object("required", required), Object("optional", optional) },
                BuiltInEnemyRules.Create(),
                ports.Bundle);
            EnemyInstance runtime = factory.Create(
                Request("required-a", "required", 2, 1L, 1d)).Runtime;
            EnemyInstance optionalRuntime = factory.Create(
                Request("optional-a", "optional", 2, 1L, 1d)).Runtime;
            StableId operation = Id("enemy-damage", "lethal-one");
            var command = new EnemyLiveDamageCommand(
                operation,
                Id("entity", "player-one"),
                Id("run-participant", "player-one"),
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                0L,
                1,
                10000d);

            EnemyLiveDamageResult first = runtime.ApplyDamage(command);
            EnemyLiveDamageResult replay = runtime.ApplyDamage(command);
            EnemyLiveDamageResult conflict = runtime.ApplyDamage(
                new EnemyLiveDamageCommand(
                    operation,
                    command.SourceEntityStableId,
                    command.SourceRunParticipantStableId,
                    command.TargetEntityStableId,
                    command.TargetLifecycleGeneration,
                    0L,
                    1,
                    9999d));

            Assert.That(first.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));
            Assert.That(first.DeathFact, Is.Not.Null);
            Assert.That(first.DeathFact.KillerEntityStableId, Is.EqualTo(command.SourceEntityStableId));
            Assert.That(first.DeathFact.KillerRunParticipantStableId,
                Is.EqualTo(command.SourceRunParticipantStableId));
            Assert.That(first.DeathFact.ExperienceProfileStableId,
                Is.EqualTo(required.ExperienceProfileId));
            Assert.That(first.DeathFact.DropProfileStableId, Is.EqualTo(required.DropProfileId));
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
            Assert.That(conflict.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.ConflictingDuplicate));
            Assert.That(ports.RoomCount, Is.EqualTo(1));
            Assert.That(ports.ExperienceCount, Is.EqualTo(1));
            Assert.That(ports.DropCount, Is.EqualTo(1));
            Assert.That(ports.KillCount, Is.EqualTo(1));
            Assert.That(ports.TerminalCollisionCount, Is.EqualTo(1));
            Assert.That(ports.LastRoomCommand.OccupantEntityStableId,
                Is.EqualTo(runtime.SpawnStableId));
            Assert.That(runtime.Runtime.BlocksRoomClear, Is.False);
            Assert.That(runtime.RoomOccupant.BlocksRoomClear, Is.True);
            Assert.That(optionalRuntime.RoomOccupant.BlocksRoomClear, Is.False);
        }

        [Test]
        public void Restart_PreservesDerivedIdentityRestoresStateAndRejectsStaleIntentAndProjectile()
        {
            EnemyDefinition definition = Definition(
                "restart", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            var ports = new RecordingPorts();
            EnemyFactory factory = Factory(
                new[] { definition },
                new[] { Object("restart", definition) },
                BuiltInEnemyRules.Create(),
                ports.Bundle);
            EnemyInstance first = factory.Create(
                Request("restart-a", "restart", 4, 1L, 1d)).Runtime;
            EnemyPerceptionSnapshot firstPerception = Perception(
                3d, 40L, new EnemyVector2(1d, 0d), true, true);
            EnemyPlacementDecision staleDecision = first.Evaluate(firstPerception);
            EnemyAttackExecutionResult oldAttack = first.TryExecuteAttack(
                staleDecision,
                new EnemyTargetingAimContext(firstPerception, 1d),
                Id("enemy-operation", "old-attack"),
                1d);
            Assert.That(oldAttack.IsAccepted, Is.True);
            EnemyPlayerDamagePortResult oldImpact = first.RoutePlayerImpact(
                oldAttack.Request,
                Id("enemy-hit", "old-hit"),
                Id("entity", "player-one"));
            Assert.That(oldImpact.Status, Is.EqualTo(EnemyLiveOperationStatus.Applied));

            first.ApplyDamage(new EnemyLiveDamageCommand(
                Id("enemy-damage", "damage-before-restart"),
                Id("entity", "player-one"),
                Id("run-participant", "player-one"),
                first.SpawnStableId,
                first.LifecycleGeneration,
                0L,
                1,
                5d));
            Assert.That(first.ActorState.Health, Is.LessThan(first.ActorState.MaximumHealth));

            EnemyInstance restarted = factory.Create(
                Request("restart-a", "restart", 4, 2L, 1d)).Runtime;
            EnemyAttackExecutionResult staleAttack = restarted.TryExecuteAttack(
                staleDecision,
                new EnemyTargetingAimContext(firstPerception, 1d),
                Id("enemy-operation", "stale-decision"),
                2d);
            EnemyPlayerDamagePortResult staleImpact = restarted.RoutePlayerImpact(
                oldAttack.Request,
                Id("enemy-hit", "stale-hit"),
                Id("entity", "player-one"));

            Assert.That(restarted.SpawnStableId, Is.EqualTo(first.SpawnStableId));
            Assert.That(restarted.RunParticipantStableId,
                Is.EqualTo(first.RunParticipantStableId));
            Assert.That(restarted.LifecycleGeneration, Is.EqualTo(2L));
            Assert.That(restarted.LifecycleStableId, Is.Not.EqualTo(first.LifecycleStableId));
            Assert.That(restarted.ActorState.Health,
                Is.EqualTo(restarted.ActorState.MaximumHealth));
            Assert.That(staleAttack.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.StaleLifecycle));
            Assert.That(staleImpact.Rejection,
                Is.EqualTo(EnemyLiveRejectionCode.StaleLifecycle));
            Assert.That(ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void DifficultyScaling_IsTypedAndAppliedWithoutChangingEnemyDefinition()
        {
            EnemyDefinition definition = Definition(
                "difficulty", "pursuit", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRole.RequiredEnemy, false, 360d, 120d);
            EnemyFactory factory = Factory(
                new[] { definition },
                new[] { Object("difficulty", definition) },
                BuiltInEnemyRules.Create(),
                EnemyLiveDownstreamPorts.None());
            EnemyInstance normal = factory.Create(
                Request("difficulty-normal", "difficulty", 5, 1L, 1d)).Runtime;
            EnemyInstance hard = factory.Create(
                Request("difficulty-hard", "difficulty", 5, 1L, 1.5d)).Runtime;

            Assert.That(hard.ActorState.MaximumHealth,
                Is.GreaterThan(normal.ActorState.MaximumHealth));
            Assert.That(hard.DifficultyScaling.DamageMultiplier, Is.GreaterThan(1d));
            Assert.That(hard.DifficultyScaling.CooldownMultiplier, Is.LessThan(1d));
            Assert.That(definition.ResolveHealth(5),
                Is.EqualTo(normal.ActorState.MaximumHealth).Within(0.000001d));
        }

        [Test]
        public void MultiAttackDecision_SelectsRegisteredCapabilityByTypedRangeFacts()
        {
            EnemyDefinition hybrid = HybridDefinition();
            EnemyFactory factory = Factory(
                new[] { hybrid },
                new[] { Object("hybrid", hybrid) },
                BuiltInEnemyRules.Create(),
                EnemyLiveDownstreamPorts.None());
            EnemyInstance runtime = factory.Create(
                Request("hybrid-a", "hybrid", 1, 1L, 1d)).Runtime;

            EnemyPlacementDecision contact = runtime.Evaluate(
                Perception(0.5d, 51L, new EnemyVector2(1d, 0d), true, true));
            EnemyPlacementDecision ranged = runtime.Evaluate(
                Perception(5d, 52L, new EnemyVector2(1d, 0d), true, true));

            Assert.That(contact.Evaluation.Decision.RequestedAttack.AttackId,
                Is.EqualTo(Id("enemy-attack-profile", "hybrid-contact")));
            Assert.That(ranged.Evaluation.Decision.RequestedAttack.AttackId,
                Is.EqualTo(Id("enemy-attack-profile", "hybrid-ranged")));
        }

        private static EnemyFactory Factory(
            IEnumerable<EnemyDefinition> definitions,
            IEnumerable<RoomContentObjectDefinition> objects,
            EnemyRules policies,
            EnemyLiveDownstreamPorts ports)
        {
            return new EnemyFactory(
                new RoomContentObjectCatalog(objects),
                new EnemyCatalog(1, Id("enemy-catalog", "factory-fixture-v1"), definitions),
                policies,
                new DeterministicEnemyLiveIdentityDeriver(),
                new EnemyDifficultyLiveRegistration(
                    new EnemyDifficultyScalingConfiguration(
                        Id("enemy-difficulty", "test-scalar"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicy()),
                new EnemyPerceptionLiveRegistration(
                    new EnemyPerceptionPolicyConfiguration(
                        Id("enemy-perception", "test-validated"),
                        false),
                    new ValidatedEnemyPerceptionLiveBridge()),
                ports);
        }

        private static EnemyRules CustomPolicies(
            EnemyDefinition definition,
            EnemyMovementPolicyRegistration movement,
            IEnemyTargetingAimPolicy aim,
            IEnemyAttackCapabilityBridge attack)
        {
            StableId aimId = Id("enemy-aim", "fixture-locked");
            return new EnemyRules(
                new[] { movement },
                new[]
                {
                    new EnemyDecisionPolicyRegistration(
                        new EnemyDecisionPolicyConfiguration(
                            definition.DecisionPolicyId,
                            Id("enemy-phase", "ready"),
                            false,
                            0d,
                            0d),
                        new FoundationEnemyDecisionLivePolicy()),
                },
                new[]
                {
                    new EnemyTargetingAimPolicyRegistration(
                        new EnemyTargetingAimPolicyConfiguration(
                            aimId,
                            EnemyAimCommitmentMode.LockedDirectionAndPoint,
                            0d,
                            0d),
                        aim),
                },
                new[]
                {
                    new EnemyAttackCapabilityLiveRegistration(
                        new EnemyAttackCapabilityConfiguration(
                            definition.Attacks[0].CapabilityId,
                            aimId,
                            ExpectedExecutionKind(definition.Attacks[0])),
                        attack),
                });
        }

        private static EnemyMovementPolicyRegistration MovementRegistration(
            EnemyDefinition definition)
        {
            return new EnemyMovementPolicyRegistration(
                new EnemyMovementPolicyConfiguration(
                    definition.MovementPolicyId,
                    definition.MovementPolicyId == Id("enemy-movement", "stationary") ? 0d : 4d,
                    10d,
                    240d,
                    true),
                new DecisionMovementLivePolicy(),
                new DirectEnemyMovementIntentRealizer());
        }

        private static EnemyAttackExecutionKind ExpectedExecutionKind(
            EnemyAttackCapabilityDescriptor attack)
        {
            if (attack.Melee != null)
            {
                return attack.Melee.PounceDistance > 0d
                    ? EnemyAttackExecutionKind.Pounce
                    : EnemyAttackExecutionKind.Contact;
            }
            return attack.Area != null
                ? EnemyAttackExecutionKind.Area
                : EnemyAttackExecutionKind.Projectile;
        }

        private static EnemyDefinition HybridDefinition()
        {
            var ranged = new EnemyAttackCapabilityDescriptor(
                Id("enemy-attack-profile", "hybrid-ranged"),
                Id("enemy-attack", "ranged-projectile"),
                20,
                90d,
                3d,
                7d,
                11d,
                1.4d,
                4d,
                Id("damage", "kinetic"),
                new EnemyProjectileAttackParameters(
                    Id("projectile", "hybrid"), 1, 13d, 14d, 0.15d, 0d, 0),
                null,
                null);
            var contact = new EnemyAttackCapabilityDescriptor(
                Id("enemy-attack-profile", "hybrid-contact"),
                Id("enemy-attack", "contact"),
                10,
                140d,
                0d,
                0.4d,
                0.75d,
                0.8d,
                3d,
                Id("damage", "impact"),
                null,
                null,
                new EnemyMeleeAttackParameters(0.8d, 0d, 0d, 0d));
            return new EnemyDefinition(
                Id("enemy", "hybrid"),
                Id("presentation", "enemy-hybrid"),
                36d,
                new EnemyLevelScalingProfile(1, 100, 2.2d, 1.01d),
                Id("faction", "hostile-machines"),
                18d,
                270d,
                Id("enemy-movement", "pursuit"),
                Id("enemy-decision", "multi-attack-standard"),
                new[] { ranged, contact },
                Id("xp", "enemy-standard"),
                Id("drop", "enemy-common"),
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyDefinition Definition(
            string name,
            string movement,
            string decision,
            string capability,
            EnemyCatalogRoomClearRole role,
            bool melee,
            double visionArc,
            double attackArc)
        {
            StableId capabilityId = Id("enemy-attack", capability);
            bool pounce = capability == "pounce";
            bool contact = capability == "contact" || pounce;
            EnemyMeleeAttackParameters meleeParameters = contact
                ? new EnemyMeleeAttackParameters(0.8d, pounce ? 6d : 0d, 0d, 0d)
                : null;
            EnemyProjectileAttackParameters projectileParameters = contact
                ? null
                : new EnemyProjectileAttackParameters(
                    Id("projectile", name), 1, 12d, 16d, 0.15d, 0d, 0);
            EnemyAreaAttackParameters areaParameters = capability == "projectile-area"
                ? new EnemyAreaAttackParameters(1.5d, 0d, 8)
                : null;
            double preferredRange = contact ? 0.4d : 3d;
            double maximumRange = contact ? 0.8d : 6d;
            return new EnemyDefinition(
                Id("enemy", name),
                Id("presentation", "enemy-" + name),
                20d,
                new EnemyLevelScalingProfile(1, 100, 2d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                visionArc,
                Id("enemy-movement", movement),
                Id("enemy-decision", decision),
                new[]
                {
                    new EnemyAttackCapabilityDescriptor(
                        Id("enemy-attack-profile", name + "-primary"),
                        capabilityId,
                        10,
                        attackArc,
                        0d,
                        preferredRange,
                        maximumRange,
                        1d,
                        4d,
                        Id("damage", contact ? "impact" : "kinetic"),
                        projectileParameters,
                        areaParameters,
                        meleeParameters),
                },
                Id("xp", "enemy-standard"),
                Id("drop", "enemy-common"),
                role,
                Array.Empty<StableId>());
        }

        private static RoomContentObjectDefinition Object(
            string name,
            EnemyDefinition definition)
        {
            return new RoomContentObjectDefinition(
                Id("room-object", name),
                RoomContentObjectKind.Enemy,
                definition.DefinitionId,
                definition.PresentationId);
        }

        private static EnemyPlacementLiveRequest Request(
            string placement,
            string roomObject,
            int level,
            long generation,
            double difficultyScalar)
        {
            return new EnemyPlacementLiveRequest(
                Placement(placement, roomObject, level),
                Id("run", "fixture-run"),
                Id("room-runtime", "fixture-room-runtime"),
                null,
                generation,
                generation,
                new EnemyDifficultyContext(
                    Id("difficulty", "fixture"),
                    difficultyScalar));
        }

        private static RoomEnemyPlacementContent Placement(
            string placement,
            string roomObject,
            int level)
        {
            return new RoomEnemyPlacementContent(
                Id("enemy-placement", placement),
                Id("room", "fixture"),
                Id("room-object", roomObject),
                level,
                new RoomVector2(0d, 0d),
                0d,
                placement);
        }

        private static EnemyPerceptionSnapshot Perception(
            double distance,
            long tick,
            EnemyVector2 direction,
            bool lineOfSight,
            bool withinVisionArc)
        {
            EnemyVector2 normalized = direction.Normalized;
            return new EnemyPerceptionSnapshot(
                new EnemyVector2(0d, 0d),
                new EnemyVector2(1d, 0d),
                new[]
                {
                    new EnemyPerceivedTarget(
                        Id("entity", "player"),
                        Id("faction", "players"),
                        EnemyTargetRelationship.Hostile,
                        new EnemyVector2(normalized.X * distance, normalized.Y * distance),
                        new EnemyVector2(0d, 0d),
                        distance,
                        normalized,
                        lineOfSight,
                        true,
                        withinVisionArc),
                },
                tick);
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }

        private sealed class RecordingMovementRealizer : IEnemyMovementIntentRealizer
        {
            public int CallCount { get; private set; }
            public EnemyMovementPolicyIntent LastIntent { get; private set; }
            public EnemyMovementRealizationContext LastContext { get; private set; }
            public EnemyMovementPolicyConfiguration LastConfiguration { get; private set; }

            public EnemyMovementRealization Realize(
                EnemyMovementPolicyIntent intent,
                EnemyMovementRealizationContext context,
                EnemyMovementPolicyConfiguration configuration)
            {
                CallCount++;
                LastIntent = intent;
                LastContext = context;
                LastConfiguration = configuration;
                return new EnemyMovementRealization(
                    new EnemyVector2(7d, 0d),
                    intent.DesiredFacing,
                    intent.Kind,
                    configuration.PolicyId);
            }
        }

        private sealed class RecordingAimPolicy : IEnemyTargetingAimPolicy
        {
            public int CallCount { get; private set; }
            public EnemyTargetingAimContext LastContext { get; private set; }

            public EnemyAttackIntent Commit(
                EnemyAttackIntent requestedIntent,
                EnemyTargetingAimContext context,
                EnemyTargetingAimPolicyConfiguration configuration)
            {
                CallCount++;
                LastContext = context;
                return requestedIntent;
            }
        }

        private sealed class RecordingAttackCapability : IEnemyAttackCapabilityBridge
        {
            private readonly RequestEnemyAttackCapabilityBridge inner =
                new RequestEnemyAttackCapabilityBridge();

            public int CallCount { get; private set; }
            public EnemyAttackExecutionContext LastContext { get; private set; }

            public EnemyAttackExecutionRequest BuildExecution(
                EnemyAttackCapabilityDescriptor descriptor,
                EnemyAttackIntent committedIntent,
                StableId itemInstanceStableId,
                EnemyAttackCapabilityConfiguration configuration,
                EnemyAttackExecutionContext context)
            {
                CallCount++;
                LastContext = context;
                return inner.BuildExecution(
                    descriptor,
                    committedIntent,
                    itemInstanceStableId,
                    configuration,
                    context);
            }
        }

        private sealed class RecordingPorts :
            IEnemyAttackEffectPort,
            IEnemyPlayerDamagePort,
            IEnemyRoomTerminalPort,
            IEnemyExperienceFactConsumer,
            IEnemyDropFactConsumer,
            IEnemyKillStatFactConsumer,
            IEnemyTerminalCollisionBridge
        {
            public RecordingPorts()
            {
                Bundle = new EnemyLiveDownstreamPorts(
                    this, this, this, this, this, this, this);
            }

            public EnemyLiveDownstreamPorts Bundle { get; }
            public int AttackEffectCount { get; private set; }
            public int PlayerDamageCount { get; private set; }
            public int RoomCount { get; private set; }
            public int ExperienceCount { get; private set; }
            public int DropCount { get; private set; }
            public int KillCount { get; private set; }
            public int TerminalCollisionCount { get; private set; }
            public ReportRoomOccupantTerminalCommand LastRoomCommand { get; private set; }

            public void Emit(EnemyAttackExecutionRequest request)
            {
                AttackEffectCount++;
            }

            public EnemyPlayerDamagePortResult Route(EnemyPlayerDamageRequest request)
            {
                PlayerDamageCount++;
                return new EnemyPlayerDamagePortResult(
                    EnemyLiveOperationStatus.Applied,
                    EnemyLiveRejectionCode.None);
            }

            public void Report(
                ReportRoomOccupantTerminalCommand command,
                EnemyDeathFact deathFact)
            {
                RoomCount++;
                LastRoomCommand = command;
            }

            void IEnemyExperienceFactConsumer.Consume(EnemyDeathFact fact)
            {
                ExperienceCount++;
            }

            void IEnemyDropFactConsumer.Consume(EnemyDeathFact fact)
            {
                DropCount++;
            }

            void IEnemyKillStatFactConsumer.Consume(EnemyDeathFact fact)
            {
                KillCount++;
            }

            public void SetTerminal(EnemyTerminalCollisionFact fact)
            {
                TerminalCollisionCount++;
            }
        }
    }
}

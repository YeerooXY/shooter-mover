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
    public sealed class EnemyPlacementRuntimeFactoryV1Tests
    {
        [Test]
        public void TenRepeatedDefinitions_DeriveDistinctIndependentActorAndParticipantIdentities()
        {
            EnemyDefinitionV1 definition = Definition(
                "mobile",
                "mobile-positioning",
                "ranged-standard",
                "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                false,
                360d,
                120d);
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { definition },
                new[] { Object("mobile", definition) },
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                EnemyRuntimeDownstreamPortsV1.None());
            var requests = new List<EnemyPlacementRuntimeRequestV1>();
            for (int index = 0; index < 10; index++)
                requests.Add(Request("mobile-" + index, "mobile", 3, 1L, 1d));

            EnemyRoomPlacementCompositionResultV1 result = factory.CreateRoom(requests);

            Assert.That(result.IsCreated, Is.True, result.Diagnostic);
            Assert.That(result.Runtimes.Count, Is.EqualTo(10));
            Assert.That(result.Occupants.Count, Is.EqualTo(10));
            var actors = new HashSet<StableId>();
            var participants = new HashSet<StableId>();
            for (int index = 0; index < result.Runtimes.Count; index++)
            {
                EnemyPlacementRuntimeInstanceV1 runtime = result.Runtimes[index];
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
            EnemyDefinitionV1 ranged = Definition(
                "ranged", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            EnemyDefinitionV1 turret = Definition(
                "turret", "stationary", "turret-standard", "projectile-area",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            EnemyDefinitionV1 pursuit = Definition(
                "pursuit", "pursuit", "contact-standard", "contact",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, true, 360d, 160d);
            EnemyDefinitionV1 melee = Definition(
                "melee", "pursuit", "pounce-standard", "pounce",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, true, 360d, 160d);
            var ports = new RecordingPorts();
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { ranged, turret, pursuit, melee },
                new[]
                {
                    Object("ranged", ranged), Object("turret", turret),
                    Object("pursuit", pursuit), Object("melee", melee),
                },
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                ports.Bundle);
            string[] objects = { "ranged", "turret", "pursuit", "melee" };
            EnemyAttackExecutionKindV1[] expected =
            {
                EnemyAttackExecutionKindV1.Projectile,
                EnemyAttackExecutionKindV1.Area,
                EnemyAttackExecutionKindV1.Contact,
                EnemyAttackExecutionKindV1.Pounce,
            };

            for (int index = 0; index < objects.Length; index++)
            {
                EnemyPlacementRuntimeInstanceV1 runtime = factory.Create(
                    Request(objects[index] + "-a", objects[index], 1, 1L, 1d)).Runtime;
                EnemyPerceptionSnapshot perception = Perception(
                    index < 2 ? 3d : 0.5d,
                    20L + index,
                    new EnemyVector2(1d, 0d),
                    true,
                    true);
                EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
                EnemyAttackExecutionResultV1 attack = runtime.TryExecuteAttack(
                    decision,
                    new EnemyTargetingAimContextV1(perception, 1d),
                    Id("enemy-operation", "attack-" + index),
                    10d + index);

                Assert.That(runtime.GetType(), Is.EqualTo(typeof(EnemyPlacementRuntimeInstanceV1)));
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
            EnemyDefinitionV1 definition = Definition(
                "mover", "obstacle-ready", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            var recording = new RecordingMovementRealizer();
            EnemyRuntimePolicyRegistryV1 policies = CustomPolicies(
                definition,
                new EnemyMovementPolicyRegistrationV1(
                    new EnemyMovementPolicyConfigurationV1(
                        definition.MovementPolicyId, 4.5d, 12d, 240d, true),
                    new DecisionMovementRuntimePolicyV1(),
                    recording),
                new LockedEnemyTargetingAimPolicyV1(),
                new RequestEnemyAttackCapabilityAdapterV1());
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { definition },
                new[] { Object("mover", definition) },
                policies,
                EnemyRuntimeDownstreamPortsV1.None());
            EnemyPlacementRuntimeInstanceV1 runtime = factory.Create(
                Request("mover-a", "mover", 1, 1L, 1.5d)).Runtime;
            EnemyPerceptionSnapshot perception = Perception(
                10d, 11L, new EnemyVector2(1d, 0d), true, true);
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            var callerContext = new EnemyMovementRealizationContextV1(
                runtime.SpawnStableId,
                runtime.RoomStableId,
                perception.ObserverPosition,
                perception.ObserverFacing,
                perception.SimulationTick,
                99d,
                null);

            EnemyMovementRealizationV1 realization = runtime.RealizeMovement(
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
            EnemyDefinitionV1 definition = Definition(
                "attacker", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            var aim = new RecordingAimPolicy();
            var capability = new RecordingAttackCapability();
            EnemyRuntimePolicyRegistryV1 policies = CustomPolicies(
                definition,
                MovementRegistration(definition),
                aim,
                capability);
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { definition },
                new[] { Object("attacker", definition) },
                policies,
                EnemyRuntimeDownstreamPortsV1.None());
            EnemyPlacementRuntimeInstanceV1 runtime = factory.Create(
                Request("attacker-a", "attacker", 1, 1L, 1.4d)).Runtime;
            EnemyPerceptionSnapshot perception = Perception(
                3d, 22L, new EnemyVector2(1d, 0d), true, true);
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            var aimContext = new EnemyTargetingAimContextV1(perception, 1.4d);
            StableId operation = Id("enemy-operation", "attack-replay");

            EnemyAttackExecutionResultV1 first = runtime.TryExecuteAttack(
                decision, aimContext, operation, 5d);
            EnemyAttackExecutionResultV1 replay = runtime.TryExecuteAttack(
                decision, aimContext, operation, 5d);
            EnemyAttackExecutionResultV1 conflict = runtime.TryExecuteAttack(
                decision, aimContext, operation, 8d);
            EnemyAttackExecutionResultV1 cooldown = runtime.TryExecuteAttack(
                decision,
                aimContext,
                Id("enemy-operation", "attack-cooldown"),
                5.1d);

            Assert.That(first.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
            Assert.That(replay.Request, Is.SameAs(first.Request));
            Assert.That(conflict.Rejection,
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate));
            Assert.That(cooldown.Rejection,
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.CooldownActive));
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
            EnemyDefinitionV1 definition = Definition(
                "arc", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 60d);
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { definition },
                new[] { Object("arc", definition) },
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                EnemyRuntimeDownstreamPortsV1.None());
            EnemyPlacementRuntimeInstanceV1 runtime = factory.Create(
                Request("arc-a", "arc", 1, 1L, 1d)).Runtime;
            EnemyPerceptionSnapshot perception = Perception(
                3d,
                30L,
                new EnemyVector2(0d, 1d),
                true,
                true);

            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);

            Assert.That(definition.VisionArcDegrees, Is.EqualTo(360d));
            Assert.That(definition.Attacks[0].AttackArcDegrees, Is.EqualTo(60d));
            Assert.That(decision.Evaluation.Debug.SelectedTargetWithinVisionArc, Is.True);
            Assert.That(decision.Evaluation.Debug.SelectedTargetWithinAttackArc, Is.False);
            Assert.That(decision.Evaluation.Decision.RequestedAttack, Is.Null);
        }

        [Test]
        public void MissingCapabilityRegistration_RejectsRoomAtomically()
        {
            EnemyDefinitionV1 valid = Definition(
                "valid", "pursuit", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            EnemyDefinitionV1 invalid = Definition(
                "invalid", "pursuit", "ranged-standard", "unregistered-capability",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { valid, invalid },
                new[] { Object("valid", valid), Object("invalid", invalid) },
                CustomPolicies(
                    valid,
                    MovementRegistration(valid),
                    new LockedEnemyTargetingAimPolicyV1(),
                    new RequestEnemyAttackCapabilityAdapterV1()),
                EnemyRuntimeDownstreamPortsV1.None());

            EnemyRoomPlacementCompositionResultV1 result = factory.CreateRoom(new[]
            {
                Request("valid-a", "valid", 1, 1L, 1d),
                Request("invalid-a", "invalid", 1, 1L, 1d),
            });

            Assert.That(result.IsCreated, Is.False);
            Assert.That(result.Rejection,
                Is.EqualTo(EnemyPlacementRuntimeFactoryRejectionV1.AttackCapabilityNotRegistered));
            Assert.That(result.Runtimes, Is.Empty);
            Assert.That(result.Occupants, Is.Empty);
        }

        [Test]
        public void LethalDamage_EmitsAttributedTerminalFactsAndDownstreamConsumersOnce()
        {
            EnemyDefinitionV1 required = Definition(
                "required", "pursuit", "contact-standard", "contact",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, true, 360d, 160d);
            EnemyDefinitionV1 optional = Definition(
                "optional", "pursuit", "contact-standard", "contact",
                EnemyCatalogRoomClearRoleV1.OptionalEnemy, true, 360d, 160d);
            var ports = new RecordingPorts();
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { required, optional },
                new[] { Object("required", required), Object("optional", optional) },
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                ports.Bundle);
            EnemyPlacementRuntimeInstanceV1 runtime = factory.Create(
                Request("required-a", "required", 2, 1L, 1d)).Runtime;
            EnemyPlacementRuntimeInstanceV1 optionalRuntime = factory.Create(
                Request("optional-a", "optional", 2, 1L, 1d)).Runtime;
            StableId operation = Id("enemy-damage", "lethal-one");
            var command = new EnemyRuntimeDamageCommandV1(
                operation,
                Id("entity", "player-one"),
                Id("run-participant", "player-one"),
                runtime.SpawnStableId,
                runtime.LifecycleGeneration,
                0L,
                1,
                10000d);

            EnemyRuntimeDamageResultV1 first = runtime.ApplyDamage(command);
            EnemyRuntimeDamageResultV1 replay = runtime.ApplyDamage(command);
            EnemyRuntimeDamageResultV1 conflict = runtime.ApplyDamage(
                new EnemyRuntimeDamageCommandV1(
                    operation,
                    command.SourceEntityStableId,
                    command.SourceRunParticipantStableId,
                    command.TargetEntityStableId,
                    command.TargetLifecycleGeneration,
                    0L,
                    1,
                    9999d));

            Assert.That(first.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));
            Assert.That(first.DeathFact, Is.Not.Null);
            Assert.That(first.DeathFact.KillerEntityStableId, Is.EqualTo(command.SourceEntityStableId));
            Assert.That(first.DeathFact.KillerRunParticipantStableId,
                Is.EqualTo(command.SourceRunParticipantStableId));
            Assert.That(first.DeathFact.ExperienceProfileStableId,
                Is.EqualTo(required.ExperienceProfileId));
            Assert.That(first.DeathFact.DropProfileStableId, Is.EqualTo(required.DropProfileId));
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
            Assert.That(conflict.Rejection,
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.ConflictingDuplicate));
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
            EnemyDefinitionV1 definition = Definition(
                "restart", "mobile-positioning", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            var ports = new RecordingPorts();
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { definition },
                new[] { Object("restart", definition) },
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                ports.Bundle);
            EnemyPlacementRuntimeInstanceV1 first = factory.Create(
                Request("restart-a", "restart", 4, 1L, 1d)).Runtime;
            EnemyPerceptionSnapshot firstPerception = Perception(
                3d, 40L, new EnemyVector2(1d, 0d), true, true);
            EnemyPlacementDecisionV1 staleDecision = first.Evaluate(firstPerception);
            EnemyAttackExecutionResultV1 oldAttack = first.TryExecuteAttack(
                staleDecision,
                new EnemyTargetingAimContextV1(firstPerception, 1d),
                Id("enemy-operation", "old-attack"),
                1d);
            Assert.That(oldAttack.IsAccepted, Is.True);
            EnemyPlayerDamagePortResultV1 oldImpact = first.RoutePlayerImpact(
                oldAttack.Request,
                Id("enemy-hit", "old-hit"),
                Id("entity", "player-one"));
            Assert.That(oldImpact.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.Applied));

            first.ApplyDamage(new EnemyRuntimeDamageCommandV1(
                Id("enemy-damage", "damage-before-restart"),
                Id("entity", "player-one"),
                Id("run-participant", "player-one"),
                first.SpawnStableId,
                first.LifecycleGeneration,
                0L,
                1,
                5d));
            Assert.That(first.ActorState.Health, Is.LessThan(first.ActorState.MaximumHealth));

            EnemyPlacementRuntimeInstanceV1 restarted = factory.Create(
                Request("restart-a", "restart", 4, 2L, 1d)).Runtime;
            EnemyAttackExecutionResultV1 staleAttack = restarted.TryExecuteAttack(
                staleDecision,
                new EnemyTargetingAimContextV1(firstPerception, 1d),
                Id("enemy-operation", "stale-decision"),
                2d);
            EnemyPlayerDamagePortResultV1 staleImpact = restarted.RoutePlayerImpact(
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
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.StaleLifecycle));
            Assert.That(staleImpact.Rejection,
                Is.EqualTo(EnemyRuntimeRejectionCodeV1.StaleLifecycle));
            Assert.That(ports.PlayerDamageCount, Is.EqualTo(1));
        }

        [Test]
        public void DifficultyScaling_IsTypedAndAppliedWithoutChangingEnemyDefinition()
        {
            EnemyDefinitionV1 definition = Definition(
                "difficulty", "pursuit", "ranged-standard", "ranged-projectile",
                EnemyCatalogRoomClearRoleV1.RequiredEnemy, false, 360d, 120d);
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { definition },
                new[] { Object("difficulty", definition) },
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                EnemyRuntimeDownstreamPortsV1.None());
            EnemyPlacementRuntimeInstanceV1 normal = factory.Create(
                Request("difficulty-normal", "difficulty", 5, 1L, 1d)).Runtime;
            EnemyPlacementRuntimeInstanceV1 hard = factory.Create(
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
            EnemyDefinitionV1 hybrid = HybridDefinition();
            EnemyPlacementRuntimeFactoryV1 factory = Factory(
                new[] { hybrid },
                new[] { Object("hybrid", hybrid) },
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                EnemyRuntimeDownstreamPortsV1.None());
            EnemyPlacementRuntimeInstanceV1 runtime = factory.Create(
                Request("hybrid-a", "hybrid", 1, 1L, 1d)).Runtime;

            EnemyPlacementDecisionV1 contact = runtime.Evaluate(
                Perception(0.5d, 51L, new EnemyVector2(1d, 0d), true, true));
            EnemyPlacementDecisionV1 ranged = runtime.Evaluate(
                Perception(5d, 52L, new EnemyVector2(1d, 0d), true, true));

            Assert.That(contact.Evaluation.Decision.RequestedAttack.AttackId,
                Is.EqualTo(Id("enemy-attack-profile", "hybrid-contact")));
            Assert.That(ranged.Evaluation.Decision.RequestedAttack.AttackId,
                Is.EqualTo(Id("enemy-attack-profile", "hybrid-ranged")));
        }

        private static EnemyPlacementRuntimeFactoryV1 Factory(
            IEnumerable<EnemyDefinitionV1> definitions,
            IEnumerable<RoomContentObjectDefinitionV1> objects,
            EnemyRuntimePolicyRegistryV1 policies,
            EnemyRuntimeDownstreamPortsV1 ports)
        {
            return new EnemyPlacementRuntimeFactoryV1(
                new RoomContentObjectCatalogV1(objects),
                new EnemyCatalogV1(1, Id("enemy-catalog", "factory-fixture-v1"), definitions),
                policies,
                new DeterministicEnemyRuntimeIdentityDeriverV1(),
                new EnemyDifficultyRuntimeRegistrationV1(
                    new EnemyDifficultyScalingConfigurationV1(
                        Id("enemy-difficulty", "test-scalar"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicyV1()),
                new EnemyPerceptionRuntimeRegistrationV1(
                    new EnemyPerceptionPolicyConfigurationV1(
                        Id("enemy-perception", "test-validated"),
                        false),
                    new ValidatedEnemyPerceptionRuntimeAdapterV1()),
                ports);
        }

        private static EnemyRuntimePolicyRegistryV1 CustomPolicies(
            EnemyDefinitionV1 definition,
            EnemyMovementPolicyRegistrationV1 movement,
            IEnemyTargetingAimPolicyV1 aim,
            IEnemyAttackCapabilityAdapterV1 attack)
        {
            StableId aimId = Id("enemy-aim", "fixture-locked");
            return new EnemyRuntimePolicyRegistryV1(
                new[] { movement },
                new[]
                {
                    new EnemyDecisionPolicyRegistrationV1(
                        new EnemyDecisionPolicyConfigurationV1(
                            definition.DecisionPolicyId,
                            Id("enemy-phase", "ready"),
                            false,
                            0d,
                            0d),
                        new FoundationEnemyDecisionRuntimePolicyV1()),
                },
                new[]
                {
                    new EnemyTargetingAimPolicyRegistrationV1(
                        new EnemyTargetingAimPolicyConfigurationV1(
                            aimId,
                            EnemyAimCommitmentModeV1.LockedDirectionAndPoint,
                            0d,
                            0d),
                        aim),
                },
                new[]
                {
                    new EnemyAttackCapabilityRuntimeRegistrationV1(
                        new EnemyAttackCapabilityConfigurationV1(
                            definition.Attacks[0].CapabilityId,
                            aimId,
                            ExpectedExecutionKind(definition.Attacks[0])),
                        attack),
                });
        }

        private static EnemyMovementPolicyRegistrationV1 MovementRegistration(
            EnemyDefinitionV1 definition)
        {
            return new EnemyMovementPolicyRegistrationV1(
                new EnemyMovementPolicyConfigurationV1(
                    definition.MovementPolicyId,
                    definition.MovementPolicyId == Id("enemy-movement", "stationary") ? 0d : 4d,
                    10d,
                    240d,
                    true),
                new DecisionMovementRuntimePolicyV1(),
                new DirectEnemyMovementIntentRealizerV1());
        }

        private static EnemyAttackExecutionKindV1 ExpectedExecutionKind(
            EnemyAttackCapabilityDescriptorV1 attack)
        {
            if (attack.Melee != null)
            {
                return attack.Melee.PounceDistance > 0d
                    ? EnemyAttackExecutionKindV1.Pounce
                    : EnemyAttackExecutionKindV1.Contact;
            }
            return attack.Area != null
                ? EnemyAttackExecutionKindV1.Area
                : EnemyAttackExecutionKindV1.Projectile;
        }

        private static EnemyDefinitionV1 HybridDefinition()
        {
            var ranged = new EnemyAttackCapabilityDescriptorV1(
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
                new EnemyProjectileAttackParametersV1(
                    Id("projectile", "hybrid"), 1, 13d, 14d, 0.15d, 0d, 0),
                null,
                null);
            var contact = new EnemyAttackCapabilityDescriptorV1(
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
                new EnemyMeleeAttackParametersV1(0.8d, 0d, 0d, 0d));
            return new EnemyDefinitionV1(
                Id("enemy", "hybrid"),
                Id("presentation", "enemy-hybrid"),
                36d,
                new EnemyLevelScalingProfileV1(1, 100, 2.2d, 1.01d),
                Id("faction", "hostile-machines"),
                18d,
                270d,
                Id("enemy-movement", "pursuit"),
                Id("enemy-decision", "multi-attack-standard"),
                new[] { ranged, contact },
                Id("xp", "enemy-standard"),
                Id("drop", "enemy-common"),
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyDefinitionV1 Definition(
            string name,
            string movement,
            string decision,
            string capability,
            EnemyCatalogRoomClearRoleV1 role,
            bool melee,
            double visionArc,
            double attackArc)
        {
            StableId capabilityId = Id("enemy-attack", capability);
            bool pounce = capability == "pounce";
            bool contact = capability == "contact" || pounce;
            EnemyMeleeAttackParametersV1 meleeParameters = contact
                ? new EnemyMeleeAttackParametersV1(0.8d, pounce ? 6d : 0d, 0d, 0d)
                : null;
            EnemyProjectileAttackParametersV1 projectileParameters = contact
                ? null
                : new EnemyProjectileAttackParametersV1(
                    Id("projectile", name), 1, 12d, 16d, 0.15d, 0d, 0);
            EnemyAreaAttackParametersV1 areaParameters = capability == "projectile-area"
                ? new EnemyAreaAttackParametersV1(1.5d, 0d, 8)
                : null;
            double preferredRange = contact ? 0.4d : 3d;
            double maximumRange = contact ? 0.8d : 6d;
            return new EnemyDefinitionV1(
                Id("enemy", name),
                Id("presentation", "enemy-" + name),
                20d,
                new EnemyLevelScalingProfileV1(1, 100, 2d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                visionArc,
                Id("enemy-movement", movement),
                Id("enemy-decision", decision),
                new[]
                {
                    new EnemyAttackCapabilityDescriptorV1(
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

        private static RoomContentObjectDefinitionV1 Object(
            string name,
            EnemyDefinitionV1 definition)
        {
            return new RoomContentObjectDefinitionV1(
                Id("room-object", name),
                RoomContentObjectKindV1.Enemy,
                definition.DefinitionId,
                definition.PresentationId);
        }

        private static EnemyPlacementRuntimeRequestV1 Request(
            string placement,
            string roomObject,
            int level,
            long generation,
            double difficultyScalar)
        {
            return new EnemyPlacementRuntimeRequestV1(
                Placement(placement, roomObject, level),
                Id("run", "fixture-run"),
                Id("room-runtime", "fixture-room-runtime"),
                null,
                generation,
                generation,
                new EnemyDifficultyContextV1(
                    Id("difficulty", "fixture"),
                    difficultyScalar));
        }

        private static RoomEnemyPlacementContentV1 Placement(
            string placement,
            string roomObject,
            int level)
        {
            return new RoomEnemyPlacementContentV1(
                Id("enemy-placement", placement),
                Id("room", "fixture"),
                Id("room-object", roomObject),
                level,
                new RoomVector2V1(0d, 0d),
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

        private sealed class RecordingMovementRealizer : IEnemyMovementIntentRealizerV1
        {
            public int CallCount { get; private set; }
            public EnemyMovementPolicyIntentV1 LastIntent { get; private set; }
            public EnemyMovementRealizationContextV1 LastContext { get; private set; }
            public EnemyMovementPolicyConfigurationV1 LastConfiguration { get; private set; }

            public EnemyMovementRealizationV1 Realize(
                EnemyMovementPolicyIntentV1 intent,
                EnemyMovementRealizationContextV1 context,
                EnemyMovementPolicyConfigurationV1 configuration)
            {
                CallCount++;
                LastIntent = intent;
                LastContext = context;
                LastConfiguration = configuration;
                return new EnemyMovementRealizationV1(
                    new EnemyVector2(7d, 0d),
                    intent.DesiredFacing,
                    intent.Kind,
                    configuration.PolicyId);
            }
        }

        private sealed class RecordingAimPolicy : IEnemyTargetingAimPolicyV1
        {
            public int CallCount { get; private set; }
            public EnemyTargetingAimContextV1 LastContext { get; private set; }

            public EnemyAttackIntent Commit(
                EnemyAttackIntent requestedIntent,
                EnemyTargetingAimContextV1 context,
                EnemyTargetingAimPolicyConfigurationV1 configuration)
            {
                CallCount++;
                LastContext = context;
                return requestedIntent;
            }
        }

        private sealed class RecordingAttackCapability : IEnemyAttackCapabilityAdapterV1
        {
            private readonly RequestEnemyAttackCapabilityAdapterV1 inner =
                new RequestEnemyAttackCapabilityAdapterV1();

            public int CallCount { get; private set; }
            public EnemyAttackExecutionContextV1 LastContext { get; private set; }

            public EnemyAttackExecutionRequestV1 BuildExecution(
                EnemyAttackCapabilityDescriptorV1 descriptor,
                EnemyAttackIntent committedIntent,
                StableId itemInstanceStableId,
                EnemyAttackCapabilityConfigurationV1 configuration,
                EnemyAttackExecutionContextV1 context)
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
            IEnemyAttackEffectPortV1,
            IEnemyPlayerDamagePortV1,
            IEnemyRoomTerminalPortV1,
            IEnemyExperienceFactConsumerV1,
            IEnemyDropFactConsumerV1,
            IEnemyKillStatFactConsumerV1,
            IEnemyTerminalCollisionAdapterV1
        {
            public RecordingPorts()
            {
                Bundle = new EnemyRuntimeDownstreamPortsV1(
                    this, this, this, this, this, this, this);
            }

            public EnemyRuntimeDownstreamPortsV1 Bundle { get; }
            public int AttackEffectCount { get; private set; }
            public int PlayerDamageCount { get; private set; }
            public int RoomCount { get; private set; }
            public int ExperienceCount { get; private set; }
            public int DropCount { get; private set; }
            public int KillCount { get; private set; }
            public int TerminalCollisionCount { get; private set; }
            public ReportRoomOccupantTerminalCommandV1 LastRoomCommand { get; private set; }

            public void Emit(EnemyAttackExecutionRequestV1 request)
            {
                AttackEffectCount++;
            }

            public EnemyPlayerDamagePortResultV1 Route(EnemyPlayerDamageRequestV1 request)
            {
                PlayerDamageCount++;
                return new EnemyPlayerDamagePortResultV1(
                    EnemyRuntimeOperationStatusV1.Applied,
                    EnemyRuntimeRejectionCodeV1.None);
            }

            public void Report(
                ReportRoomOccupantTerminalCommandV1 command,
                EnemyDeathFactV1 deathFact)
            {
                RoomCount++;
                LastRoomCommand = command;
            }

            void IEnemyExperienceFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
                ExperienceCount++;
            }

            void IEnemyDropFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
                DropCount++;
            }

            void IEnemyKillStatFactConsumerV1.Consume(EnemyDeathFactV1 fact)
            {
                KillCount++;
            }

            public void SetTerminal(EnemyTerminalCollisionFactV1 fact)
            {
                TerminalCollisionCount++;
            }
        }
    }
}

using System;
using NUnit.Framework;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternAuthorityV1Tests
    {
        [Test]
        public void CatalogSchemaVersion_ControlsLegacyCompatibilityTag()
        {
            EnemyCatalogImportResultV1 production =
                EnemyCatalogJsonImporterV1.Import(ReadAuthoredCatalog(), Registry());
            EnemyCatalogImportResultV1 fixture =
                EnemyCatalogJsonImporterV1.Import(SchemaV2Fixture(), Registry());

            Assert.That(production.IsValid, Is.True, FirstIssue(production));
            Assert.That(fixture.IsValid, Is.True, FirstIssue(fixture));
            Assert.That(
                EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(
                    production.Catalog.Definitions[0].Attacks[0]),
                Is.True);
            Assert.That(
                EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(
                    fixture.Catalog.Definitions[0].Attacks[0]),
                Is.False);
        }

        [Test]
        public void ReusingOneSourceDefinitionAcrossCatalogVersions_DoesNotLeakCompatibilityMode()
        {
            EnemyAttackCapabilityDescriptorV1 attack = Shooting(
                "shared-catalog-source",
                1,
                0d,
                1,
                0d,
                0d,
                1d,
                12d,
                null);
            var source = new EnemyDefinitionV1(
                Id("enemy.shared-catalog-source"),
                Id("presentation.enemy-mobile-blaster-droid"),
                16d,
                new EnemyLevelScalingProfileV1(1, 100, 1d, 1.01d),
                Id("faction.hostile-machines"),
                20d,
                360d,
                Id("enemy-movement.mobile-positioning"),
                Id("enemy-decision.ranged-standard"),
                new[] { attack },
                Id("xp.enemy-standard"),
                Id("drop.enemy-common"),
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());

            var legacy = new EnemyCatalogV1(
                1,
                Id("enemy-catalog.shared-source-v1"),
                new[] { source });
            var canonical = new EnemyCatalogV1(
                2,
                Id("enemy-catalog.shared-source-v2"),
                new[] { source });

            EnemyAttackCapabilityDescriptorV1 legacyAttack =
                legacy.Definitions[0].Attacks[0];
            EnemyAttackCapabilityDescriptorV1 canonicalAttack =
                canonical.Definitions[0].Attacks[0];
            Assert.That(legacyAttack, Is.Not.SameAs(canonicalAttack));
            Assert.That(legacyAttack, Is.Not.SameAs(attack));
            Assert.That(canonicalAttack, Is.Not.SameAs(attack));
            Assert.That(
                EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(legacyAttack),
                Is.True);
            Assert.That(
                EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(canonicalAttack),
                Is.False);
            Assert.That(
                EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(attack),
                Is.False);
        }
    }

    public sealed partial class EnemyAttackPatternLiveIntegrationV1Tests
    {
        [Test]
        public void SchemaV1TimedPounce_UsesHistoricalOneCallBoundaryWithoutPatternSequence()
        {
            EnemyDefinitionV1 definition = LegacyTimedPounceDefinition();
            var support = new RecordingPatternPorts();
            var legacy = new RecordingLegacyAttackPort();
            EnemyPlacementRuntimeInstanceV1 runtime = LegacyRuntime(
                definition,
                support.WithAttackEffects(legacy));
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            StableId operation = Id("enemy-operation", "legacy-timed-pounce");

            EnemyAttackExecutionResultV1 applied = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                operation,
                10d);
            EnemyAttackExecutionResultV1 replay = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContextV1(perception, 1d),
                operation,
                10d);

            Assert.That(applied.IsAccepted, Is.True);
            Assert.That(replay.Status, Is.EqualTo(EnemyRuntimeOperationStatusV1.ExactReplay));
            Assert.That(legacy.ExecutionCount, Is.EqualTo(1));
            Assert.That(runtime.AttackPatterns.Sequences, Is.Empty);
            Assert.That(
                EnemyAttackDescriptorCompatibilityV1.IsLegacyCompatibility(
                    runtime.Definition.Attacks[0]),
                Is.True);
        }

        private static EnemyPlacementRuntimeInstanceV1 LegacyRuntime(
            EnemyDefinitionV1 definition,
            EnemyRuntimeDownstreamPortsV1 ports)
        {
            var roomObject = new RoomContentObjectDefinitionV1(
                Id("room-object", "live-burst"),
                RoomContentObjectKindV1.Enemy,
                definition.DefinitionId,
                definition.PresentationId);
            var factory = new EnemyPlacementRuntimeFactoryV1(
                new RoomContentObjectCatalogV1(new[] { roomObject }),
                new EnemyCatalogV1(
                    1,
                    Id("enemy-catalog", "legacy-cutover"),
                    new[] { definition }),
                BuiltInEnemyRuntimePolicyRegistryV1.Create(),
                new DeterministicEnemyRuntimeIdentityDeriverV1(),
                new EnemyDifficultyRuntimeRegistrationV1(
                    new EnemyDifficultyScalingConfigurationV1(
                        Id("enemy-difficulty", "legacy-cutover"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicyV1()),
                new EnemyPerceptionRuntimeRegistrationV1(
                    new EnemyPerceptionPolicyConfigurationV1(
                        Id("enemy-perception", "legacy-cutover"),
                        false),
                    new ValidatedEnemyPerceptionRuntimeAdapterV1()),
                ports);
            return factory.Create(Request()).Runtime;
        }

        private static EnemyDefinitionV1 LegacyTimedPounceDefinition()
        {
            var attack = new EnemyAttackCapabilityDescriptorV1(
                Id("enemy-attack-profile", "legacy-timed-pounce"),
                Id("enemy-attack", "pounce"),
                10,
                120d,
                0d,
                5d,
                8d,
                2.2d,
                8d,
                Id("damage", "impact"),
                null,
                null,
                new EnemyMeleeAttackParametersV1(
                    0.8d,
                    6d,
                    0.35d,
                    0.6d));
            return new EnemyDefinitionV1(
                Id("enemy", "legacy-timed-pounce"),
                Id("presentation", "enemy-legacy-timed-pounce"),
                24d,
                new EnemyLevelScalingProfileV1(1, 100, 1.8d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                360d,
                Id("enemy-movement", "pursuit"),
                Id("enemy-decision", "pounce-standard"),
                new[] { attack },
                Id("xp", "enemy-standard"),
                Id("drop", "enemy-common"),
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());
        }
    }
}

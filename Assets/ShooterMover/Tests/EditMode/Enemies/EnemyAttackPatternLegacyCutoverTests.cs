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
    public sealed partial class EnemyAttackPatternStateTests
    {
        [Test]
        public void CatalogSchemaVersion_ControlsLegacyCompatibilityTag()
        {
            EnemyCatalogImportResult production =
                EnemyCatalogJsonImporter.Import(ReadAuthoredCatalog(), Registry());
            EnemyCatalogImportResult fixture =
                EnemyCatalogJsonImporter.Import(SchemaV2Fixture(), Registry());

            Assert.That(production.IsValid, Is.True, FirstIssue(production));
            Assert.That(fixture.IsValid, Is.True, FirstIssue(fixture));
            Assert.That(
                EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(
                    production.Catalog.Definitions[0].Attacks[0]),
                Is.True);
            Assert.That(
                EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(
                    fixture.Catalog.Definitions[0].Attacks[0]),
                Is.False);
        }

        [Test]
        public void ReusingOneSourceDefinitionAcrossCatalogVersions_DoesNotLeakCompatibilityMode()
        {
            EnemyAttackCapabilityDescriptor attack = Shooting(
                "shared-catalog-source",
                1,
                0d,
                1,
                0d,
                0d,
                1d,
                12d,
                null);
            var source = new EnemyDefinition(
                Id("enemy.shared-catalog-source"),
                Id("presentation.enemy-mobile-blaster-droid"),
                16d,
                new EnemyLevelScalingProfile(1, 100, 1d, 1.01d),
                Id("faction.hostile-machines"),
                20d,
                360d,
                Id("enemy-movement.mobile-positioning"),
                Id("enemy-decision.ranged-standard"),
                new[] { attack },
                Id("xp.enemy-standard"),
                Id("drop.enemy-common"),
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());

            var legacy = new EnemyCatalog(
                1,
                Id("enemy-catalog.shared-source-v1"),
                new[] { source });
            var canonical = new EnemyCatalog(
                2,
                Id("enemy-catalog.shared-source-v2"),
                new[] { source });

            EnemyAttackCapabilityDescriptor legacyAttack =
                legacy.Definitions[0].Attacks[0];
            EnemyAttackCapabilityDescriptor canonicalAttack =
                canonical.Definitions[0].Attacks[0];
            Assert.That(legacyAttack, Is.Not.SameAs(canonicalAttack));
            Assert.That(legacyAttack, Is.Not.SameAs(attack));
            Assert.That(canonicalAttack, Is.Not.SameAs(attack));
            Assert.That(
                EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(legacyAttack),
                Is.True);
            Assert.That(
                EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(canonicalAttack),
                Is.False);
            Assert.That(
                EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(attack),
                Is.False);
        }
    }

    public sealed partial class EnemyAttackPatternLiveIntegrationTests
    {
        [Test]
        public void SchemaV1TimedPounce_UsesHistoricalOneCallBoundaryWithoutPatternSequence()
        {
            EnemyDefinition definition = LegacyTimedPounceDefinition();
            var support = new RecordingPatternPorts();
            var legacy = new RecordingLegacyAttackPort();
            EnemyPlacementLiveInstance runtime = LegacyRuntime(
                definition,
                support.WithAttackEffects(legacy));
            EnemyPerceptionSnapshot perception = Perception();
            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            StableId operation = Id("enemy-operation", "legacy-timed-pounce");

            EnemyAttackExecutionResult applied = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                operation,
                10d);
            EnemyAttackExecutionResult replay = runtime.TryExecuteAttack(
                decision,
                new EnemyTargetingAimContext(perception, 1d),
                operation,
                10d);

            Assert.That(applied.IsAccepted, Is.True);
            Assert.That(replay.Status, Is.EqualTo(EnemyLiveOperationStatus.ExactReplay));
            Assert.That(legacy.ExecutionCount, Is.EqualTo(1));
            Assert.That(runtime.AttackPatterns.Sequences, Is.Empty);
            Assert.That(
                EnemyAttackDescriptorCompatibility.IsLegacyCompatibility(
                    runtime.Definition.Attacks[0]),
                Is.True);
        }

        private static EnemyPlacementLiveInstance LegacyRuntime(
            EnemyDefinition definition,
            EnemyLiveDownstreamPorts ports)
        {
            var roomObject = new RoomContentObjectDefinition(
                Id("room-object", "live-burst"),
                RoomContentObjectKind.Enemy,
                definition.DefinitionId,
                definition.PresentationId);
            var factory = new EnemyPlacementLiveFactory(
                new RoomContentObjectCatalog(new[] { roomObject }),
                new EnemyCatalog(
                    1,
                    Id("enemy-catalog", "legacy-cutover"),
                    new[] { definition }),
                BuiltInEnemyLivePolicyRegistry.Create(),
                new DeterministicEnemyLiveIdentityDeriver(),
                new EnemyDifficultyLiveRegistration(
                    new EnemyDifficultyScalingConfiguration(
                        Id("enemy-difficulty", "legacy-cutover"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicy()),
                new EnemyPerceptionLiveRegistration(
                    new EnemyPerceptionPolicyConfiguration(
                        Id("enemy-perception", "legacy-cutover"),
                        false),
                    new ValidatedEnemyPerceptionLiveBridge()),
                ports);
            return factory.Create(Request()).Runtime;
        }

        private static EnemyDefinition LegacyTimedPounceDefinition()
        {
            var attack = new EnemyAttackCapabilityDescriptor(
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
                new EnemyMeleeAttackParameters(
                    0.8d,
                    6d,
                    0.35d,
                    0.6d));
            return new EnemyDefinition(
                Id("enemy", "legacy-timed-pounce"),
                Id("presentation", "enemy-legacy-timed-pounce"),
                24d,
                new EnemyLevelScalingProfile(1, 100, 1.8d, 1.01d),
                Id("faction", "hostile-machines"),
                20d,
                360d,
                Id("enemy-movement", "pursuit"),
                Id("enemy-decision", "pounce-standard"),
                new[] { attack },
                Id("xp", "enemy-standard"),
                Id("drop", "enemy-common"),
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());
        }
    }
}

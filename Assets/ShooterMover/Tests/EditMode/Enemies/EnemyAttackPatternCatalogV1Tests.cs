using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternAuthorityV1Tests
    {
        [Test]
        public void AuthoredProductionCatalog_RemainsSchemaV1UntilScheduledConsumerCutover()
        {
            EnemyCatalogImportResultV1 result = EnemyCatalogJsonImporterV1.Import(
                ReadAuthoredCatalog(),
                Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Catalog.SchemaVersion, Is.EqualTo(1));
            Assert.That(
                result.Catalog.ContentVersion,
                Is.EqualTo(Id("enemy-catalog.content-v1")));

            EnemyDefinitionV1 blaster = result.Catalog.GetDefinition(
                Id("enemy.mobile-blaster-droid"));
            Assert.That(blaster.Attacks[0].ShootingPattern, Is.Not.Null);
            Assert.That(blaster.Attacks[0].ShootingPattern.ShotsPerSequence, Is.EqualTo(1));

            EnemyDefinitionV1 pounce = result.Catalog.GetDefinition(
                Id("enemy.ram-pouncer"));
            Assert.That(pounce.Attacks[0].MeleePattern, Is.Not.Null);
            Assert.That(pounce.Attacks[0].MeleePattern.LungeDistance, Is.EqualTo(6d));
        }

        [Test]
        public void SchemaV2Fixture_UsesReplacementPatternsWithoutCuttingOverProductionContent()
        {
            EnemyCatalogImportResultV1 result = EnemyCatalogJsonImporterV1.Import(
                SchemaV2Fixture(),
                Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Catalog.SchemaVersion, Is.EqualTo(2));

            EnemyDefinitionV1 ranged = result.Catalog.GetDefinition(
                Id("enemy.mobile-blaster-droid"));
            Assert.That(ranged.Attacks[0].ShootingPattern.ShotsPerSequence, Is.EqualTo(3));
            Assert.That(ranged.Attacks[0].ShootingPattern.ProjectilesPerShot, Is.EqualTo(1));
            Assert.That(ranged.Attacks[0].ShootingPattern.WindUpSeconds, Is.EqualTo(0.1d));
            Assert.That(
                ranged.Attacks[0].ShootingPattern.SequenceAimPolicy,
                Is.EqualTo(EnemySequenceAimPolicyV1.LockAtSequenceStart));

            EnemyDefinitionV1 pounce = result.Catalog.GetDefinition(
                Id("enemy.ram-pouncer"));
            Assert.That(pounce.Attacks[0].MeleePattern.LungeDistance, Is.EqualTo(6d));
            Assert.That(pounce.Attacks[0].MeleePattern.ActiveWindowSeconds, Is.EqualTo(0.25d));
            Assert.That(
                pounce.Attacks[0].MeleePattern.TerminalOnImpactPolicy,
                Is.EqualTo(EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence));
        }

        [Test]
        public void ReservedAimAndTerminalPolicies_RejectAtExactSchemaV2Paths()
        {
            string authored = SchemaV2Fixture();
            EnemyCatalogImportResultV1 shootingAim = EnemyCatalogJsonImporterV1.Import(
                ReplaceFirst(
                    authored,
                    "\"sequence_aim_policy\": \"lock-at-sequence-start\"",
                    "\"sequence_aim_policy\": \"reaim-each-shot\""),
                Registry());
            EnemyCatalogImportResultV1 meleeAim = EnemyCatalogJsonImporterV1.Import(
                ReplaceFirst(
                    authored,
                    "\"aim_commit_policy\": \"lock-at-wind-up\"",
                    "\"aim_commit_policy\": \"track-until-active-window\""),
                Registry());
            EnemyCatalogImportResultV1 terminal = EnemyCatalogJsonImporterV1.Import(
                ReplaceFirst(
                    authored,
                    "\"terminal_on_impact_policy\": \"continue-sequence\"",
                    "\"terminal_on_impact_policy\": \"end-sequence-on-any-impact\""),
                Registry());

            AssertIssue(
                shootingAim,
                "enemy-catalog-attack-policy-unsupported-v1",
                "$.definitions[0].attacks[0].shooting_pattern.sequence_aim_policy");
            AssertIssue(
                meleeAim,
                "enemy-catalog-attack-policy-unsupported-v1",
                "$.definitions[1].attacks[0].melee_pattern.aim_commit_policy");
            AssertIssue(
                terminal,
                "enemy-catalog-attack-policy-unsupported-v1",
                "$.definitions[1].attacks[0].melee_pattern.terminal_on_impact_policy");
        }

        [Test]
        public void ShootingScheduler_ProvesSingleBurstRapidPelletsAndSlowExplosivePayload()
        {
            EnemyAttackSequenceV1 single = Sequence(Shooting(
                "single", 1, 0d, 1, 0d, 0.1d, 0.9d, 12d, null),
                "single", 10d);
            Assert.That(single.Shots, Has.Count.EqualTo(1));
            Assert.That(single.Projectiles, Has.Count.EqualTo(1));
            Assert.That(single.Shots[0].ScheduledAtSeconds,
                Is.EqualTo(10.1d).Within(0.0000001d));

            EnemyAttackSequenceV1 burst = Sequence(Shooting(
                "burst", 3, 0.2d, 1, 0d, 0.25d, 0.8d, 12d, null),
                "burst", 20d);
            Assert.That(burst.Shots, Has.Count.EqualTo(3));
            Assert.That(burst.Projectiles, Has.Count.EqualTo(3));
            Assert.That(burst.Shots[0].ScheduledAtSeconds,
                Is.EqualTo(20.25d).Within(0.0000001d));
            Assert.That(burst.Shots[1].ScheduledAtSeconds,
                Is.EqualTo(20.45d).Within(0.0000001d));
            Assert.That(burst.Shots[2].ScheduledAtSeconds,
                Is.EqualTo(20.65d).Within(0.0000001d));

            EnemyAttackSequenceV1 rapid = Sequence(Shooting(
                "rapid", 6, 0.05d, 1, 2d, 0d, 0.4d, 16d, null),
                "rapid", 30d);
            Assert.That(rapid.Shots, Has.Count.EqualTo(6));
            Assert.That(rapid.Shots[5].ScheduledAtSeconds,
                Is.EqualTo(30.25d).Within(0.0000001d));

            EnemyAttackSequenceV1 shotgun = Sequence(Shooting(
                "shotgun", 1, 0d, 7, 60d, 0.15d, 1.1d, 10d, null),
                "shotgun", 40d);
            Assert.That(shotgun.Shots, Has.Count.EqualTo(1));
            Assert.That(shotgun.Projectiles, Has.Count.EqualTo(7));
            for (int index = 0; index < shotgun.Projectiles.Count; index++)
            {
                Assert.That(
                    shotgun.Projectiles[index].ScheduledAtSeconds,
                    Is.EqualTo(40.15d).Within(0.0000001d));
            }
            Assert.That(shotgun.Projectiles[0].SpreadOffsetDegrees, Is.EqualTo(-30d));
            Assert.That(shotgun.Projectiles[3].SpreadOffsetDegrees, Is.EqualTo(0d));
            Assert.That(shotgun.Projectiles[6].SpreadOffsetDegrees, Is.EqualTo(30d));
            var projectileIds = new HashSet<StableId>();
            for (int index = 0; index < shotgun.Projectiles.Count; index++)
                Assert.That(projectileIds.Add(shotgun.Projectiles[index].ProjectileStableId), Is.True);

            var explosion = new EnemyAreaPayloadV1(2.5d, 0d, 16);
            EnemyAttackSequenceV1 slowExplosive = Sequence(Shooting(
                "slow-explosive", 1, 0d, 1, 0d, 0.5d, 1.5d, 4d, explosion),
                "slow-explosive", 50d);
            Assert.That(slowExplosive.Projectiles[0].Payload.Speed, Is.EqualTo(4d));
            Assert.That(slowExplosive.Projectiles[0].Payload.AreaPayload, Is.SameAs(explosion));
        }

        private static string ReadAuthoredCatalog()
        {
            return File.ReadAllText(Path.Combine(
                "Assets",
                "ShooterMover",
                "Content",
                "Definitions",
                "Enemies",
                "Json",
                "enemy_catalog_v1.json"));
        }

        private static string SchemaV2Fixture()
        {
            return @"{
  ""schema_version"": 2,
  ""content_version"": ""enemy-catalog.pattern-fixture-v2"",
  ""definitions"": [
    {
      ""id"": ""enemy.mobile-blaster-droid"",
      ""presentation"": ""presentation.enemy-mobile-blaster-droid"",
      ""base_health"": 16.0,
      ""level_scaling"": { ""base_level"": 1, ""maximum_level"": 100, ""additive_health_per_level"": 1.2, ""multiplicative_health_per_level"": 1.01 },
      ""faction"": ""faction.hostile-machines"",
      ""perception"": { ""detection_radius"": 20.0, ""vision_arc_degrees"": 360.0 },
      ""movement_policy"": ""enemy-movement.mobile-positioning"",
      ""decision_policy"": ""enemy-decision.ranged-standard"",
      ""attacks"": [{
        ""id"": ""enemy-attack-profile.mobile-blaster-primary"",
        ""capability"": ""enemy-attack.ranged-projectile"",
        ""selection_priority"": 10,
        ""attack_arc_degrees"": 90.0,
        ""minimum_range"": 0.0,
        ""preferred_range"": 5.0,
        ""maximum_range"": 12.0,
        ""damage"": 3.0,
        ""damage_channel"": ""damage.kinetic"",
        ""shooting_pattern"": {
          ""shots_per_sequence"": 3,
          ""interval_between_shots_seconds"": 0.2,
          ""projectiles_per_shot"": 1,
          ""per_shot_spread_degrees"": 0.0,
          ""sequence_aim_policy"": ""lock-at-sequence-start"",
          ""wind_up_seconds"": 0.1,
          ""post_sequence_recovery_seconds"": 0.5,
          ""interruption_policy"": ""cancel-pending-on-lifecycle-end""
        },
        ""projectile_payload"": {
          ""profile"": ""projectile.enemy-blaster"",
          ""speed"": 12.0,
          ""maximum_travel_distance"": 16.0,
          ""collision_radius"": 0.15,
          ""pierce"": 0
        }
      }],
      ""xp_profile"": ""xp.enemy-standard"",
      ""drop_profile"": ""drop.enemy-common"",
      ""room_clear_role"": ""required-enemy"",
      ""special_capabilities"": []
    },
    {
      ""id"": ""enemy.ram-pouncer"",
      ""presentation"": ""presentation.enemy-ram-pouncer"",
      ""base_health"": 24.0,
      ""level_scaling"": { ""base_level"": 1, ""maximum_level"": 100, ""additive_health_per_level"": 1.8, ""multiplicative_health_per_level"": 1.01 },
      ""faction"": ""faction.hostile-machines"",
      ""perception"": { ""detection_radius"": 12.0, ""vision_arc_degrees"": 220.0 },
      ""movement_policy"": ""enemy-movement.pursuit"",
      ""decision_policy"": ""enemy-decision.pounce-standard"",
      ""attacks"": [{
        ""id"": ""enemy-attack-profile.ram-pounce"",
        ""capability"": ""enemy-attack.pounce"",
        ""selection_priority"": 10,
        ""attack_arc_degrees"": 70.0,
        ""minimum_range"": 0.0,
        ""preferred_range"": 1.0,
        ""maximum_range"": 2.0,
        ""damage"": 8.0,
        ""damage_channel"": ""damage.impact"",
        ""melee_pattern"": {
          ""wind_up_seconds"": 0.35,
          ""active_window_seconds"": 0.25,
          ""strike_count"": 1,
          ""interval_between_strikes_seconds"": 0.0,
          ""contact_radius"": 0.8,
          ""lunge_distance"": 6.0,
          ""aim_commit_policy"": ""lock-at-wind-up"",
          ""recovery_seconds"": 1.6,
          ""hits_per_target"": 1,
          ""terminal_on_impact_policy"": ""continue-sequence"",
          ""interruption_policy"": ""cancel-pending-on-lifecycle-end""
        }
      }],
      ""xp_profile"": ""xp.enemy-standard"",
      ""drop_profile"": ""drop.enemy-common"",
      ""room_clear_role"": ""required-enemy"",
      ""special_capabilities"": [""enemy-special.locked-commitment""]
    }
  ]
}";
        }

        private static string ReplaceFirst(string source, string oldValue, string newValue)
        {
            int index = source.IndexOf(oldValue, System.StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            return source.Substring(0, index)
                + newValue
                + source.Substring(index + oldValue.Length);
        }

        private static void AssertIssue(
            EnemyCatalogImportResultV1 result,
            string code,
            string path)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Count.GreaterThan(0));
            Assert.That(result.Issues[0].Code, Is.EqualTo(code));
            Assert.That(result.Issues[0].Path, Is.EqualTo(path));
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyCatalogJsonImporterV1Tests
    {
        [Test]
        public void AuthoredCatalogImportsRangedPounceTurretAndPursuitFixtures()
        {
            EnemyCatalogImportResultV1 result = EnemyCatalogJsonImporterV1.Import(
                ReadAuthoredCatalog(),
                Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Catalog.Definitions, Has.Count.EqualTo(4));
            Assert.That(
                Get(result, "enemy.mobile-blaster-droid").Attacks[0].ParameterKinds,
                Is.EqualTo(EnemyAttackParameterKindsV1.Projectile));
            Assert.That(
                Get(result, "enemy.ram-pouncer").Attacks[0].Melee.PounceDistance,
                Is.EqualTo(6d));
            Assert.That(
                Get(result, "enemy.blaster-turret").Attacks[0].ParameterKinds,
                Is.EqualTo(
                    EnemyAttackParameterKindsV1.Projectile
                    | EnemyAttackParameterKindsV1.Area));
            Assert.That(
                Get(result, "enemy.pursuer-drone").MovementPolicyId,
                Is.EqualTo(Id("enemy-movement.pursuit")));
        }

        [Test]
        public void DuplicateIdsAndMalformedRangesRejectWithoutPartialCatalog()
        {
            string definition = Fixture("enemy.fixture-alpha");
            EnemyCatalogImportResultV1 duplicate = Import(definition, definition);
            EnemyCatalogImportResultV1 badRange = Import(
                Fixture("enemy.fixture-alpha", detection: 10d, maximumRange: 12d));

            Assert.That(duplicate.Catalog, Is.Null);
            AssertIssue(duplicate, "enemy-catalog-definition-duplicate");
            Assert.That(badRange.Catalog, Is.Null);
            AssertIssue(badRange, "enemy-catalog-range-invalid");
        }

        [Test]
        public void UnknownMovementAndAttackCapabilitiesFailClosed()
        {
            EnemyCatalogImportResultV1 movement = Import(
                Fixture("enemy.fixture-alpha", movement: "enemy-movement.unknown"));
            EnemyCatalogImportResultV1 attack = Import(
                Fixture("enemy.fixture-alpha", capability: "enemy-attack.unknown"));

            AssertIssue(movement, "enemy-catalog-movement-policy-unknown");
            AssertIssue(attack, "enemy-catalog-attack-capability-unknown");
        }

        [Test]
        public void AttackArcIsIndependentFromVisionArcAndAffectsFingerprint()
        {
            EnemyCatalogImportResultV1 narrow = Import(
                Fixture("enemy.fixture-alpha", visionArc: 360d, attackArc: 60d));
            EnemyCatalogImportResultV1 wide = Import(
                Fixture("enemy.fixture-alpha", visionArc: 360d, attackArc: 240d));

            Assert.That(narrow.IsValid, Is.True, FirstIssue(narrow));
            Assert.That(wide.IsValid, Is.True, FirstIssue(wide));
            Assert.That(narrow.Catalog.Definitions[0].VisionArcDegrees, Is.EqualTo(360d));
            Assert.That(narrow.Catalog.Definitions[0].AttackArcDegrees, Is.EqualTo(60d));
            Assert.That(wide.Catalog.Definitions[0].AttackArcDegrees, Is.EqualTo(240d));
            Assert.That(narrow.Catalog.Fingerprint, Is.Not.EqualTo(wide.Catalog.Fingerprint));
        }

        [Test]
        public void DefinitionOrderDoesNotAffectCatalogFingerprint()
        {
            string alpha = Fixture("enemy.fixture-alpha");
            string beta = Fixture(
                "enemy.fixture-beta",
                presentation: "presentation.enemy-fixture-beta");

            EnemyCatalogImportResultV1 first = Import(alpha, beta);
            EnemyCatalogImportResultV1 second = Import(beta, alpha);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            Assert.That(second.IsValid, Is.True, FirstIssue(second));
            Assert.That(first.Catalog.Fingerprint, Is.EqualTo(second.Catalog.Fingerprint));
            Assert.That(
                first.Catalog.Definitions[0].DefinitionId,
                Is.EqualTo(Id("enemy.fixture-alpha")));
        }

        [Test]
        public void NewEnemyUsingRegisteredMechanicsIsDefinitionOnly()
        {
            EnemyCatalogImportResultV1 result = Import(
                Fixture(
                    "enemy.fixture-scout",
                    presentation: "presentation.enemy-fixture-scout",
                    movement: "enemy-movement.pursuit"));

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            EnemyDefinitionV1 definition;
            Assert.That(
                result.Catalog.TryGetDefinition(Id("enemy.fixture-scout"), out definition),
                Is.True);
            Assert.That(
                definition.Attacks[0].CapabilityId,
                Is.EqualTo(Id("enemy-attack.ranged-projectile")));
        }

        [Test]
        public void MissingPresentationAndIncompatibleAttackShapeReject()
        {
            EnemyCatalogImportResultV1 presentation = Import(
                Fixture(
                    "enemy.fixture-alpha",
                    presentation: "presentation.enemy-missing"));
            EnemyCatalogImportResultV1 shape = Import(
                Fixture(
                    "enemy.fixture-alpha",
                    capability: "enemy-attack.pounce"));

            AssertIssue(presentation, "enemy-catalog-presentation-missing");
            AssertIssue(shape, "enemy-catalog-attack-parameters-incompatible");
        }

        [Test]
        public void MalformedStableIdRejectsAtExactField()
        {
            EnemyCatalogImportResultV1 result = Import(
                Fixture("Enemy Fixture Alpha"));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues[0].Code, Is.EqualTo("enemy-catalog-id-invalid"));
            Assert.That(result.Issues[0].Path, Is.EqualTo("$.definitions[0].id"));
        }

        private static EnemyCatalogImportResultV1 Import(params string[] definitions)
        {
            return EnemyCatalogJsonImporterV1.Import(
                "{\"schema_version\":1,\"content_version\":\"enemy-catalog.content-v1\","
                + "\"definitions\":[" + string.Join(",", definitions) + "]}",
                Registry());
        }

        private static string Fixture(
            string id,
            string presentation = "presentation.enemy-fixture-alpha",
            double detection = 20d,
            double visionArc = 360d,
            double attackArc = 90d,
            double maximumRange = 12d,
            string movement = "enemy-movement.mobile-positioning",
            string capability = "enemy-attack.ranged-projectile")
        {
            return "{\"id\":\"" + id + "\",\"presentation\":\"" + presentation
                + "\",\"base_health\":16,\"level_scaling\":{\"base_level\":1,"
                + "\"maximum_level\":100,\"additive_health_per_level\":1,"
                + "\"multiplicative_health_per_level\":1.01},"
                + "\"faction\":\"faction.hostile-machines\",\"perception\":{"
                + "\"detection_radius\":" + Number(detection)
                + ",\"vision_arc_degrees\":" + Number(visionArc) + "},"
                + "\"attack_geometry\":{\"attack_arc_degrees\":" + Number(attackArc)
                + ",\"minimum_range\":0,\"preferred_range\":5,\"maximum_range\":"
                + Number(maximumRange) + "},\"movement_policy\":\"" + movement
                + "\",\"decision_policy\":\"enemy-decision.ranged-standard\","
                + "\"attacks\":[{\"id\":\"enemy-attack-profile.fixture-primary\","
                + "\"capability\":\"" + capability + "\",\"cooldown_seconds\":1,"
                + "\"damage\":3,\"damage_channel\":\"damage.kinetic\","
                + "\"projectile\":{\"profile\":\"projectile.enemy-blaster\","
                + "\"count\":1,\"speed\":12,\"maximum_travel_distance\":16,"
                + "\"collision_radius\":0.15,\"spread_degrees\":0,\"pierce\":0}}],"
                + "\"xp_profile\":\"xp.enemy-light\",\"drop_profile\":\"drop.enemy-none\","
                + "\"room_clear_role\":\"required-enemy\",\"special_capabilities\":[]}";
        }

        private static EnemyCatalogRegistryV1 Registry()
        {
            return new EnemyCatalogRegistryV1(
                Ids(
                    "enemy-movement.mobile-positioning",
                    "enemy-movement.pursuit",
                    "enemy-movement.stationary"),
                Ids(
                    "enemy-decision.ranged-standard",
                    "enemy-decision.pounce-standard",
                    "enemy-decision.turret-standard",
                    "enemy-decision.contact-standard"),
                new[]
                {
                    Attack("enemy-attack.ranged-projectile", EnemyAttackParameterKindsV1.Projectile),
                    Attack("enemy-attack.pounce", EnemyAttackParameterKindsV1.Melee),
                    Attack(
                        "enemy-attack.projectile-area",
                        EnemyAttackParameterKindsV1.Projectile | EnemyAttackParameterKindsV1.Area),
                    Attack("enemy-attack.contact", EnemyAttackParameterKindsV1.Melee),
                },
                Ids("enemy-special.locked-commitment", "enemy-special.rotating-aim"),
                Ids(
                    "presentation.enemy-mobile-blaster-droid",
                    "presentation.enemy-ram-pouncer",
                    "presentation.enemy-blaster-turret",
                    "presentation.enemy-pursuer-drone",
                    "presentation.enemy-fixture-alpha",
                    "presentation.enemy-fixture-beta",
                    "presentation.enemy-fixture-scout"),
                Ids("damage.kinetic", "damage.impact", "damage.thermal"),
                Ids("xp.enemy-standard", "xp.enemy-light", "xp.enemy-turret"),
                Ids("drop.enemy-common", "drop.enemy-none", "drop.enemy-turret"));
        }

        private static EnemyAttackCapabilityRegistrationV1 Attack(
            string id,
            EnemyAttackParameterKindsV1 parameters)
        {
            return new EnemyAttackCapabilityRegistrationV1(Id(id), parameters, parameters);
        }

        private static StableId[] Ids(params string[] values)
        {
            var result = new List<StableId>();
            for (int index = 0; index < values.Length; index++) result.Add(Id(values[index]));
            return result.ToArray();
        }

        private static EnemyDefinitionV1 Get(
            EnemyCatalogImportResultV1 result,
            string definitionId)
        {
            EnemyDefinitionV1 definition;
            Assert.That(result.Catalog.TryGetDefinition(Id(definitionId), out definition), Is.True);
            return definition;
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

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static void AssertIssue(EnemyCatalogImportResultV1 result, string code)
        {
            Assert.That(result.IsValid, Is.False);
            for (int index = 0; index < result.Issues.Count; index++)
            {
                if (result.Issues[index].Code == code) return;
            }
            Assert.Fail("Expected issue " + code + ", got " + FirstIssue(result));
        }

        private static string FirstIssue(EnemyCatalogImportResultV1 result)
        {
            return result.Issues.Count == 0 ? string.Empty : result.Issues[0].ToString();
        }
    }
}

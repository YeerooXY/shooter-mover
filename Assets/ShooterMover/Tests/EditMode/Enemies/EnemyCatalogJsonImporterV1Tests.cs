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
        public void AuthoredCatalogImportsAllFixturesAndCanonicalizesHybridPriority()
        {
            EnemyCatalogImportResultV1 result = EnemyCatalogJsonImporterV1.Import(
                ReadAuthoredCatalog(),
                Registry());

            Assert.That(result.IsValid, Is.True, FirstIssue(result));
            Assert.That(result.Catalog.Definitions, Has.Count.EqualTo(5));
            Assert.That(
                Get(result, "enemy.mobile-blaster-droid").Attacks[0].Projectile.ProjectileProfileId,
                Is.EqualTo(Id("projectile.enemy-blaster")));
            Assert.That(Get(result, "enemy.ram-pouncer").Attacks[0].Melee.PounceDistance, Is.EqualTo(6d));
            Assert.That(
                Get(result, "enemy.blaster-turret").Attacks[0].ParameterKinds,
                Is.EqualTo(EnemyAttackParameterKindsV1.Projectile | EnemyAttackParameterKindsV1.Area));
            Assert.That(Get(result, "enemy.pursuer-drone").MovementPolicyId, Is.EqualTo(Id("enemy-movement.pursuit")));

            EnemyDefinitionV1 hybrid = Get(result, "enemy.hybrid-sentinel");
            Assert.That(hybrid.Attacks, Has.Count.EqualTo(2));
            Assert.That(hybrid.Attacks[0].AttackId, Is.EqualTo(Id("enemy-attack-profile.hybrid-contact")));
            Assert.That(hybrid.Attacks[0].SelectionPriority, Is.EqualTo(10));
            Assert.That(hybrid.Attacks[1].AttackId, Is.EqualTo(Id("enemy-attack-profile.hybrid-ranged")));
            Assert.That(hybrid.Attacks[1].SelectionPriority, Is.EqualTo(20));
        }

        [Test]
        public void ProjectileProfilesResolveAndUnknownRejectsAtExactPath()
        {
            EnemyCatalogImportResultV1 known = Import(ProjectileDefinition(
                "enemy.fixture-alpha",
                "projectile.enemy-blaster"));
            EnemyCatalogImportResultV1 unknown = Import(ProjectileDefinition(
                "enemy.fixture-alpha",
                "projectile.enemy-unregistered"));

            Assert.That(known.IsValid, Is.True, FirstIssue(known));
            Assert.That(
                known.Catalog.Definitions[0].Attacks[0].Projectile.ProjectileProfileId,
                Is.EqualTo(Id("projectile.enemy-blaster")));
            Assert.That(unknown.Catalog, Is.Null);
            AssertIssue(
                unknown,
                "enemy-catalog-projectile-profile-unknown",
                "$.definitions[0].attacks[0].projectile.profile");
        }

        [Test]
        public void MalformedProjectileProfileReportsExactFieldAndMeleeNeedsNoProfile()
        {
            EnemyCatalogImportResultV1 malformed = Import(ProjectileDefinition(
                "enemy.fixture-alpha",
                "Projectile Bad"));
            EnemyCatalogImportResultV1 melee = ImportWithRegistry(
                Registry(new string[0]),
                MeleeDefinition("enemy.fixture-alpha", 0.7d, 0.8d, 0d));

            Assert.That(malformed.Catalog, Is.Null);
            AssertIssue(
                malformed,
                "enemy-catalog-id-invalid",
                "$.definitions[0].attacks[0].projectile.profile");
            Assert.That(melee.IsValid, Is.True, FirstIssue(melee));
            Assert.That(melee.Catalog.Definitions[0].Attacks[0].Projectile, Is.Null);
        }

        [Test]
        public void MixedAttacksKeepDistinctGeometryAndArrayOrderDoesNotAffectFingerprints()
        {
            EnemyCatalogImportResultV1 rangedFirst = Import(MixedDefinition(false));
            EnemyCatalogImportResultV1 meleeFirst = Import(MixedDefinition(true));

            Assert.That(rangedFirst.IsValid, Is.True, FirstIssue(rangedFirst));
            Assert.That(meleeFirst.IsValid, Is.True, FirstIssue(meleeFirst));
            EnemyDefinitionV1 definition = rangedFirst.Catalog.Definitions[0];
            Assert.That(definition.Attacks, Has.Count.EqualTo(2));
            Assert.That(definition.Attacks[0].SelectionPriority, Is.EqualTo(10));
            Assert.That(definition.Attacks[0].AttackArcDegrees, Is.EqualTo(140d));
            Assert.That(definition.Attacks[0].MaximumAttackRange, Is.EqualTo(0.75d));
            Assert.That(definition.Attacks[0].Melee, Is.Not.Null);
            Assert.That(definition.Attacks[1].SelectionPriority, Is.EqualTo(20));
            Assert.That(definition.Attacks[1].AttackArcDegrees, Is.EqualTo(45d));
            Assert.That(definition.Attacks[1].MinimumAttackRange, Is.EqualTo(3d));
            Assert.That(definition.Attacks[1].MaximumAttackRange, Is.EqualTo(11d));
            Assert.That(definition.Attacks[1].Projectile, Is.Not.Null);
            Assert.That(rangedFirst.Catalog.Fingerprint, Is.EqualTo(meleeFirst.Catalog.Fingerprint));
            Assert.That(definition.Fingerprint, Is.EqualTo(meleeFirst.Catalog.Definitions[0].Fingerprint));
        }

        [Test]
        public void PerAttackArcRangeTravelReachAndPriorityRejectAtExactFields()
        {
            EnemyCatalogImportResultV1 arc = Import(ProjectileDefinition(
                "enemy.fixture-alpha", "projectile.enemy-blaster", 0d));
            EnemyCatalogImportResultV1 detection = Import(ProjectileDefinition(
                "enemy.fixture-alpha", "projectile.enemy-blaster", 90d, 12d, 16d, 10, 10d));
            EnemyCatalogImportResultV1 travel = Import(ProjectileDefinition(
                "enemy.fixture-alpha", "projectile.enemy-blaster", 90d, 12d, 8d));
            EnemyCatalogImportResultV1 reach = Import(MeleeDefinition(
                "enemy.fixture-alpha", 2d, 0.8d, 0d));
            EnemyCatalogImportResultV1 priority = Import(MixedDefinition(false, 10));

            AssertIssue(arc, "enemy-catalog-arc-invalid", "$.definitions[0].attacks[0].attack_arc_degrees");
            AssertIssue(detection, "enemy-catalog-attack-range-invalid", "$.definitions[0].attacks[0].maximum_range");
            AssertIssue(travel, "enemy-catalog-projectile-range-invalid", "$.definitions[0].attacks[0].projectile.maximum_travel_distance");
            AssertIssue(reach, "enemy-catalog-melee-range-incompatible", "$.definitions[0].attacks[0].maximum_range");
            AssertIssue(priority, "enemy-catalog-attack-priority-invalid", "$.definitions[0].attacks[1].selection_priority");
        }

        [Test]
        public void DuplicateDefinitionsAndUnknownBehaviorReferencesFailClosed()
        {
            string definition = ProjectileDefinition("enemy.fixture-alpha", "projectile.enemy-blaster");
            EnemyCatalogImportResultV1 duplicate = Import(definition, definition);
            EnemyCatalogImportResultV1 movement = Import(definition.Replace(
                "enemy-movement.mobile-positioning",
                "enemy-movement.unknown"));
            EnemyCatalogImportResultV1 capability = Import(definition.Replace(
                "enemy-attack.ranged-projectile",
                "enemy-attack.unknown"));

            Assert.That(duplicate.Catalog, Is.Null);
            AssertIssue(duplicate, "enemy-catalog-definition-duplicate");
            AssertIssue(movement, "enemy-catalog-movement-policy-unknown");
            AssertIssue(capability, "enemy-catalog-attack-capability-unknown");
        }

        [Test]
        public void VisionAndAttackArcsRemainIndependentAndSemantic()
        {
            EnemyCatalogImportResultV1 narrow = Import(ProjectileDefinition(
                "enemy.fixture-alpha", "projectile.enemy-blaster", 60d));
            EnemyCatalogImportResultV1 wide = Import(ProjectileDefinition(
                "enemy.fixture-alpha", "projectile.enemy-blaster", 240d));

            Assert.That(narrow.IsValid, Is.True, FirstIssue(narrow));
            Assert.That(wide.IsValid, Is.True, FirstIssue(wide));
            Assert.That(narrow.Catalog.Definitions[0].VisionArcDegrees, Is.EqualTo(360d));
            Assert.That(narrow.Catalog.Definitions[0].Attacks[0].AttackArcDegrees, Is.EqualTo(60d));
            Assert.That(wide.Catalog.Definitions[0].Attacks[0].AttackArcDegrees, Is.EqualTo(240d));
            Assert.That(narrow.Catalog.Fingerprint, Is.Not.EqualTo(wide.Catalog.Fingerprint));
        }

        [Test]
        public void DefinitionOrderDoesNotAffectCatalogFingerprint()
        {
            string alpha = ProjectileDefinition("enemy.fixture-alpha", "projectile.enemy-blaster");
            string beta = ProjectileDefinition(
                "enemy.fixture-beta",
                "projectile.enemy-blaster",
                90d,
                12d,
                16d,
                10,
                20d,
                "presentation.enemy-fixture-beta");

            EnemyCatalogImportResultV1 first = Import(alpha, beta);
            EnemyCatalogImportResultV1 second = Import(beta, alpha);

            Assert.That(first.IsValid, Is.True, FirstIssue(first));
            Assert.That(second.IsValid, Is.True, FirstIssue(second));
            Assert.That(first.Catalog.Fingerprint, Is.EqualTo(second.Catalog.Fingerprint));
            Assert.That(first.Catalog.Definitions[0].DefinitionId, Is.EqualTo(Id("enemy.fixture-alpha")));
        }

        [Test]
        public void ExtensionAndReferenceDiagnosticsRemainDefinitionOnlyAndExact()
        {
            EnemyCatalogImportResultV1 extension = Import(ProjectileDefinition(
                "enemy.fixture-scout",
                "projectile.enemy-blaster",
                90d,
                12d,
                16d,
                10,
                20d,
                "presentation.enemy-fixture-scout",
                "enemy-movement.pursuit"));
            EnemyCatalogImportResultV1 presentation = Import(ProjectileDefinition(
                "enemy.fixture-alpha",
                "projectile.enemy-blaster",
                90d,
                12d,
                16d,
                10,
                20d,
                "presentation.enemy-missing"));
            EnemyCatalogImportResultV1 shape = Import(ProjectileDefinition(
                "enemy.fixture-alpha",
                "projectile.enemy-blaster",
                90d,
                12d,
                16d,
                10,
                20d,
                "presentation.enemy-fixture-alpha",
                "enemy-movement.mobile-positioning",
                "enemy-attack.pounce"));
            EnemyCatalogImportResultV1 malformed = Import(ProjectileDefinition(
                "Enemy Fixture Alpha",
                "projectile.enemy-blaster"));

            Assert.That(extension.IsValid, Is.True, FirstIssue(extension));
            Assert.That(extension.Catalog.Definitions[0].Attacks[0].CapabilityId, Is.EqualTo(Id("enemy-attack.ranged-projectile")));
            AssertIssue(presentation, "enemy-catalog-presentation-missing");
            AssertIssue(shape, "enemy-catalog-attack-parameters-incompatible");
            AssertIssue(malformed, "enemy-catalog-id-invalid", "$.definitions[0].id");
        }

        private static EnemyCatalogImportResultV1 Import(params string[] definitions)
        {
            return ImportWithRegistry(Registry(), definitions);
        }

        private static EnemyCatalogImportResultV1 ImportWithRegistry(
            EnemyCatalogRegistryV1 registry,
            params string[] definitions)
        {
            return EnemyCatalogJsonImporterV1.Import(
                "{\"schema_version\":1,\"content_version\":\"enemy-catalog.content-v1\","
                + "\"definitions\":[" + string.Join(",", definitions) + "]}",
                registry);
        }

        private static string ProjectileDefinition(
            string id,
            string profile,
            double attackArc = 90d,
            double maximumRange = 12d,
            double travelDistance = 16d,
            int priority = 10,
            double detectionRadius = 20d,
            string presentation = "presentation.enemy-fixture-alpha",
            string movementPolicy = "enemy-movement.mobile-positioning",
            string capability = "enemy-attack.ranged-projectile")
        {
            string attack = ProjectileAttack(
                "enemy-attack-profile.fixture-primary",
                capability,
                priority,
                attackArc,
                0d,
                5d,
                maximumRange,
                profile,
                travelDistance);
            return Definition(id, presentation, detectionRadius, movementPolicy, "enemy-decision.ranged-standard", attack);
        }

        private static string MeleeDefinition(
            string id,
            double maximumRange,
            double contactRadius,
            double pounceDistance)
        {
            string attack = MeleeAttack(
                "enemy-attack-profile.fixture-contact",
                10,
                120d,
                0d,
                0.4d,
                maximumRange,
                contactRadius,
                pounceDistance);
            return Definition(
                id,
                "presentation.enemy-fixture-alpha",
                20d,
                "enemy-movement.pursuit",
                "enemy-decision.contact-standard",
                attack);
        }

        private static string MixedDefinition(bool meleeFirst, int rangedPriority = 20)
        {
            string ranged = ProjectileAttack(
                "enemy-attack-profile.fixture-ranged",
                "enemy-attack.ranged-projectile",
                rangedPriority,
                45d,
                3d,
                7d,
                11d,
                "projectile.enemy-blaster",
                14d);
            string melee = MeleeAttack(
                "enemy-attack-profile.fixture-contact",
                10,
                140d,
                0d,
                0.4d,
                0.75d,
                0.8d,
                0d);
            return Definition(
                "enemy.fixture-alpha",
                "presentation.enemy-fixture-alpha",
                20d,
                "enemy-movement.pursuit",
                "enemy-decision.multi-attack-standard",
                meleeFirst ? new[] { melee, ranged } : new[] { ranged, melee });
        }

        private static string Definition(
            string id,
            string presentation,
            double detectionRadius,
            string movementPolicy,
            string decisionPolicy,
            params string[] attacks)
        {
            return "{\"id\":\"" + id + "\",\"presentation\":\"" + presentation
                + "\",\"base_health\":16,\"level_scaling\":{\"base_level\":1,"
                + "\"maximum_level\":100,\"additive_health_per_level\":1,"
                + "\"multiplicative_health_per_level\":1.01},"
                + "\"faction\":\"faction.hostile-machines\",\"perception\":{"
                + "\"detection_radius\":" + Number(detectionRadius)
                + ",\"vision_arc_degrees\":360},"
                + "\"movement_policy\":\"" + movementPolicy
                + "\",\"decision_policy\":\"" + decisionPolicy
                + "\",\"attacks\":[" + string.Join(",", attacks) + "],"
                + "\"xp_profile\":\"xp.enemy-light\","
                + "\"drop_profile\":\"drop.enemy-none\","
                + "\"room_clear_role\":\"required-enemy\","
                + "\"special_capabilities\":[]}";
        }

        private static string ProjectileAttack(
            string id,
            string capability,
            int priority,
            double arc,
            double minimumRange,
            double preferredRange,
            double maximumRange,
            string profile,
            double travelDistance)
        {
            return "{\"id\":\"" + id + "\",\"capability\":\"" + capability
                + "\",\"selection_priority\":" + priority
                + ",\"attack_arc_degrees\":" + Number(arc)
                + ",\"minimum_range\":" + Number(minimumRange)
                + ",\"preferred_range\":" + Number(preferredRange)
                + ",\"maximum_range\":" + Number(maximumRange)
                + ",\"cooldown_seconds\":1,\"damage\":3,"
                + "\"damage_channel\":\"damage.kinetic\",\"projectile\":{"
                + "\"profile\":\"" + profile
                + "\",\"count\":1,\"speed\":12,\"maximum_travel_distance\":"
                + Number(travelDistance)
                + ",\"collision_radius\":0.15,\"spread_degrees\":0,\"pierce\":0}}";
        }

        private static string MeleeAttack(
            string id,
            int priority,
            double arc,
            double minimumRange,
            double preferredRange,
            double maximumRange,
            double contactRadius,
            double pounceDistance)
        {
            return "{\"id\":\"" + id + "\",\"capability\":\"enemy-attack.contact\""
                + ",\"selection_priority\":" + priority
                + ",\"attack_arc_degrees\":" + Number(arc)
                + ",\"minimum_range\":" + Number(minimumRange)
                + ",\"preferred_range\":" + Number(preferredRange)
                + ",\"maximum_range\":" + Number(maximumRange)
                + ",\"cooldown_seconds\":1,\"damage\":3,"
                + "\"damage_channel\":\"damage.impact\",\"melee\":{"
                + "\"contact_radius\":" + Number(contactRadius)
                + ",\"pounce_distance\":" + Number(pounceDistance)
                + ",\"wind_up_seconds\":0,\"commitment_seconds\":0}}";
        }

        private static EnemyCatalogRegistryV1 Registry()
        {
            return Registry(new[] { "projectile.enemy-blaster", "projectile.enemy-turret-shell" });
        }

        private static EnemyCatalogRegistryV1 Registry(string[] projectileProfiles)
        {
            return new EnemyCatalogRegistryV1(
                Ids("enemy-movement.mobile-positioning", "enemy-movement.pursuit", "enemy-movement.stationary"),
                Ids(
                    "enemy-decision.ranged-standard",
                    "enemy-decision.pounce-standard",
                    "enemy-decision.turret-standard",
                    "enemy-decision.contact-standard",
                    "enemy-decision.multi-attack-standard"),
                new[]
                {
                    Attack("enemy-attack.ranged-projectile", EnemyAttackParameterKindsV1.Projectile),
                    Attack("enemy-attack.pounce", EnemyAttackParameterKindsV1.Melee),
                    Attack("enemy-attack.projectile-area", EnemyAttackParameterKindsV1.Projectile | EnemyAttackParameterKindsV1.Area),
                    Attack("enemy-attack.contact", EnemyAttackParameterKindsV1.Melee),
                },
                Ids("enemy-special.locked-commitment", "enemy-special.rotating-aim"),
                Ids(
                    "presentation.enemy-mobile-blaster-droid",
                    "presentation.enemy-ram-pouncer",
                    "presentation.enemy-blaster-turret",
                    "presentation.enemy-pursuer-drone",
                    "presentation.enemy-hybrid-sentinel",
                    "presentation.enemy-fixture-alpha",
                    "presentation.enemy-fixture-beta",
                    "presentation.enemy-fixture-scout"),
                Ids(projectileProfiles),
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

        private static EnemyDefinitionV1 Get(EnemyCatalogImportResultV1 result, string definitionId)
        {
            EnemyDefinitionV1 definition;
            Assert.That(result.Catalog.TryGetDefinition(Id(definitionId), out definition), Is.True);
            return definition;
        }

        private static string ReadAuthoredCatalog()
        {
            return File.ReadAllText(Path.Combine(
                "Assets", "ShooterMover", "Content", "Definitions", "Enemies", "Json", "enemy_catalog_v1.json"));
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

        private static void AssertIssue(
            EnemyCatalogImportResultV1 result,
            string code,
            string path)
        {
            Assert.That(result.IsValid, Is.False);
            for (int index = 0; index < result.Issues.Count; index++)
            {
                if (result.Issues[index].Code == code && result.Issues[index].Path == path) return;
            }
            Assert.Fail("Expected issue " + code + " at " + path + ", got " + FirstIssue(result));
        }

        private static string FirstIssue(EnemyCatalogImportResultV1 result)
        {
            return result.Issues.Count == 0 ? string.Empty : result.Issues[0].ToString();
        }
    }
}

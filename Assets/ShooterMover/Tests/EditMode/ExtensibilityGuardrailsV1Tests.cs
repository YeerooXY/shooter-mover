#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Application.Modifiers;
using ShooterMover.Application.Modifiers.Events;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Modifiers.Events;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Props;

namespace ShooterMover.Tests.EditMode.Architecture
{
    public sealed class ExtensibilityGuardrailsV1Tests
    {
        private const string EnemyFixturePath =
            "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_enemy_catalog_v1.json";
        private const string AccessFixturePath =
            "Assets/ShooterMover/Tests/EditMode/extensibility_guardrails_access_v1.json";

        private static readonly string[] FixtureIds =
        {
            "skill.guardrail-critical-focus",
            "skill.guardrail-pressure-cycle",
            "enemy.guardrail-scout",
            "prop.guardrail-switch",
            "room.guardrail-entry",
            "holding.guardrail-key",
            "event.guardrail-drop-boost",
        };

        [Test]
        public void NumericalAndFactWindowSkills_ProjectThroughExistingModifierLanguage()
        {
            RankedSkillDefinitionV2 criticalFocus = new RankedSkillDefinitionV2(
                "skill.guardrail-critical-focus",
                "category.guardrail-combat",
                3,
                null,
                null,
                null,
                null,
                new[] { 0.01m, 0.02m, 0.03m },
                new[]
                {
                    new SkillEffectDescriptorV2(
                        DerivedStatTargetIdsV1.CriticalChance,
                        SkillModifierKindV2.Flat,
                        1m),
                },
                null);
            RankedSkillDefinitionV2 pressureCycle = new RankedSkillDefinitionV2(
                "skill.guardrail-pressure-cycle",
                "category.guardrail-combat",
                1,
                null,
                null,
                null,
                null,
                new[] { 1m },
                new[]
                {
                    new SkillEffectDescriptorV2(
                        DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                        SkillModifierKindV2.Multiplicative,
                        1.20m,
                        "condition.guardrail-two-kills"),
                },
                null);
            RankedSkillCatalogV2 catalog = new RankedSkillCatalogV2(
                "skills.schema.v2",
                "skills.guardrails.content-v1",
                new[] { criticalFocus, pressureCycle },
                null);
            RankedSkillAllocationSnapshotV2 allocation =
                new RankedSkillAllocationSnapshotV2(
                    "profile.guardrail",
                    "striker",
                    1L,
                    catalog.SchemaVersion,
                    catalog.ContentVersion,
                    new Dictionary<string, int>
                    {
                        { criticalFocus.Id, 2 },
                        { pressureCycle.Id, 1 },
                    });

            RuntimeModifierSnapshotV1 modifiers = SkillEffectModifierAdapterV1.Adapt(
                new SkillEffectProjectorV2().Project(catalog, allocation));
            RuntimeModifierEvaluationV1 critical = modifiers.Evaluate(
                DerivedStatTargetIdsV1.CriticalChance,
                0.05m,
                null,
                0m,
                1m);

            var conditions = new FactWindowConditionAuthorityV1(
                "participant.guardrail-player",
                new[]
                {
                    new FactWindowConditionDefinitionV1(
                        "condition.guardrail-two-kills",
                        "fact.enemy-killed",
                        2,
                        5L,
                        10L),
                });
            conditions.Apply(Kill("fact.guardrail-kill-one", 100L));
            conditions.Apply(Kill("fact.guardrail-kill-two", 103L));
            RuntimeModifierEvaluationV1 activeDamage = modifiers.Evaluate(
                DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                1m,
                conditions.ActiveConditionIdsAt(103L));
            RuntimeModifierEvaluationV1 expiredDamage = modifiers.Evaluate(
                DerivedStatTargetIdsV1.OutgoingDamageMultiplier,
                1m,
                conditions.ActiveConditionIdsAt(114L));

            Assert.That(critical.FinalValue, Is.EqualTo(0.08m));
            Assert.That(activeDamage.FinalValue, Is.EqualTo(1.20m));
            Assert.That(expiredDamage.FinalValue, Is.EqualTo(1m));
        }

        [Test]
        public void EnemyFixture_ImportsThroughMergedCatalogAndExactRegisteredBoundaries()
        {
            string json = File.ReadAllText(EnemyFixturePath);
            EnemyCatalogImportResultV1 imported = EnemyCatalogJsonImporterV1.Import(
                json,
                EnemyRegistry());

            Assert.That(imported.IsValid, Is.True, FirstEnemyIssue(imported));
            Assert.That(imported.Catalog.Definitions, Has.Count.EqualTo(1));
            EnemyDefinitionV1 definition = imported.Catalog.Definitions[0];
            Assert.That(definition.DefinitionId, Is.EqualTo(Id("enemy.guardrail-scout")));
            Assert.That(definition.MovementPolicyId, Is.EqualTo(Id("enemy-movement.pursuit")));
            Assert.That(
                definition.Attacks[0].CapabilityId,
                Is.EqualTo(Id("enemy-attack.ranged-projectile")));

            EnemyCatalogImportResultV1 unknownCapability =
                EnemyCatalogJsonImporterV1.Import(
                    json.Replace(
                        "enemy-attack.ranged-projectile",
                        "enemy-attack.guardrail-unregistered"),
                    EnemyRegistry());

            Assert.That(unknownCapability.IsValid, Is.False);
            Assert.That(unknownCapability.Catalog, Is.Null);
            Assert.That(
                unknownCapability.Issues.Any(issue =>
                    issue.Code == "enemy-catalog-attack-capability-unknown"
                    && issue.Path.EndsWith(
                        ".attacks[0].capability",
                        StringComparison.Ordinal)),
                Is.True,
                FirstEnemyIssue(unknownCapability));
        }

        [Test]
        public void PropFixture_RegistersAndRunsThroughBuiltInCapabilitiesOnly()
        {
            PropDefinitionV1 definition = new PropDefinitionV1(
                Id("prop.guardrail-switch"),
                Id("presentation.prop-guardrail-switch"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.Interactable(Id("fact.guardrail-switch-used")),
                    PropCapabilitiesV1.Switch(Id("switch.guardrail-power"), false),
                    PropCapabilitiesV1.Objective(Id("objective.guardrail-power-restored")),
                });
            PropCatalogV1 catalog = new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                new[] { definition });
            PropPlacementV1 placement = new PropPlacementV1(
                PlacedObjectIdentity.CreateAuthored(Id("placed.guardrail-switch")),
                definition.DefinitionId);
            PropRuntimeCreationResultV1 created = new PropRuntimeFactoryV1().Create(
                catalog,
                placement,
                null);

            Assert.That(created.IsCreated, Is.True);
            PropInteractionResultV1 result = created.Runtime.Interact(
                new PropInteractionCommandV1(
                    Id("operation.guardrail-switch-use"),
                    Id("participant.guardrail-player")));

            Assert.That(result.Status, Is.EqualTo(PropInteractionStatusV1.Applied));
            Assert.That(result.SwitchFact, Is.Not.Null);
            Assert.That(created.Runtime.Snapshot.SwitchActive, Is.True);
        }

        [Test]
        public void RoomAndKeyedDoorFixtures_ImportThroughPublicRoomAndAccessBoundaries()
        {
            RoomContentImportResultV1 room = RoomContentJsonImporterV1.Import(
                GuardrailRoomPackage(),
                BuiltInRoomContentObjectCatalogV1.Create());

            Assert.That(room.IsValid, Is.True, FirstRoomIssue(room));
            Assert.That(
                room.Bundle.RuntimeDefinition.GetRoom(Id("room.guardrail-entry")),
                Is.Not.Null);

            RoomContentImportResultV1 unknownObject = RoomContentJsonImporterV1.Import(
                GuardrailRoomPackage("prop.guardrail-unregistered"),
                BuiltInRoomContentObjectCatalogV1.Create());
            Assert.That(unknownObject.IsValid, Is.False);
            Assert.That(unknownObject.Bundle, Is.Null);
            Assert.That(unknownObject.Issues[0].Code, Is.EqualTo("room-content-object-unknown"));
            Assert.That(
                unknownObject.Issues[0].Path,
                Is.EqualTo("$documents[\"guardrail.entry.props\"].props[0].object"));

            RoomAccessReferenceCatalogV1 references =
                new RoomAccessReferenceCatalogV1(
                    new[]
                    {
                        new RoomAccessReferenceRegistrationV1(
                            Id("holding.guardrail-key"),
                            RoomAccessReferenceKindV1.Holding,
                            RoomAccessReferenceSourceV1.RunHolding),
                    });
            string accessJson = File.ReadAllText(AccessFixturePath);
            RoomAccessImportResultV1 access = RoomAccessJsonImporterV1.Import(
                accessJson,
                room.Bundle.RuntimeDefinition,
                references);

            Assert.That(access.IsValid, Is.True, FirstAccessIssue(access));
            Assert.That(access.Definition.Doors, Has.Count.EqualTo(1));
            Assert.That(
                access.Definition.Doors[0].ConsumeHoldingStableId,
                Is.EqualTo(Id("holding.guardrail-key")));

            RoomAccessImportResultV1 unknownKey = RoomAccessJsonImporterV1.Import(
                accessJson.Replace(
                    "holding.guardrail-key",
                    "holding.guardrail-key-missing"),
                room.Bundle.RuntimeDefinition,
                references);
            Assert.That(unknownKey.IsValid, Is.False);
            Assert.That(unknownKey.Definition, Is.Null);
            Assert.That(unknownKey.Issues[0].Path, Is.EqualTo("$.conditions[0].subject"));
        }

        [Test]
        public void SpecialDropRateEvent_ProjectsThroughMergedEventCatalog()
        {
            SpecialEventDefinitionV1 definition = new SpecialEventDefinitionV1(
                SpecialEventDefinitionV1.CurrentSchemaVersion,
                "events.guardrails.content-v1",
                "event.guardrail-drop-boost",
                new EventActivationWindowV1(100L, 200L),
                10,
                SpecialEventOverlapModeV1.Combine,
                new[]
                {
                    new EventModifierDescriptorV1(
                        EventModifierTargetIdsV1.RewardStrongboxWeight,
                        RuntimeModifierOperationV1.Multiplicative,
                        1.5m),
                },
                null);
            SpecialEventCatalogV1 catalog = new SpecialEventCatalogV1(
                "events.guardrails.catalog-v1",
                new[] { definition });
            ActiveEventProjectionResultV1 result =
                new ActiveEventModifierProjectionServiceV1(
                    catalog,
                    new FixedClockV1(150L))
                .ProjectActiveEvents();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Snapshot.ActiveEvents, Has.Count.EqualTo(1));
            Assert.That(
                result.Snapshot.ModifierSnapshot.Evaluate(
                    EventModifierTargetIdsV1.RewardStrongboxWeight,
                    100m).FinalValue,
                Is.EqualTo(150m));
        }

        [Test]
        public void FixtureIds_AreAbsentFromProductionGameplaySources()
        {
            string[] roots =
            {
                "Assets/ShooterMover/Runtime",
                "Assets/ShooterMover/Production",
            };
            var offenders = new List<string>();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (!Directory.Exists(roots[rootIndex])) continue;
                string[] files = Directory.GetFiles(
                    roots[rootIndex],
                    "*.cs",
                    SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string source = File.ReadAllText(files[fileIndex]);
                    for (int idIndex = 0; idIndex < FixtureIds.Length; idIndex++)
                    {
                        if (source.IndexOf(FixtureIds[idIndex], StringComparison.Ordinal) >= 0)
                        {
                            offenders.Add(files[fileIndex] + " => " + FixtureIds[idIndex]);
                        }
                    }
                }
            }

            Assert.That(
                offenders,
                Is.Empty,
                "Fixture registration leaked into production source:\n"
                + string.Join("\n", offenders));
        }

        private static RoomContentJsonPackageV1 GuardrailRoomPackage(
            string propObjectId = "prop.level1-cover")
        {
            const string manifest =
                "{\"version\":1,\"layout\":\"layout.guardrails-extension\","
                + "\"start_room\":\"room.guardrail-entry\","
                + "\"terminal_room\":\"room.guardrail-terminal\",\"rooms\":["
                + "{\"layout\":\"guardrail.entry.layout\","
                + "\"enemies\":\"guardrail.entry.enemies\","
                + "\"props\":\"guardrail.entry.props\","
                + "\"decor\":\"guardrail.entry.decor\","
                + "\"encounter\":\"guardrail.entry.encounter\"},"
                + "{\"layout\":\"guardrail.terminal.layout\","
                + "\"enemies\":\"guardrail.terminal.enemies\","
                + "\"props\":\"guardrail.terminal.props\","
                + "\"decor\":\"guardrail.terminal.decor\","
                + "\"encounter\":\"guardrail.terminal.encounter\"}]}";
            var documents = new Dictionary<string, string>
            {
                {
                    "guardrail.entry.layout",
                    "{\"room\":\"room.guardrail-entry\",\"order\":0,"
                    + "\"display_name\":\"GUARDRAIL CACHE\",\"bounds\":{"
                    + "\"center\":[0,0],\"size\":[16,10]},\"spawns\":[{"
                    + "\"kind\":\"forward-entry\",\"position\":[-6,0],"
                    + "\"rotation\":0}],\"doors\":[{"
                    + "\"object\":\"door.room-standard\",\"position\":[7,0],"
                    + "\"rotation\":0,\"link\":{\"kind\":\"room\","
                    + "\"exit_type\":\"progression\","
                    + "\"target_room\":\"room.guardrail-terminal\","
                    + "\"target_spawn_kind\":\"forward-entry\"}}]}"
                },
                {
                    "guardrail.entry.enemies",
                    "{\"room\":\"room.guardrail-entry\",\"enemies\":[]}"
                },
                {
                    "guardrail.entry.props",
                    "{\"room\":\"room.guardrail-entry\",\"props\":[{"
                    + "\"object\":\"" + propObjectId + "\","
                    + "\"position\":[0,-2],\"rotation\":0}]}"
                },
                {
                    "guardrail.entry.decor",
                    "{\"room\":\"room.guardrail-entry\",\"tiles\":[{"
                    + "\"object\":\"tile.floor-industrial\",\"fill\":{"
                    + "\"from\":[0,0],\"to\":[1,1]}}],"
                    + "\"background\":[],\"foreground\":[]}"
                },
                {
                    "guardrail.entry.encounter",
                    "{\"room\":\"room.guardrail-entry\","
                    + "\"completion\":\"all-enemies\","
                    + "\"optional_enemy_ids\":[],\"door_rules\":[{"
                    + "\"match\":{\"exit_type\":\"progression\"},"
                    + "\"open_when\":\"room-complete\"}]}"
                },
                {
                    "guardrail.terminal.layout",
                    "{\"room\":\"room.guardrail-terminal\",\"order\":1,"
                    + "\"display_name\":\"GUARDRAIL EXIT\",\"bounds\":{"
                    + "\"center\":[0,0],\"size\":[12,8]},\"spawns\":[{"
                    + "\"kind\":\"forward-entry\",\"position\":[-4,0],"
                    + "\"rotation\":0}],\"doors\":[{"
                    + "\"object\":\"door.room-standard\",\"position\":[5,0],"
                    + "\"rotation\":0,\"link\":{\"kind\":\"final-exit\","
                    + "\"exit_type\":\"progression\"}}]}"
                },
                {
                    "guardrail.terminal.enemies",
                    "{\"room\":\"room.guardrail-terminal\",\"enemies\":[]}"
                },
                {
                    "guardrail.terminal.props",
                    "{\"room\":\"room.guardrail-terminal\",\"props\":[]}"
                },
                {
                    "guardrail.terminal.decor",
                    "{\"room\":\"room.guardrail-terminal\",\"tiles\":[],"
                    + "\"background\":[],\"foreground\":[]}"
                },
                {
                    "guardrail.terminal.encounter",
                    "{\"room\":\"room.guardrail-terminal\","
                    + "\"completion\":\"all-enemies\","
                    + "\"optional_enemy_ids\":[],\"door_rules\":[{"
                    + "\"match\":{\"link_kind\":\"final-exit\"},"
                    + "\"open_when\":\"room-complete\"}]}"
                },
            };
            return new RoomContentJsonPackageV1(manifest, documents);
        }

        private static EnemyCatalogRegistryV1 EnemyRegistry()
        {
            return new EnemyCatalogRegistryV1(
                Ids(
                    "enemy-movement.mobile-positioning",
                    "enemy-movement.pursuit",
                    "enemy-movement.stationary"),
                Ids(
                    "enemy-decision.ranged-standard",
                    "enemy-decision.contact-standard"),
                new[]
                {
                    new EnemyAttackCapabilityRegistrationV1(
                        Id("enemy-attack.ranged-projectile"),
                        EnemyAttackParameterKindsV1.Projectile,
                        EnemyAttackParameterKindsV1.Projectile),
                    new EnemyAttackCapabilityRegistrationV1(
                        Id("enemy-attack.contact"),
                        EnemyAttackParameterKindsV1.Melee,
                        EnemyAttackParameterKindsV1.Melee),
                },
                Ids("enemy-special.locked-commitment"),
                Ids("presentation.enemy-guardrail-scout"),
                Ids("projectile.enemy-blaster"),
                Ids("damage.kinetic", "damage.impact"),
                Ids("xp.enemy-light"),
                Ids("drop.enemy-none"));
        }

        private static RuntimeObservedFactV1 Kill(string factId, long tick)
        {
            return new RuntimeObservedFactV1(
                factId,
                "fact.enemy-killed",
                "participant.guardrail-player",
                tick);
        }

        private static StableId[] Ids(params string[] values)
        {
            return values.Select(Id).ToArray();
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static string FirstEnemyIssue(EnemyCatalogImportResultV1 result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code + ":" + result.Issues[0].Path;
        }

        private static string FirstRoomIssue(RoomContentImportResultV1 result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code + ":" + result.Issues[0].Path;
        }

        private static string FirstAccessIssue(RoomAccessImportResultV1 result)
        {
            return result.Issues.Count == 0
                ? string.Empty
                : result.Issues[0].Code + ":" + result.Issues[0].Path;
        }

        private sealed class FixedClockV1 : IAuthoritativeEventClockV1
        {
            private readonly long unixSeconds;

            public FixedClockV1(long unixSeconds)
            {
                this.unixSeconds = unixSeconds;
            }

            public long GetCurrentUnixTimeSeconds()
            {
                return unixSeconds;
            }
        }
    }
}
#endif

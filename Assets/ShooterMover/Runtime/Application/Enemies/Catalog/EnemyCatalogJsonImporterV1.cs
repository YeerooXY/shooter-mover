using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.Application.Enemies.Catalog
{
    public static partial class EnemyCatalogJsonImporterV1
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(
                typeof(CatalogDtoV1),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true,
                });

        public static EnemyCatalogImportResultV1 Import(
            string json,
            IEnemyCatalogRegistryV1 registry)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failure(
                    "enemy-catalog-json-invalid",
                    "$",
                    "Enemy catalog JSON is required.");
            }

            CatalogDtoV1 dto;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    dto = Serializer.ReadObject(stream) as CatalogDtoV1;
                }
            }
            catch (Exception exception)
            {
                if (!(exception is SerializationException)
                    && !(exception is FormatException)
                    && !(exception is InvalidDataContractException))
                {
                    throw;
                }
                return Failure(
                    "enemy-catalog-json-invalid",
                    "$",
                    "Malformed or incomplete enemy catalog JSON: " + exception.Message);
            }

            if (dto == null)
            {
                return Failure(
                    "enemy-catalog-json-invalid",
                    "$",
                    "JSON root must be an object.");
            }

            try
            {
                StableId contentVersion = ParseId(dto.ContentVersion, "$.content_version");
                List<EnemyDefinitionV1> definitions = MapDefinitions(
                    dto.Definitions,
                    dto.SchemaVersion);
                EnemyCatalogValidationResultV1 validation = EnemyCatalogValidatorV1.Validate(
                    dto.SchemaVersion,
                    contentVersion,
                    definitions,
                    registry);
                if (!validation.IsValid)
                    return new EnemyCatalogImportResultV1(null, validation.Issues);
                return new EnemyCatalogImportResultV1(
                    new EnemyCatalogV1(dto.SchemaVersion, contentVersion, definitions),
                    null);
            }
            catch (EnemyCatalogMappingExceptionV1 exception)
            {
                return Failure(exception.Code, exception.Path, exception.Message);
            }
        }

        private static List<EnemyDefinitionV1> MapDefinitions(
            List<DefinitionDtoV1> source,
            int schemaVersion)
        {
            source = Require(source, "$.definitions");
            var definitions = new List<EnemyDefinitionV1>();
            for (int index = 0; index < source.Count; index++)
            {
                string path = "$.definitions[" + index + "]";
                DefinitionDtoV1 dto = Require(source[index], path);
                LevelScalingDtoV1 scaling = Require(
                    dto.LevelScaling,
                    path + ".level_scaling");
                PerceptionDtoV1 perception = Require(
                    dto.Perception,
                    path + ".perception");
                definitions.Add(new EnemyDefinitionV1(
                    ParseId(dto.Id, path + ".id"),
                    ParseId(dto.Presentation, path + ".presentation"),
                    dto.BaseHealth,
                    new EnemyLevelScalingProfileV1(
                        scaling.BaseLevel,
                        scaling.MaximumLevel,
                        scaling.AdditiveHealthPerLevel,
                        scaling.MultiplicativeHealthPerLevel),
                    ParseId(dto.Faction, path + ".faction"),
                    perception.DetectionRadius,
                    perception.VisionArcDegrees,
                    ParseId(dto.MovementPolicy, path + ".movement_policy"),
                    ParseId(dto.DecisionPolicy, path + ".decision_policy"),
                    MapAttacks(dto.Attacks, path + ".attacks", schemaVersion),
                    ParseId(dto.ExperienceProfile, path + ".xp_profile"),
                    ParseId(dto.DropProfile, path + ".drop_profile"),
                    ParseRoomClearRole(dto.RoomClearRole, path + ".room_clear_role"),
                    MapIds(dto.SpecialCapabilities, path + ".special_capabilities")));
            }
            return definitions;
        }

        private static List<EnemyAttackCapabilityDescriptorV1> MapAttacks(
            List<AttackDtoV1> source,
            string path,
            int schemaVersion)
        {
            source = Require(source, path);
            var attacks = new List<EnemyAttackCapabilityDescriptorV1>();
            for (int index = 0; index < source.Count; index++)
            {
                string attackPath = path + "[" + index + "]";
                AttackDtoV1 dto = Require(source[index], attackPath);
                if (schemaVersion <= 1)
                {
                    if (!dto.CooldownSeconds.HasValue)
                    {
                        throw new EnemyCatalogMappingExceptionV1(
                            "enemy-catalog-field-missing",
                            attackPath + ".cooldown_seconds",
                            "Schema-v1 attacks require cooldown_seconds.");
                    }
                    attacks.Add(new EnemyAttackCapabilityDescriptorV1(
                        ParseId(dto.Id, attackPath + ".id"),
                        ParseId(dto.Capability, attackPath + ".capability"),
                        dto.SelectionPriority,
                        dto.AttackArcDegrees,
                        dto.MinimumRange,
                        dto.PreferredRange,
                        dto.MaximumRange,
                        dto.CooldownSeconds.Value,
                        dto.Damage,
                        ParseId(dto.DamageChannel, attackPath + ".damage_channel"),
                        MapLegacyProjectile(dto.Projectile, attackPath + ".projectile"),
                        MapLegacyArea(dto.Area),
                        MapLegacyMelee(dto.Melee)));
                    continue;
                }

                RejectLegacyPatternFields(dto, attackPath);
                attacks.Add(new EnemyAttackCapabilityDescriptorV1(
                    ParseId(dto.Id, attackPath + ".id"),
                    ParseId(dto.Capability, attackPath + ".capability"),
                    dto.SelectionPriority,
                    dto.AttackArcDegrees,
                    dto.MinimumRange,
                    dto.PreferredRange,
                    dto.MaximumRange,
                    dto.Damage,
                    ParseId(dto.DamageChannel, attackPath + ".damage_channel"),
                    MapShootingPattern(dto.ShootingPattern, attackPath + ".shooting_pattern"),
                    MapProjectilePayload(dto.ProjectilePayload, attackPath + ".projectile_payload"),
                    MapMeleePattern(dto.MeleePattern, attackPath + ".melee_pattern")));
            }
            return attacks;
        }

        private static void RejectLegacyPatternFields(AttackDtoV1 dto, string path)
        {
            if (dto.CooldownSeconds.HasValue
                || dto.Projectile != null
                || dto.Area != null
                || dto.Melee != null)
            {
                throw new EnemyCatalogMappingExceptionV1(
                    "enemy-catalog-legacy-attack-shape",
                    path,
                    "Schema-v2 attacks must use shooting_pattern/projectile_payload or melee_pattern.");
            }
        }

        private static EnemyShootingPatternV1 MapShootingPattern(
            ShootingPatternDtoV1 dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyShootingPatternV1(
                dto.ShotsPerSequence,
                dto.IntervalBetweenShotsSeconds,
                dto.ProjectilesPerShot,
                dto.PerShotSpreadDegrees,
                ParseSequenceAimPolicy(dto.SequenceAimPolicy, path + ".sequence_aim_policy"),
                dto.WindUpSeconds,
                dto.PostSequenceRecoverySeconds,
                ParseInterruptionPolicy(dto.InterruptionPolicy, path + ".interruption_policy"));
        }

        private static EnemyProjectilePayloadV1 MapProjectilePayload(
            ProjectilePayloadDtoV1 dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyProjectilePayloadV1(
                ParseId(dto.Profile, path + ".profile"),
                dto.Speed,
                dto.MaximumTravelDistance,
                dto.CollisionRadius,
                dto.Pierce,
                MapAreaPayload(dto.AreaPayload));
        }

        private static EnemyAreaPayloadV1 MapAreaPayload(AreaDtoV1 dto)
        {
            return dto == null
                ? null
                : new EnemyAreaPayloadV1(
                    dto.Radius,
                    dto.DurationSeconds,
                    dto.MaximumTargets);
        }

        private static EnemyMeleePatternV1 MapMeleePattern(
            MeleePatternDtoV1 dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyMeleePatternV1(
                dto.WindUpSeconds,
                dto.ActiveWindowSeconds,
                dto.StrikeCount,
                dto.IntervalBetweenStrikesSeconds,
                dto.ContactRadius,
                dto.LungeDistance,
                ParseMeleeAimCommitPolicy(dto.AimCommitPolicy, path + ".aim_commit_policy"),
                dto.RecoverySeconds,
                dto.HitsPerTarget,
                ParseTerminalOnImpactPolicy(
                    dto.TerminalOnImpactPolicy,
                    path + ".terminal_on_impact_policy"),
                ParseInterruptionPolicy(dto.InterruptionPolicy, path + ".interruption_policy"));
        }

        private static EnemyProjectileAttackParametersV1 MapLegacyProjectile(
            ProjectileDtoV1 dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyProjectileAttackParametersV1(
                ParseId(dto.Profile, path + ".profile"),
                dto.Count,
                dto.Speed,
                dto.MaximumTravelDistance,
                dto.CollisionRadius,
                dto.SpreadDegrees,
                dto.Pierce);
        }

        private static EnemyAreaAttackParametersV1 MapLegacyArea(AreaDtoV1 dto)
        {
            return dto == null
                ? null
                : new EnemyAreaAttackParametersV1(
                    dto.Radius,
                    dto.DurationSeconds,
                    dto.MaximumTargets);
        }

        private static EnemyMeleeAttackParametersV1 MapLegacyMelee(MeleeDtoV1 dto)
        {
            return dto == null
                ? null
                : new EnemyMeleeAttackParametersV1(
                    dto.ContactRadius,
                    dto.PounceDistance,
                    dto.WindUpSeconds,
                    dto.CommitmentSeconds);
        }

        private static EnemySequenceAimPolicyV1 ParseSequenceAimPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "lock-at-sequence-start":
                    return EnemySequenceAimPolicyV1.LockAtSequenceStart;
                case "reaim-each-shot":
                case "track-until-shot":
                    throw UnsupportedV1Policy(path, "sequence aim", value);
                default:
                    throw InvalidPolicy(path, "sequence aim", value);
            }
        }

        private static EnemyAttackInterruptionPolicyV1 ParseInterruptionPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "cancel-pending-on-lifecycle-end":
                    return EnemyAttackInterruptionPolicyV1.CancelPendingOnLifecycleEnd;
                case "complete-committed-sequence":
                    return EnemyAttackInterruptionPolicyV1.CompleteCommittedSequence;
                default:
                    throw InvalidPolicy(path, "interruption", value);
            }
        }

        private static EnemyMeleeAimCommitPolicyV1 ParseMeleeAimCommitPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "lock-at-wind-up":
                    return EnemyMeleeAimCommitPolicyV1.LockAtWindUp;
                case "track-until-active-window":
                case "lock-per-strike":
                    throw UnsupportedV1Policy(path, "melee aim/commit", value);
                default:
                    throw InvalidPolicy(path, "melee aim/commit", value);
            }
        }

        private static EnemyMeleeTerminalOnImpactPolicyV1 ParseTerminalOnImpactPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "continue-sequence":
                    return EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence;
                case "end-sequence-on-any-impact":
                case "end-sequence-on-blocking-impact":
                    throw UnsupportedV1Policy(path, "terminal-on-impact", value);
                default:
                    throw InvalidPolicy(path, "terminal-on-impact", value);
            }
        }

        private static EnemyCatalogMappingExceptionV1 UnsupportedV1Policy(
            string path,
            string policyKind,
            string value)
        {
            return new EnemyCatalogMappingExceptionV1(
                "enemy-catalog-attack-policy-unsupported-v1",
                path,
                "The V1 runtime does not realize the authored "
                    + policyKind
                    + " policy yet: "
                    + (value ?? "<null>"));
        }

        private static EnemyCatalogMappingExceptionV1 InvalidPolicy(
            string path,
            string policyKind,
            string value)
        {
            return new EnemyCatalogMappingExceptionV1(
                "enemy-catalog-attack-policy-invalid",
                path,
                "Unsupported " + policyKind + " policy: " + (value ?? "<null>"));
        }

        private static List<StableId> MapIds(List<string> source, string path)
        {
            var result = new List<StableId>();
            if (source == null) return result;
            for (int index = 0; index < source.Count; index++)
                result.Add(ParseId(source[index], path + "[" + index + "]"));
            return result;
        }

        private static EnemyCatalogRoomClearRoleV1 ParseRoomClearRole(
            string value,
            string path)
        {
            switch (value)
            {
                case "required-enemy":
                    return EnemyCatalogRoomClearRoleV1.RequiredEnemy;
                case "optional-enemy":
                    return EnemyCatalogRoomClearRoleV1.OptionalEnemy;
                case "objective-entity":
                    return EnemyCatalogRoomClearRoleV1.ObjectiveEntity;
                case "does-not-affect-room-clear":
                    return EnemyCatalogRoomClearRoleV1.DoesNotAffectRoomClear;
                default:
                    throw new EnemyCatalogMappingExceptionV1(
                        "enemy-catalog-room-clear-role-invalid",
                        path,
                        "Room-clear role must use one supported canonical value.");
            }
        }

        private static StableId ParseId(string value, string path)
        {
            StableId id;
            if (!StableId.TryParse(value, out id))
            {
                throw new EnemyCatalogMappingExceptionV1(
                    "enemy-catalog-id-invalid",
                    path,
                    "Value must be a canonical StableId.");
            }
            return id;
        }

        private static T Require<T>(T value, string path) where T : class
        {
            if (value == null)
            {
                throw new EnemyCatalogMappingExceptionV1(
                    "enemy-catalog-field-missing",
                    path,
                    "Required value is missing or null.");
            }
            return value;
        }

        private static EnemyCatalogImportResultV1 Failure(
            string code,
            string path,
            string detail)
        {
            return new EnemyCatalogImportResultV1(
                null,
                new[] { new EnemyCatalogIssueV1(code, path, detail) });
        }
    }
}

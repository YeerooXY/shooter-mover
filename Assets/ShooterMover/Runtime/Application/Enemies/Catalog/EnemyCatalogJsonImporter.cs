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
    public static partial class EnemyCatalogJsonImporter
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(
                typeof(CatalogDto),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true,
                });

        public static EnemyCatalogImportResult Import(
            string json,
            IEnemyCatalogRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failure(
                    "enemy-catalog-json-invalid",
                    "$",
                    "Enemy catalog JSON is required.");
            }

            CatalogDto dto;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    dto = Serializer.ReadObject(stream) as CatalogDto;
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
                List<EnemyDefinition> definitions = MapDefinitions(
                    dto.Definitions,
                    dto.SchemaVersion);
                EnemyCatalogValidationResult validation = EnemyCatalogValidator.Validate(
                    dto.SchemaVersion,
                    contentVersion,
                    definitions,
                    registry);
                if (!validation.IsValid)
                    return new EnemyCatalogImportResult(null, validation.Issues);
                return new EnemyCatalogImportResult(
                    new EnemyCatalog(dto.SchemaVersion, contentVersion, definitions),
                    null);
            }
            catch (EnemyCatalogMappingException exception)
            {
                return Failure(exception.Code, exception.Path, exception.Message);
            }
        }

        private static List<EnemyDefinition> MapDefinitions(
            List<DefinitionDto> source,
            int schemaVersion)
        {
            source = Require(source, "$.definitions");
            var definitions = new List<EnemyDefinition>();
            for (int index = 0; index < source.Count; index++)
            {
                string path = "$.definitions[" + index + "]";
                DefinitionDto dto = Require(source[index], path);
                LevelScalingDto scaling = Require(
                    dto.LevelScaling,
                    path + ".level_scaling");
                PerceptionDto perception = Require(
                    dto.Perception,
                    path + ".perception");
                definitions.Add(new EnemyDefinition(
                    ParseId(dto.Id, path + ".id"),
                    ParseId(dto.Presentation, path + ".presentation"),
                    dto.BaseHealth,
                    new EnemyLevelScalingProfile(
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

        private static List<EnemyAttackCapabilityDescriptor> MapAttacks(
            List<AttackDto> source,
            string path,
            int schemaVersion)
        {
            source = Require(source, path);
            var attacks = new List<EnemyAttackCapabilityDescriptor>();
            for (int index = 0; index < source.Count; index++)
            {
                string attackPath = path + "[" + index + "]";
                AttackDto dto = Require(source[index], attackPath);
                if (schemaVersion <= 1)
                {
                    if (!dto.CooldownSeconds.HasValue)
                    {
                        throw new EnemyCatalogMappingException(
                            "enemy-catalog-field-missing",
                            attackPath + ".cooldown_seconds",
                            "Schema-v1 attacks require cooldown_seconds.");
                    }
                    attacks.Add(new EnemyAttackCapabilityDescriptor(
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
                attacks.Add(new EnemyAttackCapabilityDescriptor(
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

        private static void RejectLegacyPatternFields(AttackDto dto, string path)
        {
            if (dto.CooldownSeconds.HasValue
                || dto.Projectile != null
                || dto.Area != null
                || dto.Melee != null)
            {
                throw new EnemyCatalogMappingException(
                    "enemy-catalog-legacy-attack-shape",
                    path,
                    "Schema-v2 attacks must use shooting_pattern/projectile_payload or melee_pattern.");
            }
        }

        private static EnemyShootingPattern MapShootingPattern(
            ShootingPatternDto dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyShootingPattern(
                dto.ShotsPerSequence,
                dto.IntervalBetweenShotsSeconds,
                dto.ProjectilesPerShot,
                dto.PerShotSpreadDegrees,
                ParseSequenceAimPolicy(dto.SequenceAimPolicy, path + ".sequence_aim_policy"),
                dto.WindUpSeconds,
                dto.PostSequenceRecoverySeconds,
                ParseInterruptionPolicy(dto.InterruptionPolicy, path + ".interruption_policy"));
        }

        private static EnemyProjectilePayload MapProjectilePayload(
            ProjectilePayloadDto dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyProjectilePayload(
                ParseId(dto.Profile, path + ".profile"),
                dto.Speed,
                dto.MaximumTravelDistance,
                dto.CollisionRadius,
                dto.Pierce,
                MapAreaPayload(dto.AreaPayload));
        }

        private static EnemyAreaPayload MapAreaPayload(AreaDto dto)
        {
            return dto == null
                ? null
                : new EnemyAreaPayload(
                    dto.Radius,
                    dto.DurationSeconds,
                    dto.MaximumTargets);
        }

        private static EnemyMeleePattern MapMeleePattern(
            MeleePatternDto dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyMeleePattern(
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

        private static EnemyProjectileAttackParameters MapLegacyProjectile(
            ProjectileDto dto,
            string path)
        {
            if (dto == null) return null;
            return new EnemyProjectileAttackParameters(
                ParseId(dto.Profile, path + ".profile"),
                dto.Count,
                dto.Speed,
                dto.MaximumTravelDistance,
                dto.CollisionRadius,
                dto.SpreadDegrees,
                dto.Pierce);
        }

        private static EnemyAreaAttackParameters MapLegacyArea(AreaDto dto)
        {
            return dto == null
                ? null
                : new EnemyAreaAttackParameters(
                    dto.Radius,
                    dto.DurationSeconds,
                    dto.MaximumTargets);
        }

        private static EnemyMeleeAttackParameters MapLegacyMelee(MeleeDto dto)
        {
            return dto == null
                ? null
                : new EnemyMeleeAttackParameters(
                    dto.ContactRadius,
                    dto.PounceDistance,
                    dto.WindUpSeconds,
                    dto.CommitmentSeconds);
        }

        private static EnemySequenceAimPolicy ParseSequenceAimPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "lock-at-sequence-start":
                    return EnemySequenceAimPolicy.LockAtSequenceStart;
                case "reaim-each-shot":
                case "track-until-shot":
                    throw UnsupportedV1Policy(path, "sequence aim", value);
                default:
                    throw InvalidPolicy(path, "sequence aim", value);
            }
        }

        private static EnemyAttackInterruptionPolicy ParseInterruptionPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "cancel-pending-on-lifecycle-end":
                    return EnemyAttackInterruptionPolicy.CancelPendingOnLifecycleEnd;
                case "complete-committed-sequence":
                    return EnemyAttackInterruptionPolicy.CompleteCommittedSequence;
                default:
                    throw InvalidPolicy(path, "interruption", value);
            }
        }

        private static EnemyMeleeAimCommitPolicy ParseMeleeAimCommitPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "lock-at-wind-up":
                    return EnemyMeleeAimCommitPolicy.LockAtWindUp;
                case "track-until-active-window":
                case "lock-per-strike":
                    throw UnsupportedV1Policy(path, "melee aim/commit", value);
                default:
                    throw InvalidPolicy(path, "melee aim/commit", value);
            }
        }

        private static EnemyMeleeTerminalOnImpactPolicy ParseTerminalOnImpactPolicy(
            string value,
            string path)
        {
            switch (value)
            {
                case "continue-sequence":
                    return EnemyMeleeTerminalOnImpactPolicy.ContinueSequence;
                case "end-sequence-on-any-impact":
                case "end-sequence-on-blocking-impact":
                    throw UnsupportedV1Policy(path, "terminal-on-impact", value);
                default:
                    throw InvalidPolicy(path, "terminal-on-impact", value);
            }
        }

        private static EnemyCatalogMappingException UnsupportedV1Policy(
            string path,
            string policyKind,
            string value)
        {
            return new EnemyCatalogMappingException(
                "enemy-catalog-attack-policy-unsupported-v1",
                path,
                "The V1 runtime does not realize the authored "
                    + policyKind
                    + " policy yet: "
                    + (value ?? "<null>"));
        }

        private static EnemyCatalogMappingException InvalidPolicy(
            string path,
            string policyKind,
            string value)
        {
            return new EnemyCatalogMappingException(
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

        private static EnemyCatalogRoomClearRole ParseRoomClearRole(
            string value,
            string path)
        {
            switch (value)
            {
                case "required-enemy":
                    return EnemyCatalogRoomClearRole.RequiredEnemy;
                case "optional-enemy":
                    return EnemyCatalogRoomClearRole.OptionalEnemy;
                case "objective-entity":
                    return EnemyCatalogRoomClearRole.ObjectiveEntity;
                case "does-not-affect-room-clear":
                    return EnemyCatalogRoomClearRole.DoesNotAffectRoomClear;
                default:
                    throw new EnemyCatalogMappingException(
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
                throw new EnemyCatalogMappingException(
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
                throw new EnemyCatalogMappingException(
                    "enemy-catalog-field-missing",
                    path,
                    "Required value is missing or null.");
            }
            return value;
        }

        private static EnemyCatalogImportResult Failure(
            string code,
            string path,
            string detail)
        {
            return new EnemyCatalogImportResult(
                null,
                new[] { new EnemyCatalogIssue(code, path, detail) });
        }
    }
}

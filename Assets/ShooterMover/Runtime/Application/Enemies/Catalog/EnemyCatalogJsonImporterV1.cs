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
                List<EnemyDefinitionV1> definitions = MapDefinitions(dto.Definitions);
                EnemyCatalogValidationResultV1 validation = EnemyCatalogValidatorV1.Validate(
                    dto.SchemaVersion,
                    contentVersion,
                    definitions,
                    registry);
                if (!validation.IsValid)
                {
                    return new EnemyCatalogImportResultV1(null, validation.Issues);
                }

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
            List<DefinitionDtoV1> source)
        {
            source = Require(source, "$.definitions");
            var definitions = new List<EnemyDefinitionV1>();
            for (int index = 0; index < source.Count; index++)
            {
                string path = "$.definitions[" + index + "]";
                DefinitionDtoV1 dto = Require(source[index], path);
                LevelScalingDtoV1 scaling = Require(dto.LevelScaling, path + ".level_scaling");
                PerceptionDtoV1 perception = Require(dto.Perception, path + ".perception");
                definitions.Add(
                    new EnemyDefinitionV1(
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
                        MapAttacks(dto.Attacks, path + ".attacks"),
                        ParseId(dto.ExperienceProfile, path + ".xp_profile"),
                        ParseId(dto.DropProfile, path + ".drop_profile"),
                        ParseRoomClearRole(dto.RoomClearRole, path + ".room_clear_role"),
                        MapIds(dto.SpecialCapabilities, path + ".special_capabilities")));
            }
            return definitions;
        }

        private static List<EnemyAttackCapabilityDescriptorV1> MapAttacks(
            List<AttackDtoV1> source,
            string path)
        {
            source = Require(source, path);
            var attacks = new List<EnemyAttackCapabilityDescriptorV1>();
            for (int index = 0; index < source.Count; index++)
            {
                string attackPath = path + "[" + index + "]";
                AttackDtoV1 dto = Require(source[index], attackPath);
                attacks.Add(
                    new EnemyAttackCapabilityDescriptorV1(
                        ParseId(dto.Id, attackPath + ".id"),
                        ParseId(dto.Capability, attackPath + ".capability"),
                        dto.SelectionPriority,
                        dto.AttackArcDegrees,
                        dto.MinimumRange,
                        dto.PreferredRange,
                        dto.MaximumRange,
                        dto.CooldownSeconds,
                        dto.Damage,
                        ParseId(dto.DamageChannel, attackPath + ".damage_channel"),
                        MapProjectile(dto.Projectile, attackPath + ".projectile"),
                        MapArea(dto.Area),
                        MapMelee(dto.Melee)));
            }
            return attacks;
        }

        private static EnemyProjectileAttackParametersV1 MapProjectile(
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

        private static EnemyAreaAttackParametersV1 MapArea(AreaDtoV1 dto)
        {
            return dto == null
                ? null
                : new EnemyAreaAttackParametersV1(
                    dto.Radius,
                    dto.DurationSeconds,
                    dto.MaximumTargets);
        }

        private static EnemyMeleeAttackParametersV1 MapMelee(MeleeDtoV1 dto)
        {
            return dto == null
                ? null
                : new EnemyMeleeAttackParametersV1(
                    dto.ContactRadius,
                    dto.PounceDistance,
                    dto.WindUpSeconds,
                    dto.CommitmentSeconds);
        }

        private static List<StableId> MapIds(List<string> source, string path)
        {
            var result = new List<StableId>();
            if (source == null) return result;
            for (int index = 0; index < source.Count; index++)
            {
                result.Add(ParseId(source[index], path + "[" + index + "]"));
            }
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

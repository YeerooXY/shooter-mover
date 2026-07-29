using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Guns.Catalog
{
    public static partial class GunCatalogJsonImporter
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(
                typeof(CatalogDto),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true,
                });

        public static GunCatalogImportResult Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failure("$", "JSON is required.");
            }

            CatalogDto dto;
            try
            {
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
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

                return Failure("$", "Malformed or incomplete gun catalog JSON: " + exception.Message);
            }

            if (dto == null)
            {
                return Failure("$", "JSON root must be an object.");
            }

            try
            {
                GunCatalogRules rules = MapRules(dto.Rules);
                GunCatalogInputs inputs = MapInputs(dto.Inputs);
                Dictionary<string, GunArchetypeDefinition> archetypes = MapArchetypes(dto.Archetypes);
                List<GunFamilyDefinition> families = MapFamilies(dto.Families);
                List<GunDefinitionData> definitions = MapDefinitions(dto.Definitions);

                GunCatalogValidationResult validation = GunCatalogValidator.Validate(
                    dto.Version,
                    dto.Status,
                    rules,
                    inputs,
                    archetypes,
                    families,
                    definitions);
                if (!validation.IsValid)
                {
                    return new GunCatalogImportResult(null, validation.Issues);
                }

                return new GunCatalogImportResult(
                    new GunCatalog(
                        dto.Version,
                        dto.Status,
                        rules,
                        inputs,
                        archetypes,
                        families,
                        definitions),
                    null);
            }
            catch (CatalogMappingException exception)
            {
                return new GunCatalogImportResult(
                    null,
                    new[]
                    {
                        new GunCatalogIssue(exception.Code, exception.Path, exception.Message),
                    });
            }
        }

        private static GunCatalogImportResult Failure(string path, string detail)
        {
            return new GunCatalogImportResult(
                null,
                new[]
                {
                    new GunCatalogIssue(GunCatalogIssueCode.InvalidJson, path, detail),
                });
        }

        private static GunCatalogRules MapRules(RulesDto dto)
        {
            Require(dto, "$.rules");
            return new GunCatalogRules(
                dto.FixedStatsPerDefinition,
                dto.OrdinaryMarkGap,
                null,
                Require(dto.DamageTypes, "$.rules.damage_types"),
                dto.MaxAugments,
                dto.NoRecoil,
                dto.NoSpinUp,
                dto.NoHeatGeneration);
        }

        private static GunCatalogInputs MapInputs(InputsDto dto)
        {
            Require(dto, "$.inputs");
            Dictionary<string, GunRarityInput> rarities =
                new Dictionary<string, GunRarityInput>(StringComparer.Ordinal);
            Dictionary<string, RarityDto> source = Require(dto.Rarities, "$.inputs.rarities");
            foreach (KeyValuePair<string, RarityDto> pair in source)
            {
                Require(pair.Value, "$.inputs.rarities." + pair.Key);
                rarities.Add(
                    pair.Key,
                    new GunRarityInput(
                        pair.Key,
                        pair.Value.Weight,
                        0,
                        pair.Value.EarlyTail,
                        pair.Value.LateTail));
            }

            return new GunCatalogInputs(
                0d,
                0d,
                0d,
                0d,
                rarities);
        }

        private static Dictionary<string, GunArchetypeDefinition> MapArchetypes(
            Dictionary<string, ArchetypeDto> source)
        {
            source = Require(source, "$.archetypes");
            Dictionary<string, GunArchetypeDefinition> result =
                new Dictionary<string, GunArchetypeDefinition>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ArchetypeDto> pair in source)
            {
                ArchetypeDto dto = Require(pair.Value, "$.archetypes." + pair.Key);
                result.Add(
                    pair.Key,
                    new GunArchetypeDefinition(
                        pair.Key,
                        dto.Description,
                        0d,
                        dto.FireRate,
                        dto.Projectiles,
                        dto.Burst,
                        dto.Spread,
                        dto.Speed,
                        dto.Range,
                        0d,
                        0d,
                        0d,
                        dto.Radius,
                        dto.DotDuration,
                        dto.PoolRadius,
                        dto.PoolDuration,
                        dto.Pierce,
                        dto.ChainTargets,
                        dto.ChainRange,
                        dto.Knockback,
                        dto.PowerCost));
            }
            return result;
        }

        private static List<GunFamilyDefinition> MapFamilies(List<FamilyDto> source)
        {
            source = Require(source, "$.families");
            List<GunFamilyDefinition> result = new List<GunFamilyDefinition>();
            for (int index = 0; index < source.Count; index++)
            {
                string path = "$.families[" + index + "]";
                FamilyDto dto = Require(source[index], path);
                result.Add(
                    new GunFamilyDefinition(
                        dto.FamilyId,
                        dto.DisplayName,
                        dto.Archetype,
                        dto.DamageType,
                        dto.BuildAffinity,
                        dto.Mk1Peak,
                        dto.GapMk1To2,
                        dto.GapMk2To3,
                        dto.MaxPlannedMark,
                        dto.Mk1Rarity,
                        dto.Mk2Rarity,
                        dto.Mk3Rarity,
                        dto.DefinitionWeightModifier,
                        dto.AcquisitionClass,
                        dto.PrimaryEffect,
                        dto.Notes,
                        ParseAvailability(dto.Availability, path + ".Availability"),
                        MapArtReferences(dto.SideProfileArtReference, dto.SideProfileArtReferences, path)));
            }
            return result;
        }

        private static List<GunDefinitionData> MapDefinitions(List<DefinitionDto> source)
        {
            source = Require(source, "$.definitions");
            List<GunDefinitionData> result = new List<GunDefinitionData>();
            for (int index = 0; index < source.Count; index++)
            {
                string path = "$.definitions[" + index + "]";
                DefinitionDto dto = Require(source[index], path);
                result.Add(
                    new GunDefinitionData(
                        dto.DefinitionId,
                        dto.DisplayName,
                        dto.FamilyId,
                        dto.Mark,
                        dto.DamageType,
                        dto.Archetype,
                        dto.BuildAffinity,
                        dto.FirstAppearance,
                        dto.PeakDropLevel,
                        0,
                        dto.Rarity,
                        dto.RarityWeight,
                        dto.DefinitionWeightModifier,
                        dto.FinalBaseWeight,
                        dto.EarlyTail,
                        dto.LateTail,
                        dto.AcquisitionClass,
                        ParseYesNo(dto.TopBoxOnly, path + ".TopBoxOnly"),
                        dto.CraftingRoute,
                        0d,
                        0d,
                        0d,
                        0d,
                        0d,
                        0d,
                        dto.FireRate,
                        dto.ProjectilesPerTrigger,
                        dto.BurstCount,
                        dto.DamagePerProjectile,
                        dto.SpreadDegrees,
                        dto.ProjectileSpeed,
                        dto.Range,
                        dto.Pierce,
                        dto.ExplosionRadius,
                        dto.AreaDamagePerTrigger,
                        dto.DotDps,
                        dto.DotDuration,
                        dto.PoolRadius,
                        dto.PoolDuration,
                        dto.ChainTargets,
                        dto.ChainRange,
                        dto.Knockback,
                        dto.PowerCost,
                        dto.HealingPerSecond,
                        dto.PrimaryEffect,
                        dto.Notes,
                        ParseAvailability(dto.Availability, path + ".Availability"),
                        MapArtReferences(dto.SideProfileArtReference, dto.SideProfileArtReferences, path)));
            }
            return result;
        }

        private static IReadOnlyList<string> MapArtReferences(
            string single,
            List<string> multiple,
            string path)
        {
            if (!string.IsNullOrEmpty(single) && multiple != null)
            {
                throw new CatalogMappingException(
                    GunCatalogIssueCode.InvalidArtReference,
                    path,
                    "Use either SideProfileArtReference or SideProfileArtReferences, not both.");
            }

            if (!string.IsNullOrEmpty(single))
            {
                return new[] { single };
            }
            return multiple == null ? new string[0] : multiple;
        }

        private static GunCatalogAvailability ParseAvailability(string value, string path)
        {
            if (string.IsNullOrEmpty(value) || string.Equals(value, "Live", StringComparison.Ordinal))
            {
                return GunCatalogAvailability.Live;
            }
            if (string.Equals(value, "PreviewOnly", StringComparison.Ordinal))
            {
                return GunCatalogAvailability.PreviewOnly;
            }

            throw new CatalogMappingException(
                GunCatalogIssueCode.InvalidAvailability,
                path,
                "Availability must be 'Live' or 'PreviewOnly'.");
        }

        private static bool ParseYesNo(string value, string path)
        {
            if (string.Equals(value, "Yes", StringComparison.Ordinal))
            {
                return true;
            }
            if (string.Equals(value, "No", StringComparison.Ordinal))
            {
                return false;
            }

            throw new CatalogMappingException(
                GunCatalogIssueCode.InvalidValue,
                path,
                "TopBoxOnly must be the exact string 'Yes' or 'No'.");
        }

        private static T Require<T>(T value, string path) where T : class
        {
            if (value == null)
            {
                throw new CatalogMappingException(
                    GunCatalogIssueCode.MissingRequiredField,
                    path,
                    "Required value is missing or null.");
            }
            return value;
        }
    }
}

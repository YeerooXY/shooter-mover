using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static partial class WeaponCatalogJsonImporter
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(
                typeof(CatalogDto),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true,
                });

        public static WeaponCatalogImportResult Import(string json)
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

                return Failure("$", "Malformed or incomplete weapon catalog JSON: " + exception.Message);
            }

            if (dto == null)
            {
                return Failure("$", "JSON root must be an object.");
            }

            try
            {
                WeaponCatalogRules rules = MapRules(dto.Rules);
                WeaponCatalogInputs inputs = MapInputs(dto.Inputs);
                Dictionary<string, WeaponArchetypeDefinition> archetypes = MapArchetypes(dto.Archetypes);
                List<WeaponFamilyDefinition> families = MapFamilies(dto.Families);
                List<WeaponDefinitionData> definitions = MapDefinitions(dto.Definitions);

                WeaponCatalogValidationResult validation = WeaponCatalogValidator.Validate(
                    dto.Version,
                    dto.Status,
                    rules,
                    inputs,
                    archetypes,
                    families,
                    definitions);
                if (!validation.IsValid)
                {
                    return new WeaponCatalogImportResult(null, validation.Issues);
                }

                return new WeaponCatalogImportResult(
                    new WeaponCatalog(
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
                return new WeaponCatalogImportResult(
                    null,
                    new[]
                    {
                        new WeaponCatalogIssue(exception.Code, exception.Path, exception.Message),
                    });
            }
        }

        private static WeaponCatalogImportResult Failure(string path, string detail)
        {
            return new WeaponCatalogImportResult(
                null,
                new[]
                {
                    new WeaponCatalogIssue(WeaponCatalogIssueCode.InvalidJson, path, detail),
                });
        }

        private static WeaponCatalogRules MapRules(RulesDto dto)
        {
            Require(dto, "$.rules");
            return new WeaponCatalogRules(
                dto.FixedStatsPerDefinition,
                dto.OrdinaryMarkGap,
                Require(dto.ApexPowerAnchors, "$.rules.apex_power_anchors"),
                Require(dto.DamageTypes, "$.rules.damage_types"),
                dto.MaxAugments,
                dto.NoRecoil,
                dto.NoSpinUp,
                dto.NoHeatGeneration);
        }

        private static WeaponCatalogInputs MapInputs(InputsDto dto)
        {
            Require(dto, "$.inputs");
            Dictionary<string, WeaponRarityInput> rarities =
                new Dictionary<string, WeaponRarityInput>(StringComparer.Ordinal);
            Dictionary<string, RarityDto> source = Require(dto.Rarities, "$.inputs.rarities");
            foreach (KeyValuePair<string, RarityDto> pair in source)
            {
                Require(pair.Value, "$.inputs.rarities." + pair.Key);
                rarities.Add(
                    pair.Key,
                    new WeaponRarityInput(
                        pair.Key,
                        pair.Value.Weight,
                        pair.Value.PowerBonus,
                        pair.Value.EarlyTail,
                        pair.Value.LateTail));
            }

            return new WeaponCatalogInputs(
                dto.BaseDps,
                dto.Growth1To30,
                dto.Growth31To70,
                dto.Growth71Plus,
                rarities);
        }

        private static Dictionary<string, WeaponArchetypeDefinition> MapArchetypes(
            Dictionary<string, ArchetypeDto> source)
        {
            source = Require(source, "$.archetypes");
            Dictionary<string, WeaponArchetypeDefinition> result =
                new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ArchetypeDto> pair in source)
            {
                ArchetypeDto dto = Require(pair.Value, "$.archetypes." + pair.Key);
                result.Add(
                    pair.Key,
                    new WeaponArchetypeDefinition(
                        pair.Key,
                        dto.Description,
                        dto.DpsFactor,
                        dto.FireRate,
                        dto.Projectiles,
                        dto.Burst,
                        dto.Spread,
                        dto.Speed,
                        dto.Range,
                        dto.DirectShare,
                        dto.AreaShare,
                        dto.DotShare,
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

        private static List<WeaponFamilyDefinition> MapFamilies(List<FamilyDto> source)
        {
            source = Require(source, "$.families");
            List<WeaponFamilyDefinition> result = new List<WeaponFamilyDefinition>();
            for (int index = 0; index < source.Count; index++)
            {
                string path = "$.families[" + index + "]";
                FamilyDto dto = Require(source[index], path);
                result.Add(
                    new WeaponFamilyDefinition(
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

        private static List<WeaponDefinitionData> MapDefinitions(List<DefinitionDto> source)
        {
            source = Require(source, "$.definitions");
            List<WeaponDefinitionData> result = new List<WeaponDefinitionData>();
            for (int index = 0; index < source.Count; index++)
            {
                string path = "$.definitions[" + index + "]";
                DefinitionDto dto = Require(source[index], path);
                result.Add(
                    new WeaponDefinitionData(
                        dto.DefinitionId,
                        dto.DisplayName,
                        dto.FamilyId,
                        dto.Mark,
                        dto.DamageType,
                        dto.Archetype,
                        dto.BuildAffinity,
                        dto.FirstAppearance,
                        dto.PeakDropLevel,
                        dto.PowerAnchor,
                        dto.Rarity,
                        dto.RarityWeight,
                        dto.DefinitionWeightModifier,
                        dto.FinalBaseWeight,
                        dto.EarlyTail,
                        dto.LateTail,
                        dto.AcquisitionClass,
                        ParseYesNo(dto.TopBoxOnly, path + ".TopBoxOnly"),
                        dto.CraftingRoute,
                        dto.ArchetypeDpsFactor,
                        dto.PowerIndex,
                        dto.TargetDps,
                        dto.DirectShare,
                        dto.AreaShare,
                        dto.DotShare,
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
                    WeaponCatalogIssueCode.InvalidArtReference,
                    path,
                    "Use either SideProfileArtReference or SideProfileArtReferences, not both.");
            }

            if (!string.IsNullOrEmpty(single))
            {
                return new[] { single };
            }
            return multiple == null ? new string[0] : multiple;
        }

        private static WeaponCatalogAvailability ParseAvailability(string value, string path)
        {
            if (string.IsNullOrEmpty(value) || string.Equals(value, "Live", StringComparison.Ordinal))
            {
                return WeaponCatalogAvailability.Live;
            }
            if (string.Equals(value, "PreviewOnly", StringComparison.Ordinal))
            {
                return WeaponCatalogAvailability.PreviewOnly;
            }

            throw new CatalogMappingException(
                WeaponCatalogIssueCode.InvalidAvailability,
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
                WeaponCatalogIssueCode.InvalidValue,
                path,
                "TopBoxOnly must be the exact string 'Yes' or 'No'.");
        }

        private static T Require<T>(T value, string path) where T : class
        {
            if (value == null)
            {
                throw new CatalogMappingException(
                    WeaponCatalogIssueCode.MissingRequiredField,
                    path,
                    "Required value is missing or null.");
            }
            return value;
        }

    }
}

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
        private sealed class CatalogMappingException : Exception
        {
            public CatalogMappingException(WeaponCatalogIssueCode code, string path, string message)
                : base(message)
            {
                Code = code;
                Path = path;
            }

            public WeaponCatalogIssueCode Code { get; private set; }
            public string Path { get; private set; }
        }

        [DataContract]
        private sealed class CatalogDto
        {
            [DataMember(Name = "version", IsRequired = true, Order = 0)]
            public string Version;
            [DataMember(Name = "status", IsRequired = true, Order = 1)]
            public string Status;
            [DataMember(Name = "rules", IsRequired = true, Order = 2)]
            public RulesDto Rules;
            [DataMember(Name = "inputs", IsRequired = true, Order = 3)]
            public InputsDto Inputs;
            [DataMember(Name = "archetypes", IsRequired = true, Order = 4)]
            public Dictionary<string, ArchetypeDto> Archetypes;
            [DataMember(Name = "families", IsRequired = true, Order = 5)]
            public List<FamilyDto> Families;
            [DataMember(Name = "definitions", IsRequired = true, Order = 6)]
            public List<DefinitionDto> Definitions;
        }

        [DataContract]
        private sealed class RulesDto
        {
            [DataMember(Name = "fixed_stats_per_definition", IsRequired = true, Order = 0)]
            public bool FixedStatsPerDefinition;
            [DataMember(Name = "runtime_level_scaling", IsRequired = true, Order = 1)]
            public bool RuntimeLevelScaling;
            [DataMember(Name = "ordinary_mark_gap", IsRequired = true, Order = 2)]
            public string OrdinaryMarkGap;
            [DataMember(Name = "apex_power_anchors", IsRequired = true, Order = 3)]
            public List<int> ApexPowerAnchors;
            [DataMember(Name = "damage_types", IsRequired = true, Order = 4)]
            public List<string> DamageTypes;
            [DataMember(Name = "max_augments", IsRequired = true, Order = 5)]
            public int MaxAugments;
            [DataMember(Name = "no_recoil", IsRequired = true, Order = 6)]
            public bool NoRecoil;
            [DataMember(Name = "no_spin_up", IsRequired = true, Order = 7)]
            public bool NoSpinUp;
            [DataMember(Name = "no_heat_generation", IsRequired = true, Order = 8)]
            public bool NoHeatGeneration;
        }

        [DataContract]
        private sealed class InputsDto
        {
            [DataMember(Name = "base_dps", IsRequired = true, Order = 0)]
            public double BaseDps;
            [DataMember(Name = "growth_1_30", IsRequired = true, Order = 1)]
            public double Growth1To30;
            [DataMember(Name = "growth_31_70", IsRequired = true, Order = 2)]
            public double Growth31To70;
            [DataMember(Name = "growth_71_plus", IsRequired = true, Order = 3)]
            public double Growth71Plus;
            [DataMember(Name = "rarities", IsRequired = true, Order = 4)]
            public Dictionary<string, RarityDto> Rarities;
        }

        [DataContract]
        private sealed class RarityDto
        {
            [DataMember(Name = "weight", IsRequired = true, Order = 0)]
            public double Weight;
            [DataMember(Name = "power_bonus", IsRequired = true, Order = 1)]
            public int PowerBonus;
            [DataMember(Name = "early_tail", IsRequired = true, Order = 2)]
            public double EarlyTail;
            [DataMember(Name = "late_tail", IsRequired = true, Order = 3)]
            public double LateTail;
        }

        [DataContract]
        private sealed class ArchetypeDto
        {
            [DataMember(Name = "description", IsRequired = true, Order = 0)] public string Description;
            [DataMember(Name = "dps_factor", IsRequired = true, Order = 1)] public double DpsFactor;
            [DataMember(Name = "fire_rate", IsRequired = true, Order = 2)] public double FireRate;
            [DataMember(Name = "projectiles", IsRequired = true, Order = 3)] public int Projectiles;
            [DataMember(Name = "burst", IsRequired = true, Order = 4)] public int Burst;
            [DataMember(Name = "spread", IsRequired = true, Order = 5)] public double Spread;
            [DataMember(Name = "speed", IsRequired = true, Order = 6)] public double Speed;
            [DataMember(Name = "range", IsRequired = true, Order = 7)] public double Range;
            [DataMember(Name = "direct_share", IsRequired = true, Order = 8)] public double DirectShare;
            [DataMember(Name = "area_share", IsRequired = true, Order = 9)] public double AreaShare;
            [DataMember(Name = "dot_share", IsRequired = true, Order = 10)] public double DotShare;
            [DataMember(Name = "radius", IsRequired = true, Order = 11)] public double Radius;
            [DataMember(Name = "dot_duration", IsRequired = true, Order = 12)] public double DotDuration;
            [DataMember(Name = "pool_radius", IsRequired = true, Order = 13)] public double PoolRadius;
            [DataMember(Name = "pool_duration", IsRequired = true, Order = 14)] public double PoolDuration;
            [DataMember(Name = "pierce", IsRequired = true, Order = 15)] public int Pierce;
            [DataMember(Name = "chain_targets", IsRequired = true, Order = 16)] public int ChainTargets;
            [DataMember(Name = "chain_range", IsRequired = true, Order = 17)] public double ChainRange;
            [DataMember(Name = "knockback", IsRequired = true, Order = 18)] public double Knockback;
            [DataMember(Name = "power_cost", IsRequired = true, Order = 19)] public double PowerCost;
        }

        [DataContract]
        private sealed class FamilyDto
        {
            [DataMember(Name = "FamilyId", IsRequired = true, Order = 0)] public string FamilyId;
            [DataMember(Name = "DisplayName", IsRequired = true, Order = 1)] public string DisplayName;
            [DataMember(Name = "Archetype", IsRequired = true, Order = 2)] public string Archetype;
            [DataMember(Name = "DamageType", IsRequired = true, Order = 3)] public string DamageType;
            [DataMember(Name = "BuildAffinity", IsRequired = true, Order = 4)] public string BuildAffinity;
            [DataMember(Name = "MK1Peak", IsRequired = true, Order = 5)] public int Mk1Peak;
            [DataMember(Name = "GapMK1To2", IsRequired = true, Order = 6)] public int GapMk1To2;
            [DataMember(Name = "GapMK2To3", IsRequired = true, Order = 7)] public int GapMk2To3;
            [DataMember(Name = "MaxPlannedMark", IsRequired = true, Order = 8)] public int MaxPlannedMark;
            [DataMember(Name = "MK1Rarity", IsRequired = true, Order = 9)] public string Mk1Rarity;
            [DataMember(Name = "MK2Rarity", IsRequired = true, Order = 10)] public string Mk2Rarity;
            [DataMember(Name = "MK3Rarity", IsRequired = true, Order = 11)] public string Mk3Rarity;
            [DataMember(Name = "DefinitionWeightModifier", IsRequired = true, Order = 12)] public double DefinitionWeightModifier;
            [DataMember(Name = "AcquisitionClass", IsRequired = true, Order = 13)] public string AcquisitionClass;
            [DataMember(Name = "PrimaryEffect", IsRequired = true, Order = 14)] public string PrimaryEffect;
            [DataMember(Name = "Notes", IsRequired = true, Order = 15)] public string Notes;
            [DataMember(Name = "Availability", EmitDefaultValue = false, Order = 16)] public string Availability;
            [DataMember(Name = "SideProfileArtReference", EmitDefaultValue = false, Order = 17)] public string SideProfileArtReference;
            [DataMember(Name = "SideProfileArtReferences", EmitDefaultValue = false, Order = 18)] public List<string> SideProfileArtReferences;
        }

        [DataContract]
        private sealed class DefinitionDto
        {
            [DataMember(Name = "DefinitionId", IsRequired = true, Order = 0)] public string DefinitionId;
            [DataMember(Name = "DisplayName", IsRequired = true, Order = 1)] public string DisplayName;
            [DataMember(Name = "FamilyId", IsRequired = true, Order = 2)] public string FamilyId;
            [DataMember(Name = "Mark", IsRequired = true, Order = 3)] public int Mark;
            [DataMember(Name = "DamageType", IsRequired = true, Order = 4)] public string DamageType;
            [DataMember(Name = "Archetype", IsRequired = true, Order = 5)] public string Archetype;
            [DataMember(Name = "BuildAffinity", IsRequired = true, Order = 6)] public string BuildAffinity;
            [DataMember(Name = "FirstAppearance", IsRequired = true, Order = 7)] public int FirstAppearance;
            [DataMember(Name = "PeakDropLevel", IsRequired = true, Order = 8)] public int PeakDropLevel;
            [DataMember(Name = "PowerAnchor", IsRequired = true, Order = 9)] public int PowerAnchor;
            [DataMember(Name = "Rarity", IsRequired = true, Order = 10)] public string Rarity;
            [DataMember(Name = "RarityWeight", IsRequired = true, Order = 11)] public double RarityWeight;
            [DataMember(Name = "DefinitionWeightModifier", IsRequired = true, Order = 12)] public double DefinitionWeightModifier;
            [DataMember(Name = "FinalBaseWeight", IsRequired = true, Order = 13)] public double FinalBaseWeight;
            [DataMember(Name = "EarlyTail", IsRequired = true, Order = 14)] public double EarlyTail;
            [DataMember(Name = "LateTail", IsRequired = true, Order = 15)] public double LateTail;
            [DataMember(Name = "AcquisitionClass", IsRequired = true, Order = 16)] public string AcquisitionClass;
            [DataMember(Name = "TopBoxOnly", IsRequired = true, Order = 17)] public string TopBoxOnly;
            [DataMember(Name = "CraftingRoute", IsRequired = true, Order = 18)] public string CraftingRoute;
            [DataMember(Name = "ArchetypeDPSFactor", IsRequired = true, Order = 19)] public double ArchetypeDpsFactor;
            [DataMember(Name = "PowerIndex", IsRequired = true, Order = 20)] public double PowerIndex;
            [DataMember(Name = "TargetDPS", IsRequired = true, Order = 21)] public double TargetDps;
            [DataMember(Name = "DirectShare", IsRequired = true, Order = 22)] public double DirectShare;
            [DataMember(Name = "AreaShare", IsRequired = true, Order = 23)] public double AreaShare;
            [DataMember(Name = "DoTShare", IsRequired = true, Order = 24)] public double DotShare;
            [DataMember(Name = "FireRate", IsRequired = true, Order = 25)] public double FireRate;
            [DataMember(Name = "ProjectilesPerTrigger", IsRequired = true, Order = 26)] public int ProjectilesPerTrigger;
            [DataMember(Name = "BurstCount", IsRequired = true, Order = 27)] public int BurstCount;
            [DataMember(Name = "DamagePerProjectile", IsRequired = true, Order = 28)] public double DamagePerProjectile;
            [DataMember(Name = "SpreadDegrees", IsRequired = true, Order = 29)] public double SpreadDegrees;
            [DataMember(Name = "ProjectileSpeed", IsRequired = true, Order = 30)] public double ProjectileSpeed;
            [DataMember(Name = "Range", IsRequired = true, Order = 31)] public double Range;
            [DataMember(Name = "Pierce", IsRequired = true, Order = 32)] public int Pierce;
            [DataMember(Name = "ExplosionRadius", IsRequired = true, Order = 33)] public double ExplosionRadius;
            [DataMember(Name = "AreaDamagePerTrigger", IsRequired = true, Order = 34)] public double AreaDamagePerTrigger;
            [DataMember(Name = "DoTDPS", IsRequired = true, Order = 35)] public double DotDps;
            [DataMember(Name = "DoTDuration", IsRequired = true, Order = 36)] public double DotDuration;
            [DataMember(Name = "PoolRadius", IsRequired = true, Order = 37)] public double PoolRadius;
            [DataMember(Name = "PoolDuration", IsRequired = true, Order = 38)] public double PoolDuration;
            [DataMember(Name = "ChainTargets", IsRequired = true, Order = 39)] public int ChainTargets;
            [DataMember(Name = "ChainRange", IsRequired = true, Order = 40)] public double ChainRange;
            [DataMember(Name = "Knockback", IsRequired = true, Order = 41)] public double Knockback;
            [DataMember(Name = "PowerCost", IsRequired = true, Order = 42)] public double PowerCost;
            [DataMember(Name = "HealingPerSecond", IsRequired = true, Order = 43)] public double HealingPerSecond;
            [DataMember(Name = "PrimaryEffect", IsRequired = true, Order = 44)] public string PrimaryEffect;
            [DataMember(Name = "Notes", IsRequired = true, Order = 45)] public string Notes;
            [DataMember(Name = "Availability", EmitDefaultValue = false, Order = 46)] public string Availability;
            [DataMember(Name = "SideProfileArtReference", EmitDefaultValue = false, Order = 47)] public string SideProfileArtReference;
            [DataMember(Name = "SideProfileArtReferences", EmitDefaultValue = false, Order = 48)] public List<string> SideProfileArtReferences;
        }
    }
}

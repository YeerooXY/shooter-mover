using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShooterMover.Domain.Weapons.Catalog
{
    public enum WeaponCatalogAvailability
    {
        Live = 0,
        PreviewOnly = 1,
    }

    public enum WeaponCatalogContentFilter
    {
        LiveOnly = 0,
        PreviewOnly = 1,
        All = 2,
    }

    /// <summary>
    /// Catalog-wide weapon invariants. Every weapon definition contains its final base
    /// statistics. Equipping the same weapon on a different character never recalculates
    /// those statistics from the character's current level.
    /// </summary>
    public sealed class WeaponCatalogRules
    {
        private readonly ReadOnlyCollection<int> _apexPowerAnchors;
        private readonly ReadOnlyCollection<string> _damageTypes;

        public WeaponCatalogRules(
            bool fixedStatsPerDefinition,
            string ordinaryMarkGap,
            IEnumerable<int> apexPowerAnchors,
            IEnumerable<string> damageTypes,
            int maxAugments,
            bool noRecoil,
            bool noSpinUp,
            bool noHeatGeneration)
        {
            FixedStatsPerDefinition = fixedStatsPerDefinition;
            OrdinaryMarkGap = ordinaryMarkGap ?? string.Empty;
            _apexPowerAnchors = Copy(apexPowerAnchors);
            _damageTypes = Copy(damageTypes);
            MaxAugments = maxAugments;
            NoRecoil = noRecoil;
            NoSpinUp = noSpinUp;
            NoHeatGeneration = noHeatGeneration;
        }

        public bool FixedStatsPerDefinition { get; private set; }
        public string OrdinaryMarkGap { get; private set; }
        public IReadOnlyList<int> ApexPowerAnchors { get { return _apexPowerAnchors; } }
        public IReadOnlyList<string> DamageTypes { get { return _damageTypes; } }
        public int MaxAugments { get; private set; }
        public bool NoRecoil { get; private set; }
        public bool NoSpinUp { get; private set; }
        public bool NoHeatGeneration { get; private set; }

        private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(values == null ? new List<T>() : new List<T>(values));
        }
    }

    public sealed class WeaponRarityInput
    {
        public WeaponRarityInput(
            string rarity,
            double weight,
            int powerBonus,
            double earlyTail,
            double lateTail)
        {
            Rarity = rarity ?? string.Empty;
            Weight = weight;
            PowerBonus = powerBonus;
            EarlyTail = earlyTail;
            LateTail = lateTail;
        }

        public string Rarity { get; private set; }
        public double Weight { get; private set; }
        public int PowerBonus { get; private set; }
        public double EarlyTail { get; private set; }
        public double LateTail { get; private set; }
    }

    public sealed class WeaponCatalogInputs
    {
        private readonly ReadOnlyDictionary<string, WeaponRarityInput> _rarities;

        public WeaponCatalogInputs(
            double baseDps,
            double growth1To30,
            double growth31To70,
            double growth71Plus,
            IDictionary<string, WeaponRarityInput> rarities)
        {
            BaseDps = baseDps;
            Growth1To30 = growth1To30;
            Growth31To70 = growth31To70;
            Growth71Plus = growth71Plus;
            _rarities = new ReadOnlyDictionary<string, WeaponRarityInput>(
                rarities == null
                    ? new Dictionary<string, WeaponRarityInput>(StringComparer.Ordinal)
                    : new Dictionary<string, WeaponRarityInput>(rarities, StringComparer.Ordinal));
        }

        public double BaseDps { get; private set; }
        public double Growth1To30 { get; private set; }
        public double Growth31To70 { get; private set; }
        public double Growth71Plus { get; private set; }
        public IReadOnlyDictionary<string, WeaponRarityInput> Rarities { get { return _rarities; } }

        public double CalculatePowerIndex(int powerAnchor)
        {
            if (powerAnchor < 1)
            {
                return 0.0;
            }

            double value = 100.0;
            for (int level = 2; level <= powerAnchor; level++)
            {
                double growth = level <= 30
                    ? Growth1To30
                    : level <= 70
                        ? Growth31To70
                        : Growth71Plus;
                value *= 1.0 + growth;
            }

            return value;
        }
    }

    public sealed class WeaponArchetypeDefinition
    {
        public WeaponArchetypeDefinition(
            string archetypeId,
            string description,
            double dpsFactor,
            double fireRate,
            int projectiles,
            int burst,
            double spread,
            double speed,
            double range,
            double directShare,
            double areaShare,
            double dotShare,
            double radius,
            double dotDuration,
            double poolRadius,
            double poolDuration,
            int pierce,
            int chainTargets,
            double chainRange,
            double knockback,
            double powerCost)
        {
            ArchetypeId = archetypeId ?? string.Empty;
            Description = description ?? string.Empty;
            DpsFactor = dpsFactor;
            FireRate = fireRate;
            Projectiles = projectiles;
            Burst = burst;
            Spread = spread;
            Speed = speed;
            Range = range;
            DirectShare = directShare;
            AreaShare = areaShare;
            DotShare = dotShare;
            Radius = radius;
            DotDuration = dotDuration;
            PoolRadius = poolRadius;
            PoolDuration = poolDuration;
            Pierce = pierce;
            ChainTargets = chainTargets;
            ChainRange = chainRange;
            Knockback = knockback;
            PowerCost = powerCost;
        }

        public string ArchetypeId { get; private set; }
        public string Description { get; private set; }
        public double DpsFactor { get; private set; }
        public double FireRate { get; private set; }
        public int Projectiles { get; private set; }
        public int Burst { get; private set; }
        public double Spread { get; private set; }
        public double Speed { get; private set; }
        public double Range { get; private set; }
        public double DirectShare { get; private set; }
        public double AreaShare { get; private set; }
        public double DotShare { get; private set; }
        public double Radius { get; private set; }
        public double DotDuration { get; private set; }
        public double PoolRadius { get; private set; }
        public double PoolDuration { get; private set; }
        public int Pierce { get; private set; }
        public int ChainTargets { get; private set; }
        public double ChainRange { get; private set; }
        public double Knockback { get; private set; }
        public double PowerCost { get; private set; }
    }

    public sealed class WeaponFamilyDefinition
    {
        private readonly ReadOnlyCollection<string> _sideProfileArtReferences;

        public WeaponFamilyDefinition(
            string familyId,
            string displayName,
            string archetype,
            string damageType,
            string buildAffinity,
            int mk1Peak,
            int gapMk1To2,
            int gapMk2To3,
            int maxPlannedMark,
            string mk1Rarity,
            string mk2Rarity,
            string mk3Rarity,
            double definitionWeightModifier,
            string acquisitionClass,
            string primaryEffect,
            string notes,
            WeaponCatalogAvailability availability,
            IEnumerable<string> sideProfileArtReferences)
        {
            FamilyId = familyId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Archetype = archetype ?? string.Empty;
            DamageType = damageType ?? string.Empty;
            BuildAffinity = buildAffinity ?? string.Empty;
            Mk1Peak = mk1Peak;
            GapMk1To2 = gapMk1To2;
            GapMk2To3 = gapMk2To3;
            MaxPlannedMark = maxPlannedMark;
            Mk1Rarity = mk1Rarity ?? string.Empty;
            Mk2Rarity = mk2Rarity ?? string.Empty;
            Mk3Rarity = mk3Rarity ?? string.Empty;
            DefinitionWeightModifier = definitionWeightModifier;
            AcquisitionClass = acquisitionClass ?? string.Empty;
            PrimaryEffect = primaryEffect ?? string.Empty;
            Notes = notes ?? string.Empty;
            Availability = availability;
            _sideProfileArtReferences = new ReadOnlyCollection<string>(
                sideProfileArtReferences == null
                    ? new List<string>()
                    : new List<string>(sideProfileArtReferences));
        }

        public string FamilyId { get; private set; }
        public string DisplayName { get; private set; }
        public string Archetype { get; private set; }
        public string DamageType { get; private set; }
        public string BuildAffinity { get; private set; }
        public int Mk1Peak { get; private set; }
        public int GapMk1To2 { get; private set; }
        public int GapMk2To3 { get; private set; }
        public int MaxPlannedMark { get; private set; }
        public string Mk1Rarity { get; private set; }
        public string Mk2Rarity { get; private set; }
        public string Mk3Rarity { get; private set; }
        public double DefinitionWeightModifier { get; private set; }
        public string AcquisitionClass { get; private set; }
        public string PrimaryEffect { get; private set; }
        public string Notes { get; private set; }
        public WeaponCatalogAvailability Availability { get; private set; }
        public IReadOnlyList<string> SideProfileArtReferences { get { return _sideProfileArtReferences; } }

        public string RarityForMark(int mark)
        {
            switch (mark)
            {
                case 1: return Mk1Rarity;
                case 2: return Mk2Rarity;
                case 3: return Mk3Rarity;
                default: return string.Empty;
            }
        }
    }

}

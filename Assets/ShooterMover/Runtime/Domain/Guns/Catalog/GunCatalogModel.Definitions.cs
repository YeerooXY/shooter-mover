using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Domain.Guns.Catalog
{
    public sealed class GunDefinitionData
    {
        private readonly ReadOnlyCollection<string> _sideProfileArtReferences;

        public GunDefinitionData(
            string definitionId,
            string displayName,
            string familyId,
            int mark,
            string damageType,
            string archetype,
            string buildAffinity,
            int firstAppearance,
            int peakDropLevel,
            int powerAnchor,
            string rarity,
            double rarityWeight,
            double definitionWeightModifier,
            double finalBaseWeight,
            double earlyTail,
            double lateTail,
            string acquisitionClass,
            bool topBoxOnly,
            string craftingRoute,
            double archetypeDpsFactor,
            double powerIndex,
            double targetDps,
            double directShare,
            double areaShare,
            double dotShare,
            double fireRate,
            int projectilesPerTrigger,
            int burstCount,
            double damagePerProjectile,
            double spreadDegrees,
            double projectileSpeed,
            double range,
            int pierce,
            double explosionRadius,
            double areaDamagePerTrigger,
            double dotDps,
            double dotDuration,
            double poolRadius,
            double poolDuration,
            int chainTargets,
            double chainRange,
            double knockback,
            double powerCost,
            double healingPerSecond,
            string primaryEffect,
            string notes,
            GunCatalogAvailability availability,
            IEnumerable<string> sideProfileArtReferences)
        {
            DefinitionId = definitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            FamilyId = familyId ?? string.Empty;
            Mark = mark;
            DamageType = damageType ?? string.Empty;
            Archetype = archetype ?? string.Empty;
            BuildAffinity = buildAffinity ?? string.Empty;
            FirstAppearance = firstAppearance;
            PeakDropLevel = peakDropLevel;
            PowerAnchor = powerAnchor;
            Rarity = rarity ?? string.Empty;
            RarityWeight = rarityWeight;
            DefinitionWeightModifier = definitionWeightModifier;
            FinalBaseWeight = finalBaseWeight;
            EarlyTail = earlyTail;
            LateTail = lateTail;
            AcquisitionClass = acquisitionClass ?? string.Empty;
            TopBoxOnly = topBoxOnly;
            CraftingRoute = craftingRoute ?? string.Empty;
            ArchetypeDpsFactor = archetypeDpsFactor;
            PowerIndex = powerIndex;
            DirectShare = directShare;
            AreaShare = areaShare;
            DotShare = dotShare;
            FireRate = fireRate;
            ProjectilesPerTrigger = projectilesPerTrigger;
            BurstCount = burstCount;
            DamagePerProjectile = damagePerProjectile;
            SpreadDegrees = spreadDegrees;
            ProjectileSpeed = projectileSpeed;
            Range = range;
            Pierce = pierce;
            ExplosionRadius = explosionRadius;
            AreaDamagePerTrigger = areaDamagePerTrigger;
            DotDps = dotDps;
            DotDuration = dotDuration;
            PoolRadius = poolRadius;
            PoolDuration = poolDuration;
            ChainTargets = chainTargets;
            ChainRange = chainRange;
            Knockback = knockback;
            PowerCost = powerCost;
            HealingPerSecond = healingPerSecond;
            PrimaryEffect = primaryEffect ?? string.Empty;
            Notes = notes ?? string.Empty;
            Availability = availability;
            _sideProfileArtReferences = new ReadOnlyCollection<string>(
                sideProfileArtReferences == null
                    ? new List<string>()
                    : new List<string>(sideProfileArtReferences));
        }

        public string DefinitionId { get; private set; }
        public string DisplayName { get; private set; }
        public string FamilyId { get; private set; }
        public int Mark { get; private set; }
        public string DamageType { get; private set; }
        public string Archetype { get; private set; }
        public string BuildAffinity { get; private set; }
        public int FirstAppearance { get; private set; }
        public int PeakDropLevel { get; private set; }
        public int PowerAnchor { get; private set; }
        public string Rarity { get; private set; }
        public double RarityWeight { get; private set; }
        public double DefinitionWeightModifier { get; private set; }
        public double FinalBaseWeight { get; private set; }
        public double EarlyTail { get; private set; }
        public double LateTail { get; private set; }
        public string AcquisitionClass { get; private set; }
        public bool TopBoxOnly { get; private set; }
        public string CraftingRoute { get; private set; }
        public double ArchetypeDpsFactor { get; private set; }
        public double PowerIndex { get; private set; }
        public double TargetDps
        {
            get
            {
                return DamagePerProjectile
                    * FireRate
                    * ProjectilesPerTrigger
                    * BurstCount;
            }
        }
        public double DirectShare { get; private set; }
        public double AreaShare { get; private set; }
        public double DotShare { get; private set; }
        public double FireRate { get; private set; }
        public int ProjectilesPerTrigger { get; private set; }
        public int BurstCount { get; private set; }
        public double DamagePerProjectile { get; private set; }
        public double SpreadDegrees { get; private set; }
        public double ProjectileSpeed { get; private set; }
        public double Range { get; private set; }
        public int Pierce { get; private set; }
        public double ExplosionRadius { get; private set; }
        public double AreaDamagePerTrigger { get; private set; }
        public double DotDps { get; private set; }
        public double DotDuration { get; private set; }
        public double PoolRadius { get; private set; }
        public double PoolDuration { get; private set; }
        public int ChainTargets { get; private set; }
        public double ChainRange { get; private set; }
        public double Knockback { get; private set; }
        public double PowerCost { get; private set; }
        public double HealingPerSecond { get; private set; }
        public string PrimaryEffect { get; private set; }
        public string Notes { get; private set; }
        public GunCatalogAvailability Availability { get; private set; }
        public IReadOnlyList<string> SideProfileArtReferences { get { return _sideProfileArtReferences; } }
    }

    public sealed class GunCatalog
    {
        private readonly ReadOnlyDictionary<string, GunArchetypeDefinition> _archetypes;
        private readonly ReadOnlyDictionary<string, GunFamilyDefinition> _familiesById;
        private readonly ReadOnlyDictionary<string, GunDefinitionData> _definitionsById;
        private readonly ReadOnlyCollection<GunFamilyDefinition> _families;
        private readonly ReadOnlyCollection<GunDefinitionData> _definitions;

        public GunCatalog(
            string version,
            string status,
            GunCatalogRules rules,
            GunCatalogInputs inputs,
            IDictionary<string, GunArchetypeDefinition> archetypes,
            IEnumerable<GunFamilyDefinition> families,
            IEnumerable<GunDefinitionData> definitions)
        {
            Version = version ?? string.Empty;
            Status = status ?? string.Empty;
            Rules = rules;
            Inputs = inputs;

            Dictionary<string, GunArchetypeDefinition> sortedArchetypes =
                new Dictionary<string, GunArchetypeDefinition>(StringComparer.Ordinal);
            List<string> archetypeIds = archetypes == null
                ? new List<string>()
                : new List<string>(archetypes.Keys);
            archetypeIds.Sort(StringComparer.Ordinal);
            for (int index = 0; index < archetypeIds.Count; index++)
            {
                string id = archetypeIds[index];
                sortedArchetypes.Add(id, archetypes[id]);
            }
            _archetypes = new ReadOnlyDictionary<string, GunArchetypeDefinition>(sortedArchetypes);

            List<GunFamilyDefinition> sortedFamilies = families == null
                ? new List<GunFamilyDefinition>()
                : new List<GunFamilyDefinition>(families);
            sortedFamilies.Sort(delegate(GunFamilyDefinition left, GunFamilyDefinition right)
            {
                return string.CompareOrdinal(left.FamilyId, right.FamilyId);
            });
            _families = new ReadOnlyCollection<GunFamilyDefinition>(sortedFamilies);
            Dictionary<string, GunFamilyDefinition> familyMap =
                new Dictionary<string, GunFamilyDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < sortedFamilies.Count; index++)
            {
                familyMap.Add(sortedFamilies[index].FamilyId, sortedFamilies[index]);
            }
            _familiesById = new ReadOnlyDictionary<string, GunFamilyDefinition>(familyMap);

            List<GunDefinitionData> sortedDefinitions = definitions == null
                ? new List<GunDefinitionData>()
                : new List<GunDefinitionData>(definitions);
            sortedDefinitions.Sort(delegate(GunDefinitionData left, GunDefinitionData right)
            {
                return string.CompareOrdinal(left.DefinitionId, right.DefinitionId);
            });
            _definitions = new ReadOnlyCollection<GunDefinitionData>(sortedDefinitions);
            Dictionary<string, GunDefinitionData> definitionMap =
                new Dictionary<string, GunDefinitionData>(StringComparer.Ordinal);
            for (int index = 0; index < sortedDefinitions.Count; index++)
            {
                definitionMap.Add(sortedDefinitions[index].DefinitionId, sortedDefinitions[index]);
            }
            _definitionsById = new ReadOnlyDictionary<string, GunDefinitionData>(definitionMap);
            Fingerprint = GunCatalogFingerprint.Calculate(this);
        }

        public string Version { get; private set; }
        public string Status { get; private set; }
        public GunCatalogRules Rules { get; private set; }
        public GunCatalogInputs Inputs { get; private set; }
        public IReadOnlyDictionary<string, GunArchetypeDefinition> Archetypes { get { return _archetypes; } }
        public IReadOnlyList<GunFamilyDefinition> Families { get { return _families; } }
        public IReadOnlyList<GunDefinitionData> Definitions { get { return _definitions; } }
        public string Fingerprint { get; private set; }

        public bool TryGetFamily(string familyId, out GunFamilyDefinition family)
        {
            return _familiesById.TryGetValue(familyId ?? string.Empty, out family);
        }

        public bool TryGetDefinition(string definitionId, out GunDefinitionData definition)
        {
            return _definitionsById.TryGetValue(definitionId ?? string.Empty, out definition);
        }

        public IReadOnlyList<GunFamilyDefinition> GetFamilies(GunCatalogContentFilter filter)
        {
            List<GunFamilyDefinition> result = new List<GunFamilyDefinition>();
            for (int index = 0; index < _families.Count; index++)
            {
                GunFamilyDefinition family = _families[index];
                if (Matches(filter, family.Availability))
                {
                    result.Add(family);
                }
            }
            return new ReadOnlyCollection<GunFamilyDefinition>(result);
        }

        public IReadOnlyList<GunDefinitionData> GetDefinitions(GunCatalogContentFilter filter)
        {
            List<GunDefinitionData> result = new List<GunDefinitionData>();
            for (int index = 0; index < _definitions.Count; index++)
            {
                GunDefinitionData definition = _definitions[index];
                GunFamilyDefinition family;
                GunCatalogAvailability effective = definition.Availability;
                if (_familiesById.TryGetValue(definition.FamilyId, out family)
                    && family.Availability == GunCatalogAvailability.PreviewOnly)
                {
                    effective = GunCatalogAvailability.PreviewOnly;
                }

                if (Matches(filter, effective))
                {
                    result.Add(definition);
                }
            }
            return new ReadOnlyCollection<GunDefinitionData>(result);
        }

        private static bool Matches(GunCatalogContentFilter filter, GunCatalogAvailability availability)
        {
            return filter == GunCatalogContentFilter.All
                || (filter == GunCatalogContentFilter.LiveOnly && availability == GunCatalogAvailability.Live)
                || (filter == GunCatalogContentFilter.PreviewOnly && availability == GunCatalogAvailability.PreviewOnly);
        }
    }
}

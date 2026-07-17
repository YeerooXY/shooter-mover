using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class LootboxGeneratedItemV1
    {
        public LootboxGeneratedItemV1(
            ProductionStrongboxTierV1 tier,
            EquipmentInstance equipment,
            string definitionDisplayName,
            string familyId,
            int mark)
        {
            Tier = tier ?? throw new ArgumentNullException(nameof(tier));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            DefinitionDisplayName = definitionDisplayName ?? equipment.DefinitionId.ToString();
            FamilyId = familyId ?? string.Empty;
            Mark = mark;
        }

        public ProductionStrongboxTierV1 Tier { get; }
        public EquipmentInstance Equipment { get; }
        public string DefinitionDisplayName { get; }
        public string FamilyId { get; }
        public int Mark { get; }
    }

    public sealed class LootboxOddsEntryV1 : IComparable<LootboxOddsEntryV1>
    {
        public LootboxOddsEntryV1(string key, long count, long total)
        {
            Key = key ?? string.Empty;
            Count = count;
            Percentage = total <= 0L ? 0.0 : 100.0 * count / total;
        }

        public string Key { get; }
        public long Count { get; }
        public double Percentage { get; }

        public int CompareTo(LootboxOddsEntryV1 other)
        {
            if (ReferenceEquals(other, null)) return 1;
            int byCount = other.Count.CompareTo(Count);
            return byCount != 0 ? byCount : string.CompareOrdinal(Key, other.Key);
        }
    }

    public sealed class LootboxOddsReportV1
    {
        public LootboxOddsReportV1(
            int sampleCount,
            IEnumerable<LootboxOddsEntryV1> itemOdds,
            IEnumerable<LootboxOddsEntryV1> slotOdds,
            IEnumerable<LootboxOddsEntryV1> augmentLevelOdds,
            IEnumerable<LootboxOddsEntryV1> itemLevelDeltaOdds,
            int rejectedRolls)
        {
            SampleCount = sampleCount;
            ItemOdds = Copy(itemOdds);
            SlotOdds = Copy(slotOdds);
            AugmentLevelOdds = Copy(augmentLevelOdds);
            ItemLevelDeltaOdds = Copy(itemLevelDeltaOdds);
            RejectedRolls = rejectedRolls;
        }

        public int SampleCount { get; }
        public IReadOnlyList<LootboxOddsEntryV1> ItemOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> SlotOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> AugmentLevelOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> ItemLevelDeltaOdds { get; }
        public int RejectedRolls { get; }

        private static IReadOnlyList<LootboxOddsEntryV1> Copy(IEnumerable<LootboxOddsEntryV1> values)
        {
            var result = new List<LootboxOddsEntryV1>(values ?? Array.Empty<LootboxOddsEntryV1>());
            result.Sort();
            return new ReadOnlyCollection<LootboxOddsEntryV1>(result);
        }
    }

    /// <summary>
    /// Editor-only composition for ordered lootbox opening. Generation is delegated
    /// to the production BOX equipment resolver and GEN service. Accepted equipment
    /// is admitted through the real player-holdings authority.
    /// </summary>
    public sealed class LootboxSimulatorRuntimeV1
    {
        public const long TemporarySaleValue = 1000L;

        private static readonly StableId DifficultyNormal = StableId.Parse("difficulty.normal");
        private static readonly StableId QualityCommon = StableId.Parse("quality.common");
        private static readonly StableId QualityRare = StableId.Parse("quality.rare");
        private static readonly StableId QualityExceptional = StableId.Parse("quality.exceptional");
        private static readonly StableId HoldingsAuthority = StableId.Parse("holdings.lootbox-simulator");
        private static readonly StableId SourceId = StableId.Parse("source.lootbox-simulator");

        private readonly WeaponCatalog weaponCatalog;
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly Dictionary<StableId, WeaponDefinitionData> weaponByEquipmentId;
        private readonly StrongboxDefinitionCatalogV1 strongboxDefinitions;
        private readonly StrongboxEquipmentGenerationResolverV1 resolver;
        private readonly PlayerHoldingsService holdings;
        private readonly List<EquipmentInstance> acceptedInventory = new List<EquipmentInstance>();
        private readonly HashSet<StableId> decidedItems = new HashSet<StableId>();

        private LootboxSimulatorRuntimeV1(
            WeaponCatalog weaponCatalog,
            EquipmentCatalog equipmentCatalog,
            Dictionary<StableId, WeaponDefinitionData> weaponByEquipmentId,
            StrongboxDefinitionCatalogV1 strongboxDefinitions,
            StrongboxEquipmentGenerationResolverV1 resolver)
        {
            this.weaponCatalog = weaponCatalog;
            this.equipmentCatalog = equipmentCatalog;
            this.weaponByEquipmentId = weaponByEquipmentId;
            this.strongboxDefinitions = strongboxDefinitions;
            this.resolver = resolver;
            holdings = new PlayerHoldingsService(
                HoldingsAuthority,
                1000000L,
                new SimulatorEquipmentValidator(equipmentCatalog));
        }

        public WeaponCatalog WeaponCatalog { get { return weaponCatalog; } }
        public EquipmentCatalog EquipmentCatalog { get { return equipmentCatalog; } }
        public IReadOnlyList<EquipmentInstance> AcceptedInventory
        {
            get { return new ReadOnlyCollection<EquipmentInstance>(acceptedInventory); }
        }
        public long Cash { get; private set; }

        public static bool TryCreate(
            string weaponCatalogJson,
            out LootboxSimulatorRuntimeV1 runtime,
            out string diagnostic)
        {
            runtime = null;
            diagnostic = string.Empty;
            WeaponCatalogImportResult import = WeaponCatalogJsonImporter.Import(weaponCatalogJson);
            if (!import.IsSuccess)
            {
                diagnostic = import.Issues.Count == 0
                    ? "Weapon catalog import failed."
                    : import.Issues[0].Path + ": " + import.Issues[0].Detail;
                return false;
            }

            try
            {
                Dictionary<StableId, WeaponDefinitionData> map;
                EquipmentCatalog equipment = BuildEquipmentCatalog(import.Catalog, out map);
                var definitions = new List<StrongboxDefinitionV1>();
                var bindings = new List<StrongboxEquipmentGenerationDefinitionV1>();
                for (int index = 0; index < ProductionStrongboxCatalogV1.Tiers.Count; index++)
                {
                    ProductionStrongboxTierV1 tier = ProductionStrongboxCatalogV1.Tiers[index];
                    EquipmentGenerationPolicyV1 policy = BuildPolicy(tier, import.Catalog, map);
                    StrongboxDefinitionV1 definition = tier.CreateDefinition(policy.PolicyId);
                    definitions.Add(definition);
                    bindings.Add(new StrongboxEquipmentGenerationDefinitionV1(
                        tier.TierStableId,
                        tier.CreatePowerBudgetPolicy(),
                        policy,
                        equipment));
                }

                runtime = new LootboxSimulatorRuntimeV1(
                    import.Catalog,
                    equipment,
                    map,
                    new StrongboxDefinitionCatalogV1(definitions),
                    new StrongboxEquipmentGenerationResolverV1(
                        new RewardGenerationServiceV1(),
                        new StrongboxEquipmentGenerationDefinitionCatalogV1(bindings)));
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.Message;
                return false;
            }
        }

        public LootboxGeneratedItemV1 Generate(
            int tierNumber,
            int playerLevel,
            ulong rootSeed,
            int queueOrdinal)
        {
            if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (queueOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(queueOrdinal));

            ProductionStrongboxTierV1 tier = ProductionStrongboxCatalogV1.GetByNumber(tierNumber);
            StrongboxDefinitionV1 definition;
            if (!strongboxDefinitions.TryGet(tier.TierStableId, out definition))
            {
                throw new InvalidOperationException("Missing strongbox definition " + tier.TierStableId + ".");
            }

            int effectiveLevel = tier.ResolveEffectivePlayerLevel(playerLevel);
            ProgressionContext context = ProgressionContext.Create(
                effectiveLevel,
                effectiveLevel,
                DifficultyNormal,
                1,
                Array.Empty<StableId>());
            ulong seed = DeriveSeed(rootSeed, queueOrdinal);
            StableId instanceId = DynamicId("box-instance", rootSeed, queueOrdinal);
            StrongboxInstanceContextV1 boxContext = StrongboxInstanceContextV1.Create(
                instanceId,
                tier.TierStableId,
                seed,
                DeterministicRandom.AlgorithmVersion1,
                context,
                SourceId,
                DynamicId("collection", rootSeed, queueOrdinal),
                definition.Fingerprint);
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                DynamicId("run", rootSeed, 0),
                instanceId,
                DynamicId("box-operation", rootSeed, queueOrdinal),
                DynamicId("box-commitment", rootSeed, queueOrdinal),
                definition.BaseRewardProfile.ProfileStableId,
                definition.Fingerprint);
            RewardGrantV1 grant = RewardGrantV1.Create(
                DynamicId("equipment-grant", rootSeed, queueOrdinal),
                RewardGrantKindV1.EquipmentReference,
                StableId.Parse("equipment-category.weapon"),
                1L);

            IReadOnlyList<EquipmentInstance> generated;
            string rejection;
            if (!resolver.TryResolve(
                definition,
                boxContext,
                operation,
                grant,
                out generated,
                out rejection))
            {
                throw new InvalidOperationException("Strongbox generation rejected: " + rejection);
            }
            if (generated.Count != 1)
            {
                throw new InvalidOperationException("The opener expects exactly one equipment item per box.");
            }

            EquipmentInstance item = generated[0];
            WeaponDefinitionData source;
            if (!weaponByEquipmentId.TryGetValue(item.DefinitionId, out source))
            {
                throw new InvalidOperationException("Generated equipment is missing its weapon-catalog projection.");
            }
            return new LootboxGeneratedItemV1(
                tier,
                item,
                source.DisplayName,
                source.FamilyId,
                source.Mark);
        }

        public PlayerHoldingsMutationStatusV1 Keep(
            LootboxGeneratedItemV1 generated,
            int decisionOrdinal)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            if (decisionOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(decisionOrdinal));
            if (decidedItems.Contains(generated.Equipment.InstanceId))
            {
                return PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange;
            }

            HoldingProvenanceV1 provenance = HoldingProvenanceV1.Create(
                StableId.Create("lootbox-grant", decisionOrdinal.ToString("D6")),
                SourceId);
            PlayerHoldingsMutationResultV1 result = holdings.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    StableId.Create("lootbox-keep-transaction", decisionOrdinal.ToString("D6")),
                    StableId.Create("lootbox-keep-operation", decisionOrdinal.ToString("D6")),
                    HoldingsAuthority,
                    generated.Equipment,
                    provenance,
                    holdings.Sequence));
            if (result.Status == PlayerHoldingsMutationStatusV1.Applied)
            {
                acceptedInventory.Add(generated.Equipment);
                decidedItems.Add(generated.Equipment.InstanceId);
            }
            return result.Status;
        }

        public bool Sell(LootboxGeneratedItemV1 generated)
        {
            if (generated == null) throw new ArgumentNullException(nameof(generated));
            if (!decidedItems.Add(generated.Equipment.InstanceId))
            {
                return false;
            }
            // TODO(ECONOMY): replace the temporary fixed sale value with the real item valuation service.
            Cash = checked(Cash + TemporarySaleValue);
            return true;
        }

        public LootboxOddsReportV1 CalculateOdds(
            int tierNumber,
            int playerLevel,
            ulong rootSeed,
            int sampleCount)
        {
            if (sampleCount < 1) throw new ArgumentOutOfRangeException(nameof(sampleCount));
            var items = new Dictionary<string, long>(StringComparer.Ordinal);
            var slots = new Dictionary<string, long>(StringComparer.Ordinal);
            var levels = new Dictionary<string, long>(StringComparer.Ordinal);
            var deltas = new Dictionary<string, long>(StringComparer.Ordinal);
            long augmentTotal = 0L;
            int rejected = 0;

            for (int index = 0; index < sampleCount; index++)
            {
                try
                {
                    LootboxGeneratedItemV1 generated = Generate(
                        tierNumber,
                        playerLevel,
                        rootSeed,
                        index);
                    Add(items, generated.DefinitionDisplayName, 1L);
                    Add(slots, generated.Equipment.Augments.Count.ToString(), 1L);
                    Add(
                        deltas,
                        (generated.Equipment.ItemLevel - playerLevel).ToString("+0;-0;0"),
                        1L);
                    for (int augmentIndex = 0; augmentIndex < generated.Equipment.Augments.Count; augmentIndex++)
                    {
                        Add(levels, generated.Equipment.Augments[augmentIndex].Level.ToString(), 1L);
                        augmentTotal++;
                    }
                }
                catch (InvalidOperationException)
                {
                    rejected++;
                }
            }

            return new LootboxOddsReportV1(
                sampleCount,
                Entries(items, sampleCount - rejected),
                Entries(slots, sampleCount - rejected),
                Entries(levels, augmentTotal),
                Entries(deltas, sampleCount - rejected),
                rejected);
        }

        private static EquipmentCatalog BuildEquipmentCatalog(
            WeaponCatalog source,
            out Dictionary<StableId, WeaponDefinitionData> map)
        {
            map = new Dictionary<StableId, WeaponDefinitionData>();
            EquipmentQualityTier common = EquipmentQualityTier.Create(QualityCommon, "Common", 1);
            EquipmentQualityTier rare = EquipmentQualityTier.Create(QualityRare, "Rare", 2);
            EquipmentQualityTier exceptional = EquipmentQualityTier.Create(QualityExceptional, "Exceptional", 3);
            var equipment = new List<EquipmentDefinition>();
            IReadOnlyList<WeaponDefinitionData> live =
                source.GetDefinitions(WeaponCatalogContentFilter.LiveOnly);
            for (int index = 0; index < live.Count; index++)
            {
                WeaponDefinitionData weapon = live[index];
                StableId definitionId = StableId.Create(
                    "weapon-definition",
                    "catalog-" + index.ToString("D4"));
                int minimumLevel = Math.Max(1, weapon.FirstAppearance);
                int maximumLevel = Math.Max(minimumLevel, Math.Max(200, weapon.PowerAnchor + 50));
                equipment.Add(EquipmentDefinition.Create(
                    definitionId,
                    EquipmentCategoryIds.Weapon,
                    StableId.Create("weapon-family", SafeSlug(weapon.FamilyId)),
                    weapon.DisplayName,
                    StableId.Create("weapon-runtime", "catalog-" + index.ToString("D4")),
                    InclusiveIntRange.Create(minimumLevel, maximumLevel),
                    3,
                    new[] { common, rare, exceptional },
                    Array.Empty<StableId>()));
                map.Add(definitionId, weapon);
            }
            if (equipment.Count == 0)
            {
                throw new InvalidOperationException("The live weapon catalog is empty.");
            }

            AugmentCompatibility any = AugmentCompatibility.Create(
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
            var augments = new List<AugmentDefinition>();
            for (int index = 1; index <= 3; index++)
            {
                augments.Add(AugmentDefinition.Create(
                    StableId.Create("augment", "simulator-" + index),
                    StableId.Create("augment-family", "simulator-" + index),
                    "Simulator Augment " + index,
                    any,
                    Array.Empty<StableId>(),
                    AugmentDuplicatePolicy.DisallowSameDefinition,
                    InclusiveIntRange.Create(1, 3),
                    InclusiveIntRange.Create(1, 10)));
            }

            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(equipment, augments);
            if (!build.IsValid)
            {
                throw new InvalidOperationException("Weapon-to-equipment catalog projection is invalid.");
            }
            return build.Catalog;
        }

        private static EquipmentGenerationPolicyV1 BuildPolicy(
            ProductionStrongboxTierV1 tier,
            WeaponCatalog source,
            Dictionary<StableId, WeaponDefinitionData> map)
        {
            var candidates = new List<EquipmentGenerationCandidateV1>();
            var keys = new List<StableId>(map.Keys);
            keys.Sort();
            for (int index = 0; index < keys.Count; index++)
            {
                StableId key = keys[index];
                WeaponDefinitionData weapon = map[key];
                if (weapon.TopBoxOnly && tier.TierNumber < 11)
                {
                    continue;
                }

                candidates.Add(EquipmentGenerationCandidateV1.Create(
                    key,
                    0,
                    1000,
                    0,
                    1000,
                    Array.Empty<StableId>(),
                    Math.Max(1, weapon.PeakDropLevel),
                    InclusiveIntRange.Create(
                        Math.Max(1, weapon.FirstAppearance),
                        Math.Max(200, weapon.PowerAnchor + 50)),
                    Math.Max(0.000001, weapon.FinalBaseWeight),
                    1.0));
            }

            return EquipmentGenerationPolicyV1.Create(
                StableId.Create("lootbox-policy", tier.Slug),
                candidates,
                new[]
                {
                    EquipmentQualityCandidateV1.Create(QualityCommon, 0L, tier.CommonWeight),
                    EquipmentQualityCandidateV1.Create(QualityRare, 0L, tier.RareWeight),
                    EquipmentQualityCandidateV1.Create(QualityExceptional, 0L, tier.ExceptionalWeight),
                },
                new[]
                {
                    AugmentGenerationCandidateV1.Create(StableId.Parse("augment.simulator-1"), 0, 1000, 1UL),
                    AugmentGenerationCandidateV1.Create(StableId.Parse("augment.simulator-2"), 0, 1000, 1UL),
                    AugmentGenerationCandidateV1.Create(StableId.Parse("augment.simulator-3"), 0, 1000, 1UL),
                },
                0,
                3,
                false,
                new SoftActivationCurveParameters(0.08, 12L, 8L),
                new ObsolescenceCurveParameters(30L, 20.0, 0.15));
        }

        private static IEnumerable<LootboxOddsEntryV1> Entries(
            Dictionary<string, long> values,
            long total)
        {
            var result = new List<LootboxOddsEntryV1>();
            foreach (KeyValuePair<string, long> pair in values)
            {
                result.Add(new LootboxOddsEntryV1(pair.Key, pair.Value, total));
            }
            return result;
        }

        private static void Add(Dictionary<string, long> values, string key, long quantity)
        {
            long current;
            values.TryGetValue(key, out current);
            values[key] = checked(current + quantity);
        }

        private static ulong DeriveSeed(ulong rootSeed, int ordinal)
        {
            DeterministicRandom random = DeterministicRandom.Create(rootSeed)
                .Fork(StableId.Parse("lootbox-simulator.open"), checked((ulong)ordinal));
            ulong value;
            random.NextUInt64(out value);
            return value;
        }

        private static StableId DynamicId(string purpose, ulong seed, int ordinal)
        {
            return StableId.Create(
                "lootbox-simulator",
                purpose + "-" + seed.ToString("x16") + "-" + ordinal.ToString("D6"));
        }

        private static string SafeSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            var chars = new char[value.Length];
            int count = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char current = char.ToLowerInvariant(value[index]);
                chars[count++] = char.IsLetterOrDigit(current) ? current : '-';
            }
            return new string(chars, 0, count).Trim('-');
        }

        private sealed class SimulatorEquipmentValidator : IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public SimulatorEquipmentValidator(EquipmentCatalog catalog)
            {
                this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance = request == null ? null : request.Instance;
                bool valid = instance != null
                    && catalog.FindEquipmentDefinition(instance.DefinitionId) != null;
                return new EquipmentInstanceValidationResponse(
                    valid,
                    catalog.Fingerprint,
                    instance == null ? null : instance.Fingerprint,
                    new List<EquipmentModelIssue>());
            }
        }
    }
}

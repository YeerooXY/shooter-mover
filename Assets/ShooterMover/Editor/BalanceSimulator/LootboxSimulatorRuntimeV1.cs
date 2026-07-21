using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
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
        private readonly string canonicalText;

        public LootboxGeneratedItemV1(
            ProductionStrongboxTierV1 tier,
            EquipmentInstance equipment,
            string sourceDefinitionId,
            string definitionDisplayName,
            string familyId,
            int mark)
        {
            Tier = tier ?? throw new ArgumentNullException(nameof(tier));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            SourceDefinitionId = sourceDefinitionId
                ?? equipment.DefinitionId.ToString();
            DefinitionDisplayName = definitionDisplayName
                ?? equipment.DefinitionId.ToString();
            FamilyId = familyId ?? string.Empty;
            Mark = mark;
            OddsKey = DefinitionDisplayName + " [" + SourceDefinitionId + "]";

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(
                builder,
                "tier",
                Tier.TierStableId.ToString());
            StrongboxCanonicalV1.AppendToken(
                builder,
                "source_definition",
                SourceDefinitionId);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "display_name",
                DefinitionDisplayName);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "family",
                FamilyId);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "mark",
                Mark.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "equipment",
                Equipment.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public ProductionStrongboxTierV1 Tier { get; }
        public EquipmentInstance Equipment { get; }
        public string SourceDefinitionId { get; }
        public string DefinitionDisplayName { get; }
        public string FamilyId { get; }
        public int Mark { get; }
        public string OddsKey { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }

    public sealed class LootboxOddsEntryV1 : IComparable<LootboxOddsEntryV1>
    {
        public LootboxOddsEntryV1(
            string key,
            long count,
            long total)
        {
            Key = key ?? string.Empty;
            if (count < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (total < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(total));
            }
            if (count > total)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Count = count;
            Total = total;
            Percentage = total == 0L
                ? 0.0
                : 100.0 * count / total;
        }

        public string Key { get; }
        public long Count { get; }
        public long Total { get; }
        public double Percentage { get; }

        public string ToCanonicalString()
        {
            return "key=" + Key
                + "\ncount="
                + Count.ToString(CultureInfo.InvariantCulture)
                + "\ntotal="
                + Total.ToString(CultureInfo.InvariantCulture);
        }

        public int CompareTo(LootboxOddsEntryV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int byCount = other.Count.CompareTo(Count);
            return byCount != 0
                ? byCount
                : string.CompareOrdinal(Key, other.Key);
        }
    }

    public sealed class LootboxOddsReportV1
    {
        private readonly string canonicalText;

        public LootboxOddsReportV1(
            int tierNumber,
            int playerLevel,
            ulong rootSeed,
            int sampleCount,
            int successfulOpenCount,
            IEnumerable<LootboxOddsEntryV1> itemOdds,
            IEnumerable<LootboxOddsEntryV1> qualityOdds,
            IEnumerable<LootboxOddsEntryV1> slotOdds,
            IEnumerable<LootboxOddsEntryV1> augmentTierOdds,
            IEnumerable<LootboxOddsEntryV1> augmentLevelOdds,
            IEnumerable<LootboxOddsEntryV1> itemLevelDeltaOdds,
            int rejectedRolls)
        {
            if (tierNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tierNumber));
            }
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            }
            if (successfulOpenCount < 0
                || successfulOpenCount > sampleCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(successfulOpenCount));
            }
            if (rejectedRolls < 0
                || rejectedRolls
                    != sampleCount - successfulOpenCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rejectedRolls));
            }

            TierNumber = tierNumber;
            PlayerLevel = playerLevel;
            RootSeed = rootSeed;
            SampleCount = sampleCount;
            SuccessfulOpenCount = successfulOpenCount;
            ItemOdds = Copy(itemOdds);
            QualityOdds = Copy(qualityOdds);
            SlotOdds = Copy(slotOdds);
            AugmentTierOdds = Copy(augmentTierOdds);
            AugmentLevelOdds = Copy(augmentLevelOdds);
            ItemLevelDeltaOdds = Copy(itemLevelDeltaOdds);
            RejectedRolls = rejectedRolls;
            canonicalText = BuildCanonicalText();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public int TierNumber { get; }
        public int PlayerLevel { get; }
        public ulong RootSeed { get; }
        public int SampleCount { get; }
        public int SuccessfulOpenCount { get; }
        public IReadOnlyList<LootboxOddsEntryV1> ItemOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> QualityOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> SlotOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> AugmentTierOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> AugmentLevelOdds { get; }
        public IReadOnlyList<LootboxOddsEntryV1> ItemLevelDeltaOdds { get; }
        public int RejectedRolls { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public LootboxOddsEntryV1 FindItemOdds(string key)
        {
            for (int index = 0; index < ItemOdds.Count; index++)
            {
                if (string.Equals(
                    ItemOdds[index].Key,
                    key,
                    StringComparison.Ordinal))
                {
                    return ItemOdds[index];
                }
            }

            return null;
        }

        private string BuildCanonicalText()
        {
            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(
                builder,
                "schema",
                "lootbox-odds-report-v1");
            StrongboxCanonicalV1.AppendToken(
                builder,
                "tier",
                TierNumber.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "player_level",
                PlayerLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "root_seed",
                RootSeed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "sample_count",
                SampleCount.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "successful_open_count",
                SuccessfulOpenCount.ToString(
                    CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "rejected_rolls",
                RejectedRolls.ToString(CultureInfo.InvariantCulture));
            Append(builder, "item", ItemOdds);
            Append(builder, "quality", QualityOdds);
            Append(builder, "slot", SlotOdds);
            Append(builder, "augment_tier", AugmentTierOdds);
            Append(builder, "augment_level", AugmentLevelOdds);
            Append(builder, "item_level_delta", ItemLevelDeltaOdds);
            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string prefix,
            IReadOnlyList<LootboxOddsEntryV1> values)
        {
            StrongboxCanonicalV1.AppendToken(
                builder,
                prefix + "_count",
                values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    prefix
                    + "_"
                    + index.ToString(
                        "D4",
                        CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
        }

        private static IReadOnlyList<LootboxOddsEntryV1> Copy(
            IEnumerable<LootboxOddsEntryV1> values)
        {
            var result = new List<LootboxOddsEntryV1>(
                values ?? Array.Empty<LootboxOddsEntryV1>());
            result.Sort();
            return new ReadOnlyCollection<LootboxOddsEntryV1>(
                result);
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

        private static readonly StableId DifficultyNormal =
            StableId.Parse("difficulty.normal");
        private static readonly StableId QualityCommon =
            StableId.Parse("quality.common");
        private static readonly StableId QualityRare =
            StableId.Parse("quality.rare");
        private static readonly StableId QualityExceptional =
            StableId.Parse("quality.exceptional");
        private static readonly StableId HoldingsAuthority =
            StableId.Parse("holdings.lootbox-simulator");
        private static readonly StableId SourceId =
            StableId.Parse("source.lootbox-simulator");

        private readonly WeaponCatalog weaponCatalog;
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly Dictionary<StableId, WeaponDefinitionData>
            weaponByEquipmentId;
        private readonly StrongboxDefinitionCatalogV1
            strongboxDefinitions;
        private readonly StrongboxEquipmentGenerationResolverV1
            resolver;
        private readonly PlayerHoldingsService holdings;
        private readonly List<EquipmentInstance> acceptedInventory =
            new List<EquipmentInstance>();
        private readonly HashSet<StableId> decidedItems =
            new HashSet<StableId>();

        private LootboxSimulatorRuntimeV1(
            WeaponCatalog weaponCatalog,
            EquipmentCatalog equipmentCatalog,
            Dictionary<StableId, WeaponDefinitionData>
                weaponByEquipmentId,
            StrongboxDefinitionCatalogV1 strongboxDefinitions,
            StrongboxEquipmentGenerationResolverV1 resolver)
        {
            this.weaponCatalog = weaponCatalog
                ?? throw new ArgumentNullException(
                    nameof(weaponCatalog));
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(
                    nameof(equipmentCatalog));
            this.weaponByEquipmentId = weaponByEquipmentId
                ?? throw new ArgumentNullException(
                    nameof(weaponByEquipmentId));
            this.strongboxDefinitions = strongboxDefinitions
                ?? throw new ArgumentNullException(
                    nameof(strongboxDefinitions));
            this.resolver = resolver
                ?? throw new ArgumentNullException(nameof(resolver));
            holdings = new PlayerHoldingsService(
                HoldingsAuthority,
                1000000L,
                new SimulatorEquipmentValidator(equipmentCatalog));
        }

        public WeaponCatalog WeaponCatalog
        {
            get { return weaponCatalog; }
        }

        public EquipmentCatalog EquipmentCatalog
        {
            get { return equipmentCatalog; }
        }

        public IReadOnlyList<EquipmentInstance> AcceptedInventory
        {
            get
            {
                return new ReadOnlyCollection<EquipmentInstance>(
                    acceptedInventory);
            }
        }

        public long Cash { get; private set; }

        public static bool TryCreate(
            string weaponCatalogJson,
            out LootboxSimulatorRuntimeV1 runtime,
            out string diagnostic)
        {
            runtime = null;
            diagnostic = string.Empty;
            WeaponCatalogImportResult import =
                WeaponCatalogJsonImporter.Import(weaponCatalogJson);
            if (!import.IsSuccess)
            {
                diagnostic = import.Issues.Count == 0
                    ? "Weapon catalog import failed."
                    : import.Issues[0].Path
                        + ": "
                        + import.Issues[0].Detail;
                return false;
            }

            try
            {
                Dictionary<StableId, WeaponDefinitionData> map;
                EquipmentCatalog equipment = BuildEquipmentCatalog(
                    import.Catalog,
                    out map);
                var definitions =
                    new List<StrongboxDefinitionV1>();
                var bindings =
                    new List<
                        StrongboxEquipmentGenerationDefinitionV1>();

                for (int index = 0;
                    index
                        < ProductionStrongboxCatalogV1.Tiers.Count;
                    index++)
                {
                    ProductionStrongboxTierV1 tier =
                        ProductionStrongboxCatalogV1.Tiers[index];
                    EquipmentGenerationPolicyV1 policy =
                        BuildPolicy(tier, map);
                    StrongboxDefinitionV1 definition =
                        tier.CreateDefinition(policy.PolicyId);
                    definitions.Add(definition);
                    bindings.Add(
                        new StrongboxEquipmentGenerationDefinitionV1(
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
                        new
                            StrongboxEquipmentGenerationDefinitionCatalogV1(
                                bindings)));
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.ToString();
                return false;
            }
        }

        public LootboxGeneratedItemV1 Generate(
            int tierNumber,
            int playerLevel,
            ulong rootSeed,
            int queueOrdinal)
        {
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerLevel));
            }
            if (queueOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(queueOrdinal));
            }

            ProductionStrongboxTierV1 tier =
                ProductionStrongboxCatalogV1.GetByNumber(
                    tierNumber);
            StrongboxDefinitionV1 definition;
            if (!strongboxDefinitions.TryGet(
                    tier.TierStableId,
                    out definition))
            {
                throw new InvalidOperationException(
                    "Missing strongbox definition "
                    + tier.TierStableId
                    + ".");
            }

            int effectiveLevel =
                tier.ResolveEffectivePlayerLevel(playerLevel);
            ProgressionContext context = ProgressionContext.Create(
                effectiveLevel,
                effectiveLevel,
                DifficultyNormal,
                1,
                Array.Empty<StableId>());
            ulong seed = DeriveSeed(rootSeed, queueOrdinal);
            StableId instanceId = DynamicId(
                "box-instance",
                rootSeed,
                queueOrdinal);
            StrongboxInstanceContextV1 boxContext =
                StrongboxInstanceContextV1.Create(
                    instanceId,
                    tier.TierStableId,
                    seed,
                    DeterministicRandom.AlgorithmVersion1,
                    context,
                    SourceId,
                    DynamicId(
                        "collection",
                        rootSeed,
                        queueOrdinal),
                    definition.Fingerprint);
            RewardOperationRequestV1 operation =
                RewardOperationRequestV1.Create(
                    DynamicId("run", rootSeed, 0),
                    instanceId,
                    DynamicId(
                        "box-operation",
                        rootSeed,
                        queueOrdinal),
                    DynamicId(
                        "box-commitment",
                        rootSeed,
                        queueOrdinal),
                    definition.BaseRewardProfile.ProfileStableId,
                    definition.Fingerprint);
            RewardGrantV1 grant = RewardGrantV1.Create(
                DynamicId(
                    "equipment-grant",
                    rootSeed,
                    queueOrdinal),
                RewardGrantKindV1.EquipmentReference,
                EquipmentCategoryIds.Weapon,
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
                throw new InvalidOperationException(
                    "Strongbox generation rejected: "
                    + rejection);
            }
            if (generated.Count != 1)
            {
                throw new InvalidOperationException(
                    "The opener expects exactly one equipment item per box.");
            }

            EquipmentInstance item = generated[0];
            if (item.Augments.Count != 0)
            {
                throw new InvalidOperationException(
                    "Fresh strongbox equipment must not contain installed augments.");
            }

            WeaponDefinitionData source;
            if (!weaponByEquipmentId.TryGetValue(
                    item.DefinitionId,
                    out source))
            {
                throw new InvalidOperationException(
                    "Generated equipment is missing its weapon-catalog projection.");
            }

            return new LootboxGeneratedItemV1(
                tier,
                item,
                source.DefinitionId,
                source.DisplayName,
                source.FamilyId,
                source.Mark);
        }

        public PlayerHoldingsMutationStatusV1 Keep(
            LootboxGeneratedItemV1 generated)
        {
            if (generated == null)
            {
                throw new ArgumentNullException(nameof(generated));
            }
            if (decidedItems.Contains(
                    generated.Equipment.InstanceId))
            {
                return PlayerHoldingsMutationStatusV1
                    .ExactDuplicateNoChange;
            }

            StableId transactionId =
                StrongboxCanonicalV1.DeriveId(
                    "lootboxkeeptransaction",
                    generated.Equipment.InstanceId.ToString());
            StableId operationId =
                StrongboxCanonicalV1.DeriveId(
                    "lootboxkeepoperation",
                    generated.Equipment.InstanceId.ToString());
            HoldingProvenanceV1 provenance =
                HoldingProvenanceV1.Create(
                    StrongboxCanonicalV1.DeriveId(
                        "lootboxgrant",
                        generated.Equipment.InstanceId.ToString()),
                    SourceId);
            PlayerHoldingsMutationResultV1 result =
                holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        transactionId,
                        operationId,
                        HoldingsAuthority,
                        generated.Equipment,
                        provenance));
            if (result.Status
                == PlayerHoldingsMutationStatusV1.Applied)
            {
                acceptedInventory.Add(generated.Equipment);
                decidedItems.Add(
                    generated.Equipment.InstanceId);
            }

            return result.Status;
        }

        public bool Sell(LootboxGeneratedItemV1 generated)
        {
            if (generated == null)
            {
                throw new ArgumentNullException(nameof(generated));
            }
            if (!decidedItems.Add(
                    generated.Equipment.InstanceId))
            {
                return false;
            }

            // TODO(ECONOMY): replace the temporary fixed sale value
            // with the real item valuation service.
            Cash = checked(Cash + TemporarySaleValue);
            return true;
        }

        public LootboxOddsReportV1 CalculateOdds(
            int tierNumber,
            int playerLevel,
            ulong rootSeed,
            int sampleCount)
        {
            if (sampleCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleCount));
            }

            var items = new Dictionary<string, long>(
                StringComparer.Ordinal);
            var qualities = new Dictionary<string, long>(
                StringComparer.Ordinal);
            var slots = new Dictionary<string, long>(
                StringComparer.Ordinal);
            var deltas = new Dictionary<string, long>(
                StringComparer.Ordinal);
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
                    Add(items, generated.OddsKey, 1L);
                    Add(
                        qualities,
                        generated.Equipment.QualityId.ToString(),
                        1L);
                    Add(
                        slots,
                        generated.Equipment.Augments.Count.ToString(
                            CultureInfo.InvariantCulture),
                        1L);
                    Add(
                        deltas,
                        (generated.Equipment.ItemLevel - playerLevel)
                            .ToString(
                                "+0;-0;0",
                                CultureInfo.InvariantCulture),
                        1L);
                }
                catch (InvalidOperationException)
                {
                    rejected++;
                }
                catch (ArgumentException)
                {
                    rejected++;
                }
                catch (OverflowException)
                {
                    rejected++;
                }
            }

            int successful = sampleCount - rejected;
            return new LootboxOddsReportV1(
                tierNumber,
                playerLevel,
                rootSeed,
                sampleCount,
                successful,
                Entries(items, successful),
                Entries(qualities, successful),
                Entries(slots, successful),
                Array.Empty<LootboxOddsEntryV1>(),
                Array.Empty<LootboxOddsEntryV1>(),
                Entries(deltas, successful),
                rejected);
        }

        private static EquipmentCatalog BuildEquipmentCatalog(
            WeaponCatalog source,
            out Dictionary<StableId, WeaponDefinitionData> map)
        {
            map =
                new Dictionary<StableId, WeaponDefinitionData>();
            EquipmentQualityTier common =
                EquipmentQualityTier.Create(
                    QualityCommon,
                    "Common",
                    1);
            EquipmentQualityTier rare =
                EquipmentQualityTier.Create(
                    QualityRare,
                    "Rare",
                    2);
            EquipmentQualityTier exceptional =
                EquipmentQualityTier.Create(
                    QualityExceptional,
                    "Exceptional",
                    3);
            var equipment =
                new List<EquipmentDefinition>();
            IReadOnlyList<WeaponDefinitionData> live =
                source.GetDefinitions(
                    WeaponCatalogContentFilter.LiveOnly);

            for (int index = 0; index < live.Count; index++)
            {
                WeaponDefinitionData weapon = live[index];
                StableId definitionId =
                    StrongboxCanonicalV1.DeriveId(
                        "weapondefinition",
                        weapon.DefinitionId);
                StableId runtimeReferenceId =
                    StrongboxCanonicalV1.DeriveId(
                        "weapon",
                        weapon.DefinitionId);
                int minimumLevel = Math.Max(
                    1,
                    weapon.FirstAppearance);
                int maximumLevel = MaximumItemLevel(weapon);
                equipment.Add(
                    EquipmentDefinition.Create(
                        definitionId,
                        EquipmentCategoryIds.Weapon,
                        StrongboxCanonicalV1.DeriveId(
                            "weaponfamily",
                            weapon.FamilyId),
                        weapon.DisplayName,
                        runtimeReferenceId,
                        InclusiveIntRange.Create(
                            minimumLevel,
                            maximumLevel),
                        3,
                        new[]
                        {
                            common,
                            rare,
                            exceptional,
                        },
                        Array.Empty<StableId>()));
                map.Add(definitionId, weapon);
            }

            if (equipment.Count == 0)
            {
                throw new InvalidOperationException(
                    "The live weapon catalog is empty.");
            }

            EquipmentCatalogBuildResult build =
                EquipmentCatalog.Build(
                    equipment,
                    Array.Empty<AugmentDefinition>());
            if (!build.IsValid)
            {
                throw new InvalidOperationException(
                    "Weapon-to-equipment catalog projection is invalid: "
                    + (build.Issues.Count == 0
                        ? "unknown"
                        : build.Issues[0].ToString()));
            }

            return build.Catalog;
        }

        private static EquipmentGenerationPolicyV1 BuildPolicy(
            ProductionStrongboxTierV1 tier,
            Dictionary<StableId, WeaponDefinitionData> map)
        {
            var candidates =
                new List<EquipmentGenerationCandidateV1>();
            var keys = new List<StableId>(map.Keys);
            keys.Sort();

            for (int index = 0; index < keys.Count; index++)
            {
                StableId key = keys[index];
                WeaponDefinitionData weapon = map[key];
                if (weapon.TopBoxOnly
                    && tier.TierNumber < 11)
                {
                    continue;
                }

                candidates.Add(
                    EquipmentGenerationCandidateV1.Create(
                        key,
                        0,
                        1000,
                        0,
                        1000,
                        Array.Empty<StableId>(),
                        Math.Max(1, weapon.PeakDropLevel),
                        InclusiveIntRange.Create(
                            Math.Max(
                                1,
                                weapon.FirstAppearance),
                            MaximumItemLevel(weapon)),
                        Math.Max(
                            0.000001,
                            weapon.FinalBaseWeight),
                        1.0));
            }

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "Strongbox tier "
                    + tier.DisplayName
                    + " has no eligible live weapon definitions.");
            }

            return EquipmentGenerationPolicyV1.Create(
                StableId.Create(
                    "lootbox-policy",
                    tier.Slug),
                candidates,
                new[]
                {
                    EquipmentQualityCandidateV1.Create(
                        QualityCommon,
                        0L,
                        tier.CommonWeight),
                    EquipmentQualityCandidateV1.Create(
                        QualityRare,
                        0L,
                        tier.RareWeight),
                    EquipmentQualityCandidateV1.Create(
                        QualityExceptional,
                        0L,
                        tier.ExceptionalWeight),
                },
                Array.Empty<AugmentGenerationCandidateV1>(),
                0,
                0,
                true,
                new SoftActivationCurveParameters(
                    0.08,
                    12L,
                    8L),
                new ObsolescenceCurveParameters(
                    30L,
                    20.0,
                    0.15));
        }

        private static int MaximumItemLevel(
            WeaponDefinitionData weapon)
        {
            return Math.Max(
                Math.Max(1, weapon.FirstAppearance),
                Math.Max(
                    200,
                    checked(weapon.PowerAnchor + 50)));
        }

        private static IEnumerable<LootboxOddsEntryV1> Entries(
            Dictionary<string, long> values,
            long total)
        {
            var result = new List<LootboxOddsEntryV1>();
            foreach (
                KeyValuePair<string, long> pair
                    in values)
            {
                result.Add(
                    new LootboxOddsEntryV1(
                        pair.Key,
                        pair.Value,
                        total));
            }

            return result;
        }

        private static void Add(
            Dictionary<string, long> values,
            string key,
            long quantity)
        {
            long current;
            values.TryGetValue(key, out current);
            values[key] = checked(current + quantity);
        }

        private static ulong DeriveSeed(
            ulong rootSeed,
            int ordinal)
        {
            DeterministicRandom random =
                DeterministicRandom.Create(rootSeed)
                    .Fork(
                        StableId.Parse(
                            "lootbox-simulator.open"),
                        checked((ulong)ordinal));
            ulong value;
            random.NextUInt64(out value);
            return value;
        }

        private static StableId DynamicId(
            string purpose,
            ulong seed,
            int ordinal)
        {
            return StableId.Create(
                "lootbox-simulator",
                purpose
                + "-"
                + seed.ToString(
                    "x16",
                    CultureInfo.InvariantCulture)
                + "-"
                + ordinal.ToString(
                    "D6",
                    CultureInfo.InvariantCulture));
        }

        private sealed class SimulatorEquipmentValidator :
            IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public SimulatorEquipmentValidator(
                EquipmentCatalog catalog)
            {
                this.catalog = catalog
                    ?? throw new ArgumentNullException(
                        nameof(catalog));
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance =
                    request == null
                        ? null
                        : request.Instance;
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    instance,
                    catalog.ValidateInstance(instance));
            }
        }
    }
}

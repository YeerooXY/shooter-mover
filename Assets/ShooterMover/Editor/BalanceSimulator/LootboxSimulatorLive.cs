using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Guns.Catalog;
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
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Editor.BalanceSimulator
{
    public sealed class LootboxGeneratedItem
    {
        private readonly string canonicalText;

        public LootboxGeneratedItem(
            StrongboxTier tier,
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
            Strongbox.AppendToken(
                builder,
                "tier",
                Tier.TierStableId.ToString());
            Strongbox.AppendToken(
                builder,
                "source_definition",
                SourceDefinitionId);
            Strongbox.AppendToken(
                builder,
                "display_name",
                DefinitionDisplayName);
            Strongbox.AppendToken(
                builder,
                "family",
                FamilyId);
            Strongbox.AppendToken(
                builder,
                "mark",
                Mark.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
                builder,
                "equipment",
                Equipment.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StrongboxTier Tier { get; }
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

    public sealed class LootboxOddsEntry : IComparable<LootboxOddsEntry>
    {
        public LootboxOddsEntry(
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

        public int CompareTo(LootboxOddsEntry other)
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

    public sealed class LootboxOddsReport
    {
        private readonly string canonicalText;

        public LootboxOddsReport(
            int tierNumber,
            int playerLevel,
            ulong rootSeed,
            int sampleCount,
            int successfulOpenCount,
            IEnumerable<LootboxOddsEntry> itemOdds,
            IEnumerable<LootboxOddsEntry> qualityOdds,
            IEnumerable<LootboxOddsEntry> slotOdds,
            IEnumerable<LootboxOddsEntry> augmentTierOdds,
            IEnumerable<LootboxOddsEntry> augmentLevelOdds,
            IEnumerable<LootboxOddsEntry> itemLevelDeltaOdds,
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
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public int TierNumber { get; }
        public int PlayerLevel { get; }
        public ulong RootSeed { get; }
        public int SampleCount { get; }
        public int SuccessfulOpenCount { get; }
        public IReadOnlyList<LootboxOddsEntry> ItemOdds { get; }
        public IReadOnlyList<LootboxOddsEntry> QualityOdds { get; }
        public IReadOnlyList<LootboxOddsEntry> SlotOdds { get; }
        public IReadOnlyList<LootboxOddsEntry> AugmentTierOdds { get; }
        public IReadOnlyList<LootboxOddsEntry> AugmentLevelOdds { get; }
        public IReadOnlyList<LootboxOddsEntry> ItemLevelDeltaOdds { get; }
        public int RejectedRolls { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public LootboxOddsEntry FindItemOdds(string key)
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
            Strongbox.AppendToken(
                builder,
                "schema",
                "lootbox-odds-report-v1");
            Strongbox.AppendToken(
                builder,
                "tier",
                TierNumber.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
                builder,
                "player_level",
                PlayerLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
                builder,
                "root_seed",
                RootSeed.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
                builder,
                "sample_count",
                SampleCount.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
                builder,
                "successful_open_count",
                SuccessfulOpenCount.ToString(
                    CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
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
            IReadOnlyList<LootboxOddsEntry> values)
        {
            Strongbox.AppendToken(
                builder,
                prefix + "_count",
                values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                Strongbox.AppendToken(
                    builder,
                    prefix
                    + "_"
                    + index.ToString(
                        "D4",
                        CultureInfo.InvariantCulture),
                    values[index].ToCanonicalString());
            }
        }

        private static IReadOnlyList<LootboxOddsEntry> Copy(
            IEnumerable<LootboxOddsEntry> values)
        {
            var result = new List<LootboxOddsEntry>(
                values ?? Array.Empty<LootboxOddsEntry>());
            result.Sort();
            return new ReadOnlyCollection<LootboxOddsEntry>(
                result);
        }
    }

    /// <summary>
    /// Editor-only composition for ordered lootbox opening. Generation is delegated
    /// to the production BOX equipment resolver and GEN service. Accepted equipment
    /// is admitted through the real player-holdings authority.
    /// </summary>
    public sealed class LootboxSimulatorLive
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

        private readonly GunCatalog gunCatalog;
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly Dictionary<StableId, GunDefinitionData>
            gunByEquipmentId;
        private readonly StrongboxDefinitionCatalog
            strongboxDefinitions;
        private readonly StrongboxEquipmentGenerationResolver
            resolver;
        private readonly PlayerHoldingsActions holdings;
        private readonly List<EquipmentInstance> acceptedInventory =
            new List<EquipmentInstance>();
        private readonly HashSet<StableId> decidedItems =
            new HashSet<StableId>();

        private LootboxSimulatorLive(
            GunCatalog gunCatalog,
            EquipmentCatalog equipmentCatalog,
            Dictionary<StableId, GunDefinitionData>
                gunByEquipmentId,
            StrongboxDefinitionCatalog strongboxDefinitions,
            StrongboxEquipmentGenerationResolver resolver)
        {
            this.gunCatalog = gunCatalog
                ?? throw new ArgumentNullException(
                    nameof(gunCatalog));
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(
                    nameof(equipmentCatalog));
            this.gunByEquipmentId = gunByEquipmentId
                ?? throw new ArgumentNullException(
                    nameof(gunByEquipmentId));
            this.strongboxDefinitions = strongboxDefinitions
                ?? throw new ArgumentNullException(
                    nameof(strongboxDefinitions));
            this.resolver = resolver
                ?? throw new ArgumentNullException(nameof(resolver));
            holdings = new PlayerHoldingsActions(
                HoldingsAuthority,
                1000000L,
                new SimulatorEquipmentValidator(equipmentCatalog));
        }

        public GunCatalog GunCatalog
        {
            get { return gunCatalog; }
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
            string gunCatalogJson,
            out LootboxSimulatorLive runtime,
            out string diagnostic)
        {
            runtime = null;
            diagnostic = string.Empty;
            GunCatalogImportResult import =
                GunCatalogJsonImporter.Import(gunCatalogJson);
            if (!import.IsSuccess)
            {
                diagnostic = import.Issues.Count == 0
                    ? "Gun catalog import failed."
                    : import.Issues[0].Path
                        + ": "
                        + import.Issues[0].Detail;
                return false;
            }

            try
            {
                Dictionary<StableId, GunDefinitionData> map;
                EquipmentCatalog equipment = BuildEquipmentCatalog(
                    import.Catalog,
                    out map);
                var definitions =
                    new List<StrongboxDefinition>();
                var bindings =
                    new List<
                        StrongboxEquipmentGenerationDefinition>();

                for (int index = 0;
                    index
                        < StrongboxCatalog.Tiers.Count;
                    index++)
                {
                    StrongboxTier tier =
                        StrongboxCatalog.Tiers[index];
                    EquipmentGenerationPolicy policy =
                        BuildPolicy(tier, map);
                    StrongboxDefinition definition =
                        tier.CreateDefinition(policy.PolicyId);
                    definitions.Add(definition);
                    bindings.Add(
                        new StrongboxEquipmentGenerationDefinition(
                            tier.TierStableId,
                            tier.CreatePowerBudgetPolicy(),
                            policy,
                            equipment));
                }

                runtime = new LootboxSimulatorLive(
                    import.Catalog,
                    equipment,
                    map,
                    new StrongboxDefinitionCatalog(definitions),
                    new StrongboxEquipmentGenerationResolver(
                        new RewardGenerationActions(),
                        new
                            StrongboxEquipmentGenerationDefinitionCatalog(
                                bindings)));
                return true;
            }
            catch (Exception exception)
            {
                diagnostic = exception.ToString();
                return false;
            }
        }

        public LootboxGeneratedItem Generate(
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

            StrongboxTier tier =
                StrongboxCatalog.GetByNumber(
                    tierNumber);
            StrongboxDefinition definition;
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
            StrongboxInstanceContext boxContext =
                StrongboxInstanceContext.Create(
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
            RewardOperationRequest operation =
                RewardOperationRequest.Create(
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
            RewardGrant grant = RewardGrant.Create(
                DynamicId(
                    "equipment-grant",
                    rootSeed,
                    queueOrdinal),
                RewardGrantKind.EquipmentReference,
                EquipmentCategoryIds.Gun,
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

            GunDefinitionData source;
            if (!gunByEquipmentId.TryGetValue(
                    item.DefinitionId,
                    out source))
            {
                throw new InvalidOperationException(
                    "Generated equipment is missing its gun-catalog projection.");
            }

            return new LootboxGeneratedItem(
                tier,
                item,
                source.DefinitionId,
                source.DisplayName,
                source.FamilyId,
                source.Mark);
        }

        public PlayerHoldingsMutationStatus Keep(
            LootboxGeneratedItem generated)
        {
            if (generated == null)
            {
                throw new ArgumentNullException(nameof(generated));
            }
            if (decidedItems.Contains(
                    generated.Equipment.InstanceId))
            {
                return PlayerHoldingsMutationStatus
                    .ExactDuplicateNoChange;
            }

            StableId transactionId =
                Strongbox.DeriveId(
                    "lootboxkeeptransaction",
                    generated.Equipment.InstanceId.ToString());
            StableId operationId =
                Strongbox.DeriveId(
                    "lootboxkeepoperation",
                    generated.Equipment.InstanceId.ToString());
            HoldingProvenance provenance =
                HoldingProvenance.Create(
                    Strongbox.DeriveId(
                        "lootboxgrant",
                        generated.Equipment.InstanceId.ToString()),
                    SourceId);
            PlayerHoldingsMutationResult result =
                holdings.Apply(
                    PlayerHoldingsCommand.AddEquipment(
                        transactionId,
                        operationId,
                        HoldingsAuthority,
                        generated.Equipment,
                        provenance));
            if (result.Status
                == PlayerHoldingsMutationStatus.Applied)
            {
                acceptedInventory.Add(generated.Equipment);
                decidedItems.Add(
                    generated.Equipment.InstanceId);
            }

            return result.Status;
        }

        public bool Sell(LootboxGeneratedItem generated)
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

        public LootboxOddsReport CalculateOdds(
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
                    LootboxGeneratedItem generated = Generate(
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
            return new LootboxOddsReport(
                tierNumber,
                playerLevel,
                rootSeed,
                sampleCount,
                successful,
                Entries(items, successful),
                Entries(qualities, successful),
                Entries(slots, successful),
                Array.Empty<LootboxOddsEntry>(),
                Array.Empty<LootboxOddsEntry>(),
                Entries(deltas, successful),
                rejected);
        }

        private static EquipmentCatalog BuildEquipmentCatalog(
            GunCatalog source,
            out Dictionary<StableId, GunDefinitionData> map)
        {
            map =
                new Dictionary<StableId, GunDefinitionData>();
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
            IReadOnlyList<GunDefinitionData> live =
                source.GetDefinitions(
                    GunCatalogContentFilter.LiveOnly);

            for (int index = 0; index < live.Count; index++)
            {
                GunDefinitionData gun = live[index];
                StableId definitionId =
                    Strongbox.DeriveId(
                        "gundefinition",
                        gun.DefinitionId);
                StableId runtimeReferenceId =
                    new GunDefinitionId(
                        gun.DefinitionId).ToRuntimeReference();
                int minimumLevel = Math.Max(
                    1,
                    gun.FirstAppearance);
                int maximumLevel = MaximumItemLevel(gun);
                equipment.Add(
                    EquipmentDefinition.Create(
                        definitionId,
                        EquipmentCategoryIds.Gun,
                        Strongbox.DeriveId(
                            "gunfamily",
                            gun.FamilyId),
                        gun.DisplayName,
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
                map.Add(definitionId, gun);
            }

            if (equipment.Count == 0)
            {
                throw new InvalidOperationException(
                    "The live gun catalog is empty.");
            }

            EquipmentCatalogBuildResult build =
                EquipmentCatalog.Build(
                    equipment,
                    Array.Empty<AugmentDefinition>());
            if (!build.IsValid)
            {
                throw new InvalidOperationException(
                    "Gun-to-equipment catalog projection is invalid: "
                    + (build.Issues.Count == 0
                        ? "unknown"
                        : build.Issues[0].ToString()));
            }

            return build.Catalog;
        }

        private static EquipmentGenerationPolicy BuildPolicy(
            StrongboxTier tier,
            Dictionary<StableId, GunDefinitionData> map)
        {
            var candidates =
                new List<EquipmentGenerationCandidate>();
            var keys = new List<StableId>(map.Keys);
            keys.Sort();

            for (int index = 0; index < keys.Count; index++)
            {
                StableId key = keys[index];
                GunDefinitionData gun = map[key];
                if (gun.TopBoxOnly
                    && tier.TierNumber < 11)
                {
                    continue;
                }

                candidates.Add(
                    EquipmentGenerationCandidate.Create(
                        key,
                        0,
                        1000,
                        0,
                        1000,
                        Array.Empty<StableId>(),
                        Math.Max(1, gun.PeakDropLevel),
                        InclusiveIntRange.Create(
                            Math.Max(
                                1,
                                gun.FirstAppearance),
                            MaximumItemLevel(gun)),
                        Math.Max(
                            0.000001,
                            gun.FinalBaseWeight),
                        1.0));
            }

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "Strongbox tier "
                    + tier.DisplayName
                    + " has no eligible live gun definitions.");
            }

            return EquipmentGenerationPolicy.Create(
                StableId.Create(
                    "lootbox-policy",
                    tier.Slug),
                candidates,
                new[]
                {
                    EquipmentQualityCandidate.Create(
                        QualityCommon,
                        0L,
                        tier.CommonWeight),
                    EquipmentQualityCandidate.Create(
                        QualityRare,
                        0L,
                        tier.RareWeight),
                    EquipmentQualityCandidate.Create(
                        QualityExceptional,
                        0L,
                        tier.ExceptionalWeight),
                },
                Array.Empty<AugmentGenerationCandidate>(),
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
            GunDefinitionData gun)
        {
            return Math.Max(
                Math.Max(1, gun.FirstAppearance),
                Math.Max(
                    200,
                    checked(gun.PowerAnchor + 50)));
        }

        private static IEnumerable<LootboxOddsEntry> Entries(
            Dictionary<string, long> values,
            long total)
        {
            var result = new List<LootboxOddsEntry>();
            foreach (
                KeyValuePair<string, long> pair
                    in values)
            {
                result.Add(
                    new LootboxOddsEntry(
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

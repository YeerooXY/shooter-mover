using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Simulation;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Local Editor bridge for the Item Maker Strongbox page. The bridge exports the
    /// already-composed live gun catalog and sends every opening through the production
    /// Strongbox resolver. Requests use the project Temp folder so no second HTTP listener
    /// or browser-side loot formula is required.
    /// </summary>
    [InitializeOnLoad]
    public static class StrongboxPreviewBridge
    {
        private const string LiveCatalogAuthority =
            "GunCatalogProvider.GunCatalog";
        private const int MaximumAnalysisSamples = 10000;

        [Serializable]
        private sealed class Request
        {
            public string requestId;
            public string mode;
            public int playerLevel;
            public int tierNumber;
            public string seed;
            public int sampleCount;
        }

        [Serializable]
        private sealed class Candidate
        {
            public int rollOrder;
            public string definitionId;
            public string displayName;
            public string rarityId;
            public int firstAppearanceLevel;
            public int peakLevel;
            public int distance;
            public double baseWeight;
            public double levelAffinity;
            public double rarityMultiplier;
            public double finalWeight;
            public double chancePercent;
            public bool hardEligible;
            public bool selected;
            public string reason;
        }

        [Serializable]
        private sealed class DistributionEntry
        {
            public string key;
            public string label;
            public int count;
            public double percentage;
        }

        [Serializable]
        private sealed class WeaponBreakdown
        {
            public string definitionId;
            public string displayName;
            public int count;
            public double percentage;
            public List<DistributionEntry> targetLevelDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> itemLevelDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> qualityDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> augmentSlotDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> augmentLevelDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> augmentSignatureDistribution =
                new List<DistributionEntry>();
        }

        [Serializable]
        private sealed class Response
        {
            public bool ok;
            public string error;
            public string requestId;
            public string mode;
            public string catalogAuthority;
            public string catalogFingerprint;
            public int catalogDefinitionCount;
            public int playerLevel;
            public int tierNumber;
            public string tierId;
            public string seed;
            public int minimumTargetDelta;
            public int mostLikelyTargetDelta;
            public int maximumTargetDelta;

            // Single opening
            public int targetLevel;
            public string selectedDefinitionId;
            public string selectedName;
            public string selectedRarityId;
            public string selectedRarityVisualId;
            public int itemLevel;
            public string qualityId;
            public int augmentSlots;
            public int augmentLevel;
            public string generationFingerprint;
            public double totalWeight;
            public List<Candidate> candidates = new List<Candidate>();

            // Analysis
            public int sampleCount;
            public int successfulOpenings;
            public int rejectedOpenings;
            public double averageTargetLevel;
            public int minimumTargetLevel;
            public int maximumTargetLevel;
            public double averageItemLevel;
            public int minimumItemLevel;
            public int maximumItemLevel;
            public List<DistributionEntry> weaponDistribution =
                new List<DistributionEntry>();
            public List<WeaponBreakdown> weaponBreakdowns =
                new List<WeaponBreakdown>();
            public List<DistributionEntry> rarityDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> qualityDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> targetLevelDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> itemLevelDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> augmentSlotDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> augmentLevelDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> augmentSignatureDistribution =
                new List<DistributionEntry>();
            public List<DistributionEntry> rejectionDistribution =
                new List<DistributionEntry>();
        }

        private sealed class Counter
        {
            private readonly Dictionary<string, int> counts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> labels =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public void Add(string key, string label)
            {
                key = key ?? string.Empty;
                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;
                if (!labels.ContainsKey(key))
                {
                    labels[key] = string.IsNullOrWhiteSpace(label) ? key : label;
                }
            }

            public List<DistributionEntry> Build(int total)
            {
                var values = new List<DistributionEntry>();
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    values.Add(new DistributionEntry
                    {
                        key = pair.Key,
                        label = labels[pair.Key],
                        count = pair.Value,
                        percentage = total <= 0
                            ? 0d
                            : 100d * pair.Value / total,
                    });
                }
                values.Sort(delegate(
                    DistributionEntry left,
                    DistributionEntry right)
                {
                    int byCount = right.count.CompareTo(left.count);
                    return byCount != 0
                        ? byCount
                        : string.CompareOrdinal(left.key, right.key);
                });
                return values;
            }
        }

        private sealed class WeaponAccumulator
        {
            private readonly string definitionId;
            private readonly string displayName;
            private readonly Counter targetLevels = new Counter();
            private readonly Counter itemLevels = new Counter();
            private readonly Counter qualities = new Counter();
            private readonly Counter augmentSlots = new Counter();
            private readonly Counter augmentLevels = new Counter();
            private readonly Counter augmentSignatures = new Counter();
            private int count;

            public WeaponAccumulator(string definitionId, string displayName)
            {
                this.definitionId = definitionId ?? string.Empty;
                this.displayName = string.IsNullOrWhiteSpace(displayName)
                    ? this.definitionId
                    : displayName;
            }

            public void Add(
                StrongboxGeneratedEquipmentObservation observation,
                string qualityId)
            {
                count++;
                string targetLevel = observation.TargetLevel.ToString(
                    CultureInfo.InvariantCulture);
                string itemLevel = observation.ItemLevel.ToString(
                    CultureInfo.InvariantCulture);
                string slots = observation.AugmentSlotCount.ToString(
                    CultureInfo.InvariantCulture);
                targetLevels.Add(targetLevel, targetLevel);
                itemLevels.Add(itemLevel, itemLevel);
                qualities.Add(qualityId, qualityId);
                augmentSlots.Add(slots, slots);

                if (observation.AugmentSlotCount > 0)
                {
                    string level = observation.SharedAugmentLevel.ToString(
                        CultureInfo.InvariantCulture);
                    augmentLevels.Add(level, level);
                    string signature = level + "/" + slots;
                    augmentSignatures.Add(signature, signature);
                }
                else
                {
                    augmentLevels.Add("none", "none");
                    augmentSignatures.Add("none", "none");
                }
            }

            public WeaponBreakdown Build(int successfulOpenings)
            {
                return new WeaponBreakdown
                {
                    definitionId = definitionId,
                    displayName = displayName,
                    count = count,
                    percentage = successfulOpenings <= 0
                        ? 0d
                        : 100d * count / successfulOpenings,
                    targetLevelDistribution = targetLevels.Build(count),
                    itemLevelDistribution = itemLevels.Build(count),
                    qualityDistribution = qualities.Build(count),
                    augmentSlotDistribution = augmentSlots.Build(count),
                    augmentLevelDistribution = augmentLevels.Build(count),
                    augmentSignatureDistribution =
                        augmentSignatures.Build(count),
                };
            }
        }

        private static double nextPoll;

        static StrongboxPreviewBridge()
        {
            EditorApplication.update += Poll;
        }

        private static string BridgeFolder
        {
            get
            {
                string projectRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, ".."));
                return Path.Combine(
                    projectRoot,
                    "Temp",
                    "ShooterMoverStrongboxPreview");
            }
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll)
            {
                return;
            }
            nextPoll = EditorApplication.timeSinceStartup + 0.15d;

            string folder = BridgeFolder;
            if (!Directory.Exists(folder))
            {
                return;
            }

            string[] requests;
            try
            {
                requests = Directory.GetFiles(folder, "*.request.json");
                Array.Sort(requests, StringComparer.Ordinal);
            }
            catch
            {
                return;
            }

            int count = Math.Min(4, requests.Length);
            for (int index = 0; index < count; index++)
            {
                Process(requests[index]);
            }
        }

        private static void Process(string requestPath)
        {
            Request request = null;
            Response response;
            try
            {
                request = JsonUtility.FromJson<Request>(
                    File.ReadAllText(requestPath));
                response = Resolve(request);
            }
            catch (Exception exception)
            {
                response = Failure(
                    request == null ? string.Empty : request.requestId,
                    "strongbox-preview-bridge-exception-"
                        + exception.GetType().Name.ToLowerInvariant());
            }

            string responsePath = requestPath.Replace(
                ".request.json",
                ".response.json");
            string temporaryPath = responsePath + ".tmp";
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(response, true));
                if (File.Exists(responsePath))
                {
                    File.Delete(responsePath);
                }
                File.Move(temporaryPath, responsePath);
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Strongbox preview bridge could not write its response: "
                    + exception.Message);
            }
        }

        private static Response Resolve(Request request)
        {
            if (request == null)
            {
                return Failure(string.Empty, "strongbox-preview-request-null");
            }
            if (string.IsNullOrWhiteSpace(request.requestId))
            {
                return Failure(
                    string.Empty,
                    "strongbox-preview-request-id-missing");
            }
            if (request.playerLevel < 0)
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-player-level-invalid");
            }
            if (request.tierNumber < 1
                || request.tierNumber > StrongboxCatalog.Tiers.Count)
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-tier-invalid");
            }

            ulong rootSeed;
            if (!ulong.TryParse(
                    request.seed,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out rootSeed))
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-seed-invalid");
            }

            string gunCatalogJson;
            try
            {
                if (GunCatalogProvider.GunCatalog == null)
                {
                    return Failure(
                        request.requestId,
                        "strongbox-preview-live-catalog-unavailable");
                }
                gunCatalogJson = GunCatalogJson.Export(
                    GunCatalogProvider.GunCatalog);
            }
            catch (Exception exception)
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-live-catalog-export-exception-"
                        + exception.GetType().Name.ToLowerInvariant());
            }
            if (string.IsNullOrWhiteSpace(gunCatalogJson))
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-live-catalog-export-empty");
            }

            string diagnostic;
            AuthoritativeStrongboxSimulationGateway gateway;
            if (!AuthoritativeStrongboxSimulationGatewayFactory.TryCreate(
                    gunCatalogJson,
                    out gateway,
                    out diagnostic)
                || gateway == null)
            {
                return Failure(
                    request.requestId,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "strongbox-preview-gateway-create-rejected"
                        : diagnostic);
            }

            string mode = string.Equals(
                request.mode,
                "analysis",
                StringComparison.OrdinalIgnoreCase)
                    ? "analysis"
                    : "single";
            Response response = BaseResponse(
                request,
                rootSeed,
                mode,
                gateway);

            if (mode == "analysis")
            {
                int sampleCount = request.sampleCount <= 0
                    ? 1000
                    : request.sampleCount;
                if (sampleCount > MaximumAnalysisSamples)
                {
                    return Failure(
                        request.requestId,
                        "strongbox-preview-sample-count-too-large");
                }
                return ResolveAnalysis(
                    request,
                    rootSeed,
                    sampleCount,
                    gateway,
                    response);
            }

            return ResolveSingle(
                request,
                rootSeed,
                gunCatalogJson,
                gateway,
                response);
        }

        private static Response BaseResponse(
            Request request,
            ulong rootSeed,
            string mode,
            AuthoritativeStrongboxSimulationGateway gateway)
        {
            StrongboxTier tier = StrongboxCatalog.GetByNumber(
                request.tierNumber);
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(
                    request.tierNumber);
            return new Response
            {
                ok = true,
                requestId = request.requestId,
                mode = mode,
                catalogAuthority = LiveCatalogAuthority,
                catalogFingerprint = gateway.Fingerprints.EquipmentCatalog,
                catalogDefinitionCount = gateway.EquipmentDefinitions.Count,
                playerLevel = request.playerLevel,
                tierNumber = request.tierNumber,
                tierId = tier.TierStableId.ToString(),
                seed = rootSeed.ToString(CultureInfo.InvariantCulture),
                minimumTargetDelta = policy.MinimumTargetDelta,
                mostLikelyTargetDelta = policy.MostLikelyTargetDelta,
                maximumTargetDelta = policy.MaximumTargetDelta,
            };
        }

        private static Response ResolveSingle(
            Request request,
            ulong rootSeed,
            string gunCatalogJson,
            AuthoritativeStrongboxSimulationGateway gateway,
            Response response)
        {
            string diagnostic;
            AuthoritativeStrongboxSimulatorLive runtime;
            if (!AuthoritativeStrongboxSimulatorLive.TryCreate(
                    gunCatalogJson,
                    out runtime,
                    out diagnostic)
                || runtime == null)
            {
                return Failure(
                    request.requestId,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "strongbox-preview-runtime-create-rejected"
                        : diagnostic);
            }

            IReadOnlyList<AuthoritativeStrongboxPreparedOpen> prepared =
                runtime.PrepareBatch(
                    new[] { request.tierNumber },
                    request.playerLevel,
                    rootSeed);
            if (prepared == null
                || prepared.Count != 1
                || prepared[0] == null)
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-prepared-opening-invalid");
            }

            AuthoritativeStrongboxPreparedOpen opening = prepared[0];
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(
                    request.tierNumber);
            StrongboxTargetLevelRoll target = policy.RollTargetLevel(
                request.playerLevel,
                opening.Context.RootSeed,
                opening.Context.AlgorithmVersion,
                0UL);
            response.targetLevel = target.TargetLevel;

            BuildCandidates(
                gateway.EquipmentDefinitions,
                policy,
                target,
                request.tierNumber,
                response);

            StrongboxOpeningResultLive openingResult =
                runtime.OpenOrRetry(opening);
            IReadOnlyList<EquipmentInstance> generated =
                runtime.EquipmentFrom(openingResult);
            if (generated == null
                || generated.Count != 1
                || generated[0] == null)
            {
                return Failure(
                    request.requestId,
                    openingResult == null
                        || string.IsNullOrWhiteSpace(
                            openingResult.RejectionCode)
                            ? "strongbox-preview-equipment-count-invalid"
                            : openingResult.RejectionCode);
            }

            EquipmentInstance equipment = generated[0];
            StrongboxEquipmentMetadata selected = FindMetadata(
                gateway.EquipmentDefinitions,
                equipment.DefinitionId);
            if (selected == null)
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-selected-metadata-missing");
            }

            GeneratedEquipmentAugmentSignature signature;
            if (!runtime.TryGetAugmentSignature(
                    equipment.InstanceId,
                    out signature)
                || signature == null)
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-augment-signature-missing");
            }

            response.selectedDefinitionId =
                selected.DefinitionId.ToString();
            response.selectedName = selected.DisplayName;
            response.selectedRarityId = selected.RarityId == null
                ? string.Empty
                : selected.RarityId.ToString();
            response.selectedRarityVisualId = VisualRarity(
                response.selectedRarityId);
            response.itemLevel = equipment.ItemLevel;
            response.qualityId = equipment.QualityId == null
                ? string.Empty
                : equipment.QualityId.ToString();
            response.augmentSlots = signature.Capacity;
            response.augmentLevel = signature.SharedLevel;
            response.generationFingerprint = equipment.Fingerprint;

            for (int index = 0;
                 index < response.candidates.Count;
                 index++)
            {
                response.candidates[index].selected = string.Equals(
                    response.candidates[index].definitionId,
                    response.selectedDefinitionId,
                    StringComparison.Ordinal);
            }
            return response;
        }

        private static Response ResolveAnalysis(
            Request request,
            ulong rootSeed,
            int sampleCount,
            AuthoritativeStrongboxSimulationGateway gateway,
            Response response)
        {
            StrongboxTier tier = StrongboxCatalog.GetByNumber(
                request.tierNumber);
            var scenario = new StrongboxSimulationScenario(
                request.playerLevel,
                tier.TierStableId,
                sampleCount,
                rootSeed);

            var weapons = new Counter();
            var rarities = new Counter();
            var qualities = new Counter();
            var targetLevels = new Counter();
            var itemLevels = new Counter();
            var augmentSlots = new Counter();
            var augmentLevels = new Counter();
            var augmentSignatures = new Counter();
            var rejections = new Counter();
            var weaponDetails =
                new Dictionary<string, WeaponAccumulator>(
                    StringComparer.Ordinal);

            long targetTotal = 0L;
            long itemTotal = 0L;
            int minimumTarget = int.MaxValue;
            int maximumTarget = int.MinValue;
            int minimumItem = int.MaxValue;
            int maximumItem = int.MinValue;

            for (int ordinal = 0; ordinal < sampleCount; ordinal++)
            {
                StrongboxGeneratedEquipmentObservation observation;
                string diagnostic;
                if (!gateway.TryGenerate(
                        scenario,
                        ordinal,
                        out observation,
                        out diagnostic)
                    || observation == null)
                {
                    response.rejectedOpenings++;
                    rejections.Add(
                        string.IsNullOrWhiteSpace(diagnostic)
                            ? "unknown"
                            : diagnostic,
                        string.IsNullOrWhiteSpace(diagnostic)
                            ? "unknown"
                            : diagnostic);
                    continue;
                }

                response.successfulOpenings++;
                StrongboxEquipmentMetadata equipment =
                    observation.Equipment;
                string definitionId = equipment.DefinitionId.ToString();
                string rarityId = equipment.RarityId == null
                    ? string.Empty
                    : equipment.RarityId.ToString();
                string qualityId = observation.QualityId == null
                    ? string.Empty
                    : observation.QualityId.ToString();

                WeaponAccumulator weaponDetail;
                if (!weaponDetails.TryGetValue(
                        definitionId,
                        out weaponDetail))
                {
                    weaponDetail = new WeaponAccumulator(
                        definitionId,
                        equipment.DisplayName);
                    weaponDetails.Add(definitionId, weaponDetail);
                }
                weaponDetail.Add(observation, qualityId);

                weapons.Add(definitionId, equipment.DisplayName);
                rarities.Add(rarityId, rarityId);
                qualities.Add(qualityId, qualityId);
                targetLevels.Add(
                    observation.TargetLevel.ToString(
                        CultureInfo.InvariantCulture),
                    observation.TargetLevel.ToString(
                        CultureInfo.InvariantCulture));
                itemLevels.Add(
                    observation.ItemLevel.ToString(
                        CultureInfo.InvariantCulture),
                    observation.ItemLevel.ToString(
                        CultureInfo.InvariantCulture));
                augmentSlots.Add(
                    observation.AugmentSlotCount.ToString(
                        CultureInfo.InvariantCulture),
                    observation.AugmentSlotCount.ToString(
                        CultureInfo.InvariantCulture));
                if (observation.AugmentSlotCount > 0)
                {
                    augmentLevels.Add(
                        observation.SharedAugmentLevel.ToString(
                            CultureInfo.InvariantCulture),
                        observation.SharedAugmentLevel.ToString(
                            CultureInfo.InvariantCulture));
                    string signature =
                        observation.SharedAugmentLevel.ToString(
                            CultureInfo.InvariantCulture)
                        + "/"
                        + observation.AugmentSlotCount.ToString(
                            CultureInfo.InvariantCulture);
                    augmentSignatures.Add(signature, signature);
                }
                else
                {
                    augmentLevels.Add("none", "none");
                    augmentSignatures.Add("none", "none");
                }

                targetTotal += observation.TargetLevel;
                itemTotal += observation.ItemLevel;
                minimumTarget = Math.Min(
                    minimumTarget,
                    observation.TargetLevel);
                maximumTarget = Math.Max(
                    maximumTarget,
                    observation.TargetLevel);
                minimumItem = Math.Min(
                    minimumItem,
                    observation.ItemLevel);
                maximumItem = Math.Max(
                    maximumItem,
                    observation.ItemLevel);
            }

            response.sampleCount = sampleCount;
            int success = response.successfulOpenings;
            response.averageTargetLevel = success == 0
                ? 0d
                : targetTotal / (double)success;
            response.minimumTargetLevel = success == 0
                ? 0
                : minimumTarget;
            response.maximumTargetLevel = success == 0
                ? 0
                : maximumTarget;
            response.averageItemLevel = success == 0
                ? 0d
                : itemTotal / (double)success;
            response.minimumItemLevel = success == 0
                ? 0
                : minimumItem;
            response.maximumItemLevel = success == 0
                ? 0
                : maximumItem;
            response.weaponDistribution = weapons.Build(success);
            foreach (KeyValuePair<string, WeaponAccumulator> pair
                     in weaponDetails)
            {
                response.weaponBreakdowns.Add(pair.Value.Build(success));
            }
            response.weaponBreakdowns.Sort(delegate(
                WeaponBreakdown left,
                WeaponBreakdown right)
            {
                int byCount = right.count.CompareTo(left.count);
                return byCount != 0
                    ? byCount
                    : string.CompareOrdinal(
                        left.definitionId,
                        right.definitionId);
            });
            response.rarityDistribution = rarities.Build(success);
            response.qualityDistribution = qualities.Build(success);
            response.targetLevelDistribution = targetLevels.Build(success);
            response.itemLevelDistribution = itemLevels.Build(success);
            response.augmentSlotDistribution = augmentSlots.Build(success);
            response.augmentLevelDistribution = augmentLevels.Build(success);
            response.augmentSignatureDistribution =
                augmentSignatures.Build(success);
            response.rejectionDistribution =
                rejections.Build(response.rejectedOpenings);
            return response;
        }

        private static void BuildCandidates(
            IReadOnlyList<StrongboxEquipmentMetadata> metadata,
            StrongboxHybridLootPolicy policy,
            StrongboxTargetLevelRoll target,
            int tierNumber,
            Response response)
        {
            bool topTier = tierNumber == StrongboxCatalog.Tiers.Count;
            double total = 0d;
            for (int index = 0; index < metadata.Count; index++)
            {
                StrongboxEquipmentMetadata item = metadata[index];
                int distance = Math.Abs(
                    item.AnchorLevel - target.TargetLevel);
                StrongboxRarityProfile rarity = FindRarity(
                    policy,
                    item.RarityId);
                bool hardEligible = item.Available
                    && (!item.TopBoxOnly || topTier)
                    && rarity != null;
                double rarityMultiplier = rarity == null
                    ? 0d
                    : rarity.SelectionMultiplierMilli
                        / (double)
                            StrongboxHybridLootPolicy.RarityMultiplierScale;
                double levelAffinity =
                    distance <= policy.DefinitionSelectionRadius
                        ? policy.DefinitionBellWeights[distance]
                            .WeightMillionths
                            / (double)
                                StrongboxHybridLootPolicy
                                    .DefinitionWeightScale
                        : 0d;
                double weight = 0d;
                string reason;
                if (!item.Available)
                {
                    reason = "not-live";
                }
                else if (item.TopBoxOnly && !topTier)
                {
                    reason = "top-box-only";
                }
                else if (rarity == null)
                {
                    reason = "rarity-not-in-tier-policy";
                }
                else
                {
                    weight = policy.EvaluateDefinitionWeight(
                        target,
                        item.AnchorLevel,
                        item.AuthoredBaseWeight,
                        item.RarityId);
                    if (distance > policy.DefinitionSelectionRadius)
                    {
                        reason = "outside-level-window";
                    }
                    else if (rarity.SelectionMultiplierMilli == 0)
                    {
                        reason = "rarity-disabled-for-tier";
                    }
                    else if (weight <= 0d)
                    {
                        reason = "zero-weight";
                    }
                    else
                    {
                        reason = "eligible";
                    }
                }

                total += weight;
                response.candidates.Add(new Candidate
                {
                    rollOrder = index,
                    definitionId = item.DefinitionId.ToString(),
                    displayName = item.DisplayName,
                    rarityId = item.RarityId == null
                        ? string.Empty
                        : item.RarityId.ToString(),
                    firstAppearanceLevel = item.FirstAppearanceLevel,
                    peakLevel = item.AnchorLevel,
                    distance = distance,
                    baseWeight = item.AuthoredBaseWeight,
                    levelAffinity = levelAffinity,
                    rarityMultiplier = rarityMultiplier,
                    finalWeight = weight,
                    hardEligible = hardEligible,
                    reason = reason,
                });
            }

            response.totalWeight = total;
            if (total <= 0d)
            {
                return;
            }
            for (int index = 0;
                 index < response.candidates.Count;
                 index++)
            {
                Candidate candidate = response.candidates[index];
                candidate.chancePercent =
                    100d * candidate.finalWeight / total;
            }
        }

        private static StrongboxEquipmentMetadata FindMetadata(
            IReadOnlyList<StrongboxEquipmentMetadata> metadata,
            ShooterMover.Domain.Common.StableId definitionId)
        {
            for (int index = 0; index < metadata.Count; index++)
            {
                if (metadata[index].DefinitionId == definitionId)
                {
                    return metadata[index];
                }
            }
            return null;
        }

        private static StrongboxRarityProfile FindRarity(
            StrongboxHybridLootPolicy policy,
            ShooterMover.Domain.Common.StableId rarityId)
        {
            for (int index = 0;
                 index < policy.RarityProfiles.Count;
                 index++)
            {
                StrongboxRarityProfile value =
                    policy.RarityProfiles[index];
                if (value.RarityId == rarityId)
                {
                    return value;
                }
            }
            return null;
        }

        private static string VisualRarity(string rarityId)
        {
            string value = (rarityId ?? string.Empty).ToLowerInvariant();
            if (value.Contains("artifact") || value.Contains("mythic"))
            {
                return "mythic";
            }
            if (value.Contains("legendary")) return "legendary";
            if (value.Contains("epic")) return "epic";
            if (value.Contains("rare")
                && !value.Contains("uncommon"))
            {
                return "rare";
            }
            return "common";
        }

        private static Response Failure(string requestId, string error)
        {
            return new Response
            {
                ok = false,
                requestId = requestId ?? string.Empty,
                error = string.IsNullOrWhiteSpace(error)
                    ? "strongbox-preview-unknown-error"
                    : error,
            };
        }
    }
}

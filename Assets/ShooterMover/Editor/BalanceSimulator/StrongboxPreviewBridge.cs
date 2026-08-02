using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Simulation;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using UnityEditor;
using UnityEngine;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Local Editor bridge for the Item Maker Strongbox page. Requests are exchanged
    /// through the project's Temp folder so no second web listener, URL reservation, or
    /// duplicated JavaScript loot formula is required. The winning item is opened through
    /// AuthoritativeStrongboxSimulatorLive and the diagnostic table calls the same hybrid
    /// policy used by StrongboxHybridEquipmentGenerationResolver.
    /// </summary>
    [InitializeOnLoad]
    public static class StrongboxPreviewBridge
    {
        [Serializable]
        private sealed class Request
        {
            public string requestId;
            public int playerLevel;
            public int tierNumber;
            public string seed;
            public string catalogSource;
            public string gunCatalogJson;
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
        private sealed class Response
        {
            public bool ok;
            public string error;
            public string requestId;
            public string catalogSource;
            public int playerLevel;
            public int tierNumber;
            public string tierId;
            public string seed;
            public int minimumTargetDelta;
            public int mostLikelyTargetDelta;
            public int maximumTargetDelta;
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
                return Failure(string.Empty, "strongbox-preview-request-id-missing");
            }
            if (request.playerLevel < 0)
            {
                return Failure(request.requestId, "strongbox-preview-player-level-invalid");
            }
            if (request.tierNumber < 1
                || request.tierNumber > StrongboxCatalog.Tiers.Count)
            {
                return Failure(request.requestId, "strongbox-preview-tier-invalid");
            }
            if (string.IsNullOrWhiteSpace(request.gunCatalogJson))
            {
                return Failure(request.requestId, "strongbox-preview-catalog-missing");
            }

            ulong rootSeed;
            if (!ulong.TryParse(
                    request.seed,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out rootSeed))
            {
                return Failure(request.requestId, "strongbox-preview-seed-invalid");
            }

            string diagnostic;
            AuthoritativeStrongboxSimulationGateway gateway;
            if (!AuthoritativeStrongboxSimulationGatewayFactory.TryCreate(
                    request.gunCatalogJson,
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

            AuthoritativeStrongboxSimulatorLive runtime;
            if (!AuthoritativeStrongboxSimulatorLive.TryCreate(
                    request.gunCatalogJson,
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

            StrongboxTier tier = StrongboxCatalog.GetByNumber(request.tierNumber);
            IReadOnlyList<AuthoritativeStrongboxPreparedOpen> prepared =
                runtime.PrepareBatch(
                    new[] { request.tierNumber },
                    request.playerLevel,
                    rootSeed);
            if (prepared == null || prepared.Count != 1 || prepared[0] == null)
            {
                return Failure(
                    request.requestId,
                    "strongbox-preview-prepared-opening-invalid");
            }

            AuthoritativeStrongboxPreparedOpen opening = prepared[0];
            StrongboxHybridLootPolicy policy =
                StrongboxHybridLootCatalog.GetByTierNumber(request.tierNumber);
            StrongboxTargetLevelRoll target = policy.RollTargetLevel(
                request.playerLevel,
                opening.Context.RootSeed,
                opening.Context.AlgorithmVersion,
                0UL);

            var response = new Response
            {
                ok = true,
                requestId = request.requestId,
                catalogSource = request.catalogSource ?? string.Empty,
                playerLevel = request.playerLevel,
                tierNumber = request.tierNumber,
                tierId = tier.TierStableId.ToString(),
                seed = rootSeed.ToString(CultureInfo.InvariantCulture),
                minimumTargetDelta = policy.MinimumTargetDelta,
                mostLikelyTargetDelta = policy.MostLikelyTargetDelta,
                maximumTargetDelta = policy.MaximumTargetDelta,
                targetLevel = target.TargetLevel,
            };

            BuildCandidates(
                gateway.EquipmentDefinitions,
                policy,
                target,
                request.tierNumber,
                response);

            StrongboxOpeningResultLive openingResult = runtime.OpenOrRetry(opening);
            IReadOnlyList<EquipmentInstance> generated =
                runtime.EquipmentFrom(openingResult);
            if (generated == null || generated.Count != 1 || generated[0] == null)
            {
                return Failure(
                    request.requestId,
                    openingResult == null
                        || string.IsNullOrWhiteSpace(openingResult.RejectionCode)
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

            response.selectedDefinitionId = selected.DefinitionId.ToString();
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

            for (int index = 0; index < response.candidates.Count; index++)
            {
                response.candidates[index].selected = string.Equals(
                    response.candidates[index].definitionId,
                    response.selectedDefinitionId,
                    StringComparison.Ordinal);
            }
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
                int distance = Math.Abs(item.AnchorLevel - target.TargetLevel);
                StrongboxRarityProfile rarity = FindRarity(
                    policy,
                    item.RarityId);
                bool hardEligible = item.Available
                    && (!item.TopBoxOnly || topTier)
                    && rarity != null;
                double rarityMultiplier = rarity == null
                    ? 0d
                    : rarity.SelectionMultiplierMilli
                        / (double)StrongboxHybridLootPolicy.RarityMultiplierScale;
                double levelAffinity = distance <= policy.DefinitionSelectionRadius
                    ? policy.DefinitionBellWeights[distance].WeightMillionths
                        / (double)StrongboxHybridLootPolicy.DefinitionWeightScale
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
            for (int index = 0; index < response.candidates.Count; index++)
            {
                Candidate candidate = response.candidates[index];
                candidate.chancePercent = 100d * candidate.finalWeight / total;
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
            for (int index = 0; index < policy.RarityProfiles.Count; index++)
            {
                StrongboxRarityProfile value = policy.RarityProfiles[index];
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
            if (value.Contains("rare") && !value.Contains("uncommon"))
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

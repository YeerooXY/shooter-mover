using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    internal static class CollectedRunRewardTransferCanonicalV1
    {
        public static void Append(
            StringBuilder builder,
            string key,
            object value)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(
                    "A canonical field name is required.",
                    nameof(key));

            string text;
            if (value == null)
            {
                text = string.Empty;
            }
            else if (value is IFormattable formattable)
            {
                text = formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture);
            }
            else
            {
                text = value.ToString();
            }

            builder.Append('|');
            builder.Append(key.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(key);
            builder.Append('=');
            builder.Append(text.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(text);
        }

        public static string Hash(string canonicalText)
        {
            if (canonicalText == null)
                throw new ArgumentNullException(nameof(canonicalText));

            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
                byte[] hash = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder("sha256:");
                for (int index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }

        public static StableId DeriveStableId(
            string category,
            string prefix,
            string material)
        {
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException(
                    "A stable-ID category is required.",
                    nameof(category));
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException(
                    "A stable-ID prefix is required.",
                    nameof(prefix));
            if (string.IsNullOrWhiteSpace(material))
                throw new ArgumentException(
                    "Stable-ID derivation material is required.",
                    nameof(material));

            string hash = Hash(material).Substring(7, 40);
            return StableId.Create(
                category.Trim(),
                prefix.Trim() + "-" + hash);
        }
    }

    /// <summary>
    /// Immutable transfer-facing projection of one canonical collected-reward journal
    /// record. The PICKUP-LIVE-001 adapter must copy these values from the shared Run
    /// Session record without rebuilding them from drop profiles or UI state.
    /// </summary>
    public sealed class CollectedRunRewardTransferItemV1
    {
        private readonly string canonicalText;

        public CollectedRunRewardTransferItemV1(
            StableId rewardInstanceStableId,
            RewardGrantKindV1 rewardKind,
            StableId contentStableId,
            long quantity,
            StableId pickupStableId,
            StableId sourceGrantStableId,
            StableId dropOperationStableId,
            StableId terminalEventStableId,
            StableId triggeringEventStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            long sourceLifecycleGeneration,
            StableId sourceDefinitionStableId,
            StableId attributedParticipantStableId,
            string generatedBatchFingerprint,
            string generatedRewardFingerprint,
            StableId roomStableId,
            double worldPositionX,
            double worldPositionY,
            string worldSpawnFingerprint,
            string availablePickupFingerprint,
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId,
            StableId collectionOperationStableId,
            long collectionOrder,
            long collectedAtAuthoritativeTick,
            string collectedRewardFingerprint)
        {
            RewardInstanceStableId = rewardInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(rewardInstanceStableId));
            if (!Enum.IsDefined(typeof(RewardGrantKindV1), rewardKind))
                throw new ArgumentOutOfRangeException(nameof(rewardKind));
            ContentStableId = contentStableId
                ?? throw new ArgumentNullException(nameof(contentStableId));
            if (quantity < 1L)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            PickupStableId = pickupStableId
                ?? throw new ArgumentNullException(nameof(pickupStableId));
            SourceGrantStableId = sourceGrantStableId
                ?? throw new ArgumentNullException(nameof(sourceGrantStableId));
            DropOperationStableId = dropOperationStableId
                ?? throw new ArgumentNullException(
                    nameof(dropOperationStableId));
            TerminalEventStableId = terminalEventStableId
                ?? throw new ArgumentNullException(
                    nameof(terminalEventStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(runLifecycleGeneration));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(
                    nameof(sourceEntityStableId));
            if (sourceLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceLifecycleGeneration));
            SourceDefinitionStableId = sourceDefinitionStableId
                ?? throw new ArgumentNullException(
                    nameof(sourceDefinitionStableId));
            AttributedParticipantStableId =
                attributedParticipantStableId
                ?? throw new ArgumentNullException(
                    nameof(attributedParticipantStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (!IsFinite(worldPositionX)
                || !IsFinite(worldPositionY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(worldPositionX));
            }
            CollectorEntityStableId = collectorEntityStableId
                ?? throw new ArgumentNullException(
                    nameof(collectorEntityStableId));
            CollectorParticipantStableId =
                collectorParticipantStableId
                ?? throw new ArgumentNullException(
                    nameof(collectorParticipantStableId));
            CollectionOperationStableId =
                collectionOperationStableId
                ?? throw new ArgumentNullException(
                    nameof(collectionOperationStableId));
            if (collectionOrder < 1L)
                throw new ArgumentOutOfRangeException(
                    nameof(collectionOrder));
            if (collectedAtAuthoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(collectedAtAuthoritativeTick));

            RewardKind = rewardKind;
            Quantity = quantity;
            TriggeringEventStableId = triggeringEventStableId;
            RunLifecycleGeneration = runLifecycleGeneration;
            SourcePlacementStableId = sourcePlacementStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            GeneratedBatchFingerprint = RequireFingerprint(
                generatedBatchFingerprint,
                nameof(generatedBatchFingerprint));
            GeneratedRewardFingerprint = RequireFingerprint(
                generatedRewardFingerprint,
                nameof(generatedRewardFingerprint));
            WorldPositionX = worldPositionX;
            WorldPositionY = worldPositionY;
            WorldSpawnFingerprint = RequireFingerprint(
                worldSpawnFingerprint,
                nameof(worldSpawnFingerprint));
            AvailablePickupFingerprint = RequireFingerprint(
                availablePickupFingerprint,
                nameof(availablePickupFingerprint));
            CollectionOrder = collectionOrder;
            CollectedAtAuthoritativeTick = collectedAtAuthoritativeTick;
            CollectedRewardFingerprint = RequireFingerprint(
                collectedRewardFingerprint,
                nameof(collectedRewardFingerprint));

            var builder = new StringBuilder(
                "schema=collected-run-reward-transfer-item-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "reward-instance",
                RewardInstanceStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "reward-kind",
                (int)RewardKind);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "content",
                ContentStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "quantity",
                Quantity);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "pickup",
                PickupStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "source-grant",
                SourceGrantStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "drop-operation",
                DropOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "terminal-event",
                TerminalEventStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "triggering-event",
                TriggeringEventStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "run",
                RunStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "run-lifecycle",
                RunLifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "source-entity",
                SourceEntityStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "source-placement",
                SourcePlacementStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "source-lifecycle",
                SourceLifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "source-definition",
                SourceDefinitionStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "participant",
                AttributedParticipantStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "generated-batch",
                GeneratedBatchFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "generated-reward",
                GeneratedRewardFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "room",
                RoomStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "world-x",
                WorldPositionX);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "world-y",
                WorldPositionY);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "world-spawn",
                WorldSpawnFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "available-pickup",
                AvailablePickupFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "collector-entity",
                CollectorEntityStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "collector-participant",
                CollectorParticipantStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "collection-operation",
                CollectionOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "collection-order",
                CollectionOrder);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "collected-tick",
                CollectedAtAuthoritativeTick);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "collected-reward",
                CollectedRewardFingerprint);
            canonicalText = builder.ToString();
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(canonicalText);
        }

        public StableId RewardInstanceStableId { get; }
        public RewardGrantKindV1 RewardKind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public StableId PickupStableId { get; }
        public StableId SourceGrantStableId { get; }
        public StableId DropOperationStableId { get; }
        public StableId TerminalEventStableId { get; }
        public StableId TriggeringEventStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourcePlacementStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public StableId SourceDefinitionStableId { get; }
        public StableId AttributedParticipantStableId { get; }
        public string GeneratedBatchFingerprint { get; }
        public string GeneratedRewardFingerprint { get; }
        public StableId RoomStableId { get; }
        public double WorldPositionX { get; }
        public double WorldPositionY { get; }
        public string WorldSpawnFingerprint { get; }
        public string AvailablePickupFingerprint { get; }
        public StableId CollectorEntityStableId { get; }
        public StableId CollectorParticipantStableId { get; }
        public StableId CollectionOperationStableId { get; }
        public long CollectionOrder { get; }
        public long CollectedAtAuthoritativeTick { get; }
        public string CollectedRewardFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value);
        }

        private static string RequireFingerprint(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "A deterministic fingerprint is required.",
                    parameterName);
            return value.Trim();
        }
    }

    /// <summary>
    /// One immutable, order-independent transfer batch frozen only after the existing
    /// mission-result authority accepted the exact completion.
    /// </summary>
    public sealed class CollectedRunRewardTransferBatchV1
    {
        private readonly ReadOnlyCollection<
            CollectedRunRewardTransferItemV1> rewards;
        private readonly string canonicalText;

        public CollectedRunRewardTransferBatchV1(
            StableId transferOperationStableId,
            StableId runStableId,
            long acceptedLifecycleGeneration,
            StableId acceptedMissionResultStableId,
            MissionResultPayloadV1 acceptedMissionResult,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            IEnumerable<CollectedRunRewardTransferItemV1> collectedRewards)
        {
            TransferOperationStableId = transferOperationStableId
                ?? throw new ArgumentNullException(
                    nameof(transferOperationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (acceptedLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(acceptedLifecycleGeneration));
            AcceptedMissionResultStableId =
                acceptedMissionResultStableId
                ?? throw new ArgumentNullException(
                    nameof(acceptedMissionResultStableId));
            AcceptedMissionResult = acceptedMissionResult
                ?? throw new ArgumentNullException(
                    nameof(acceptedMissionResult));
            SelectedCharacterStableId =
                selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            if (expectedCharacterRevision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(expectedCharacterRevision));
            if (string.IsNullOrWhiteSpace(
                expectedCharacterFingerprint))
            {
                throw new ArgumentException(
                    "An expected permanent-character fingerprint is required.",
                    nameof(expectedCharacterFingerprint));
            }
            if (AcceptedMissionResult.RunStableId != RunStableId)
            {
                throw new ArgumentException(
                    "The accepted mission result must belong to the exact run.",
                    nameof(acceptedMissionResult));
            }
            if (AcceptedMissionResult.RoutePayload == null
                || AcceptedMissionResult.RoutePayload
                    .SelectedCharacterStableId
                    != SelectedCharacterStableId)
            {
                throw new ArgumentException(
                    "The accepted mission result must belong to the exact selected character.",
                    nameof(acceptedMissionResult));
            }

            var copy = new List<CollectedRunRewardTransferItemV1>(
                collectedRewards
                ?? throw new ArgumentNullException(
                    nameof(collectedRewards)));
            if (copy.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Transfer rewards cannot contain null.",
                    nameof(collectedRewards));
            }
            copy.Sort(CompareRewards);
            var rewardIds = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                CollectedRunRewardTransferItemV1 reward = copy[index];
                if (reward.RunStableId != RunStableId)
                {
                    throw new ArgumentException(
                        "Every transfer reward must belong to the exact run.",
                        nameof(collectedRewards));
                }
                if (reward.RunLifecycleGeneration
                    != acceptedLifecycleGeneration)
                {
                    throw new ArgumentException(
                        "Every transfer reward must belong to the accepted lifecycle.",
                        nameof(collectedRewards));
                }
                if (!rewardIds.Add(reward.RewardInstanceStableId))
                {
                    throw new ArgumentException(
                        "A transfer batch cannot contain duplicate reward identities.",
                        nameof(collectedRewards));
                }
            }

            AcceptedLifecycleGeneration =
                acceptedLifecycleGeneration;
            ExpectedCharacterRevision =
                expectedCharacterRevision;
            ExpectedCharacterFingerprint =
                expectedCharacterFingerprint.Trim();
            rewards =
                new ReadOnlyCollection<
                    CollectedRunRewardTransferItemV1>(copy);

            var builder = new StringBuilder(
                "schema=collected-run-reward-transfer-batch-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "operation",
                TransferOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "run",
                RunStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "lifecycle",
                AcceptedLifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "mission-result-id",
                AcceptedMissionResultStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "mission-result",
                AcceptedMissionResult.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "character-revision",
                ExpectedCharacterRevision);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "character-fingerprint",
                ExpectedCharacterFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "reward-count",
                rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "reward:" + index.ToString(
                        CultureInfo.InvariantCulture),
                    rewards[index].Fingerprint);
            }
            canonicalText = builder.ToString();
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(canonicalText);
        }

        public StableId TransferOperationStableId { get; }
        public StableId RunStableId { get; }
        public long AcceptedLifecycleGeneration { get; }
        public StableId AcceptedMissionResultStableId { get; }
        public MissionResultPayloadV1 AcceptedMissionResult { get; }
        public StableId SelectedCharacterStableId { get; }
        public long ExpectedCharacterRevision { get; }
        public string ExpectedCharacterFingerprint { get; }
        public IReadOnlyList<CollectedRunRewardTransferItemV1> Rewards
        {
            get { return rewards; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public StableId DeriveChildOperationStableId(
            CollectedRunRewardTransferItemV1 reward,
            string authorityTarget)
        {
            RequireOwnedReward(reward);
            return CollectedRunRewardTransferCanonicalV1
                .DeriveStableId(
                    "operation",
                    "collected-run-transfer-child",
                    Fingerprint
                        + "|"
                        + authorityTarget
                        + "|"
                        + reward.RewardInstanceStableId
                        + "|"
                        + reward.Fingerprint);
        }

        public StableId DeriveChildTransactionStableId(
            CollectedRunRewardTransferItemV1 reward,
            string authorityTarget)
        {
            RequireOwnedReward(reward);
            return CollectedRunRewardTransferCanonicalV1
                .DeriveStableId(
                    "transaction",
                    "collected-run-transfer-child",
                    Fingerprint
                        + "|"
                        + authorityTarget
                        + "|"
                        + reward.RewardInstanceStableId
                        + "|"
                        + reward.Fingerprint);
        }

        public StableId DeriveSaveOperationStableId()
        {
            return CollectedRunRewardTransferCanonicalV1
                .DeriveStableId(
                    "operation",
                    "collected-run-transfer-save",
                    Fingerprint);
        }

        private void RequireOwnedReward(
            CollectedRunRewardTransferItemV1 reward)
        {
            if (reward == null)
                throw new ArgumentNullException(nameof(reward));
            for (int index = 0; index < rewards.Count; index++)
            {
                if (rewards[index].RewardInstanceStableId
                        == reward.RewardInstanceStableId
                    && string.Equals(
                        rewards[index].Fingerprint,
                        reward.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }
            throw new ArgumentException(
                "The child reward is not part of this exact transfer batch.",
                nameof(reward));
        }

        private static int CompareRewards(
            CollectedRunRewardTransferItemV1 left,
            CollectedRunRewardTransferItemV1 right)
        {
            int identity = string.CompareOrdinal(
                left.RewardInstanceStableId.ToString(),
                right.RewardInstanceStableId.ToString());
            return identity != 0
                ? identity
                : string.CompareOrdinal(
                    left.Fingerprint,
                    right.Fingerprint);
        }
    }

    public sealed class CollectedRunRewardTransferReceiptV1
    {
        private readonly ReadOnlyCollection<StableId>
            appliedRewardStableIds;
        private readonly ReadOnlyDictionary<string, string>
            authorityFingerprints;
        private readonly string canonicalText;

        public CollectedRunRewardTransferReceiptV1(
            StableId operationStableId,
            string batchFingerprint,
            StableId runStableId,
            long acceptedLifecycleGeneration,
            StableId missionResultStableId,
            string missionResultFingerprint,
            StableId selectedCharacterStableId,
            IEnumerable<StableId> appliedRewardStableIds,
            IDictionary<string, string> authorityFingerprints)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(
                    nameof(operationStableId));
            if (string.IsNullOrWhiteSpace(batchFingerprint))
                throw new ArgumentException(
                    "A batch fingerprint is required.",
                    nameof(batchFingerprint));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (acceptedLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(acceptedLifecycleGeneration));
            MissionResultStableId = missionResultStableId
                ?? throw new ArgumentNullException(
                    nameof(missionResultStableId));
            if (string.IsNullOrWhiteSpace(
                missionResultFingerprint))
            {
                throw new ArgumentException(
                    "A mission-result fingerprint is required.",
                    nameof(missionResultFingerprint));
            }
            SelectedCharacterStableId =
                selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));

            var rewardCopy = new List<StableId>(
                appliedRewardStableIds
                ?? throw new ArgumentNullException(
                    nameof(appliedRewardStableIds)));
            if (rewardCopy.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Applied reward identities cannot contain null.",
                    nameof(appliedRewardStableIds));
            }
            rewardCopy.Sort((left, right) =>
                string.CompareOrdinal(
                    left.ToString(),
                    right.ToString()));
            for (int index = 1; index < rewardCopy.Count; index++)
            {
                if (rewardCopy[index - 1] == rewardCopy[index])
                {
                    throw new ArgumentException(
                        "A receipt cannot contain duplicate reward identities.",
                        nameof(appliedRewardStableIds));
                }
            }

            var authorityCopy =
                new SortedDictionary<string, string>(
                    StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in
                authorityFingerprints
                ?? throw new ArgumentNullException(
                    nameof(authorityFingerprints)))
            {
                if (string.IsNullOrWhiteSpace(pair.Key)
                    || string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException(
                        "Authority receipt keys and fingerprints must be non-empty.",
                        nameof(authorityFingerprints));
                }
                authorityCopy.Add(
                    pair.Key.Trim(),
                    pair.Value.Trim());
            }

            BatchFingerprint = batchFingerprint.Trim();
            AcceptedLifecycleGeneration =
                acceptedLifecycleGeneration;
            MissionResultFingerprint =
                missionResultFingerprint.Trim();
            this.appliedRewardStableIds =
                new ReadOnlyCollection<StableId>(rewardCopy);
            this.authorityFingerprints =
                new ReadOnlyDictionary<string, string>(
                    authorityCopy);

            var builder = new StringBuilder(
                "schema=collected-run-reward-transfer-receipt-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "operation",
                OperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "batch",
                BatchFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "run",
                RunStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "lifecycle",
                AcceptedLifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "mission-result-id",
                MissionResultStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "mission-result",
                MissionResultFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "reward-count",
                this.appliedRewardStableIds.Count);
            for (int index = 0;
                index < this.appliedRewardStableIds.Count;
                index++)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "reward:" + index.ToString(
                        CultureInfo.InvariantCulture),
                    this.appliedRewardStableIds[index]);
            }
            foreach (KeyValuePair<string, string> pair in
                this.authorityFingerprints)
            {
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "authority:" + pair.Key,
                    pair.Value);
            }
            canonicalText = builder.ToString();
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(
                    canonicalText);
        }

        public StableId OperationStableId { get; }
        public string BatchFingerprint { get; }
        public StableId RunStableId { get; }
        public long AcceptedLifecycleGeneration { get; }
        public StableId MissionResultStableId { get; }
        public string MissionResultFingerprint { get; }
        public StableId SelectedCharacterStableId { get; }
        public IReadOnlyList<StableId> AppliedRewardStableIds
        {
            get { return appliedRewardStableIds; }
        }
        public IReadOnlyDictionary<string, string>
            AuthorityFingerprints
        {
            get { return authorityFingerprints; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }
}

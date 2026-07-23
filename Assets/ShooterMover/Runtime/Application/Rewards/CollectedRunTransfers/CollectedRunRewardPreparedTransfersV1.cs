using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    public enum CollectedRunRewardPreparedTransferStateV1
    {
        AwaitingAcceptedEnd = 1,
        Prepared = 2,
        Persisted = 3,
    }

    /// <summary>
    /// Durable crash-recovery custody for one exact collected-run transfer. It owns no
    /// permanent inventory or currency; it retains immutable transfer material until the
    /// matching receipt is durably confirmed.
    /// </summary>
    public sealed class CollectedRunRewardPreparedTransferV1
    {
        private readonly ReadOnlyCollection<CollectedRunRewardTransferItemV1> rewards;
        private readonly ReadOnlyCollection<EquipmentInstance> equipment;
        private readonly ReadOnlyCollection<StrongboxInstanceContextV1> strongboxes;
        private readonly ReadOnlyDictionary<string, string> frozenAuthorityFingerprints;
        private readonly string canonicalText;

        private CollectedRunRewardPreparedTransferV1(
            CollectedRunRewardPreparedTransferStateV1 state,
            StableId custodyStableId,
            StableId preparationOperationStableId,
            StableId transferOperationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            StableId endOperationStableId,
            string endCommandFingerprint,
            StableId acceptedMissionResultStableId,
            string acceptedMissionResultFingerprint,
            string batchFingerprint,
            string applicationPlanFingerprint,
            string persistedReceiptFingerprint,
            ulong generationRootSeed,
            int generationAlgorithmVersion,
            ProgressionContext progressionContext,
            string eventModifierFingerprint,
            long expectedMoneySequence,
            long expectedScrapSequence,
            long expectedHoldingsSequence,
            IDictionary<string, string> frozenAuthorityFingerprints,
            IEnumerable<CollectedRunRewardTransferItemV1> rewards,
            IEnumerable<EquipmentInstance> equipment,
            IEnumerable<StrongboxInstanceContextV1> strongboxes)
        {
            if (!Enum.IsDefined(typeof(CollectedRunRewardPreparedTransferStateV1), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            CustodyStableId = custodyStableId
                ?? throw new ArgumentNullException(nameof(custodyStableId));
            PreparationOperationStableId = preparationOperationStableId
                ?? throw new ArgumentNullException(nameof(preparationOperationStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(nameof(selectedCharacterStableId));
            if (expectedCharacterRevision < 0L)
                throw new ArgumentOutOfRangeException(nameof(expectedCharacterRevision));
            RequireText(expectedCharacterFingerprint, nameof(expectedCharacterFingerprint));
            EndOperationStableId = endOperationStableId
                ?? throw new ArgumentNullException(nameof(endOperationStableId));
            RequireText(endCommandFingerprint, nameof(endCommandFingerprint));
            if (generationAlgorithmVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(generationAlgorithmVersion));
            ProgressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            RequireText(eventModifierFingerprint, nameof(eventModifierFingerprint));
            if (expectedMoneySequence < 0L
                || expectedScrapSequence < 0L
                || expectedHoldingsSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedMoneySequence));
            }

            bool accepted = state
                != CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd;
            if (accepted)
            {
                if (transferOperationStableId == null
                    || acceptedMissionResultStableId == null)
                {
                    throw new ArgumentException(
                        "Prepared custody requires accepted operation/result identities.");
                }
                RequireText(
                    acceptedMissionResultFingerprint,
                    nameof(acceptedMissionResultFingerprint));
                RequireText(batchFingerprint, nameof(batchFingerprint));
                RequireText(
                    applicationPlanFingerprint,
                    nameof(applicationPlanFingerprint));
            }
            else if (transferOperationStableId != null
                || acceptedMissionResultStableId != null
                || !string.IsNullOrEmpty(acceptedMissionResultFingerprint)
                || !string.IsNullOrEmpty(batchFingerprint)
                || !string.IsNullOrEmpty(applicationPlanFingerprint)
                || !string.IsNullOrEmpty(persistedReceiptFingerprint))
            {
                throw new ArgumentException(
                    "Awaiting-End custody cannot claim accepted transfer facts.");
            }
            if (state == CollectedRunRewardPreparedTransferStateV1.Persisted)
                RequireText(
                    persistedReceiptFingerprint,
                    nameof(persistedReceiptFingerprint));
            else if (!string.IsNullOrEmpty(persistedReceiptFingerprint))
                throw new ArgumentException(
                    "Only persisted custody may carry a receipt fingerprint.",
                    nameof(persistedReceiptFingerprint));

            var rewardCopy = new List<CollectedRunRewardTransferItemV1>(
                rewards ?? throw new ArgumentNullException(nameof(rewards)));
            if (rewardCopy.Any(item => item == null))
                throw new ArgumentException(
                    "Prepared rewards cannot contain null.",
                    nameof(rewards));
            rewardCopy.Sort((left, right) =>
                left.RewardInstanceStableId.CompareTo(
                    right.RewardInstanceStableId));
            var rewardIds = new HashSet<StableId>();
            for (int index = 0; index < rewardCopy.Count; index++)
            {
                CollectedRunRewardTransferItemV1 reward = rewardCopy[index];
                if (reward.RunStableId != RunStableId
                    || reward.RunLifecycleGeneration != lifecycleGeneration
                    || !rewardIds.Add(reward.RewardInstanceStableId))
                {
                    throw new ArgumentException(
                        "Prepared rewards must be unique and belong to the exact run lifecycle.",
                        nameof(rewards));
                }
            }

            var equipmentCopy = new List<EquipmentInstance>(
                equipment ?? Array.Empty<EquipmentInstance>());
            if (equipmentCopy.Any(item => item == null))
                throw new ArgumentException(
                    "Prepared equipment cannot contain null.",
                    nameof(equipment));
            equipmentCopy.Sort((left, right) =>
                left.InstanceId.CompareTo(right.InstanceId));
            var equipmentIds = new HashSet<StableId>();
            for (int index = 0; index < equipmentCopy.Count; index++)
                if (!equipmentIds.Add(equipmentCopy[index].InstanceId))
                    throw new ArgumentException(
                        "Prepared equipment identities must be unique.",
                        nameof(equipment));

            var strongboxCopy = new List<StrongboxInstanceContextV1>(
                strongboxes ?? Array.Empty<StrongboxInstanceContextV1>());
            if (strongboxCopy.Any(item => item == null))
                throw new ArgumentException(
                    "Prepared strongboxes cannot contain null.",
                    nameof(strongboxes));
            strongboxCopy.Sort();
            var strongboxIds = new HashSet<StableId>();
            for (int index = 0; index < strongboxCopy.Count; index++)
                if (!strongboxIds.Add(strongboxCopy[index].InstanceStableId))
                    throw new ArgumentException(
                        "Prepared strongbox identities must be unique.",
                        nameof(strongboxes));

            for (int index = 0; index < rewardCopy.Count; index++)
            {
                CollectedRunRewardTransferItemV1 reward = rewardCopy[index];
                if (reward.RewardKind == RewardGrantKindV1.EquipmentReference
                    && !equipmentIds.Contains(reward.RewardInstanceStableId))
                {
                    throw new ArgumentException(
                        "Every equipment reward requires its exact retained instance.",
                        nameof(equipment));
                }
                if (reward.RewardKind == RewardGrantKindV1.Strongbox
                    && !strongboxIds.Contains(reward.RewardInstanceStableId))
                {
                    throw new ArgumentException(
                        "Every strongbox reward requires its exact unopened context.",
                        nameof(strongboxes));
                }
            }

            var authorityCopy = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in
                frozenAuthorityFingerprints
                ?? throw new ArgumentNullException(
                    nameof(frozenAuthorityFingerprints)))
            {
                RequireText(pair.Key, nameof(frozenAuthorityFingerprints));
                RequireText(pair.Value, nameof(frozenAuthorityFingerprints));
                authorityCopy.Add(pair.Key.Trim(), pair.Value.Trim());
            }

            State = state;
            TransferOperationStableId = transferOperationStableId;
            LifecycleGeneration = lifecycleGeneration;
            ExpectedCharacterRevision = expectedCharacterRevision;
            ExpectedCharacterFingerprint = expectedCharacterFingerprint.Trim();
            EndCommandFingerprint = endCommandFingerprint.Trim();
            AcceptedMissionResultStableId = acceptedMissionResultStableId;
            AcceptedMissionResultFingerprint =
                acceptedMissionResultFingerprint ?? string.Empty;
            BatchFingerprint = batchFingerprint ?? string.Empty;
            ApplicationPlanFingerprint =
                applicationPlanFingerprint ?? string.Empty;
            PersistedReceiptFingerprint =
                persistedReceiptFingerprint ?? string.Empty;
            GenerationRootSeed = generationRootSeed;
            GenerationAlgorithmVersion = generationAlgorithmVersion;
            EventModifierFingerprint = eventModifierFingerprint.Trim();
            ExpectedMoneySequence = expectedMoneySequence;
            ExpectedScrapSequence = expectedScrapSequence;
            ExpectedHoldingsSequence = expectedHoldingsSequence;
            this.rewards =
                new ReadOnlyCollection<CollectedRunRewardTransferItemV1>(
                    rewardCopy);
            this.equipment = new ReadOnlyCollection<EquipmentInstance>(
                equipmentCopy);
            this.strongboxes =
                new ReadOnlyCollection<StrongboxInstanceContextV1>(
                    strongboxCopy);
            this.frozenAuthorityFingerprints =
                new ReadOnlyDictionary<string, string>(authorityCopy);

            var builder = new StringBuilder(
                "schema=collected-run-reward-prepared-transfer-v1");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "state", (int)State);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "custody", CustodyStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "prepare-operation", PreparationOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "transfer-operation", TransferOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "run", RunStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "lifecycle", LifecycleGeneration);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character", SelectedCharacterStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-revision", ExpectedCharacterRevision);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "character-fingerprint", ExpectedCharacterFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "end-operation", EndOperationStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "end-command", EndCommandFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "mission-result-id", AcceptedMissionResultStableId);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "mission-result", AcceptedMissionResultFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "batch", BatchFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "plan", ApplicationPlanFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "receipt", PersistedReceiptFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "generation-seed", GenerationRootSeed);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "generation-algorithm", GenerationAlgorithmVersion);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "progression", ProgressionContext.Fingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "event-modifiers", EventModifierFingerprint);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "money-sequence", ExpectedMoneySequence);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "scrap-sequence", ExpectedScrapSequence);
            CollectedRunRewardTransferCanonicalV1.Append(builder, "holdings-sequence", ExpectedHoldingsSequence);
            foreach (KeyValuePair<string, string> pair in this.frozenAuthorityFingerprints)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "authority:" + pair.Key,
                    pair.Value);
            for (int index = 0; index < this.rewards.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "reward:" + index.ToString(CultureInfo.InvariantCulture),
                    this.rewards[index].Fingerprint);
            for (int index = 0; index < this.equipment.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "equipment:" + index.ToString(CultureInfo.InvariantCulture),
                    this.equipment[index].Fingerprint);
            for (int index = 0; index < this.strongboxes.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "strongbox:" + index.ToString(CultureInfo.InvariantCulture),
                    this.strongboxes[index].Fingerprint);
            canonicalText = builder.ToString();
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(canonicalText);
        }

        public CollectedRunRewardPreparedTransferStateV1 State { get; }
        public StableId CustodyStableId { get; }
        public StableId PreparationOperationStableId { get; }
        public StableId TransferOperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public StableId SelectedCharacterStableId { get; }
        public long ExpectedCharacterRevision { get; }
        public string ExpectedCharacterFingerprint { get; }
        public StableId EndOperationStableId { get; }
        public string EndCommandFingerprint { get; }
        public StableId AcceptedMissionResultStableId { get; }
        public string AcceptedMissionResultFingerprint { get; }
        public string BatchFingerprint { get; }
        public string ApplicationPlanFingerprint { get; }
        public string PersistedReceiptFingerprint { get; }
        public ulong GenerationRootSeed { get; }
        public int GenerationAlgorithmVersion { get; }
        public ProgressionContext ProgressionContext { get; }
        public string EventModifierFingerprint { get; }
        public long ExpectedMoneySequence { get; }
        public long ExpectedScrapSequence { get; }
        public long ExpectedHoldingsSequence { get; }
        public IReadOnlyDictionary<string, string> FrozenAuthorityFingerprints
        {
            get { return frozenAuthorityFingerprints; }
        }
        public IReadOnlyList<CollectedRunRewardTransferItemV1> Rewards
        {
            get { return rewards; }
        }
        public IReadOnlyList<EquipmentInstance> Equipment
        {
            get { return equipment; }
        }
        public IReadOnlyList<StrongboxInstanceContextV1> Strongboxes
        {
            get { return strongboxes; }
        }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }

        public static CollectedRunRewardPreparedTransferV1 AwaitingAcceptedEnd(
            StableId custodyStableId,
            StableId preparationOperationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            StableId endOperationStableId,
            string endCommandFingerprint,
            ulong generationRootSeed,
            int generationAlgorithmVersion,
            ProgressionContext progressionContext,
            string eventModifierFingerprint,
            long expectedMoneySequence,
            long expectedScrapSequence,
            long expectedHoldingsSequence,
            IDictionary<string, string> frozenAuthorityFingerprints,
            IEnumerable<CollectedRunRewardTransferItemV1> rewards,
            IEnumerable<EquipmentInstance> equipment,
            IEnumerable<StrongboxInstanceContextV1> strongboxes)
        {
            return new CollectedRunRewardPreparedTransferV1(
                CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd,
                custodyStableId,
                preparationOperationStableId,
                null,
                runStableId,
                lifecycleGeneration,
                selectedCharacterStableId,
                expectedCharacterRevision,
                expectedCharacterFingerprint,
                endOperationStableId,
                endCommandFingerprint,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                generationRootSeed,
                generationAlgorithmVersion,
                progressionContext,
                eventModifierFingerprint,
                expectedMoneySequence,
                expectedScrapSequence,
                expectedHoldingsSequence,
                frozenAuthorityFingerprints,
                rewards,
                equipment,
                strongboxes);
        }

        public CollectedRunRewardPreparedTransferV1 AcceptEnd(
            StableId transferOperationStableId,
            StableId acceptedMissionResultStableId,
            string acceptedMissionResultFingerprint,
            string batchFingerprint,
            string applicationPlanFingerprint)
        {
            if (State
                != CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
            {
                throw new InvalidOperationException(
                    "Only Awaiting-End custody can accept a mission result.");
            }
            return Copy(
                CollectedRunRewardPreparedTransferStateV1.Prepared,
                transferOperationStableId,
                acceptedMissionResultStableId,
                acceptedMissionResultFingerprint,
                batchFingerprint,
                applicationPlanFingerprint,
                string.Empty);
        }

        public CollectedRunRewardPreparedTransferV1 MarkPersisted(
            string receiptFingerprint)
        {
            if (State
                == CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
            {
                throw new InvalidOperationException(
                    "An unaccepted run cannot be marked persisted.");
            }
            return Copy(
                CollectedRunRewardPreparedTransferStateV1.Persisted,
                TransferOperationStableId,
                AcceptedMissionResultStableId,
                AcceptedMissionResultFingerprint,
                BatchFingerprint,
                ApplicationPlanFingerprint,
                receiptFingerprint);
        }

        private CollectedRunRewardPreparedTransferV1 Copy(
            CollectedRunRewardPreparedTransferStateV1 state,
            StableId transferOperationStableId,
            StableId missionResultStableId,
            string missionResultFingerprint,
            string batchFingerprint,
            string planFingerprint,
            string receiptFingerprint)
        {
            return new CollectedRunRewardPreparedTransferV1(
                state,
                CustodyStableId,
                PreparationOperationStableId,
                transferOperationStableId,
                RunStableId,
                LifecycleGeneration,
                SelectedCharacterStableId,
                ExpectedCharacterRevision,
                ExpectedCharacterFingerprint,
                EndOperationStableId,
                EndCommandFingerprint,
                missionResultStableId,
                missionResultFingerprint,
                batchFingerprint,
                planFingerprint,
                receiptFingerprint,
                GenerationRootSeed,
                GenerationAlgorithmVersion,
                ProgressionContext,
                EventModifierFingerprint,
                ExpectedMoneySequence,
                ExpectedScrapSequence,
                ExpectedHoldingsSequence,
                new Dictionary<string, string>(frozenAuthorityFingerprints),
                rewards,
                equipment,
                strongboxes);
        }

        private static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "A non-empty deterministic value is required.",
                    parameterName);
        }
    }

    public sealed class CollectedRunRewardPreparedTransferSnapshotV1
    {
        private readonly ReadOnlyCollection<CollectedRunRewardPreparedTransferV1> records;
        private readonly Dictionary<StableId, CollectedRunRewardPreparedTransferV1> byCustody;
        private readonly Dictionary<StableId, CollectedRunRewardPreparedTransferV1> byTransfer;

        public CollectedRunRewardPreparedTransferSnapshotV1(
            long revision,
            IEnumerable<CollectedRunRewardPreparedTransferV1> records)
        {
            if (revision < 0L) throw new ArgumentOutOfRangeException(nameof(revision));
            var copy = new List<CollectedRunRewardPreparedTransferV1>(
                records ?? throw new ArgumentNullException(nameof(records)));
            if (copy.Any(item => item == null))
                throw new ArgumentException(
                    "Prepared-transfer snapshots cannot contain null.",
                    nameof(records));
            copy.Sort((left, right) =>
                left.CustodyStableId.CompareTo(right.CustodyStableId));
            byCustody =
                new Dictionary<StableId, CollectedRunRewardPreparedTransferV1>();
            byTransfer =
                new Dictionary<StableId, CollectedRunRewardPreparedTransferV1>();
            for (int index = 0; index < copy.Count; index++)
            {
                CollectedRunRewardPreparedTransferV1 record = copy[index];
                if (byCustody.ContainsKey(record.CustodyStableId))
                    throw new ArgumentException(
                        "Prepared custody identities must be unique.",
                        nameof(records));
                byCustody.Add(record.CustodyStableId, record);
                if (record.TransferOperationStableId != null)
                {
                    if (byTransfer.ContainsKey(record.TransferOperationStableId))
                        throw new ArgumentException(
                            "Prepared transfer operations must be unique.",
                            nameof(records));
                    byTransfer.Add(record.TransferOperationStableId, record);
                }
            }
            Revision = revision;
            this.records =
                new ReadOnlyCollection<CollectedRunRewardPreparedTransferV1>(
                    copy);
            var builder = new StringBuilder(
                "schema=collected-run-reward-prepared-transfer-snapshot-v1");
            CollectedRunRewardTransferCanonicalV1.Append(
                builder,
                "revision",
                Revision);
            for (int index = 0; index < copy.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(
                    builder,
                    "record:" + index.ToString(CultureInfo.InvariantCulture),
                    copy[index].Fingerprint);
            Fingerprint =
                CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }

        public long Revision { get; }
        public IReadOnlyList<CollectedRunRewardPreparedTransferV1> Records
        {
            get { return records; }
        }
        public string Fingerprint { get; }
        public bool TryGetByCustody(
            StableId id,
            out CollectedRunRewardPreparedTransferV1 value)
        {
            value = null;
            return id != null && byCustody.TryGetValue(id, out value);
        }
        public bool TryGetByTransfer(
            StableId id,
            out CollectedRunRewardPreparedTransferV1 value)
        {
            value = null;
            return id != null && byTransfer.TryGetValue(id, out value);
        }
        public static CollectedRunRewardPreparedTransferSnapshotV1 Empty()
        {
            return new CollectedRunRewardPreparedTransferSnapshotV1(
                0L,
                Array.Empty<CollectedRunRewardPreparedTransferV1>());
        }
    }

    public sealed class CollectedRunRewardPreparedTransferAuthorityV1
    {
        private CollectedRunRewardPreparedTransferSnapshotV1 snapshot;

        public CollectedRunRewardPreparedTransferAuthorityV1(
            CollectedRunRewardPreparedTransferSnapshotV1 initial = null)
        {
            snapshot = initial
                ?? CollectedRunRewardPreparedTransferSnapshotV1.Empty();
        }

        public CollectedRunRewardPreparedTransferSnapshotV1 ExportSnapshot()
        {
            return snapshot;
        }
        public bool TryGetByCustody(
            StableId id,
            out CollectedRunRewardPreparedTransferV1 value)
        {
            return snapshot.TryGetByCustody(id, out value);
        }
        public bool TryGetByTransfer(
            StableId id,
            out CollectedRunRewardPreparedTransferV1 value)
        {
            return snapshot.TryGetByTransfer(id, out value);
        }
        public IReadOnlyList<CollectedRunRewardPreparedTransferV1>
            ExportRecoverable(StableId selectedCharacterStableId)
        {
            return snapshot.Records
                .Where(item =>
                    item.SelectedCharacterStableId
                        == selectedCharacterStableId
                    && item.State
                        == CollectedRunRewardPreparedTransferStateV1.Prepared)
                .OrderBy(item => item.CustodyStableId)
                .ToArray();
        }

        public CollectedRunRewardTransferAuthorityStatusV1 Upsert(
            CollectedRunRewardPreparedTransferV1 incoming,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (incoming == null)
            {
                diagnostic = "collected-run-prepared-transfer-null";
                return CollectedRunRewardTransferAuthorityStatusV1.Rejected;
            }
            CollectedRunRewardPreparedTransferV1 existing;
            if (snapshot.TryGetByCustody(incoming.CustodyStableId, out existing))
            {
                if (string.Equals(
                    existing.Fingerprint,
                    incoming.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return CollectedRunRewardTransferAuthorityStatusV1.ExactReplay;
                }
                if ((int)incoming.State < (int)existing.State
                    || incoming.RunStableId != existing.RunStableId
                    || incoming.LifecycleGeneration
                        != existing.LifecycleGeneration
                    || incoming.SelectedCharacterStableId
                        != existing.SelectedCharacterStableId
                    || incoming.PreparationOperationStableId
                        != existing.PreparationOperationStableId
                    || incoming.EndOperationStableId
                        != existing.EndOperationStableId
                    || !string.Equals(
                        incoming.EndCommandFingerprint,
                        existing.EndCommandFingerprint,
                        StringComparison.Ordinal))
                {
                    diagnostic = "collected-run-prepared-transfer-conflict";
                    return CollectedRunRewardTransferAuthorityStatusV1
                        .ConflictingDuplicate;
                }
            }
            var next = new List<CollectedRunRewardPreparedTransferV1>(
                snapshot.Records);
            if (existing != null) next.Remove(existing);
            next.Add(incoming);
            snapshot = new CollectedRunRewardPreparedTransferSnapshotV1(
                checked(snapshot.Revision + 1L),
                next);
            return CollectedRunRewardTransferAuthorityStatusV1.Applied;
        }

        public SaveComponentApplyResultV1 ImportSnapshot(
            CollectedRunRewardPreparedTransferSnapshotV1 imported)
        {
            if (imported == null)
                return SaveComponentApplyResultV1.Rejected(
                    "collected-run-prepared-transfer-snapshot-null");
            snapshot = imported;
            return SaveComponentApplyResultV1.Applied();
        }
    }

    public static class CollectedRunRewardPreparedTransferSaveComponentV1
    {
        public const int SchemaVersion = 1;
        public const string ContentVersion =
            "collected-run-reward-prepared-transfers-explicit-v1";
        public static readonly StableId ComponentStableId =
            StableId.Parse(
                "save-component.collected-run-reward-prepared-transfers");

        public static SaveComponentDefinitionV1 Definition()
        {
            return new SaveComponentDefinitionV1(
                ComponentStableId,
                SchemaVersion,
                ContentVersion,
                false,
                75);
        }

        public static ISaveComponentAdapterV1 CreateAdapter(
            CollectedRunRewardPreparedTransferAuthorityV1 authority)
        {
            if (authority == null)
                throw new ArgumentNullException(nameof(authority));
            return new AuthoritySnapshotSaveComponentAdapterV1<
                CollectedRunRewardPreparedTransferSnapshotV1>(
                    Definition(),
                    Codec.Instance,
                    authority.ExportSnapshot,
                    Codec.Instance.Validate,
                    authority.ImportSnapshot);
        }

        /// <summary>
        /// Explicit deterministic binary contract encoded as Base64. No reflection, CLR
        /// names, locale-sensitive numbers, or canonical-text reparsing is used.
        /// </summary>
        public sealed class Codec :
            ISaveComponentPayloadCodecV1<
                CollectedRunRewardPreparedTransferSnapshotV1>
        {
            public static readonly Codec Instance = new Codec();
            public string ContractId { get { return ContentVersion; } }

            public SaveComponentValidationResultV1 Validate(
                CollectedRunRewardPreparedTransferSnapshotV1 snapshot)
            {
                if (snapshot == null)
                    return SaveComponentValidationResultV1.Reject(
                        "collected-run-prepared-transfer-snapshot-null");
                try
                {
                    var rebuilt =
                        new CollectedRunRewardPreparedTransferSnapshotV1(
                            snapshot.Revision,
                            snapshot.Records);
                    return string.Equals(
                            rebuilt.Fingerprint,
                            snapshot.Fingerprint,
                            StringComparison.Ordinal)
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject(
                            "collected-run-prepared-transfer-snapshot-fingerprint-invalid");
                }
                catch (Exception exception)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "collected-run-prepared-transfer-snapshot-invalid:"
                        + exception.GetType().Name);
                }
            }

            public string Encode(
                CollectedRunRewardPreparedTransferSnapshotV1 snapshot)
            {
                SaveComponentValidationResultV1 validation = Validate(snapshot);
                if (!validation.Succeeded)
                    throw new InvalidOperationException(validation.RejectionCode);
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    writer.Write(ContentVersion);
                    writer.Write(snapshot.Revision);
                    writer.Write(snapshot.Records.Count);
                    for (int index = 0; index < snapshot.Records.Count; index++)
                        WriteRecord(writer, snapshot.Records[index]);
                    writer.Flush();
                    return Convert.ToBase64String(stream.ToArray());
                }
            }

            public bool TryDecode(
                string canonicalPayload,
                out CollectedRunRewardPreparedTransferSnapshotV1 snapshot,
                out string rejectionCode)
            {
                snapshot = null;
                rejectionCode = string.Empty;
                try
                {
                    byte[] bytes = Convert.FromBase64String(
                        canonicalPayload
                        ?? throw new ArgumentNullException(
                            nameof(canonicalPayload)));
                    using (var stream = new MemoryStream(bytes, false))
                    using (var reader = new BinaryReader(
                        stream,
                        Encoding.UTF8,
                        true))
                    {
                        if (!string.Equals(
                            reader.ReadString(),
                            ContentVersion,
                            StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("version");
                        }
                        long revision = reader.ReadInt64();
                        int count = ReadCount(reader);
                        var records =
                            new List<CollectedRunRewardPreparedTransferV1>(
                                count);
                        for (int index = 0; index < count; index++)
                            records.Add(ReadRecord(reader));
                        if (stream.Position != stream.Length)
                            throw new InvalidDataException("trailing-data");
                        snapshot =
                            new CollectedRunRewardPreparedTransferSnapshotV1(
                                revision,
                                records);
                    }
                    SaveComponentValidationResultV1 validation = Validate(snapshot);
                    if (!validation.Succeeded)
                    {
                        rejectionCode = validation.RejectionCode;
                        snapshot = null;
                        return false;
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    snapshot = null;
                    rejectionCode =
                        "collected-run-prepared-transfer-payload-invalid:"
                        + exception.GetType().Name;
                    return false;
                }
            }

            private static void WriteRecord(
                BinaryWriter writer,
                CollectedRunRewardPreparedTransferV1 record)
            {
                writer.Write((int)record.State);
                WriteId(writer, record.CustodyStableId);
                WriteId(writer, record.PreparationOperationStableId);
                WriteOptionalId(writer, record.TransferOperationStableId);
                WriteId(writer, record.RunStableId);
                writer.Write(record.LifecycleGeneration);
                WriteId(writer, record.SelectedCharacterStableId);
                writer.Write(record.ExpectedCharacterRevision);
                writer.Write(record.ExpectedCharacterFingerprint);
                WriteId(writer, record.EndOperationStableId);
                writer.Write(record.EndCommandFingerprint);
                WriteOptionalId(writer, record.AcceptedMissionResultStableId);
                writer.Write(record.AcceptedMissionResultFingerprint);
                writer.Write(record.BatchFingerprint);
                writer.Write(record.ApplicationPlanFingerprint);
                writer.Write(record.PersistedReceiptFingerprint);
                writer.Write(record.GenerationRootSeed);
                writer.Write(record.GenerationAlgorithmVersion);
                WriteProgression(writer, record.ProgressionContext);
                writer.Write(record.EventModifierFingerprint);
                writer.Write(record.ExpectedMoneySequence);
                writer.Write(record.ExpectedScrapSequence);
                writer.Write(record.ExpectedHoldingsSequence);
                writer.Write(record.FrozenAuthorityFingerprints.Count);
                foreach (KeyValuePair<string, string> pair in
                    record.FrozenAuthorityFingerprints)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
                writer.Write(record.Rewards.Count);
                for (int index = 0; index < record.Rewards.Count; index++)
                    WriteReward(writer, record.Rewards[index]);
                writer.Write(record.Equipment.Count);
                for (int index = 0; index < record.Equipment.Count; index++)
                    WriteEquipment(writer, record.Equipment[index]);
                writer.Write(record.Strongboxes.Count);
                for (int index = 0; index < record.Strongboxes.Count; index++)
                    WriteStrongbox(writer, record.Strongboxes[index]);
            }

            private static CollectedRunRewardPreparedTransferV1 ReadRecord(
                BinaryReader reader)
            {
                var state = (CollectedRunRewardPreparedTransferStateV1)
                    reader.ReadInt32();
                StableId custody = ReadId(reader);
                StableId preparationOperation = ReadId(reader);
                StableId transferOperation = ReadOptionalId(reader);
                StableId run = ReadId(reader);
                long lifecycle = reader.ReadInt64();
                StableId character = ReadId(reader);
                long characterRevision = reader.ReadInt64();
                string characterFingerprint = reader.ReadString();
                StableId endOperation = ReadId(reader);
                string endFingerprint = reader.ReadString();
                StableId missionResult = ReadOptionalId(reader);
                string missionFingerprint = reader.ReadString();
                string batchFingerprint = reader.ReadString();
                string planFingerprint = reader.ReadString();
                string receiptFingerprint = reader.ReadString();
                ulong seed = reader.ReadUInt64();
                int algorithm = reader.ReadInt32();
                ProgressionContext progression = ReadProgression(reader);
                string eventFingerprint = reader.ReadString();
                long moneySequence = reader.ReadInt64();
                long scrapSequence = reader.ReadInt64();
                long holdingsSequence = reader.ReadInt64();
                int authorityCount = ReadCount(reader);
                var authorities = new Dictionary<string, string>(
                    StringComparer.Ordinal);
                for (int index = 0; index < authorityCount; index++)
                    authorities.Add(reader.ReadString(), reader.ReadString());
                int rewardCount = ReadCount(reader);
                var rewards = new List<CollectedRunRewardTransferItemV1>(
                    rewardCount);
                for (int index = 0; index < rewardCount; index++)
                    rewards.Add(ReadReward(reader));
                int equipmentCount = ReadCount(reader);
                var equipment = new List<EquipmentInstance>(equipmentCount);
                for (int index = 0; index < equipmentCount; index++)
                    equipment.Add(ReadEquipment(reader));
                int strongboxCount = ReadCount(reader);
                var strongboxes =
                    new List<StrongboxInstanceContextV1>(strongboxCount);
                for (int index = 0; index < strongboxCount; index++)
                    strongboxes.Add(ReadStrongbox(reader));

                CollectedRunRewardPreparedTransferV1 record =
                    CollectedRunRewardPreparedTransferV1.AwaitingAcceptedEnd(
                        custody,
                        preparationOperation,
                        run,
                        lifecycle,
                        character,
                        characterRevision,
                        characterFingerprint,
                        endOperation,
                        endFingerprint,
                        seed,
                        algorithm,
                        progression,
                        eventFingerprint,
                        moneySequence,
                        scrapSequence,
                        holdingsSequence,
                        authorities,
                        rewards,
                        equipment,
                        strongboxes);
                if (state
                    == CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
                    return record;
                record = record.AcceptEnd(
                    transferOperation,
                    missionResult,
                    missionFingerprint,
                    batchFingerprint,
                    planFingerprint);
                if (state
                    == CollectedRunRewardPreparedTransferStateV1.Persisted)
                    record = record.MarkPersisted(receiptFingerprint);
                return record;
            }

            private static void WriteReward(
                BinaryWriter writer,
                CollectedRunRewardTransferItemV1 value)
            {
                WriteId(writer, value.RewardInstanceStableId);
                writer.Write((int)value.RewardKind);
                WriteId(writer, value.ContentStableId);
                writer.Write(value.Quantity);
                WriteId(writer, value.PickupStableId);
                WriteId(writer, value.SourceGrantStableId);
                WriteId(writer, value.DropOperationStableId);
                WriteId(writer, value.TerminalEventStableId);
                WriteOptionalId(writer, value.TriggeringEventStableId);
                WriteId(writer, value.RunStableId);
                writer.Write(value.RunLifecycleGeneration);
                WriteId(writer, value.SourceEntityStableId);
                WriteOptionalId(writer, value.SourcePlacementStableId);
                writer.Write(value.SourceLifecycleGeneration);
                WriteId(writer, value.SourceDefinitionStableId);
                WriteId(writer, value.AttributedParticipantStableId);
                writer.Write(value.GeneratedBatchFingerprint);
                writer.Write(value.GeneratedRewardFingerprint);
                WriteId(writer, value.RoomStableId);
                writer.Write(value.WorldPositionX);
                writer.Write(value.WorldPositionY);
                writer.Write(value.WorldSpawnFingerprint);
                writer.Write(value.AvailablePickupFingerprint);
                WriteId(writer, value.CollectorEntityStableId);
                WriteId(writer, value.CollectorParticipantStableId);
                WriteId(writer, value.CollectionOperationStableId);
                writer.Write(value.CollectionOrder);
                writer.Write(value.CollectedAtAuthoritativeTick);
                writer.Write(value.CollectedRewardFingerprint);
            }

            private static CollectedRunRewardTransferItemV1 ReadReward(
                BinaryReader reader)
            {
                return new CollectedRunRewardTransferItemV1(
                    ReadId(reader),
                    (RewardGrantKindV1)reader.ReadInt32(),
                    ReadId(reader),
                    reader.ReadInt64(),
                    ReadId(reader),
                    ReadId(reader),
                    ReadId(reader),
                    ReadId(reader),
                    ReadOptionalId(reader),
                    ReadId(reader),
                    reader.ReadInt64(),
                    ReadId(reader),
                    ReadOptionalId(reader),
                    reader.ReadInt64(),
                    ReadId(reader),
                    ReadId(reader),
                    reader.ReadString(),
                    reader.ReadString(),
                    ReadId(reader),
                    reader.ReadDouble(),
                    reader.ReadDouble(),
                    reader.ReadString(),
                    reader.ReadString(),
                    ReadId(reader),
                    ReadId(reader),
                    ReadId(reader),
                    reader.ReadInt64(),
                    reader.ReadInt64(),
                    reader.ReadString());
            }

            private static void WriteEquipment(
                BinaryWriter writer,
                EquipmentInstance value)
            {
                WriteId(writer, value.InstanceId);
                WriteId(writer, value.DefinitionId);
                writer.Write(value.ItemLevel);
                WriteId(writer, value.QualityId);
                writer.Write(value.Augments.Count);
                for (int index = 0; index < value.Augments.Count; index++)
                {
                    AugmentInstance augment = value.Augments[index];
                    WriteId(writer, augment.InstanceId);
                    WriteId(writer, augment.DefinitionId);
                    writer.Write(augment.Tier);
                    writer.Write(augment.Level);
                }
            }

            private static EquipmentInstance ReadEquipment(BinaryReader reader)
            {
                StableId instance = ReadId(reader);
                StableId definition = ReadId(reader);
                int level = reader.ReadInt32();
                StableId quality = ReadId(reader);
                int count = ReadCount(reader);
                var augments = new List<AugmentInstance>(count);
                for (int index = 0; index < count; index++)
                {
                    augments.Add(AugmentInstance.Create(
                        ReadId(reader),
                        ReadId(reader),
                        reader.ReadInt32(),
                        reader.ReadInt32()));
                }
                return EquipmentInstance.Create(
                    instance,
                    definition,
                    level,
                    quality,
                    augments);
            }

            private static void WriteStrongbox(
                BinaryWriter writer,
                StrongboxInstanceContextV1 value)
            {
                WriteId(writer, value.InstanceStableId);
                WriteId(writer, value.TierStableId);
                writer.Write(value.RootSeed);
                writer.Write(value.AlgorithmVersion);
                WriteProgression(writer, value.ProgressionContext);
                WriteId(writer, value.SourceContextStableId);
                WriteId(writer, value.CollectionProvenanceStableId);
                WriteOptionalText(writer, value.AlgorithmContentFingerprint);
            }

            private static StrongboxInstanceContextV1 ReadStrongbox(
                BinaryReader reader)
            {
                return StrongboxInstanceContextV1.Create(
                    ReadId(reader),
                    ReadId(reader),
                    reader.ReadUInt64(),
                    reader.ReadInt32(),
                    ReadProgression(reader),
                    ReadId(reader),
                    ReadId(reader),
                    ReadOptionalText(reader));
            }

            private static void WriteProgression(
                BinaryWriter writer,
                ProgressionContext value)
            {
                writer.Write(value.CharacterLevel);
                writer.Write(value.RegionLevel);
                WriteId(writer, value.DifficultyId);
                writer.Write(value.DifficultyValue);
                writer.Write(value.ProgressionTags.Count);
                for (int index = 0; index < value.ProgressionTags.Count; index++)
                    WriteId(writer, value.ProgressionTags[index]);
            }

            private static ProgressionContext ReadProgression(
                BinaryReader reader)
            {
                int characterLevel = reader.ReadInt32();
                int regionLevel = reader.ReadInt32();
                StableId difficulty = ReadId(reader);
                int difficultyValue = reader.ReadInt32();
                int count = ReadCount(reader);
                var tags = new List<StableId>(count);
                for (int index = 0; index < count; index++)
                    tags.Add(ReadId(reader));
                return ProgressionContext.Create(
                    characterLevel,
                    regionLevel,
                    difficulty,
                    difficultyValue,
                    tags);
            }

            private static void WriteId(BinaryWriter writer, StableId value)
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                writer.Write(value.ToString());
            }
            private static StableId ReadId(BinaryReader reader)
            {
                return StableId.Parse(reader.ReadString());
            }
            private static void WriteOptionalId(
                BinaryWriter writer,
                StableId value)
            {
                writer.Write(value != null);
                if (value != null) WriteId(writer, value);
            }
            private static StableId ReadOptionalId(BinaryReader reader)
            {
                return reader.ReadBoolean() ? ReadId(reader) : null;
            }
            private static void WriteOptionalText(
                BinaryWriter writer,
                string value)
            {
                writer.Write(value != null);
                if (value != null) writer.Write(value);
            }
            private static string ReadOptionalText(BinaryReader reader)
            {
                return reader.ReadBoolean() ? reader.ReadString() : null;
            }
            private static int ReadCount(BinaryReader reader)
            {
                int value = reader.ReadInt32();
                if (value < 0
                    || value > SavePersistenceLimitsV1.MaximumCollectionCount)
                {
                    throw new InvalidDataException("collection-count-invalid");
                }
                return value;
            }
        }
    }
}

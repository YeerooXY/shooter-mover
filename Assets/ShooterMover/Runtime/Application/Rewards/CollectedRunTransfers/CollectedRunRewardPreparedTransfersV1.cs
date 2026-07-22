using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
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
    /// Durable crash-recovery custody for one exact collected-run transfer. This owns no
    /// wallet, holdings, equipment or strongbox state. It retains the exact immutable
    /// payload until the corresponding durable transfer receipt is confirmed.
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
            if (string.IsNullOrWhiteSpace(expectedCharacterFingerprint))
                throw new ArgumentException("The run-frozen character fingerprint is required.", nameof(expectedCharacterFingerprint));
            EndOperationStableId = endOperationStableId
                ?? throw new ArgumentNullException(nameof(endOperationStableId));
            if (string.IsNullOrWhiteSpace(endCommandFingerprint))
                throw new ArgumentException("The exact End command fingerprint is required.", nameof(endCommandFingerprint));
            if (generationAlgorithmVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(generationAlgorithmVersion));
            ProgressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (string.IsNullOrWhiteSpace(eventModifierFingerprint))
                throw new ArgumentException("The frozen event/modifier fingerprint is required.", nameof(eventModifierFingerprint));
            if (expectedMoneySequence < 0L || expectedScrapSequence < 0L || expectedHoldingsSequence < 0L)
                throw new ArgumentOutOfRangeException(nameof(expectedMoneySequence));

            bool accepted = state != CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd;
            if (accepted)
            {
                if (transferOperationStableId == null
                    || acceptedMissionResultStableId == null
                    || string.IsNullOrWhiteSpace(acceptedMissionResultFingerprint)
                    || string.IsNullOrWhiteSpace(batchFingerprint)
                    || string.IsNullOrWhiteSpace(applicationPlanFingerprint))
                {
                    throw new ArgumentException("Prepared transfers require exact accepted result and plan identity.");
                }
            }
            else if (transferOperationStableId != null
                || acceptedMissionResultStableId != null
                || !string.IsNullOrEmpty(acceptedMissionResultFingerprint)
                || !string.IsNullOrEmpty(batchFingerprint)
                || !string.IsNullOrEmpty(applicationPlanFingerprint)
                || !string.IsNullOrEmpty(persistedReceiptFingerprint))
            {
                throw new ArgumentException("Awaiting-End custody cannot claim accepted transfer facts.");
            }
            if (state == CollectedRunRewardPreparedTransferStateV1.Persisted
                && string.IsNullOrWhiteSpace(persistedReceiptFingerprint))
            {
                throw new ArgumentException("Persisted custody requires the exact durable receipt fingerprint.", nameof(persistedReceiptFingerprint));
            }
            if (state != CollectedRunRewardPreparedTransferStateV1.Persisted
                && !string.IsNullOrEmpty(persistedReceiptFingerprint))
            {
                throw new ArgumentException("Only persisted custody may carry a receipt fingerprint.", nameof(persistedReceiptFingerprint));
            }

            var rewardCopy = new List<CollectedRunRewardTransferItemV1>(
                rewards ?? throw new ArgumentNullException(nameof(rewards)));
            if (rewardCopy.Any(item => item == null))
                throw new ArgumentException("Prepared rewards cannot contain null.", nameof(rewards));
            rewardCopy.Sort((left, right) => left.RewardInstanceStableId.CompareTo(right.RewardInstanceStableId));
            var rewardIds = new HashSet<StableId>();
            for (int index = 0; index < rewardCopy.Count; index++)
            {
                CollectedRunRewardTransferItemV1 reward = rewardCopy[index];
                if (reward.RunStableId != RunStableId
                    || reward.RunLifecycleGeneration != lifecycleGeneration
                    || !rewardIds.Add(reward.RewardInstanceStableId))
                {
                    throw new ArgumentException("Prepared rewards must be unique and belong to the exact run lifecycle.", nameof(rewards));
                }
            }

            var equipmentCopy = new List<EquipmentInstance>(equipment ?? Array.Empty<EquipmentInstance>());
            if (equipmentCopy.Any(item => item == null))
                throw new ArgumentException("Prepared equipment cannot contain null.", nameof(equipment));
            equipmentCopy.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            var equipmentIds = new HashSet<StableId>();
            for (int index = 0; index < equipmentCopy.Count; index++)
            {
                if (!equipmentIds.Add(equipmentCopy[index].InstanceId))
                    throw new ArgumentException("Prepared equipment identities must be unique.", nameof(equipment));
            }

            var strongboxCopy = new List<StrongboxInstanceContextV1>(strongboxes ?? Array.Empty<StrongboxInstanceContextV1>());
            if (strongboxCopy.Any(item => item == null))
                throw new ArgumentException("Prepared strongbox contexts cannot contain null.", nameof(strongboxes));
            strongboxCopy.Sort();
            var strongboxIds = new HashSet<StableId>();
            for (int index = 0; index < strongboxCopy.Count; index++)
            {
                if (!strongboxIds.Add(strongboxCopy[index].InstanceStableId))
                    throw new ArgumentException("Prepared strongbox identities must be unique.", nameof(strongboxes));
            }

            foreach (CollectedRunRewardTransferItemV1 reward in rewardCopy)
            {
                if (reward.RewardKind == RewardGrantKindV1.EquipmentReference
                    && !equipmentIds.Contains(reward.RewardInstanceStableId))
                {
                    throw new ArgumentException("Every equipment reward requires its exact retained instance.", nameof(equipment));
                }
                if (reward.RewardKind == RewardGrantKindV1.Strongbox
                    && !strongboxIds.Contains(reward.RewardInstanceStableId))
                {
                    throw new ArgumentException("Every strongbox reward requires its exact unopened context.", nameof(strongboxes));
                }
            }

            var authorityCopy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in frozenAuthorityFingerprints
                ?? throw new ArgumentNullException(nameof(frozenAuthorityFingerprints)))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("Frozen authority keys and fingerprints are required.", nameof(frozenAuthorityFingerprints));
                authorityCopy.Add(pair.Key.Trim(), pair.Value.Trim());
            }

            State = state;
            TransferOperationStableId = transferOperationStableId;
            LifecycleGeneration = lifecycleGeneration;
            ExpectedCharacterRevision = expectedCharacterRevision;
            ExpectedCharacterFingerprint = expectedCharacterFingerprint.Trim();
            EndCommandFingerprint = endCommandFingerprint.Trim();
            AcceptedMissionResultStableId = acceptedMissionResultStableId;
            AcceptedMissionResultFingerprint = acceptedMissionResultFingerprint ?? string.Empty;
            BatchFingerprint = batchFingerprint ?? string.Empty;
            ApplicationPlanFingerprint = applicationPlanFingerprint ?? string.Empty;
            PersistedReceiptFingerprint = persistedReceiptFingerprint ?? string.Empty;
            GenerationRootSeed = generationRootSeed;
            GenerationAlgorithmVersion = generationAlgorithmVersion;
            EventModifierFingerprint = eventModifierFingerprint.Trim();
            ExpectedMoneySequence = expectedMoneySequence;
            ExpectedScrapSequence = expectedScrapSequence;
            ExpectedHoldingsSequence = expectedHoldingsSequence;
            this.rewards = new ReadOnlyCollection<CollectedRunRewardTransferItemV1>(rewardCopy);
            this.equipment = new ReadOnlyCollection<EquipmentInstance>(equipmentCopy);
            this.strongboxes = new ReadOnlyCollection<StrongboxInstanceContextV1>(strongboxCopy);
            this.frozenAuthorityFingerprints = new ReadOnlyDictionary<string, string>(authorityCopy);

            var builder = new StringBuilder("schema=collected-run-reward-prepared-transfer-v1");
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
                CollectedRunRewardTransferCanonicalV1.Append(builder, "authority:" + pair.Key, pair.Value);
            for (int index = 0; index < this.rewards.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(builder, "reward:" + index.ToString(CultureInfo.InvariantCulture), this.rewards[index].Fingerprint);
            for (int index = 0; index < this.equipment.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(builder, "equipment:" + index.ToString(CultureInfo.InvariantCulture), this.equipment[index].Fingerprint);
            for (int index = 0; index < this.strongboxes.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(builder, "strongbox:" + index.ToString(CultureInfo.InvariantCulture), this.strongboxes[index].Fingerprint);
            canonicalText = builder.ToString();
            Fingerprint = CollectedRunRewardTransferCanonicalV1.Hash(canonicalText);
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
        public IReadOnlyDictionary<string, string> FrozenAuthorityFingerprints { get { return frozenAuthorityFingerprints; } }
        public IReadOnlyList<CollectedRunRewardTransferItemV1> Rewards { get { return rewards; } }
        public IReadOnlyList<EquipmentInstance> Equipment { get { return equipment; } }
        public IReadOnlyList<StrongboxInstanceContextV1> Strongboxes { get { return strongboxes; } }
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
                custodyStableId, preparationOperationStableId, null, runStableId,
                lifecycleGeneration, selectedCharacterStableId, expectedCharacterRevision,
                expectedCharacterFingerprint, endOperationStableId, endCommandFingerprint,
                null, string.Empty, string.Empty, string.Empty, string.Empty,
                generationRootSeed, generationAlgorithmVersion, progressionContext,
                eventModifierFingerprint, expectedMoneySequence, expectedScrapSequence,
                expectedHoldingsSequence, frozenAuthorityFingerprints, rewards, equipment,
                strongboxes);
        }

        public CollectedRunRewardPreparedTransferV1 AcceptEnd(
            StableId transferOperationStableId,
            StableId acceptedMissionResultStableId,
            string acceptedMissionResultFingerprint,
            string batchFingerprint,
            string applicationPlanFingerprint)
        {
            if (State != CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
                throw new InvalidOperationException("Only Awaiting-End custody can accept a mission result.");
            return Copy(
                CollectedRunRewardPreparedTransferStateV1.Prepared,
                transferOperationStableId,
                acceptedMissionResultStableId,
                acceptedMissionResultFingerprint,
                batchFingerprint,
                applicationPlanFingerprint,
                string.Empty);
        }

        public CollectedRunRewardPreparedTransferV1 MarkPersisted(string receiptFingerprint)
        {
            if (State == CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
                throw new InvalidOperationException("An unaccepted run cannot be marked persisted.");
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
                state, CustodyStableId, PreparationOperationStableId,
                transferOperationStableId, RunStableId, LifecycleGeneration,
                SelectedCharacterStableId, ExpectedCharacterRevision,
                ExpectedCharacterFingerprint, EndOperationStableId,
                EndCommandFingerprint, missionResultStableId,
                missionResultFingerprint, batchFingerprint, planFingerprint,
                receiptFingerprint, GenerationRootSeed, GenerationAlgorithmVersion,
                ProgressionContext, EventModifierFingerprint, ExpectedMoneySequence,
                ExpectedScrapSequence, ExpectedHoldingsSequence,
                new Dictionary<string, string>(frozenAuthorityFingerprints), rewards,
                equipment, strongboxes);
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
                throw new ArgumentException("Prepared-transfer snapshots cannot contain null.", nameof(records));
            copy.Sort((left, right) => left.CustodyStableId.CompareTo(right.CustodyStableId));
            byCustody = new Dictionary<StableId, CollectedRunRewardPreparedTransferV1>();
            byTransfer = new Dictionary<StableId, CollectedRunRewardPreparedTransferV1>();
            for (int index = 0; index < copy.Count; index++)
            {
                CollectedRunRewardPreparedTransferV1 record = copy[index];
                if (byCustody.ContainsKey(record.CustodyStableId))
                    throw new ArgumentException("Prepared custody identities must be unique.", nameof(records));
                byCustody.Add(record.CustodyStableId, record);
                if (record.TransferOperationStableId != null)
                {
                    if (byTransfer.ContainsKey(record.TransferOperationStableId))
                        throw new ArgumentException("Prepared transfer operation identities must be unique.", nameof(records));
                    byTransfer.Add(record.TransferOperationStableId, record);
                }
            }
            Revision = revision;
            this.records = new ReadOnlyCollection<CollectedRunRewardPreparedTransferV1>(copy);
            var builder = new StringBuilder("schema=collected-run-reward-prepared-transfer-snapshot-v1");
            CollectedRunRewardTransferCanonicalV1.Append(builder, "revision", Revision);
            for (int index = 0; index < copy.Count; index++)
                CollectedRunRewardTransferCanonicalV1.Append(builder, "record:" + index.ToString(CultureInfo.InvariantCulture), copy[index].Fingerprint);
            Fingerprint = CollectedRunRewardTransferCanonicalV1.Hash(builder.ToString());
        }

        public long Revision { get; }
        public IReadOnlyList<CollectedRunRewardPreparedTransferV1> Records { get { return records; } }
        public string Fingerprint { get; }
        public bool TryGetByCustody(StableId id, out CollectedRunRewardPreparedTransferV1 value)
        {
            value = null;
            return id != null && byCustody.TryGetValue(id, out value);
        }
        public bool TryGetByTransfer(StableId id, out CollectedRunRewardPreparedTransferV1 value)
        {
            value = null;
            return id != null && byTransfer.TryGetValue(id, out value);
        }
        public static CollectedRunRewardPreparedTransferSnapshotV1 Empty()
        {
            return new CollectedRunRewardPreparedTransferSnapshotV1(
                0L, Array.Empty<CollectedRunRewardPreparedTransferV1>());
        }
    }

    public sealed class CollectedRunRewardPreparedTransferAuthorityV1
    {
        private CollectedRunRewardPreparedTransferSnapshotV1 snapshot;

        public CollectedRunRewardPreparedTransferAuthorityV1(
            CollectedRunRewardPreparedTransferSnapshotV1 initial = null)
        {
            snapshot = initial ?? CollectedRunRewardPreparedTransferSnapshotV1.Empty();
        }

        public CollectedRunRewardPreparedTransferSnapshotV1 ExportSnapshot() { return snapshot; }
        public bool TryGetByCustody(StableId id, out CollectedRunRewardPreparedTransferV1 value)
        {
            return snapshot.TryGetByCustody(id, out value);
        }
        public bool TryGetByTransfer(StableId id, out CollectedRunRewardPreparedTransferV1 value)
        {
            return snapshot.TryGetByTransfer(id, out value);
        }
        public IReadOnlyList<CollectedRunRewardPreparedTransferV1> ExportRecoverable(
            StableId selectedCharacterStableId)
        {
            return snapshot.Records
                .Where(item => item.SelectedCharacterStableId == selectedCharacterStableId
                    && item.State != CollectedRunRewardPreparedTransferStateV1.Persisted)
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
                if (string.Equals(existing.Fingerprint, incoming.Fingerprint, StringComparison.Ordinal))
                    return CollectedRunRewardTransferAuthorityStatusV1.ExactReplay;
                if (incoming.State < existing.State
                    || incoming.RunStableId != existing.RunStableId
                    || incoming.LifecycleGeneration != existing.LifecycleGeneration
                    || incoming.SelectedCharacterStableId != existing.SelectedCharacterStableId
                    || incoming.PreparationOperationStableId != existing.PreparationOperationStableId
                    || incoming.EndOperationStableId != existing.EndOperationStableId
                    || !string.Equals(incoming.EndCommandFingerprint, existing.EndCommandFingerprint, StringComparison.Ordinal))
                {
                    diagnostic = "collected-run-prepared-transfer-conflict";
                    return CollectedRunRewardTransferAuthorityStatusV1.ConflictingDuplicate;
                }
            }
            var next = new List<CollectedRunRewardPreparedTransferV1>(snapshot.Records);
            if (existing != null) next.Remove(existing);
            next.Add(incoming);
            snapshot = new CollectedRunRewardPreparedTransferSnapshotV1(
                checked(snapshot.Revision + 1L), next);
            return CollectedRunRewardTransferAuthorityStatusV1.Applied;
        }

        public SaveComponentApplyResultV1 ImportSnapshot(
            CollectedRunRewardPreparedTransferSnapshotV1 imported)
        {
            if (imported == null)
                return SaveComponentApplyResultV1.Rejected("collected-run-prepared-transfer-snapshot-null");
            snapshot = imported;
            return SaveComponentApplyResultV1.Applied();
        }
    }

    public static class CollectedRunRewardPreparedTransferSaveComponentV1
    {
        public const int SchemaVersion = 1;
        public const string ContentVersion = "collected-run-reward-prepared-transfers-explicit-v1";
        public static readonly StableId ComponentStableId =
            StableId.Parse("save-component.collected-run-reward-prepared-transfers");

        public static SaveComponentDefinitionV1 Definition()
        {
            return new SaveComponentDefinitionV1(
                ComponentStableId, SchemaVersion, ContentVersion, false, 75);
        }

        public static ISaveComponentAdapterV1 CreateAdapter(
            CollectedRunRewardPreparedTransferAuthorityV1 authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            return new AuthoritySnapshotSaveComponentAdapterV1<CollectedRunRewardPreparedTransferSnapshotV1>(
                Definition(), Codec.Instance, authority.ExportSnapshot, Codec.Instance.Validate,
                authority.ImportSnapshot);
        }

        public sealed class Codec : ISaveComponentPayloadCodecV1<CollectedRunRewardPreparedTransferSnapshotV1>
        {
            public static readonly Codec Instance = new Codec();
            public string ContractId { get { return ContentVersion; } }

            public SaveComponentValidationResultV1 Validate(
                CollectedRunRewardPreparedTransferSnapshotV1 snapshot)
            {
                if (snapshot == null)
                    return SaveComponentValidationResultV1.Reject("collected-run-prepared-transfer-snapshot-null");
                try
                {
                    var rebuilt = new CollectedRunRewardPreparedTransferSnapshotV1(
                        snapshot.Revision, snapshot.Records);
                    return string.Equals(rebuilt.Fingerprint, snapshot.Fingerprint, StringComparison.Ordinal)
                        ? SaveComponentValidationResultV1.Accept()
                        : SaveComponentValidationResultV1.Reject("collected-run-prepared-transfer-snapshot-fingerprint-invalid");
                }
                catch (Exception exception)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "collected-run-prepared-transfer-snapshot-invalid:" + exception.GetType().Name);
                }
            }

            public string Encode(CollectedRunRewardPreparedTransferSnapshotV1 snapshot)
            {
                SaveComponentValidationResultV1 validation = Validate(snapshot);
                if (!validation.Succeeded) throw new InvalidOperationException(validation.RejectionCode);
                var writer = new LineWriter();
                writer.Write(ContentVersion);
                writer.Write(snapshot.Revision);
                writer.Write(snapshot.Records.Count);
                for (int index = 0; index < snapshot.Records.Count; index++)
                    WriteRecord(writer, snapshot.Records[index]);
                return writer.ToString();
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
                    var reader = new LineReader(canonicalPayload);
                    if (!string.Equals(reader.ReadString(), ContentVersion, StringComparison.Ordinal))
                        throw new FormatException("version");
                    long revision = reader.ReadInt64();
                    int count = reader.ReadInt32();
                    if (count < 0) throw new FormatException("count");
                    var records = new List<CollectedRunRewardPreparedTransferV1>(count);
                    for (int index = 0; index < count; index++) records.Add(ReadRecord(reader));
                    reader.RequireEnd();
                    snapshot = new CollectedRunRewardPreparedTransferSnapshotV1(revision, records);
                    SaveComponentValidationResultV1 validation = Validate(snapshot);
                    if (!validation.Succeeded)
                    {
                        snapshot = null;
                        rejectionCode = validation.RejectionCode;
                        return false;
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    snapshot = null;
                    rejectionCode = "collected-run-prepared-transfer-payload-invalid:" + exception.GetType().Name;
                    return false;
                }
            }

            private static void WriteRecord(LineWriter writer, CollectedRunRewardPreparedTransferV1 record)
            {
                writer.Write((int)record.State);
                writer.Write(record.CustodyStableId);
                writer.Write(record.PreparationOperationStableId);
                writer.Write(record.TransferOperationStableId);
                writer.Write(record.RunStableId);
                writer.Write(record.LifecycleGeneration);
                writer.Write(record.SelectedCharacterStableId);
                writer.Write(record.ExpectedCharacterRevision);
                writer.Write(record.ExpectedCharacterFingerprint);
                writer.Write(record.EndOperationStableId);
                writer.Write(record.EndCommandFingerprint);
                writer.Write(record.AcceptedMissionResultStableId);
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
                foreach (KeyValuePair<string, string> pair in record.FrozenAuthorityFingerprints)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value);
                }
                writer.Write(record.Rewards.Count);
                for (int index = 0; index < record.Rewards.Count; index++)
                    writer.Write(record.Rewards[index].ToCanonicalString());
                writer.Write(record.Equipment.Count);
                for (int index = 0; index < record.Equipment.Count; index++)
                    WriteEquipment(writer, record.Equipment[index]);
                writer.Write(record.Strongboxes.Count);
                for (int index = 0; index < record.Strongboxes.Count; index++)
                    WriteStrongbox(writer, record.Strongboxes[index]);
            }

            private static CollectedRunRewardPreparedTransferV1 ReadRecord(LineReader reader)
            {
                var state = (CollectedRunRewardPreparedTransferStateV1)reader.ReadInt32();
                StableId custody = reader.ReadId();
                StableId prepareOperation = reader.ReadId();
                StableId transferOperation = reader.ReadOptionalId();
                StableId run = reader.ReadId();
                long lifecycle = reader.ReadInt64();
                StableId character = reader.ReadId();
                long characterRevision = reader.ReadInt64();
                string characterFingerprint = reader.ReadString();
                StableId endOperation = reader.ReadId();
                string endFingerprint = reader.ReadString();
                StableId missionResult = reader.ReadOptionalId();
                string missionFingerprint = reader.ReadString();
                string batchFingerprint = reader.ReadString();
                string planFingerprint = reader.ReadString();
                string receiptFingerprint = reader.ReadString();
                ulong rootSeed = reader.ReadUInt64();
                int algorithm = reader.ReadInt32();
                ProgressionContext progression = ReadProgression(reader);
                string eventFingerprint = reader.ReadString();
                long moneySequence = reader.ReadInt64();
                long scrapSequence = reader.ReadInt64();
                long holdingsSequence = reader.ReadInt64();
                int authorityCount = reader.ReadInt32();
                var authorities = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int index = 0; index < authorityCount; index++)
                    authorities.Add(reader.ReadString(), reader.ReadString());
                int rewardCount = reader.ReadInt32();
                var rewards = new List<CollectedRunRewardTransferItemV1>(rewardCount);
                for (int index = 0; index < rewardCount; index++)
                    rewards.Add(ReadReward(reader.ReadString()));
                int equipmentCount = reader.ReadInt32();
                var equipment = new List<EquipmentInstance>(equipmentCount);
                for (int index = 0; index < equipmentCount; index++)
                    equipment.Add(ReadEquipment(reader));
                int boxCount = reader.ReadInt32();
                var boxes = new List<StrongboxInstanceContextV1>(boxCount);
                for (int index = 0; index < boxCount; index++) boxes.Add(ReadStrongbox(reader));

                CollectedRunRewardPreparedTransferV1 record =
                    CollectedRunRewardPreparedTransferV1.AwaitingAcceptedEnd(
                        custody, prepareOperation, run, lifecycle, character,
                        characterRevision, characterFingerprint, endOperation,
                        endFingerprint, rootSeed, algorithm, progression,
                        eventFingerprint, moneySequence, scrapSequence,
                        holdingsSequence, authorities, rewards, equipment, boxes);
                if (state == CollectedRunRewardPreparedTransferStateV1.AwaitingAcceptedEnd)
                    return record;
                record = record.AcceptEnd(
                    transferOperation, missionResult, missionFingerprint,
                    batchFingerprint, planFingerprint);
                return state == CollectedRunRewardPreparedTransferStateV1.Persisted
                    ? record.MarkPersisted(receiptFingerprint)
                    : record;
            }

            private static void WriteProgression(LineWriter writer, ProgressionContext value)
            {
                writer.Write(value.CharacterLevel);
                writer.Write(value.RegionLevel);
                writer.Write(value.DifficultyId);
                writer.Write(value.DifficultyValue);
                writer.Write(value.ProgressionTags.Count);
                for (int index = 0; index < value.ProgressionTags.Count; index++)
                    writer.Write(value.ProgressionTags[index]);
            }

            private static ProgressionContext ReadProgression(LineReader reader)
            {
                int characterLevel = reader.ReadInt32();
                int regionLevel = reader.ReadInt32();
                StableId difficulty = reader.ReadId();
                int difficultyValue = reader.ReadInt32();
                int tagCount = reader.ReadInt32();
                var tags = new List<StableId>(tagCount);
                for (int index = 0; index < tagCount; index++) tags.Add(reader.ReadId());
                return ProgressionContext.Create(
                    characterLevel, regionLevel, difficulty, difficultyValue, tags);
            }

            private static void WriteEquipment(LineWriter writer, EquipmentInstance value)
            {
                writer.Write(value.InstanceId);
                writer.Write(value.DefinitionId);
                writer.Write(value.ItemLevel);
                writer.Write(value.QualityId);
                writer.Write(value.Augments.Count);
                for (int index = 0; index < value.Augments.Count; index++)
                {
                    AugmentInstance augment = value.Augments[index];
                    writer.Write(augment.InstanceId);
                    writer.Write(augment.DefinitionId);
                    writer.Write(augment.Tier);
                    writer.Write(augment.Level);
                }
            }

            private static EquipmentInstance ReadEquipment(LineReader reader)
            {
                StableId instance = reader.ReadId();
                StableId definition = reader.ReadId();
                int level = reader.ReadInt32();
                StableId quality = reader.ReadId();
                int count = reader.ReadInt32();
                var augments = new List<AugmentInstance>(count);
                for (int index = 0; index < count; index++)
                {
                    augments.Add(AugmentInstance.Create(
                        reader.ReadId(), reader.ReadId(), reader.ReadInt32(), reader.ReadInt32()));
                }
                return EquipmentInstance.Create(instance, definition, level, quality, augments);
            }

            private static void WriteStrongbox(LineWriter writer, StrongboxInstanceContextV1 value)
            {
                writer.Write(value.InstanceStableId);
                writer.Write(value.TierStableId);
                writer.Write(value.RootSeed);
                writer.Write(value.AlgorithmVersion);
                WriteProgression(writer, value.ProgressionContext);
                writer.Write(value.SourceContextStableId);
                writer.Write(value.CollectionProvenanceStableId);
                writer.Write(value.AlgorithmContentFingerprint ?? string.Empty);
            }

            private static StrongboxInstanceContextV1 ReadStrongbox(LineReader reader)
            {
                StableId instance = reader.ReadId();
                StableId tier = reader.ReadId();
                ulong seed = reader.ReadUInt64();
                int algorithm = reader.ReadInt32();
                ProgressionContext progression = ReadProgression(reader);
                StableId source = reader.ReadId();
                StableId provenance = reader.ReadId();
                string content = reader.ReadString();
                return StrongboxInstanceContextV1.Create(
                    instance, tier, seed, algorithm, progression, source,
                    provenance, string.IsNullOrEmpty(content) ? null : content);
            }

            private static CollectedRunRewardTransferItemV1 ReadReward(string canonical)
            {
                IDictionary<string, string> fields = ParseTransferTokens(canonical);
                return new CollectedRunRewardTransferItemV1(
                    Id(fields, "reward-instance"),
                    (RewardGrantKindV1)Int(fields, "reward-kind"),
                    Id(fields, "content"),
                    Long(fields, "quantity"),
                    Id(fields, "pickup"),
                    Id(fields, "source-grant"),
                    Id(fields, "drop-operation"),
                    Id(fields, "terminal-event"),
                    OptionalId(fields, "triggering-event"),
                    Id(fields, "run"),
                    Long(fields, "run-lifecycle"),
                    Id(fields, "source-entity"),
                    OptionalId(fields, "source-placement"),
                    Long(fields, "source-lifecycle"),
                    Id(fields, "source-definition"),
                    Id(fields, "participant"),
                    fields["generated-batch"],
                    fields["generated-reward"],
                    Id(fields, "room"),
                    Double(fields, "world-x"),
                    Double(fields, "world-y"),
                    fields["world-spawn"],
                    fields["available-pickup"],
                    Id(fields, "collector-entity"),
                    Id(fields, "collector-participant"),
                    Id(fields, "collection-operation"),
                    Long(fields, "collection-order"),
                    Long(fields, "collected-tick"),
                    fields["collected-reward"]);
            }

            private static IDictionary<string, string> ParseTransferTokens(string canonical)
            {
                if (canonical == null) throw new FormatException("canonical-null");
                int position = canonical.IndexOf('|');
                if (position < 0) throw new FormatException("canonical-fields-missing");
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                while (position < canonical.Length)
                {
                    if (canonical[position] != '|') throw new FormatException("field-prefix");
                    position++;
                    int colon = canonical.IndexOf(':', position);
                    if (colon < 0) throw new FormatException("key-length");
                    int keyLength = int.Parse(canonical.Substring(position, colon - position), CultureInfo.InvariantCulture);
                    position = colon + 1;
                    string key = canonical.Substring(position, keyLength);
                    position += keyLength;
                    if (position >= canonical.Length || canonical[position] != '=') throw new FormatException("field-equals");
                    position++;
                    colon = canonical.IndexOf(':', position);
                    if (colon < 0) throw new FormatException("value-length");
                    int valueLength = int.Parse(canonical.Substring(position, colon - position), CultureInfo.InvariantCulture);
                    position = colon + 1;
                    string value = canonical.Substring(position, valueLength);
                    position += valueLength;
                    values.Add(key, value);
                }
                return values;
            }

            private static StableId Id(IDictionary<string, string> values, string key)
            {
                return StableId.Parse(values[key]);
            }
            private static StableId OptionalId(IDictionary<string, string> values, string key)
            {
                string value = values[key];
                return string.IsNullOrEmpty(value) ? null : StableId.Parse(value);
            }
            private static int Int(IDictionary<string, string> values, string key)
            {
                return int.Parse(values[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            private static long Long(IDictionary<string, string> values, string key)
            {
                return long.Parse(values[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            private static double Double(IDictionary<string, string> values, string key)
            {
                return double.Parse(values[key], NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }

        private sealed class LineWriter
        {
            private readonly StringBuilder builder = new StringBuilder();
            public void Write(object value)
            {
                string text;
                if (value == null) text = string.Empty;
                else if (value is IFormattable formattable)
                    text = formattable.ToString(null, CultureInfo.InvariantCulture);
                else text = value.ToString();
                builder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(text))).Append('\n');
            }
            public override string ToString() { return builder.ToString(); }
        }

        private sealed class LineReader
        {
            private readonly string[] lines;
            private int index;
            public LineReader(string payload)
            {
                if (payload == null) throw new ArgumentNullException(nameof(payload));
                lines = payload.Replace("\r\n", "\n").Split('\n');
            }
            public string ReadString()
            {
                if (index >= lines.Length) throw new FormatException("unexpected-end");
                string encoded = lines[index++];
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            public int ReadInt32() { return int.Parse(ReadString(), NumberStyles.Integer, CultureInfo.InvariantCulture); }
            public long ReadInt64() { return long.Parse(ReadString(), NumberStyles.Integer, CultureInfo.InvariantCulture); }
            public ulong ReadUInt64() { return ulong.Parse(ReadString(), NumberStyles.Integer, CultureInfo.InvariantCulture); }
            public StableId ReadId() { return StableId.Parse(ReadString()); }
            public StableId ReadOptionalId()
            {
                string value = ReadString();
                return string.IsNullOrEmpty(value) ? null : StableId.Parse(value);
            }
            public void RequireEnd()
            {
                while (index < lines.Length && lines[index].Length == 0) index++;
                if (index != lines.Length) throw new FormatException("trailing-data");
            }
        }
    }
}

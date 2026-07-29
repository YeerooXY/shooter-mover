using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Contracts.Rewards.Application
{
    /// <summary>
    /// Immutable application payload paired one-to-one with a generated grant.
    /// Value and stack grants need no extra payload. Unique grants retain the exact
    /// instance identities (and equipment instances) required for replay.
    /// </summary>
    public sealed class RewardGrantApplicationPayload :
        IComparable<RewardGrantApplicationPayload>,
        IEquatable<RewardGrantApplicationPayload>
    {
        private readonly ReadOnlyCollection<StableId> instanceStableIds;
        private readonly ReadOnlyCollection<EquipmentInstance> equipmentInstances;
        private readonly string canonicalText;

        private RewardGrantApplicationPayload(
            RewardGrant grant,
            IEnumerable<StableId> instanceStableIds,
            IEnumerable<EquipmentInstance> equipmentInstances)
        {
            Grant = grant ?? throw new ArgumentNullException(nameof(grant));
            this.instanceStableIds = CopyAndSortInstanceIds(instanceStableIds);
            this.equipmentInstances = CopyAndSortEquipment(equipmentInstances);
            ValidateShape();

            var builder = new StringBuilder();
            RewardApplication.AppendToken(
                builder,
                "grant",
                Grant.ToCanonicalString());
            RewardApplication.AppendToken(
                builder,
                "instance_count",
                this.instanceStableIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.instanceStableIds.Count; index++)
            {
                RewardApplication.AppendToken(
                    builder,
                    "instance_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    this.instanceStableIds[index].ToString());
            }

            RewardApplication.AppendToken(
                builder,
                "equipment_count",
                this.equipmentInstances.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.equipmentInstances.Count; index++)
            {
                RewardApplication.AppendToken(
                    builder,
                    "equipment_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    this.equipmentInstances[index].ToCanonicalString());
            }

            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public RewardGrant Grant { get; }

        public IReadOnlyList<StableId> InstanceStableIds
        {
            get { return instanceStableIds; }
        }

        public IReadOnlyList<EquipmentInstance> EquipmentInstances
        {
            get { return equipmentInstances; }
        }

        public string Fingerprint { get; }

        public static RewardGrantApplicationPayload ForValue(RewardGrant grant)
        {
            return new RewardGrantApplicationPayload(
                grant,
                Array.Empty<StableId>(),
                Array.Empty<EquipmentInstance>());
        }

        public static RewardGrantApplicationPayload ForStrongboxes(
            RewardGrant grant,
            IEnumerable<StableId> strongboxInstanceStableIds)
        {
            return new RewardGrantApplicationPayload(
                grant,
                strongboxInstanceStableIds,
                Array.Empty<EquipmentInstance>());
        }

        public static RewardGrantApplicationPayload ForEquipment(
            RewardGrant grant,
            IEnumerable<EquipmentInstance> equipmentInstances)
        {
            List<EquipmentInstance> copy = new List<EquipmentInstance>(
                equipmentInstances ?? throw new ArgumentNullException(nameof(equipmentInstances)));
            List<StableId> ids = new List<StableId>(copy.Count);
            for (int index = 0; index < copy.Count; index++)
            {
                EquipmentInstance equipment = copy[index]
                    ?? throw new ArgumentException(
                        "Equipment payloads must not contain null entries.",
                        nameof(equipmentInstances));
                ids.Add(equipment.InstanceId);
            }

            return new RewardGrantApplicationPayload(grant, ids, copy);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(RewardGrantApplicationPayload other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : Grant.GrantStableId.CompareTo(other.Grant.GrantStableId);
        }

        public bool Equals(RewardGrantApplicationPayload other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardGrantApplicationPayload);
        }

        public override int GetHashCode()
        {
            return RewardApplication.DeterministicHash(canonicalText);
        }

        private void ValidateShape()
        {
            switch (Grant.Kind)
            {
                case RewardGrantKind.Money:
                case RewardGrantKind.Scrap:
                case RewardGrantKind.PremiumAmmo:
                case RewardGrantKind.Miscellaneous:
                    if (instanceStableIds.Count != 0 || equipmentInstances.Count != 0)
                    {
                        throw new ArgumentException(
                            "Value and stack grants must not carry unique-instance payloads.");
                    }

                    break;
                case RewardGrantKind.Strongbox:
                    if (equipmentInstances.Count != 0
                        || instanceStableIds.Count != Grant.Quantity)
                    {
                        throw new ArgumentException(
                            "Strongbox payload instance count must equal the generated quantity.");
                    }

                    break;
                case RewardGrantKind.EquipmentReference:
                    if (instanceStableIds.Count != Grant.Quantity
                        || equipmentInstances.Count != Grant.Quantity)
                    {
                        throw new ArgumentException(
                            "Equipment payload count must equal the generated quantity.");
                    }

                    for (int index = 0; index < equipmentInstances.Count; index++)
                    {
                        EquipmentInstance equipment = equipmentInstances[index];
                        if (equipment.InstanceId != instanceStableIds[index])
                        {
                            throw new ArgumentException(
                                "Equipment and retained instance identities must match.");
                        }

                        if (equipment.DefinitionId != Grant.ContentStableId)
                        {
                            throw new ArgumentException(
                                "Equipment definition must match the generated grant content identity.");
                        }
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(Grant.Kind),
                        Grant.Kind,
                        "Unsupported reward grant kind.");
            }
        }

        private static ReadOnlyCollection<StableId> CopyAndSortInstanceIds(
            IEnumerable<StableId> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<StableId>();
            var identities = new HashSet<StableId>();
            foreach (StableId value in source)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Instance identities must not contain null entries.",
                        nameof(source));
                }

                if (!identities.Add(value))
                {
                    throw new ArgumentException(
                        "Instance identities contain duplicate identity " + value + ".",
                        nameof(source));
                }

                copy.Add(value);
            }

            copy.Sort();
            return new ReadOnlyCollection<StableId>(copy);
        }

        private static ReadOnlyCollection<EquipmentInstance> CopyAndSortEquipment(
            IEnumerable<EquipmentInstance> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<EquipmentInstance>();
            var identities = new HashSet<StableId>();
            foreach (EquipmentInstance value in source)
            {
                if (value == null)
                {
                    throw new ArgumentException(
                        "Equipment payloads must not contain null entries.",
                        nameof(source));
                }

                if (!identities.Add(value.InstanceId))
                {
                    throw new ArgumentException(
                        "Equipment payloads contain duplicate instance identity "
                        + value.InstanceId
                        + ".",
                        nameof(source));
                }

                copy.Add(value);
            }

            copy.Sort(delegate(EquipmentInstance left, EquipmentInstance right)
            {
                return left.InstanceId.CompareTo(right.InstanceId);
            });
            return new ReadOnlyCollection<EquipmentInstance>(copy);
        }
    }

    public sealed class RewardCommitCommand : IEquatable<RewardCommitCommand>
    {
        private readonly ReadOnlyCollection<RewardGrantApplicationPayload> grantPayloads;
        private readonly string canonicalText;

        private RewardCommitCommand(
            RewardOperationRequest operation,
            RewardResult generatedReward,
            string generationFingerprint,
            IEnumerable<RewardGrantApplicationPayload> grantPayloads)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            GeneratedReward = generatedReward
                ?? throw new ArgumentNullException(nameof(generatedReward));
            if (!RewardApplication.IsCanonicalFingerprint(generationFingerprint))
            {
                throw new ArgumentException(
                    "Generation fingerprint must use canonical sha256 form.",
                    nameof(generationFingerprint));
            }

            if (Operation.CommitmentStableId != GeneratedReward.CommitmentStableId
                || Operation.SourceOperationStableId
                    != GeneratedReward.SourceOperationStableId)
            {
                throw new ArgumentException(
                    "Operation and generated reward commitment/source identities must match.");
            }

            GenerationFingerprint = generationFingerprint;
            this.grantPayloads = CopyAndValidatePayloads(grantPayloads, GeneratedReward);

            var builder = new StringBuilder();
            RewardApplication.AppendToken(
                builder,
                "operation",
                Operation.ToCanonicalString());
            RewardApplication.AppendToken(
                builder,
                "generated_reward",
                GeneratedReward.ToCanonicalString());
            RewardApplication.AppendToken(
                builder,
                "generation_fingerprint",
                GenerationFingerprint);
            RewardApplication.AppendToken(
                builder,
                "payload_count",
                this.grantPayloads.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.grantPayloads.Count; index++)
            {
                RewardApplication.AppendToken(
                    builder,
                    "payload_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    this.grantPayloads[index].ToCanonicalString());
            }

            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public RewardOperationRequest Operation { get; }

        public RewardResult GeneratedReward { get; }

        public string GenerationFingerprint { get; }

        public IReadOnlyList<RewardGrantApplicationPayload> GrantPayloads
        {
            get { return grantPayloads; }
        }

        public StableId CommitmentStableId
        {
            get { return GeneratedReward.CommitmentStableId; }
        }

        public StableId SourceOperationStableId
        {
            get { return GeneratedReward.SourceOperationStableId; }
        }

        public string Fingerprint { get; }

        public static RewardCommitCommand Create(
            RewardOperationRequest operation,
            RewardResult generatedReward,
            string generationFingerprint,
            IEnumerable<RewardGrantApplicationPayload> grantPayloads)
        {
            return new RewardCommitCommand(
                operation,
                generatedReward,
                generationFingerprint,
                grantPayloads);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(RewardCommitCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardCommitCommand);
        }

        public override int GetHashCode()
        {
            return RewardApplication.DeterministicHash(canonicalText);
        }

        private static ReadOnlyCollection<RewardGrantApplicationPayload>
            CopyAndValidatePayloads(
                IEnumerable<RewardGrantApplicationPayload> source,
                RewardResult result)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<RewardGrantApplicationPayload>();
            var ids = new HashSet<StableId>();
            foreach (RewardGrantApplicationPayload payload in source)
            {
                if (payload == null)
                {
                    throw new ArgumentException(
                        "Grant payloads must not contain null entries.",
                        nameof(source));
                }

                if (!ids.Add(payload.Grant.GrantStableId))
                {
                    throw new ArgumentException(
                        "Grant payloads contain duplicate grant identity "
                        + payload.Grant.GrantStableId
                        + ".",
                        nameof(source));
                }

                copy.Add(payload);
            }

            copy.Sort();
            if (copy.Count != result.Grants.Count)
            {
                throw new ArgumentException(
                    "Application payloads must map one-to-one to generated grants.",
                    nameof(source));
            }

            for (int index = 0; index < copy.Count; index++)
            {
                if (!copy[index].Grant.Equals(result.Grants[index]))
                {
                    throw new ArgumentException(
                        "Application payload does not match the canonical generated grant at index "
                        + index.ToString(CultureInfo.InvariantCulture)
                        + ".",
                        nameof(source));
                }
            }

            return new ReadOnlyCollection<RewardGrantApplicationPayload>(copy);
        }
    }

    public sealed class RewardProjectCommand :
        IComparable<RewardProjectCommand>,
        IEquatable<RewardProjectCommand>
    {
        private readonly string canonicalText;

        private RewardProjectCommand(
            StableId projectionStableId,
            StableId commitmentStableId,
            StableId presentationStableId)
        {
            ProjectionStableId = projectionStableId
                ?? throw new ArgumentNullException(nameof(projectionStableId));
            CommitmentStableId = commitmentStableId
                ?? throw new ArgumentNullException(nameof(commitmentStableId));
            PresentationStableId = presentationStableId
                ?? throw new ArgumentNullException(nameof(presentationStableId));

            var builder = new StringBuilder();
            RewardApplication.AppendToken(
                builder,
                "projection_stable_id",
                ProjectionStableId.ToString());
            RewardApplication.AppendToken(
                builder,
                "commitment_stable_id",
                CommitmentStableId.ToString());
            RewardApplication.AppendToken(
                builder,
                "presentation_stable_id",
                PresentationStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public StableId ProjectionStableId { get; }
        public StableId CommitmentStableId { get; }
        public StableId PresentationStableId { get; }
        public string Fingerprint { get; }

        public static RewardProjectCommand Create(
            StableId projectionStableId,
            StableId commitmentStableId,
            StableId presentationStableId)
        {
            return new RewardProjectCommand(
                projectionStableId,
                commitmentStableId,
                presentationStableId);
        }

        public string ToCanonicalString() { return canonicalText; }

        public int CompareTo(RewardProjectCommand other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : ProjectionStableId.CompareTo(other.ProjectionStableId);
        }

        public bool Equals(RewardProjectCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as RewardProjectCommand); }
        public override int GetHashCode() { return RewardApplication.DeterministicHash(canonicalText); }
    }

    public sealed class RewardClaimCommand : IEquatable<RewardClaimCommand>
    {
        private readonly string canonicalText;

        private RewardClaimCommand(
            StableId claimStableId,
            StableId commitmentStableId,
            StableId claimantStableId,
            StableId moneyAuthorityStableId,
            StableId scrapAuthorityStableId,
            StableId holdingsAuthorityStableId,
            long? expectedMoneySequence,
            long? expectedScrapSequence,
            long? expectedHoldingsSequence)
        {
            ClaimStableId = claimStableId
                ?? throw new ArgumentNullException(nameof(claimStableId));
            CommitmentStableId = commitmentStableId
                ?? throw new ArgumentNullException(nameof(commitmentStableId));
            ClaimantStableId = claimantStableId
                ?? throw new ArgumentNullException(nameof(claimantStableId));
            MoneyAuthorityStableId = moneyAuthorityStableId
                ?? throw new ArgumentNullException(nameof(moneyAuthorityStableId));
            ScrapAuthorityStableId = scrapAuthorityStableId
                ?? throw new ArgumentNullException(nameof(scrapAuthorityStableId));
            HoldingsAuthorityStableId = holdingsAuthorityStableId
                ?? throw new ArgumentNullException(nameof(holdingsAuthorityStableId));
            ValidateExpectedSequence(expectedMoneySequence, nameof(expectedMoneySequence));
            ValidateExpectedSequence(expectedScrapSequence, nameof(expectedScrapSequence));
            ValidateExpectedSequence(expectedHoldingsSequence, nameof(expectedHoldingsSequence));
            ExpectedMoneySequence = expectedMoneySequence;
            ExpectedScrapSequence = expectedScrapSequence;
            ExpectedHoldingsSequence = expectedHoldingsSequence;

            var builder = new StringBuilder();
            RewardApplication.AppendToken(builder, "claim_stable_id", ClaimStableId.ToString());
            RewardApplication.AppendToken(builder, "commitment_stable_id", CommitmentStableId.ToString());
            RewardApplication.AppendToken(builder, "claimant_stable_id", ClaimantStableId.ToString());
            RewardApplication.AppendToken(builder, "money_authority_stable_id", MoneyAuthorityStableId.ToString());
            RewardApplication.AppendToken(builder, "scrap_authority_stable_id", ScrapAuthorityStableId.ToString());
            RewardApplication.AppendToken(builder, "holdings_authority_stable_id", HoldingsAuthorityStableId.ToString());
            RewardApplication.AppendToken(builder, "expected_money_sequence", RewardApplication.OptionalLong(ExpectedMoneySequence));
            RewardApplication.AppendToken(builder, "expected_scrap_sequence", RewardApplication.OptionalLong(ExpectedScrapSequence));
            RewardApplication.AppendToken(builder, "expected_holdings_sequence", RewardApplication.OptionalLong(ExpectedHoldingsSequence));
            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public StableId ClaimStableId { get; }
        public StableId CommitmentStableId { get; }
        public StableId ClaimantStableId { get; }
        public StableId MoneyAuthorityStableId { get; }
        public StableId ScrapAuthorityStableId { get; }
        public StableId HoldingsAuthorityStableId { get; }
        public long? ExpectedMoneySequence { get; }
        public long? ExpectedScrapSequence { get; }
        public long? ExpectedHoldingsSequence { get; }
        public string Fingerprint { get; }

        public static RewardClaimCommand Create(
            StableId claimStableId,
            StableId commitmentStableId,
            StableId claimantStableId,
            StableId moneyAuthorityStableId,
            StableId scrapAuthorityStableId,
            StableId holdingsAuthorityStableId,
            long? expectedMoneySequence = null,
            long? expectedScrapSequence = null,
            long? expectedHoldingsSequence = null)
        {
            return new RewardClaimCommand(
                claimStableId,
                commitmentStableId,
                claimantStableId,
                moneyAuthorityStableId,
                scrapAuthorityStableId,
                holdingsAuthorityStableId,
                expectedMoneySequence,
                expectedScrapSequence,
                expectedHoldingsSequence);
        }

        public string ToCanonicalString() { return canonicalText; }

        public bool Equals(RewardClaimCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as RewardClaimCommand); }
        public override int GetHashCode() { return RewardApplication.DeterministicHash(canonicalText); }

        private static void ValidateExpectedSequence(long? value, string parameterName)
        {
            if (value.HasValue && value.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class RewardRetryClaimCommand
    {
        private RewardRetryClaimCommand(
            StableId commitmentStableId,
            StableId claimStableId)
        {
            CommitmentStableId = commitmentStableId
                ?? throw new ArgumentNullException(nameof(commitmentStableId));
            ClaimStableId = claimStableId
                ?? throw new ArgumentNullException(nameof(claimStableId));
        }

        public StableId CommitmentStableId { get; }
        public StableId ClaimStableId { get; }

        public static RewardRetryClaimCommand Create(
            StableId commitmentStableId,
            StableId claimStableId)
        {
            return new RewardRetryClaimCommand(commitmentStableId, claimStableId);
        }
    }

    public sealed class RewardCancelCommand : IEquatable<RewardCancelCommand>
    {
        private readonly string canonicalText;

        private RewardCancelCommand(
            StableId cancellationStableId,
            StableId commitmentStableId,
            StableId reasonStableId)
        {
            CancellationStableId = cancellationStableId
                ?? throw new ArgumentNullException(nameof(cancellationStableId));
            CommitmentStableId = commitmentStableId
                ?? throw new ArgumentNullException(nameof(commitmentStableId));
            ReasonStableId = reasonStableId
                ?? throw new ArgumentNullException(nameof(reasonStableId));

            var builder = new StringBuilder();
            RewardApplication.AppendToken(builder, "cancellation_stable_id", CancellationStableId.ToString());
            RewardApplication.AppendToken(builder, "commitment_stable_id", CommitmentStableId.ToString());
            RewardApplication.AppendToken(builder, "reason_stable_id", ReasonStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public StableId CancellationStableId { get; }
        public StableId CommitmentStableId { get; }
        public StableId ReasonStableId { get; }
        public string Fingerprint { get; }

        public static RewardCancelCommand Create(
            StableId cancellationStableId,
            StableId commitmentStableId,
            StableId reasonStableId)
        {
            return new RewardCancelCommand(
                cancellationStableId,
                commitmentStableId,
                reasonStableId);
        }

        public string ToCanonicalString() { return canonicalText; }

        public bool Equals(RewardCancelCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as RewardCancelCommand); }
        public override int GetHashCode() { return RewardApplication.DeterministicHash(canonicalText); }
    }

    /// <summary>
    /// Fully prepared immutable child command. Transaction and operation identities
    /// are deterministic and never regenerated during retry.
    /// </summary>
    public sealed class RewardChildGrantCommand :
        IComparable<RewardChildGrantCommand>,
        IEquatable<RewardChildGrantCommand>
    {
        private readonly string canonicalText;

        private RewardChildGrantCommand(
            StableId transactionStableId,
            StableId operationStableId,
            StableId destinationAuthorityStableId,
            StableId sourceOperationStableId,
            StableId claimantStableId,
            StableId grantStableId,
            RewardGrantKind grantKind,
            StableId contentStableId,
            long quantity,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            long? expectedSequence)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            DestinationAuthorityStableId = destinationAuthorityStableId
                ?? throw new ArgumentNullException(nameof(destinationAuthorityStableId));
            SourceOperationStableId = sourceOperationStableId
                ?? throw new ArgumentNullException(nameof(sourceOperationStableId));
            ClaimantStableId = claimantStableId
                ?? throw new ArgumentNullException(nameof(claimantStableId));
            GrantStableId = grantStableId
                ?? throw new ArgumentNullException(nameof(grantStableId));
            if (!Enum.IsDefined(typeof(RewardGrantKind), grantKind))
            {
                throw new ArgumentOutOfRangeException(nameof(grantKind));
            }

            ContentStableId = contentStableId
                ?? throw new ArgumentNullException(nameof(contentStableId));
            if (quantity < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            if (expectedSequence.HasValue && expectedSequence.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedSequence));
            }

            bool unique = grantKind == RewardGrantKind.Strongbox
                || grantKind == RewardGrantKind.EquipmentReference;
            if (unique && (instanceStableId == null || quantity != 1L))
            {
                throw new ArgumentException(
                    "Unique child grants require one stable instance identity and quantity one.");
            }

            if (!unique && instanceStableId != null)
            {
                throw new ArgumentException(
                    "Non-unique child grants must not carry an instance identity.",
                    nameof(instanceStableId));
            }

            if (grantKind == RewardGrantKind.EquipmentReference)
            {
                if (equipmentInstance == null
                    || equipmentInstance.InstanceId != instanceStableId
                    || equipmentInstance.DefinitionId != contentStableId)
                {
                    throw new ArgumentException(
                        "Equipment child payload must match its instance and definition identities.",
                        nameof(equipmentInstance));
                }
            }
            else if (equipmentInstance != null)
            {
                throw new ArgumentException(
                    "Only equipment child grants may carry equipment payloads.",
                    nameof(equipmentInstance));
            }

            GrantKind = grantKind;
            Quantity = quantity;
            InstanceStableId = instanceStableId;
            EquipmentInstance = equipmentInstance;
            ExpectedSequence = expectedSequence;

            var builder = new StringBuilder();
            RewardApplication.AppendToken(builder, "transaction_stable_id", TransactionStableId.ToString());
            RewardApplication.AppendToken(builder, "operation_stable_id", OperationStableId.ToString());
            RewardApplication.AppendToken(builder, "destination_authority_stable_id", DestinationAuthorityStableId.ToString());
            RewardApplication.AppendToken(builder, "source_operation_stable_id", SourceOperationStableId.ToString());
            RewardApplication.AppendToken(builder, "claimant_stable_id", ClaimantStableId.ToString());
            RewardApplication.AppendToken(builder, "grant_stable_id", GrantStableId.ToString());
            RewardApplication.AppendToken(builder, "grant_kind", ((int)GrantKind).ToString(CultureInfo.InvariantCulture));
            RewardApplication.AppendToken(builder, "content_stable_id", ContentStableId.ToString());
            RewardApplication.AppendToken(builder, "quantity", Quantity.ToString(CultureInfo.InvariantCulture));
            RewardApplication.AppendToken(builder, "instance_stable_id", RewardApplication.OptionalId(InstanceStableId));
            RewardApplication.AppendToken(builder, "equipment", EquipmentInstance == null ? "none" : EquipmentInstance.ToCanonicalString());
            RewardApplication.AppendToken(builder, "expected_sequence", RewardApplication.OptionalLong(ExpectedSequence));
            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public StableId TransactionStableId { get; }
        public StableId OperationStableId { get; }
        public StableId DestinationAuthorityStableId { get; }
        public StableId SourceOperationStableId { get; }
        public StableId ClaimantStableId { get; }
        public StableId GrantStableId { get; }
        public RewardGrantKind GrantKind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public StableId InstanceStableId { get; }
        public EquipmentInstance EquipmentInstance { get; }
        public long? ExpectedSequence { get; }
        public string Fingerprint { get; }

        public static RewardChildGrantCommand Create(
            StableId transactionStableId,
            StableId operationStableId,
            StableId destinationAuthorityStableId,
            StableId sourceOperationStableId,
            StableId claimantStableId,
            StableId grantStableId,
            RewardGrantKind grantKind,
            StableId contentStableId,
            long quantity,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            long? expectedSequence)
        {
            return new RewardChildGrantCommand(
                transactionStableId,
                operationStableId,
                destinationAuthorityStableId,
                sourceOperationStableId,
                claimantStableId,
                grantStableId,
                grantKind,
                contentStableId,
                quantity,
                instanceStableId,
                equipmentInstance,
                expectedSequence);
        }

        public string ToCanonicalString() { return canonicalText; }

        public int CompareTo(RewardChildGrantCommand other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : TransactionStableId.CompareTo(other.TransactionStableId);
        }

        public bool Equals(RewardChildGrantCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as RewardChildGrantCommand); }
        public override int GetHashCode() { return RewardApplication.DeterministicHash(canonicalText); }
    }
}

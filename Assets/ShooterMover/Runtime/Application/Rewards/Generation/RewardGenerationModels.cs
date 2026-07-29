using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Generation
{
    public sealed class RewardGenerationScalingValue : IComparable<RewardGenerationScalingValue>
    {
        private RewardGenerationScalingValue(StableId inputId, long value)
        {
            InputId = inputId ?? throw new ArgumentNullException(nameof(inputId));
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public StableId InputId { get; }
        public long Value { get; }

        public static RewardGenerationScalingValue Create(StableId inputId, long value)
        {
            return new RewardGenerationScalingValue(inputId, value);
        }

        public int CompareTo(RewardGenerationScalingValue other)
        {
            return ReferenceEquals(other, null) ? 1 : InputId.CompareTo(other.InputId);
        }

        public string ToCanonicalString()
        {
            return "input_id=" + InputId + "\nvalue=" + Value.ToString(CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class RewardGenerationRequest
    {
        private readonly ReadOnlyCollection<RewardGenerationScalingValue> scalingValues;

        private RewardGenerationRequest(
            RewardOperationRequest operation,
            RewardProfile profile,
            ProgressionContext context,
            ulong rootSeed,
            int algorithmVersion,
            IEnumerable<RewardGenerationScalingValue> scalingValues)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (Operation.RewardProfileStableId != Profile.ProfileStableId)
            {
                throw new ArgumentException("Operation and profile identities must match.", nameof(profile));
            }

            DeterministicRandom.Create(rootSeed, algorithmVersion);
            RootSeed = rootSeed;
            AlgorithmVersion = algorithmVersion;
            this.scalingValues = CopyScalingValues(scalingValues);
        }

        public RewardOperationRequest Operation { get; }
        public RewardProfile Profile { get; }
        public ProgressionContext Context { get; }
        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }
        public IReadOnlyList<RewardGenerationScalingValue> ScalingValues { get { return scalingValues; } }

        public static RewardGenerationRequest Create(
            RewardOperationRequest operation,
            RewardProfile profile,
            ProgressionContext context,
            ulong rootSeed,
            int algorithmVersion,
            IEnumerable<RewardGenerationScalingValue> scalingValues = null)
        {
            return new RewardGenerationRequest(
                operation,
                profile,
                context,
                rootSeed,
                algorithmVersion,
                scalingValues);
        }

        public bool TryGetScalingValue(StableId inputId, out long value)
        {
            for (int index = 0; index < scalingValues.Count; index++)
            {
                int comparison = scalingValues[index].InputId.CompareTo(inputId);
                if (comparison == 0)
                {
                    value = scalingValues[index].Value;
                    return true;
                }

                if (comparison > 0)
                {
                    break;
                }
            }

            value = 0L;
            return false;
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=reward-generation-request-v1")
                .Append("\noperation_fingerprint=").Append(Operation.Fingerprint)
                .Append("\nprofile_fingerprint=").Append(Profile.Fingerprint)
                .Append("\ncontext_fingerprint=").Append(Context.Fingerprint)
                .Append("\nroot_seed=").Append(RootSeed.ToString(CultureInfo.InvariantCulture))
                .Append("\nalgorithm_version=").Append(AlgorithmVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\nscaling_value_count=").Append(scalingValues.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < scalingValues.Count; index++)
            {
                builder.Append("\nscaling_value_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n").Append(scalingValues[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        private static ReadOnlyCollection<RewardGenerationScalingValue> CopyScalingValues(
            IEnumerable<RewardGenerationScalingValue> source)
        {
            List<RewardGenerationScalingValue> values = new List<RewardGenerationScalingValue>();
            HashSet<StableId> ids = new HashSet<StableId>();
            if (source != null)
            {
                foreach (RewardGenerationScalingValue value in source)
                {
                    if (value == null)
                    {
                        throw new ArgumentException("Scaling values must not contain null entries.", nameof(source));
                    }

                    if (!ids.Add(value.InputId))
                    {
                        throw new ArgumentException("Scaling values contain duplicate identity " + value.InputId + ".", nameof(source));
                    }

                    values.Add(value);
                }
            }

            values.Sort();
            return new ReadOnlyCollection<RewardGenerationScalingValue>(values);
        }
    }

    public sealed class RewardGenerationResultEnvelope
    {
        internal RewardGenerationResultEnvelope(
            RewardGenerationStatus status,
            RewardResult result,
            RewardTrace rewardTrace,
            RewardGenerationTrace generationTrace,
            string contentFingerprint,
            string contextFingerprint,
            string resultFingerprint,
            string failureReason)
        {
            Status = status;
            Result = result;
            RewardTrace = rewardTrace;
            GenerationTrace = generationTrace ?? throw new ArgumentNullException(nameof(generationTrace));
            ContentFingerprint = contentFingerprint;
            ContextFingerprint = contextFingerprint;
            ResultFingerprint = resultFingerprint;
            FailureReason = failureReason ?? string.Empty;
        }

        public RewardGenerationStatus Status { get; }
        public RewardResult Result { get; }
        public RewardTrace RewardTrace { get; }
        public RewardGenerationTrace GenerationTrace { get; }
        public string ContentFingerprint { get; }
        public string ContextFingerprint { get; }
        public string ResultFingerprint { get; }
        public string FailureReason { get; }
        public bool IsSuccess { get { return Status == RewardGenerationStatus.Generated || Status == RewardGenerationStatus.ExplicitNoDrop; } }
    }

    public sealed class EquipmentGenerationRequest
    {
        private EquipmentGenerationRequest(
            StableId operationId,
            StableId equipmentInstanceId,
            EquipmentGenerationPolicy policy,
            EquipmentCatalog catalog,
            ProgressionContext context,
            ulong rootSeed,
            int algorithmVersion)
        {
            OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
            EquipmentInstanceId = equipmentInstanceId ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            DeterministicRandom.Create(rootSeed, algorithmVersion);
            RootSeed = rootSeed;
            AlgorithmVersion = algorithmVersion;
        }

        public StableId OperationId { get; }
        public StableId EquipmentInstanceId { get; }
        public EquipmentGenerationPolicy Policy { get; }
        public EquipmentCatalog Catalog { get; }
        public ProgressionContext Context { get; }
        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }

        public static EquipmentGenerationRequest Create(
            StableId operationId,
            StableId equipmentInstanceId,
            EquipmentGenerationPolicy policy,
            EquipmentCatalog catalog,
            ProgressionContext context,
            ulong rootSeed,
            int algorithmVersion)
        {
            return new EquipmentGenerationRequest(
                operationId,
                equipmentInstanceId,
                policy,
                catalog,
                context,
                rootSeed,
                algorithmVersion);
        }

        public string ToCanonicalString()
        {
            return "schema=equipment-generation-request-v1"
                + "\noperation_id=" + OperationId
                + "\nequipment_instance_id=" + EquipmentInstanceId
                + "\npolicy_fingerprint=" + Policy.Fingerprint
                + "\ncatalog_fingerprint=" + Catalog.Fingerprint
                + "\ncontext_fingerprint=" + Context.Fingerprint
                + "\nroot_seed=" + RootSeed.ToString(CultureInfo.InvariantCulture)
                + "\nalgorithm_version=" + AlgorithmVersion.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class EquipmentGenerationResult
    {
        internal EquipmentGenerationResult(
            RewardGenerationStatus status,
            EquipmentInstance equipment,
            RewardGenerationTrace trace,
            string contentFingerprint,
            string contextFingerprint,
            string resultFingerprint,
            string failureReason)
        {
            Status = status;
            Equipment = equipment;
            Trace = trace ?? throw new ArgumentNullException(nameof(trace));
            ContentFingerprint = contentFingerprint;
            ContextFingerprint = contextFingerprint;
            ResultFingerprint = resultFingerprint;
            FailureReason = failureReason ?? string.Empty;
        }

        public RewardGenerationStatus Status { get; }
        public EquipmentInstance Equipment { get; }
        public RewardGenerationTrace Trace { get; }
        public string ContentFingerprint { get; }
        public string ContextFingerprint { get; }
        public string ResultFingerprint { get; }
        public string FailureReason { get; }
        public bool IsSuccess { get { return Status == RewardGenerationStatus.Generated; } }
    }
}

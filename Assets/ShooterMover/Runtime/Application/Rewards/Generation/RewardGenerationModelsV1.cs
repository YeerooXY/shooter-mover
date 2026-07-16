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
    public sealed class RewardGenerationScalingValueV1 : IComparable<RewardGenerationScalingValueV1>
    {
        private RewardGenerationScalingValueV1(StableId inputId, long value)
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

        public static RewardGenerationScalingValueV1 Create(StableId inputId, long value)
        {
            return new RewardGenerationScalingValueV1(inputId, value);
        }

        public int CompareTo(RewardGenerationScalingValueV1 other)
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

    public sealed class RewardGenerationRequestV1
    {
        private readonly ReadOnlyCollection<RewardGenerationScalingValueV1> scalingValues;

        private RewardGenerationRequestV1(
            RewardOperationRequestV1 operation,
            RewardProfileV1 profile,
            ProgressionContext context,
            ulong rootSeed,
            int algorithmVersion,
            IEnumerable<RewardGenerationScalingValueV1> scalingValues)
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

        public RewardOperationRequestV1 Operation { get; }
        public RewardProfileV1 Profile { get; }
        public ProgressionContext Context { get; }
        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }
        public IReadOnlyList<RewardGenerationScalingValueV1> ScalingValues { get { return scalingValues; } }

        public static RewardGenerationRequestV1 Create(
            RewardOperationRequestV1 operation,
            RewardProfileV1 profile,
            ProgressionContext context,
            ulong rootSeed,
            int algorithmVersion,
            IEnumerable<RewardGenerationScalingValueV1> scalingValues = null)
        {
            return new RewardGenerationRequestV1(
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

        private static ReadOnlyCollection<RewardGenerationScalingValueV1> CopyScalingValues(
            IEnumerable<RewardGenerationScalingValueV1> source)
        {
            List<RewardGenerationScalingValueV1> values = new List<RewardGenerationScalingValueV1>();
            HashSet<StableId> ids = new HashSet<StableId>();
            if (source != null)
            {
                foreach (RewardGenerationScalingValueV1 value in source)
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
            return new ReadOnlyCollection<RewardGenerationScalingValueV1>(values);
        }
    }

    public sealed class RewardGenerationResultEnvelopeV1
    {
        internal RewardGenerationResultEnvelopeV1(
            RewardGenerationStatusV1 status,
            RewardResultV1 result,
            RewardTraceV1 rewardTrace,
            RewardGenerationTraceV1 generationTrace,
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

        public RewardGenerationStatusV1 Status { get; }
        public RewardResultV1 Result { get; }
        public RewardTraceV1 RewardTrace { get; }
        public RewardGenerationTraceV1 GenerationTrace { get; }
        public string ContentFingerprint { get; }
        public string ContextFingerprint { get; }
        public string ResultFingerprint { get; }
        public string FailureReason { get; }
        public bool IsSuccess { get { return Status == RewardGenerationStatusV1.Generated || Status == RewardGenerationStatusV1.ExplicitNoDrop; } }
    }

    public sealed class EquipmentGenerationRequestV1
    {
        private EquipmentGenerationRequestV1(
            StableId operationId,
            StableId equipmentInstanceId,
            EquipmentGenerationPolicyV1 policy,
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
        public EquipmentGenerationPolicyV1 Policy { get; }
        public EquipmentCatalog Catalog { get; }
        public ProgressionContext Context { get; }
        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }

        public static EquipmentGenerationRequestV1 Create(
            StableId operationId,
            StableId equipmentInstanceId,
            EquipmentGenerationPolicyV1 policy,
            EquipmentCatalog catalog,
            ProgressionContext context,
            ulong rootSeed,
            int algorithmVersion)
        {
            return new EquipmentGenerationRequestV1(
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

    public sealed class EquipmentGenerationResultV1
    {
        internal EquipmentGenerationResultV1(
            RewardGenerationStatusV1 status,
            EquipmentInstance equipment,
            RewardGenerationTraceV1 trace,
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

        public RewardGenerationStatusV1 Status { get; }
        public EquipmentInstance Equipment { get; }
        public RewardGenerationTraceV1 Trace { get; }
        public string ContentFingerprint { get; }
        public string ContextFingerprint { get; }
        public string ResultFingerprint { get; }
        public string FailureReason { get; }
        public bool IsSuccess { get { return Status == RewardGenerationStatusV1.Generated; } }
    }
}

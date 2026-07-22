using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Run-local exact equipment payload retention. Equipment is materialized during the
    /// same DROP/GEN call that produced its unique child identity, never during Results.
    /// </summary>
    public sealed class RetainedTerminalDropEquipmentPayloadAuthority :
        ICollectedRunEquipmentPayloadSource
    {
        private readonly object gate = new object();
        private readonly RewardGenerationServiceV1 generator;
        private readonly EquipmentCatalog catalog;
        private readonly Dictionary<StableId, EquipmentInstance> retained =
            new Dictionary<StableId, EquipmentInstance>();

        public RetainedTerminalDropEquipmentPayloadAuthority(
            RewardGenerationServiceV1 generator,
            EquipmentCatalog catalog)
        {
            this.generator = generator
                ?? throw new ArgumentNullException(nameof(generator));
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int Count
        {
            get
            {
                lock (gate) return retained.Count;
            }
        }

        public void RetainExactPayloads(
            RewardGenerationRequestV1 request,
            RewardGenerationResultEnvelopeV1 envelope)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (envelope == null
                || !envelope.IsSuccess
                || envelope.Result == null)
            {
                return;
            }

            for (int grantIndex = 0;
                grantIndex < envelope.Result.Grants.Count;
                grantIndex++)
            {
                RewardGrantV1 grant = envelope.Result.Grants[grantIndex];
                if (grant.Kind != RewardGrantKindV1.EquipmentReference)
                    continue;
                if (grant.Quantity > int.MaxValue)
                    throw new InvalidOperationException(
                        "Equipment reward quantity exceeds deterministic child capacity.");

                for (long unitIndex = 0L;
                    unitIndex < grant.Quantity;
                    unitIndex++)
                {
                    StableId childStableId = DeriveChildStableId(
                        request.Operation,
                        grant,
                        unitIndex);
                    EquipmentInstance generated = GenerateExact(
                        request,
                        grant.ContentStableId,
                        childStableId,
                        unitIndex);
                    lock (gate)
                    {
                        EquipmentInstance existing;
                        if (retained.TryGetValue(
                            childStableId,
                            out existing))
                        {
                            if (!string.Equals(
                                existing.ToCanonicalString(),
                                generated.ToCanonicalString(),
                                StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException(
                                    "A retained equipment child identity resolved to conflicting payloads: "
                                    + childStableId);
                            }
                            continue;
                        }
                        retained.Add(childStableId, generated);
                    }
                }
            }
        }

        public bool TryResolveExact(
            StableId rewardInstanceStableId,
            StableId equipmentDefinitionStableId,
            out EquipmentInstance equipment,
            out string diagnostic)
        {
            equipment = null;
            diagnostic = string.Empty;
            if (rewardInstanceStableId == null
                || equipmentDefinitionStableId == null)
            {
                diagnostic =
                    "collected-run-transfer-equipment-identity-missing";
                return false;
            }
            lock (gate)
            {
                if (!retained.TryGetValue(
                    rewardInstanceStableId,
                    out equipment)
                    || equipment == null)
                {
                    diagnostic =
                        "collected-run-transfer-equipment-payload-not-retained:"
                        + rewardInstanceStableId;
                    equipment = null;
                    return false;
                }
                if (equipment.InstanceId != rewardInstanceStableId
                    || equipment.DefinitionId
                        != equipmentDefinitionStableId)
                {
                    diagnostic =
                        "collected-run-transfer-equipment-payload-identity-conflict:"
                        + rewardInstanceStableId;
                    equipment = null;
                    return false;
                }
                return true;
            }
        }

        private EquipmentInstance GenerateExact(
            RewardGenerationRequestV1 rewardRequest,
            StableId definitionStableId,
            StableId equipmentInstanceStableId,
            long unitIndex)
        {
            EquipmentDefinition definition =
                catalog.FindEquipmentDefinition(definitionStableId);
            if (definition == null)
                throw new InvalidOperationException(
                    "Equipment reward references an unknown definition: "
                    + definitionStableId);

            EquipmentGenerationPolicyV1 policy =
                BuildDefinitionLockedPolicy(definition);
            ulong seed = DeriveEquipmentSeed(
                rewardRequest.RootSeed,
                rewardRequest.Operation.Fingerprint,
                equipmentInstanceStableId,
                unitIndex);
            StableId operationStableId =
                RewardApplicationCanonicalV1.DeriveStableId(
                    "terminaldropequipment",
                    rewardRequest.Operation.SourceOperationStableId.ToString(),
                    equipmentInstanceStableId.ToString(),
                    definitionStableId.ToString());
            EquipmentGenerationResultV1 result =
                generator.GenerateEquipment(
                    EquipmentGenerationRequestV1.Create(
                        operationStableId,
                        equipmentInstanceStableId,
                        policy,
                        catalog,
                        rewardRequest.Context,
                        seed,
                        rewardRequest.AlgorithmVersion));
            if (result == null
                || !result.IsSuccess
                || result.Equipment == null
                || result.Equipment.InstanceId
                    != equipmentInstanceStableId
                || result.Equipment.DefinitionId
                    != definitionStableId)
            {
                throw new InvalidOperationException(
                    "Exact equipment payload generation failed: "
                    + (result == null
                        ? "null-result"
                        : result.FailureReason));
            }
            return result.Equipment;
        }

        private EquipmentGenerationPolicyV1 BuildDefinitionLockedPolicy(
            EquipmentDefinition definition)
        {
            var qualities = new List<EquipmentQualityCandidateV1>();
            for (int index = 0;
                index < definition.QualityTiers.Count;
                index++)
            {
                qualities.Add(EquipmentQualityCandidateV1.Create(
                    definition.QualityTiers[index].QualityId,
                    0L,
                    1UL));
            }
            if (qualities.Count == 0)
                throw new InvalidOperationException(
                    "Equipment definition has no generation quality tiers: "
                    + definition.DefinitionId);

            var augments = new List<AugmentGenerationCandidateV1>();
            for (int index = 0;
                index < catalog.AugmentDefinitions.Count;
                index++)
            {
                augments.Add(AugmentGenerationCandidateV1.Create(
                    catalog.AugmentDefinitions[index].DefinitionId,
                    0,
                    100,
                    1UL));
            }
            int maximumAugmentSlots = Math.Min(
                definition.MaximumAugmentSlots,
                augments.Count);
            return EquipmentGenerationPolicyV1.Create(
                RewardApplicationCanonicalV1.DeriveStableId(
                    "terminaldropequipmentpolicy",
                    definition.DefinitionId.ToString(),
                    catalog.Fingerprint),
                new[]
                {
                    EquipmentGenerationCandidateV1.Create(
                        definition.DefinitionId,
                        0,
                        100,
                        0,
                        100,
                        Array.Empty<StableId>(),
                        1L,
                        definition.ItemLevelRange,
                        1d,
                        1d),
                },
                qualities,
                augments,
                0,
                maximumAugmentSlots,
                false,
                new SoftActivationCurveParameters(0.1, 10L, 10L),
                new ObsolescenceCurveParameters(100L, 100d, 1d));
        }

        private static StableId DeriveChildStableId(
            RewardOperationRequestV1 operation,
            RewardGrantV1 grant,
            long unitIndex)
        {
            return RewardApplicationCanonicalV1.DeriveStableId(
                "terminaldropchild",
                operation.SourceOperationStableId.ToString(),
                grant.GrantStableId.ToString(),
                ((int)grant.Kind).ToString(
                    CultureInfo.InvariantCulture),
                grant.ContentStableId.ToString(),
                unitIndex.ToString(CultureInfo.InvariantCulture));
        }

        private static ulong DeriveEquipmentSeed(
            ulong rootSeed,
            string operationFingerprint,
            StableId equipmentInstanceStableId,
            long unitIndex)
        {
            ulong ordinal = RewardGenerationFingerprintV1.StableOrdinal(
                StableId.Create(
                    "terminal-drop-equipment-seed",
                    equipmentInstanceStableId.ToString()
                    + "-"
                    + unitIndex.ToString(CultureInfo.InvariantCulture)));
            return DeterministicSeedDerivationV1.Derive(
                rootSeed,
                operationFingerprint,
                ordinal);
        }
    }

    /// <summary>
    /// Existing GEN executor plus exact run-local equipment payload retention. A retention
    /// failure rejects the terminal generation before any pickup is admitted.
    /// </summary>
    public sealed class RetainingTerminalDropRewardGenerationExecutor :
        IRewardGenerationExecutorV1
    {
        private readonly RewardGenerationServiceV1 generator;
        private readonly RetainedTerminalDropEquipmentPayloadAuthority payloads;

        public RetainingTerminalDropRewardGenerationExecutor(
            RewardGenerationServiceV1 generator,
            RetainedTerminalDropEquipmentPayloadAuthority payloads)
        {
            this.generator = generator
                ?? throw new ArgumentNullException(nameof(generator));
            this.payloads = payloads
                ?? throw new ArgumentNullException(nameof(payloads));
        }

        public RewardGenerationResultEnvelopeV1 Generate(
            RewardGenerationRequestV1 request)
        {
            RewardGenerationResultEnvelopeV1 envelope =
                generator.GenerateReward(request);
            payloads.RetainExactPayloads(request, envelope);
            return envelope;
        }
    }
}

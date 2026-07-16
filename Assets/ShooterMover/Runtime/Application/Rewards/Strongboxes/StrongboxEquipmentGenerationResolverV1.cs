using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Supplies the authored equipment-generation policy, catalog, and strongbox
    /// power budget for a strongbox tier. Production content may use any number of
    /// tiers without a runtime enum or switch.
    /// </summary>
    public interface IStrongboxEquipmentGenerationDefinitionProviderV1
    {
        bool TryGet(
            StableId tierStableId,
            out StrongboxPowerBudgetPolicyV1 powerBudgetPolicy,
            out EquipmentGenerationPolicyV1 equipmentGenerationPolicy,
            out EquipmentCatalog equipmentCatalog);
    }

    /// <summary>
    /// Canonical data binding between one strongbox tier and the accepted shared
    /// equipment generator. The tier owns only its power-budget inputs; equipment
    /// definitions, quality and augment compatibility remain owned by GEN/EQP.
    /// </summary>
    public sealed class StrongboxEquipmentGenerationDefinitionV1 :
        IComparable<StrongboxEquipmentGenerationDefinitionV1>,
        IEquatable<StrongboxEquipmentGenerationDefinitionV1>
    {
        private readonly string canonicalText;

        public StrongboxEquipmentGenerationDefinitionV1(
            StableId tierStableId,
            StrongboxPowerBudgetPolicyV1 powerBudgetPolicy,
            EquipmentGenerationPolicyV1 equipmentGenerationPolicy,
            EquipmentCatalog equipmentCatalog)
        {
            TierStableId = tierStableId ?? throw new ArgumentNullException(nameof(tierStableId));
            PowerBudgetPolicy = powerBudgetPolicy ?? throw new ArgumentNullException(nameof(powerBudgetPolicy));
            EquipmentGenerationPolicy = equipmentGenerationPolicy
                ?? throw new ArgumentNullException(nameof(equipmentGenerationPolicy));
            EquipmentCatalog = equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog));

            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "tier_stable_id", TierStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "power_budget_policy", PowerBudgetPolicy.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(
                builder,
                "equipment_generation_policy",
                EquipmentGenerationPolicy.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "equipment_catalog_fingerprint", EquipmentCatalog.Fingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId TierStableId { get; }
        public StrongboxPowerBudgetPolicyV1 PowerBudgetPolicy { get; }
        public EquipmentGenerationPolicyV1 EquipmentGenerationPolicy { get; }
        public EquipmentCatalog EquipmentCatalog { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public int CompareTo(StrongboxEquipmentGenerationDefinitionV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : TierStableId.CompareTo(other.TierStableId);
        }

        public bool Equals(StrongboxEquipmentGenerationDefinitionV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StrongboxEquipmentGenerationDefinitionV1);
        }

        public override int GetHashCode()
        {
            return StrongboxCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    /// <summary>
    /// Immutable enum-free provider for all authored strongbox equipment tiers.
    /// </summary>
    public sealed class StrongboxEquipmentGenerationDefinitionCatalogV1 :
        IStrongboxEquipmentGenerationDefinitionProviderV1
    {
        private readonly ReadOnlyCollection<StrongboxEquipmentGenerationDefinitionV1> definitions;
        private readonly Dictionary<StableId, StrongboxEquipmentGenerationDefinitionV1> byTier;

        public StrongboxEquipmentGenerationDefinitionCatalogV1(
            IEnumerable<StrongboxEquipmentGenerationDefinitionV1> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<StrongboxEquipmentGenerationDefinitionV1> copy =
                new List<StrongboxEquipmentGenerationDefinitionV1>();
            byTier = new Dictionary<StableId, StrongboxEquipmentGenerationDefinitionV1>();
            foreach (StrongboxEquipmentGenerationDefinitionV1 definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Strongbox equipment definitions must not contain null entries.",
                        nameof(definitions));
                }

                if (byTier.ContainsKey(definition.TierStableId))
                {
                    throw new ArgumentException(
                        "Duplicate strongbox equipment tier " + definition.TierStableId + ".",
                        nameof(definitions));
                }

                byTier.Add(definition.TierStableId, definition);
                copy.Add(definition);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one strongbox equipment definition is required.",
                    nameof(definitions));
            }

            copy.Sort();
            this.definitions = new ReadOnlyCollection<StrongboxEquipmentGenerationDefinitionV1>(copy);
            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(
                builder,
                "definition_count",
                copy.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < copy.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(
                    builder,
                    "definition_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    copy[index].ToCanonicalString());
            }

            Fingerprint = StrongboxCanonicalV1.Fingerprint(builder.ToString());
        }

        public IReadOnlyList<StrongboxEquipmentGenerationDefinitionV1> Definitions
        {
            get { return definitions; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId tierStableId,
            out StrongboxPowerBudgetPolicyV1 powerBudgetPolicy,
            out EquipmentGenerationPolicyV1 equipmentGenerationPolicy,
            out EquipmentCatalog equipmentCatalog)
        {
            StrongboxEquipmentGenerationDefinitionV1 definition = null;
            bool found = tierStableId != null && byTier.TryGetValue(tierStableId, out definition);
            powerBudgetPolicy = found ? definition.PowerBudgetPolicy : null;
            equipmentGenerationPolicy = found ? definition.EquipmentGenerationPolicy : null;
            equipmentCatalog = found ? definition.EquipmentCatalog : null;
            return found;
        }
    }

    /// <summary>
    /// Concrete BOX-to-GEN equipment resolver. Each reward unit is generated as an
    /// independent slot from the complete eligible candidate set. Candidate
    /// definitions are therefore sampled with replacement: two slots may select
    /// the same weapon definition, while deterministic instance IDs remain unique.
    /// </summary>
    public sealed class StrongboxEquipmentGenerationResolverV1 : IStrongboxEquipmentPayloadResolverV1
    {
        private static readonly StableId EquipmentSelectionSeedPurposeId =
            StableId.Parse("strongbox-rng.equipment-selection-v1");
        private static readonly StableId EquipmentFinalizationSeedPurposeId =
            StableId.Parse("strongbox-rng.equipment-finalization-v1");

        private readonly RewardGenerationServiceV1 generator;
        private readonly IStrongboxEquipmentGenerationDefinitionProviderV1 definitionProvider;

        public StrongboxEquipmentGenerationResolverV1(
            RewardGenerationServiceV1 generator,
            IStrongboxEquipmentGenerationDefinitionProviderV1 definitionProvider)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.definitionProvider = definitionProvider ?? throw new ArgumentNullException(nameof(definitionProvider));
        }

        public bool TryResolve(
            StrongboxDefinitionV1 definition,
            StrongboxInstanceContextV1 boxContext,
            RewardOperationRequestV1 operation,
            RewardGrantV1 equipmentGrant,
            out IReadOnlyList<EquipmentInstance> equipmentInstances,
            out string rejectionCode)
        {
            equipmentInstances = Array.Empty<EquipmentInstance>();
            rejectionCode = null;
            if (definition == null || boxContext == null || operation == null || equipmentGrant == null)
            {
                rejectionCode = "strongbox-equipment-input-null";
                return false;
            }

            if (definition.TierStableId != boxContext.TierStableId)
            {
                rejectionCode = "strongbox-equipment-tier-context-mismatch";
                return false;
            }

            if (equipmentGrant.Kind != RewardGrantKindV1.EquipmentReference || equipmentGrant.Quantity < 1L)
            {
                rejectionCode = "strongbox-equipment-grant-invalid";
                return false;
            }

            if (equipmentGrant.Quantity > int.MaxValue)
            {
                rejectionCode = "strongbox-equipment-grant-count-too-large";
                return false;
            }

            StrongboxPowerBudgetPolicyV1 powerBudget;
            EquipmentGenerationPolicyV1 basePolicy;
            EquipmentCatalog catalog;
            if (!definitionProvider.TryGet(
                definition.TierStableId,
                out powerBudget,
                out basePolicy,
                out catalog)
                || powerBudget == null
                || basePolicy == null
                || catalog == null)
            {
                rejectionCode = "strongbox-equipment-definition-unavailable";
                return false;
            }

            if (basePolicy.PolicyId != definition.CompatibleGenerationPolicyStableId)
            {
                rejectionCode = "strongbox-equipment-policy-identity-mismatch";
                return false;
            }

            List<EquipmentInstance> generated = new List<EquipmentInstance>((int)equipmentGrant.Quantity);
            for (int slotIndex = 0; slotIndex < (int)equipmentGrant.Quantity; slotIndex++)
            {
                ulong slotOrdinal = (ulong)slotIndex;
                StrongboxItemLevelRollV1 itemLevelRoll;
                try
                {
                    itemLevelRoll = powerBudget.RollItemLevel(
                        boxContext.ProgressionContext.CharacterLevel,
                        boxContext.RootSeed,
                        boxContext.AlgorithmVersion,
                        slotOrdinal);
                }
                catch (Exception exception)
                {
                    rejectionCode = "strongbox-item-level-roll-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }

                ProgressionContext generationContext;
                try
                {
                    generationContext = ProgressionContext.Create(
                        itemLevelRoll.MeanItemLevel,
                        boxContext.ProgressionContext.RegionLevel,
                        boxContext.ProgressionContext.DifficultyId,
                        boxContext.ProgressionContext.DifficultyValue,
                        boxContext.ProgressionContext.ProgressionTags);
                }
                catch (Exception exception)
                {
                    rejectionCode = "strongbox-generation-context-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }

                EquipmentGenerationPolicyV1 selectionPolicy;
                if (!TryCreateSelectionPolicy(
                    basePolicy,
                    catalog,
                    boxContext,
                    itemLevelRoll,
                    out selectionPolicy,
                    out rejectionCode))
                {
                    return false;
                }

                StableId selectionOperationId = StrongboxCanonicalV1.DeriveId(
                    "boxequipmentselectionop",
                    operation.SourceOperationStableId.ToString(),
                    equipmentGrant.GrantStableId.ToString(),
                    slotIndex.ToString(CultureInfo.InvariantCulture),
                    itemLevelRoll.Fingerprint);
                StableId selectionInstanceId = StrongboxCanonicalV1.DeriveId(
                    "boxequipmentselection",
                    selectionOperationId.ToString());
                DeterministicRandom selectionSeedStream = DeterministicRandom.CreateSubstream(
                    boxContext.RootSeed,
                    boxContext.AlgorithmVersion,
                    EquipmentSelectionSeedPurposeId,
                    slotOrdinal);
                EquipmentGenerationResultV1 selectionResult;
                try
                {
                    selectionResult = generator.GenerateEquipment(
                        EquipmentGenerationRequestV1.Create(
                            selectionOperationId,
                            selectionInstanceId,
                            selectionPolicy,
                            catalog,
                            generationContext,
                            selectionSeedStream.StreamSeed,
                            boxContext.AlgorithmVersion));
                }
                catch (Exception exception)
                {
                    rejectionCode = "strongbox-equipment-selection-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }

                if (selectionResult == null || !selectionResult.IsSuccess || selectionResult.Equipment == null)
                {
                    rejectionCode = selectionResult == null
                        ? "strongbox-equipment-selection-result-null"
                        : "strongbox-equipment-selection-rejected-" + selectionResult.FailureReason;
                    return false;
                }

                EquipmentDefinition selectedDefinition = catalog.FindEquipmentDefinition(
                    selectionResult.Equipment.DefinitionId);
                if (selectedDefinition == null)
                {
                    rejectionCode = "strongbox-selected-equipment-definition-missing";
                    return false;
                }

                StrongboxEquipmentRollPlanV1 rollPlan;
                try
                {
                    rollPlan = powerBudget.RollAugmentSlots(
                        itemLevelRoll,
                        selectedDefinition.DefinitionId,
                        selectedDefinition.MaximumAugmentSlots,
                        boxContext.RootSeed,
                        boxContext.AlgorithmVersion);
                }
                catch (Exception exception)
                {
                    rejectionCode = "strongbox-augment-slot-roll-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }

                EquipmentGenerationPolicyV1 finalPolicy;
                if (!TryCreateFinalPolicy(
                    basePolicy,
                    catalog,
                    boxContext,
                    selectionResult.Equipment,
                    rollPlan,
                    out finalPolicy,
                    out rejectionCode))
                {
                    return false;
                }

                StableId equipmentInstanceId = StrongboxCanonicalV1.DeriveId(
                    "boxequipment",
                    operation.SourceOperationStableId.ToString(),
                    equipmentGrant.GrantStableId.ToString(),
                    slotIndex.ToString(CultureInfo.InvariantCulture));
                StableId equipmentOperationId = StrongboxCanonicalV1.DeriveId(
                    "boxequipmentop",
                    operation.SourceOperationStableId.ToString(),
                    equipmentGrant.GrantStableId.ToString(),
                    slotIndex.ToString(CultureInfo.InvariantCulture),
                    rollPlan.Fingerprint);
                DeterministicRandom finalSeedStream = DeterministicRandom.CreateSubstream(
                    boxContext.RootSeed,
                    boxContext.AlgorithmVersion,
                    EquipmentFinalizationSeedPurposeId,
                    slotOrdinal);

                EquipmentGenerationResultV1 result;
                try
                {
                    result = generator.GenerateEquipment(
                        EquipmentGenerationRequestV1.Create(
                            equipmentOperationId,
                            equipmentInstanceId,
                            finalPolicy,
                            catalog,
                            generationContext,
                            finalSeedStream.StreamSeed,
                            boxContext.AlgorithmVersion));
                }
                catch (Exception exception)
                {
                    rejectionCode = "strongbox-equipment-finalization-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }

                if (result == null || !result.IsSuccess || result.Equipment == null)
                {
                    rejectionCode = result == null
                        ? "strongbox-equipment-finalization-result-null"
                        : "strongbox-equipment-finalization-rejected-" + result.FailureReason;
                    return false;
                }

                if (result.Equipment.DefinitionId != selectionResult.Equipment.DefinitionId
                    || result.Equipment.QualityId != selectionResult.Equipment.QualityId)
                {
                    rejectionCode = "strongbox-equipment-selection-drift";
                    return false;
                }

                if (result.Equipment.ItemLevel != rollPlan.TargetItemLevel)
                {
                    rejectionCode = "strongbox-equipment-item-level-drift";
                    return false;
                }

                if (result.Equipment.Augments.Count != rollPlan.RolledAugmentSlots)
                {
                    rejectionCode = "strongbox-equipment-augment-slot-drift";
                    return false;
                }

                generated.Add(result.Equipment);
            }

            equipmentInstances = generated.AsReadOnly();
            return true;
        }

        private static bool TryCreateSelectionPolicy(
            EquipmentGenerationPolicyV1 basePolicy,
            EquipmentCatalog catalog,
            StrongboxInstanceContextV1 boxContext,
            StrongboxItemLevelRollV1 itemLevelRoll,
            out EquipmentGenerationPolicyV1 selectionPolicy,
            out string rejectionCode)
        {
            List<EquipmentGenerationCandidateV1> candidates = BuildFixedLevelCandidates(
                basePolicy,
                catalog,
                itemLevelRoll.TargetItemLevel,
                null);
            if (candidates.Count == 0)
            {
                selectionPolicy = null;
                rejectionCode = "strongbox-no-equipment-at-rolled-item-level-"
                    + itemLevelRoll.TargetItemLevel.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            StableId selectionPolicyId = StrongboxCanonicalV1.DeriveId(
                "boxequipmentselectionpolicy",
                basePolicy.PolicyId.ToString(),
                boxContext.TierStableId.ToString(),
                itemLevelRoll.Fingerprint);
            selectionPolicy = EquipmentGenerationPolicyV1.Create(
                selectionPolicyId,
                candidates,
                basePolicy.QualityCandidates,
                basePolicy.AugmentCandidates,
                0,
                0,
                true,
                basePolicy.Activation,
                basePolicy.Obsolescence);
            rejectionCode = null;
            return true;
        }

        private static bool TryCreateFinalPolicy(
            EquipmentGenerationPolicyV1 basePolicy,
            EquipmentCatalog catalog,
            StrongboxInstanceContextV1 boxContext,
            EquipmentInstance selectedEquipment,
            StrongboxEquipmentRollPlanV1 rollPlan,
            out EquipmentGenerationPolicyV1 finalPolicy,
            out string rejectionCode)
        {
            List<EquipmentGenerationCandidateV1> candidates = BuildFixedLevelCandidates(
                basePolicy,
                catalog,
                rollPlan.TargetItemLevel,
                selectedEquipment.DefinitionId);
            if (candidates.Count != 1)
            {
                finalPolicy = null;
                rejectionCode = "strongbox-selected-equipment-candidate-unavailable";
                return false;
            }

            EquipmentQualityCandidateV1 selectedQuality = null;
            for (int index = 0; index < basePolicy.QualityCandidates.Count; index++)
            {
                if (basePolicy.QualityCandidates[index].QualityId == selectedEquipment.QualityId)
                {
                    selectedQuality = basePolicy.QualityCandidates[index];
                    break;
                }
            }

            if (selectedQuality == null)
            {
                finalPolicy = null;
                rejectionCode = "strongbox-selected-equipment-quality-unavailable";
                return false;
            }

            StableId finalPolicyId = StrongboxCanonicalV1.DeriveId(
                "boxequipmentpolicy",
                basePolicy.PolicyId.ToString(),
                boxContext.TierStableId.ToString(),
                rollPlan.Fingerprint);
            finalPolicy = EquipmentGenerationPolicyV1.Create(
                finalPolicyId,
                candidates,
                new[]
                {
                    EquipmentQualityCandidateV1.Create(
                        selectedQuality.QualityId,
                        selectedQuality.NominalAvailabilityLevel,
                        selectedQuality.Weight)
                },
                basePolicy.AugmentCandidates,
                rollPlan.RolledAugmentSlots,
                rollPlan.RolledAugmentSlots,
                true,
                basePolicy.Activation,
                basePolicy.Obsolescence);
            rejectionCode = null;
            return true;
        }

        private static List<EquipmentGenerationCandidateV1> BuildFixedLevelCandidates(
            EquipmentGenerationPolicyV1 basePolicy,
            EquipmentCatalog catalog,
            int targetItemLevel,
            StableId requiredDefinitionId)
        {
            List<EquipmentGenerationCandidateV1> candidates = new List<EquipmentGenerationCandidateV1>();
            for (int index = 0; index < basePolicy.EquipmentCandidates.Count; index++)
            {
                EquipmentGenerationCandidateV1 candidate = basePolicy.EquipmentCandidates[index];
                if (requiredDefinitionId != null && candidate.EquipmentDefinitionId != requiredDefinitionId)
                {
                    continue;
                }

                EquipmentDefinition equipmentDefinition = catalog.FindEquipmentDefinition(
                    candidate.EquipmentDefinitionId);
                if (equipmentDefinition == null
                    || !candidate.GeneratedItemLevelRange.Contains(targetItemLevel)
                    || !equipmentDefinition.ItemLevelRange.Contains(targetItemLevel))
                {
                    continue;
                }

                candidates.Add(EquipmentGenerationCandidateV1.Create(
                    candidate.EquipmentDefinitionId,
                    candidate.MinimumCharacterLevel,
                    candidate.MaximumCharacterLevel,
                    candidate.MinimumRegionLevel,
                    candidate.MaximumRegionLevel,
                    candidate.RequiredProgressionTags,
                    candidate.NominalActivationLevel,
                    InclusiveIntRange.Create(targetItemLevel, targetItemLevel),
                    candidate.BaseWeight,
                    candidate.SourceBias));
            }

            return candidates;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;

namespace ShooterMover.Domain.Rewards.Generation
{
    public enum RewardGenerationStatusV1
    {
        Generated = 1,
        ExplicitNoDrop = 2,
        NoEligibleCandidate = 3,
        ImpossiblePolicy = 4,
    }

    public enum RewardGenerationTraceDecisionV1
    {
        Eligibility = 1,
        WeightedSelection = 2,
        IndependentChance = 3,
        ExclusiveSelection = 4,
        Quantity = 5,
        ScalingInput = 6,
        Quality = 7,
        SlotCount = 8,
        AugmentSelection = 9,
        AugmentTier = 10,
        AugmentLevel = 11,
        ExplicitNoDrop = 12,
        Validation = 13,
        GrantProduced = 14,
    }

    public sealed class EquipmentGenerationCandidateV1 : IComparable<EquipmentGenerationCandidateV1>
    {
        private readonly ReadOnlyCollection<StableId> requiredProgressionTags;

        private EquipmentGenerationCandidateV1(
            StableId equipmentDefinitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            int minimumRegionLevel,
            int maximumRegionLevel,
            IEnumerable<StableId> requiredProgressionTags,
            long nominalActivationLevel,
            InclusiveIntRange generatedItemLevelRange,
            double baseWeight,
            double sourceBias)
        {
            EquipmentDefinitionId = RequireId(equipmentDefinitionId, nameof(equipmentDefinitionId));
            ValidateLevelRange(minimumCharacterLevel, maximumCharacterLevel, nameof(minimumCharacterLevel));
            ValidateLevelRange(minimumRegionLevel, maximumRegionLevel, nameof(minimumRegionLevel));
            if (nominalActivationLevel < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(nominalActivationLevel));
            }

            if (generatedItemLevelRange == null || !generatedItemLevelRange.IsOrderedPositive)
            {
                throw new ArgumentException("Generated item-level range must be positive and ordered.", nameof(generatedItemLevelRange));
            }

            if (double.IsNaN(baseWeight) || double.IsInfinity(baseWeight) || baseWeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseWeight));
            }

            if (double.IsNaN(sourceBias) || double.IsInfinity(sourceBias) || sourceBias <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceBias));
            }

            MinimumCharacterLevel = minimumCharacterLevel;
            MaximumCharacterLevel = maximumCharacterLevel;
            MinimumRegionLevel = minimumRegionLevel;
            MaximumRegionLevel = maximumRegionLevel;
            this.requiredProgressionTags = CopyIds(requiredProgressionTags, nameof(requiredProgressionTags));
            NominalActivationLevel = nominalActivationLevel;
            GeneratedItemLevelRange = generatedItemLevelRange;
            BaseWeight = baseWeight;
            SourceBias = sourceBias;
        }

        public StableId EquipmentDefinitionId { get; }
        public int MinimumCharacterLevel { get; }
        public int MaximumCharacterLevel { get; }
        public int MinimumRegionLevel { get; }
        public int MaximumRegionLevel { get; }
        public IReadOnlyList<StableId> RequiredProgressionTags { get { return requiredProgressionTags; } }
        public long NominalActivationLevel { get; }
        public InclusiveIntRange GeneratedItemLevelRange { get; }
        public double BaseWeight { get; }
        public double SourceBias { get; }

        public static EquipmentGenerationCandidateV1 Create(
            StableId equipmentDefinitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            int minimumRegionLevel,
            int maximumRegionLevel,
            IEnumerable<StableId> requiredProgressionTags,
            long nominalActivationLevel,
            InclusiveIntRange generatedItemLevelRange,
            double baseWeight,
            double sourceBias)
        {
            return new EquipmentGenerationCandidateV1(
                equipmentDefinitionId,
                minimumCharacterLevel,
                maximumCharacterLevel,
                minimumRegionLevel,
                maximumRegionLevel,
                requiredProgressionTags,
                nominalActivationLevel,
                generatedItemLevelRange,
                baseWeight,
                sourceBias);
        }

        public bool IsEligible(ProgressionContext context, EquipmentCatalog catalog)
        {
            if (context == null || catalog == null)
            {
                return false;
            }

            if (context.CharacterLevel < MinimumCharacterLevel || context.CharacterLevel > MaximumCharacterLevel
                || context.RegionLevel < MinimumRegionLevel || context.RegionLevel > MaximumRegionLevel)
            {
                return false;
            }

            for (int index = 0; index < requiredProgressionTags.Count; index++)
            {
                bool found = false;
                for (int tagIndex = 0; tagIndex < context.ProgressionTags.Count; tagIndex++)
                {
                    if (requiredProgressionTags[index] == context.ProgressionTags[tagIndex])
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            EquipmentDefinition definition = catalog.FindEquipmentDefinition(EquipmentDefinitionId);
            if (definition == null || definition.ItemLevelRange == null)
            {
                return false;
            }

            return Math.Max(definition.ItemLevelRange.Minimum, GeneratedItemLevelRange.Minimum)
                <= Math.Min(definition.ItemLevelRange.Maximum, GeneratedItemLevelRange.Maximum);
        }

        public double EvaluateWeight(
            ProgressionContext context,
            SoftActivationCurveParameters activation,
            ObsolescenceCurveParameters obsolescence)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            ItemEligibilityCurveParameters parameters = new ItemEligibilityCurveParameters(
                activation,
                obsolescence,
                BaseWeight,
                SourceBias);
            return ProgressionCurveMath.EvaluateItemEligibilityWeight(
                context.CharacterLevel,
                GeneratedItemLevelRange.Minimum,
                NominalActivationLevel,
                parameters);
        }

        public int CompareTo(EquipmentGenerationCandidateV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : EquipmentDefinitionId.CompareTo(other.EquipmentDefinitionId);
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("equipment_definition_id=").Append(EquipmentDefinitionId)
                .Append("\ncharacter_levels=").Append(MinimumCharacterLevel.ToString(CultureInfo.InvariantCulture))
                .Append("..").Append(MaximumCharacterLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\nregion_levels=").Append(MinimumRegionLevel.ToString(CultureInfo.InvariantCulture))
                .Append("..").Append(MaximumRegionLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\nnominal_activation_level=").Append(NominalActivationLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\ngenerated_item_levels=").Append(GeneratedItemLevelRange.ToCanonicalString())
                .Append("\nbase_weight=").Append(BaseWeight.ToString("R", CultureInfo.InvariantCulture))
                .Append("\nsource_bias=").Append(SourceBias.ToString("R", CultureInfo.InvariantCulture))
                .Append("\nrequired_tag_count=").Append(requiredProgressionTags.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < requiredProgressionTags.Count; index++)
            {
                builder.Append("\nrequired_tag=").Append(requiredProgressionTags[index]);
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        private static void ValidateLevelRange(int minimum, int maximum, string parameterName)
        {
            if (minimum < 0 || maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        internal static StableId RequireId(StableId value, string parameterName)
        {
            return value ?? throw new ArgumentNullException(parameterName);
        }

        internal static ReadOnlyCollection<StableId> CopyIds(IEnumerable<StableId> source, string parameterName)
        {
            if (source == null)
            {
                return new ReadOnlyCollection<StableId>(new List<StableId>());
            }

            SortedSet<StableId> ids = new SortedSet<StableId>();
            foreach (StableId value in source)
            {
                if (value == null)
                {
                    throw new ArgumentException(parameterName + " must not contain null entries.", parameterName);
                }

                ids.Add(value);
            }

            return new ReadOnlyCollection<StableId>(new List<StableId>(ids));
        }
    }

    public sealed class EquipmentQualityCandidateV1 : IComparable<EquipmentQualityCandidateV1>
    {
        private EquipmentQualityCandidateV1(StableId qualityId, long nominalAvailabilityLevel, ulong weight)
        {
            QualityId = EquipmentGenerationCandidateV1.RequireId(qualityId, nameof(qualityId));
            if (nominalAvailabilityLevel < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(nominalAvailabilityLevel));
            }

            if (weight == 0UL || weight > long.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            NominalAvailabilityLevel = nominalAvailabilityLevel;
            Weight = weight;
        }

        public StableId QualityId { get; }
        public long NominalAvailabilityLevel { get; }
        public ulong Weight { get; }

        public static EquipmentQualityCandidateV1 Create(StableId qualityId, long nominalAvailabilityLevel, ulong weight)
        {
            return new EquipmentQualityCandidateV1(qualityId, nominalAvailabilityLevel, weight);
        }

        public int CompareTo(EquipmentQualityCandidateV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : QualityId.CompareTo(other.QualityId);
        }

        public string ToCanonicalString()
        {
            return "quality_id=" + QualityId
                + "\nnominal_availability_level=" + NominalAvailabilityLevel.ToString(CultureInfo.InvariantCulture)
                + "\nweight=" + Weight.ToString(CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class AugmentGenerationCandidateV1 : IComparable<AugmentGenerationCandidateV1>
    {
        private AugmentGenerationCandidateV1(
            StableId augmentDefinitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            ulong weight)
        {
            AugmentDefinitionId = EquipmentGenerationCandidateV1.RequireId(augmentDefinitionId, nameof(augmentDefinitionId));
            if (minimumCharacterLevel < 0 || maximumCharacterLevel < minimumCharacterLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumCharacterLevel));
            }

            if (weight == 0UL || weight > long.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            MinimumCharacterLevel = minimumCharacterLevel;
            MaximumCharacterLevel = maximumCharacterLevel;
            Weight = weight;
        }

        public StableId AugmentDefinitionId { get; }
        public int MinimumCharacterLevel { get; }
        public int MaximumCharacterLevel { get; }
        public ulong Weight { get; }

        public static AugmentGenerationCandidateV1 Create(
            StableId augmentDefinitionId,
            int minimumCharacterLevel,
            int maximumCharacterLevel,
            ulong weight)
        {
            return new AugmentGenerationCandidateV1(
                augmentDefinitionId,
                minimumCharacterLevel,
                maximumCharacterLevel,
                weight);
        }

        public bool IsLevelEligible(ProgressionContext context)
        {
            return context != null
                && context.CharacterLevel >= MinimumCharacterLevel
                && context.CharacterLevel <= MaximumCharacterLevel;
        }

        public int CompareTo(AugmentGenerationCandidateV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : AugmentDefinitionId.CompareTo(other.AugmentDefinitionId);
        }

        public string ToCanonicalString()
        {
            return "augment_definition_id=" + AugmentDefinitionId
                + "\ncharacter_levels=" + MinimumCharacterLevel.ToString(CultureInfo.InvariantCulture)
                + ".." + MaximumCharacterLevel.ToString(CultureInfo.InvariantCulture)
                + "\nweight=" + Weight.ToString(CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public sealed class EquipmentGenerationPolicyV1
    {
        private readonly ReadOnlyCollection<EquipmentGenerationCandidateV1> equipmentCandidates;
        private readonly ReadOnlyCollection<EquipmentQualityCandidateV1> qualityCandidates;
        private readonly ReadOnlyCollection<AugmentGenerationCandidateV1> augmentCandidates;
        private readonly string canonicalText;

        private EquipmentGenerationPolicyV1(
            StableId policyId,
            IEnumerable<EquipmentGenerationCandidateV1> equipmentCandidates,
            IEnumerable<EquipmentQualityCandidateV1> qualityCandidates,
            IEnumerable<AugmentGenerationCandidateV1> augmentCandidates,
            int minimumAugmentSlots,
            int maximumAugmentSlots,
            bool requireExactSlotCount,
            SoftActivationCurveParameters activation,
            ObsolescenceCurveParameters obsolescence)
        {
            PolicyId = EquipmentGenerationCandidateV1.RequireId(policyId, nameof(policyId));
            this.equipmentCandidates = CopyUnique(equipmentCandidates, nameof(equipmentCandidates), delegate(EquipmentGenerationCandidateV1 value) { return value.EquipmentDefinitionId; });
            this.qualityCandidates = CopyUnique(qualityCandidates, nameof(qualityCandidates), delegate(EquipmentQualityCandidateV1 value) { return value.QualityId; });
            this.augmentCandidates = CopyUnique(augmentCandidates, nameof(augmentCandidates), delegate(AugmentGenerationCandidateV1 value) { return value.AugmentDefinitionId; });
            if (this.equipmentCandidates.Count == 0)
            {
                throw new ArgumentException("At least one equipment candidate is required.", nameof(equipmentCandidates));
            }

            if (this.qualityCandidates.Count == 0)
            {
                throw new ArgumentException("At least one quality candidate is required.", nameof(qualityCandidates));
            }

            if (minimumAugmentSlots < 0 || maximumAugmentSlots < minimumAugmentSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAugmentSlots));
            }

            MinimumAugmentSlots = minimumAugmentSlots;
            MaximumAugmentSlots = maximumAugmentSlots;
            RequireExactSlotCount = requireExactSlotCount;
            Activation = activation ?? throw new ArgumentNullException(nameof(activation));
            Obsolescence = obsolescence ?? throw new ArgumentNullException(nameof(obsolescence));
            canonicalText = BuildCanonicalText();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public StableId PolicyId { get; }
        public IReadOnlyList<EquipmentGenerationCandidateV1> EquipmentCandidates { get { return equipmentCandidates; } }
        public IReadOnlyList<EquipmentQualityCandidateV1> QualityCandidates { get { return qualityCandidates; } }
        public IReadOnlyList<AugmentGenerationCandidateV1> AugmentCandidates { get { return augmentCandidates; } }
        public int MinimumAugmentSlots { get; }
        public int MaximumAugmentSlots { get; }
        public bool RequireExactSlotCount { get; }
        public SoftActivationCurveParameters Activation { get; }
        public ObsolescenceCurveParameters Obsolescence { get; }
        public string Fingerprint { get; }

        public static EquipmentGenerationPolicyV1 Create(
            StableId policyId,
            IEnumerable<EquipmentGenerationCandidateV1> equipmentCandidates,
            IEnumerable<EquipmentQualityCandidateV1> qualityCandidates,
            IEnumerable<AugmentGenerationCandidateV1> augmentCandidates,
            int minimumAugmentSlots,
            int maximumAugmentSlots,
            bool requireExactSlotCount,
            SoftActivationCurveParameters activation,
            ObsolescenceCurveParameters obsolescence)
        {
            return new EquipmentGenerationPolicyV1(
                policyId,
                equipmentCandidates,
                qualityCandidates,
                augmentCandidates,
                minimumAugmentSlots,
                maximumAugmentSlots,
                requireExactSlotCount,
                activation,
                obsolescence);
        }

        public string ToCanonicalString() { return canonicalText; }

        public override string ToString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("policy_id=").Append(PolicyId)
                .Append("\nminimum_augment_slots=").Append(MinimumAugmentSlots.ToString(CultureInfo.InvariantCulture))
                .Append("\nmaximum_augment_slots=").Append(MaximumAugmentSlots.ToString(CultureInfo.InvariantCulture))
                .Append("\nrequire_exact_slot_count=").Append(RequireExactSlotCount ? "1" : "0")
                .Append("\nactivation=")
                .Append(Activation.EarlyTailWeight.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(Activation.EarlyTailLevels.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Activation.PostNominalActivationLevels.ToString(CultureInfo.InvariantCulture))
                .Append("\nobsolescence=")
                .Append(Obsolescence.DecayStartsAfterLevels.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Obsolescence.HalfLifeLevels.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(Obsolescence.MinimumRetention.ToString("R", CultureInfo.InvariantCulture));
            Append(builder, "equipment_candidate", equipmentCandidates);
            Append(builder, "quality_candidate", qualityCandidates);
            Append(builder, "augment_candidate", augmentCandidates);
            return builder.ToString();
        }

        private static void Append<T>(StringBuilder builder, string label, IReadOnlyList<T> values)
        {
            builder.Append('\n').Append(label).Append("_count=").Append(values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append('\n').Append(label).Append('_').Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append(":\n").Append(values[index]);
            }
        }

        private static ReadOnlyCollection<T> CopyUnique<T>(
            IEnumerable<T> source,
            string parameterName,
            Func<T, StableId> idSelector)
            where T : IComparable<T>
        {
            if (source == null)
            {
                return new ReadOnlyCollection<T>(new List<T>());
            }

            List<T> values = new List<T>();
            HashSet<StableId> ids = new HashSet<StableId>();
            foreach (T value in source)
            {
                if (ReferenceEquals(value, null))
                {
                    throw new ArgumentException(parameterName + " must not contain null entries.", parameterName);
                }

                StableId id = idSelector(value);
                if (!ids.Add(id))
                {
                    throw new ArgumentException(parameterName + " contains duplicate identity " + id + ".", parameterName);
                }

                values.Add(value);
            }

            values.Sort();
            return new ReadOnlyCollection<T>(values);
        }
    }

    public sealed class RewardGenerationTraceEntryV1 : IComparable<RewardGenerationTraceEntryV1>
    {
        public RewardGenerationTraceEntryV1(
            int ordinal,
            StableId stepId,
            StableId subjectId,
            RewardGenerationTraceDecisionV1 decision,
            StableId substreamPurposeId,
            ulong substreamOrdinal,
            ulong samplesConsumed,
            long inputValue,
            long outputValue,
            string detail)
        {
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            Ordinal = ordinal;
            StepId = EquipmentGenerationCandidateV1.RequireId(stepId, nameof(stepId));
            SubjectId = EquipmentGenerationCandidateV1.RequireId(subjectId, nameof(subjectId));
            if (!Enum.IsDefined(typeof(RewardGenerationTraceDecisionV1), decision))
            {
                throw new ArgumentOutOfRangeException(nameof(decision));
            }

            Decision = decision;
            SubstreamPurposeId = substreamPurposeId;
            SubstreamOrdinal = substreamOrdinal;
            SamplesConsumed = samplesConsumed;
            InputValue = inputValue;
            OutputValue = outputValue;
            Detail = detail ?? string.Empty;
        }

        public int Ordinal { get; }
        public StableId StepId { get; }
        public StableId SubjectId { get; }
        public RewardGenerationTraceDecisionV1 Decision { get; }
        public StableId SubstreamPurposeId { get; }
        public ulong SubstreamOrdinal { get; }
        public ulong SamplesConsumed { get; }
        public long InputValue { get; }
        public long OutputValue { get; }
        public string Detail { get; }

        public int CompareTo(RewardGenerationTraceEntryV1 other)
        {
            if (ReferenceEquals(other, null)) { return 1; }
            int comparison = Ordinal.CompareTo(other.Ordinal);
            return comparison != 0 ? comparison : StepId.CompareTo(other.StepId);
        }

        public string ToCanonicalString()
        {
            return "ordinal=" + Ordinal.ToString(CultureInfo.InvariantCulture)
                + "\nstep_id=" + StepId
                + "\nsubject_id=" + SubjectId
                + "\ndecision=" + ((int)Decision).ToString(CultureInfo.InvariantCulture)
                + "\nsubstream_purpose_id=" + (SubstreamPurposeId == null ? "none" : SubstreamPurposeId.ToString())
                + "\nsubstream_ordinal=" + SubstreamOrdinal.ToString(CultureInfo.InvariantCulture)
                + "\nsamples_consumed=" + SamplesConsumed.ToString(CultureInfo.InvariantCulture)
                + "\ninput_value=" + InputValue.ToString(CultureInfo.InvariantCulture)
                + "\noutput_value=" + OutputValue.ToString(CultureInfo.InvariantCulture)
                + "\ndetail=" + Detail.Replace("\r", string.Empty).Replace("\n", "\\n");
        }
    }

    public sealed class RewardGenerationTraceV1
    {
        private readonly ReadOnlyCollection<RewardGenerationTraceEntryV1> entries;
        private readonly string canonicalText;

        public RewardGenerationTraceV1(
            int algorithmVersion,
            ulong rootSeed,
            string contentFingerprint,
            string contextFingerprint,
            string resultFingerprint,
            IEnumerable<RewardGenerationTraceEntryV1> entries)
        {
            if (algorithmVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            }

            AlgorithmVersion = algorithmVersion;
            RootSeed = rootSeed;
            ContentFingerprint = RequireFingerprint(contentFingerprint, nameof(contentFingerprint));
            ContextFingerprint = RequireFingerprint(contextFingerprint, nameof(contextFingerprint));
            ResultFingerprint = RequireFingerprint(resultFingerprint, nameof(resultFingerprint));
            List<RewardGenerationTraceEntryV1> copy = new List<RewardGenerationTraceEntryV1>(entries ?? throw new ArgumentNullException(nameof(entries)));
            copy.Sort();
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null || copy[index].Ordinal != index)
                {
                    throw new ArgumentException("Trace entries must be non-null and use contiguous canonical ordinals.", nameof(entries));
                }
            }

            this.entries = new ReadOnlyCollection<RewardGenerationTraceEntryV1>(copy);
            canonicalText = BuildCanonicalText();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }

        public int AlgorithmVersion { get; }
        public ulong RootSeed { get; }
        public string ContentFingerprint { get; }
        public string ContextFingerprint { get; }
        public string ResultFingerprint { get; }
        public IReadOnlyList<RewardGenerationTraceEntryV1> Entries { get { return entries; } }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=reward-generator-trace-v1")
                .Append("\nalgorithm_version=").Append(AlgorithmVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\nroot_seed=").Append(RootSeed.ToString(CultureInfo.InvariantCulture))
                .Append("\ncontent_fingerprint=").Append(ContentFingerprint)
                .Append("\ncontext_fingerprint=").Append(ContextFingerprint)
                .Append("\nresult_fingerprint=").Append(ResultFingerprint)
                .Append("\nentry_count=").Append(entries.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < entries.Count; index++)
            {
                builder.Append("\nentry_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append(":\n").Append(entries[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        private static string RequireFingerprint(string value, string parameterName)
        {
            if (value == null || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected sha256 fingerprint.", parameterName);
            }

            for (int index = 7; index < value.Length; index++)
            {
                char current = value[index];
                if (!((current >= '0' && current <= '9') || (current >= 'a' && current <= 'f')))
                {
                    throw new ArgumentException("Expected lowercase hexadecimal sha256 fingerprint.", parameterName);
                }
            }

            return value;
        }
    }

    public static class RewardGenerationFingerprintV1
    {
        public static string Compute(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            byte[] digest;
            using (SHA256 algorithm = SHA256.Create())
            {
                digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonicalText));
            }

            StringBuilder builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static ulong StableOrdinal(StableId id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            unchecked
            {
                ulong hash = 14695981039346656037UL;
                string text = id.ToString();
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= (byte)text[index];
                    hash *= 1099511628211UL;
                }

                return hash;
            }
        }

        public static StableId DeriveStableId(string namespaceName, params string[] parts)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < parts.Length; index++)
            {
                if (index > 0) { builder.Append('|'); }
                builder.Append(parts[index] ?? "null");
            }

            string fingerprint = Compute(builder.ToString());
            return StableId.Create(namespaceName, fingerprint.Substring("sha256:".Length, 32));
        }
    }
}

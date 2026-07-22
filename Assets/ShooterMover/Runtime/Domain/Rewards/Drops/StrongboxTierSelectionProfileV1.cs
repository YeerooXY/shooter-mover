using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    public sealed class StrongboxTierWeightV1 : IComparable<StrongboxTierWeightV1>
    {
        public StrongboxTierWeightV1(StableId tierStableId, ulong weight) { TierStableId = tierStableId ?? throw new ArgumentNullException(nameof(tierStableId)); if (weight == 0UL) throw new ArgumentOutOfRangeException(nameof(weight)); Weight = weight; }
        public StableId TierStableId { get; }
        public ulong Weight { get; }
        public int CompareTo(StrongboxTierWeightV1 other) { return ReferenceEquals(other, null) ? 1 : TierStableId.CompareTo(other.TierStableId); }
        public string ToCanonicalString() { return TierStableId + ":" + Weight.ToString(CultureInfo.InvariantCulture); }
    }

    public sealed class StrongboxTierContextModifierV1 : IComparable<StrongboxTierContextModifierV1>
    {
        public StrongboxTierContextModifierV1(StableId contextStableId, StableId tierStableId, int multiplierPermille)
        {
            ContextStableId = contextStableId ?? throw new ArgumentNullException(nameof(contextStableId));
            TierStableId = tierStableId ?? throw new ArgumentNullException(nameof(tierStableId));
            if (multiplierPermille < 0) throw new ArgumentOutOfRangeException(nameof(multiplierPermille));
            MultiplierPermille = multiplierPermille;
        }
        public StableId ContextStableId { get; }
        public StableId TierStableId { get; }
        public int MultiplierPermille { get; }
        public int CompareTo(StrongboxTierContextModifierV1 other) { if (ReferenceEquals(other, null)) return 1; int context = ContextStableId.CompareTo(other.ContextStableId); return context != 0 ? context : TierStableId.CompareTo(other.TierStableId); }
        public string ToCanonicalString() { return ContextStableId + ":" + TierStableId + ":" + MultiplierPermille.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>Authored inspectable tier distribution plus difficulty, mode and event multipliers.</summary>
    public sealed class StrongboxTierSelectionProfileV1
    {
        private readonly ReadOnlyCollection<StrongboxTierWeightV1> baseWeights;
        private readonly ReadOnlyCollection<StrongboxTierContextModifierV1> modifiers;
        private readonly string canonicalText;
        public StrongboxTierSelectionProfileV1(StableId profileStableId, IEnumerable<StrongboxTierWeightV1> baseWeights, IEnumerable<StrongboxTierContextModifierV1> modifiers)
        {
            ProfileStableId = profileStableId ?? throw new ArgumentNullException(nameof(profileStableId));
            this.baseWeights = CopyWeights(baseWeights);
            this.modifiers = CopyModifiers(modifiers);
            var builder = new StringBuilder("schema=strongbox-tier-selection-profile-v1");
            builder.Append("\nprofile_id=").Append(ProfileStableId).Append("\nbase_weight_count=").Append(this.baseWeights.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.baseWeights.Count; index++) builder.Append("\nbase_weight_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append("=").Append(this.baseWeights[index].ToCanonicalString());
            builder.Append("\nmodifier_count=").Append(this.modifiers.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.modifiers.Count; index++) builder.Append("\nmodifier_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append("=").Append(this.modifiers[index].ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
        }
        public StableId ProfileStableId { get; }
        public IReadOnlyList<StrongboxTierWeightV1> BaseWeights { get { return baseWeights; } }
        public IReadOnlyList<StrongboxTierContextModifierV1> Modifiers { get { return modifiers; } }
        public string Fingerprint { get; }
        public IReadOnlyList<StrongboxTierWeightV1> Evaluate(IEnumerable<StableId> activeContextIds)
        {
            var active = new HashSet<StableId>();
            if (activeContextIds != null) foreach (StableId contextId in activeContextIds) if (contextId != null) active.Add(contextId);
            var output = new List<StrongboxTierWeightV1>(baseWeights.Count);
            for (int weightIndex = 0; weightIndex < baseWeights.Count; weightIndex++)
            {
                StrongboxTierWeightV1 authored = baseWeights[weightIndex];
                ulong effective = authored.Weight;
                for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
                {
                    StrongboxTierContextModifierV1 modifier = modifiers[modifierIndex];
                    if (modifier.TierStableId == authored.TierStableId && active.Contains(modifier.ContextStableId)) effective = checked(effective * checked((ulong)modifier.MultiplierPermille) / 1000UL);
                }
                if (effective > 0UL) output.Add(new StrongboxTierWeightV1(authored.TierStableId, effective));
            }
            if (output.Count == 0) throw new InvalidOperationException("Tier-selection modifiers removed every canonical tier.");
            return new ReadOnlyCollection<StrongboxTierWeightV1>(output);
        }
        public string ToCanonicalString() { return canonicalText; }
        private static ReadOnlyCollection<StrongboxTierWeightV1> CopyWeights(IEnumerable<StrongboxTierWeightV1> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new List<StrongboxTierWeightV1>(); var ids = new HashSet<StableId>();
            foreach (StrongboxTierWeightV1 weight in source) { if (weight == null || !ids.Add(weight.TierStableId)) throw new ArgumentException("Tier weights must be non-null and unique.", nameof(source)); copy.Add(weight); }
            copy.Sort(); if (copy.Count == 0) throw new ArgumentException("At least one tier weight is required.", nameof(source));
            return new ReadOnlyCollection<StrongboxTierWeightV1>(copy);
        }
        private static ReadOnlyCollection<StrongboxTierContextModifierV1> CopyModifiers(IEnumerable<StrongboxTierContextModifierV1> source)
        {
            var copy = new List<StrongboxTierContextModifierV1>(); var keys = new HashSet<string>(StringComparer.Ordinal);
            if (source != null) foreach (StrongboxTierContextModifierV1 modifier in source) { string key = modifier == null ? null : modifier.ContextStableId + "|" + modifier.TierStableId; if (modifier == null || !keys.Add(key)) throw new ArgumentException("Tier modifiers must be non-null and unique by context/tier.", nameof(source)); copy.Add(modifier); }
            copy.Sort(); return new ReadOnlyCollection<StrongboxTierContextModifierV1>(copy);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    public sealed class StrongboxTierWeight : IComparable<StrongboxTierWeight>
    {
        public StrongboxTierWeight(StableId tierStableId, ulong weight) { TierStableId = tierStableId ?? throw new ArgumentNullException(nameof(tierStableId)); if (weight == 0UL) throw new ArgumentOutOfRangeException(nameof(weight)); Weight = weight; }
        public StableId TierStableId { get; }
        public ulong Weight { get; }
        public int CompareTo(StrongboxTierWeight other) { return ReferenceEquals(other, null) ? 1 : TierStableId.CompareTo(other.TierStableId); }
        public string ToCanonicalString() { return TierStableId + ":" + Weight.ToString(CultureInfo.InvariantCulture); }
    }

    public sealed class StrongboxTierContextModifier : IComparable<StrongboxTierContextModifier>
    {
        public StrongboxTierContextModifier(StableId contextStableId, StableId tierStableId, int multiplierPermille)
        {
            ContextStableId = contextStableId ?? throw new ArgumentNullException(nameof(contextStableId));
            TierStableId = tierStableId ?? throw new ArgumentNullException(nameof(tierStableId));
            if (multiplierPermille < 0) throw new ArgumentOutOfRangeException(nameof(multiplierPermille));
            MultiplierPermille = multiplierPermille;
        }
        public StableId ContextStableId { get; }
        public StableId TierStableId { get; }
        public int MultiplierPermille { get; }
        public int CompareTo(StrongboxTierContextModifier other) { if (ReferenceEquals(other, null)) return 1; int context = ContextStableId.CompareTo(other.ContextStableId); return context != 0 ? context : TierStableId.CompareTo(other.TierStableId); }
        public string ToCanonicalString() { return ContextStableId + ":" + TierStableId + ":" + MultiplierPermille.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>Authored inspectable tier distribution plus difficulty, mode and event multipliers.</summary>
    public sealed class StrongboxTierSelectionProfile
    {
        private readonly ReadOnlyCollection<StrongboxTierWeight> baseWeights;
        private readonly ReadOnlyCollection<StrongboxTierContextModifier> modifiers;
        private readonly string canonicalText;
        public StrongboxTierSelectionProfile(StableId profileStableId, IEnumerable<StrongboxTierWeight> baseWeights, IEnumerable<StrongboxTierContextModifier> modifiers)
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
            Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }
        public StableId ProfileStableId { get; }
        public IReadOnlyList<StrongboxTierWeight> BaseWeights { get { return baseWeights; } }
        public IReadOnlyList<StrongboxTierContextModifier> Modifiers { get { return modifiers; } }
        public string Fingerprint { get; }
        public IReadOnlyList<StrongboxTierWeight> Evaluate(IEnumerable<StableId> activeContextIds)
        {
            var active = new HashSet<StableId>();
            if (activeContextIds != null) foreach (StableId contextId in activeContextIds) if (contextId != null) active.Add(contextId);
            var output = new List<StrongboxTierWeight>(baseWeights.Count);
            for (int weightIndex = 0; weightIndex < baseWeights.Count; weightIndex++)
            {
                StrongboxTierWeight authored = baseWeights[weightIndex];
                ulong effective = authored.Weight;
                for (int modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
                {
                    StrongboxTierContextModifier modifier = modifiers[modifierIndex];
                    if (modifier.TierStableId == authored.TierStableId && active.Contains(modifier.ContextStableId)) effective = checked(effective * checked((ulong)modifier.MultiplierPermille) / 1000UL);
                }
                if (effective > 0UL) output.Add(new StrongboxTierWeight(authored.TierStableId, effective));
            }
            if (output.Count == 0) throw new InvalidOperationException("Tier-selection modifiers removed every canonical tier.");
            return new ReadOnlyCollection<StrongboxTierWeight>(output);
        }
        public string ToCanonicalString() { return canonicalText; }
        private static ReadOnlyCollection<StrongboxTierWeight> CopyWeights(IEnumerable<StrongboxTierWeight> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new List<StrongboxTierWeight>(); var ids = new HashSet<StableId>();
            foreach (StrongboxTierWeight weight in source) { if (weight == null || !ids.Add(weight.TierStableId)) throw new ArgumentException("Tier weights must be non-null and unique.", nameof(source)); copy.Add(weight); }
            copy.Sort(); if (copy.Count == 0) throw new ArgumentException("At least one tier weight is required.", nameof(source));
            return new ReadOnlyCollection<StrongboxTierWeight>(copy);
        }
        private static ReadOnlyCollection<StrongboxTierContextModifier> CopyModifiers(IEnumerable<StrongboxTierContextModifier> source)
        {
            var copy = new List<StrongboxTierContextModifier>(); var keys = new HashSet<string>(StringComparer.Ordinal);
            if (source != null) foreach (StrongboxTierContextModifier modifier in source) { string key = modifier == null ? null : modifier.ContextStableId + "|" + modifier.TierStableId; if (modifier == null || !keys.Add(key)) throw new ArgumentException("Tier modifiers must be non-null and unique by context/tier.", nameof(source)); copy.Add(modifier); }
            copy.Sort(); return new ReadOnlyCollection<StrongboxTierContextModifier>(copy);
        }
    }
}

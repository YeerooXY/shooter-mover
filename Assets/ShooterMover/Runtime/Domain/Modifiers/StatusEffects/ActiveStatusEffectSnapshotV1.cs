using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Modifiers;

namespace ShooterMover.Domain.Modifiers.StatusEffects
{
    public sealed class ActiveStatusEffectSnapshotV1
    {
        public ActiveStatusEffectSnapshotV1(
            string effectId,
            string definitionFingerprint,
            StatusEffectStackingPolicyV1 stackingPolicy,
            string dispelCategoryId,
            IEnumerable<ActiveStatusEffectStackSnapshotV1> stacks)
        {
            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException(
                    "A status-effect definition identity is required.",
                    nameof(effectId));
            }
            if (string.IsNullOrWhiteSpace(definitionFingerprint))
            {
                throw new ArgumentException(
                    "A status-effect definition fingerprint is required.",
                    nameof(definitionFingerprint));
            }
            if (!Enum.IsDefined(
                typeof(StatusEffectStackingPolicyV1),
                stackingPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(stackingPolicy));
            }
            if (string.IsNullOrWhiteSpace(dispelCategoryId))
            {
                throw new ArgumentException(
                    "A dispel category identity is required.",
                    nameof(dispelCategoryId));
            }

            List<ActiveStatusEffectStackSnapshotV1> items =
                (stacks ?? throw new ArgumentNullException(nameof(stacks)))
                .ToList();
            if (items.Count == 0 || items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "An active status effect needs at least one non-null stack.",
                    nameof(stacks));
            }
            if (items.Any(item => !string.Equals(
                item.EffectId,
                effectId.Trim(),
                StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Every stack must reference the containing effect.",
                    nameof(stacks));
            }
            if (items.Select(item => item.StackId)
                .Distinct(StringComparer.Ordinal)
                .Count() != items.Count)
            {
                throw new ArgumentException(
                    "Status-effect stack identities must be unique.",
                    nameof(stacks));
            }

            EffectId = effectId.Trim();
            DefinitionFingerprint = definitionFingerprint.Trim();
            StackingPolicy = stackingPolicy;
            DispelCategoryId = dispelCategoryId.Trim();
            Stacks = new ReadOnlyCollection<
                ActiveStatusEffectStackSnapshotV1>(
                    items.OrderBy(
                            item => item.ExpiresAtTickExclusive)
                        .ThenBy(item => item.StackId, StringComparer.Ordinal)
                        .ToList());
            Fingerprint = StatusEffectFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string EffectId { get; }

        public string DefinitionFingerprint { get; }

        public StatusEffectStackingPolicyV1 StackingPolicy { get; }

        public string DispelCategoryId { get; }

        public IReadOnlyList<ActiveStatusEffectStackSnapshotV1> Stacks
        {
            get;
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(builder, "effect", EffectId);
            StatusEffectFingerprintV1.Append(
                builder,
                "definition",
                DefinitionFingerprint);
            StatusEffectFingerprintV1.Append(
                builder,
                "stacking-policy",
                ((int)StackingPolicy).ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "dispel-category",
                DispelCategoryId);
            foreach (ActiveStatusEffectStackSnapshotV1 stack in Stacks)
            {
                StatusEffectFingerprintV1.Append(
                    builder,
                    "stack",
                    stack.ToCanonicalString());
            }

            return builder.ToString();
        }
    }

}

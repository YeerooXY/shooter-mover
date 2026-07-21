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
    public sealed class StatusEffectStateSnapshotV1
    {
        public StatusEffectStateSnapshotV1(
            string subjectId,
            int lifecycleGeneration,
            long latestAcceptedTick,
            string catalogFingerprint,
            IEnumerable<ActiveStatusEffectSnapshotV1> activeEffects,
            RuntimeModifierSnapshotV1 modifierProjection)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                throw new ArgumentException(
                    "A status-effect subject identity is required.",
                    nameof(subjectId));
            }
            if (lifecycleGeneration < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifecycleGeneration));
            }
            if (latestAcceptedTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(latestAcceptedTick));
            }
            if (string.IsNullOrWhiteSpace(catalogFingerprint))
            {
                throw new ArgumentException(
                    "A status-effect catalog fingerprint is required.",
                    nameof(catalogFingerprint));
            }

            List<ActiveStatusEffectSnapshotV1> items =
                (activeEffects
                    ?? Array.Empty<ActiveStatusEffectSnapshotV1>())
                .ToList();
            if (items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Active status effects must be non-null.",
                    nameof(activeEffects));
            }
            if (items.Select(item => item.EffectId)
                .Distinct(StringComparer.Ordinal)
                .Count() != items.Count)
            {
                throw new ArgumentException(
                    "Active status-effect identities must be unique.",
                    nameof(activeEffects));
            }

            SubjectId = subjectId.Trim();
            LifecycleGeneration = lifecycleGeneration;
            LatestAcceptedTick = latestAcceptedTick;
            CatalogFingerprint = catalogFingerprint.Trim();
            ActiveEffects =
                new ReadOnlyCollection<ActiveStatusEffectSnapshotV1>(
                    items.OrderBy(
                            item => item.EffectId,
                            StringComparer.Ordinal)
                        .ToList());
            ModifierProjection = modifierProjection
                ?? throw new ArgumentNullException(
                    nameof(modifierProjection));
            Fingerprint = StatusEffectFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string SubjectId { get; }

        public int LifecycleGeneration { get; }

        public long LatestAcceptedTick { get; }

        public string CatalogFingerprint { get; }

        public IReadOnlyList<ActiveStatusEffectSnapshotV1> ActiveEffects
        {
            get;
        }

        public RuntimeModifierSnapshotV1 ModifierProjection { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(
                builder,
                "subject",
                SubjectId);
            StatusEffectFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "latest-tick",
                LatestAcceptedTick.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "catalog",
                CatalogFingerprint);
            foreach (ActiveStatusEffectSnapshotV1 effect in ActiveEffects)
            {
                StatusEffectFingerprintV1.Append(
                    builder,
                    "active-effect",
                    effect.ToCanonicalString());
            }
            StatusEffectFingerprintV1.Append(
                builder,
                "modifier-projection",
                ModifierProjection.Fingerprint);
            return builder.ToString();
        }
    }

}

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
    public sealed class StatusEffectStateSnapshot
    {
        public StatusEffectStateSnapshot(
            string subjectId,
            int lifecycleGeneration,
            long latestAcceptedTick,
            string catalogFingerprint,
            IEnumerable<ActiveStatusEffectSnapshot> activeEffects,
            LiveModifierSnapshot modifierProjection)
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

            List<ActiveStatusEffectSnapshot> items =
                (activeEffects
                    ?? Array.Empty<ActiveStatusEffectSnapshot>())
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
                new ReadOnlyCollection<ActiveStatusEffectSnapshot>(
                    items.OrderBy(
                            item => item.EffectId,
                            StringComparer.Ordinal)
                        .ToList());
            ModifierProjection = modifierProjection
                ?? throw new ArgumentNullException(
                    nameof(modifierProjection));
            Fingerprint = StatusEffectFingerprint.Hash(
                ToCanonicalString());
        }

        public string SubjectId { get; }

        public int LifecycleGeneration { get; }

        public long LatestAcceptedTick { get; }

        public string CatalogFingerprint { get; }

        public IReadOnlyList<ActiveStatusEffectSnapshot> ActiveEffects
        {
            get;
        }

        public LiveModifierSnapshot ModifierProjection { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprint.Append(
                builder,
                "subject",
                SubjectId);
            StatusEffectFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "latest-tick",
                LatestAcceptedTick.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "catalog",
                CatalogFingerprint);
            foreach (ActiveStatusEffectSnapshot effect in ActiveEffects)
            {
                StatusEffectFingerprint.Append(
                    builder,
                    "active-effect",
                    effect.ToCanonicalString());
            }
            StatusEffectFingerprint.Append(
                builder,
                "modifier-projection",
                ModifierProjection.Fingerprint);
            return builder.ToString();
        }
    }

}

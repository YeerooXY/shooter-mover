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
    public sealed class ActiveStatusEffectStackSnapshotV1
    {
        public ActiveStatusEffectStackSnapshotV1(
            string stackId,
            string effectId,
            string sourceId,
            long appliedAtTick,
            long expiresAtTickExclusive)
        {
            if (string.IsNullOrWhiteSpace(stackId))
            {
                throw new ArgumentException(
                    "A status-effect stack identity is required.",
                    nameof(stackId));
            }
            if (string.IsNullOrWhiteSpace(effectId))
            {
                throw new ArgumentException(
                    "A status-effect definition identity is required.",
                    nameof(effectId));
            }
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException(
                    "A status-effect source identity is required.",
                    nameof(sourceId));
            }
            if (appliedAtTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(appliedAtTick));
            }
            if (expiresAtTickExclusive <= appliedAtTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expiresAtTickExclusive));
            }

            StackId = stackId.Trim();
            EffectId = effectId.Trim();
            SourceId = sourceId.Trim();
            AppliedAtTick = appliedAtTick;
            ExpiresAtTickExclusive = expiresAtTickExclusive;
            Fingerprint = StatusEffectFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string StackId { get; }

        public string EffectId { get; }

        public string SourceId { get; }

        public long AppliedAtTick { get; }

        public long ExpiresAtTickExclusive { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(builder, "stack", StackId);
            StatusEffectFingerprintV1.Append(builder, "effect", EffectId);
            StatusEffectFingerprintV1.Append(builder, "source", SourceId);
            StatusEffectFingerprintV1.Append(
                builder,
                "applied-at",
                AppliedAtTick.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "expires-at",
                ExpiresAtTickExclusive.ToString(
                    CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

}

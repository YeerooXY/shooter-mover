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
    public sealed class StatusEffectCommandResultV1
    {
        public StatusEffectCommandResultV1(
            string operationId,
            string commandFingerprint,
            StatusEffectCommandStatusV1 status,
            StatusEffectCommandActionV1 action,
            string rejectionCode,
            int affectedStackCount,
            int expiredStackCount,
            StatusEffectStateSnapshotV1 state)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                throw new ArgumentException(
                    "A status-effect operation identity is required.",
                    nameof(operationId));
            }
            if (string.IsNullOrWhiteSpace(commandFingerprint))
            {
                throw new ArgumentException(
                    "A status-effect command fingerprint is required.",
                    nameof(commandFingerprint));
            }
            if (!Enum.IsDefined(
                typeof(StatusEffectCommandStatusV1),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (!Enum.IsDefined(
                typeof(StatusEffectCommandActionV1),
                action))
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }
            if (affectedStackCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(affectedStackCount));
            }
            if (expiredStackCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expiredStackCount));
            }

            OperationId = operationId.Trim();
            CommandFingerprint = commandFingerprint.Trim();
            Status = status;
            Action = action;
            RejectionCode = rejectionCode ?? string.Empty;
            AffectedStackCount = affectedStackCount;
            ExpiredStackCount = expiredStackCount;
            State = state ?? throw new ArgumentNullException(nameof(state));
            Fingerprint = StatusEffectFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string OperationId { get; }

        public string CommandFingerprint { get; }

        public StatusEffectCommandStatusV1 Status { get; }

        public StatusEffectCommandActionV1 Action { get; }

        public string RejectionCode { get; }

        public int AffectedStackCount { get; }

        public int ExpiredStackCount { get; }

        public StatusEffectStateSnapshotV1 State { get; }

        public string Fingerprint { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == StatusEffectCommandStatusV1.Accepted
                    || Status
                        == StatusEffectCommandStatusV1.AcceptedNoChange;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(
                builder,
                "operation",
                OperationId);
            StatusEffectFingerprintV1.Append(
                builder,
                "command",
                CommandFingerprint);
            StatusEffectFingerprintV1.Append(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "action",
                ((int)Action).ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "rejection",
                RejectionCode);
            StatusEffectFingerprintV1.Append(
                builder,
                "affected-stacks",
                AffectedStackCount.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "expired-stacks",
                ExpiredStackCount.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "state",
                State.Fingerprint);
            return builder.ToString();
        }
    }

}

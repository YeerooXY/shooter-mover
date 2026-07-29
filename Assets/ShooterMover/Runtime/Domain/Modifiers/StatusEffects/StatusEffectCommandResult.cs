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
    public sealed class StatusEffectCommandResult
    {
        public StatusEffectCommandResult(
            string operationId,
            string commandFingerprint,
            StatusEffectCommandStatus status,
            StatusEffectCommandAction action,
            string rejectionCode,
            int affectedStackCount,
            int expiredStackCount,
            StatusEffectStateSnapshot state)
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
                typeof(StatusEffectCommandStatus),
                status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (!Enum.IsDefined(
                typeof(StatusEffectCommandAction),
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
            Fingerprint = StatusEffectFingerprint.Hash(
                ToCanonicalString());
        }

        public string OperationId { get; }

        public string CommandFingerprint { get; }

        public StatusEffectCommandStatus Status { get; }

        public StatusEffectCommandAction Action { get; }

        public string RejectionCode { get; }

        public int AffectedStackCount { get; }

        public int ExpiredStackCount { get; }

        public StatusEffectStateSnapshot State { get; }

        public string Fingerprint { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == StatusEffectCommandStatus.Accepted
                    || Status
                        == StatusEffectCommandStatus.AcceptedNoChange;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprint.Append(
                builder,
                "operation",
                OperationId);
            StatusEffectFingerprint.Append(
                builder,
                "command",
                CommandFingerprint);
            StatusEffectFingerprint.Append(
                builder,
                "status",
                ((int)Status).ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "action",
                ((int)Action).ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "rejection",
                RejectionCode);
            StatusEffectFingerprint.Append(
                builder,
                "affected-stacks",
                AffectedStackCount.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "expired-stacks",
                ExpiredStackCount.ToString(
                    CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "state",
                State.Fingerprint);
            return builder.ToString();
        }
    }

}

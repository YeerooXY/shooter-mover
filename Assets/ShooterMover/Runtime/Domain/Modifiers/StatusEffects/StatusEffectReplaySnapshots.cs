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
    public sealed class StatusEffectReplayRecordSnapshot
    {
        public StatusEffectReplayRecordSnapshot(
            string operationId,
            string commandFingerprint,
            StatusEffectCommandResult result)
        {
            if (string.IsNullOrWhiteSpace(operationId))
            {
                throw new ArgumentException(
                    "A replay operation identity is required.",
                    nameof(operationId));
            }
            if (string.IsNullOrWhiteSpace(commandFingerprint))
            {
                throw new ArgumentException(
                    "A replay command fingerprint is required.",
                    nameof(commandFingerprint));
            }

            OperationId = operationId.Trim();
            CommandFingerprint = commandFingerprint.Trim();
            Result = result ?? throw new ArgumentNullException(nameof(result));
            if (!string.Equals(
                OperationId,
                Result.OperationId,
                StringComparison.Ordinal)
                || !string.Equals(
                    CommandFingerprint,
                    Result.CommandFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Replay identity must match the stored result.",
                    nameof(result));
            }

            Fingerprint = StatusEffectFingerprint.Hash(
                ToCanonicalString());
        }

        public string OperationId { get; }

        public string CommandFingerprint { get; }

        public StatusEffectCommandResult Result { get; }

        public string Fingerprint { get; }

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
                "result",
                Result.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class StatusEffectLedgerSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public StatusEffectLedgerSnapshot(
            StatusEffectStateSnapshot state,
            IEnumerable<StatusEffectReplayRecordSnapshot> replayHistory,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "Unsupported status-effect authority snapshot schema.");
            }

            List<StatusEffectReplayRecordSnapshot> replay =
                (replayHistory
                    ?? Array.Empty<
                        StatusEffectReplayRecordSnapshot>())
                .ToList();
            if (replay.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Status-effect replay records must be non-null.",
                    nameof(replayHistory));
            }
            if (replay.Select(item => item.OperationId)
                .Distinct(StringComparer.Ordinal)
                .Count() != replay.Count)
            {
                throw new ArgumentException(
                    "Status-effect replay operation identities must be unique.",
                    nameof(replayHistory));
            }

            SchemaVersion = schemaVersion;
            State = state ?? throw new ArgumentNullException(nameof(state));
            ReplayHistory =
                new ReadOnlyCollection<
                    StatusEffectReplayRecordSnapshot>(
                    replay.OrderBy(
                            item => item.OperationId,
                            StringComparer.Ordinal)
                        .ToList());
            Fingerprint = StatusEffectFingerprint.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public StatusEffectStateSnapshot State { get; }

        public IReadOnlyList<StatusEffectReplayRecordSnapshot>
            ReplayHistory
        {
            get;
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprint.Append(
                builder,
                "schema",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprint.Append(
                builder,
                "state",
                State.Fingerprint);
            foreach (StatusEffectReplayRecordSnapshot record in
                ReplayHistory)
            {
                StatusEffectFingerprint.Append(
                    builder,
                    "replay",
                    record.ToCanonicalString());
            }

            return builder.ToString();
        }
    }

}

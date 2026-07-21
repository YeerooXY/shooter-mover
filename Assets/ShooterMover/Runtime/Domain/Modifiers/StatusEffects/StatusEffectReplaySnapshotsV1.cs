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
    public sealed class StatusEffectReplayRecordSnapshotV1
    {
        public StatusEffectReplayRecordSnapshotV1(
            string operationId,
            string commandFingerprint,
            StatusEffectCommandResultV1 result)
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

            Fingerprint = StatusEffectFingerprintV1.Hash(
                ToCanonicalString());
        }

        public string OperationId { get; }

        public string CommandFingerprint { get; }

        public StatusEffectCommandResultV1 Result { get; }

        public string Fingerprint { get; }

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
                "result",
                Result.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class StatusEffectAuthoritySnapshotV1
    {
        public const int CurrentSchemaVersion = 1;

        public StatusEffectAuthoritySnapshotV1(
            StatusEffectStateSnapshotV1 state,
            IEnumerable<StatusEffectReplayRecordSnapshotV1> replayHistory,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(schemaVersion),
                    "Unsupported status-effect authority snapshot schema.");
            }

            List<StatusEffectReplayRecordSnapshotV1> replay =
                (replayHistory
                    ?? Array.Empty<
                        StatusEffectReplayRecordSnapshotV1>())
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
                    StatusEffectReplayRecordSnapshotV1>(
                    replay.OrderBy(
                            item => item.OperationId,
                            StringComparer.Ordinal)
                        .ToList());
            Fingerprint = StatusEffectFingerprintV1.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion { get; }

        public StatusEffectStateSnapshotV1 State { get; }

        public IReadOnlyList<StatusEffectReplayRecordSnapshotV1>
            ReplayHistory
        {
            get;
        }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            StatusEffectFingerprintV1.Append(
                builder,
                "schema",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            StatusEffectFingerprintV1.Append(
                builder,
                "state",
                State.Fingerprint);
            foreach (StatusEffectReplayRecordSnapshotV1 record in
                ReplayHistory)
            {
                StatusEffectFingerprintV1.Append(
                    builder,
                    "replay",
                    record.ToCanonicalString());
            }

            return builder.ToString();
        }
    }

}

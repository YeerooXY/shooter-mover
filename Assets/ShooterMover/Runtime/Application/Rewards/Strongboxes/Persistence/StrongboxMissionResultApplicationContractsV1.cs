using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public enum StrongboxMissionResultApplicationStatusV1
    {
        Applied = 1,
        AcceptedNoChange = 2,
        ExactReplay = 3,
        ConflictingDuplicate = 4,
        Rejected = 5,
    }

    public sealed class StrongboxMissionResultApplicationCommandV1
    {
        private readonly string canonicalText;

        public StrongboxMissionResultApplicationCommandV1(
            StableId operationStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            MissionResultPayloadV1 terminalResult,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            long expectedAccountRevision,
            PlayerHoldingsSnapshotV1 sourceHoldings,
            StrongboxOpeningSnapshotV1 sourceStrongboxes)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            }
            TerminalResult = terminalResult
                ?? throw new ArgumentNullException(nameof(terminalResult));
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(nameof(selectedCharacterStableId));
            if (expectedCharacterRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedCharacterRevision));
            }
            if (string.IsNullOrWhiteSpace(expectedCharacterFingerprint))
            {
                throw new ArgumentException(
                    "An expected character fingerprint is required.",
                    nameof(expectedCharacterFingerprint));
            }
            if (expectedAccountRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedAccountRevision));
            }
            SourceHoldings = sourceHoldings
                ?? throw new ArgumentNullException(nameof(sourceHoldings));
            SourceStrongboxes = sourceStrongboxes
                ?? throw new ArgumentNullException(nameof(sourceStrongboxes));

            RunLifecycleGeneration = runLifecycleGeneration;
            ExpectedCharacterRevision = expectedCharacterRevision;
            ExpectedCharacterFingerprint = expectedCharacterFingerprint.Trim();
            ExpectedAccountRevision = expectedAccountRevision;

            var builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema", "box-mission-result-application-v1");
            StrongboxCanonicalV1.AppendToken(builder, "operation", OperationStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "run", RunStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "run_generation", RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "result", TerminalResult.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "result_fingerprint", TerminalResult.Fingerprint);
            StrongboxCanonicalV1.AppendToken(builder, "character", SelectedCharacterStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "character_revision", ExpectedCharacterRevision.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "character_fingerprint", ExpectedCharacterFingerprint);
            StrongboxCanonicalV1.AppendToken(builder, "account_revision", ExpectedAccountRevision.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "source_holdings", SourceHoldings.Fingerprint);
            StrongboxCanonicalV1.AppendToken(builder, "source_strongboxes", SourceStrongboxes.Fingerprint);
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public MissionResultPayloadV1 TerminalResult { get; }
        public StableId SelectedCharacterStableId { get; }
        public long ExpectedCharacterRevision { get; }
        public string ExpectedCharacterFingerprint { get; }
        public long ExpectedAccountRevision { get; }
        public PlayerHoldingsSnapshotV1 SourceHoldings { get; }
        public StrongboxOpeningSnapshotV1 SourceStrongboxes { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class StrongboxMissionResultApplicationResultV1
    {
        public StrongboxMissionResultApplicationResultV1(
            StrongboxMissionResultApplicationStatusV1 status,
            StableId operationStableId,
            string commandFingerprint,
            string resultFingerprint,
            int transferredCount,
            string holdingsFingerprint,
            string strongboxFingerprint,
            string accountFingerprint,
            string rejectionCode)
        {
            Status = status;
            OperationStableId = operationStableId;
            CommandFingerprint = commandFingerprint ?? string.Empty;
            ResultFingerprint = resultFingerprint ?? string.Empty;
            TransferredCount = transferredCount;
            HoldingsFingerprint = holdingsFingerprint ?? string.Empty;
            StrongboxFingerprint = strongboxFingerprint ?? string.Empty;
            AccountFingerprint = accountFingerprint ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public StrongboxMissionResultApplicationStatusV1 Status { get; }
        public StableId OperationStableId { get; }
        public string CommandFingerprint { get; }
        public string ResultFingerprint { get; }
        public int TransferredCount { get; }
        public string HoldingsFingerprint { get; }
        public string StrongboxFingerprint { get; }
        public string AccountFingerprint { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == StrongboxMissionResultApplicationStatusV1.Applied
                    || Status == StrongboxMissionResultApplicationStatusV1.AcceptedNoChange
                    || Status == StrongboxMissionResultApplicationStatusV1.ExactReplay;
            }
        }
    }

}

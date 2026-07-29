using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public enum StrongboxMissionResultApplicationStatus
    {
        Applied = 1,
        AcceptedNoChange = 2,
        ExactReplay = 3,
        ConflictingDuplicate = 4,
        Rejected = 5,
    }

    public sealed class StrongboxMissionResultApplicationCommand
    {
        private readonly string canonicalText;

        public StrongboxMissionResultApplicationCommand(
            StableId operationStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            MissionResultPayload terminalResult,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            long expectedAccountRevision,
            PlayerHoldingsSnapshot sourceHoldings,
            StrongboxOpeningSnapshot sourceStrongboxes)
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
            Strongbox.AppendToken(builder, "schema", "box-mission-result-application-v1");
            Strongbox.AppendToken(builder, "operation", OperationStableId.ToString());
            Strongbox.AppendToken(builder, "run", RunStableId.ToString());
            Strongbox.AppendToken(builder, "run_generation", RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "result", TerminalResult.ToCanonicalString());
            Strongbox.AppendToken(builder, "result_fingerprint", TerminalResult.Fingerprint);
            Strongbox.AppendToken(builder, "character", SelectedCharacterStableId.ToString());
            Strongbox.AppendToken(builder, "character_revision", ExpectedCharacterRevision.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "character_fingerprint", ExpectedCharacterFingerprint);
            Strongbox.AppendToken(builder, "account_revision", ExpectedAccountRevision.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "source_holdings", SourceHoldings.Fingerprint);
            Strongbox.AppendToken(builder, "source_strongboxes", SourceStrongboxes.Fingerprint);
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public MissionResultPayload TerminalResult { get; }
        public StableId SelectedCharacterStableId { get; }
        public long ExpectedCharacterRevision { get; }
        public string ExpectedCharacterFingerprint { get; }
        public long ExpectedAccountRevision { get; }
        public PlayerHoldingsSnapshot SourceHoldings { get; }
        public StrongboxOpeningSnapshot SourceStrongboxes { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class StrongboxMissionResultApplicationResult
    {
        public StrongboxMissionResultApplicationResult(
            StrongboxMissionResultApplicationStatus status,
            StableId operationStableId,
            string commandFingerprint,
            string resultFingerprint,
            int transferredCount,
            string holdingsFingerprint,
            string strongboxFingerprint,
            string accountFingerprint,
            string rejectionCode,
            bool exactRetryAllowed = false)
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
            ExactRetryAllowed = exactRetryAllowed;
        }

        public StrongboxMissionResultApplicationStatus Status { get; }
        public StableId OperationStableId { get; }
        public string CommandFingerprint { get; }
        public string ResultFingerprint { get; }
        public int TransferredCount { get; }
        public string HoldingsFingerprint { get; }
        public string StrongboxFingerprint { get; }
        public string AccountFingerprint { get; }
        public string RejectionCode { get; }
        public bool ExactRetryAllowed { get; }
        public bool Succeeded
        {
            get
            {
                return Status == StrongboxMissionResultApplicationStatus.Applied
                    || Status == StrongboxMissionResultApplicationStatus.AcceptedNoChange
                    || Status == StrongboxMissionResultApplicationStatus.ExactReplay;
            }
        }
    }

}

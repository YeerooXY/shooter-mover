using System;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.UnityAdapters.Rewards.Sources
{
    public enum LootSourceResolutionStatus
    {
        Resolved = 0,
        MissingPlacedObject = 1,
        PlacedObjectBindingFailed = 2,
        MissingInheritedProfile = 3,
        InvalidInheritedProfile = 4,
        InvalidOverride = 5,
        ConflictingResolvedOperation = 6
    }

    public sealed class LootSourceResolvedPreview
    {
        public LootSourceResolvedPreview(
            LootSourceOverrideAuthoringMode mode,
            RewardProfile inheritedProfile,
            RewardProfile resolvedProfile,
            RewardOperationRequest operationRequest,
            StableId restartParticipantId,
            string fingerprint)
        {
            Mode = mode;
            InheritedProfile = inheritedProfile
                ?? throw new ArgumentNullException(nameof(inheritedProfile));
            ResolvedProfile = resolvedProfile
                ?? throw new ArgumentNullException(nameof(resolvedProfile));
            OperationRequest = operationRequest
                ?? throw new ArgumentNullException(nameof(operationRequest));
            RestartParticipantId = restartParticipantId
                ?? throw new ArgumentNullException(nameof(restartParticipantId));
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        }

        public LootSourceOverrideAuthoringMode Mode { get; }

        public RewardProfile InheritedProfile { get; }

        public RewardProfile ResolvedProfile { get; }

        public RewardOperationRequest OperationRequest { get; }

        public StableId RestartParticipantId { get; }

        public string Fingerprint { get; }
    }

    public sealed class LootSourceResolutionResult
    {
        private LootSourceResolutionResult(
            LootSourceResolutionStatus status,
            LootSourceResolvedPreview preview,
            string diagnostic)
        {
            Status = status;
            Preview = preview;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootSourceResolutionStatus Status { get; }

        public LootSourceResolvedPreview Preview { get; }

        public string Diagnostic { get; }

        public bool IsResolved
        {
            get { return Status == LootSourceResolutionStatus.Resolved; }
        }

        public static LootSourceResolutionResult Resolved(
            LootSourceResolvedPreview preview)
        {
            return new LootSourceResolutionResult(
                LootSourceResolutionStatus.Resolved,
                preview,
                "Reward source authoring resolved successfully.");
        }

        public static LootSourceResolutionResult Failed(
            LootSourceResolutionStatus status,
            string diagnostic)
        {
            if (status == LootSourceResolutionStatus.Resolved)
            {
                throw new ArgumentException(
                    "A failed result cannot use Resolved status.",
                    nameof(status));
            }

            return new LootSourceResolutionResult(status, null, diagnostic);
        }
    }

    public enum LootSourceSubmissionStatus
    {
        Accepted = 0,
        ExactDuplicateNoChange = 1,
        ConflictingDuplicate = 2,
        Rejected = 3
    }

    public sealed class LootSourceSubmissionResult
    {
        public LootSourceSubmissionResult(
            LootSourceSubmissionStatus status,
            string diagnostic)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootSourceSubmissionStatus Status { get; }

        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == LootSourceSubmissionStatus.Accepted
                    || Status == LootSourceSubmissionStatus.ExactDuplicateNoChange;
            }
        }
    }

    public interface ILootSourceOperationSink
    {
        LootSourceSubmissionResult Submit(LootSourceResolvedPreview preview);
    }
}

using System;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.UnityAdapters.Rewards.Sources
{
    public enum RewardSourceResolutionStatus
    {
        Resolved = 0,
        MissingPlacedObject = 1,
        PlacedObjectBindingFailed = 2,
        MissingInheritedProfile = 3,
        InvalidInheritedProfile = 4,
        InvalidOverride = 5,
        ConflictingResolvedOperation = 6
    }

    public sealed class RewardSourceResolvedPreview
    {
        public RewardSourceResolvedPreview(
            RewardSourceOverrideAuthoringMode mode,
            RewardProfileV1 inheritedProfile,
            RewardProfileV1 resolvedProfile,
            RewardOperationRequestV1 operationRequest,
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

        public RewardSourceOverrideAuthoringMode Mode { get; }

        public RewardProfileV1 InheritedProfile { get; }

        public RewardProfileV1 ResolvedProfile { get; }

        public RewardOperationRequestV1 OperationRequest { get; }

        public StableId RestartParticipantId { get; }

        public string Fingerprint { get; }
    }

    public sealed class RewardSourceResolutionResult
    {
        private RewardSourceResolutionResult(
            RewardSourceResolutionStatus status,
            RewardSourceResolvedPreview preview,
            string diagnostic)
        {
            Status = status;
            Preview = preview;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardSourceResolutionStatus Status { get; }

        public RewardSourceResolvedPreview Preview { get; }

        public string Diagnostic { get; }

        public bool IsResolved
        {
            get { return Status == RewardSourceResolutionStatus.Resolved; }
        }

        public static RewardSourceResolutionResult Resolved(
            RewardSourceResolvedPreview preview)
        {
            return new RewardSourceResolutionResult(
                RewardSourceResolutionStatus.Resolved,
                preview,
                "Reward source authoring resolved successfully.");
        }

        public static RewardSourceResolutionResult Failed(
            RewardSourceResolutionStatus status,
            string diagnostic)
        {
            if (status == RewardSourceResolutionStatus.Resolved)
            {
                throw new ArgumentException(
                    "A failed result cannot use Resolved status.",
                    nameof(status));
            }

            return new RewardSourceResolutionResult(status, null, diagnostic);
        }
    }

    public enum RewardSourceSubmissionStatus
    {
        Accepted = 0,
        ExactDuplicateNoChange = 1,
        ConflictingDuplicate = 2,
        Rejected = 3
    }

    public sealed class RewardSourceSubmissionResult
    {
        public RewardSourceSubmissionResult(
            RewardSourceSubmissionStatus status,
            string diagnostic)
        {
            Status = status;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardSourceSubmissionStatus Status { get; }

        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == RewardSourceSubmissionStatus.Accepted
                    || Status == RewardSourceSubmissionStatus.ExactDuplicateNoChange;
            }
        }
    }

    public interface IRewardSourceOperationSink
    {
        RewardSourceSubmissionResult Submit(RewardSourceResolvedPreview preview);
    }
}

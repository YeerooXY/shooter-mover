using System;
using ShooterMover.Application.Rewards.GameplayDrops;
using ShooterMover.UnityAdapters.Rewards.Sources;

namespace ShooterMover.UnityAdapters.Rewards.GameplayDrops
{
    public enum GameplayDropResolutionStatus
    {
        Resolved = 1,
        MissingPlacedObject = 2,
        PlacedObjectBindingFailed = 3,
        MissingProfile = 4,
        InvalidProfile = 5,
        InvalidOverride = 6,
        ConflictingResolvedOperation = 7,
    }

    public sealed class GameplayDropResolutionResult
    {
        private GameplayDropResolutionResult(
            GameplayDropResolutionStatus status,
            GameplayDropOperationV1 operation,
            RewardSourceResolvedPreview sourcePreview,
            string diagnostic)
        {
            Status = status;
            Operation = operation;
            SourcePreview = sourcePreview;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public GameplayDropResolutionStatus Status { get; }

        public GameplayDropOperationV1 Operation { get; }

        public RewardSourceResolvedPreview SourcePreview { get; }

        public string Diagnostic { get; }

        public bool IsResolved
        {
            get { return Status == GameplayDropResolutionStatus.Resolved; }
        }

        public static GameplayDropResolutionResult Resolved(
            GameplayDropOperationV1 operation,
            RewardSourceResolvedPreview sourcePreview)
        {
            return new GameplayDropResolutionResult(
                GameplayDropResolutionStatus.Resolved,
                operation ?? throw new ArgumentNullException(nameof(operation)),
                sourcePreview ?? throw new ArgumentNullException(nameof(sourcePreview)),
                "Gameplay drop operation resolved.");
        }

        public static GameplayDropResolutionResult Failed(
            GameplayDropResolutionStatus status,
            string diagnostic)
        {
            if (status == GameplayDropResolutionStatus.Resolved)
            {
                throw new ArgumentException(
                    "A failed gameplay drop result cannot use Resolved status.",
                    nameof(status));
            }

            return new GameplayDropResolutionResult(status, null, null, diagnostic);
        }
    }

    /// <summary>
    /// Common host-agnostic gameplay-drop boundary. Destructible props, turrets,
    /// droids, bosses, and future sources use this same contract.
    /// </summary>
    public interface IGameplayDropSourceV1
    {
        GameplayDropResolutionResult ResolveGameplayDrop();

        RewardSourceSubmissionResult SubmitGameplayDrop();
    }
}

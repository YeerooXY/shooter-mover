using System;
using ShooterMover.Application.Rewards.LootDrops;
using ShooterMover.UnityAdapters.Rewards.Sources;

namespace ShooterMover.UnityAdapters.Rewards.LootDrops
{
    public enum LootDropResolutionStatus
    {
        Resolved = 1,
        MissingPlacedObject = 2,
        PlacedObjectBindingFailed = 3,
        MissingProfile = 4,
        InvalidProfile = 5,
        InvalidOverride = 6,
        ConflictingResolvedOperation = 7,
    }

    public sealed class LootDropResolutionResult
    {
        private LootDropResolutionResult(
            LootDropResolutionStatus status,
            LootDropOperation operation,
            LootSourceResolvedPreview sourcePreview,
            string diagnostic)
        {
            Status = status;
            Operation = operation;
            SourcePreview = sourcePreview;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootDropResolutionStatus Status { get; }

        public LootDropOperation Operation { get; }

        public LootSourceResolvedPreview SourcePreview { get; }

        public string Diagnostic { get; }

        public bool IsResolved
        {
            get { return Status == LootDropResolutionStatus.Resolved; }
        }

        public static LootDropResolutionResult Resolved(
            LootDropOperation operation,
            LootSourceResolvedPreview sourcePreview)
        {
            return new LootDropResolutionResult(
                LootDropResolutionStatus.Resolved,
                operation ?? throw new ArgumentNullException(nameof(operation)),
                sourcePreview ?? throw new ArgumentNullException(nameof(sourcePreview)),
                "Gameplay drop operation resolved.");
        }

        public static LootDropResolutionResult Failed(
            LootDropResolutionStatus status,
            string diagnostic)
        {
            if (status == LootDropResolutionStatus.Resolved)
            {
                throw new ArgumentException(
                    "A failed gameplay drop result cannot use Resolved status.",
                    nameof(status));
            }

            return new LootDropResolutionResult(status, null, null, diagnostic);
        }
    }

    /// <summary>
    /// Common host-agnostic gameplay-drop boundary. Destructible props, turrets,
    /// droids, bosses, and future sources use this same contract.
    /// </summary>
    public interface ILootDropSource
    {
        LootDropResolutionResult ResolveLootDrop();

        LootSourceSubmissionResult SubmitLootDrop();
    }
}

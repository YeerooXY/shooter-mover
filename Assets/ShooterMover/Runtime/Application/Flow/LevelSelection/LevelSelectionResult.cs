using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.LevelSelection
{
    public sealed class LevelSelectionResult
    {
        internal LevelSelectionResult(
            LevelSelectionStatus status,
            LevelSelectionRoute route,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            PlayerRouteProfilePayload payload,
            string destinationScenePath,
            string feedbackCode)
        {
            Status = status;
            Route = route;
            SelectedModeStableId = selectedModeStableId;
            SelectedLevelStableId = selectedLevelStableId;
            Payload = payload;
            DestinationScenePath = destinationScenePath ?? string.Empty;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public LevelSelectionStatus Status { get; }

        public LevelSelectionRoute Route { get; }

        public StableId SelectedModeStableId { get; }

        public StableId SelectedLevelStableId { get; }

        public PlayerRouteProfilePayload Payload { get; }

        public string DestinationScenePath { get; }

        public string FeedbackCode { get; }

        public bool RouteEmitted
        {
            get { return Status == LevelSelectionStatus.RouteEmitted; }
        }
    }
}

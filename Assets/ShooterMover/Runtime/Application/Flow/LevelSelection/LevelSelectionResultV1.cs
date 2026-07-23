using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.LevelSelection
{
    public sealed class LevelSelectionResultV1
    {
        internal LevelSelectionResultV1(
            LevelSelectionStatusV1 status,
            LevelSelectionRouteV1 route,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            PlayerRouteProfilePayloadV1 payload,
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

        public LevelSelectionStatusV1 Status { get; }

        public LevelSelectionRouteV1 Route { get; }

        public StableId SelectedModeStableId { get; }

        public StableId SelectedLevelStableId { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }

        public string DestinationScenePath { get; }

        public string FeedbackCode { get; }

        public bool RouteEmitted
        {
            get { return Status == LevelSelectionStatusV1.RouteEmitted; }
        }
    }
}

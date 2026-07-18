using System;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Run
{
    public enum Stage1RunCompositionStatusV1
    {
        Composed = 1,
        InvalidRequest = 2,
        CoordinatorRejected = 3,
    }

    public sealed class Stage1RunCompositionRequestV1
    {
        public Stage1RunCompositionRequestV1(
            PlayerRouteProfilePayloadV1 routePayload,
            StableId selectedModeStableId,
            StableId selectedLevelStableId,
            ILevelRunStableIdFactoryV1 runStableIdFactory,
            ILevelRunLoadoutResolverV1 loadoutResolver,
            EnemyExperienceRewardServiceV1 experienceRewards,
            MissionRunResultAuthorityV1 missionResults,
            MissionRunAuthorityCheckpointV1 checkpoint)
        {
            RoutePayload = routePayload;
            SelectedModeStableId = selectedModeStableId;
            SelectedLevelStableId = selectedLevelStableId;
            RunStableIdFactory = runStableIdFactory;
            LoadoutResolver = loadoutResolver;
            ExperienceRewards = experienceRewards;
            MissionResults = missionResults;
            Checkpoint = checkpoint;
        }

        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public StableId SelectedModeStableId { get; }
        public StableId SelectedLevelStableId { get; }
        public ILevelRunStableIdFactoryV1 RunStableIdFactory { get; }
        public ILevelRunLoadoutResolverV1 LoadoutResolver { get; }
        public EnemyExperienceRewardServiceV1 ExperienceRewards { get; }
        public MissionRunResultAuthorityV1 MissionResults { get; }
        public MissionRunAuthorityCheckpointV1 Checkpoint { get; }
    }

    public sealed class Stage1RunCompositionResultV1
    {
        internal Stage1RunCompositionResultV1(
            Stage1RunCompositionStatusV1 status,
            LevelRunStartStatusV1 coordinatorStatus,
            string rejectionCode,
            Stage1ProductionRunSessionV1 session)
        {
            Status = status;
            CoordinatorStatus = coordinatorStatus;
            RejectionCode = rejectionCode ?? string.Empty;
            Session = session;
        }

        public Stage1RunCompositionStatusV1 Status { get; }
        public LevelRunStartStatusV1 CoordinatorStatus { get; }
        public string RejectionCode { get; }
        public Stage1ProductionRunSessionV1 Session { get; }
        public bool Succeeded
        {
            get { return Status == Stage1RunCompositionStatusV1.Composed; }
        }
    }

    /// <summary>
    /// Canonical engine-independent Stage 1 composition boundary. It owns creation of
    /// the accepted Level 1 graph and refuses to construct a session around missing,
    /// stale or unsupported route and authority dependencies.
    /// </summary>
    public sealed class Stage1ProductionRunComposerV1
    {
        public Stage1RunCompositionResultV1 Compose(
            Stage1RunCompositionRequestV1 request)
        {
            string validation = Validate(request);
            if (!string.IsNullOrEmpty(validation))
            {
                return new Stage1RunCompositionResultV1(
                    Stage1RunCompositionStatusV1.InvalidRequest,
                    LevelRunStartStatusV1.InvalidRoutePayload,
                    validation,
                    null);
            }

            LevelRunCoordinatorV1 coordinator;
            string rejectionCode;
            LevelRunStartStatusV1 coordinatorStatus =
                LevelRunCoordinatorV1.TryCreateNewRun(
                    request.RoutePayload,
                    request.SelectedModeStableId,
                    request.SelectedLevelStableId,
                    request.RunStableIdFactory,
                    Level1RoomGraphDefinitionV1.TerminalRoomStableId,
                    new RoomMissionLayoutV1(
                        Level1RoomGraphDefinitionV1.Create()),
                    request.LoadoutResolver,
                    request.ExperienceRewards,
                    request.MissionResults,
                    request.Checkpoint,
                    out coordinator,
                    out rejectionCode);
            if (coordinatorStatus != LevelRunStartStatusV1.Started
                || coordinator == null)
            {
                return new Stage1RunCompositionResultV1(
                    Stage1RunCompositionStatusV1.CoordinatorRejected,
                    coordinatorStatus,
                    string.IsNullOrWhiteSpace(rejectionCode)
                        ? "stage1-coordinator-rejected"
                        : rejectionCode,
                    null);
            }

            return new Stage1RunCompositionResultV1(
                Stage1RunCompositionStatusV1.Composed,
                coordinatorStatus,
                string.Empty,
                new Stage1ProductionRunSessionV1(coordinator));
        }

        private static string Validate(Stage1RunCompositionRequestV1 request)
        {
            if (request == null)
            {
                return "stage1-composition-request-null";
            }

            if (request.RoutePayload == null
                || !request.RoutePayload.HasValidFingerprint())
            {
                return "stage1-route-payload-invalid";
            }

            if (request.SelectedModeStableId == null)
            {
                return "stage1-selected-mode-missing";
            }

            if (request.SelectedLevelStableId == null)
            {
                return "stage1-selected-level-missing";
            }

            if (request.SelectedLevelStableId
                != LevelRunCoordinatorV1.Level1StableId)
            {
                return "stage1-selected-level-unsupported";
            }

            if (request.RunStableIdFactory == null)
            {
                return "stage1-run-id-factory-missing";
            }

            if (request.LoadoutResolver == null)
            {
                return "stage1-loadout-resolver-missing";
            }

            if (request.ExperienceRewards == null)
            {
                return "stage1-experience-rewards-missing";
            }

            if (request.MissionResults == null)
            {
                return "stage1-mission-results-missing";
            }

            if (request.Checkpoint == null)
            {
                return "stage1-authority-checkpoint-missing";
            }

            return string.Empty;
        }
    }
}

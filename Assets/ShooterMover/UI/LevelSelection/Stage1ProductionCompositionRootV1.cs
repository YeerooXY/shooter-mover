using ShooterMover.Application.Missions.Run;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Missions.Run;
using UnityEngine;

namespace ShooterMover.UI.LevelSelection
{
    public enum Stage1ProductionStartupStatusV1
    {
        Started = 1,
        ExactDuplicateNoChange = 2,
        MissingRouteContext = 3,
        UnsupportedRoute = 4,
        MissingAuthorityContext = 5,
        CompositionRejected = 6,
        SceneAdapterRejected = 7,
        MissingPresentationHost = 8,
    }

    /// <summary>
    /// Production Stage 1 scene composition root. It consumes the exact Level Selection
    /// handoff, composes through existing authorities, configures the production scene
    /// adapter, and installs the authoritative 1-4 weapon input bridge. It creates no
    /// replacement inventory, XP, reward, strongbox or mission-result authority.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class Stage1ProductionCompositionRootV1 : MonoBehaviour
    {
        public static readonly StableId PlayerSourceActorStableId =
            StableId.Parse("actor.stage1-player-source");

        [SerializeField]
        private Stage1ProductionPresentationHostV1 gameplayPresentationRoot;

        private Stage1ProductionRunSceneAdapterV1 sceneAdapter;
        private Stage1ProductionWeaponInputV1 weaponInput;
        private Stage1ProductionRunSessionV1 session;
        private bool startupAttempted;

        public Stage1ProductionStartupStatusV1 Status { get; private set; }
        public string RejectionCode { get; private set; }
        public bool Started
        {
            get { return Status == Stage1ProductionStartupStatusV1.Started; }
        }
        public Stage1ProductionRunSessionV1 Session { get { return session; } }
        public Stage1ProductionRunSceneAdapterV1 SceneAdapter { get { return sceneAdapter; } }
        public Stage1ProductionPresentationHostV1 PresentationHost
        {
            get { return gameplayPresentationRoot; }
        }

        private void Awake()
        {
            StartProductionRun();
        }

        public Stage1ProductionStartupStatusV1 StartProductionRun()
        {
            if (startupAttempted)
            {
                return Status == Stage1ProductionStartupStatusV1.Started
                    ? Stage1ProductionStartupStatusV1.ExactDuplicateNoChange
                    : Status;
            }

            startupAttempted = true;
            PlayerRouteProfilePayloadV1 payload;
            StableId modeStableId;
            StableId levelStableId;
            if (!LevelSelectionRouteContextV1.TryRead(
                    out payload,
                    out modeStableId,
                    out levelStableId))
            {
                return Reject(
                    Stage1ProductionStartupStatusV1.MissingRouteContext,
                    "stage1-level-selection-context-missing");
            }

            if (levelStableId != LevelRunCoordinatorV1.Level1StableId
                || modeStableId != Stage1ProductionRunComposerV1.SoloModeStableId)
            {
                return Reject(
                    Stage1ProductionStartupStatusV1.UnsupportedRoute,
                    "stage1-level-selection-context-unsupported");
            }

            if (gameplayPresentationRoot == null
                || !gameplayPresentationRoot.HasRetainedPresentation)
            {
                return Reject(
                    Stage1ProductionStartupStatusV1.MissingPresentationHost,
                    "stage1-production-presentation-host-missing");
            }

            Stage1ProductionAuthorityBundleV1 authorities;
            if (!Stage1ProductionAuthorityContextV1.TryConsume(out authorities))
            {
                return Reject(
                    Stage1ProductionStartupStatusV1.MissingAuthorityContext,
                    "stage1-production-authority-context-missing");
            }

            Stage1RunCompositionResultV1 composition =
                new Stage1ProductionRunComposerV1().Compose(
                    new Stage1RunCompositionRequestV1(
                        payload,
                        modeStableId,
                        levelStableId,
                        authorities.RunStableIdFactory,
                        authorities.LoadoutResolver,
                        authorities.ExperienceRewards,
                        authorities.MissionResults,
                        authorities.Checkpoint));
            if (!composition.Succeeded || composition.Session == null)
            {
                Stage1ProductionAuthorityContextV1.Capture(authorities);
                return Reject(
                    Stage1ProductionStartupStatusV1.CompositionRejected,
                    string.IsNullOrWhiteSpace(composition.RejectionCode)
                        ? "stage1-production-composition-rejected"
                        : composition.RejectionCode);
            }

            sceneAdapter = GetComponent<Stage1ProductionRunSceneAdapterV1>();
            if (sceneAdapter == null)
            {
                sceneAdapter = gameObject.AddComponent<Stage1ProductionRunSceneAdapterV1>();
            }

            Stage1SceneAdapterStatusV1 configured = sceneAdapter.Configure(
                composition.Session,
                PlayerSourceActorStableId,
                payload.SelectedCharacterStableId);
            if (configured != Stage1SceneAdapterStatusV1.Configured
                && configured != Stage1SceneAdapterStatusV1.ExactDuplicateNoChange)
            {
                Stage1ProductionAuthorityContextV1.Capture(authorities);
                return Reject(
                    Stage1ProductionStartupStatusV1.SceneAdapterRejected,
                    "stage1-production-scene-adapter-rejected");
            }

            weaponInput = GetComponent<Stage1ProductionWeaponInputV1>();
            if (weaponInput == null)
            {
                weaponInput = gameObject.AddComponent<Stage1ProductionWeaponInputV1>();
            }
            weaponInput.Configure(sceneAdapter);

            gameplayPresentationRoot.SetPresentationEnabled(true);
            session = composition.Session;
            Status = Stage1ProductionStartupStatusV1.Started;
            RejectionCode = string.Empty;
            return Status;
        }

        private Stage1ProductionStartupStatusV1 Reject(
            Stage1ProductionStartupStatusV1 status,
            string rejectionCode)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            if (gameplayPresentationRoot != null
                && gameplayPresentationRoot.HasRetainedPresentation)
            {
                gameplayPresentationRoot.SetPresentationEnabled(false);
            }
            return Status;
        }
    }
}

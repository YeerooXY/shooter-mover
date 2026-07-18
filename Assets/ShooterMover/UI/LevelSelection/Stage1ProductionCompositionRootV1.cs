using System;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
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
    }

    /// <summary>
    /// Existing production authorities required to compose one Stage 1 run. This is a
    /// dependency handoff only: it owns no holdings, XP, rewards, strongboxes or results.
    /// </summary>
    public sealed class Stage1ProductionAuthorityBundleV1
    {
        public Stage1ProductionAuthorityBundleV1(
            ILevelRunStableIdFactoryV1 runStableIdFactory,
            ILevelRunLoadoutResolverV1 loadoutResolver,
            EnemyExperienceRewardServiceV1 experienceRewards,
            MissionRunResultAuthorityV1 missionResults,
            MissionRunAuthorityCheckpointV1 checkpoint)
        {
            RunStableIdFactory = runStableIdFactory
                ?? throw new ArgumentNullException(nameof(runStableIdFactory));
            LoadoutResolver = loadoutResolver
                ?? throw new ArgumentNullException(nameof(loadoutResolver));
            ExperienceRewards = experienceRewards
                ?? throw new ArgumentNullException(nameof(experienceRewards));
            MissionResults = missionResults
                ?? throw new ArgumentNullException(nameof(missionResults));
            Checkpoint = checkpoint
                ?? throw new ArgumentNullException(nameof(checkpoint));
        }

        public ILevelRunStableIdFactoryV1 RunStableIdFactory { get; }
        public ILevelRunLoadoutResolverV1 LoadoutResolver { get; }
        public EnemyExperienceRewardServiceV1 ExperienceRewards { get; }
        public MissionRunResultAuthorityV1 MissionResults { get; }
        public MissionRunAuthorityCheckpointV1 Checkpoint { get; }
    }

    /// <summary>
    /// One-shot process-local dependency handoff into the Stage 1 scene. Failed startup
    /// restores the exact same bundle so a scene retry cannot manufacture new authority.
    /// </summary>
    public static class Stage1ProductionAuthorityContextV1
    {
        private static readonly object Gate = new object();
        private static Stage1ProductionAuthorityBundleV1 bundle;

        public static void Capture(Stage1ProductionAuthorityBundleV1 authorityBundle)
        {
            if (authorityBundle == null)
            {
                throw new ArgumentNullException(nameof(authorityBundle));
            }

            lock (Gate)
            {
                if (bundle != null && !ReferenceEquals(bundle, authorityBundle))
                {
                    throw new InvalidOperationException(
                        "A different Stage 1 authority bundle is already pending.");
                }

                bundle = authorityBundle;
            }
        }

        public static bool TryConsume(out Stage1ProductionAuthorityBundleV1 authorityBundle)
        {
            lock (Gate)
            {
                authorityBundle = bundle;
                bundle = null;
                return authorityBundle != null;
            }
        }

        public static bool HasPendingBundle
        {
            get
            {
                lock (Gate)
                {
                    return bundle != null;
                }
            }
        }

        public static void ClearForTests()
        {
            lock (Gate)
            {
                bundle = null;
            }
        }
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
        private MonoBehaviour gameplayPresentationRoot;

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
            if (gameplayPresentationRoot != null)
            {
                gameplayPresentationRoot.enabled = false;
            }
            return Status;
        }
    }
}

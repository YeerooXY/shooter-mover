using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Missions.Run
{
    public enum Stage1SceneAdapterStatusV1
    {
        Configured = 1,
        ExactDuplicateNoChange = 2,
        InvalidRequest = 3,
        SessionRejected = 4,
        NotConfigured = 5,
        ResultsRouted = 6,
        NotReady = 7,
        Traversed = 8,
        TraversalRejected = 9,
    }

    public interface IStage1ResultsSceneLoaderV1
    {
        void LoadResultsScene();
    }

    public sealed class UnityStage1ResultsSceneLoaderV1 : IStage1ResultsSceneLoaderV1
    {
        private readonly string sceneName;

        public UnityStage1ResultsSceneLoaderV1(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException(
                    "A Results scene name is required.",
                    nameof(sceneName));
            }

            this.sceneName = sceneName;
        }

        public void LoadResultsScene()
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Thin production Unity boundary for Stage 1. Concrete runtime objects report
    /// accepted facts here; the adapter delegates all run mutation to the application
    /// session. It does not calculate room clears, grant XP, produce rewards, or own
    /// inventory state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage1ProductionRunSceneAdapterV1 :
        MonoBehaviour,
        IStage1ProductionRunBindingV1
    {
        public const string DefaultResultsSceneName = "Results";

        private readonly Dictionary<StableId, Stage1RunRoomRegistrationV1> rooms =
            new Dictionary<StableId, Stage1RunRoomRegistrationV1>();
        private Stage1ProductionRunSessionV1 session;
        private IStage1ResultsSceneLoaderV1 resultsLoader;
        private StableId playerSourceActorStableId;
        private StableId playerStableId;
        private bool resultsSceneLoaded;

        public bool IsConfigured { get { return session != null; } }
        public bool ResultsSceneLoaded { get { return resultsSceneLoaded; } }
        public Stage1ProductionRunSessionV1 Session { get { return session; } }
        public StableId CurrentRoomStableId
        {
            get { return session == null ? null : session.CurrentRoomStableId; }
        }

        public Stage1SceneAdapterStatusV1 Configure(
            Stage1ProductionRunSessionV1 productionSession,
            StableId sourceActorStableId,
            StableId selectedPlayerStableId,
            IStage1ResultsSceneLoaderV1 sceneLoader = null)
        {
            if (productionSession == null
                || sourceActorStableId == null
                || selectedPlayerStableId == null)
            {
                return Stage1SceneAdapterStatusV1.InvalidRequest;
            }

            if (session != null)
            {
                return ReferenceEquals(session, productionSession)
                    && playerSourceActorStableId == sourceActorStableId
                    && playerStableId == selectedPlayerStableId
                    ? Stage1SceneAdapterStatusV1.ExactDuplicateNoChange
                    : Stage1SceneAdapterStatusV1.SessionRejected;
            }

            if (!productionSession.RegisterPlayerSource(
                    sourceActorStableId,
                    selectedPlayerStableId))
            {
                return Stage1SceneAdapterStatusV1.SessionRejected;
            }

            session = productionSession;
            playerSourceActorStableId = sourceActorStableId;
            playerStableId = selectedPlayerStableId;
            resultsLoader = sceneLoader
                ?? new UnityStage1ResultsSceneLoaderV1(DefaultResultsSceneName);
            return Stage1SceneAdapterStatusV1.Configured;
        }

        public Stage1RunRegistrationStatusV1 RegisterRoom(
            StableId roomStableId,
            IEnumerable<Stage1RunEnemyRegistrationV1> enemies)
        {
            if (session == null || roomStableId == null || enemies == null)
            {
                return Stage1RunRegistrationStatusV1.InvalidRequest;
            }

            var registration = new Stage1RunRoomRegistrationV1(
                roomStableId,
                enemies);
            Stage1RunRegistrationStatusV1 result =
                session.RegisterRoom(registration);
            if (result == Stage1RunRegistrationStatusV1.Registered
                || result
                    == Stage1RunRegistrationStatusV1.ExactDuplicateNoChange)
            {
                rooms[roomStableId] = registration;
            }

            return result;
        }

        public LevelRunEnemyDestructionResultV1 ReportEnemyDestroyed(
            StableId roomStableId,
            EnemyDestroyedNotification acceptedDestruction)
        {
            if (session == null)
            {
                return InvalidDestruction("stage1-scene-adapter-not-configured");
            }

            return session.RecordEnemyDestroyed(
                roomStableId,
                acceptedDestruction);
        }

        public Stage1SceneAdapterStatusV1 TryTraverse(
            StableId exitStableId,
            out RoomGraphOperationResultV1 traversal)
        {
            traversal = null;
            if (session == null)
            {
                return Stage1SceneAdapterStatusV1.NotConfigured;
            }

            if (exitStableId == null)
            {
                return Stage1SceneAdapterStatusV1.InvalidRequest;
            }

            traversal = session.Traverse(exitStableId);
            return traversal != null && traversal.Changed
                ? Stage1SceneAdapterStatusV1.Traversed
                : Stage1SceneAdapterStatusV1.TraversalRejected;
        }

        public Stage1SceneAdapterStatusV1 CompleteAndRouteResults(
            out Stage1RunCompletionResultV1 completion)
        {
            completion = null;
            if (session == null)
            {
                return Stage1SceneAdapterStatusV1.NotConfigured;
            }

            completion = session.CompleteAndCaptureResults();
            if (!completion.Succeeded)
            {
                return completion.Status == Stage1RunCompletionStatusV1.NotReady
                    ? Stage1SceneAdapterStatusV1.NotReady
                    : Stage1SceneAdapterStatusV1.SessionRejected;
            }

            if (resultsSceneLoaded)
            {
                return Stage1SceneAdapterStatusV1.ExactDuplicateNoChange;
            }

            resultsSceneLoaded = true;
            resultsLoader.LoadResultsScene();
            return Stage1SceneAdapterStatusV1.ResultsRouted;
        }

        private static LevelRunEnemyDestructionResultV1 InvalidDestruction(
            string rejectionCode)
        {
            return new LevelRunEnemyDestructionResultV1(
                LevelRunEnemyDestructionStatusV1.InvalidRequest,
                rejectionCode,
                null,
                0L,
                false);
        }
    }
}

using System;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.LevelSelection
{
    public interface ILevelSelectionSceneLoader
    {
        void Load(string scenePath);
    }

    public sealed class UnityLevelSelectionSceneLoader :
        ILevelSelectionSceneLoader
    {
        public void Load(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException(
                    "A destination scene path is required.",
                    nameof(scenePath));
            }

            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Process-local route handoff for immutable session projection only. It never
    /// invents or mutates inventory, loadout, XP, reward, wallet, or gameplay truth.
    /// </summary>
    public static class LevelSelectionRouteContext
    {
        private static readonly object Gate = new object();

        private static PlayerRouteProfilePayload payload;
        private static StableId selectedModeStableId;
        private static StableId selectedLevelStableId;

        public static void CaptureEntry(
            PlayerRouteProfilePayload routePayload,
            StableId modeStableId)
        {
            ValidateContext(routePayload, modeStableId);

            lock (Gate)
            {
                payload = routePayload;
                selectedModeStableId = modeStableId;
                selectedLevelStableId = null;
            }
        }

        public static void Capture(LevelSelectionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!result.RouteEmitted)
            {
                throw new ArgumentException(
                    "Only accepted terminal routes can be captured.",
                    nameof(result));
            }

            ValidateContext(result.Payload, result.SelectedModeStableId);

            lock (Gate)
            {
                payload = result.Payload;
                selectedModeStableId = result.SelectedModeStableId;
                selectedLevelStableId = result.SelectedLevelStableId;
            }
        }

        public static bool TryRead(
            out PlayerRouteProfilePayload routePayload,
            out StableId modeStableId,
            out StableId levelStableId)
        {
            lock (Gate)
            {
                routePayload = payload;
                modeStableId = selectedModeStableId;
                levelStableId = selectedLevelStableId;
                return routePayload != null
                    && routePayload.HasValidFingerprint()
                    && modeStableId != null;
            }
        }

        private static void ValidateContext(
            PlayerRouteProfilePayload routePayload,
            StableId modeStableId)
        {
            if (routePayload == null || !routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable route payload is required.",
                    nameof(routePayload));
            }

            if (modeStableId == null)
            {
                throw new ArgumentException(
                    "A selected mode identity is required.",
                    nameof(modeStableId));
            }
        }

        public static void ClearForTests()
        {
            lock (Gate)
            {
                payload = null;
                selectedModeStableId = null;
                selectedLevelStableId = null;
            }
        }
    }

    public sealed class UnityLevelSelectionRouteBridge :
        ILevelSelectionRouteBridge
    {
        private readonly ILevelSelectionSceneLoader sceneLoader;

        public UnityLevelSelectionRouteBridge()
            : this(new UnityLevelSelectionSceneLoader())
        {
        }

        public UnityLevelSelectionRouteBridge(
            ILevelSelectionSceneLoader sceneLoader)
        {
            this.sceneLoader = sceneLoader
                ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public void Present(LevelSelectionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!result.RouteEmitted
                || result.Route == LevelSelectionRoute.None)
            {
                throw new ArgumentException(
                    "Only an accepted route can be presented.",
                    nameof(result));
            }

            LevelSelectionRouteContext.Capture(result);
            sceneLoader.Load(result.DestinationScenePath);
        }
    }

    public sealed class RecordingLevelSelectionRouteBridge :
        ILevelSelectionRouteBridge
    {
        public LevelSelectionResult LastResult { get; private set; }

        public int PresentCount { get; private set; }

        public void Present(LevelSelectionResult result)
        {
            if (result == null || !result.RouteEmitted)
            {
                throw new ArgumentException(
                    "Only accepted routes can be recorded.",
                    nameof(result));
            }

            LastResult = result;
            PresentCount++;
        }
    }
}

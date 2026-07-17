using System;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.LevelSelection
{
    public interface ILevelSelectionSceneLoaderV1
    {
        void Load(string scenePath);
    }

    public sealed class UnityLevelSelectionSceneLoaderV1 :
        ILevelSelectionSceneLoaderV1
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
    public static class LevelSelectionRouteContextV1
    {
        private static readonly object Gate = new object();

        private static PlayerRouteProfilePayloadV1 payload;
        private static StableId selectedModeStableId;
        private static StableId selectedLevelStableId;

        public static void CaptureEntry(
            PlayerRouteProfilePayloadV1 routePayload,
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

        public static void Capture(LevelSelectionResultV1 result)
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
            out PlayerRouteProfilePayloadV1 routePayload,
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
            PlayerRouteProfilePayloadV1 routePayload,
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

    public sealed class UnityLevelSelectionRouteAdapterV1 :
        ILevelSelectionRouteAdapterV1
    {
        private readonly ILevelSelectionSceneLoaderV1 sceneLoader;

        public UnityLevelSelectionRouteAdapterV1()
            : this(new UnityLevelSelectionSceneLoaderV1())
        {
        }

        public UnityLevelSelectionRouteAdapterV1(
            ILevelSelectionSceneLoaderV1 sceneLoader)
        {
            this.sceneLoader = sceneLoader
                ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public void Present(LevelSelectionResultV1 result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!result.RouteEmitted
                || result.Route == LevelSelectionRouteV1.None)
            {
                throw new ArgumentException(
                    "Only an accepted route can be presented.",
                    nameof(result));
            }

            LevelSelectionRouteContextV1.Capture(result);
            sceneLoader.Load(result.DestinationScenePath);
        }
    }

    public sealed class RecordingLevelSelectionRouteAdapterV1 :
        ILevelSelectionRouteAdapterV1
    {
        public LevelSelectionResultV1 LastResult { get; private set; }

        public int PresentCount { get; private set; }

        public void Present(LevelSelectionResultV1 result)
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

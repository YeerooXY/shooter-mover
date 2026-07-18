using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Hub
{
    public sealed class UnityHubRouteDestinationAdapterV1 :
        IHubRouteDestinationAdapterV1
    {
        public const string MainMenuScenePath =
            "Assets/ShooterMover/Scenes/Menu/MainMenu.unity";
        public const string CharacterSelectScenePath =
            "Assets/ShooterMover/Scenes/Flow/CharacterSelect/CharacterSelect.unity";
        public const string HubScenePath =
            "Assets/ShooterMover/Scenes/Flow/Hub/HubFlow.unity";
        public const string PlaySelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/PlaySelection/PlaySelection.unity";

        public void Present(
            HubRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null || !payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable Hub payload is required.",
                    nameof(payload));
            }

            switch (route)
            {
                case HubRouteV1.MainMenu:
                    LoadIfDifferent(MainMenuScenePath);
                    return;
                case HubRouteV1.CharacterSelect:
                    CharacterSelectionEntryRouteContextV1.Capture(payload);
                    LoadIfDifferent(CharacterSelectScenePath);
                    return;
                case HubRouteV1.InventoryLoadoutHub:
                    if (!IsActive(HubScenePath))
                    {
                        HubReturnRouteContextV1.Capture(payload);
                        SceneManager.LoadScene(HubScenePath, LoadSceneMode.Single);
                    }
                    return;
                case HubRouteV1.Play:
                    PlaySelectionEntryRouteContextV1.Capture(payload);
                    LoadIfDifferent(PlaySelectionScenePath);
                    return;
                case HubRouteV1.Inventory:
                case HubRouteV1.Skills:
                case HubRouteV1.Shop:
                case HubRouteV1.Crafting:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        private static void LoadIfDifferent(string scenePath)
        {
            if (!IsActive(scenePath))
            {
                SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
            }
        }

        private static bool IsActive(string scenePath)
        {
            Scene active = SceneManager.GetActiveScene();
            return active.IsValid()
                && string.Equals(active.path, scenePath, StringComparison.Ordinal);
        }
    }

    internal static class HubProductionRoutingBootstrapV1
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForActiveScene()
        {
            Install(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid()
                || (!string.Equals(
                        scene.path,
                        UnityHubRouteDestinationAdapterV1.MainMenuScenePath,
                        StringComparison.Ordinal)
                    && !string.Equals(
                        scene.path,
                        UnityHubRouteDestinationAdapterV1.HubScenePath,
                        StringComparison.Ordinal)))
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<HubProductionRoutingInstallerV1>(true)
                    != null)
                {
                    return;
                }
            }

            var host = new GameObject("LEVELRUN Hub Production Routing");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<HubProductionRoutingInstallerV1>();
        }
    }

    [DefaultExecutionOrder(12000)]
    [DisallowMultipleComponent]
    internal sealed class HubProductionRoutingInstallerV1 : MonoBehaviour
    {
        private void Start()
        {
            HubFlowControllerV1 controller =
                FindFirstObjectByType<HubFlowControllerV1>();
            if (controller == null)
            {
                return;
            }

            if (HubReturnRouteContextV1.HasValue)
            {
                return;
            }

            controller.ConfigureForTests(
                controller.Payload,
                new UnityHubRouteDestinationAdapterV1());
        }
    }
}

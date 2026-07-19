using System;
using System.Collections.Generic;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Content.Definitions.Characters.Selection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Shops;
using ShooterMover.UI.Crafting;
using ShooterMover.UI.Hub;
using ShooterMover.UI.InventoryLoadout;
using ShooterMover.UI.LevelSelection;
using ShooterMover.UI.PlaySelection;
using ShooterMover.UI.Shop;
using ShooterMover.UI.Skills;
using ShooterMover.UI.StrongboxOpening;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Persistent Unity composition adapter. It owns scene-transition state, profile
    /// persistence and one UI camera, while delegating route truth to
    /// HubNavigationServiceV1 and strongbox opening to StrongboxOpeningServiceV1.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class ProductionFlowCoordinatorV1 : MonoBehaviour
    {
        private static ProductionFlowCoordinatorV1 instance;
        private static ProductionResultsContextV1 pendingResultsContext;

        private IProductionFlowProfileStoreV1 profileStore;
        private ProductionFlowProfileRecordV1 profile;
        private PlayerRouteProfilePayloadV1 draftPayload;
        private ProductionSceneTransitionCoordinatorV1 transitions;
        private UnitySceneLoadPortV1 sceneLoader;
        private Camera flowCamera;
        private StableId selectedModeStableId;
        private ProductionResultsContextV1 resultsContext;
        private ProductionStrongboxOpeningBindingV1 strongboxBinding;

        public ProductionSceneTransitionCoordinatorV1 Transitions
        {
            get { return transitions; }
        }

        public ProductionFlowProfileRecordV1 Profile
        {
            get { return profile; }
        }

        public static bool HasInstance { get { return instance != null; } }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            pendingResultsContext = null;
        }


        public static bool PresentResults(
            ProductionResultsContextV1 context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            pendingResultsContext = context;
            if (instance == null) return false;
            instance.resultsContext = context;
            return instance.transitions.TryLoadSubflow(
                ProductionFlowScenePathsV1.Results);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            profileStore = new PlayerPrefsProductionFlowProfileStoreV1();
            profileStore.TryLoad(out profile);
            draftPayload = CreateDraftPayload();
            HubNavigationServiceV1 navigation =
                new HubNavigationServiceV1(
                    profile == null ? draftPayload : profile.Payload);
            sceneLoader = new UnitySceneLoadPortV1();
            transitions = new ProductionSceneTransitionCoordinatorV1(
                navigation,
                sceneLoader);
            resultsContext = pendingResultsContext;
            EnsureFlowCamera();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this) instance = null;
        }

        private void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode mode)
        {
            if (!scene.IsValid()) return;

            bool matched = transitions == null
                || transitions.CompleteSceneLoad(scene.path);
            if (!matched) return;

            CanonicalizeCamera(scene.path);
            BindScene(scene);

            if (string.Equals(
                scene.path,
                ProductionFlowScenePathsV1.Bootstrap,
                StringComparison.Ordinal)
                && !transitions.IsTransitionPending)
            {
                transitions.TryLoadSubflow(
                    ProductionFlowScenePathsV1.MainMenu);
            }
        }

        private void BindScene(Scene scene)
        {
            string path = scene.path;
            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.MainMenu,
                StringComparison.Ordinal))
            {
                ProductionMainMenuControllerV1 controller =
                    Find<ProductionMainMenuControllerV1>(scene);
                if (controller != null)
                {
                    controller.Configure(OpenCharacterSelection);
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.CharacterSelection,
                StringComparison.Ordinal))
            {
                ProductionCharacterSelectionControllerV1 controller =
                    Find<ProductionCharacterSelectionControllerV1>(scene);
                if (controller != null)
                {
                    controller.Configure(
                        profile == null ? draftPayload : profile.Payload,
                        profile,
                        SelectExistingProfile,
                        CreateProfile,
                        transitions.TryNavigateBack);
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.Hub,
                StringComparison.Ordinal))
            {
                HubFlowControllerV1 controller =
                    Find<HubFlowControllerV1>(scene);
                if (controller != null)
                {
                    controller.ConfigureProduction(transitions);
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.Inventory,
                StringComparison.Ordinal))
            {
                InventoryLoadoutScreenControllerV1 controller =
                    Find<InventoryLoadoutScreenControllerV1>(scene);
                if (controller != null)
                {
                    controller.ConfigureDisconnected(ReturnToHub);
                    controller.Present(
                        HubRouteV1.Inventory,
                        transitions.Navigation.Payload);
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.Skills,
                StringComparison.Ordinal))
            {
                SkillsSceneController controller =
                    Find<SkillsSceneController>(scene);
                if (controller != null)
                {
                    controller.ShowDisconnected(
                        transitions.Navigation.Payload,
                        new SkillsNavigationAdapter(this));
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.Shop,
                StringComparison.Ordinal))
            {
                ShopScreenControllerV1 controller =
                    Find<ShopScreenControllerV1>(scene);
                if (controller != null)
                {
                    controller.ConfigureDisconnected(
                        transitions.Navigation.Payload,
                        new ShopNavigationAdapter(this));
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.Crafting,
                StringComparison.Ordinal))
            {
                CraftingScreenControllerV1 controller =
                    Find<CraftingScreenControllerV1>(scene);
                if (controller != null)
                {
                    controller.ConfigureDisconnected(ReturnToHub);
                    controller.Present(
                        HubRouteV1.Crafting,
                        transitions.Navigation.Payload);
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.PlaySelection,
                StringComparison.Ordinal))
            {
                PlaySelectionControllerV1 controller =
                    Find<PlaySelectionControllerV1>(scene);
                if (controller != null)
                {
                    controller.Configure(
                        transitions.Navigation.Payload,
                        PlayModeCatalogDefinitionV1.CreateDefaultCatalog(),
                        new PlayNavigationAdapter(this, controller));
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.LevelSelection,
                StringComparison.Ordinal))
            {
                LevelSelectionControllerV1 controller =
                    Find<LevelSelectionControllerV1>(scene);
                if (controller != null)
                {
                    controller.Configure(
                        transitions.Navigation.Payload,
                        selectedModeStableId,
                        LevelSelectionCatalogDefinitionV1
                            .CreateDefaultCatalog(),
                        new LevelNavigationAdapter(this));
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.Results,
                StringComparison.Ordinal))
            {
                ProductionResultsControllerV1 controller =
                    Find<ProductionResultsControllerV1>(scene);
                if (controller != null && resultsContext != null)
                {
                    controller.Configure(
                        resultsContext.Result,
                        OpenStrongbox,
                        ReturnFromResults);
                }
                return;
            }

            if (string.Equals(
                path,
                ProductionFlowScenePathsV1.StrongboxOpening,
                StringComparison.Ordinal))
            {
                StrongboxOpeningController controller =
                    Find<StrongboxOpeningController>(scene);
                if (controller != null && strongboxBinding != null)
                {
                    controller.BindRuntime(
                        strongboxBinding.OpeningService,
                        strongboxBinding.Command,
                        strongboxBinding.EquipmentCatalog);
                    controller.ContinueOrBackRequested -=
                        ReturnFromStrongboxOpening;
                    controller.ContinueOrBackRequested +=
                        ReturnFromStrongboxOpening;
                }
            }
        }

        private bool OpenCharacterSelection()
        {
            return transitions.TryNavigateTo(HubRouteV1.CharacterSelect);
        }

        private bool SelectExistingProfile(
            PlayerRouteProfilePayloadV1 payload)
        {
            if (profile == null
                || !ReferenceEquals(profile.Payload, payload))
            {
                return false;
            }

            return transitions.TryNavigateTo(
                HubRouteV1.InventoryLoadoutHub);
        }

        private bool CreateProfile(
            string displayName,
            CharacterSelectionRouteResultV1 result)
        {
            if (result == null
                || result.Status
                    != CharacterSelectionRouteStatusV1.Confirmed)
            {
                return false;
            }

            ProductionFlowProfileRecordV1 candidate =
                new ProductionFlowProfileRecordV1(
                    displayName,
                    result.Payload);
            if (!transitions.TryReturnToHub(candidate.Payload))
            {
                return false;
            }

            profileStore.Save(candidate);
            profile = candidate;
            return true;
        }

        private void ReturnToHub(PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null) return;
            if (profile != null && !payload.Equals(profile.Payload))
            {
                ProductionFlowProfileRecordV1 updated =
                    new ProductionFlowProfileRecordV1(
                        profile.DisplayName,
                        payload);
                profileStore.Save(updated);
                profile = updated;
            }

            transitions.TryReturnToHub(payload);
        }

        private bool ReturnFromResults()
        {
            if (resultsContext == null) return false;
            return transitions.TryReturnToHub(
                resultsContext.Result.RoutePayload);
        }

        private bool OpenStrongbox(
            MissionRunStrongboxResultV1 exactStrongbox)
        {
            if (resultsContext == null
                || transitions.IsTransitionPending)
            {
                return false;
            }

            ProductionStrongboxOpeningBindingV1 binding =
                resultsContext.BindExact(exactStrongbox);
            if (!transitions.TryLoadSubflow(
                ProductionFlowScenePathsV1.StrongboxOpening))
            {
                return false;
            }

            strongboxBinding = binding;
            return true;
        }

        private void ReturnFromStrongboxOpening()
        {
            if (resultsContext == null || strongboxBinding == null) return;

            Scene activeScene = SceneManager.GetActiveScene();
            StrongboxOpeningController controller =
                Find<StrongboxOpeningController>(activeScene);
            bool openingSucceeded = controller != null
                && controller.Session != null
                && controller.Session.Result != null
                && controller.Session.Result.Succeeded;

            resultsContext = resultsContext.RefreshAfterExactOpening(
                strongboxBinding.SelectedStrongbox,
                openingSucceeded);
            pendingResultsContext = resultsContext;
            strongboxBinding = null;
            transitions.TryLoadSubflow(
                ProductionFlowScenePathsV1.Results);
        }

        private bool PresentPlayRoute(
            PlaySelectionRouteV1 route,
            PlaySelectionControllerV1 controller)
        {
            if (route == PlaySelectionRouteV1.Hub)
            {
                return transitions.TryNavigateBack();
            }

            if (route != PlaySelectionRouteV1.LevelSelection
                || controller.LastResult == null
                || controller.LastResult.SelectedModeStableId == null)
            {
                return false;
            }

            selectedModeStableId =
                controller.LastResult.SelectedModeStableId;
            return transitions.TryLoadSubflow(
                ProductionFlowScenePathsV1.LevelSelection);
        }

        private bool PresentLevelRoute(LevelSelectionResultV1 result)
        {
            if (result == null || !result.RouteEmitted) return false;
            if (result.Route == LevelSelectionRouteV1.PlaySelection)
            {
                return transitions.TryLoadSubflow(
                    ProductionFlowScenePathsV1.PlaySelection);
            }

            return transitions.TryLoadSubflow(
                result.DestinationScenePath);
        }

        private void EnsureFlowCamera()
        {
            if (flowCamera != null) return;
            GameObject cameraObject = new GameObject(
                "FLOW-UI-001 Canonical UI Camera");
            DontDestroyOnLoad(cameraObject);
            flowCamera = cameraObject.AddComponent<Camera>();
            flowCamera.clearFlags = CameraClearFlags.SolidColor;
            flowCamera.backgroundColor = Color.black;
            flowCamera.cullingMask = 0;
            flowCamera.depth = -1000f;
        }

        private void CanonicalizeCamera(string scenePath)
        {
            bool canonical = IsCanonicalScreen(scenePath);
            flowCamera.enabled = canonical;
            if (!canonical) return;

            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                candidate.enabled = candidate == flowCamera;
            }
        }

        private static bool IsCanonicalScreen(string scenePath)
        {
            return string.Equals(scenePath, ProductionFlowScenePathsV1.Bootstrap, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.MainMenu, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.CharacterSelection, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.Hub, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.PlaySelection, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.LevelSelection, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.Inventory, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.Skills, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.Shop, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.Crafting, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.Results, StringComparison.Ordinal)
                || string.Equals(scenePath, ProductionFlowScenePathsV1.StrongboxOpening, StringComparison.Ordinal);
        }

        private static PlayerRouteProfilePayloadV1 CreateDraftPayload()
        {
            var catalog = BuiltInCharacterSelectionCatalogV1.Create();
            var instances = new List<StableId>
            {
                StableId.Parse("equipment-instance.flow-draft-slot-1"),
                StableId.Parse("equipment-instance.flow-draft-slot-2"),
                StableId.Parse("equipment-instance.flow-draft-slot-3"),
                StableId.Parse("equipment-instance.flow-draft-slot-4"),
            };
            return PlayerRouteProfilePayloadV1.Create(
                catalog.DefaultCharacter.CharacterStableId,
                catalog.DefaultCharacter.DefaultLoadoutProfileStableId,
                instances);
        }

        private static T Find<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                T value = roots[index].GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private sealed class UnitySceneLoadPortV1 :
            IProductionSceneLoadPortV1
        {
            public bool BeginLoad(string scenePath)
            {
                if (string.IsNullOrWhiteSpace(scenePath)) return false;
                AsyncOperation operation = SceneManager.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Single);
                return operation != null;
            }
        }

        private sealed class SkillsNavigationAdapter :
            ISkillsScreenNavigationPortV1
        {
            private readonly ProductionFlowCoordinatorV1 owner;

            public SkillsNavigationAdapter(
                ProductionFlowCoordinatorV1 owner)
            {
                this.owner = owner;
            }

            public void ReturnToHub(
                PlayerRouteProfilePayloadV1 routePayload)
            {
                owner.ReturnToHub(routePayload);
            }
        }

        private sealed class ShopNavigationAdapter :
            IShopScreenRouteAdapterV1
        {
            private readonly ProductionFlowCoordinatorV1 owner;

            public ShopNavigationAdapter(
                ProductionFlowCoordinatorV1 owner)
            {
                this.owner = owner;
            }

            public void Present(
                ShopScreenRouteV1 route,
                PlayerRouteProfilePayloadV1 payload)
            {
                if (route == ShopScreenRouteV1.Hub)
                {
                    owner.ReturnToHub(payload);
                }
            }
        }

        private sealed class PlayNavigationAdapter :
            IPlaySelectionRouteAdapterV1
        {
            private readonly ProductionFlowCoordinatorV1 owner;
            private readonly PlaySelectionControllerV1 controller;

            public PlayNavigationAdapter(
                ProductionFlowCoordinatorV1 owner,
                PlaySelectionControllerV1 controller)
            {
                this.owner = owner;
                this.controller = controller;
            }

            public void Present(
                PlaySelectionRouteV1 route,
                PlayerRouteProfilePayloadV1 payload)
            {
                owner.PresentPlayRoute(route, controller);
            }
        }

        private sealed class LevelNavigationAdapter :
            ILevelSelectionRouteAdapterV1
        {
            private readonly ProductionFlowCoordinatorV1 owner;

            public LevelNavigationAdapter(
                ProductionFlowCoordinatorV1 owner)
            {
                this.owner = owner;
            }

            public void Present(LevelSelectionResultV1 result)
            {
                owner.PresentLevelRoute(result);
            }
        }
    }
}

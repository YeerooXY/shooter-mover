using System;
using System.Collections.Generic;
using ShooterMover.Application.Characters.Selection;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Flow.LevelSelection;
using ShooterMover.Application.Flow.PlaySelection;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Shops;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Application.Skills.Presentation;
using ShooterMover.Content.Definitions.Characters.Selection;
using ShooterMover.Content.Definitions.Flow.PlayModes;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Shops;
using ShooterMover.Domain.Guns.Catalog;
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

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Persistent Unity flow adapter. Character slot lifecycle delegates to the connected
    /// account composition; PlayerPrefs is retained only as migration input and a UI cache.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class GameFlow : MonoBehaviour
    {
        private const int ProfileSlotCount = 6;
        private static GameFlow instance;
        private static ResultsContext pendingResultsContext;

        private PlayerPrefsFlowProfileStore profileStore;
        private readonly FlowProfileRecord[] profiles =
            new FlowProfileRecord[ProfileSlotCount];
        private FlowProfileRecord profile;
        private int activeProfileSlot;
        private PlayerRouteProfilePayload draftPayload;
        private SceneTransitionFlow transitions;
        private UnitySceneLoadPort sceneLoader;
        private Camera flowCamera;
        private StableId selectedModeStableId;
        private ResultsContext resultsContext;
        private StrongboxOpeningBinding strongboxBinding;
        private ICharacterProfiles profileLifecycle;

        public SceneTransitionFlow Transitions
        {
            get { return transitions; }
        }

        public FlowProfileRecord Profile
        {
            get { return profile; }
        }

        public int ActiveProfileSlotIndex
        {
            get { return activeProfileSlot; }
        }

        public static bool HasInstance { get { return instance != null; } }

        public bool ConnectCharacterProfileLifecycle(
            ICharacterProfiles lifecycle)
        {
            if (lifecycle == null)
            {
                return false;
            }

            IReadOnlyList<FlowProfileRecord> projection;
            string rejectionCode;
            if (!lifecycle.TryExportProfiles(
                    out projection,
                    out rejectionCode)
                || projection == null
                || projection.Count != ProfileSlotCount)
            {
                Debug.LogError(
                    "Character profile projection rejected: "
                        + rejectionCode,
                    this);
                return false;
            }

            profileLifecycle = lifecycle;
            for (int slotIndex = 0;
                 slotIndex < ProfileSlotCount;
                 slotIndex++)
            {
                profiles[slotIndex] = projection[slotIndex];
                if (profiles[slotIndex] == null)
                {
                    profileStore.Clear(slotIndex);
                }
                else
                {
                    profileStore.Save(slotIndex, profiles[slotIndex]);
                }
            }

            if (!IsValidSlot(activeProfileSlot)
                || profiles[activeProfileSlot] == null)
            {
                activeProfileSlot = FindFirstOccupiedSlot();
            }
            profile = profiles[activeProfileSlot];
            draftPayload = CreateDraftPayload();
            if (transitions != null
                && !transitions.IsTransitionPending
                && transitions.Navigation.CurrentRoute == HubRoute.MainMenu)
            {
                transitions.ReplaceAtMainMenu(
                    profile == null ? draftPayload : profile.Payload);
            }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            pendingResultsContext = null;
        }

        public static bool PresentResults(
            ResultsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            pendingResultsContext = context;
            if (instance == null) return false;
            instance.resultsContext = context;
            return instance.transitions.TryLoadSubflow(
                FlowScenePaths.Results);
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
            profileStore = new PlayerPrefsFlowProfileStore();
            for (int slotIndex = 0; slotIndex < ProfileSlotCount; slotIndex++)
            {
                profileStore.TryLoad(slotIndex, out profiles[slotIndex]);
            }
            activeProfileSlot = FindFirstOccupiedSlot();
            profile = profiles[activeProfileSlot];
            draftPayload = CreateDraftPayload();
            HubNavigationActions navigation =
                new HubNavigationActions(
                    profile == null ? draftPayload : profile.Payload);
            sceneLoader = new UnitySceneLoadPort();
            transitions = new SceneTransitionFlow(
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
                    FlowScenePaths.Bootstrap,
                    StringComparison.Ordinal)
                && !transitions.IsTransitionPending)
            {
                transitions.TryLoadSubflow(
                    FlowScenePaths.MainMenu);
            }
        }

        private void BindScene(Scene scene)
        {
            string path = scene.path;
            if (string.Equals(
                    path,
                    FlowScenePaths.MainMenu,
                    StringComparison.Ordinal))
            {
                MainMenu controller =
                    Find<MainMenu>(scene);
                if (controller != null)
                {
                    controller.Configure(OpenCharacterSelection);
                    controller.ConfigureMoneyPresentation(
                        ResolveSelectedMoneyBalance);
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.CharacterSelection,
                    StringComparison.Ordinal))
            {
                CharacterMenu controller =
                    Find<CharacterMenu>(scene);
                if (controller != null)
                {
                    controller.Configure(
                        profile == null ? draftPayload : profile.Payload,
                        profiles,
                        SelectExistingProfile,
                        CreateProfile,
                        DeleteProfile,
                        transitions.TryNavigateBack);
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.Hub,
                    StringComparison.Ordinal))
            {
                HubMenu controller =
                    Find<HubMenu>(scene);
                if (controller != null)
                {
                    controller.ConfigureProduction(transitions);
                    controller.ConfigureMoneyPresentation(
                        ResolveSelectedMoneyBalance);
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.Inventory,
                    StringComparison.Ordinal))
            {
                InventoryMenu controller =
                    Find<InventoryMenu>(scene);
                if (controller != null)
                {
                    controller.ConfigureDisconnected(ReturnToHub);
                    CharacterLiveGraph graph;
                    FlowProfileRecord authoritativeProfile;
                    if (CharacterSave.TryResolveCurrent(
                            out graph,
                            out authoritativeProfile)
                        && graph != null
                        && !graph.IsDisposed
                        && graph.LoadoutRuntime != null)
                    {
                        PlayerLoadoutLive runtime =
                            graph.LoadoutRuntime;
                        LoadoutRegistry.Register(
                            runtime.GunInventory,
                            runtime.MountLoadoutAuthority);
                        controller.ConnectCanonicalAuthorities(
                            runtime.Holdings,
                            runtime.CatalogBridge,
                            runtime.GunInventory,
                            runtime.LoadoutAuthority,
                            runtime.MountLayout,
                            runtime.GunCatalog);
                        controller.ConfigureGunPresentation(
                            runtime.EquipmentCatalog,
                            runtime.GunCatalog);
                        controller.Present(
                            HubRoute.Inventory,
                            runtime.CurrentRoutePayload);
                    }
                    else
                    {
                        controller.Present(
                            HubRoute.Inventory,
                            transitions.Navigation.Payload);
                        Debug.LogError(
                            "inventory-character-context-unavailable:"
                                + CharacterSave
                                    .CurrentDiagnostic,
                            controller);
                    }
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.Skills,
                    StringComparison.Ordinal))
            {
                SkillsMenu controller =
                    Find<SkillsMenu>(scene);
                if (controller != null)
                {
                    ConfigureRankedSkills(controller);
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.Shop,
                    StringComparison.Ordinal))
            {
                ShopMenu controller =
                    Find<ShopMenu>(scene);
                if (controller != null)
                {
                    if (!ConfigureProductionShop(controller))
                    {
                        controller.ConfigureDisconnected(
                            transitions.Navigation.Payload,
                            new ShopNavigationBridge(this));
                    }
                    controller.ConfigureMoneyPresentation(
                        ResolveSelectedMoneyBalance);
                    ConfigureShopGunPresentation(controller);
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.Crafting,
                    StringComparison.Ordinal))
            {
                CraftingMenu controller =
                    Find<CraftingMenu>(scene);
                if (controller != null)
                {
                    controller.ConfigureDisconnected(ReturnToHub);
                    controller.Present(
                        HubRoute.Crafting,
                        transitions.Navigation.Payload);
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.PlaySelection,
                    StringComparison.Ordinal))
            {
                PlayMenu controller =
                    Find<PlayMenu>(scene);
                if (controller != null)
                {
                    controller.Configure(
                        transitions.Navigation.Payload,
                        PlayModeCatalogDefinition.CreateDefaultCatalog(),
                        new PlayNavigationBridge(this, controller));
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.LevelSelection,
                    StringComparison.Ordinal))
            {
                LevelMenu controller =
                    Find<LevelMenu>(scene);
                if (controller != null)
                {
                    controller.Configure(
                        transitions.Navigation.Payload,
                        selectedModeStableId,
                        LevelSelectionCatalogDefinition
                            .CreateDefaultCatalog(),
                        new LevelNavigationBridge(this));
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.Results,
                    StringComparison.Ordinal))
            {
                Results controller =
                    Find<Results>(scene);
                if (controller != null && resultsContext != null)
                {
                    controller.Configure(
                        resultsContext.Result,
                        BuildResultsSummary(resultsContext),
                        OpenStrongbox,
                        ReturnFromResults);
                }
                return;
            }

            if (string.Equals(
                    path,
                    FlowScenePaths.StrongboxOpening,
                    StringComparison.Ordinal))
            {
                StrongboxMenu controller =
                    Find<StrongboxMenu>(scene);
                if (controller != null && strongboxBinding != null)
                {
                    GunCatalog gunCatalog = ResolveGunCatalog(
                        strongboxBinding.GunCatalog);
                    GeneratedEquipmentAugmentSignatureState
                        augmentSignatures = null;
                    CharacterLiveGraph graph;
                    FlowProfileRecord activeProfile;
                    if (CharacterSave.TryResolveCurrent(
                            out graph,
                            out activeProfile)
                        && graph != null
                        && !graph.IsDisposed)
                    {
                        augmentSignatures = graph.AugmentSignatures;
                    }
                    if (strongboxBinding.DurableOpeningExecutor != null)
                    {
                        controller.BindDurableRuntime(
                            strongboxBinding.OpeningService,
                            strongboxBinding.Command,
                            strongboxBinding.EquipmentCatalog,
                            gunCatalog,
                            strongboxBinding.SelectedStrongbox,
                            strongboxBinding.DurableOpeningExecutor,
                            augmentSignatures);
                    }
                    else
                    {
                        controller.BindRuntime(
                            strongboxBinding.OpeningService,
                            strongboxBinding.Command,
                            strongboxBinding.EquipmentCatalog,
                            gunCatalog,
                            augmentSignatures);
                    }
                    controller.ContinueOrBackRequested -=
                        ReturnFromStrongboxOpening;
                    controller.ContinueOrBackRequested +=
                        ReturnFromStrongboxOpening;
                }
            }
        }

        private bool OpenCharacterSelection()
        {
            return transitions.TryNavigateTo(HubRoute.CharacterSelect);
        }

        private static ResultsSummary BuildResultsSummary(
            ResultsContext context)
        {
            if (context == null || context.Experience == null) return null;
            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            CharacterSetupFlow coordinator;
            if (!CharacterSave.TryResolveCurrent(
                    out graph,
                    out profile,
                    out coordinator)
                || graph == null
                || graph.IsDisposed)
            {
                return null;
            }

            return new ResultsSummary(
                graph.Character.DisplayName,
                graph.Character.ClassDefinitionStableId.ToString(),
                context.Experience.NewLevel,
                context.Experience.ParticipantStableId,
                context.Experience.EnemiesKilled,
                context.Experience.EnemyExperience,
                context.Experience.CompletedRooms,
                context.Experience.CompletionExperience,
                context.Experience.TotalExperience,
                context.Experience.PreviousLevel,
                context.Experience.NewLevel,
                context.Experience.SkillPointsEarned,
                0L,
                0L);
        }

        private static GunCatalog ResolveGunCatalog(
            GunCatalog supplied)
        {
            if (supplied != null)
            {
                return supplied;
            }

            PlayerLoadoutLive runtime;
            FlowProfileRecord profile;
            return InventoryLoadoutFlow.TryGetCurrent(
                out runtime,
                out profile)
                ? runtime.GunCatalog
                : null;
        }

        private void ConfigureRankedSkills(SkillsMenu controller)
        {
            PlayerRouteProfilePayload payload = transitions.Navigation.Payload;
            var navigation = new SkillsNavigationBridge(this);
            if (payload == null)
            {
                controller.ShowUnavailable(
                    null,
                    navigation,
                    "skills-v2-route-missing");
                return;
            }
            if (!payload.HasValidFingerprint())
            {
                controller.ShowUnavailable(
                    null,
                    navigation,
                    "skills-v2-route-invalid");
                return;
            }

            CharacterLiveGraph graph;
            FlowProfileRecord authoritativeProfile;
            CharacterSetupFlow composition;
            if (!CharacterSave.TryResolveCurrent(
                    out graph,
                    out authoritativeProfile,
                    out composition)
                || graph == null
                || graph.IsDisposed
                || authoritativeProfile == null
                || authoritativeProfile.Payload == null
                || composition == null)
            {
                controller.ShowUnavailable(
                    payload,
                    navigation,
                    "skills-v2-active-character-graph-unavailable");
                return;
            }

            bool exactCharacter = graph.Character != null
                && graph.Character.CharacterInstanceStableId
                    == payload.SelectedCharacterStableId
                && graph.Character.ClassDefinitionStableId
                    == payload.LoadoutProfileStableId;
            bool exactRoute = graph.RoutePayload != null
                && string.Equals(
                    graph.RoutePayload.Fingerprint,
                    payload.Fingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    authoritativeProfile.Payload.Fingerprint,
                    payload.Fingerprint,
                    StringComparison.Ordinal);
            if (!exactCharacter || !exactRoute)
            {
                controller.ShowUnavailable(
                    payload,
                    navigation,
                    "skills-v2-active-character-identity-mismatch");
                return;
            }

            RankedSkillsScreenSession session;
            string rejectionCode;
            if (!RankedSkillsScreenSession.TryCreate(
                    payload,
                    graph.ExperienceAuthority,
                    graph.SkillAuthority,
                    graph.SkillProfileId,
                    new RankedSkillsPersistenceBridge(
                        this,
                        composition,
                        graph,
                        payload,
                        graph.SkillAuthority,
                        graph.SkillProfileId),
                    out session,
                    out rejectionCode))
            {
                controller.ShowUnavailable(
                    payload,
                    navigation,
                    rejectionCode);
                return;
            }

            controller.ShowRanked(session, navigation);
        }

        private static void ConfigureShopGunPresentation(
            ShopMenu controller)
        {
            PlayerLoadoutLive runtime;
            FlowProfileRecord profile;
            if (controller != null
                && InventoryLoadoutFlow.TryGetCurrent(
                    out runtime,
                    out profile))
            {
                controller.ConfigureGunPresentation(
                    runtime.EquipmentCatalog,
                    runtime.GunCatalog);
            }
        }

        private bool ConfigureProductionShop(ShopMenu controller)
        {
            CharacterLiveGraph graph;
            FlowProfileRecord authoritativeProfile;
            CharacterSetupFlow composition;
            if (controller == null
                || !CharacterSave.TryResolveCurrent(
                    out graph,
                    out authoritativeProfile,
                    out composition)
                || graph == null
                || graph.IsDisposed
                || graph.Character == null
                || graph.RoutePayload == null
                || graph.ExperienceAuthority == null
                || graph.LoadoutRuntime == null
                || graph.Shop == null
                || authoritativeProfile == null
                || composition == null)
            {
                return false;
            }

            CharacterShopLive shop = graph.Shop;
            ShopRefreshWindow window = ShopRefreshSchedule.Resolve(
                DateTime.UtcNow);
            StableId characterId =
                graph.Character.CharacterInstanceStableId;
            var session = new ShopScreenSession(
                graph.RoutePayload,
                window.StockId(
                    characterId,
                    shop.Definition.ShopStableId),
                characterId,
                shop.Authority,
                graph.MoneyWallet,
                shop.Definition,
                graph.LoadoutRuntime.EquipmentCatalog,
                graph.ExperienceAuthority.CurrentContext,
                shop.OfferAugments,
                window.RefreshesAtUtc,
                new CharacterShopSave(graph));
            controller.Configure(
                session,
                new ShopNavigationBridge(this));
            return true;
        }

        private long? ResolveSelectedMoneyBalance()
        {
            CharacterLiveGraph graph;
            FlowProfileRecord activeProfile;
            if (!CharacterSave.TryResolveCurrent(
                    out graph,
                    out activeProfile)
                || graph == null
                || graph.IsDisposed
                || graph.MoneyWallet == null)
            {
                return null;
            }

            PlayerRouteProfilePayload route = transitions == null
                    || transitions.Navigation == null
                ? null
                : transitions.Navigation.Payload;
            if (route != null
                && graph.Character.CharacterInstanceStableId
                    != route.SelectedCharacterStableId)
            {
                return null;
            }
            return graph.MoneyWallet.Balance;
        }

        private bool SelectExistingProfile(
            int slotIndex,
            PlayerRouteProfilePayload payload)
        {
            if (!IsValidSlot(slotIndex)
                || profiles[slotIndex] == null
                || !ReferenceEquals(profiles[slotIndex].Payload, payload))
            {
                return false;
            }

            FlowProfileRecord selected = profiles[slotIndex];
            if (profileLifecycle != null)
            {
                string rejectionCode;
                FlowProfileRecord authoritative;
                if (!profileLifecycle.TryActivate(
                        slotIndex,
                        selected,
                        out authoritative,
                        out rejectionCode))
                {
                    Debug.LogError(
                        "Character activation rejected: " + rejectionCode,
                        this);
                    return false;
                }
                selected = authoritative;
                profiles[slotIndex] = selected;
                profileStore.Save(slotIndex, selected);
            }

            activeProfileSlot = slotIndex;
            profile = selected;
            return transitions.TryReturnToHub(selected.Payload);
        }

        private bool CreateProfile(
            int slotIndex,
            string displayName,
            CharacterSelectionRouteResult result)
        {
            if (!IsValidSlot(slotIndex) || profiles[slotIndex] != null)
            {
                return false;
            }
            if (result == null
                || result.Status
                    != CharacterSelectionRouteStatus.Confirmed)
            {
                return false;
            }

            FlowProfileRecord candidate =
                new FlowProfileRecord(
                    displayName,
                    result.Payload);
            if (profileLifecycle != null)
            {
                string rejectionCode;
                FlowProfileRecord authoritative;
                if (!profileLifecycle.TryActivate(
                        slotIndex,
                        candidate,
                        out authoritative,
                        out rejectionCode))
                {
                    Debug.LogError(
                        "Character creation rejected: " + rejectionCode,
                        this);
                    return false;
                }
                candidate = authoritative;
            }

            if (!transitions.TryReturnToHub(candidate.Payload))
            {
                return false;
            }

            profileStore.Save(slotIndex, candidate);
            profiles[slotIndex] = candidate;
            activeProfileSlot = slotIndex;
            profile = candidate;
            return true;
        }

        private bool DeleteProfile(int slotIndex)
        {
            if (!IsValidSlot(slotIndex)
                || profiles[slotIndex] == null
                || transitions.IsTransitionPending)
            {
                return false;
            }

            FlowProfileRecord deleting = profiles[slotIndex];
            if (profileLifecycle != null)
            {
                string rejectionCode;
                if (!profileLifecycle.TryDelete(
                        slotIndex,
                        deleting,
                        out rejectionCode))
                {
                    Debug.LogError(
                        "Character deletion rejected: " + rejectionCode,
                        this);
                    return false;
                }
            }

            if (!transitions.TryLoadSubflow(
                    FlowScenePaths.CharacterSelection))
            {
                return false;
            }

            profileStore.Clear(slotIndex);
            profiles[slotIndex] = null;
            if (activeProfileSlot == slotIndex)
            {
                activeProfileSlot = FindFirstOccupiedSlot();
                profile = profiles[activeProfileSlot];
            }
            draftPayload = CreateDraftPayload();
            return true;
        }

        private void ReturnToHub(PlayerRouteProfilePayload payload)
        {
            if (payload == null) return;
            if (profile != null && !payload.Equals(profile.Payload))
            {
                FlowProfileRecord updated =
                    new FlowProfileRecord(
                        profile.DisplayName,
                        payload);
                profileStore.Save(activeProfileSlot, updated);
                profiles[activeProfileSlot] = updated;
                profile = updated;
            }

            transitions.TryReturnToHub(payload);
        }

        private int FindFirstOccupiedSlot()
        {
            for (int index = 0; index < profiles.Length; index++)
            {
                if (profiles[index] != null) return index;
            }
            return 0;
        }

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < ProfileSlotCount;
        }

        private bool ReturnFromResults()
        {
            if (resultsContext == null) return false;
            return transitions.TryReturnToHub(
                resultsContext.Result.RoutePayload);
        }

        private bool OpenStrongbox(
            MissionRunStrongboxResult exactStrongbox)
        {
            if (resultsContext == null
                || transitions.IsTransitionPending)
            {
                return false;
            }

            StrongboxOpeningBinding binding =
                resultsContext.BindExact(exactStrongbox);
            if (!transitions.TryLoadSubflow(
                    FlowScenePaths.StrongboxOpening))
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
            StrongboxMenu controller =
                Find<StrongboxMenu>(activeScene);
            bool openingSucceeded = controller != null
                && controller.Session != null
                && controller.Session.Result != null
                && controller.Session.Result.Succeeded;

            resultsContext = resultsContext.RefreshAfterExactOpening(
                strongboxBinding.SelectedStrongbox,
                openingSucceeded,
                strongboxBinding.DurableOpeningExecutor != null);
            pendingResultsContext = resultsContext;
            strongboxBinding = null;
            transitions.TryLoadSubflow(
                FlowScenePaths.Results);
        }

        private bool PresentPlayRoute(
            PlaySelectionRoute route,
            PlayMenu controller)
        {
            if (route == PlaySelectionRoute.Hub)
            {
                return transitions.TryNavigateBack();
            }

            if (route != PlaySelectionRoute.LevelSelection
                || controller.LastResult == null
                || controller.LastResult.SelectedModeStableId == null)
            {
                return false;
            }

            selectedModeStableId =
                controller.LastResult.SelectedModeStableId;
            return transitions.TryLoadSubflow(
                FlowScenePaths.LevelSelection);
        }

        private bool PresentLevelRoute(LevelSelectionResult result)
        {
            if (result == null || !result.RouteEmitted) return false;
            if (result.Route == LevelSelectionRoute.PlaySelection)
            {
                return transitions.TryLoadSubflow(
                    FlowScenePaths.PlaySelection);
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
            return string.Equals(scenePath, FlowScenePaths.Bootstrap, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.MainMenu, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.CharacterSelection, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.Hub, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.PlaySelection, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.LevelSelection, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.Inventory, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.Skills, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.Shop, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.Crafting, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.Results, StringComparison.Ordinal)
                || string.Equals(scenePath, FlowScenePaths.StrongboxOpening, StringComparison.Ordinal);
        }

        private static PlayerRouteProfilePayload CreateDraftPayload()
        {
            var catalog = BuiltInCharacterSelectionCatalog.Create();
            return PlayerRouteProfilePayload.Create(
                catalog.DefaultCharacter.CharacterStableId,
                catalog.DefaultCharacter.DefaultLoadoutProfileStableId,
                new StableId[PlayerRouteProfilePayload.GunSlotCount]);
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

        private sealed class UnitySceneLoadPort :
            ISceneLoadPort
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

        private sealed class RankedSkillsPersistenceBridge :
            IRankedSkillsPersistencePort
        {
            private readonly GameFlow owner;
            private readonly CharacterSetupFlow expectedComposition;
            private readonly CharacterLiveGraph expectedGraph;
            private readonly PlayerRouteProfilePayload expectedRoute;
            private readonly RankedSkillAllocationState expectedAuthority;
            private readonly string expectedSkillProfileId;
            public RankedSkillsPersistenceBridge(
                GameFlow owner,
                CharacterSetupFlow expectedComposition,
                CharacterLiveGraph expectedGraph,
                PlayerRouteProfilePayload expectedRoute,
                RankedSkillAllocationState expectedAuthority,
                string expectedSkillProfileId)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                this.expectedComposition = expectedComposition ?? throw new ArgumentNullException(nameof(expectedComposition));
                this.expectedGraph = expectedGraph ?? throw new ArgumentNullException(nameof(expectedGraph));
                this.expectedRoute = expectedRoute ?? throw new ArgumentNullException(nameof(expectedRoute));
                this.expectedAuthority = expectedAuthority ?? throw new ArgumentNullException(nameof(expectedAuthority));
                this.expectedSkillProfileId = string.IsNullOrWhiteSpace(expectedSkillProfileId)
                    ? throw new ArgumentException("A stable ranked-skill profile identity is required.", nameof(expectedSkillProfileId))
                    : expectedSkillProfileId.Trim();
            }
            public RankedSkillsPersistenceResult Persist(
                string mutationScope, string immutableMutationFingerprint)
            {
                if (string.IsNullOrWhiteSpace(mutationScope)
                    || string.IsNullOrWhiteSpace(immutableMutationFingerprint))
                    return Reject("skills-v2-persistence-request-invalid", true);
                string fingerprint = immutableMutationFingerprint.Trim();
                RankedSkillAllocationSnapshot accepted;
                string rejectionCode;
                if (!TryReadExactActiveSnapshot(out accepted, out rejectionCode))
                    return Reject(rejectionCode, true);
                if (!string.Equals(accepted.Fingerprint, fingerprint, StringComparison.Ordinal))
                    return Reject("skills-v2-persistence-snapshot-mismatch", true);
                CharacterSetupResult result =
                    CharacterSave.PersistCurrent(mutationScope, fingerprint);
                if (result == null || !result.Succeeded)
                    return Reject(result == null
                        ? "character-composition-save-result-null"
                        : result.Diagnostic, true);
                if (!TryReadExactActiveSnapshot(out accepted, out rejectionCode))
                    return Reject(rejectionCode, false);
                if (!TryVerifyPersistedSnapshot(result.Character, fingerprint, out rejectionCode))
                    return Reject(rejectionCode, false);
                return new RankedSkillsPersistenceResult(true, string.Empty, false);
            }
            private bool TryReadExactActiveSnapshot(
                out RankedSkillAllocationSnapshot allocation, out string rejectionCode)
            {
                allocation = null;
                CharacterLiveGraph currentGraph =
                    expectedComposition.ActiveRuntime as CharacterLiveGraph;
                PlayerRouteProfilePayload navigationRoute = owner.transitions == null
                    || owner.transitions.Navigation == null ? null : owner.transitions.Navigation.Payload;
                FlowProfileRecord currentProfile = owner.profile;
                if (currentGraph == null || currentGraph.IsDisposed
                    || !ReferenceEquals(currentGraph, expectedGraph)
                    || !ReferenceEquals(currentGraph.SkillAuthority, expectedAuthority))
                { rejectionCode = "skills-v2-persistence-active-graph-changed"; return false; }
                if (currentGraph.Character == null
                    || currentGraph.Character.CharacterInstanceStableId != expectedRoute.SelectedCharacterStableId
                    || currentGraph.Character.ClassDefinitionStableId != expectedRoute.LoadoutProfileStableId
                    || currentGraph.RoutePayload == null || !currentGraph.RoutePayload.HasValidFingerprint()
                    || !string.Equals(currentGraph.RoutePayload.Fingerprint, expectedRoute.Fingerprint, StringComparison.Ordinal)
                    || navigationRoute == null || !navigationRoute.HasValidFingerprint()
                    || !string.Equals(navigationRoute.Fingerprint, expectedRoute.Fingerprint, StringComparison.Ordinal)
                    || currentProfile == null || currentProfile.Payload == null
                    || !string.Equals(currentProfile.Payload.Fingerprint, expectedRoute.Fingerprint, StringComparison.Ordinal)
                    || !string.Equals(currentGraph.SkillProfileId, expectedSkillProfileId, StringComparison.Ordinal))
                { rejectionCode = "skills-v2-persistence-active-identity-mismatch"; return false; }
                if (!expectedAuthority.TryGet(expectedSkillProfileId, out allocation))
                { rejectionCode = "skills-v2-persistence-profile-unavailable"; return false; }
                rejectionCode = string.Empty;
                return true;
            }
            private bool TryVerifyPersistedSnapshot(
                CharacterInstanceSnapshot character, string fingerprint, out string rejectionCode)
            {
                if (character == null
                    || character.CharacterInstanceStableId != expectedRoute.SelectedCharacterStableId
                    || character.ClassDefinitionStableId != expectedRoute.LoadoutProfileStableId)
                { rejectionCode = "skills-v2-persistence-character-mismatch"; return false; }
                SavePartSnapshot component;
                if (!character.TryGetComponent(
                    GameSaveParts.RankedSkillAllocation().ComponentStableId,
                    out component))
                { rejectionCode = "skills-v2-persistence-component-missing"; return false; }
                RankedSkillAllocationSnapshot persisted;
                if (!GameSaveFormats.RankedSkillAllocation.TryDecode(
                    component.CanonicalPayload, out persisted, out rejectionCode))
                { rejectionCode = "skills-v2-persistence-component-invalid:" + rejectionCode; return false; }
                if (!string.Equals(persisted.ProfileId, expectedSkillProfileId, StringComparison.Ordinal)
                    || !string.Equals(persisted.Fingerprint, fingerprint, StringComparison.Ordinal))
                { rejectionCode = "skills-v2-persistence-committed-snapshot-mismatch"; return false; }
                rejectionCode = string.Empty;
                return true;
            }
            private static RankedSkillsPersistenceResult Reject(
                string rejectionCode, bool shouldRollback)
            { return new RankedSkillsPersistenceResult(false, rejectionCode, shouldRollback); }
        }

        private sealed class SkillsNavigationBridge :
            ISkillsScreenNavigationPort
        {
            private readonly GameFlow owner;

            public SkillsNavigationBridge(
                GameFlow owner)
            {
                this.owner = owner;
            }

            public void ReturnToHub(
                PlayerRouteProfilePayload routePayload)
            {
                if (routePayload == null)
                {
                    owner.transitions.TryNavigateBack();
                    return;
                }
                owner.ReturnToHub(routePayload);
            }
        }

        private sealed class ShopNavigationBridge :
            IShopScreenRouteBridge
        {
            private readonly GameFlow owner;

            public ShopNavigationBridge(
                GameFlow owner)
            {
                this.owner = owner;
            }

            public void Present(
                ShopScreenRoute route,
                PlayerRouteProfilePayload payload)
            {
                if (route == ShopScreenRoute.Hub)
                {
                    owner.ReturnToHub(payload);
                }
            }
        }

        private sealed class CharacterShopSave : ICompensatingShopSave
        {
            private readonly CharacterLiveGraph graph;
            private ShooterMover.Domain.Economy.Money.MoneyWalletSnapshot money;
            private ShooterMover.Contracts.Holdings.PlayerHoldingsSnapshot holdings;
            private GunInventorySnapshot guns;
            private GeneratedEquipmentAugmentSignatureSnapshot augments;
            private GeneratedEquipmentAugmentSignatureSnapshot offerAugments;
            private ShopReceiptSnapshot receipts;
            private ShopLiveSnapshot shop;
            private ShooterMover.Contracts.Rewards.Application.RewardApplicationSnapshot
                purchaseRewards;
            private bool prepared;

            public CharacterShopSave(CharacterLiveGraph graph)
            {
                this.graph = graph
                    ?? throw new ArgumentNullException(nameof(graph));
            }

            public bool Prepare(out string rejectionCode)
            {
                try
                {
                    money = graph.MoneyWallet.CurrentSnapshot;
                    holdings = graph.LoadoutRuntime.Holdings.ExportSnapshot();
                    guns = graph.LoadoutRuntime.GunInventory.ExportSnapshot();
                    augments = graph.AugmentSignatures.ExportDurableSnapshot();
                    offerAugments = graph.Shop.OfferAugments
                        .ExportDurableSnapshot();
                    receipts = graph.Shop.Receipts.ExportSnapshot();
                    shop = graph.Shop.Authority.ExportSnapshot();
                    purchaseRewards = graph.Shop.PurchaseRewards.ExportSnapshot();
                    prepared = true;
                    rejectionCode = string.Empty;
                    return true;
                }
                catch (Exception exception)
                {
                    prepared = false;
                    rejectionCode = "shop-purchase-checkpoint-exception-"
                        + exception.GetType().Name.ToLowerInvariant();
                    return false;
                }
            }

            public bool Persist(
                string mutationFingerprint,
                out string rejectionCode)
            {
                CharacterSetupResult result =
                    CharacterSave.PersistCurrent(
                        "shop-purchase",
                        mutationFingerprint);
                if (result != null && result.Succeeded)
                {
                    rejectionCode = string.Empty;
                    return true;
                }

                rejectionCode = result == null
                    ? "shop-purchase-save-result-null"
                    : string.IsNullOrWhiteSpace(result.Diagnostic)
                        ? "shop-purchase-save-rejected"
                        : result.Diagnostic;
                return false;
            }

            public bool Restore(out string rejectionCode)
            {
                if (!prepared)
                {
                    rejectionCode = "shop-purchase-checkpoint-missing";
                    return false;
                }

                var failures = new List<string>();
                try
                {
                    if (!graph.MoneyWallet.ImportSnapshot(money).Succeeded)
                    {
                        failures.Add("money");
                    }
                    if (!graph.LoadoutRuntime.Holdings
                        .ImportSnapshot(holdings).Succeeded)
                    {
                        failures.Add("holdings");
                    }
                    if (!graph.LoadoutRuntime.GunInventory
                        .ImportSnapshot(guns).Succeeded)
                    {
                        failures.Add("gun-inventory");
                    }
                    graph.AugmentSignatures.RestoreDurableSnapshot(augments);
                    graph.Shop.OfferAugments.RestoreDurableSnapshot(
                        offerAugments);
                    string receiptRejection;
                    if (!graph.Shop.Receipts.TryImportSnapshot(
                        receipts,
                        out receiptRejection))
                    {
                        failures.Add("receipts:" + receiptRejection);
                    }
                    string shopRejection;
                    if (!graph.Shop.Authority.TryImportSnapshot(
                        shop,
                        out shopRejection))
                    {
                        failures.Add("shop:" + shopRejection);
                    }
                    if (!graph.Shop.PurchaseRewards.ImportSnapshot(
                        purchaseRewards).Succeeded)
                    {
                        failures.Add("reward-application");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(
                        "exception-"
                        + exception.GetType().Name.ToLowerInvariant());
                }

                prepared = false;
                rejectionCode = failures.Count == 0
                    ? string.Empty
                    : string.Join(",", failures);
                return failures.Count == 0;
            }
        }

        private sealed class PlayNavigationBridge :
            IPlaySelectionRouteBridge
        {
            private readonly GameFlow owner;
            private readonly PlayMenu controller;

            public PlayNavigationBridge(
                GameFlow owner,
                PlayMenu controller)
            {
                this.owner = owner;
                this.controller = controller;
            }

            public void Present(
                PlaySelectionRoute route,
                PlayerRouteProfilePayload payload)
            {
                owner.PresentPlayRoute(route, controller);
            }
        }

        private sealed class LevelNavigationBridge :
            ILevelSelectionRouteBridge
        {
            private readonly GameFlow owner;

            public LevelNavigationBridge(
                GameFlow owner)
            {
                this.owner = owner;
            }

            public void Present(LevelSelectionResult result)
            {
                owner.PresentLevelRoute(result);
            }
        }
    }
}

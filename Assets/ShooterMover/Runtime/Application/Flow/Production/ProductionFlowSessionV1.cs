using System;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Application.Flow.Production
{
    public static class ProductionFlowScenePathsV1
    {
        public const string Bootstrap = "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        public const string MainMenu = "Assets/ShooterMover/Scenes/Menu/MainMenu.unity";
        public const string CharacterSelection = "Assets/ShooterMover/Scenes/Flow/CharacterSelect/CharacterSelect.unity";
        public const string Hub = "Assets/ShooterMover/Scenes/Flow/Hub/HubFlow.unity";
        public const string PlaySelection = "Assets/ShooterMover/Scenes/Flow/PlaySelection/PlaySelection.unity";
        public const string LevelSelection = "Assets/ShooterMover/Scenes/Flow/LevelSelection/LevelSelection.unity";
        public const string Inventory = "Assets/ShooterMover/Scenes/Flow/InventoryLoadout/InventoryLoadout.unity";
        public const string Skills = "Assets/ShooterMover/Scenes/Flow/Skills/Skills.unity";
        public const string Shop = "Assets/ShooterMover/Scenes/Flow/Shop/Shop.unity";
        public const string Crafting = "Assets/ShooterMover/Scenes/Flow/Crafting/Crafting.unity";
        public const string Results = "Assets/ShooterMover/Scenes/Flow/Results/Results.unity";
        public const string StrongboxOpening = "Assets/ShooterMover/Scenes/StrongboxOpening/StrongboxOpening.unity";

        public static string ForHubRoute(HubRouteV1 route)
        {
            switch (route)
            {
                case HubRouteV1.MainMenu: return MainMenu;
                case HubRouteV1.CharacterSelect: return CharacterSelection;
                case HubRouteV1.InventoryLoadoutHub: return Hub;
                case HubRouteV1.Inventory: return Inventory;
                case HubRouteV1.Skills: return Skills;
                case HubRouteV1.Shop: return Shop;
                case HubRouteV1.Crafting: return Crafting;
                case HubRouteV1.Play: return PlaySelection;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, "No production scene is registered for the route.");
            }
        }
    }

    public sealed class ProductionFlowProfileRecordV1
    {
        public ProductionFlowProfileRecordV1(
            string displayName,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("A character display name is required.", nameof(displayName));
            }

            DisplayName = displayName.Trim();
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException("The route payload fingerprint is invalid.", nameof(payload));
            }
        }

        public string DisplayName { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }
    }

    public interface IProductionFlowProfileStoreV1
    {
        bool TryLoad(out ProductionFlowProfileRecordV1 record);

        void Save(ProductionFlowProfileRecordV1 record);

        void Clear();
    }

    public sealed class InMemoryProductionFlowProfileStoreV1 :
        IProductionFlowProfileStoreV1
    {
        private ProductionFlowProfileRecordV1 record;

        public bool TryLoad(out ProductionFlowProfileRecordV1 value)
        {
            value = record;
            return value != null;
        }

        public void Save(ProductionFlowProfileRecordV1 value)
        {
            record = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void Clear()
        {
            record = null;
        }
    }

    public interface IProductionSceneLoadPortV1
    {
        bool BeginLoad(string scenePath);
    }

    /// <summary>
    /// Transaction boundary around the existing HubNavigationServiceV1. This type owns
    /// only one in-flight Unity scene request. Route semantics, history and payload
    /// identity remain owned by HubNavigationServiceV1.
    /// </summary>
    public sealed class ProductionSceneTransitionCoordinatorV1 :
        IHubRouteTransactionPortV1
    {
        private readonly IProductionSceneLoadPortV1 sceneLoader;
        private HubNavigationServiceV1 navigation;
        private string pendingScenePath;

        public ProductionSceneTransitionCoordinatorV1(
            HubNavigationServiceV1 navigation,
            IProductionSceneLoadPortV1 sceneLoader)
        {
            this.navigation = navigation
                ?? throw new ArgumentNullException(nameof(navigation));
            this.sceneLoader = sceneLoader
                ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public HubNavigationServiceV1 Navigation
        {
            get { return navigation; }
        }

        public bool IsTransitionPending
        {
            get { return pendingScenePath != null; }
        }

        public string PendingScenePath
        {
            get { return pendingScenePath ?? string.Empty; }
        }

        public int AcceptedLoadCount { get; private set; }

        public int RejectedWhilePendingCount { get; private set; }

        public int MismatchedCompletionCount { get; private set; }

        public bool TryNavigateTo(HubRouteV1 route)
        {
            if (IsTransitionPending)
            {
                RejectedWhilePendingCount++;
                return false;
            }

            if (!navigation.CanNavigateTo(route))
            {
                return false;
            }

            string scenePath = ProductionFlowScenePathsV1.ForHubRoute(route);
            if (!TryBegin(scenePath))
            {
                return false;
            }

            HubNavigationResultV1 result = navigation.TryNavigateTo(route);
            if (!result.Changed)
            {
                pendingScenePath = null;
                throw new InvalidOperationException(
                    "The route changed between validation and accepted scene loading.");
            }

            return true;
        }

        public bool TryNavigateBack()
        {
            if (IsTransitionPending)
            {
                RejectedWhilePendingCount++;
                return false;
            }

            HubRouteV1 target;
            if (!navigation.TryPeekBackRoute(out target))
            {
                return false;
            }

            string scenePath = ProductionFlowScenePathsV1.ForHubRoute(target);
            if (!TryBegin(scenePath))
            {
                return false;
            }

            HubNavigationResultV1 result = navigation.NavigateBack();
            if (!result.Changed)
            {
                pendingScenePath = null;
                throw new InvalidOperationException(
                    "The back route changed between validation and accepted scene loading.");
            }

            return true;
        }

        public bool TryLoadSubflow(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("A destination scene path is required.", nameof(scenePath));
            }

            if (IsTransitionPending)
            {
                RejectedWhilePendingCount++;
                return false;
            }

            return TryBegin(scenePath);
        }

        public bool TryReturnToHub(PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException("The returned route payload is invalid.", nameof(payload));
            }

            if (IsTransitionPending)
            {
                RejectedWhilePendingCount++;
                return false;
            }

            if (!TryBegin(ProductionFlowScenePathsV1.Hub))
            {
                return false;
            }

            if (ReferenceEquals(payload, navigation.Payload)
                || payload.Equals(navigation.Payload))
            {
                HubNavigationResultV1 back = navigation.NavigateBack();
                if (back.Changed
                    && navigation.CurrentRoute == HubRouteV1.InventoryLoadoutHub)
                {
                    return true;
                }
            }

            ReplaceWithHubNavigation(payload);
            return true;
        }

        public void ReplaceAtMainMenu(PlayerRouteProfilePayloadV1 payload)
        {
            if (IsTransitionPending)
            {
                throw new InvalidOperationException(
                    "Navigation cannot be replaced while a scene transition is pending.");
            }

            navigation = new HubNavigationServiceV1(
                payload ?? throw new ArgumentNullException(nameof(payload)));
        }

        public bool CompleteSceneLoad(string loadedScenePath)
        {
            if (pendingScenePath == null)
            {
                return true;
            }

            if (string.Equals(
                loadedScenePath,
                pendingScenePath,
                StringComparison.Ordinal))
            {
                pendingScenePath = null;
                return true;
            }

            MismatchedCompletionCount++;
            if (!sceneLoader.BeginLoad(pendingScenePath))
            {
                throw new InvalidOperationException(
                    "The loaded scene did not match the accepted route and reconciliation failed.");
            }

            AcceptedLoadCount++;
            return false;
        }

        private bool TryBegin(string scenePath)
        {
            pendingScenePath = scenePath;
            if (!sceneLoader.BeginLoad(scenePath))
            {
                pendingScenePath = null;
                return false;
            }

            AcceptedLoadCount++;
            return true;
        }

        private void ReplaceWithHubNavigation(
            PlayerRouteProfilePayloadV1 payload)
        {
            HubNavigationServiceV1 replacement =
                new HubNavigationServiceV1(payload);
            HubNavigationResultV1 character =
                replacement.TryNavigateTo(HubRouteV1.CharacterSelect);
            HubNavigationResultV1 hub =
                replacement.TryNavigateTo(HubRouteV1.InventoryLoadoutHub);
            if (!character.Changed || !hub.Changed)
            {
                pendingScenePath = null;
                throw new InvalidOperationException(
                    "Unable to rebuild the canonical route at Hub.");
            }

            navigation = replacement;
        }
    }

    public sealed class ProductionStrongboxOpeningBindingV1
    {
        public ProductionStrongboxOpeningBindingV1(
            MissionRunStrongboxResultV1 selectedStrongbox,
            StrongboxOpeningServiceV1 openingService,
            StrongboxOpenCommandV1 command,
            EquipmentCatalog equipmentCatalog)
        {
            SelectedStrongbox = selectedStrongbox
                ?? throw new ArgumentNullException(nameof(selectedStrongbox));
            if (!selectedStrongbox.IsUnopened)
            {
                throw new ArgumentException(
                    "Only an unopened exact strongbox result may be bound.",
                    nameof(selectedStrongbox));
            }

            OpeningService = openingService
                ?? throw new ArgumentNullException(nameof(openingService));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            EquipmentCatalog = equipmentCatalog;
        }

        public MissionRunStrongboxResultV1 SelectedStrongbox { get; }

        public StrongboxOpeningServiceV1 OpeningService { get; }

        public StrongboxOpenCommandV1 Command { get; }

        public EquipmentCatalog EquipmentCatalog { get; }
    }

    /// <summary>
    /// Immutable composition context. The result payload and exact strongbox objects
    /// remain RUN-001 facts; BOX owns opening. Refresh is supplied by the authoritative
    /// run/results composition after BOX completes.
    /// </summary>
    public sealed class ProductionResultsContextV1
    {
        private readonly Func<MissionRunStrongboxResultV1, StrongboxOpenCommandV1>
            commandFactory;
        private readonly Func<MissionResultPayloadV1> refreshResult;

        public ProductionResultsContextV1(
            MissionResultPayloadV1 result,
            StrongboxOpeningServiceV1 openingService,
            Func<MissionRunStrongboxResultV1, StrongboxOpenCommandV1> commandFactory,
            EquipmentCatalog equipmentCatalog,
            Func<MissionResultPayloadV1> refreshResult)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            OpeningService = openingService
                ?? throw new ArgumentNullException(nameof(openingService));
            this.commandFactory = commandFactory
                ?? throw new ArgumentNullException(nameof(commandFactory));
            EquipmentCatalog = equipmentCatalog;
            this.refreshResult = refreshResult
                ?? throw new ArgumentNullException(nameof(refreshResult));
        }

        public MissionResultPayloadV1 Result { get; }

        public StrongboxOpeningServiceV1 OpeningService { get; }

        public EquipmentCatalog EquipmentCatalog { get; }

        public ProductionStrongboxOpeningBindingV1 BindExact(
            MissionRunStrongboxResultV1 selected)
        {
            bool exactReference = false;
            for (int index = 0; index < Result.UnopenedStrongboxes.Count; index++)
            {
                if (ReferenceEquals(Result.UnopenedStrongboxes[index], selected))
                {
                    exactReference = true;
                    break;
                }
            }

            if (!exactReference)
            {
                throw new ArgumentException(
                    "The selected strongbox must be the exact unopened result object.",
                    nameof(selected));
            }

            StrongboxOpenCommandV1 command = commandFactory(selected);
            return new ProductionStrongboxOpeningBindingV1(
                selected,
                OpeningService,
                command,
                EquipmentCatalog);
        }

        public ProductionResultsContextV1 RefreshAfterExactOpening(
            MissionRunStrongboxResultV1 selected,
            bool openingSucceeded)
        {
            bool exactSelection = false;
            for (int index = 0; index < Result.UnopenedStrongboxes.Count; index++)
            {
                if (ReferenceEquals(Result.UnopenedStrongboxes[index], selected))
                {
                    exactSelection = true;
                    break;
                }
            }

            if (!exactSelection)
            {
                throw new ArgumentException(
                    "The refreshed opening must reference the exact previously selected strongbox.",
                    nameof(selected));
            }

            MissionResultPayloadV1 refreshed = refreshResult();
            if (refreshed == null)
            {
                throw new InvalidOperationException(
                    "The authoritative Results refresh returned no payload.");
            }

            if (refreshed.RunStableId != Result.RunStableId
                || !refreshed.RoutePayload.Equals(Result.RoutePayload)
                || refreshed.Strongboxes.Count != Result.Strongboxes.Count)
            {
                throw new InvalidOperationException(
                    "The refreshed Results payload does not describe the same run, route and strongbox set.");
            }

            for (int index = 0; index < Result.Strongboxes.Count; index++)
            {
                MissionRunStrongboxResultV1 before = Result.Strongboxes[index];
                MissionRunStrongboxResultV1 after =
                    FindByInstance(refreshed, before.InstanceStableId);
                if (after == null)
                {
                    throw new InvalidOperationException(
                        "The refreshed Results payload lost a strongbox instance.");
                }

                if (ReferenceEquals(before, selected))
                {
                    if (openingSucceeded && after.IsUnopened)
                    {
                        throw new InvalidOperationException(
                            "A successful opening did not mark the exact selected strongbox opened.");
                    }

                    if (!openingSucceeded && !after.Equals(before))
                    {
                        throw new InvalidOperationException(
                            "A rejected opening changed the selected strongbox result.");
                    }

                    continue;
                }

                if (!after.Equals(before))
                {
                    throw new InvalidOperationException(
                        "Opening one strongbox changed a different strongbox result.");
                }
            }

            return new ProductionResultsContextV1(
                refreshed,
                OpeningService,
                commandFactory,
                EquipmentCatalog,
                refreshResult);
        }

        private static MissionRunStrongboxResultV1 FindByInstance(
            MissionResultPayloadV1 result,
            ShooterMover.Domain.Common.StableId instanceStableId)
        {
            for (int index = 0; index < result.Strongboxes.Count; index++)
            {
                if (result.Strongboxes[index].InstanceStableId
                    == instanceStableId)
                {
                    return result.Strongboxes[index];
                }
            }

            return null;
        }
    }
}

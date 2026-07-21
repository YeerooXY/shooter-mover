using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;

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
                    throw new ArgumentOutOfRangeException(
                        nameof(route),
                        route,
                        "No production scene is registered for the route.");
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
                throw new ArgumentException(
                    "A character display name is required.",
                    nameof(displayName));
            }

            DisplayName = displayName.Trim();
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The route payload fingerprint is invalid.",
                    nameof(payload));
            }
        }

        public string DisplayName { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }
    }

    public interface IProductionFlowProfileStoreV1
    {
        bool TryLoad(out ProductionFlowProfileRecordV1 record);

        bool TryLoad(int slotIndex, out ProductionFlowProfileRecordV1 record);

        void Save(ProductionFlowProfileRecordV1 record);

        void Save(int slotIndex, ProductionFlowProfileRecordV1 record);

        void Clear();
    }

    public sealed class InMemoryProductionFlowProfileStoreV1 :
        IProductionFlowProfileStoreV1
    {
        public const int ProfileSlotCount = 6;
        private readonly ProductionFlowProfileRecordV1[] records =
            new ProductionFlowProfileRecordV1[ProfileSlotCount];

        public bool TryLoad(out ProductionFlowProfileRecordV1 value)
        {
            return TryLoad(0, out value);
        }

        public bool TryLoad(
            int slotIndex,
            out ProductionFlowProfileRecordV1 value)
        {
            ValidateSlotIndex(slotIndex);
            value = records[slotIndex];
            return value != null;
        }

        public void Save(ProductionFlowProfileRecordV1 value)
        {
            Save(0, value);
        }

        public void Save(int slotIndex, ProductionFlowProfileRecordV1 value)
        {
            ValidateSlotIndex(slotIndex);
            records[slotIndex] = value
                ?? throw new ArgumentNullException(nameof(value));
        }

        public void Clear()
        {
            Array.Clear(records, 0, records.Length);
        }

        private static void ValidateSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= ProfileSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }
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
                throw new ArgumentException(
                    "A destination scene path is required.",
                    nameof(scenePath));
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
                throw new ArgumentException(
                    "The returned route payload is invalid.",
                    nameof(payload));
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
                    && navigation.CurrentRoute
                        == HubRouteV1.InventoryLoadoutHub)
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
    /// Immutable run/results context. Exact run facts remain RUN-owned. Opening is routed
    /// through the selected character's BOX authority when a character bridge is present;
    /// the character snapshot is persisted before the run projection is refreshed.
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
            RequireExactUnopenedSelection(selected);
            StrongboxOpeningServiceV1 service = ResolveOpeningService();
            StrongboxOpenCommandV1 command = commandFactory(selected);
            return new ProductionStrongboxOpeningBindingV1(
                selected,
                service,
                command,
                EquipmentCatalog);
        }

        public ProductionResultsContextV1 RefreshAfterExactOpening(
            MissionRunStrongboxResultV1 selected,
            bool openingSucceeded)
        {
            RequireExactUnopenedSelection(selected);
            if (openingSucceeded)
            {
                StrongboxOpeningServiceV1 characterAuthority;
                string bridgeError;
                if (!ProductionCharacterStrongboxBridgeRegistryV1.TryResolve(
                    out characterAuthority,
                    out bridgeError))
                {
                    throw new InvalidOperationException(
                        "The confirmed opening has no selected-character BOX authority: "
                            + bridgeError);
                }

                StrongboxOpeningSnapshotV1 characterSnapshot =
                    characterAuthority.ExportSnapshot();
                if (!ProductionCharacterStrongboxBridgeRegistryV1.TryPersist(
                    characterSnapshot.Fingerprint,
                    out bridgeError))
                {
                    throw new InvalidOperationException(
                        "The confirmed opening could not be persisted: "
                            + bridgeError);
                }

                if (!ReferenceEquals(characterAuthority, OpeningService))
                {
                    StrongboxOpeningSnapshotV1 runScope = ProjectRunScope(
                        characterSnapshot,
                        OpeningService.ExportSnapshot());
                    ImportOrThrow(
                        OpeningService,
                        runScope,
                        "run-strongbox-refresh-import-rejected");
                }
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

        private StrongboxOpeningServiceV1 ResolveOpeningService()
        {
            StrongboxOpeningServiceV1 characterAuthority;
            string rejectionCode;
            if (!ProductionCharacterStrongboxBridgeRegistryV1.TryResolve(
                out characterAuthority,
                out rejectionCode))
            {
                return OpeningService;
            }
            if (ReferenceEquals(characterAuthority, OpeningService))
            {
                return characterAuthority;
            }

            StrongboxOpeningSnapshotV1 merged = MergeSnapshots(
                characterAuthority.ExportSnapshot(),
                OpeningService.ExportSnapshot());
            ImportOrThrow(
                characterAuthority,
                merged,
                "character-strongbox-handoff-import-rejected");
            return characterAuthority;
        }

        private void RequireExactUnopenedSelection(
            MissionRunStrongboxResultV1 selected)
        {
            bool exactReference = false;
            for (int index = 0;
                index < Result.UnopenedStrongboxes.Count;
                index++)
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
        }

        private static StrongboxOpeningSnapshotV1 MergeSnapshots(
            StrongboxOpeningSnapshotV1 character,
            StrongboxOpeningSnapshotV1 run)
        {
            RequireCompatibleCatalogs(character, run);
            var contexts = new Dictionary<
                StableId,
                StrongboxInstanceContextV1>();
            AddContexts(contexts, character.Contexts);
            AddContexts(contexts, run.Contexts);

            var openings = new Dictionary<
                StableId,
                StrongboxOpeningRecordSnapshotV1>();
            AddOpenings(openings, character.Openings);
            AddOpenings(openings, run.Openings);
            return StrongboxOpeningSnapshotV1.CreateCanonical(
                character.DefinitionCatalogFingerprint,
                CountOpened(openings.Values),
                contexts.Values,
                openings.Values);
        }

        private static StrongboxOpeningSnapshotV1 ProjectRunScope(
            StrongboxOpeningSnapshotV1 character,
            StrongboxOpeningSnapshotV1 runScope)
        {
            RequireCompatibleCatalogs(character, runScope);
            var scopeIds = new HashSet<StableId>(
                runScope.Contexts.Select(item => item.InstanceStableId));
            var contexts = new List<StrongboxInstanceContextV1>();
            for (int index = 0; index < runScope.Contexts.Count; index++)
            {
                StableId instanceId = runScope.Contexts[index].InstanceStableId;
                StrongboxInstanceContextV1 current = character.Contexts
                    .FirstOrDefault(item => item.InstanceStableId == instanceId);
                if (current == null)
                {
                    throw new InvalidOperationException(
                        "The character BOX snapshot lost run context "
                            + instanceId + ".");
                }
                contexts.Add(current);
            }

            List<StrongboxOpeningRecordSnapshotV1> openings = character.Openings
                .Where(item => scopeIds.Contains(
                    item.Command.StrongboxInstanceStableId))
                .ToList();
            return StrongboxOpeningSnapshotV1.CreateCanonical(
                runScope.DefinitionCatalogFingerprint,
                CountOpened(openings),
                contexts,
                openings);
        }

        private static void AddContexts(
            IDictionary<StableId, StrongboxInstanceContextV1> output,
            IEnumerable<StrongboxInstanceContextV1> source)
        {
            foreach (StrongboxInstanceContextV1 item in source)
            {
                StrongboxInstanceContextV1 existing;
                if (output.TryGetValue(item.InstanceStableId, out existing))
                {
                    if (!string.Equals(
                        existing.Fingerprint,
                        item.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Strongbox context identity conflict: "
                                + item.InstanceStableId);
                    }
                    continue;
                }
                output.Add(item.InstanceStableId, item);
            }
        }

        private static void AddOpenings(
            IDictionary<StableId, StrongboxOpeningRecordSnapshotV1> output,
            IEnumerable<StrongboxOpeningRecordSnapshotV1> source)
        {
            foreach (StrongboxOpeningRecordSnapshotV1 item in source)
            {
                StableId openingId = item.Command.OpeningStableId;
                StrongboxOpeningRecordSnapshotV1 existing;
                if (output.TryGetValue(openingId, out existing))
                {
                    if (!string.Equals(
                        existing.Fingerprint,
                        item.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Strongbox opening identity conflict: "
                                + openingId);
                    }
                    continue;
                }
                output.Add(openingId, item);
            }
        }

        private static long CountOpened(
            IEnumerable<StrongboxOpeningRecordSnapshotV1> openings)
        {
            return openings.LongCount(item =>
                item.Stage == StrongboxOpeningStageV1.Opened);
        }

        private static void RequireCompatibleCatalogs(
            StrongboxOpeningSnapshotV1 left,
            StrongboxOpeningSnapshotV1 right)
        {
            if (left == null || right == null)
            {
                throw new ArgumentNullException(
                    left == null ? nameof(left) : nameof(right));
            }
            if (!string.Equals(
                left.DefinitionCatalogFingerprint,
                right.DefinitionCatalogFingerprint,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Strongbox catalog fingerprints do not match across the character/run handoff.");
            }
        }

        private static void ImportOrThrow(
            StrongboxOpeningServiceV1 authority,
            StrongboxOpeningSnapshotV1 snapshot,
            string rejectionPrefix)
        {
            StrongboxOpeningImportResultV1 imported =
                authority.ImportSnapshot(snapshot);
            if (!imported.Succeeded)
            {
                throw new InvalidOperationException(
                    rejectionPrefix + ":" + imported.RejectionCode);
            }
        }

        private static MissionRunStrongboxResultV1 FindByInstance(
            MissionResultPayloadV1 result,
            StableId instanceStableId)
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

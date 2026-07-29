using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Flow.Game
{
    public static class FlowScenePaths
    {
        public const string Bootstrap =
            "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity";
        public const string MainMenu =
            "Assets/ShooterMover/Scenes/Menu/MainMenu.unity";
        public const string CharacterSelection =
            "Assets/ShooterMover/Scenes/Flow/CharacterSelect/CharacterSelect.unity";
        public const string Hub =
            "Assets/ShooterMover/Scenes/Flow/Hub/HubFlow.unity";
        public const string PlaySelection =
            "Assets/ShooterMover/Scenes/Flow/PlaySelection/PlaySelection.unity";
        public const string LevelSelection =
            "Assets/ShooterMover/Scenes/Flow/LevelSelection/LevelSelection.unity";
        public const string Inventory =
            "Assets/ShooterMover/Scenes/Flow/InventoryLoadout/InventoryLoadout.unity";
        public const string Skills =
            "Assets/ShooterMover/Scenes/Flow/Skills/Skills.unity";
        public const string Shop =
            "Assets/ShooterMover/Scenes/Flow/Shop/Shop.unity";
        public const string Crafting =
            "Assets/ShooterMover/Scenes/Flow/Crafting/Crafting.unity";
        public const string Results =
            "Assets/ShooterMover/Scenes/Flow/Results/Results.unity";
        public const string StrongboxOpening =
            "Assets/ShooterMover/Scenes/StrongboxOpening/StrongboxOpening.unity";

        public static string ForHubRoute(HubRoute route)
        {
            switch (route)
            {
                case HubRoute.MainMenu:
                    return MainMenu;
                case HubRoute.CharacterSelect:
                    return CharacterSelection;
                case HubRoute.InventoryLoadoutHub:
                    return Hub;
                case HubRoute.Inventory:
                    return Inventory;
                case HubRoute.Skills:
                    return Skills;
                case HubRoute.Shop:
                    return Shop;
                case HubRoute.Crafting:
                    return Crafting;
                case HubRoute.Play:
                    return PlaySelection;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(route),
                        route,
                        "No production scene is registered for the route.");
            }
        }
    }

    public sealed class FlowProfileRecord
    {
        public FlowProfileRecord(
            string displayName,
            PlayerRouteProfilePayload payload)
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

        public PlayerRouteProfilePayload Payload { get; }
    }

    public interface IFlowProfileStore
    {
        bool TryLoad(out FlowProfileRecord record);

        bool TryLoad(int slotIndex, out FlowProfileRecord record);

        void Save(FlowProfileRecord record);

        void Save(int slotIndex, FlowProfileRecord record);

        void Clear();
    }

    public sealed class InMemoryFlowProfileStore :
        IFlowProfileStore
    {
        public const int ProfileSlotCount = 6;
        private readonly FlowProfileRecord[] records =
            new FlowProfileRecord[ProfileSlotCount];

        public bool TryLoad(out FlowProfileRecord value)
        {
            return TryLoad(0, out value);
        }

        public bool TryLoad(
            int slotIndex,
            out FlowProfileRecord value)
        {
            ValidateSlotIndex(slotIndex);
            value = records[slotIndex];
            return value != null;
        }

        public void Save(FlowProfileRecord value)
        {
            Save(0, value);
        }

        public void Save(int slotIndex, FlowProfileRecord value)
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

    public interface ISceneLoadPort
    {
        bool BeginLoad(string scenePath);
    }

    /// <summary>
    /// Transaction boundary around the existing HubNavigationActions. This type owns
    /// only one in-flight Unity scene request. Route semantics, history and payload
    /// identity remain owned by HubNavigationActions.
    /// </summary>
    public sealed class SceneTransitionFlow :
        IHubRouteTransactionPort
    {
        private readonly ISceneLoadPort sceneLoader;
        private HubNavigationActions navigation;
        private string pendingScenePath;

        public SceneTransitionFlow(
            HubNavigationActions navigation,
            ISceneLoadPort sceneLoader)
        {
            this.navigation = navigation
                ?? throw new ArgumentNullException(nameof(navigation));
            this.sceneLoader = sceneLoader
                ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public HubNavigationActions Navigation
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

        public bool TryNavigateTo(HubRoute route)
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

            string scenePath = FlowScenePaths.ForHubRoute(route);
            if (!TryBegin(scenePath))
            {
                return false;
            }

            HubNavigationResult result = navigation.TryNavigateTo(route);
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

            HubRoute target;
            if (!navigation.TryPeekBackRoute(out target))
            {
                return false;
            }
            if (!TryBegin(FlowScenePaths.ForHubRoute(target)))
            {
                return false;
            }

            HubNavigationResult result = navigation.NavigateBack();
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

        public bool TryReturnToHub(PlayerRouteProfilePayload payload)
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
            if (!TryBegin(FlowScenePaths.Hub))
            {
                return false;
            }

            if (ReferenceEquals(payload, navigation.Payload)
                || payload.Equals(navigation.Payload))
            {
                HubNavigationResult back = navigation.NavigateBack();
                if (back.Changed
                    && navigation.CurrentRoute
                        == HubRoute.InventoryLoadoutHub)
                {
                    return true;
                }
            }

            ReplaceWithHubNavigation(payload);
            return true;
        }

        public void ReplaceAtMainMenu(PlayerRouteProfilePayload payload)
        {
            if (IsTransitionPending)
            {
                throw new InvalidOperationException(
                    "Navigation cannot be replaced while a scene transition is pending.");
            }
            navigation = new HubNavigationActions(
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
            PlayerRouteProfilePayload payload)
        {
            HubNavigationActions replacement =
                new HubNavigationActions(payload);
            HubNavigationResult character =
                replacement.TryNavigateTo(HubRoute.CharacterSelect);
            HubNavigationResult hub =
                replacement.TryNavigateTo(HubRoute.InventoryLoadoutHub);
            if (!character.Changed || !hub.Changed)
            {
                pendingScenePath = null;
                throw new InvalidOperationException(
                    "Unable to rebuild the canonical route at Hub.");
            }
            navigation = replacement;
        }
    }

    public sealed class StrongboxOpeningBinding
    {
        public StrongboxOpeningBinding(
            MissionRunStrongboxResult selectedStrongbox,
            StrongboxOpeningActions openingService,
            StrongboxOpenCommand command,
            EquipmentCatalog equipmentCatalog,
            IStrongboxDurableOpeningExecutor durableOpeningExecutor = null)
            : this(
                selectedStrongbox,
                openingService,
                command,
                equipmentCatalog,
                null,
                durableOpeningExecutor)
        {
        }

        public StrongboxOpeningBinding(
            MissionRunStrongboxResult selectedStrongbox,
            StrongboxOpeningActions openingService,
            StrongboxOpenCommand command,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            IStrongboxDurableOpeningExecutor durableOpeningExecutor)
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
            GunCatalog = gunCatalog;
            DurableOpeningExecutor = durableOpeningExecutor;
        }

        public MissionRunStrongboxResult SelectedStrongbox { get; }

        public StrongboxOpeningActions OpeningService { get; }

        public StrongboxOpenCommand Command { get; }

        public EquipmentCatalog EquipmentCatalog { get; }

        public GunCatalog GunCatalog { get; }

        public IStrongboxDurableOpeningExecutor DurableOpeningExecutor { get; }
    }

    /// <summary>
    /// Immutable run/results context. Exact run facts remain RUN-owned. With the Unity
    /// character bridge installed, opening is executed by the selected character's BOX
    /// authority and durably persisted before Results refresh. Engine-neutral callers
    /// without that bridge retain the original supplied BOX service behavior.
    /// </summary>
    public sealed class ResultsContext
    {
        private readonly Func<MissionRunStrongboxResult, StrongboxOpenCommand>
            commandFactory;
        private readonly Func<MissionResultPayload> refreshResult;

        public ResultsContext(
            MissionResultPayload result,
            StrongboxOpeningActions openingService,
            Func<MissionRunStrongboxResult, StrongboxOpenCommand>
                commandFactory,
            EquipmentCatalog equipmentCatalog,
            Func<MissionResultPayload> refreshResult)
            : this(
                result,
                openingService,
                commandFactory,
                equipmentCatalog,
                null,
                refreshResult)
        {
        }

        public ResultsContext(
            MissionResultPayload result,
            StrongboxOpeningActions openingService,
            Func<MissionRunStrongboxResult, StrongboxOpenCommand>
                commandFactory,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            Func<MissionResultPayload> refreshResult)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            OpeningService = openingService
                ?? throw new ArgumentNullException(nameof(openingService));
            this.commandFactory = commandFactory
                ?? throw new ArgumentNullException(nameof(commandFactory));
            EquipmentCatalog = equipmentCatalog;
            GunCatalog = gunCatalog;
            this.refreshResult = refreshResult
                ?? throw new ArgumentNullException(nameof(refreshResult));
        }

        public MissionResultPayload Result { get; }

        public StrongboxOpeningActions OpeningService { get; }

        public EquipmentCatalog EquipmentCatalog { get; }

        public GunCatalog GunCatalog { get; }

        public StrongboxOpeningBinding BindExact(
            MissionRunStrongboxResult selected)
        {
            RequireExactUnopenedSelection(selected);
            StrongboxOpeningActions authority = ResolveOpeningService();
            IStrongboxDurableOpeningExecutor durableOpeningExecutor = null;
            if (CharacterStrongboxesRegistry.IsConfigured
                && !CharacterStrongboxesRegistry
                    .TryResolveDurableOpeningExecutor(
                        out durableOpeningExecutor,
                        out string rejectionCode))
            {
                throw new InvalidOperationException(
                    "The selected-character durable BOX executor is unavailable: "
                    + rejectionCode);
            }
            return new StrongboxOpeningBinding(
                selected,
                authority,
                commandFactory(selected),
                EquipmentCatalog,
                GunCatalog,
                durableOpeningExecutor);
        }

        public ResultsContext RefreshAfterExactOpening(
            MissionRunStrongboxResult selected,
            bool openingSucceeded,
            bool durablePersistenceAlreadyCompleted = false)
        {
            RequireExactUnopenedSelection(selected);
            if (openingSucceeded
                && CharacterStrongboxesRegistry.IsConfigured)
            {
                if (!durablePersistenceAlreadyCompleted)
                {
                    PersistCharacterOpening();
                }

                // Durable opening already persists the selected-character state,
                // but Results is projected from the run-local authority. Always
                // synchronize that projection before refreshing Results.
                SynchronizeOpeningToRunScope();
            }

            MissionResultPayload refreshed = refreshResult();
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
                MissionRunStrongboxResult before = Result.Strongboxes[index];
                MissionRunStrongboxResult after =
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

            return new ResultsContext(
                refreshed,
                OpeningService,
                commandFactory,
                EquipmentCatalog,
                GunCatalog,
                refreshResult);
        }

        private StrongboxOpeningActions ResolveOpeningService()
        {
            if (!CharacterStrongboxesRegistry.IsConfigured)
            {
                return OpeningService;
            }

            StrongboxOpeningActions characterAuthority;
            string rejectionCode;
            if (!CharacterStrongboxesRegistry.TryResolve(
                out characterAuthority,
                out rejectionCode))
            {
                throw new InvalidOperationException(
                    "The selected-character BOX authority is unavailable: "
                        + rejectionCode);
            }
            if (ReferenceEquals(characterAuthority, OpeningService))
            {
                return characterAuthority;
            }

            ImportOrThrow(
                characterAuthority,
                MergeSnapshots(
                    characterAuthority.ExportSnapshot(),
                    OpeningService.ExportSnapshot()),
                "character-strongbox-handoff-import-rejected");
            return characterAuthority;
        }

        private void PersistCharacterOpening()
        {
            StrongboxOpeningActions characterAuthority;
            string rejectionCode;
            if (!CharacterStrongboxesRegistry.TryResolve(
                out characterAuthority,
                out rejectionCode))
            {
                throw new InvalidOperationException(
                    "The confirmed opening has no selected-character BOX authority: "
                        + rejectionCode);
            }

            StrongboxOpeningSnapshot characterSnapshot =
                characterAuthority.ExportSnapshot();
            if (!CharacterStrongboxesRegistry.TryPersist(
                characterSnapshot.Fingerprint,
                out rejectionCode))
            {
                throw new InvalidOperationException(
                    "The confirmed opening could not be persisted: "
                        + rejectionCode);
            }

        }

        private void SynchronizeOpeningToRunScope()
        {
            StrongboxOpeningActions characterAuthority;
            string rejectionCode;
            if (!CharacterStrongboxesRegistry.TryResolve(
                out characterAuthority,
                out rejectionCode))
            {
                throw new InvalidOperationException(
                    "The confirmed opening has no selected-character BOX authority: "
                        + rejectionCode);
            }

            if (!ReferenceEquals(characterAuthority, OpeningService))
            {
                StrongboxOpeningSnapshot characterSnapshot =
                    characterAuthority.ExportSnapshot();
                ImportOrThrow(
                    OpeningService,
                    ProjectRunScope(
                        characterSnapshot,
                        OpeningService.ExportSnapshot()),
                    "run-strongbox-refresh-import-rejected");
            }
        }

        private void RequireExactUnopenedSelection(
            MissionRunStrongboxResult selected)
        {
            for (int index = 0;
                index < Result.UnopenedStrongboxes.Count;
                index++)
            {
                if (ReferenceEquals(Result.UnopenedStrongboxes[index], selected))
                {
                    return;
                }
            }
            throw new ArgumentException(
                "The selected strongbox must be the exact unopened result object.",
                nameof(selected));
        }

        private static StrongboxOpeningSnapshot MergeSnapshots(
            StrongboxOpeningSnapshot character,
            StrongboxOpeningSnapshot run)
        {
            RequireCompatibleCatalogs(character, run);
            var contexts = new Dictionary<StableId, StrongboxInstanceContext>();
            AddContexts(contexts, character.Contexts);
            AddContexts(contexts, run.Contexts);
            var openings = new Dictionary<
                StableId,
                StrongboxOpeningRecordSnapshot>();
            AddOpenings(openings, character.Openings);
            AddOpenings(openings, run.Openings);
            return StrongboxOpeningSnapshot.CreateCanonical(
                character.DefinitionCatalogFingerprint,
                CountOpened(openings.Values),
                contexts.Values,
                openings.Values);
        }

        private static StrongboxOpeningSnapshot ProjectRunScope(
            StrongboxOpeningSnapshot character,
            StrongboxOpeningSnapshot runScope)
        {
            RequireCompatibleCatalogs(character, runScope);
            var scopeIds = new HashSet<StableId>(
                runScope.Contexts.Select(item => item.InstanceStableId));
            var contexts = new List<StrongboxInstanceContext>();
            for (int index = 0; index < runScope.Contexts.Count; index++)
            {
                StableId instanceId = runScope.Contexts[index].InstanceStableId;
                StrongboxInstanceContext current = character.Contexts
                    .FirstOrDefault(item => item.InstanceStableId == instanceId);
                if (current == null)
                {
                    throw new InvalidOperationException(
                        "The character BOX snapshot lost run context "
                            + instanceId + ".");
                }
                contexts.Add(current);
            }
            List<StrongboxOpeningRecordSnapshot> openings = character.Openings
                .Where(item => scopeIds.Contains(
                    item.Command.StrongboxInstanceStableId))
                .ToList();
            return StrongboxOpeningSnapshot.CreateCanonical(
                runScope.DefinitionCatalogFingerprint,
                CountOpened(openings),
                contexts,
                openings);
        }

        private static void AddContexts(
            IDictionary<StableId, StrongboxInstanceContext> output,
            IEnumerable<StrongboxInstanceContext> source)
        {
            foreach (StrongboxInstanceContext item in source)
            {
                StrongboxInstanceContext existing;
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
            IDictionary<StableId, StrongboxOpeningRecordSnapshot> output,
            IEnumerable<StrongboxOpeningRecordSnapshot> source)
        {
            foreach (StrongboxOpeningRecordSnapshot item in source)
            {
                StableId openingId = item.Command.OpeningStableId;
                StrongboxOpeningRecordSnapshot existing;
                if (output.TryGetValue(openingId, out existing))
                {
                    if (!string.Equals(
                        existing.Fingerprint,
                        item.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Strongbox opening identity conflict: " + openingId);
                    }
                    continue;
                }
                output.Add(openingId, item);
            }
        }

        private static long CountOpened(
            IEnumerable<StrongboxOpeningRecordSnapshot> openings)
        {
            return openings.LongCount(item =>
                item.Stage == StrongboxOpeningStage.Opened);
        }

        private static void RequireCompatibleCatalogs(
            StrongboxOpeningSnapshot left,
            StrongboxOpeningSnapshot right)
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
            StrongboxOpeningActions authority,
            StrongboxOpeningSnapshot snapshot,
            string rejectionPrefix)
        {
            StrongboxOpeningImportResult imported =
                authority.ImportSnapshot(snapshot);
            if (!imported.Succeeded)
            {
                throw new InvalidOperationException(
                    rejectionPrefix + ":" + imported.RejectionCode);
            }
        }

        private static MissionRunStrongboxResult FindByInstance(
            MissionResultPayload result,
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

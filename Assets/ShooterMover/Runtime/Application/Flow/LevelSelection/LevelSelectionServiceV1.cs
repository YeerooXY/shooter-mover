using System;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.LevelSelection
{
    /// <summary>
    /// Engine-independent route decision owner. It consumes level metadata and retains
    /// the exact incoming immutable profile/loadout payload and selected play mode.
    /// It does not mutate XP, inventory, equipment, rewards, wallets, or gameplay.
    /// </summary>
    public sealed class LevelSelectionServiceV1
    {
        public const string PlaySelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/PlaySelection/PlaySelection.unity";

        private readonly PlayerRouteProfilePayloadV1 payload;
        private readonly StableId selectedModeStableId;
        private readonly LevelSelectionCatalogV1 catalog;
        private LevelSelectionResultV1 terminalResult;

        public LevelSelectionServiceV1(
            PlayerRouteProfilePayloadV1 payload,
            StableId selectedModeStableId,
            LevelSelectionCatalogV1 catalog)
        {
            this.payload = payload;
            this.selectedModeStableId = selectedModeStableId;
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public PlayerRouteProfilePayloadV1 Payload
        {
            get { return payload; }
        }

        public StableId SelectedModeStableId
        {
            get { return selectedModeStableId; }
        }

        public LevelSelectionCatalogV1 Catalog
        {
            get { return catalog; }
        }

        public bool IsInputLocked
        {
            get { return terminalResult != null; }
        }

        public LevelSelectionResultV1 TerminalResult
        {
            get { return terminalResult; }
        }

        public LevelSelectionResultV1 SelectLevel(StableId levelStableId)
        {
            if (terminalResult != null)
            {
                return Result(
                    LevelSelectionStatusV1.InputLocked,
                    LevelSelectionRouteV1.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-input-locked");
            }

            if (!HasValidContext())
            {
                return Result(
                    LevelSelectionStatusV1.InvalidContext,
                    LevelSelectionRouteV1.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-context-invalid");
            }

            LevelSelectionDefinitionV1 definition;
            if (!catalog.TryGet(levelStableId, out definition))
            {
                return Result(
                    LevelSelectionStatusV1.UnknownLevel,
                    LevelSelectionRouteV1.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-level-unknown");
            }

            if (definition.Availability != LevelAvailabilityV1.Unlocked)
            {
                return Result(
                    LevelSelectionStatusV1.LevelLocked,
                    LevelSelectionRouteV1.None,
                    definition.LevelStableId,
                    definition.ScenePath,
                    "level-selection-level-locked");
            }

            LevelSelectionRouteV1 route =
                definition.RouteKind == LevelRouteKindV1.Gameplay
                    ? LevelSelectionRouteV1.GameplayScene
                    : LevelSelectionRouteV1.PrototypeScene;

            terminalResult = Result(
                LevelSelectionStatusV1.RouteEmitted,
                route,
                definition.LevelStableId,
                definition.ScenePath,
                string.Empty);
            return terminalResult;
        }

        public LevelSelectionResultV1 NavigateBack()
        {
            if (terminalResult != null)
            {
                return Result(
                    LevelSelectionStatusV1.InputLocked,
                    LevelSelectionRouteV1.None,
                    null,
                    string.Empty,
                    "level-selection-input-locked");
            }

            if (!HasValidContext())
            {
                return Result(
                    LevelSelectionStatusV1.InvalidContext,
                    LevelSelectionRouteV1.None,
                    null,
                    string.Empty,
                    "level-selection-context-invalid");
            }

            terminalResult = Result(
                LevelSelectionStatusV1.RouteEmitted,
                LevelSelectionRouteV1.PlaySelection,
                null,
                PlaySelectionScenePath,
                string.Empty);
            return terminalResult;
        }

        private bool HasValidContext()
        {
            return payload != null
                && payload.HasValidFingerprint()
                && selectedModeStableId != null;
        }

        private LevelSelectionResultV1 Result(
            LevelSelectionStatusV1 status,
            LevelSelectionRouteV1 route,
            StableId selectedLevelStableId,
            string destinationScenePath,
            string feedbackCode)
        {
            return new LevelSelectionResultV1(
                status,
                route,
                selectedModeStableId,
                selectedLevelStableId,
                payload,
                destinationScenePath,
                feedbackCode);
        }
    }
}

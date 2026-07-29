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
    public sealed class LevelSelectionActions
    {
        public const string PlaySelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/PlaySelection/PlaySelection.unity";

        private readonly PlayerRouteProfilePayload payload;
        private readonly StableId selectedModeStableId;
        private readonly LevelSelectionCatalog catalog;
        private LevelSelectionResult terminalResult;

        public LevelSelectionActions(
            PlayerRouteProfilePayload payload,
            StableId selectedModeStableId,
            LevelSelectionCatalog catalog)
        {
            this.payload = payload;
            this.selectedModeStableId = selectedModeStableId;
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public PlayerRouteProfilePayload Payload
        {
            get { return payload; }
        }

        public StableId SelectedModeStableId
        {
            get { return selectedModeStableId; }
        }

        public LevelSelectionCatalog Catalog
        {
            get { return catalog; }
        }

        public bool IsInputLocked
        {
            get { return terminalResult != null; }
        }

        public LevelSelectionResult TerminalResult
        {
            get { return terminalResult; }
        }

        public LevelSelectionResult SelectLevel(StableId levelStableId)
        {
            if (terminalResult != null)
            {
                return Result(
                    LevelSelectionStatus.InputLocked,
                    LevelSelectionRoute.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-input-locked");
            }

            if (!HasValidContext())
            {
                return Result(
                    LevelSelectionStatus.InvalidContext,
                    LevelSelectionRoute.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-context-invalid");
            }

            LevelSelectionDefinition definition;
            if (!catalog.TryGet(levelStableId, out definition))
            {
                return Result(
                    LevelSelectionStatus.UnknownLevel,
                    LevelSelectionRoute.None,
                    levelStableId,
                    string.Empty,
                    "level-selection-level-unknown");
            }

            if (definition.Availability != LevelAvailability.Unlocked)
            {
                return Result(
                    LevelSelectionStatus.LevelLocked,
                    LevelSelectionRoute.None,
                    definition.LevelStableId,
                    definition.ScenePath,
                    "level-selection-level-locked");
            }

            LevelSelectionRoute route =
                definition.RouteKind == LevelRouteKind.Gameplay
                    ? LevelSelectionRoute.GameplayScene
                    : LevelSelectionRoute.PrototypeScene;

            terminalResult = Result(
                LevelSelectionStatus.RouteEmitted,
                route,
                definition.LevelStableId,
                definition.ScenePath,
                string.Empty);
            return terminalResult;
        }

        public LevelSelectionResult NavigateBack()
        {
            if (terminalResult != null)
            {
                return Result(
                    LevelSelectionStatus.InputLocked,
                    LevelSelectionRoute.None,
                    null,
                    string.Empty,
                    "level-selection-input-locked");
            }

            if (!HasValidContext())
            {
                return Result(
                    LevelSelectionStatus.InvalidContext,
                    LevelSelectionRoute.None,
                    null,
                    string.Empty,
                    "level-selection-context-invalid");
            }

            terminalResult = Result(
                LevelSelectionStatus.RouteEmitted,
                LevelSelectionRoute.PlaySelection,
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

        private LevelSelectionResult Result(
            LevelSelectionStatus status,
            LevelSelectionRoute route,
            StableId selectedLevelStableId,
            string destinationScenePath,
            string feedbackCode)
        {
            return new LevelSelectionResult(
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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Crafting;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.Application.Crafting.Presentation
{
    public enum CraftingRecipeAvailability
    {
        Locked = 1,
        Available = 2,
        InsufficientScrap = 3,
        InvalidTarget = 4,
        PreviewUnavailable = 5,
    }

    public enum CraftingScreenStatus
    {
        Ready = 1,
        Refreshed = 2,
        SelectionChanged = 3,
        PreviewReady = 4,
        PreviewRejected = 5,
        Locked = 6,
        InsufficientScrap = 7,
        Crafted = 8,
        ExactDuplicateNoChange = 9,
        RetryRequired = 10,
        ConflictingDuplicate = 11,
        Rejected = 12,
        AlreadyResolved = 13,
        RetryNotAvailable = 14,
        NoSelection = 15,
        ResultMismatch = 16,
        Cancelled = 17,
        AlreadyClosed = 18,
    }

    /// <summary>
    /// Read-only projection of the existing crafting, scrap, holdings, and equipment
    /// authorities. The screen never mutates these values directly.
    /// </summary>
    public sealed class CraftingPresentationStateSnapshot
    {
        public CraftingPresentationStateSnapshot(
            long scrapBalance,
            long scrapSequence,
            long holdingsSequence,
            CraftingRecipeCatalog recipeCatalog,
            EquipmentCatalog equipmentCatalog,
            string fingerprint)
        {
            if (scrapBalance < 0L) throw new ArgumentOutOfRangeException(nameof(scrapBalance));
            if (scrapSequence < 0L) throw new ArgumentOutOfRangeException(nameof(scrapSequence));
            if (holdingsSequence < 0L) throw new ArgumentOutOfRangeException(nameof(holdingsSequence));

            ScrapBalance = scrapBalance;
            ScrapSequence = scrapSequence;
            HoldingsSequence = holdingsSequence;
            RecipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
            EquipmentCatalog = equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            Fingerprint = fingerprint ?? string.Empty;
        }

        public long ScrapBalance { get; }
        public long ScrapSequence { get; }
        public long HoldingsSequence { get; }
        public CraftingRecipeCatalog RecipeCatalog { get; }
        public EquipmentCatalog EquipmentCatalog { get; }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Public presentation-safe shape for CRA-001 terminal and retry facts. It carries
    /// the exact immutable equipment instance returned by the authoritative operation.
    /// </summary>
    public sealed class CraftingPresentationStateResult
    {
        public CraftingPresentationStateResult(
            CraftingResultStatus status,
            StableId recipeStableId,
            int? unlockLevel,
            long scrapCost,
            EquipmentInstance equipment,
            string commandFingerprint,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(CraftingResultStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (scrapCost < 0L) throw new ArgumentOutOfRangeException(nameof(scrapCost));

            Status = status;
            RecipeStableId = recipeStableId;
            UnlockLevel = unlockLevel;
            ScrapCost = scrapCost;
            Equipment = equipment;
            CommandFingerprint = commandFingerprint ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public CraftingResultStatus Status { get; }
        public StableId RecipeStableId { get; }
        public int? UnlockLevel { get; }
        public long ScrapCost { get; }
        public EquipmentInstance Equipment { get; }
        public string CommandFingerprint { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == CraftingResultStatus.Crafted
                    || Status == CraftingResultStatus.ExactDuplicateNoChange;
            }
        }

        public static CraftingPresentationStateResult FromAuthority(
            CraftingResult source)
        {
            if (source == null) return null;
            return new CraftingPresentationStateResult(
                source.Status,
                source.RecipeStableId,
                source.UnlockLevel,
                source.ScrapCost,
                source.Equipment,
                source.CommandFingerprint,
                source.RejectionCode);
        }
    }

    /// <summary>
    /// Composition boundary for CRAFTUI-001. Preview must be read-only and must use the
    /// same deterministic generation inputs as Craft. Craft delegates to CRA-001.
    /// </summary>
    public interface ICraftingPresentationStatePort
    {
        CraftingPresentationStateSnapshot ExportSnapshot();
        CraftingPresentationStateResult Preview(CraftEquipmentCommand command);
        CraftingPresentationStateResult Craft(CraftEquipmentCommand command);
    }

    /// <summary>
    /// Production adapter for an existing CraftingActions. The preview delegate is
    /// intentionally injected so composition can run CRA/GEN against cloned snapshots
    /// without copying generation rules into presentation code.
    /// </summary>
    public sealed class CraftingActionsPresentationStatePort :
        ICraftingPresentationStatePort
    {
        private readonly Func<CraftingPresentationStateSnapshot> snapshotExporter;
        private readonly Func<CraftEquipmentCommand, CraftingPresentationStateResult> preview;
        private readonly CraftingActions craftingService;

        public CraftingActionsPresentationStatePort(
            Func<CraftingPresentationStateSnapshot> snapshotExporter,
            Func<CraftEquipmentCommand, CraftingPresentationStateResult> preview,
            CraftingActions craftingService)
        {
            this.snapshotExporter = snapshotExporter
                ?? throw new ArgumentNullException(nameof(snapshotExporter));
            this.preview = preview ?? throw new ArgumentNullException(nameof(preview));
            this.craftingService = craftingService
                ?? throw new ArgumentNullException(nameof(craftingService));
        }

        public CraftingPresentationStateSnapshot ExportSnapshot()
        {
            CraftingPresentationStateSnapshot snapshot = snapshotExporter();
            return snapshot ?? throw new InvalidOperationException(
                "The crafting presentation snapshot exporter returned null.");
        }

        public CraftingPresentationStateResult Preview(
            CraftEquipmentCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            CraftingPresentationStateResult result = preview(command);
            return result ?? throw new InvalidOperationException(
                "The crafting preview delegate returned null.");
        }

        public CraftingPresentationStateResult Craft(
            CraftEquipmentCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return CraftingPresentationStateResult.FromAuthority(
                craftingService.Craft(command));
        }
    }

    public sealed class CraftingRecipeView
    {
        public CraftingRecipeView(
            StableId recipeStableId,
            StableId targetEquipmentDefinitionStableId,
            string targetDisplayName,
            StableId targetCategoryStableId,
            int naturalDiscoveryLevel,
            int craftingUnlockLevel,
            int characterLevel,
            long scrapCost,
            long scrapBalance,
            CraftingRecipeAvailability availability,
            int attemptOrdinal,
            CraftEquipmentCommand command,
            EquipmentInstance previewEquipment,
            string previewRejectionCode,
            bool isAttemptResolved,
            bool isRetryPending)
        {
            RecipeStableId = recipeStableId
                ?? throw new ArgumentNullException(nameof(recipeStableId));
            TargetEquipmentDefinitionStableId = targetEquipmentDefinitionStableId
                ?? throw new ArgumentNullException(nameof(targetEquipmentDefinitionStableId));
            TargetDisplayName = string.IsNullOrWhiteSpace(targetDisplayName)
                ? targetEquipmentDefinitionStableId.ToString()
                : targetDisplayName.Trim();
            TargetCategoryStableId = targetCategoryStableId;
            NaturalDiscoveryLevel = naturalDiscoveryLevel;
            CraftingUnlockLevel = craftingUnlockLevel;
            CharacterLevel = characterLevel;
            ScrapCost = scrapCost;
            ScrapBalance = scrapBalance;
            Availability = availability;
            AttemptOrdinal = attemptOrdinal;
            Command = command;
            PreviewEquipment = previewEquipment;
            PreviewRejectionCode = previewRejectionCode ?? string.Empty;
            IsAttemptResolved = isAttemptResolved;
            IsRetryPending = isRetryPending;
        }

        public StableId RecipeStableId { get; }
        public StableId TargetEquipmentDefinitionStableId { get; }
        public string TargetDisplayName { get; }
        public StableId TargetCategoryStableId { get; }
        public int NaturalDiscoveryLevel { get; }
        public int CraftingUnlockLevel { get; }
        public int CharacterLevel { get; }
        public long ScrapCost { get; }
        public long ScrapBalance { get; }
        public CraftingRecipeAvailability Availability { get; }
        public int AttemptOrdinal { get; }
        public CraftEquipmentCommand Command { get; }
        public EquipmentInstance PreviewEquipment { get; }
        public string PreviewRejectionCode { get; }
        public bool IsAttemptResolved { get; }
        public bool IsRetryPending { get; }
        public bool IsLocked { get { return Availability == CraftingRecipeAvailability.Locked; } }
        public bool CanCraft
        {
            get
            {
                return Availability == CraftingRecipeAvailability.Available
                    && !IsAttemptResolved
                    && !IsRetryPending;
            }
        }
        public bool HasPreview { get { return PreviewEquipment != null; } }
    }

    public sealed class CraftingScreenSnapshot
    {
        private readonly ReadOnlyCollection<CraftingRecipeView> recipes;

        public CraftingScreenSnapshot(
            PlayerRouteProfilePayload incomingRoutePayload,
            long scrapBalance,
            long scrapSequence,
            long holdingsSequence,
            string authorityFingerprint,
            IEnumerable<CraftingRecipeView> recipes,
            StableId selectedRecipeStableId,
            CraftingPresentationStateResult lastAuthorityResult,
            bool isClosed)
        {
            IncomingRoutePayload = incomingRoutePayload
                ?? throw new ArgumentNullException(nameof(incomingRoutePayload));
            ScrapBalance = scrapBalance;
            ScrapSequence = scrapSequence;
            HoldingsSequence = holdingsSequence;
            AuthorityFingerprint = authorityFingerprint ?? string.Empty;
            this.recipes = new ReadOnlyCollection<CraftingRecipeView>(
                new List<CraftingRecipeView>(
                    recipes ?? throw new ArgumentNullException(nameof(recipes))));
            SelectedRecipeStableId = selectedRecipeStableId;
            LastAuthorityResult = lastAuthorityResult;
            IsClosed = isClosed;
        }

        public PlayerRouteProfilePayload IncomingRoutePayload { get; }
        public long ScrapBalance { get; }
        public long ScrapSequence { get; }
        public long HoldingsSequence { get; }
        public string AuthorityFingerprint { get; }
        public IReadOnlyList<CraftingRecipeView> Recipes { get { return recipes; } }
        public StableId SelectedRecipeStableId { get; }
        public CraftingPresentationStateResult LastAuthorityResult { get; }
        public bool IsClosed { get; }

        public CraftingRecipeView SelectedRecipe
        {
            get { return FindRecipe(SelectedRecipeStableId); }
        }

        public CraftingRecipeView FindRecipe(StableId recipeStableId)
        {
            if (recipeStableId == null) return null;
            for (int index = 0; index < recipes.Count; index++)
            {
                if (recipes[index].RecipeStableId == recipeStableId) return recipes[index];
            }
            return null;
        }
    }

    public sealed class CraftingScreenResult
    {
        public CraftingScreenResult(
            CraftingScreenStatus status,
            string rejectionCode,
            CraftingScreenSnapshot snapshot,
            CraftingPresentationStateResult authorityResult,
            PlayerRouteProfilePayload routePayload)
        {
            if (!Enum.IsDefined(typeof(CraftingScreenStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            AuthorityResult = authorityResult;
            RoutePayload = routePayload;
        }

        public CraftingScreenStatus Status { get; }
        public string RejectionCode { get; }
        public CraftingScreenSnapshot Snapshot { get; }
        public CraftingPresentationStateResult AuthorityResult { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public bool LeavesScreen { get { return Status == CraftingScreenStatus.Cancelled; } }
    }

    /// <summary>
    /// Engine-independent crafting-screen state. It owns selection and operation-attempt
    /// presentation only. Scrap, holdings, recipes, generation, and result application
    /// remain authoritative behind ICraftingPresentationStatePort.
    /// </summary>
    public sealed class CraftingScreenActions
    {
        private sealed class AttemptState
        {
            public AttemptState(int ordinal)
            {
                Ordinal = ordinal;
            }

            public int Ordinal;
            public CraftEquipmentCommand Command;
            public CraftingPresentationStateResult Preview;
            public CraftingPresentationStateResult LastExecution;
            public bool RetryPending;
            public bool Terminal;
        }

        private readonly PlayerRouteProfilePayload incomingRoutePayload;
        private readonly ProgressionContext progressionContext;
        private readonly ulong rootSeed;
        private readonly StableId screenSessionStableId;
        private readonly StableId runStableId;
        private readonly StableId claimantStableId;
        private readonly ICraftingPresentationStatePort authority;
        private readonly Dictionary<StableId, AttemptState> attempts =
            new Dictionary<StableId, AttemptState>();

        private CraftingPresentationStateSnapshot authoritySnapshot;
        private CraftingScreenSnapshot snapshot;
        private StableId selectedRecipeStableId;
        private CraftingPresentationStateResult lastAuthorityResult;
        private bool closed;

        public CraftingScreenActions(
            PlayerRouteProfilePayload incomingRoutePayload,
            ProgressionContext progressionContext,
            ulong rootSeed,
            StableId screenSessionStableId,
            StableId runStableId,
            StableId claimantStableId,
            ICraftingPresentationStatePort authority)
        {
            this.incomingRoutePayload = incomingRoutePayload
                ?? throw new ArgumentNullException(nameof(incomingRoutePayload));
            if (!incomingRoutePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The incoming HUB route payload fingerprint is invalid.",
                    nameof(incomingRoutePayload));
            }
            this.progressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            this.rootSeed = rootSeed;
            this.screenSessionStableId = screenSessionStableId
                ?? throw new ArgumentNullException(nameof(screenSessionStableId));
            this.runStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            this.claimantStableId = claimantStableId
                ?? throw new ArgumentNullException(nameof(claimantStableId));
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));

            RefreshAuthority();
            if (authoritySnapshot.RecipeCatalog.Recipes.Count > 0)
            {
                selectedRecipeStableId = authoritySnapshot.RecipeCatalog.Recipes[0].RecipeStableId;
                EnsureAttempt(selectedRecipeStableId);
            }
            RebuildSnapshot();
        }

        public PlayerRouteProfilePayload IncomingRoutePayload { get { return incomingRoutePayload; } }
        public CraftingScreenSnapshot Snapshot { get { return snapshot; } }

        public CraftingScreenResult Refresh()
        {
            if (closed) return Result(CraftingScreenStatus.AlreadyClosed, "crafting-screen-closed");
            RefreshAuthority();
            if (selectedRecipeStableId != null) EnsureAttempt(selectedRecipeStableId);
            RebuildSnapshot();
            return Result(CraftingScreenStatus.Refreshed, string.Empty);
        }

        public CraftingScreenResult SelectRecipe(StableId recipeStableId)
        {
            if (closed) return Result(CraftingScreenStatus.AlreadyClosed, "crafting-screen-closed");
            if (recipeStableId == null
                || authoritySnapshot.RecipeCatalog.Find(recipeStableId) == null)
            {
                return Result(CraftingScreenStatus.NoSelection, "crafting-recipe-unknown");
            }
            if (selectedRecipeStableId == recipeStableId)
            {
                EnsureAttempt(recipeStableId);
                RebuildSnapshot();
                return Result(CraftingScreenStatus.PreviewReady, string.Empty);
            }

            selectedRecipeStableId = recipeStableId;
            AttemptState attempt = EnsureAttempt(recipeStableId);
            RebuildSnapshot();
            return Result(
                HasValidPreview(attempt)
                    ? CraftingScreenStatus.SelectionChanged
                    : CraftingScreenStatus.PreviewRejected,
                attempt.Preview == null ? "crafting-preview-unavailable" : attempt.Preview.RejectionCode);
        }

        public CraftingScreenResult CraftSelected()
        {
            return ExecuteSelected(false);
        }

        public CraftingScreenResult RetrySelected()
        {
            return ExecuteSelected(true);
        }

        public CraftingScreenResult BeginNextAttempt()
        {
            if (closed) return Result(CraftingScreenStatus.AlreadyClosed, "crafting-screen-closed");
            if (selectedRecipeStableId == null)
            {
                return Result(CraftingScreenStatus.NoSelection, "crafting-recipe-not-selected");
            }

            CraftingRecipe recipe = authoritySnapshot.RecipeCatalog.Find(selectedRecipeStableId);
            if (recipe == null)
            {
                return Result(CraftingScreenStatus.NoSelection, "crafting-recipe-unknown");
            }

            AttemptState current;
            int nextOrdinal = attempts.TryGetValue(selectedRecipeStableId, out current)
                ? checked(current.Ordinal + 1)
                : 0;
            attempts[selectedRecipeStableId] = new AttemptState(nextOrdinal);
            lastAuthorityResult = null;
            AttemptState next = EnsureAttempt(selectedRecipeStableId);
            RebuildSnapshot();
            return Result(
                HasValidPreview(next)
                    ? CraftingScreenStatus.PreviewReady
                    : CraftingScreenStatus.PreviewRejected,
                next.Preview == null ? "crafting-preview-unavailable" : next.Preview.RejectionCode);
        }

        public CraftingScreenResult Back()
        {
            if (closed) return Result(CraftingScreenStatus.AlreadyClosed, "crafting-screen-closed");
            closed = true;
            RebuildSnapshot();
            return new CraftingScreenResult(
                CraftingScreenStatus.Cancelled,
                string.Empty,
                snapshot,
                lastAuthorityResult,
                incomingRoutePayload);
        }

        private CraftingScreenResult ExecuteSelected(bool retryOnly)
        {
            if (closed) return Result(CraftingScreenStatus.AlreadyClosed, "crafting-screen-closed");
            if (selectedRecipeStableId == null)
            {
                return Result(CraftingScreenStatus.NoSelection, "crafting-recipe-not-selected");
            }

            CraftingRecipe recipe = authoritySnapshot.RecipeCatalog.Find(selectedRecipeStableId);
            if (recipe == null)
            {
                return Result(CraftingScreenStatus.NoSelection, "crafting-recipe-unknown");
            }

            AttemptState attempt = EnsureAttempt(selectedRecipeStableId);
            if (attempt.Terminal)
            {
                return Result(CraftingScreenStatus.AlreadyResolved, "crafting-operation-already-resolved");
            }
            if (retryOnly && !attempt.RetryPending)
            {
                return Result(CraftingScreenStatus.RetryNotAvailable, "crafting-retry-not-pending");
            }

            int unlockLevel = recipe.ResolveUnlockLevel(attempt.Command.RootSeed);
            if (progressionContext.CharacterLevel < unlockLevel)
            {
                return Result(CraftingScreenStatus.Locked, "crafting-not-unlocked");
            }
            if (!attempt.RetryPending && authoritySnapshot.ScrapBalance < recipe.ScrapCost)
            {
                return Result(CraftingScreenStatus.InsufficientScrap, "insufficient-scrap");
            }

            CraftingPresentationStateResult execution = authority.Craft(attempt.Command);
            if (execution == null)
            {
                return Result(CraftingScreenStatus.Rejected, "crafting-authority-result-null");
            }
            attempt.LastExecution = execution;
            lastAuthorityResult = execution;

            if (!string.IsNullOrEmpty(execution.CommandFingerprint)
                && !string.Equals(
                    execution.CommandFingerprint,
                    attempt.Command.Fingerprint,
                    StringComparison.Ordinal))
            {
                attempt.Terminal = true;
                attempt.RetryPending = false;
                RefreshAuthority();
                RebuildSnapshot();
                return Result(
                    CraftingScreenStatus.ResultMismatch,
                    "crafting-result-command-fingerprint-mismatch",
                    execution);
            }

            CraftingScreenStatus mappedStatus = MapStatus(execution.Status);
            if (execution.Succeeded)
            {
                if (execution.Equipment == null)
                {
                    attempt.Terminal = true;
                    mappedStatus = CraftingScreenStatus.ResultMismatch;
                }
                else if (attempt.Preview == null
                    || attempt.Preview.Equipment == null
                    || !string.Equals(
                        execution.Equipment.Fingerprint,
                        attempt.Preview.Equipment.Fingerprint,
                        StringComparison.Ordinal))
                {
                    attempt.Terminal = true;
                    mappedStatus = CraftingScreenStatus.ResultMismatch;
                }
                else
                {
                    attempt.Terminal = true;
                }
                attempt.RetryPending = false;
            }
            else if (execution.Status == CraftingResultStatus.RewardApplicationRetryRequired)
            {
                attempt.RetryPending = true;
            }
            else if (execution.Status == CraftingResultStatus.ConflictingDuplicate
                || execution.Status == CraftingResultStatus.InvalidCommand
                || execution.Status == CraftingResultStatus.UnknownRecipe
                || execution.Status == CraftingResultStatus.UnknownTargetEquipment
                || execution.Status == CraftingResultStatus.InvalidRecipeForCatalog
                || execution.Status == CraftingResultStatus.GenerationRejected)
            {
                attempt.Terminal = true;
                attempt.RetryPending = false;
            }

            RefreshAuthority();
            RebuildSnapshot();
            string rejection = mappedStatus == CraftingScreenStatus.ResultMismatch
                ? "crafting-result-does-not-match-preview"
                : execution.RejectionCode;
            return Result(mappedStatus, rejection, execution);
        }

        private AttemptState EnsureAttempt(StableId recipeStableId)
        {
            AttemptState attempt;
            if (!attempts.TryGetValue(recipeStableId, out attempt))
            {
                attempt = new AttemptState(0);
                attempts.Add(recipeStableId, attempt);
            }
            if (attempt.Command != null) return attempt;

            CraftingRecipe recipe = authoritySnapshot.RecipeCatalog.Find(recipeStableId);
            if (recipe == null) return attempt;

            StableId operationStableId = Crafting.DeriveStableId(
                "craftui-operation",
                screenSessionStableId.ToString(),
                recipeStableId.ToString(),
                attempt.Ordinal.ToString(CultureInfo.InvariantCulture));
            ulong operationSeed = DeriveSeed(
                rootSeed,
                screenSessionStableId,
                recipeStableId,
                attempt.Ordinal);
            attempt.Command = new CraftEquipmentCommand(
                operationStableId,
                recipeStableId,
                runStableId,
                claimantStableId,
                progressionContext,
                operationSeed);

            int unlockLevel = recipe.ResolveUnlockLevel(operationSeed);
            if (progressionContext.CharacterLevel >= unlockLevel
                && authoritySnapshot.EquipmentCatalog.FindEquipmentDefinition(
                    recipe.TargetEquipmentDefinitionStableId) != null)
            {
                attempt.Preview = authority.Preview(attempt.Command);
            }
            return attempt;
        }


        private static bool HasValidPreview(AttemptState attempt)
        {
            if (attempt == null
                || attempt.Command == null
                || attempt.Preview == null
                || attempt.Preview.Equipment == null)
            {
                return false;
            }
            return string.IsNullOrEmpty(attempt.Preview.CommandFingerprint)
                || string.Equals(
                    attempt.Preview.CommandFingerprint,
                    attempt.Command.Fingerprint,
                    StringComparison.Ordinal);
        }

        private void RefreshAuthority()
        {
            authoritySnapshot = authority.ExportSnapshot();
            if (authoritySnapshot == null)
            {
                throw new InvalidOperationException(
                    "The crafting presentation authority returned a null snapshot.");
            }
        }

        private void RebuildSnapshot()
        {
            var projected = new List<CraftingRecipeView>(
                authoritySnapshot.RecipeCatalog.Recipes.Count);
            for (int index = 0; index < authoritySnapshot.RecipeCatalog.Recipes.Count; index++)
            {
                CraftingRecipe recipe = authoritySnapshot.RecipeCatalog.Recipes[index];
                AttemptState attempt = EnsureAttempt(recipe.RecipeStableId);
                EquipmentDefinition target = authoritySnapshot.EquipmentCatalog
                    .FindEquipmentDefinition(recipe.TargetEquipmentDefinitionStableId);
                int unlockLevel = recipe.ResolveUnlockLevel(attempt.Command.RootSeed);
                CraftingRecipeAvailability availability;
                string previewRejection = attempt.Preview == null
                    ? string.Empty
                    : attempt.Preview.RejectionCode;
                if (target == null)
                {
                    availability = CraftingRecipeAvailability.InvalidTarget;
                }
                else if (progressionContext.CharacterLevel < unlockLevel)
                {
                    availability = CraftingRecipeAvailability.Locked;
                }
                else if (!HasValidPreview(attempt))
                {
                    availability = CraftingRecipeAvailability.PreviewUnavailable;
                }
                else if (authoritySnapshot.ScrapBalance < recipe.ScrapCost
                    && !attempt.RetryPending)
                {
                    availability = CraftingRecipeAvailability.InsufficientScrap;
                }
                else
                {
                    availability = CraftingRecipeAvailability.Available;
                }

                projected.Add(new CraftingRecipeView(
                    recipe.RecipeStableId,
                    recipe.TargetEquipmentDefinitionStableId,
                    target == null ? recipe.TargetEquipmentDefinitionStableId.ToString() : target.DisplayName,
                    target == null ? null : target.CategoryId,
                    recipe.NaturalDiscoveryLevel,
                    unlockLevel,
                    progressionContext.CharacterLevel,
                    recipe.ScrapCost,
                    authoritySnapshot.ScrapBalance,
                    availability,
                    attempt.Ordinal,
                    attempt.Command,
                    attempt.Preview == null ? null : attempt.Preview.Equipment,
                    previewRejection,
                    attempt.Terminal,
                    attempt.RetryPending));
            }

            snapshot = new CraftingScreenSnapshot(
                incomingRoutePayload,
                authoritySnapshot.ScrapBalance,
                authoritySnapshot.ScrapSequence,
                authoritySnapshot.HoldingsSequence,
                authoritySnapshot.Fingerprint,
                projected,
                selectedRecipeStableId,
                lastAuthorityResult,
                closed);
        }

        private CraftingScreenResult Result(
            CraftingScreenStatus status,
            string rejectionCode,
            CraftingPresentationStateResult authorityResult = null)
        {
            return new CraftingScreenResult(
                status,
                rejectionCode,
                snapshot,
                authorityResult,
                null);
        }

        private static CraftingScreenStatus MapStatus(CraftingResultStatus status)
        {
            switch (status)
            {
                case CraftingResultStatus.Crafted:
                    return CraftingScreenStatus.Crafted;
                case CraftingResultStatus.ExactDuplicateNoChange:
                    return CraftingScreenStatus.ExactDuplicateNoChange;
                case CraftingResultStatus.ProgressionUnavailable:
                    return CraftingScreenStatus.Locked;
                case CraftingResultStatus.InsufficientScrap:
                    return CraftingScreenStatus.InsufficientScrap;
                case CraftingResultStatus.RewardApplicationRetryRequired:
                    return CraftingScreenStatus.RetryRequired;
                case CraftingResultStatus.ConflictingDuplicate:
                    return CraftingScreenStatus.ConflictingDuplicate;
                default:
                    return CraftingScreenStatus.Rejected;
            }
        }

        private static ulong DeriveSeed(
            ulong baseSeed,
            StableId screenSessionStableId,
            StableId recipeStableId,
            int ordinal)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                hash = Add(hash, baseSeed.ToString(CultureInfo.InvariantCulture));
                hash = Add(hash, screenSessionStableId.ToString());
                hash = Add(hash, recipeStableId.ToString());
                hash = Add(hash, ordinal.ToString(CultureInfo.InvariantCulture));
                return hash;
            }
        }

        private static ulong Add(ulong hash, string text)
        {
            unchecked
            {
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= (byte)text[index];
                    hash *= 1099511628211UL;
                }
                hash ^= (byte)'\n';
                hash *= 1099511628211UL;
                return hash;
            }
        }
    }
}

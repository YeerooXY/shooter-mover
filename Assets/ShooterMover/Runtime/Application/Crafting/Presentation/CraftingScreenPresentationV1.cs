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
    public enum CraftingRecipeAvailabilityV1
    {
        Locked = 1,
        Available = 2,
        InsufficientScrap = 3,
        InvalidTarget = 4,
        PreviewUnavailable = 5,
    }

    public enum CraftingScreenStatusV1
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
    public sealed class CraftingPresentationAuthoritySnapshotV1
    {
        public CraftingPresentationAuthoritySnapshotV1(
            long scrapBalance,
            long scrapSequence,
            long holdingsSequence,
            CraftingRecipeCatalogV1 recipeCatalog,
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
        public CraftingRecipeCatalogV1 RecipeCatalog { get; }
        public EquipmentCatalog EquipmentCatalog { get; }
        public string Fingerprint { get; }
    }

    /// <summary>
    /// Public presentation-safe shape for CRA-001 terminal and retry facts. It carries
    /// the exact immutable equipment instance returned by the authoritative operation.
    /// </summary>
    public sealed class CraftingPresentationAuthorityResultV1
    {
        public CraftingPresentationAuthorityResultV1(
            CraftingResultStatusV1 status,
            StableId recipeStableId,
            int? unlockLevel,
            long scrapCost,
            EquipmentInstance equipment,
            string commandFingerprint,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(CraftingResultStatusV1), status))
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

        public CraftingResultStatusV1 Status { get; }
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
                return Status == CraftingResultStatusV1.Crafted
                    || Status == CraftingResultStatusV1.ExactDuplicateNoChange;
            }
        }

        public static CraftingPresentationAuthorityResultV1 FromAuthority(
            CraftingResultV1 source)
        {
            if (source == null) return null;
            return new CraftingPresentationAuthorityResultV1(
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
    public interface ICraftingPresentationAuthorityPortV1
    {
        CraftingPresentationAuthoritySnapshotV1 ExportSnapshot();
        CraftingPresentationAuthorityResultV1 Preview(CraftEquipmentCommandV1 command);
        CraftingPresentationAuthorityResultV1 Craft(CraftEquipmentCommandV1 command);
    }

    /// <summary>
    /// Production adapter for an existing CraftingServiceV1. The preview delegate is
    /// intentionally injected so composition can run CRA/GEN against cloned snapshots
    /// without copying generation rules into presentation code.
    /// </summary>
    public sealed class CraftingServicePresentationAuthorityPortV1 :
        ICraftingPresentationAuthorityPortV1
    {
        private readonly Func<CraftingPresentationAuthoritySnapshotV1> snapshotExporter;
        private readonly Func<CraftEquipmentCommandV1, CraftingPresentationAuthorityResultV1> preview;
        private readonly CraftingServiceV1 craftingService;

        public CraftingServicePresentationAuthorityPortV1(
            Func<CraftingPresentationAuthoritySnapshotV1> snapshotExporter,
            Func<CraftEquipmentCommandV1, CraftingPresentationAuthorityResultV1> preview,
            CraftingServiceV1 craftingService)
        {
            this.snapshotExporter = snapshotExporter
                ?? throw new ArgumentNullException(nameof(snapshotExporter));
            this.preview = preview ?? throw new ArgumentNullException(nameof(preview));
            this.craftingService = craftingService
                ?? throw new ArgumentNullException(nameof(craftingService));
        }

        public CraftingPresentationAuthoritySnapshotV1 ExportSnapshot()
        {
            CraftingPresentationAuthoritySnapshotV1 snapshot = snapshotExporter();
            return snapshot ?? throw new InvalidOperationException(
                "The crafting presentation snapshot exporter returned null.");
        }

        public CraftingPresentationAuthorityResultV1 Preview(
            CraftEquipmentCommandV1 command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            CraftingPresentationAuthorityResultV1 result = preview(command);
            return result ?? throw new InvalidOperationException(
                "The crafting preview delegate returned null.");
        }

        public CraftingPresentationAuthorityResultV1 Craft(
            CraftEquipmentCommandV1 command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            return CraftingPresentationAuthorityResultV1.FromAuthority(
                craftingService.Craft(command));
        }
    }

    public sealed class CraftingRecipeProjectionV1
    {
        public CraftingRecipeProjectionV1(
            StableId recipeStableId,
            StableId targetEquipmentDefinitionStableId,
            string targetDisplayName,
            StableId targetCategoryStableId,
            int naturalDiscoveryLevel,
            int craftingUnlockLevel,
            int characterLevel,
            long scrapCost,
            long scrapBalance,
            CraftingRecipeAvailabilityV1 availability,
            int attemptOrdinal,
            CraftEquipmentCommandV1 command,
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
        public CraftingRecipeAvailabilityV1 Availability { get; }
        public int AttemptOrdinal { get; }
        public CraftEquipmentCommandV1 Command { get; }
        public EquipmentInstance PreviewEquipment { get; }
        public string PreviewRejectionCode { get; }
        public bool IsAttemptResolved { get; }
        public bool IsRetryPending { get; }
        public bool IsLocked { get { return Availability == CraftingRecipeAvailabilityV1.Locked; } }
        public bool CanCraft
        {
            get
            {
                return Availability == CraftingRecipeAvailabilityV1.Available
                    && !IsAttemptResolved
                    && !IsRetryPending;
            }
        }
        public bool HasPreview { get { return PreviewEquipment != null; } }
    }

    public sealed class CraftingScreenSnapshotV1
    {
        private readonly ReadOnlyCollection<CraftingRecipeProjectionV1> recipes;

        public CraftingScreenSnapshotV1(
            PlayerRouteProfilePayloadV1 incomingRoutePayload,
            long scrapBalance,
            long scrapSequence,
            long holdingsSequence,
            string authorityFingerprint,
            IEnumerable<CraftingRecipeProjectionV1> recipes,
            StableId selectedRecipeStableId,
            CraftingPresentationAuthorityResultV1 lastAuthorityResult,
            bool isClosed)
        {
            IncomingRoutePayload = incomingRoutePayload
                ?? throw new ArgumentNullException(nameof(incomingRoutePayload));
            ScrapBalance = scrapBalance;
            ScrapSequence = scrapSequence;
            HoldingsSequence = holdingsSequence;
            AuthorityFingerprint = authorityFingerprint ?? string.Empty;
            this.recipes = new ReadOnlyCollection<CraftingRecipeProjectionV1>(
                new List<CraftingRecipeProjectionV1>(
                    recipes ?? throw new ArgumentNullException(nameof(recipes))));
            SelectedRecipeStableId = selectedRecipeStableId;
            LastAuthorityResult = lastAuthorityResult;
            IsClosed = isClosed;
        }

        public PlayerRouteProfilePayloadV1 IncomingRoutePayload { get; }
        public long ScrapBalance { get; }
        public long ScrapSequence { get; }
        public long HoldingsSequence { get; }
        public string AuthorityFingerprint { get; }
        public IReadOnlyList<CraftingRecipeProjectionV1> Recipes { get { return recipes; } }
        public StableId SelectedRecipeStableId { get; }
        public CraftingPresentationAuthorityResultV1 LastAuthorityResult { get; }
        public bool IsClosed { get; }

        public CraftingRecipeProjectionV1 SelectedRecipe
        {
            get { return FindRecipe(SelectedRecipeStableId); }
        }

        public CraftingRecipeProjectionV1 FindRecipe(StableId recipeStableId)
        {
            if (recipeStableId == null) return null;
            for (int index = 0; index < recipes.Count; index++)
            {
                if (recipes[index].RecipeStableId == recipeStableId) return recipes[index];
            }
            return null;
        }
    }

    public sealed class CraftingScreenResultV1
    {
        public CraftingScreenResultV1(
            CraftingScreenStatusV1 status,
            string rejectionCode,
            CraftingScreenSnapshotV1 snapshot,
            CraftingPresentationAuthorityResultV1 authorityResult,
            PlayerRouteProfilePayloadV1 routePayload)
        {
            if (!Enum.IsDefined(typeof(CraftingScreenStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            AuthorityResult = authorityResult;
            RoutePayload = routePayload;
        }

        public CraftingScreenStatusV1 Status { get; }
        public string RejectionCode { get; }
        public CraftingScreenSnapshotV1 Snapshot { get; }
        public CraftingPresentationAuthorityResultV1 AuthorityResult { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public bool LeavesScreen { get { return Status == CraftingScreenStatusV1.Cancelled; } }
    }

    /// <summary>
    /// Engine-independent crafting-screen state. It owns selection and operation-attempt
    /// presentation only. Scrap, holdings, recipes, generation, and result application
    /// remain authoritative behind ICraftingPresentationAuthorityPortV1.
    /// </summary>
    public sealed class CraftingScreenServiceV1
    {
        private sealed class AttemptState
        {
            public AttemptState(int ordinal)
            {
                Ordinal = ordinal;
            }

            public int Ordinal;
            public CraftEquipmentCommandV1 Command;
            public CraftingPresentationAuthorityResultV1 Preview;
            public CraftingPresentationAuthorityResultV1 LastExecution;
            public bool RetryPending;
            public bool Terminal;
        }

        private readonly PlayerRouteProfilePayloadV1 incomingRoutePayload;
        private readonly ProgressionContext progressionContext;
        private readonly ulong rootSeed;
        private readonly StableId screenSessionStableId;
        private readonly StableId runStableId;
        private readonly StableId claimantStableId;
        private readonly ICraftingPresentationAuthorityPortV1 authority;
        private readonly Dictionary<StableId, AttemptState> attempts =
            new Dictionary<StableId, AttemptState>();

        private CraftingPresentationAuthoritySnapshotV1 authoritySnapshot;
        private CraftingScreenSnapshotV1 snapshot;
        private StableId selectedRecipeStableId;
        private CraftingPresentationAuthorityResultV1 lastAuthorityResult;
        private bool closed;

        public CraftingScreenServiceV1(
            PlayerRouteProfilePayloadV1 incomingRoutePayload,
            ProgressionContext progressionContext,
            ulong rootSeed,
            StableId screenSessionStableId,
            StableId runStableId,
            StableId claimantStableId,
            ICraftingPresentationAuthorityPortV1 authority)
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

        public PlayerRouteProfilePayloadV1 IncomingRoutePayload { get { return incomingRoutePayload; } }
        public CraftingScreenSnapshotV1 Snapshot { get { return snapshot; } }

        public CraftingScreenResultV1 Refresh()
        {
            if (closed) return Result(CraftingScreenStatusV1.AlreadyClosed, "crafting-screen-closed");
            RefreshAuthority();
            if (selectedRecipeStableId != null) EnsureAttempt(selectedRecipeStableId);
            RebuildSnapshot();
            return Result(CraftingScreenStatusV1.Refreshed, string.Empty);
        }

        public CraftingScreenResultV1 SelectRecipe(StableId recipeStableId)
        {
            if (closed) return Result(CraftingScreenStatusV1.AlreadyClosed, "crafting-screen-closed");
            if (recipeStableId == null
                || authoritySnapshot.RecipeCatalog.Find(recipeStableId) == null)
            {
                return Result(CraftingScreenStatusV1.NoSelection, "crafting-recipe-unknown");
            }
            if (selectedRecipeStableId == recipeStableId)
            {
                EnsureAttempt(recipeStableId);
                RebuildSnapshot();
                return Result(CraftingScreenStatusV1.PreviewReady, string.Empty);
            }

            selectedRecipeStableId = recipeStableId;
            AttemptState attempt = EnsureAttempt(recipeStableId);
            RebuildSnapshot();
            return Result(
                HasValidPreview(attempt)
                    ? CraftingScreenStatusV1.SelectionChanged
                    : CraftingScreenStatusV1.PreviewRejected,
                attempt.Preview == null ? "crafting-preview-unavailable" : attempt.Preview.RejectionCode);
        }

        public CraftingScreenResultV1 CraftSelected()
        {
            return ExecuteSelected(false);
        }

        public CraftingScreenResultV1 RetrySelected()
        {
            return ExecuteSelected(true);
        }

        public CraftingScreenResultV1 BeginNextAttempt()
        {
            if (closed) return Result(CraftingScreenStatusV1.AlreadyClosed, "crafting-screen-closed");
            if (selectedRecipeStableId == null)
            {
                return Result(CraftingScreenStatusV1.NoSelection, "crafting-recipe-not-selected");
            }

            CraftingRecipeV1 recipe = authoritySnapshot.RecipeCatalog.Find(selectedRecipeStableId);
            if (recipe == null)
            {
                return Result(CraftingScreenStatusV1.NoSelection, "crafting-recipe-unknown");
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
                    ? CraftingScreenStatusV1.PreviewReady
                    : CraftingScreenStatusV1.PreviewRejected,
                next.Preview == null ? "crafting-preview-unavailable" : next.Preview.RejectionCode);
        }

        public CraftingScreenResultV1 Back()
        {
            if (closed) return Result(CraftingScreenStatusV1.AlreadyClosed, "crafting-screen-closed");
            closed = true;
            RebuildSnapshot();
            return new CraftingScreenResultV1(
                CraftingScreenStatusV1.Cancelled,
                string.Empty,
                snapshot,
                lastAuthorityResult,
                incomingRoutePayload);
        }

        private CraftingScreenResultV1 ExecuteSelected(bool retryOnly)
        {
            if (closed) return Result(CraftingScreenStatusV1.AlreadyClosed, "crafting-screen-closed");
            if (selectedRecipeStableId == null)
            {
                return Result(CraftingScreenStatusV1.NoSelection, "crafting-recipe-not-selected");
            }

            CraftingRecipeV1 recipe = authoritySnapshot.RecipeCatalog.Find(selectedRecipeStableId);
            if (recipe == null)
            {
                return Result(CraftingScreenStatusV1.NoSelection, "crafting-recipe-unknown");
            }

            AttemptState attempt = EnsureAttempt(selectedRecipeStableId);
            if (attempt.Terminal)
            {
                return Result(CraftingScreenStatusV1.AlreadyResolved, "crafting-operation-already-resolved");
            }
            if (retryOnly && !attempt.RetryPending)
            {
                return Result(CraftingScreenStatusV1.RetryNotAvailable, "crafting-retry-not-pending");
            }

            int unlockLevel = recipe.ResolveUnlockLevel(attempt.Command.RootSeed);
            if (progressionContext.CharacterLevel < unlockLevel)
            {
                return Result(CraftingScreenStatusV1.Locked, "crafting-not-unlocked");
            }
            if (!attempt.RetryPending && authoritySnapshot.ScrapBalance < recipe.ScrapCost)
            {
                return Result(CraftingScreenStatusV1.InsufficientScrap, "insufficient-scrap");
            }

            CraftingPresentationAuthorityResultV1 execution = authority.Craft(attempt.Command);
            if (execution == null)
            {
                return Result(CraftingScreenStatusV1.Rejected, "crafting-authority-result-null");
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
                    CraftingScreenStatusV1.ResultMismatch,
                    "crafting-result-command-fingerprint-mismatch",
                    execution);
            }

            CraftingScreenStatusV1 mappedStatus = MapStatus(execution.Status);
            if (execution.Succeeded)
            {
                if (execution.Equipment == null)
                {
                    attempt.Terminal = true;
                    mappedStatus = CraftingScreenStatusV1.ResultMismatch;
                }
                else if (attempt.Preview == null
                    || attempt.Preview.Equipment == null
                    || !string.Equals(
                        execution.Equipment.Fingerprint,
                        attempt.Preview.Equipment.Fingerprint,
                        StringComparison.Ordinal))
                {
                    attempt.Terminal = true;
                    mappedStatus = CraftingScreenStatusV1.ResultMismatch;
                }
                else
                {
                    attempt.Terminal = true;
                }
                attempt.RetryPending = false;
            }
            else if (execution.Status == CraftingResultStatusV1.RewardApplicationRetryRequired)
            {
                attempt.RetryPending = true;
            }
            else if (execution.Status == CraftingResultStatusV1.ConflictingDuplicate
                || execution.Status == CraftingResultStatusV1.InvalidCommand
                || execution.Status == CraftingResultStatusV1.UnknownRecipe
                || execution.Status == CraftingResultStatusV1.UnknownTargetEquipment
                || execution.Status == CraftingResultStatusV1.InvalidRecipeForCatalog
                || execution.Status == CraftingResultStatusV1.GenerationRejected)
            {
                attempt.Terminal = true;
                attempt.RetryPending = false;
            }

            RefreshAuthority();
            RebuildSnapshot();
            string rejection = mappedStatus == CraftingScreenStatusV1.ResultMismatch
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

            CraftingRecipeV1 recipe = authoritySnapshot.RecipeCatalog.Find(recipeStableId);
            if (recipe == null) return attempt;

            StableId operationStableId = CraftingCanonicalV1.DeriveStableId(
                "craftui-operation",
                screenSessionStableId.ToString(),
                recipeStableId.ToString(),
                attempt.Ordinal.ToString(CultureInfo.InvariantCulture));
            ulong operationSeed = DeriveSeed(
                rootSeed,
                screenSessionStableId,
                recipeStableId,
                attempt.Ordinal);
            attempt.Command = new CraftEquipmentCommandV1(
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
            var projected = new List<CraftingRecipeProjectionV1>(
                authoritySnapshot.RecipeCatalog.Recipes.Count);
            for (int index = 0; index < authoritySnapshot.RecipeCatalog.Recipes.Count; index++)
            {
                CraftingRecipeV1 recipe = authoritySnapshot.RecipeCatalog.Recipes[index];
                AttemptState attempt = EnsureAttempt(recipe.RecipeStableId);
                EquipmentDefinition target = authoritySnapshot.EquipmentCatalog
                    .FindEquipmentDefinition(recipe.TargetEquipmentDefinitionStableId);
                int unlockLevel = recipe.ResolveUnlockLevel(attempt.Command.RootSeed);
                CraftingRecipeAvailabilityV1 availability;
                string previewRejection = attempt.Preview == null
                    ? string.Empty
                    : attempt.Preview.RejectionCode;
                if (target == null)
                {
                    availability = CraftingRecipeAvailabilityV1.InvalidTarget;
                }
                else if (progressionContext.CharacterLevel < unlockLevel)
                {
                    availability = CraftingRecipeAvailabilityV1.Locked;
                }
                else if (!HasValidPreview(attempt))
                {
                    availability = CraftingRecipeAvailabilityV1.PreviewUnavailable;
                }
                else if (authoritySnapshot.ScrapBalance < recipe.ScrapCost
                    && !attempt.RetryPending)
                {
                    availability = CraftingRecipeAvailabilityV1.InsufficientScrap;
                }
                else
                {
                    availability = CraftingRecipeAvailabilityV1.Available;
                }

                projected.Add(new CraftingRecipeProjectionV1(
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

            snapshot = new CraftingScreenSnapshotV1(
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

        private CraftingScreenResultV1 Result(
            CraftingScreenStatusV1 status,
            string rejectionCode,
            CraftingPresentationAuthorityResultV1 authorityResult = null)
        {
            return new CraftingScreenResultV1(
                status,
                rejectionCode,
                snapshot,
                authorityResult,
                null);
        }

        private static CraftingScreenStatusV1 MapStatus(CraftingResultStatusV1 status)
        {
            switch (status)
            {
                case CraftingResultStatusV1.Crafted:
                    return CraftingScreenStatusV1.Crafted;
                case CraftingResultStatusV1.ExactDuplicateNoChange:
                    return CraftingScreenStatusV1.ExactDuplicateNoChange;
                case CraftingResultStatusV1.ProgressionUnavailable:
                    return CraftingScreenStatusV1.Locked;
                case CraftingResultStatusV1.InsufficientScrap:
                    return CraftingScreenStatusV1.InsufficientScrap;
                case CraftingResultStatusV1.RewardApplicationRetryRequired:
                    return CraftingScreenStatusV1.RetryRequired;
                case CraftingResultStatusV1.ConflictingDuplicate:
                    return CraftingScreenStatusV1.ConflictingDuplicate;
                default:
                    return CraftingScreenStatusV1.Rejected;
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

using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Crafting
{
    public enum CraftingResultStatus
    {
        Crafted = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        UnknownRecipe = 4,
        UnknownTargetEquipment = 5,
        ProgressionUnavailable = 6,
        InsufficientScrap = 7,
        InvalidRecipeForCatalog = 8,
        GenerationRejected = 9,
        RewardApplicationRetryRequired = 10,
        RewardApplicationRejected = 11,
        InvalidCommand = 12,
    }

    public sealed class CraftEquipmentCommand : IEquatable<CraftEquipmentCommand>
    {
        private readonly string canonicalText;

        public CraftEquipmentCommand(
            StableId craftTransactionStableId,
            StableId recipeStableId,
            StableId runStableId,
            StableId claimantStableId,
            ProgressionContext progressionContext,
            ulong rootSeed,
            long? expectedScrapSequence = null,
            long? expectedHoldingsSequence = null)
        {
            CraftTransactionStableId = craftTransactionStableId
                ?? throw new ArgumentNullException(nameof(craftTransactionStableId));
            RecipeStableId = recipeStableId ?? throw new ArgumentNullException(nameof(recipeStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            ClaimantStableId = claimantStableId ?? throw new ArgumentNullException(nameof(claimantStableId));
            ProgressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            ValidateExpectedSequence(expectedScrapSequence, nameof(expectedScrapSequence));
            ValidateExpectedSequence(expectedHoldingsSequence, nameof(expectedHoldingsSequence));

            RootSeed = rootSeed;
            ExpectedScrapSequence = expectedScrapSequence;
            ExpectedHoldingsSequence = expectedHoldingsSequence;
            canonicalText = "schema=craft-equipment-command-v1"
                + "\ncraft_transaction_id=" + CraftTransactionStableId
                + "\nrecipe_id=" + RecipeStableId
                + "\nrun_id=" + RunStableId
                + "\nclaimant_id=" + ClaimantStableId
                + "\nprogression_fingerprint=" + ProgressionContext.Fingerprint
                + "\nroot_seed=" + RootSeed.ToString(CultureInfo.InvariantCulture)
                + "\nexpected_scrap_sequence=" + Optional(ExpectedScrapSequence)
                + "\nexpected_holdings_sequence=" + Optional(ExpectedHoldingsSequence);
            Fingerprint = Crafting.Fingerprint(canonicalText);
        }

        public StableId CraftTransactionStableId { get; }
        public StableId RecipeStableId { get; }
        public StableId RunStableId { get; }
        public StableId ClaimantStableId { get; }
        public ProgressionContext ProgressionContext { get; }
        public ulong RootSeed { get; }
        public long? ExpectedScrapSequence { get; }
        public long? ExpectedHoldingsSequence { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString() { return canonicalText; }

        public bool Equals(CraftEquipmentCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as CraftEquipmentCommand); }
        public override int GetHashCode() { return Crafting.DeterministicHash(canonicalText); }

        private static void ValidateExpectedSequence(long? value, string parameterName)
        {
            if (value.HasValue && value.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static string Optional(long? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "none";
        }
    }

    public sealed class CraftingResult
    {
        internal CraftingResult(
            CraftingResultStatus status,
            StableId recipeStableId,
            int? unlockLevel,
            long scrapCost,
            EquipmentInstance equipment,
            string recipeFingerprint,
            string commandFingerprint,
            RewardApplicationResult rewardApplicationResult,
            string rejectionCode)
        {
            Status = status;
            RecipeStableId = recipeStableId;
            UnlockLevel = unlockLevel;
            ScrapCost = scrapCost;
            Equipment = equipment;
            RecipeFingerprint = recipeFingerprint;
            CommandFingerprint = commandFingerprint;
            RewardApplicationResult = rewardApplicationResult;
            RejectionCode = rejectionCode;
        }

        public CraftingResultStatus Status { get; }
        public StableId RecipeStableId { get; }
        public int? UnlockLevel { get; }
        public long ScrapCost { get; }
        public EquipmentInstance Equipment { get; }
        public StableId EquipmentInstanceStableId
        {
            get { return Equipment == null ? null : Equipment.InstanceId; }
        }
        public string EquipmentFingerprint
        {
            get { return Equipment == null ? null : Equipment.Fingerprint; }
        }
        public string RecipeFingerprint { get; }
        public string CommandFingerprint { get; }
        public RewardApplicationResult RewardApplicationResult { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == CraftingResultStatus.Crafted
                    || Status == CraftingResultStatus.ExactDuplicateNoChange;
            }
        }
    }

    public sealed class CraftingActions
    {
        private readonly CraftingRecipeCatalog recipeCatalog;
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly RewardGenerationActions generator;
        private readonly RewardApplicationActions rewardApplication;
        private readonly ScrapWalletActions scrapWallet;
        private readonly StableId moneyAuthorityStableId;
        private readonly StableId holdingsAuthorityStableId;

        public CraftingActions(
            CraftingRecipeCatalog recipeCatalog,
            EquipmentCatalog equipmentCatalog,
            RewardGenerationActions generator,
            RewardApplicationActions rewardApplication,
            ScrapWalletActions scrapWallet,
            StableId moneyAuthorityStableId,
            StableId holdingsAuthorityStableId)
        {
            this.recipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
            this.equipmentCatalog = equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.rewardApplication = rewardApplication ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.scrapWallet = scrapWallet ?? throw new ArgumentNullException(nameof(scrapWallet));
            this.moneyAuthorityStableId = moneyAuthorityStableId
                ?? throw new ArgumentNullException(nameof(moneyAuthorityStableId));
            this.holdingsAuthorityStableId = holdingsAuthorityStableId
                ?? throw new ArgumentNullException(nameof(holdingsAuthorityStableId));
        }

        public CraftingResult Craft(CraftEquipmentCommand command)
        {
            if (command == null)
            {
                return Result(
                    CraftingResultStatus.InvalidCommand,
                    null,
                    null,
                    0L,
                    null,
                    null,
                    "command-null");
            }

            CraftingRecipe recipe = recipeCatalog.Find(command.RecipeStableId);
            if (recipe == null)
            {
                return Result(
                    CraftingResultStatus.UnknownRecipe,
                    command,
                    null,
                    0L,
                    null,
                    null,
                    "recipe-unknown");
            }

            EquipmentDefinition target = equipmentCatalog.FindEquipmentDefinition(
                recipe.TargetEquipmentDefinitionStableId);
            if (target == null)
            {
                return Result(
                    CraftingResultStatus.UnknownTargetEquipment,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    null,
                    null,
                    "target-equipment-unknown");
            }

            int unlockLevel = recipe.ResolveUnlockLevel(command.RootSeed);
            if (command.ProgressionContext.CharacterLevel < unlockLevel)
            {
                return Result(
                    CraftingResultStatus.ProgressionUnavailable,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    null,
                    null,
                    "crafting-not-unlocked",
                    unlockLevel);
            }

            StableId commitmentId = Derive("craftcommit", command, "commitment");
            RewardCommitmentSnapshot existingCommitment;
            bool isReplay = rewardApplication.TryGetCommitment(
                commitmentId,
                out existingCommitment);
            if (!isReplay && scrapWallet.Balance < recipe.ScrapCost)
            {
                return Result(
                    CraftingResultStatus.InsufficientScrap,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    null,
                    null,
                    "insufficient-scrap",
                    unlockLevel);
            }

            CraftingGenerationInput generationInput;
            string preparationFailure;
            if (!TryPrepareGeneration(
                recipe,
                target,
                out generationInput,
                out preparationFailure))
            {
                return Result(
                    CraftingResultStatus.InvalidRecipeForCatalog,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    null,
                    null,
                    preparationFailure,
                    unlockLevel);
            }

            StableId sourceOperationId = Derive("craftop", command, "source-operation");
            StableId equipmentInstanceId = Derive("craftitem", command, "equipment-instance");
            EquipmentGenerationResult generated = generator.GenerateEquipment(
                EquipmentGenerationRequest.Create(
                    sourceOperationId,
                    equipmentInstanceId,
                    generationInput.Policy,
                    generationInput.Catalog,
                    command.ProgressionContext,
                    command.RootSeed,
                    recipe.GeneratorPolicy.AlgorithmVersion));
            if (generated == null || !generated.IsSuccess || generated.Equipment == null)
            {
                return Result(
                    CraftingResultStatus.GenerationRejected,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    generated == null ? null : generated.Equipment,
                    null,
                    generated == null ? "generator-result-null" : generated.FailureReason,
                    unlockLevel);
            }

            EquipmentValidationResult validation =
                equipmentCatalog.ValidateInstance(generated.Equipment);
            if (!validation.IsValid)
            {
                return Result(
                    CraftingResultStatus.GenerationRejected,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    generated.Equipment,
                    null,
                    "generated-instance-invalid-for-authoritative-catalog",
                    unlockLevel);
            }

            StableId scrapGrantId = Derive("craftgrant", command, "scrap-spend");
            StableId equipmentGrantId = Derive("craftgrant", command, "equipment-grant");
            RewardGrant scrapGrant = RewardGrant.Create(
                scrapGrantId,
                RewardGrantKind.Scrap,
                scrapWallet.CurrencyStableId,
                recipe.ScrapCost);
            RewardGrant equipmentGrant = RewardGrant.Create(
                equipmentGrantId,
                RewardGrantKind.EquipmentReference,
                recipe.TargetEquipmentDefinitionStableId,
                1L);
            RewardResult rewardResult = RewardResult.CreateGrants(
                commitmentId,
                sourceOperationId,
                new[] { scrapGrant, equipmentGrant });
            string contentFingerprint = Crafting.Fingerprint(
                "schema=crafting-commit-content-v1"
                + "\ncommand_fingerprint=" + command.Fingerprint
                + "\nrecipe_fingerprint=" + recipe.Fingerprint
                + "\ngenerator_result_fingerprint=" + generated.ResultFingerprint
                + "\nequipment_fingerprint=" + generated.Equipment.Fingerprint);
            RewardOperationRequest operation = RewardOperationRequest.Create(
                command.RunStableId,
                Derive("craftsource", command, "source-instance"),
                sourceOperationId,
                commitmentId,
                recipe.GeneratorPolicy.PolicyStableId,
                contentFingerprint);
            RewardCommitCommand commitCommand = RewardCommitCommand.Create(
                operation,
                rewardResult,
                contentFingerprint,
                new[]
                {
                    RewardGrantApplicationPayload.ForValue(scrapGrant),
                    RewardGrantApplicationPayload.ForEquipment(
                        equipmentGrant,
                        new[] { generated.Equipment }),
                });

            RewardApplicationResult commit = rewardApplication.Commit(commitCommand);
            if (commit.Status == RewardApplicationResultStatus.ConflictingDuplicate)
            {
                return Result(
                    CraftingResultStatus.ConflictingDuplicate,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    generated.Equipment,
                    commit,
                    commit.RejectionCode,
                    unlockLevel);
            }
            if (commit.Status != RewardApplicationResultStatus.Generated
                && commit.Status != RewardApplicationResultStatus.ExactDuplicateNoChange)
            {
                return Result(
                    CraftingResultStatus.RewardApplicationRejected,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    generated.Equipment,
                    commit,
                    commit.RejectionCode,
                    unlockLevel);
            }

            StableId claimId = Derive("craftclaim", command, "claim");
            RewardApplicationResult claim = rewardApplication.Claim(
                RewardClaimCommand.Create(
                    claimId,
                    commitmentId,
                    command.ClaimantStableId,
                    moneyAuthorityStableId,
                    scrapWallet.AuthorityStableId,
                    holdingsAuthorityStableId,
                    null,
                    command.ExpectedScrapSequence,
                    command.ExpectedHoldingsSequence));
            return MapClaim(
                command,
                recipe,
                unlockLevel,
                generated.Equipment,
                claim,
                claimId,
                commitmentId);
        }

        private CraftingResult MapClaim(
            CraftEquipmentCommand command,
            CraftingRecipe recipe,
            int unlockLevel,
            EquipmentInstance equipment,
            RewardApplicationResult claim,
            StableId claimId,
            StableId commitmentId)
        {
            if (claim.Status == RewardApplicationResultStatus.Applied)
            {
                return Result(
                    CraftingResultStatus.Crafted,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    null,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatus.AlreadyAppliedNoChange
                || (claim.Status == RewardApplicationResultStatus.ExactDuplicateNoChange
                    && claim.CommitmentState == RewardCommitmentState.Applied))
            {
                return Result(
                    CraftingResultStatus.ExactDuplicateNoChange,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    null,
                    unlockLevel);
            }
            if ((claim.Status == RewardApplicationResultStatus.ExactDuplicateNoChange
                    || claim.Status == RewardApplicationResultStatus.InvalidStateTransition)
                && claim.CommitmentState == RewardCommitmentState.Claimed)
            {
                RewardApplicationResult retry = rewardApplication.Retry(
                    RewardRetryClaimCommand.Create(commitmentId, claimId));
                if (retry.Status == RewardApplicationResultStatus.Applied)
                {
                    return Result(
                        CraftingResultStatus.Crafted,
                        command,
                        recipe,
                        recipe.ScrapCost,
                        equipment,
                        retry,
                        null,
                        unlockLevel);
                }
                if (retry.Status == RewardApplicationResultStatus.AlreadyAppliedNoChange)
                {
                    return Result(
                        CraftingResultStatus.ExactDuplicateNoChange,
                        command,
                        recipe,
                        recipe.ScrapCost,
                        equipment,
                        retry,
                        null,
                        unlockLevel);
                }
                return Result(
                    CraftingResultStatus.RewardApplicationRetryRequired,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    retry,
                    retry.RejectionCode,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatus.ClaimedPendingApplication)
            {
                return Result(
                    CraftingResultStatus.RewardApplicationRetryRequired,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    claim.RejectionCode,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatus.InsufficientFunds)
            {
                return Result(
                    CraftingResultStatus.InsufficientScrap,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    claim.RejectionCode,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatus.ConflictingDuplicate)
            {
                return Result(
                    CraftingResultStatus.ConflictingDuplicate,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    claim.RejectionCode,
                    unlockLevel);
            }
            return Result(
                CraftingResultStatus.RewardApplicationRejected,
                command,
                recipe,
                recipe.ScrapCost,
                equipment,
                claim,
                claim.RejectionCode,
                unlockLevel);
        }

        private bool TryPrepareGeneration(
            CraftingRecipe recipe,
            EquipmentDefinition target,
            out CraftingGenerationInput input,
            out string failure)
        {
            input = null;
            int minimumItemLevel = Math.Max(
                recipe.MinimumItemLevel,
                target.ItemLevelRange.Minimum);
            int maximumItemLevel = Math.Min(
                recipe.MaximumItemLevel,
                target.ItemLevelRange.Maximum);
            int maximumSlots = recipe.AugmentOptions.Count == 0
                ? 0
                : Math.Min(recipe.MaximumAugmentSlots, target.MaximumAugmentSlots);
            if (minimumItemLevel > maximumItemLevel)
            {
                failure = "recipe-item-level-range-does-not-overlap-target";
                return false;
            }
            if (recipe.MinimumAugmentSlots > maximumSlots)
            {
                failure = "recipe-minimum-slots-exceed-target-capacity";
                return false;
            }

            for (int index = 0; index < recipe.QualityOptions.Count; index++)
            {
                if (!target.SupportsQuality(
                    recipe.QualityOptions[index].DefinitionStableId))
                {
                    failure = "recipe-quality-not-supported:"
                        + recipe.QualityOptions[index].DefinitionStableId;
                    return false;
                }
            }

            EquipmentDefinition cappedTarget = EquipmentDefinition.Create(
                target.DefinitionId,
                target.CategoryId,
                target.FamilyId,
                target.DisplayName,
                target.RuntimeWeaponReferenceId,
                InclusiveIntRange.Create(minimumItemLevel, maximumItemLevel),
                maximumSlots,
                target.QualityTiers,
                target.Tags);
            var cappedAugments = new List<AugmentDefinition>();
            var augmentCandidates = new List<AugmentGenerationCandidate>();
            for (int index = 0; index < recipe.AugmentOptions.Count; index++)
            {
                CraftingWeightedDefinition option = recipe.AugmentOptions[index];
                AugmentDefinition original = equipmentCatalog.FindAugmentDefinition(
                    option.DefinitionStableId);
                if (original == null)
                {
                    failure = "recipe-augment-unknown:" + option.DefinitionStableId;
                    return false;
                }

                int maximumTier = Math.Min(
                    recipe.MaximumAugmentTier,
                    original.TierRange.Maximum);
                int maximumLevel = Math.Min(
                    recipe.MaximumAugmentLevel,
                    original.LevelRange.Maximum);
                if (maximumTier < original.TierRange.Minimum
                    || maximumLevel < original.LevelRange.Minimum)
                {
                    failure = "recipe-augment-cap-below-definition-minimum:"
                        + option.DefinitionStableId;
                    return false;
                }

                AugmentDefinition capped = AugmentDefinition.Create(
                    original.DefinitionId,
                    original.FamilyId,
                    original.DisplayName,
                    AugmentCompatibility.Create(
                        original.Compatibility.CategoryIds,
                        original.Compatibility.FamilyIds,
                        original.Compatibility.RequiredTags,
                        original.Compatibility.ExcludedTags),
                    original.ExclusionGroupIds,
                    original.DuplicatePolicy,
                    InclusiveIntRange.Create(
                        original.TierRange.Minimum,
                        maximumTier),
                    InclusiveIntRange.Create(
                        original.LevelRange.Minimum,
                        maximumLevel));
                cappedAugments.Add(capped);
                augmentCandidates.Add(AugmentGenerationCandidate.Create(
                    capped.DefinitionId,
                    0,
                    int.MaxValue,
                    option.Weight));
            }

            EquipmentCatalogBuildResult build = EquipmentCatalog.Build(
                new[] { cappedTarget },
                cappedAugments);
            if (build == null || !build.IsValid || build.Catalog == null)
            {
                failure = "capped-generation-catalog-invalid";
                return false;
            }

            var qualityCandidates = new List<EquipmentQualityCandidate>();
            for (int index = 0; index < recipe.QualityOptions.Count; index++)
            {
                CraftingWeightedDefinition option = recipe.QualityOptions[index];
                qualityCandidates.Add(EquipmentQualityCandidate.Create(
                    option.DefinitionStableId,
                    0L,
                    recipe.QualityPolicyKind == CraftingQualityPolicyKind.Fixed
                        ? 1UL
                        : option.Weight));
            }

            EquipmentGenerationPolicy policy = EquipmentGenerationPolicy.Create(
                recipe.GeneratorPolicy.PolicyStableId,
                new[]
                {
                    EquipmentGenerationCandidate.Create(
                        target.DefinitionId,
                        0,
                        int.MaxValue,
                        0,
                        int.MaxValue,
                        Array.Empty<StableId>(),
                        recipe.NaturalDiscoveryLevel,
                        InclusiveIntRange.Create(
                            minimumItemLevel,
                            maximumItemLevel),
                        1.0,
                        1.0),
                },
                qualityCandidates,
                augmentCandidates,
                recipe.MinimumAugmentSlots,
                maximumSlots,
                recipe.MinimumAugmentSlots == maximumSlots,
                recipe.GeneratorPolicy.Activation,
                recipe.GeneratorPolicy.Obsolescence);
            input = new CraftingGenerationInput(build.Catalog, policy);
            failure = null;
            return true;
        }

        private static StableId Derive(
            string namespaceName,
            CraftEquipmentCommand command,
            string purpose)
        {
            return Crafting.DeriveStableId(
                namespaceName,
                purpose,
                command.CraftTransactionStableId.ToString());
        }

        private static CraftingResult Result(
            CraftingResultStatus status,
            CraftEquipmentCommand command,
            CraftingRecipe recipe,
            long scrapCost,
            EquipmentInstance equipment,
            RewardApplicationResult rewardApplicationResult,
            string rejectionCode,
            int? unlockLevel = null)
        {
            return new CraftingResult(
                status,
                recipe == null
                    ? (command == null ? null : command.RecipeStableId)
                    : recipe.RecipeStableId,
                unlockLevel,
                scrapCost,
                equipment,
                recipe == null ? null : recipe.Fingerprint,
                command == null ? null : command.Fingerprint,
                rewardApplicationResult,
                rejectionCode);
        }

        private sealed class CraftingGenerationInput
        {
            public CraftingGenerationInput(
                EquipmentCatalog catalog,
                EquipmentGenerationPolicy policy)
            {
                Catalog = catalog;
                Policy = policy;
            }

            public EquipmentCatalog Catalog { get; }
            public EquipmentGenerationPolicy Policy { get; }
        }
    }

    public sealed class CraftingScrapSpendRewardChildState :
        IRewardChildState
    {
        private readonly ScrapWalletActions wallet;

        public CraftingScrapSpendRewardChildState(
            ScrapWalletActions wallet)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public StableId AuthorityStableId { get { return wallet.AuthorityStableId; } }
        public long Sequence { get { return wallet.Sequence; } }

        public RewardStatePreflightResult Preflight(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            List<RewardChildGrantCommand> ordered = CopyCommands(commands);
            var simulated = new ScrapWalletActions(
                wallet.AuthorityStableId,
                wallet.CurrencyStableId);
            ScrapSnapshotImportResult imported = simulated.ImportSnapshot(
                wallet.ExportSnapshot());
            if (!imported.Succeeded)
            {
                throw new InvalidOperationException(
                    imported.RejectionCode ?? "crafting-scrap-snapshot-import-failed");
            }

            var facts = new List<RewardStatePreflightFact>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommand child = ordered[index];
                string validationCode;
                RewardStateAdmissionStatus validationStatus;
                if (!TryValidateChild(
                    child,
                    out validationStatus,
                    out validationCode))
                {
                    facts.Add(new RewardStatePreflightFact(
                        child.TransactionStableId,
                        validationStatus,
                        validationCode));
                    continue;
                }

                ScrapTransactionResult result = simulated.Apply(
                    CreateTyped(simulated, child));
                facts.Add(MapPreflight(child, result));
            }

            return new RewardStatePreflightResult(facts);
        }

        public RewardChildApplyResult Apply(
            RewardChildGrantCommand command)
        {
            string validationCode = null;
            RewardStateAdmissionStatus ignored;
            if (command == null
                || !TryValidateChild(command, out ignored, out validationCode))
            {
                StableId transactionId = command == null
                    ? StableId.Parse("raptx.invalid")
                    : command.TransactionStableId;
                return new RewardChildApplyResult(
                    transactionId,
                    RewardChildApplyStatus.InvalidCommand,
                    false,
                    validationCode ?? "crafting-scrap-command-invalid");
            }

            ScrapTransactionResult result = wallet.Apply(
                CreateTyped(wallet, command));
            switch (result.Status)
            {
                case EconomyTransactionStatus.Applied:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.Applied,
                        true,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.ExactDuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.ExactDuplicateNoChange,
                        result.ChangeFact.OriginalLedgerStatus
                            == LedgerMutationStatus.Applied,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.ConflictingDuplicate:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.ConflictingDuplicate,
                        false,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.ExpectedSequenceConflict:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.ExpectedSequenceConflict,
                        false,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.InsufficientValue:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.InsufficientFunds,
                        false,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.InsufficientCapacity:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.CapacityRejected,
                        false,
                        result.ChangeFact.RejectionCode);
                default:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.Rejected,
                        false,
                        result.ChangeFact.RejectionCode);
            }
        }

        private bool TryValidateChild(
            RewardChildGrantCommand child,
            out RewardStateAdmissionStatus status,
            out string code)
        {
            if (child == null)
            {
                status = RewardStateAdmissionStatus.InvalidCommand;
                code = "crafting-scrap-command-null";
                return false;
            }
            if (child.GrantKind != RewardGrantKind.Scrap)
            {
                status = RewardStateAdmissionStatus.InvalidCommand;
                code = "crafting-scrap-kind-invalid";
                return false;
            }
            if (child.DestinationAuthorityStableId != AuthorityStableId)
            {
                status = RewardStateAdmissionStatus.AuthorityMismatch;
                code = "crafting-scrap-authority-mismatch";
                return false;
            }
            if (child.ContentStableId != wallet.CurrencyStableId)
            {
                status = RewardStateAdmissionStatus.InvalidCommand;
                code = "crafting-scrap-currency-mismatch";
                return false;
            }

            status = RewardStateAdmissionStatus.Accepted;
            code = null;
            return true;
        }

        private static RewardStatePreflightFact MapPreflight(
            RewardChildGrantCommand child,
            ScrapTransactionResult result)
        {
            RewardStateAdmissionStatus status;
            switch (result.Status)
            {
                case EconomyTransactionStatus.Applied:
                    status = RewardStateAdmissionStatus.Accepted;
                    break;
                case EconomyTransactionStatus.ExactDuplicateNoChange:
                    status = result.ChangeFact.OriginalLedgerStatus
                        == LedgerMutationStatus.Applied
                        ? RewardStateAdmissionStatus.AlreadyApplied
                        : RewardStateAdmissionStatus.Rejected;
                    break;
                case EconomyTransactionStatus.ConflictingDuplicate:
                    status = RewardStateAdmissionStatus.ConflictingDuplicate;
                    break;
                case EconomyTransactionStatus.ExpectedSequenceConflict:
                    status = RewardStateAdmissionStatus.ExpectedSequenceConflict;
                    break;
                case EconomyTransactionStatus.InsufficientValue:
                    status = RewardStateAdmissionStatus.InsufficientFunds;
                    break;
                case EconomyTransactionStatus.InsufficientCapacity:
                    status = RewardStateAdmissionStatus.CapacityRejected;
                    break;
                default:
                    status = RewardStateAdmissionStatus.InvalidCommand;
                    break;
            }

            return new RewardStatePreflightFact(
                child.TransactionStableId,
                status,
                result.ChangeFact.RejectionCode);
        }

        private static ScrapTransactionCommand CreateTyped(
            ScrapWalletActions destination,
            RewardChildGrantCommand command)
        {
            return new ScrapTransactionCommand(
                command.TransactionStableId,
                command.OperationStableId,
                destination.AuthorityStableId,
                destination.CurrencyStableId,
                ScrapMutationKind.Spend,
                command.Quantity,
                ScrapIdentity.CraftingSpendReason,
                new ScrapProvenance(
                    ScrapIdentity.CraftingSourceKind,
                    command.SourceOperationStableId,
                    command.ClaimantStableId),
                command.ExpectedSequence);
        }

        private static List<RewardChildGrantCommand> CopyCommands(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var copy = new List<RewardChildGrantCommand>(commands.Count);
            for (int index = 0; index < commands.Count; index++)
            {
                if (commands[index] == null)
                {
                    throw new ArgumentException(
                        "Commands must not contain null entries.",
                        nameof(commands));
                }
                copy.Add(commands[index]);
            }
            copy.Sort(delegate(
                RewardChildGrantCommand left,
                RewardChildGrantCommand right)
            {
                return left.TransactionStableId.CompareTo(
                    right.TransactionStableId);
            });
            return copy;
        }

        private static RewardChildApplyResult ApplyResult(
            RewardChildGrantCommand command,
            RewardChildApplyStatus status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResult(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    public sealed class CraftingUnusedMoneyRewardChildState :
        IRewardChildState
    {
        public static readonly StableId StableAuthorityId =
            StableId.Create("craft-money", "unused");

        public StableId AuthorityStableId { get { return StableAuthorityId; } }
        public long Sequence { get { return 0L; } }

        public RewardStatePreflightResult Preflight(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var facts = new List<RewardStatePreflightFact>();
            for (int index = 0; index < commands.Count; index++)
            {
                RewardChildGrantCommand command = commands[index]
                    ?? throw new ArgumentException(
                        "Commands must not contain null entries.",
                        nameof(commands));
                facts.Add(new RewardStatePreflightFact(
                    command.TransactionStableId,
                    RewardStateAdmissionStatus.InvalidCommand,
                    "crafting-money-grant-not-supported"));
            }
            return new RewardStatePreflightResult(facts);
        }

        public RewardChildApplyResult Apply(
            RewardChildGrantCommand command)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResult(
                id,
                RewardChildApplyStatus.InvalidCommand,
                false,
                "crafting-money-grant-not-supported");
        }
    }

    public static class CraftingRewardApplicationFactory
    {
        public static RewardApplicationActions Create(
            StableId rewardApplicationAuthorityStableId,
            ScrapWalletActions scrapWallet,
            IPlayerHoldingsState holdings,
            IEquipmentInstanceValidator equipmentValidator)
        {
            if (scrapWallet == null)
            {
                throw new ArgumentNullException(nameof(scrapWallet));
            }
            if (holdings == null)
            {
                throw new ArgumentNullException(nameof(holdings));
            }
            if (equipmentValidator == null)
            {
                throw new ArgumentNullException(nameof(equipmentValidator));
            }

            return new RewardApplicationActions(
                rewardApplicationAuthorityStableId,
                new CraftingUnusedMoneyRewardChildState(),
                new CraftingScrapSpendRewardChildState(scrapWallet),
                new PlayerHoldingsRewardChildState(
                    holdings,
                    equipmentValidator));
        }
    }
}

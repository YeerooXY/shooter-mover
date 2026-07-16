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
    public enum CraftingResultStatusV1
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

    public sealed class CraftEquipmentCommandV1 : IEquatable<CraftEquipmentCommandV1>
    {
        private readonly string canonicalText;

        public CraftEquipmentCommandV1(
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
            Fingerprint = CraftingCanonicalV1.Fingerprint(canonicalText);
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

        public bool Equals(CraftEquipmentCommandV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as CraftEquipmentCommandV1); }
        public override int GetHashCode() { return CraftingCanonicalV1.DeterministicHash(canonicalText); }

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

    public sealed class CraftingResultV1
    {
        internal CraftingResultV1(
            CraftingResultStatusV1 status,
            StableId recipeStableId,
            int? unlockLevel,
            long scrapCost,
            EquipmentInstance equipment,
            string recipeFingerprint,
            string commandFingerprint,
            RewardApplicationResultV1 rewardApplicationResult,
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

        public CraftingResultStatusV1 Status { get; }
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
        public RewardApplicationResultV1 RewardApplicationResult { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == CraftingResultStatusV1.Crafted
                    || Status == CraftingResultStatusV1.ExactDuplicateNoChange;
            }
        }
    }

    public sealed class CraftingServiceV1
    {
        private readonly CraftingRecipeCatalogV1 recipeCatalog;
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly RewardGenerationServiceV1 generator;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly ScrapWalletServiceV1 scrapWallet;
        private readonly StableId moneyAuthorityStableId;
        private readonly StableId holdingsAuthorityStableId;

        public CraftingServiceV1(
            CraftingRecipeCatalogV1 recipeCatalog,
            EquipmentCatalog equipmentCatalog,
            RewardGenerationServiceV1 generator,
            RewardApplicationServiceV1 rewardApplication,
            ScrapWalletServiceV1 scrapWallet,
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

        public CraftingResultV1 Craft(CraftEquipmentCommandV1 command)
        {
            if (command == null)
            {
                return Result(
                    CraftingResultStatusV1.InvalidCommand,
                    null,
                    null,
                    0L,
                    null,
                    null,
                    "command-null");
            }

            CraftingRecipeV1 recipe = recipeCatalog.Find(command.RecipeStableId);
            if (recipe == null)
            {
                return Result(
                    CraftingResultStatusV1.UnknownRecipe,
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
                    CraftingResultStatusV1.UnknownTargetEquipment,
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
                    CraftingResultStatusV1.ProgressionUnavailable,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    null,
                    null,
                    "crafting-not-unlocked",
                    unlockLevel);
            }

            StableId commitmentId = Derive("craftcommit", command, "commitment");
            RewardCommitmentSnapshotV1 existingCommitment;
            bool isReplay = rewardApplication.TryGetCommitment(
                commitmentId,
                out existingCommitment);
            if (!isReplay && scrapWallet.Balance < recipe.ScrapCost)
            {
                return Result(
                    CraftingResultStatusV1.InsufficientScrap,
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
                    CraftingResultStatusV1.InvalidRecipeForCatalog,
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
            EquipmentGenerationResultV1 generated = generator.GenerateEquipment(
                EquipmentGenerationRequestV1.Create(
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
                    CraftingResultStatusV1.GenerationRejected,
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
                    CraftingResultStatusV1.GenerationRejected,
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
            RewardGrantV1 scrapGrant = RewardGrantV1.Create(
                scrapGrantId,
                RewardGrantKindV1.Scrap,
                scrapWallet.CurrencyStableId,
                recipe.ScrapCost);
            RewardGrantV1 equipmentGrant = RewardGrantV1.Create(
                equipmentGrantId,
                RewardGrantKindV1.EquipmentReference,
                recipe.TargetEquipmentDefinitionStableId,
                1L);
            RewardResultV1 rewardResult = RewardResultV1.CreateGrants(
                commitmentId,
                sourceOperationId,
                new[] { scrapGrant, equipmentGrant });
            string contentFingerprint = CraftingCanonicalV1.Fingerprint(
                "schema=crafting-commit-content-v1"
                + "\ncommand_fingerprint=" + command.Fingerprint
                + "\nrecipe_fingerprint=" + recipe.Fingerprint
                + "\ngenerator_result_fingerprint=" + generated.ResultFingerprint
                + "\nequipment_fingerprint=" + generated.Equipment.Fingerprint);
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                command.RunStableId,
                Derive("craftsource", command, "source-instance"),
                sourceOperationId,
                commitmentId,
                recipe.GeneratorPolicy.PolicyStableId,
                contentFingerprint);
            RewardCommitCommandV1 commitCommand = RewardCommitCommandV1.Create(
                operation,
                rewardResult,
                contentFingerprint,
                new[]
                {
                    RewardGrantApplicationPayloadV1.ForValue(scrapGrant),
                    RewardGrantApplicationPayloadV1.ForEquipment(
                        equipmentGrant,
                        new[] { generated.Equipment }),
                });

            RewardApplicationResultV1 commit = rewardApplication.Commit(commitCommand);
            if (commit.Status == RewardApplicationResultStatusV1.ConflictingDuplicate)
            {
                return Result(
                    CraftingResultStatusV1.ConflictingDuplicate,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    generated.Equipment,
                    commit,
                    commit.RejectionCode,
                    unlockLevel);
            }
            if (commit.Status != RewardApplicationResultStatusV1.Generated
                && commit.Status != RewardApplicationResultStatusV1.ExactDuplicateNoChange)
            {
                return Result(
                    CraftingResultStatusV1.RewardApplicationRejected,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    generated.Equipment,
                    commit,
                    commit.RejectionCode,
                    unlockLevel);
            }

            StableId claimId = Derive("craftclaim", command, "claim");
            RewardApplicationResultV1 claim = rewardApplication.Claim(
                RewardClaimCommandV1.Create(
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

        private CraftingResultV1 MapClaim(
            CraftEquipmentCommandV1 command,
            CraftingRecipeV1 recipe,
            int unlockLevel,
            EquipmentInstance equipment,
            RewardApplicationResultV1 claim,
            StableId claimId,
            StableId commitmentId)
        {
            if (claim.Status == RewardApplicationResultStatusV1.Applied)
            {
                return Result(
                    CraftingResultStatusV1.Crafted,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    null,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatusV1.AlreadyAppliedNoChange
                || (claim.Status == RewardApplicationResultStatusV1.ExactDuplicateNoChange
                    && claim.CommitmentState == RewardCommitmentStateV1.Applied))
            {
                return Result(
                    CraftingResultStatusV1.ExactDuplicateNoChange,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    null,
                    unlockLevel);
            }
            if ((claim.Status == RewardApplicationResultStatusV1.ExactDuplicateNoChange
                    || claim.Status == RewardApplicationResultStatusV1.InvalidStateTransition)
                && claim.CommitmentState == RewardCommitmentStateV1.Claimed)
            {
                RewardApplicationResultV1 retry = rewardApplication.Retry(
                    RewardRetryClaimCommandV1.Create(commitmentId, claimId));
                if (retry.Status == RewardApplicationResultStatusV1.Applied)
                {
                    return Result(
                        CraftingResultStatusV1.Crafted,
                        command,
                        recipe,
                        recipe.ScrapCost,
                        equipment,
                        retry,
                        null,
                        unlockLevel);
                }
                if (retry.Status == RewardApplicationResultStatusV1.AlreadyAppliedNoChange)
                {
                    return Result(
                        CraftingResultStatusV1.ExactDuplicateNoChange,
                        command,
                        recipe,
                        recipe.ScrapCost,
                        equipment,
                        retry,
                        null,
                        unlockLevel);
                }
                return Result(
                    CraftingResultStatusV1.RewardApplicationRetryRequired,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    retry,
                    retry.RejectionCode,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatusV1.ClaimedPendingApplication)
            {
                return Result(
                    CraftingResultStatusV1.RewardApplicationRetryRequired,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    claim.RejectionCode,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatusV1.InsufficientFunds)
            {
                return Result(
                    CraftingResultStatusV1.InsufficientScrap,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    claim.RejectionCode,
                    unlockLevel);
            }
            if (claim.Status == RewardApplicationResultStatusV1.ConflictingDuplicate)
            {
                return Result(
                    CraftingResultStatusV1.ConflictingDuplicate,
                    command,
                    recipe,
                    recipe.ScrapCost,
                    equipment,
                    claim,
                    claim.RejectionCode,
                    unlockLevel);
            }
            return Result(
                CraftingResultStatusV1.RewardApplicationRejected,
                command,
                recipe,
                recipe.ScrapCost,
                equipment,
                claim,
                claim.RejectionCode,
                unlockLevel);
        }

        private bool TryPrepareGeneration(
            CraftingRecipeV1 recipe,
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
            var augmentCandidates = new List<AugmentGenerationCandidateV1>();
            for (int index = 0; index < recipe.AugmentOptions.Count; index++)
            {
                CraftingWeightedDefinitionV1 option = recipe.AugmentOptions[index];
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
                augmentCandidates.Add(AugmentGenerationCandidateV1.Create(
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

            var qualityCandidates = new List<EquipmentQualityCandidateV1>();
            for (int index = 0; index < recipe.QualityOptions.Count; index++)
            {
                CraftingWeightedDefinitionV1 option = recipe.QualityOptions[index];
                qualityCandidates.Add(EquipmentQualityCandidateV1.Create(
                    option.DefinitionStableId,
                    0L,
                    recipe.QualityPolicyKind == CraftingQualityPolicyKindV1.Fixed
                        ? 1UL
                        : option.Weight));
            }

            EquipmentGenerationPolicyV1 policy = EquipmentGenerationPolicyV1.Create(
                recipe.GeneratorPolicy.PolicyStableId,
                new[]
                {
                    EquipmentGenerationCandidateV1.Create(
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
            CraftEquipmentCommandV1 command,
            string purpose)
        {
            return CraftingCanonicalV1.DeriveStableId(
                namespaceName,
                purpose,
                command.CraftTransactionStableId.ToString());
        }

        private static CraftingResultV1 Result(
            CraftingResultStatusV1 status,
            CraftEquipmentCommandV1 command,
            CraftingRecipeV1 recipe,
            long scrapCost,
            EquipmentInstance equipment,
            RewardApplicationResultV1 rewardApplicationResult,
            string rejectionCode,
            int? unlockLevel = null)
        {
            return new CraftingResultV1(
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
                EquipmentGenerationPolicyV1 policy)
            {
                Catalog = catalog;
                Policy = policy;
            }

            public EquipmentCatalog Catalog { get; }
            public EquipmentGenerationPolicyV1 Policy { get; }
        }
    }

    public sealed class CraftingScrapSpendRewardChildAuthorityV1 :
        IRewardChildAuthorityV1
    {
        private readonly ScrapWalletServiceV1 wallet;

        public CraftingScrapSpendRewardChildAuthorityV1(
            ScrapWalletServiceV1 wallet)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public StableId AuthorityStableId { get { return wallet.AuthorityStableId; } }
        public long Sequence { get { return wallet.Sequence; } }

        public RewardAuthorityPreflightResultV1 Preflight(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            List<RewardChildGrantCommandV1> ordered = CopyCommands(commands);
            var simulated = new ScrapWalletServiceV1(
                wallet.AuthorityStableId,
                wallet.CurrencyStableId);
            ScrapSnapshotImportResultV1 imported = simulated.ImportSnapshot(
                wallet.ExportSnapshot());
            if (!imported.Succeeded)
            {
                throw new InvalidOperationException(
                    imported.RejectionCode ?? "crafting-scrap-snapshot-import-failed");
            }

            var facts = new List<RewardAuthorityPreflightFactV1>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommandV1 child = ordered[index];
                string validationCode;
                RewardAuthorityAdmissionStatusV1 validationStatus;
                if (!TryValidateChild(
                    child,
                    out validationStatus,
                    out validationCode))
                {
                    facts.Add(new RewardAuthorityPreflightFactV1(
                        child.TransactionStableId,
                        validationStatus,
                        validationCode));
                    continue;
                }

                ScrapTransactionResultV1 result = simulated.Apply(
                    CreateTyped(simulated, child));
                facts.Add(MapPreflight(child, result));
            }

            return new RewardAuthorityPreflightResultV1(facts);
        }

        public RewardChildApplyResultV1 Apply(
            RewardChildGrantCommandV1 command)
        {
            string validationCode = null;
            RewardAuthorityAdmissionStatusV1 ignored;
            if (command == null
                || !TryValidateChild(command, out ignored, out validationCode))
            {
                StableId transactionId = command == null
                    ? StableId.Parse("raptx.invalid")
                    : command.TransactionStableId;
                return new RewardChildApplyResultV1(
                    transactionId,
                    RewardChildApplyStatusV1.InvalidCommand,
                    false,
                    validationCode ?? "crafting-scrap-command-invalid");
            }

            ScrapTransactionResultV1 result = wallet.Apply(
                CreateTyped(wallet, command));
            switch (result.Status)
            {
                case EconomyTransactionStatusV1.Applied:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.Applied,
                        true,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.ExactDuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.ExactDuplicateNoChange,
                        result.ChangeFact.OriginalLedgerStatus
                            == LedgerMutationStatus.Applied,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.ConflictingDuplicate:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.ConflictingDuplicate,
                        false,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.ExpectedSequenceConflict:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.ExpectedSequenceConflict,
                        false,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.InsufficientValue:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.InsufficientFunds,
                        false,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.InsufficientCapacity:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.CapacityRejected,
                        false,
                        result.ChangeFact.RejectionCode);
                default:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        result.ChangeFact.RejectionCode);
            }
        }

        private bool TryValidateChild(
            RewardChildGrantCommandV1 child,
            out RewardAuthorityAdmissionStatusV1 status,
            out string code)
        {
            if (child == null)
            {
                status = RewardAuthorityAdmissionStatusV1.InvalidCommand;
                code = "crafting-scrap-command-null";
                return false;
            }
            if (child.GrantKind != RewardGrantKindV1.Scrap)
            {
                status = RewardAuthorityAdmissionStatusV1.InvalidCommand;
                code = "crafting-scrap-kind-invalid";
                return false;
            }
            if (child.DestinationAuthorityStableId != AuthorityStableId)
            {
                status = RewardAuthorityAdmissionStatusV1.AuthorityMismatch;
                code = "crafting-scrap-authority-mismatch";
                return false;
            }
            if (child.ContentStableId != wallet.CurrencyStableId)
            {
                status = RewardAuthorityAdmissionStatusV1.InvalidCommand;
                code = "crafting-scrap-currency-mismatch";
                return false;
            }

            status = RewardAuthorityAdmissionStatusV1.Accepted;
            code = null;
            return true;
        }

        private static RewardAuthorityPreflightFactV1 MapPreflight(
            RewardChildGrantCommandV1 child,
            ScrapTransactionResultV1 result)
        {
            RewardAuthorityAdmissionStatusV1 status;
            switch (result.Status)
            {
                case EconomyTransactionStatusV1.Applied:
                    status = RewardAuthorityAdmissionStatusV1.Accepted;
                    break;
                case EconomyTransactionStatusV1.ExactDuplicateNoChange:
                    status = result.ChangeFact.OriginalLedgerStatus
                        == LedgerMutationStatus.Applied
                        ? RewardAuthorityAdmissionStatusV1.AlreadyApplied
                        : RewardAuthorityAdmissionStatusV1.Rejected;
                    break;
                case EconomyTransactionStatusV1.ConflictingDuplicate:
                    status = RewardAuthorityAdmissionStatusV1.ConflictingDuplicate;
                    break;
                case EconomyTransactionStatusV1.ExpectedSequenceConflict:
                    status = RewardAuthorityAdmissionStatusV1.ExpectedSequenceConflict;
                    break;
                case EconomyTransactionStatusV1.InsufficientValue:
                    status = RewardAuthorityAdmissionStatusV1.InsufficientFunds;
                    break;
                case EconomyTransactionStatusV1.InsufficientCapacity:
                    status = RewardAuthorityAdmissionStatusV1.CapacityRejected;
                    break;
                default:
                    status = RewardAuthorityAdmissionStatusV1.InvalidCommand;
                    break;
            }

            return new RewardAuthorityPreflightFactV1(
                child.TransactionStableId,
                status,
                result.ChangeFact.RejectionCode);
        }

        private static ScrapTransactionCommandV1 CreateTyped(
            ScrapWalletServiceV1 destination,
            RewardChildGrantCommandV1 command)
        {
            return new ScrapTransactionCommandV1(
                command.TransactionStableId,
                command.OperationStableId,
                destination.AuthorityStableId,
                destination.CurrencyStableId,
                ScrapMutationKindV1.Spend,
                command.Quantity,
                ScrapIdentityV1.CraftingSpendReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.CraftingSourceKind,
                    command.SourceOperationStableId,
                    command.ClaimantStableId),
                command.ExpectedSequence);
        }

        private static List<RewardChildGrantCommandV1> CopyCommands(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var copy = new List<RewardChildGrantCommandV1>(commands.Count);
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
                RewardChildGrantCommandV1 left,
                RewardChildGrantCommandV1 right)
            {
                return left.TransactionStableId.CompareTo(
                    right.TransactionStableId);
            });
            return copy;
        }

        private static RewardChildApplyResultV1 ApplyResult(
            RewardChildGrantCommandV1 command,
            RewardChildApplyStatusV1 status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResultV1(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    public sealed class CraftingUnusedMoneyRewardChildAuthorityV1 :
        IRewardChildAuthorityV1
    {
        public static readonly StableId StableAuthorityId =
            StableId.Create("craft-money", "unused");

        public StableId AuthorityStableId { get { return StableAuthorityId; } }
        public long Sequence { get { return 0L; } }

        public RewardAuthorityPreflightResultV1 Preflight(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var facts = new List<RewardAuthorityPreflightFactV1>();
            for (int index = 0; index < commands.Count; index++)
            {
                RewardChildGrantCommandV1 command = commands[index]
                    ?? throw new ArgumentException(
                        "Commands must not contain null entries.",
                        nameof(commands));
                facts.Add(new RewardAuthorityPreflightFactV1(
                    command.TransactionStableId,
                    RewardAuthorityAdmissionStatusV1.InvalidCommand,
                    "crafting-money-grant-not-supported"));
            }
            return new RewardAuthorityPreflightResultV1(facts);
        }

        public RewardChildApplyResultV1 Apply(
            RewardChildGrantCommandV1 command)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResultV1(
                id,
                RewardChildApplyStatusV1.InvalidCommand,
                false,
                "crafting-money-grant-not-supported");
        }
    }

    public static class CraftingRewardApplicationFactoryV1
    {
        public static RewardApplicationServiceV1 Create(
            StableId rewardApplicationAuthorityStableId,
            ScrapWalletServiceV1 scrapWallet,
            IPlayerHoldingsAuthorityV1 holdings,
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

            return new RewardApplicationServiceV1(
                rewardApplicationAuthorityStableId,
                new CraftingUnusedMoneyRewardChildAuthorityV1(),
                new CraftingScrapSpendRewardChildAuthorityV1(scrapWallet),
                new PlayerHoldingsRewardChildAuthorityV1(
                    holdings,
                    equipmentValidator));
        }
    }
}

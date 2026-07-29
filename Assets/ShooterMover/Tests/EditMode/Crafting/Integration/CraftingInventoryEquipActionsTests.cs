using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Crafting;
using ShooterMover.Application.Crafting.Integration;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Crafting.Integration
{
    public sealed class CraftingInventoryEquipActionsTests
    {
        private static readonly StableId CommonQuality =
            Id("quality.common");
        private static readonly StableId EquipmentAlpha =
            Id("equipment.alpha");
        private static readonly StableId EquipmentBeta =
            Id("equipment.beta");
        private static readonly StableId WeaponSlotOne =
            Id("loadout-slot.weapon-1");
        private static readonly StableId WeaponSlotTwo =
            Id("loadout-slot.weapon-2");

        [Test]
        public void SuccessSpendsScrapInsertsOneInstanceAndEquipsThatInstance()
        {
            var fixture = new Fixture();

            CraftingInventoryEquipResult result =
                fixture.Integration.CraftAndEquip(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatus.Applied));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.AppliedCount, Is.EqualTo(1));
            Assert.That(
                fixture.Loadout.LastAppliedCommand.EquipmentInstanceStableId,
                Is.EqualTo(result.EquipmentInstanceStableId));
            Assert.That(
                fixture.Loadout.LastAppliedCommand.EquipmentFingerprint,
                Is.EqualTo(result.EquipmentFingerprint));
            AssertCraftedHolding(fixture, result);
        }

        [Test]
        public void RecipeSelectionCraftsAndEquipsTheSelectedTarget()
        {
            CraftingRecipe alpha = CreateRecipe(
                Id("recipe.alpha"),
                EquipmentAlpha);
            CraftingRecipe beta = CreateRecipe(
                Id("recipe.beta"),
                EquipmentBeta);
            var fixture = new Fixture(recipes: new[] { alpha, beta });

            CraftingInventoryEquipResult result =
                fixture.Integration.CraftAndEquip(
                    fixture.Command(
                        recipeId: beta.RecipeStableId,
                        craftId: Id("craft.beta")));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.CraftingResult.Equipment.DefinitionId,
                Is.EqualTo(EquipmentBeta));
            Assert.That(
                fixture.Loadout.LastAppliedCommand.EquipmentInstanceStableId,
                Is.EqualTo(result.EquipmentInstanceStableId));
        }

        [Test]
        public void ExactReplayDoesNotSpendGrantOrEquipTwice()
        {
            var fixture = new Fixture();
            CraftAndEquipCommand command = fixture.Command();

            CraftingInventoryEquipResult first =
                fixture.Integration.CraftAndEquip(command);
            CraftingInventoryEquipResult replay =
                fixture.Integration.CraftAndEquip(command);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatus.ExactDuplicateNoChange));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.AppliedCount, Is.EqualTo(1));
            Assert.That(
                replay.EquipmentInstanceStableId,
                Is.EqualTo(first.EquipmentInstanceStableId));
            Assert.That(
                replay.EquipmentFingerprint,
                Is.EqualTo(first.EquipmentFingerprint));
        }

        [Test]
        public void InsufficientScrapDoesNotReachLoadout()
        {
            var fixture = new Fixture(initialScrap: 9L);

            CraftingInventoryEquipResult result =
                fixture.Integration.CraftAndEquip(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatus.CraftRejected));
            Assert.That(
                result.CraftingResult.Status,
                Is.EqualTo(CraftingResultStatus.InsufficientScrap));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(9L));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(fixture.Loadout.CallCount, Is.Zero);
        }

        [Test]
        public void CraftingLevelGateDoesNotReachLoadout()
        {
            var fixture = new Fixture();

            CraftingInventoryEquipResult result =
                fixture.Integration.CraftAndEquip(
                    fixture.Command(characterLevel: 54));

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatus.CraftRejected));
            Assert.That(
                result.CraftingResult.Status,
                Is.EqualTo(CraftingResultStatus.ProgressionUnavailable));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(100L));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(fixture.Loadout.CallCount, Is.Zero);
        }

        [Test]
        public void InterruptedCraftRollsForwardBeforeEquipWithoutDuplication()
        {
            var fixture = new Fixture(failHoldingsApplyOnce: true);
            CraftAndEquipCommand command = fixture.Command();

            CraftingInventoryEquipResult interrupted =
                fixture.Integration.CraftAndEquip(command);

            Assert.That(
                interrupted.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatus.CraftRetryRequired));
            Assert.That(fixture.Loadout.CallCount, Is.Zero);

            CraftingInventoryEquipResult retry =
                fixture.Integration.CraftAndEquip(command);

            Assert.That(fixture.Loadout.AppliedCount, Is.EqualTo(1));
            Assert.That(retry.Succeeded, Is.True);
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.Sequence, Is.EqualTo(1L));
            Assert.That(
                retry.EquipmentInstanceStableId,
                Is.EqualTo(interrupted.EquipmentInstanceStableId));
            Assert.That(
                retry.EquipmentFingerprint,
                Is.EqualTo(interrupted.EquipmentFingerprint));
        }

        [Test]
        public void InterruptedEquipRetriesSameCraftedInstanceWithoutRecrafting()
        {
            var loadout = new RecordingLoadoutPort(retryFirst: true);
            var fixture = new Fixture(loadout: loadout);
            CraftAndEquipCommand command = fixture.Command();

            CraftingInventoryEquipResult interrupted =
                fixture.Integration.CraftAndEquip(command);
            CraftingInventoryEquipResult retry =
                fixture.Integration.CraftAndEquip(command);

            Assert.That(
                interrupted.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatus.EquipRetryRequired));
            Assert.That(
                retry.Status,
                Is.EqualTo(CraftingInventoryEquipStatus.Applied));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.AppliedCount, Is.EqualTo(1));
            Assert.That(
                retry.EquipmentInstanceStableId,
                Is.EqualTo(interrupted.EquipmentInstanceStableId));
            Assert.That(
                retry.EquipmentFingerprint,
                Is.EqualTo(interrupted.EquipmentFingerprint));
        }

        [Test]
        public void SameCraftIdentityWithDifferentSlotIsConflictingDuplicate()
        {
            var fixture = new Fixture();
            CraftAndEquipCommand first = fixture.Command(
                slotId: WeaponSlotOne);
            CraftAndEquipCommand conflict = fixture.Command(
                slotId: WeaponSlotTwo);

            CraftingInventoryEquipResult applied =
                fixture.Integration.CraftAndEquip(first);
            CraftingInventoryEquipResult rejected =
                fixture.Integration.CraftAndEquip(conflict);

            Assert.That(applied.Succeeded, Is.True);
            Assert.That(
                rejected.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatus.ConflictingDuplicate));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.AppliedCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingCraftDuplicateNeverIssuesAnotherEquip()
        {
            var fixture = new Fixture();
            CraftAndEquipCommand first =
                fixture.Command(rootSeed: 10UL);
            CraftAndEquipCommand conflict =
                fixture.Command(rootSeed: 11UL);

            fixture.Integration.CraftAndEquip(first);
            CraftingInventoryEquipResult rejected =
                fixture.Integration.CraftAndEquip(conflict);

            Assert.That(
                rejected.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatus.ConflictingDuplicate));
            Assert.That(
                rejected.CraftingResult.Status,
                Is.EqualTo(CraftingResultStatus.ConflictingDuplicate));
            Assert.That(fixture.Loadout.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void CraftedItemRemainsDistinctFromStrongboxOriginItem()
        {
            var fixture = new Fixture();
            EquipmentInstance strongboxItem = EquipmentInstance.Create(
                Id("equipment-instance.strongbox-origin"),
                EquipmentAlpha,
                55,
                CommonQuality,
                Array.Empty<AugmentInstance>());
            HoldingProvenance strongboxProvenance =
                HoldingProvenance.Create(
                    Id("strongbox-grant.equipment"),
                    Id("strongbox-source.opening"));
            PlayerHoldingsMutationResult seeded = fixture.Holdings.Apply(
                PlayerHoldingsCommand.AddEquipment(
                    Id("holdings-tx.strongbox-origin"),
                    Id("holdings-op.strongbox-origin"),
                    fixture.Holdings.AuthorityStableId,
                    strongboxItem,
                    strongboxProvenance));

            Assert.That(
                seeded.Status,
                Is.EqualTo(PlayerHoldingsMutationStatus.Applied));

            CraftingInventoryEquipResult result =
                fixture.Integration.CraftAndEquip(fixture.Command());
            PlayerHoldingsSnapshot snapshot =
                fixture.Holdings.ExportSnapshot();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(snapshot.UniqueHoldings.Count, Is.EqualTo(2));
            Assert.That(
                result.EquipmentInstanceStableId,
                Is.Not.EqualTo(strongboxItem.InstanceId));
            Assert.That(
                fixture.Loadout.LastAppliedCommand.EquipmentInstanceStableId,
                Is.EqualTo(result.EquipmentInstanceStableId));

            UniqueHoldingSnapshot crafted =
                FindUnique(snapshot, result.EquipmentInstanceStableId);
            UniqueHoldingSnapshot strongbox =
                FindUnique(snapshot, strongboxItem.InstanceId);
            Assert.That(crafted, Is.Not.Null);
            Assert.That(strongbox, Is.Not.Null);
            Assert.That(
                crafted.Provenance.Fingerprint,
                Is.Not.EqualTo(strongbox.Provenance.Fingerprint));
            Assert.That(
                crafted.Provenance.GrantStableId,
                Is.EqualTo(
                    CraftingIntegrationIdentity
                        .EquipmentGrantStableId(
                            fixture.LastCraftCommand)));
        }

        [Test]
        public void MismatchedLoadoutResponseCannotReportSuccess()
        {
            var fixture = new Fixture(
                loadout: new RecordingLoadoutPort(mismatchFirst: true));

            CraftingInventoryEquipResult result =
                fixture.Integration.CraftAndEquip(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatus.EquipRejected));
            Assert.That(
                result.RejectionCode,
                Is.EqualTo("loadout-result-mismatch"));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void NullCommandIsRejectedWithoutCallingAuthorities()
        {
            var fixture = new Fixture();
            long scrapSequence = fixture.Scrap.Sequence;
            long holdingsSequence = fixture.Holdings.Sequence;

            CraftingInventoryEquipResult result =
                fixture.Integration.CraftAndEquip(null);

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatus.InvalidCommand));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequence));
            Assert.That(
                fixture.Holdings.Sequence,
                Is.EqualTo(holdingsSequence));
            Assert.That(fixture.Loadout.CallCount, Is.Zero);
        }

        private static void AssertCraftedHolding(
            Fixture fixture,
            CraftingInventoryEquipResult result)
        {
            UniqueHoldingSnapshot holding = FindUnique(
                fixture.Holdings.ExportSnapshot(),
                result.EquipmentInstanceStableId);
            Assert.That(holding, Is.Not.Null);
            Assert.That(
                holding.EquipmentInstance.Fingerprint,
                Is.EqualTo(result.EquipmentFingerprint));
            Assert.That(
                holding.Provenance.GrantStableId,
                Is.EqualTo(
                    CraftingIntegrationIdentity
                        .EquipmentGrantStableId(
                            fixture.LastCraftCommand)));
            Assert.That(
                holding.Provenance.SourceStableId,
                Is.EqualTo(
                    CraftingIntegrationIdentity
                        .SourceOperationStableId(
                            fixture.LastCraftCommand)));
        }

        private static UniqueHoldingSnapshot FindUnique(
            PlayerHoldingsSnapshot snapshot,
            StableId instanceId)
        {
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshot candidate =
                    snapshot.UniqueHoldings[index];
                if (candidate != null
                    && Equals(candidate.InstanceStableId, instanceId))
                {
                    return candidate;
                }
            }

            return null;
        }

        private sealed class Fixture
        {
            public Fixture(
                long initialScrap = 100L,
                IEnumerable<CraftingRecipe> recipes = null,
                bool failHoldingsApplyOnce = false,
                RecordingLoadoutPort loadout = null)
            {
                Catalog = BuildEquipmentCatalog();
                Validator = new CatalogValidator(Catalog);
                Scrap = new ScrapWalletActions(
                    Id("authority.scrap"),
                    Id("currency.scrap"));
                Fund(Scrap, initialScrap);
                Holdings = new PlayerHoldingsActions(
                    Id("holdings.player"),
                    1000L,
                    Validator);

                IRewardChildState holdingsAdapter =
                    new PlayerHoldingsRewardChildState(
                        Holdings,
                        Validator);
                if (failHoldingsApplyOnce)
                {
                    holdingsAdapter =
                        new FailOnceApplyState(holdingsAdapter);
                }

                Rap = new RewardApplicationActions(
                    Id("authority.crafting-rap"),
                    new CraftingUnusedMoneyRewardChildState(),
                    new CraftingScrapSpendRewardChildState(Scrap),
                    holdingsAdapter);

                CraftingRecipe[] recipeArray = recipes == null
                    ? new[] { CreateRecipe() }
                    : new List<CraftingRecipe>(recipes).ToArray();
                PrimaryRecipe = recipeArray[0];
                Crafting = new CraftingActions(
                    new CraftingRecipeCatalog(recipeArray),
                    Catalog,
                    new RewardGenerationActions(),
                    Rap,
                    Scrap,
                    CraftingUnusedMoneyRewardChildState
                        .StableAuthorityId,
                    Holdings.AuthorityStableId);
                Loadout = loadout ?? new RecordingLoadoutPort();
                Integration = new CraftingInventoryEquipActions(
                    Crafting,
                    Holdings,
                    Loadout);
            }

            public EquipmentCatalog Catalog { get; }

            public CatalogValidator Validator { get; }

            public ScrapWalletActions Scrap { get; }

            public PlayerHoldingsActions Holdings { get; }

            public RewardApplicationActions Rap { get; }

            public CraftingRecipe PrimaryRecipe { get; }

            public CraftingActions Crafting { get; }

            public RecordingLoadoutPort Loadout { get; }

            public CraftingInventoryEquipActions Integration { get; }

            public CraftEquipmentCommand LastCraftCommand { get; private set; }

            public CraftAndEquipCommand Command(
                int characterLevel = 60,
                ulong rootSeed = 44UL,
                StableId craftId = null,
                StableId recipeId = null,
                StableId slotId = null,
                long? expectedLoadoutSequence = null)
            {
                LastCraftCommand = new CraftEquipmentCommand(
                    craftId ?? Id("craft.transaction"),
                    recipeId ?? PrimaryRecipe.RecipeStableId,
                    Id("run.test"),
                    Id("player.test"),
                    Context(characterLevel),
                    rootSeed);
                return new CraftAndEquipCommand(
                    LastCraftCommand,
                    slotId ?? WeaponSlotOne,
                    expectedLoadoutSequence);
            }
        }

        private sealed class RecordingLoadoutPort :
            ICraftedEquipmentLoadoutPort
        {
            private readonly Dictionary<StableId, AppliedRecord> records =
                new Dictionary<StableId, AppliedRecord>();
            private bool retryFirst;
            private bool mismatchFirst;

            public RecordingLoadoutPort(
                bool retryFirst = false,
                bool mismatchFirst = false)
            {
                this.retryFirst = retryFirst;
                this.mismatchFirst = mismatchFirst;
            }

            public StableId AuthorityStableId
            {
                get { return Id("authority.loadout"); }
            }

            public long Sequence { get; private set; }

            public int CallCount { get; private set; }

            public int AppliedCount { get; private set; }

            public CraftedEquipmentEquipCommand LastAppliedCommand
            {
                get;
                private set;
            }

            public CraftedEquipmentEquipResult Apply(
                CraftedEquipmentEquipCommand command)
            {
                CallCount++;
                AppliedRecord existing;
                if (records.TryGetValue(
                    command.TransactionStableId,
                    out existing))
                {
                    if (!string.Equals(
                        existing.CommandFingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return CraftedEquipmentEquipResult.FromCommand(
                            command,
                            CraftedEquipmentEquipStatus
                                .ConflictingDuplicate,
                            Sequence,
                            false,
                            "loadout-transaction-conflict");
                    }

                    return CraftedEquipmentEquipResult.FromCommand(
                        command,
                        CraftedEquipmentEquipStatus
                            .ExactDuplicateNoChange,
                        Sequence,
                        existing.OriginalApplied,
                        existing.RejectionCode);
                }

                if (retryFirst)
                {
                    retryFirst = false;
                    return CraftedEquipmentEquipResult.FromCommand(
                        command,
                        CraftedEquipmentEquipStatus.RetryRequired,
                        Sequence,
                        false,
                        "test-loadout-interruption");
                }

                if (mismatchFirst)
                {
                    mismatchFirst = false;
                    return new CraftedEquipmentEquipResult(
                        CraftedEquipmentEquipStatus.Applied,
                        command.TransactionStableId,
                        command.OperationStableId,
                        Id("loadout-slot.mismatch"),
                        command.EquipmentInstanceStableId,
                        command.Fingerprint,
                        Sequence,
                        true,
                        null);
                }

                if (command.ExpectedLoadoutSequence.HasValue
                    && command.ExpectedLoadoutSequence.Value != Sequence)
                {
                    return CraftedEquipmentEquipResult.FromCommand(
                        command,
                        CraftedEquipmentEquipStatus.Rejected,
                        Sequence,
                        false,
                        "loadout-expected-sequence-conflict");
                }

                Sequence = checked(Sequence + 1L);
                AppliedCount++;
                LastAppliedCommand = command;
                records.Add(
                    command.TransactionStableId,
                    new AppliedRecord(command.Fingerprint, true, null));
                return CraftedEquipmentEquipResult.FromCommand(
                    command,
                    CraftedEquipmentEquipStatus.Applied,
                    Sequence,
                    true,
                    null);
            }

            private sealed class AppliedRecord
            {
                public AppliedRecord(
                    string commandFingerprint,
                    bool originalApplied,
                    string rejectionCode)
                {
                    CommandFingerprint = commandFingerprint;
                    OriginalApplied = originalApplied;
                    RejectionCode = rejectionCode;
                }

                public string CommandFingerprint { get; }

                public bool OriginalApplied { get; }

                public string RejectionCode { get; }
            }
        }

        private sealed class CatalogValidator :
            IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public CatalogValidator(EquipmentCatalog catalog)
            {
                this.catalog = catalog;
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                EquipmentInstance instance = request == null
                    ? null
                    : request.Instance;
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    instance,
                    catalog.ValidateInstance(instance));
            }
        }

        private sealed class FailOnceApplyState :
            IRewardChildState
        {
            private readonly IRewardChildState inner;
            private bool failed;

            public FailOnceApplyState(
                IRewardChildState inner)
            {
                this.inner = inner;
            }

            public StableId AuthorityStableId
            {
                get { return inner.AuthorityStableId; }
            }

            public long Sequence
            {
                get { return inner.Sequence; }
            }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResult Apply(
                RewardChildGrantCommand command)
            {
                if (!failed)
                {
                    failed = true;
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.Rejected,
                        false,
                        "test-interruption");
                }

                return inner.Apply(command);
            }
        }

        private static CraftingRecipe CreateRecipe(
            StableId recipeId = null,
            StableId target = null)
        {
            return new CraftingRecipe(
                1,
                recipeId ?? Id("recipe.alpha"),
                target ?? EquipmentAlpha,
                Id("progression-source.equipment"),
                50,
                50,
                5,
                new CraftingDelayVariance(0, 0),
                10L,
                CraftingQualityPolicyKind.Fixed,
                new[]
                {
                    new CraftingWeightedDefinition(
                        CommonQuality,
                        1UL),
                },
                50,
                60,
                0,
                0,
                1,
                1,
                Array.Empty<CraftingWeightedDefinition>(),
                new CraftingGeneratorPolicy(
                    Id("generator-policy.crafting"),
                    1,
                    new SoftActivationCurveParameters(
                        0.25,
                        2L,
                        2L),
                    new ObsolescenceCurveParameters(
                        1000L,
                        1000.0,
                        1.0)));
        }

        private static EquipmentCatalog BuildEquipmentCatalog()
        {
            EquipmentQualityTier[] qualities =
            {
                EquipmentQualityTier.Create(
                    CommonQuality,
                    "Common",
                    1),
            };
            EquipmentDefinition alpha =
                EquipmentDefinition.Create(
                    EquipmentAlpha,
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.alpha"),
                    "Alpha",
                    Id("weapon.alpha"),
                    InclusiveIntRange.Create(1, 100),
                    0,
                    qualities,
                    Array.Empty<StableId>());
            EquipmentDefinition beta =
                EquipmentDefinition.Create(
                    EquipmentBeta,
                    EquipmentCategoryIds.Weapon,
                    Id("equipment-family.beta"),
                    "Beta",
                    Id("weapon.beta"),
                    InclusiveIntRange.Create(1, 100),
                    0,
                    qualities,
                    Array.Empty<StableId>());
            EquipmentCatalogBuildResult build =
                EquipmentCatalog.Build(
                    new[] { alpha, beta },
                    Array.Empty<AugmentDefinition>());
            Assert.That(build.IsValid, Is.True);
            return build.Catalog;
        }

        private static void Fund(
            ScrapWalletActions wallet,
            long amount)
        {
            if (amount == 0L)
            {
                return;
            }

            ScrapTransactionResult result = wallet.Apply(
                new ScrapTransactionCommand(
                    Id("scrap-tx.initial"),
                    Id("scrap-op.initial"),
                    wallet.AuthorityStableId,
                    wallet.CurrencyStableId,
                    ScrapMutationKind.Grant,
                    amount,
                    ScrapIdentity.RewardGrantReason,
                    new ScrapProvenance(
                        ScrapIdentity.RewardSourceKind,
                        Id("reward-op.initial"),
                        Id("player.test"))));
            Assert.That(result.ChangedState, Is.True);
        }

        private static ProgressionContext Context(
            int characterLevel)
        {
            return ProgressionContext.Create(
                characterLevel,
                1,
                Id("difficulty.normal"),
                1,
                Array.Empty<StableId>());
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}

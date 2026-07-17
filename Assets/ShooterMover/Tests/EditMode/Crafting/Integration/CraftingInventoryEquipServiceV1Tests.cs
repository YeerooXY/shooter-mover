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
    public sealed class CraftingInventoryEquipServiceV1Tests
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

            CraftingInventoryEquipResultV1 result =
                fixture.Integration.CraftAndEquip(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatusV1.Applied));
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
            CraftingRecipeV1 alpha = CreateRecipe(
                Id("recipe.alpha"),
                EquipmentAlpha);
            CraftingRecipeV1 beta = CreateRecipe(
                Id("recipe.beta"),
                EquipmentBeta);
            var fixture = new Fixture(recipes: new[] { alpha, beta });

            CraftingInventoryEquipResultV1 result =
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
            CraftAndEquipCommandV1 command = fixture.Command();

            CraftingInventoryEquipResultV1 first =
                fixture.Integration.CraftAndEquip(command);
            CraftingInventoryEquipResultV1 replay =
                fixture.Integration.CraftAndEquip(command);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatusV1.ExactDuplicateNoChange));
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

            CraftingInventoryEquipResultV1 result =
                fixture.Integration.CraftAndEquip(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatusV1.CraftRejected));
            Assert.That(
                result.CraftingResult.Status,
                Is.EqualTo(CraftingResultStatusV1.InsufficientScrap));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(9L));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(fixture.Loadout.CallCount, Is.Zero);
        }

        [Test]
        public void CraftingLevelGateDoesNotReachLoadout()
        {
            var fixture = new Fixture();

            CraftingInventoryEquipResultV1 result =
                fixture.Integration.CraftAndEquip(
                    fixture.Command(characterLevel: 54));

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatusV1.CraftRejected));
            Assert.That(
                result.CraftingResult.Status,
                Is.EqualTo(CraftingResultStatusV1.ProgressionUnavailable));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(100L));
            Assert.That(fixture.Holdings.Sequence, Is.Zero);
            Assert.That(fixture.Loadout.CallCount, Is.Zero);
        }

        [Test]
        public void InterruptedCraftRollsForwardBeforeEquipWithoutDuplication()
        {
            var fixture = new Fixture(failHoldingsApplyOnce: true);
            CraftAndEquipCommandV1 command = fixture.Command();

            CraftingInventoryEquipResultV1 interrupted =
                fixture.Integration.CraftAndEquip(command);

            Assert.That(
                interrupted.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatusV1.CraftRetryRequired));
            Assert.That(fixture.Loadout.CallCount, Is.Zero);

            CraftingInventoryEquipResultV1 retry =
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
            CraftAndEquipCommandV1 command = fixture.Command();

            CraftingInventoryEquipResultV1 interrupted =
                fixture.Integration.CraftAndEquip(command);
            CraftingInventoryEquipResultV1 retry =
                fixture.Integration.CraftAndEquip(command);

            Assert.That(
                interrupted.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatusV1.EquipRetryRequired));
            Assert.That(
                retry.Status,
                Is.EqualTo(CraftingInventoryEquipStatusV1.Applied));
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
            CraftAndEquipCommandV1 first = fixture.Command(
                slotId: WeaponSlotOne);
            CraftAndEquipCommandV1 conflict = fixture.Command(
                slotId: WeaponSlotTwo);

            CraftingInventoryEquipResultV1 applied =
                fixture.Integration.CraftAndEquip(first);
            CraftingInventoryEquipResultV1 rejected =
                fixture.Integration.CraftAndEquip(conflict);

            Assert.That(applied.Succeeded, Is.True);
            Assert.That(
                rejected.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatusV1.ConflictingDuplicate));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(90L));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.Sequence, Is.EqualTo(1L));
            Assert.That(fixture.Loadout.AppliedCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingCraftDuplicateNeverIssuesAnotherEquip()
        {
            var fixture = new Fixture();
            CraftAndEquipCommandV1 first =
                fixture.Command(rootSeed: 10UL);
            CraftAndEquipCommandV1 conflict =
                fixture.Command(rootSeed: 11UL);

            fixture.Integration.CraftAndEquip(first);
            CraftingInventoryEquipResultV1 rejected =
                fixture.Integration.CraftAndEquip(conflict);

            Assert.That(
                rejected.Status,
                Is.EqualTo(
                    CraftingInventoryEquipStatusV1.ConflictingDuplicate));
            Assert.That(
                rejected.CraftingResult.Status,
                Is.EqualTo(CraftingResultStatusV1.ConflictingDuplicate));
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
            HoldingProvenanceV1 strongboxProvenance =
                HoldingProvenanceV1.Create(
                    Id("strongbox-grant.equipment"),
                    Id("strongbox-source.opening"));
            PlayerHoldingsMutationResultV1 seeded = fixture.Holdings.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    Id("holdings-tx.strongbox-origin"),
                    Id("holdings-op.strongbox-origin"),
                    fixture.Holdings.AuthorityStableId,
                    strongboxItem,
                    strongboxProvenance));

            Assert.That(
                seeded.Status,
                Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));

            CraftingInventoryEquipResultV1 result =
                fixture.Integration.CraftAndEquip(fixture.Command());
            PlayerHoldingsSnapshotV1 snapshot =
                fixture.Holdings.ExportSnapshot();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(snapshot.UniqueHoldings.Count, Is.EqualTo(2));
            Assert.That(
                result.EquipmentInstanceStableId,
                Is.Not.EqualTo(strongboxItem.InstanceId));
            Assert.That(
                fixture.Loadout.LastAppliedCommand.EquipmentInstanceStableId,
                Is.EqualTo(result.EquipmentInstanceStableId));

            UniqueHoldingSnapshotV1 crafted =
                FindUnique(snapshot, result.EquipmentInstanceStableId);
            UniqueHoldingSnapshotV1 strongbox =
                FindUnique(snapshot, strongboxItem.InstanceId);
            Assert.That(crafted, Is.Not.Null);
            Assert.That(strongbox, Is.Not.Null);
            Assert.That(
                crafted.Provenance.Fingerprint,
                Is.Not.EqualTo(strongbox.Provenance.Fingerprint));
            Assert.That(
                crafted.Provenance.GrantStableId,
                Is.EqualTo(
                    CraftingIntegrationIdentityV1
                        .EquipmentGrantStableId(
                            fixture.LastCraftCommand)));
        }

        [Test]
        public void MismatchedLoadoutResponseCannotReportSuccess()
        {
            var fixture = new Fixture(
                loadout: new RecordingLoadoutPort(mismatchFirst: true));

            CraftingInventoryEquipResultV1 result =
                fixture.Integration.CraftAndEquip(fixture.Command());

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatusV1.EquipRejected));
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

            CraftingInventoryEquipResultV1 result =
                fixture.Integration.CraftAndEquip(null);

            Assert.That(
                result.Status,
                Is.EqualTo(CraftingInventoryEquipStatusV1.InvalidCommand));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequence));
            Assert.That(
                fixture.Holdings.Sequence,
                Is.EqualTo(holdingsSequence));
            Assert.That(fixture.Loadout.CallCount, Is.Zero);
        }

        private static void AssertCraftedHolding(
            Fixture fixture,
            CraftingInventoryEquipResultV1 result)
        {
            UniqueHoldingSnapshotV1 holding = FindUnique(
                fixture.Holdings.ExportSnapshot(),
                result.EquipmentInstanceStableId);
            Assert.That(holding, Is.Not.Null);
            Assert.That(
                holding.EquipmentInstance.Fingerprint,
                Is.EqualTo(result.EquipmentFingerprint));
            Assert.That(
                holding.Provenance.GrantStableId,
                Is.EqualTo(
                    CraftingIntegrationIdentityV1
                        .EquipmentGrantStableId(
                            fixture.LastCraftCommand)));
            Assert.That(
                holding.Provenance.SourceStableId,
                Is.EqualTo(
                    CraftingIntegrationIdentityV1
                        .SourceOperationStableId(
                            fixture.LastCraftCommand)));
        }

        private static UniqueHoldingSnapshotV1 FindUnique(
            PlayerHoldingsSnapshotV1 snapshot,
            StableId instanceId)
        {
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 candidate =
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
                IEnumerable<CraftingRecipeV1> recipes = null,
                bool failHoldingsApplyOnce = false,
                RecordingLoadoutPort loadout = null)
            {
                Catalog = BuildEquipmentCatalog();
                Validator = new CatalogValidator(Catalog);
                Scrap = new ScrapWalletServiceV1(
                    Id("authority.scrap"),
                    Id("currency.scrap"));
                Fund(Scrap, initialScrap);
                Holdings = new PlayerHoldingsService(
                    Id("holdings.player"),
                    1000L,
                    Validator);

                IRewardChildAuthorityV1 holdingsAdapter =
                    new PlayerHoldingsRewardChildAuthorityV1(
                        Holdings,
                        Validator);
                if (failHoldingsApplyOnce)
                {
                    holdingsAdapter =
                        new FailOnceApplyAuthority(holdingsAdapter);
                }

                Rap = new RewardApplicationServiceV1(
                    Id("authority.crafting-rap"),
                    new CraftingUnusedMoneyRewardChildAuthorityV1(),
                    new CraftingScrapSpendRewardChildAuthorityV1(Scrap),
                    holdingsAdapter);

                CraftingRecipeV1[] recipeArray = recipes == null
                    ? new[] { CreateRecipe() }
                    : new List<CraftingRecipeV1>(recipes).ToArray();
                PrimaryRecipe = recipeArray[0];
                Crafting = new CraftingServiceV1(
                    new CraftingRecipeCatalogV1(recipeArray),
                    Catalog,
                    new RewardGenerationServiceV1(),
                    Rap,
                    Scrap,
                    CraftingUnusedMoneyRewardChildAuthorityV1
                        .StableAuthorityId,
                    Holdings.AuthorityStableId);
                Loadout = loadout ?? new RecordingLoadoutPort();
                Integration = new CraftingInventoryEquipServiceV1(
                    Crafting,
                    Holdings,
                    Loadout);
            }

            public EquipmentCatalog Catalog { get; }

            public CatalogValidator Validator { get; }

            public ScrapWalletServiceV1 Scrap { get; }

            public PlayerHoldingsService Holdings { get; }

            public RewardApplicationServiceV1 Rap { get; }

            public CraftingRecipeV1 PrimaryRecipe { get; }

            public CraftingServiceV1 Crafting { get; }

            public RecordingLoadoutPort Loadout { get; }

            public CraftingInventoryEquipServiceV1 Integration { get; }

            public CraftEquipmentCommandV1 LastCraftCommand { get; private set; }

            public CraftAndEquipCommandV1 Command(
                int characterLevel = 60,
                ulong rootSeed = 44UL,
                StableId craftId = null,
                StableId recipeId = null,
                StableId slotId = null,
                long? expectedLoadoutSequence = null)
            {
                LastCraftCommand = new CraftEquipmentCommandV1(
                    craftId ?? Id("craft.transaction"),
                    recipeId ?? PrimaryRecipe.RecipeStableId,
                    Id("run.test"),
                    Id("player.test"),
                    Context(characterLevel),
                    rootSeed);
                return new CraftAndEquipCommandV1(
                    LastCraftCommand,
                    slotId ?? WeaponSlotOne,
                    expectedLoadoutSequence);
            }
        }

        private sealed class RecordingLoadoutPort :
            ICraftedEquipmentLoadoutPortV1
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

            public CraftedEquipmentEquipCommandV1 LastAppliedCommand
            {
                get;
                private set;
            }

            public CraftedEquipmentEquipResultV1 Apply(
                CraftedEquipmentEquipCommandV1 command)
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
                        return CraftedEquipmentEquipResultV1.FromCommand(
                            command,
                            CraftedEquipmentEquipStatusV1
                                .ConflictingDuplicate,
                            Sequence,
                            false,
                            "loadout-transaction-conflict");
                    }

                    return CraftedEquipmentEquipResultV1.FromCommand(
                        command,
                        CraftedEquipmentEquipStatusV1
                            .ExactDuplicateNoChange,
                        Sequence,
                        existing.OriginalApplied,
                        existing.RejectionCode);
                }

                if (retryFirst)
                {
                    retryFirst = false;
                    return CraftedEquipmentEquipResultV1.FromCommand(
                        command,
                        CraftedEquipmentEquipStatusV1.RetryRequired,
                        Sequence,
                        false,
                        "test-loadout-interruption");
                }

                if (mismatchFirst)
                {
                    mismatchFirst = false;
                    return new CraftedEquipmentEquipResultV1(
                        CraftedEquipmentEquipStatusV1.Applied,
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
                    return CraftedEquipmentEquipResultV1.FromCommand(
                        command,
                        CraftedEquipmentEquipStatusV1.Rejected,
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
                return CraftedEquipmentEquipResultV1.FromCommand(
                    command,
                    CraftedEquipmentEquipStatusV1.Applied,
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

        private sealed class FailOnceApplyAuthority :
            IRewardChildAuthorityV1
        {
            private readonly IRewardChildAuthorityV1 inner;
            private bool failed;

            public FailOnceApplyAuthority(
                IRewardChildAuthorityV1 inner)
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

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResultV1 Apply(
                RewardChildGrantCommandV1 command)
            {
                if (!failed)
                {
                    failed = true;
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        "test-interruption");
                }

                return inner.Apply(command);
            }
        }

        private static CraftingRecipeV1 CreateRecipe(
            StableId recipeId = null,
            StableId target = null)
        {
            return new CraftingRecipeV1(
                1,
                recipeId ?? Id("recipe.alpha"),
                target ?? EquipmentAlpha,
                Id("progression-source.equipment"),
                50,
                50,
                5,
                new CraftingDelayVarianceV1(0, 0),
                10L,
                CraftingQualityPolicyKindV1.Fixed,
                new[]
                {
                    new CraftingWeightedDefinitionV1(
                        CommonQuality,
                        1UL),
                },
                50,
                60,
                0,
                0,
                1,
                1,
                Array.Empty<CraftingWeightedDefinitionV1>(),
                new CraftingGeneratorPolicyV1(
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
            ScrapWalletServiceV1 wallet,
            long amount)
        {
            if (amount == 0L)
            {
                return;
            }

            ScrapTransactionResultV1 result = wallet.Apply(
                new ScrapTransactionCommandV1(
                    Id("scrap-tx.initial"),
                    Id("scrap-op.initial"),
                    wallet.AuthorityStableId,
                    wallet.CurrencyStableId,
                    ScrapMutationKindV1.Grant,
                    amount,
                    ScrapIdentityV1.RewardGrantReason,
                    new ScrapProvenanceV1(
                        ScrapIdentityV1.RewardSourceKind,
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

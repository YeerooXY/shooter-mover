using System;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    public sealed class StrongboxSaveAdapterReplayTests
    {
        [Test]
        public void StrongboxOpeningReplayHistorySurvivesTypedAdapter()
        {
            StrongboxReplayFixture source = new StrongboxReplayFixture();
            source.AddAndRegisterBox();
            StrongboxOpeningResultRuntimeV1 rejected = source.Service.Open(
                source.Command);
            Assert.That(rejected.Status, Is.EqualTo(
                StrongboxOpeningRuntimeStatusV1.GeneratorRejected));
            Assert.That(source.Generator.CallCount, Is.EqualTo(1));

            ISaveComponentAdapterV1 sourceAdapter =
                KnownSaveComponentAdaptersV1.StrongboxState(
                    source.Service.ExportSnapshot,
                    CanonicalSnapshotIntegrityV1.Validate,
                    snapshot => ApplyStrongboxSnapshot(
                        source.Service,
                        snapshot),
                    true);
            PlayerAccountSnapshotV1 account = AccountWithCharacter(
                sourceAdapter.ExportComponent());

            StrongboxReplayFixture target = new StrongboxReplayFixture();
            ISaveComponentAdapterV1 targetAdapter =
                KnownSaveComponentAdaptersV1.StrongboxState(
                    target.Service.ExportSnapshot,
                    ValidateStrongboxSnapshot,
                    snapshot => ApplyStrongboxSnapshot(
                        target.Service,
                        snapshot),
                    true);

            PlayerAccountRestoreResultV1 restore =
                new PlayerAccountRestoreCoordinatorV1().Restore(
                    account,
                    new[]
                    {
                        new CharacterSaveRestoreBindingV1(
                            0,
                            StableId.Parse("character.strongbox-replay"),
                            new[] { targetAdapter }),
                    });
            Assert.That(restore.Succeeded, Is.True, restore.RejectionCode);

            StrongboxOpeningResultRuntimeV1 replay = target.Service.Open(
                target.Command);
            Assert.That(replay.Status, Is.EqualTo(
                StrongboxOpeningRuntimeStatusV1.GeneratorRejected));
            Assert.That(target.Generator.CallCount, Is.Zero);
            Assert.That(target.Service.ExportSnapshot().Fingerprint, Is.EqualTo(
                source.Service.ExportSnapshot().Fingerprint));
        }

        private static SaveComponentValidationResultV1
            ValidateStrongboxSnapshot(StrongboxOpeningSnapshotV1 snapshot)
        {
            StrongboxReplayFixture shadow = new StrongboxReplayFixture();
            StrongboxOpeningImportResultV1 imported =
                shadow.Service.ImportSnapshot(snapshot);
            return imported.Succeeded
                ? SaveComponentValidationResultV1.Accept()
                : SaveComponentValidationResultV1.Reject(
                    imported.RejectionCode);
        }

        private static SaveComponentApplyResultV1 ApplyStrongboxSnapshot(
            StrongboxOpeningServiceV1 authority,
            StrongboxOpeningSnapshotV1 snapshot)
        {
            StrongboxOpeningImportResultV1 imported =
                authority.ImportSnapshot(snapshot);
            return imported.Succeeded
                ? SaveComponentApplyResultV1.Applied()
                : SaveComponentApplyResultV1.Rejected(
                    imported.RejectionCode);
        }

        private static PlayerAccountSnapshotV1 AccountWithCharacter(
            SaveComponentSnapshotV1 component)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshotV1(
                StableId.Parse("character.strongbox-replay"),
                StableId.Parse("class.test-mech"),
                0,
                "Strongbox Test Mech",
                0L,
                new[] { component });
            return new PlayerAccountSnapshotV1(
                StableId.Parse("account.strongbox-save-adapter-test"),
                0L,
                slots,
                null);
        }

        private sealed class StrongboxReplayFixture
        {
            private static readonly StableId ScrapAuthority =
                StableId.Parse("authority.save-adapter-scrap");
            private static readonly StableId ScrapCurrency =
                StableId.Parse("currency.scrap");
            private static readonly StableId HoldingsAuthority =
                StableId.Parse("holdings.save-adapter-player");
            private static readonly StableId RapAuthority =
                StableId.Parse("authority.save-adapter-rap");
            private static readonly StableId PlayerId =
                StableId.Parse("player.save-adapter");
            private static readonly StableId BoxId =
                StableId.Parse("strongbox.save-adapter-instance");
            private static readonly StableId TierId =
                StableId.Parse("strongbox.save-adapter-tier");

            public StrongboxReplayFixture()
            {
                Definition = CreateStrongboxDefinition();
                Catalog = new StrongboxDefinitionCatalogV1(
                    new[] { Definition });
                Generator = new CountingThrowingStrongboxGenerator();
                var money = new MoneyWalletService();
                var scrap = new ScrapWalletServiceV1(
                    ScrapAuthority,
                    ScrapCurrency);
                Holdings = new PlayerHoldingsService(
                    HoldingsAuthority,
                    1000L,
                    new AcceptingEquipmentValidator());
                var rap = new RewardApplicationServiceV1(
                    RapAuthority,
                    new MoneyRewardChildAuthorityV1(money),
                    new ScrapRewardChildAuthorityV1(scrap),
                    new PlayerHoldingsRewardChildAuthorityV1(
                        Holdings,
                        new AcceptingEquipmentValidator()));
                Service = new StrongboxOpeningServiceV1(
                    Catalog,
                    Generator,
                    Holdings,
                    rap,
                    new DeterministicStrongboxGrantPayloadResolverV1());
                Command = StrongboxOpenCommandV1.Create(
                    StableId.Parse("opening.save-adapter-primary"),
                    StableId.Parse("run.save-adapter-primary"),
                    BoxId,
                    PlayerId,
                    MoneyWalletIdsV1.AuthorityStableId,
                    ScrapAuthority,
                    HoldingsAuthority);
            }

            public StrongboxDefinitionV1 Definition { get; }

            public StrongboxDefinitionCatalogV1 Catalog { get; }

            public CountingThrowingStrongboxGenerator Generator { get; }

            public PlayerHoldingsService Holdings { get; }

            public StrongboxOpeningServiceV1 Service { get; }

            public StrongboxOpenCommandV1 Command { get; }

            public void AddAndRegisterBox()
            {
                PlayerHoldingsMutationResultV1 add = Holdings.Apply(
                    PlayerHoldingsCommandV1.AddStrongbox(
                        StableId.Parse("holdtx.save-adapter-add-box"),
                        StableId.Parse("holdop.save-adapter-add-box"),
                        HoldingsAuthority,
                        TierId,
                        BoxId,
                        HoldingProvenanceV1.Create(
                            StableId.Parse("grant.save-adapter-add-box"),
                            StableId.Parse("source.save-adapter-add-box"))));
                Assert.That(add.Status, Is.EqualTo(
                    PlayerHoldingsMutationStatusV1.Applied));
                StrongboxRegistrationResultV1 registration =
                    Service.RegisterInstance(StrongboxInstanceContextV1.Create(
                        BoxId,
                        TierId,
                        123456UL,
                        1,
                        ProgressionContext.Create(
                            5,
                            2,
                            StableId.Parse("difficulty.normal"),
                            1),
                        StableId.Parse("source.save-adapter-strongbox"),
                        StableId.Parse("provenance.save-adapter-strongbox"),
                        Definition.Fingerprint));
                Assert.That(registration.Status, Is.EqualTo(
                    StrongboxRegistrationStatusV1.Registered));
            }

            private static StrongboxDefinitionV1 CreateStrongboxDefinition()
            {
                RewardGrantSpecificationV1 misc =
                    RewardGrantSpecificationV1.Create(
                        StableId.Parse("grant.save-adapter-part"),
                        RewardGrantKindV1.Miscellaneous,
                        StableId.Parse("misc.save-adapter-part"),
                        RewardQuantityRangeV1.Fixed(1L),
                        new[]
                        {
                            RewardScalingInputDescriptorV1.Create(
                                StableId.Parse("scaling.source-tier"),
                                RewardScalingInputKindV1.SourceTier),
                            RewardScalingInputDescriptorV1.Create(
                                StableId.Parse("scaling.exceptional"),
                                RewardScalingInputKindV1.Custom),
                        });
                RewardProfileV1 profile = RewardProfileV1.Create(
                    StableId.Parse("profile.save-adapter-strongbox"),
                    new[] { misc },
                    Array.Empty<IndependentRewardRollV1>(),
                    Array.Empty<ExclusiveRewardGroupV1>());
                return StrongboxDefinitionV1.Create(
                    TierId,
                    0,
                    1L,
                    1L,
                    0L,
                    StrongboxRewardCountPolicyV1.Create(1, 1),
                    StrongboxMandatoryScrapPolicyV1.Create(
                        ScrapCurrency,
                        1L,
                        1L),
                    StableId.Parse("generation-policy.save-adapter"),
                    profile,
                    StableId.Parse("scaling.source-tier"),
                    StableId.Parse("scaling.exceptional"));
            }
        }

        private sealed class CountingThrowingStrongboxGenerator :
            IStrongboxRewardGeneratorV1
        {
            public int CallCount { get; private set; }

            public RewardGenerationResultEnvelopeV1 Generate(
                RewardGenerationRequestV1 request)
            {
                CallCount++;
                throw new InvalidOperationException(
                    "forced-save-adapter-generator-rejection");
            }
        }

        private sealed class AcceptingEquipmentValidator :
            IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "save-adapter-test-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }
    }
}

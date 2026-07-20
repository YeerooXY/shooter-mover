using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
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
        public void AcceptedOpeningAndGeneratedIdentitiesSurviveRestoreWithoutSecondAward()
        {
            var source = new AcceptedStrongboxFixture();
            source.AddAndRegister(source.UnopenedBoxId, "unopened");
            source.AddAndRegister(source.OpenedBoxId, "opened");
            StrongboxOpeningResultRuntimeV1 opened = source.OpenOpenedBox();
            Assert.That(opened.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(source.Generator.CallCount, Is.EqualTo(1));
            Assert.That(source.TotalChildApplyCount, Is.GreaterThan(0));
            Assert.That(source.HasBox(source.UnopenedBoxId), Is.True);
            Assert.That(source.HasBox(source.OpenedBoxId), Is.False);
            StableId generatedEquipmentId = opened.GeneratedOutcome.Payloads
                .Single(payload => payload.Grant.Kind
                    == RewardGrantKindV1.EquipmentReference)
                .EquipmentInstances.Single().InstanceId;
            Assert.That(source.HasEquipment(generatedEquipmentId), Is.True);

            SaveComponentSnapshotV1 holdingsComponent =
                source.HoldingsAdapter().ExportComponent();
            SaveComponentSnapshotV1 strongboxComponent =
                source.StrongboxAdapter().ExportComponent();
            PlayerAccountSnapshotV1 encodedAccount = Account(
                holdingsComponent,
                strongboxComponent);
            string file = PlayerAccountFileCodecV1.Encode(encodedAccount);
            PlayerAccountSnapshotV1 decoded;
            string rejection;
            Assert.That(PlayerAccountFileCodecV1.TryDecode(
                file,
                out decoded,
                out rejection), Is.True, rejection);

            var target = new AcceptedStrongboxFixture();
            PlayerAccountRestoreResultV1 restore =
                new PlayerAccountRestoreCoordinatorV1(
                    validateAggregate: account =>
                        PlayerAccountComponentSemanticsV1.Validate(
                            account,
                            target.ExpectedDefinitionFingerprint))
                    .Restore(
                        decoded,
                        new[]
                        {
                            new CharacterSaveRestoreBindingV1(
                                0,
                                Id("character.accepted-strongbox"),
                                new[]
                                {
                                    target.HoldingsAdapter(),
                                    target.StrongboxAdapter(),
                                }),
                        });

            Assert.That(restore.Succeeded, Is.True, restore.RejectionCode);
            Assert.That(target.Holdings.ExportSnapshot().Fingerprint,
                Is.EqualTo(source.Holdings.ExportSnapshot().Fingerprint));
            Assert.That(target.Service.ExportSnapshot().Fingerprint,
                Is.EqualTo(source.Service.ExportSnapshot().Fingerprint));
            Assert.That(target.HasBox(target.UnopenedBoxId), Is.True);
            Assert.That(target.HasBox(target.OpenedBoxId), Is.False);
            Assert.That(target.HasEquipment(generatedEquipmentId), Is.True);

            long holdingsSequence = target.Holdings.Sequence;
            long openingSequence = target.Service.Sequence;
            int generatorCalls = target.Generator.CallCount;
            int childApplies = target.TotalChildApplyCount;
            StrongboxOpeningResultRuntimeV1 replay = target.OpenOpenedBox();

            Assert.That(replay.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.TerminalFact.Fingerprint,
                Is.EqualTo(opened.TerminalFact.Fingerprint));
            Assert.That(replay.GeneratedOutcome.Fingerprint,
                Is.EqualTo(opened.GeneratedOutcome.Fingerprint));
            Assert.That(target.Generator.CallCount, Is.EqualTo(generatorCalls));
            Assert.That(target.TotalChildApplyCount, Is.EqualTo(childApplies));
            Assert.That(target.Holdings.Sequence, Is.EqualTo(holdingsSequence));
            Assert.That(target.Service.Sequence, Is.EqualTo(openingSequence));
            Assert.That(target.HasBox(target.OpenedBoxId), Is.False);
            Assert.That(target.HasEquipment(generatedEquipmentId), Is.True);
        }

        [Test]
        public void StrongboxCodecHasDeterministicExplicitGoldenPayload()
        {
            var fixture = new AcceptedStrongboxFixture();
            fixture.AddAndRegister(fixture.UnopenedBoxId, "unopened");
            fixture.AddAndRegister(fixture.OpenedBoxId, "opened");
            fixture.OpenOpenedBox();
            StrongboxOpeningSnapshotV1 snapshot = fixture.Service.ExportSnapshot();

            string first = KnownSaveComponentCodecsV1.StrongboxState.Encode(snapshot);
            StrongboxOpeningSnapshotV1 decoded;
            string rejection;
            Assert.That(KnownSaveComponentCodecsV1.StrongboxState.TryDecode(
                first,
                out decoded,
                out rejection), Is.True, rejection);
            string second = KnownSaveComponentCodecsV1.StrongboxState.Encode(decoded);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(decoded.Fingerprint, Is.EqualTo(snapshot.Fingerprint));
            Assert.That(first, Does.StartWith("O5:"));
            Assert.That(first, Does.Not.Contain("StrongboxOpeningSnapshotV1"));
            Assert.That(first, Does.Not.Contain("System."));
        }

        [Test]
        public void HeldBoxWithoutRegistrationRejects()
        {
            var fixture = new AcceptedStrongboxFixture();
            fixture.AddBox(fixture.UnopenedBoxId, "unopened");
            StrongboxOpeningSnapshotV1 emptyOpening =
                StrongboxOpeningSnapshotV1.CreateCanonical(
                    fixture.Catalog.Fingerprint,
                    0L,
                    Array.Empty<StrongboxInstanceContextV1>(),
                    Array.Empty<StrongboxOpeningRecordSnapshotV1>());

            AssertRejected(
                fixture.Holdings.ExportSnapshot(),
                emptyOpening,
                fixture,
                "held-strongbox-registration-missing");
        }

        [Test]
        public void RegisteredUnopenedBoxAbsentFromHoldingsRejects()
        {
            var fixture = new AcceptedStrongboxFixture();
            fixture.Register(fixture.UnopenedBoxId, "unopened");

            AssertRejected(
                fixture.EmptyHoldingsSnapshot(),
                fixture.Service.ExportSnapshot(),
                fixture,
                "registered-unopened-strongbox-absent-from-holdings");
        }

        [Test]
        public void TierAndProvenanceConflictsReject()
        {
            var tierFixture = new AcceptedStrongboxFixture();
            tierFixture.AddBox(
                tierFixture.UnopenedBoxId,
                "unopened",
                Id("strongbox.tier.conflicting"));
            tierFixture.Register(tierFixture.UnopenedBoxId, "unopened");
            AssertRejected(
                tierFixture.Holdings.ExportSnapshot(),
                tierFixture.Service.ExportSnapshot(),
                tierFixture,
                "held-strongbox-tier-conflict");

            var provenanceFixture = new AcceptedStrongboxFixture();
            provenanceFixture.AddBox(provenanceFixture.UnopenedBoxId, "unopened");
            provenanceFixture.Register(
                provenanceFixture.UnopenedBoxId,
                "unopened",
                Id("grant.conflicting-provenance"));
            AssertRejected(
                provenanceFixture.Holdings.ExportSnapshot(),
                provenanceFixture.Service.ExportSnapshot(),
                provenanceFixture,
                "held-strongbox-provenance-conflict");
        }

        [Test]
        public void DefinitionFingerprintConflictRejects()
        {
            var fixture = new AcceptedStrongboxFixture();
            fixture.AddAndRegister(fixture.UnopenedBoxId, "unopened");
            CharacterInstanceSnapshotV1 character = Character(
                fixture.Holdings.ExportSnapshot(),
                fixture.Service.ExportSnapshot());

            SaveComponentValidationResultV1 result =
                PlayerAccountComponentSemanticsV1.ValidateCharacter(
                    character,
                    ignored => new string('0', 64));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionCode,
                Does.StartWith("strongbox-definition-fingerprint-conflict"));
        }

        [Test]
        public void OpeningReferencingMissingExactContextRejects()
        {
            var fixture = new AcceptedStrongboxFixture();
            fixture.AddAndRegister(fixture.UnopenedBoxId, "unopened");
            fixture.AddAndRegister(fixture.OpenedBoxId, "opened");
            fixture.OpenOpenedBox();
            StrongboxOpeningSnapshotV1 source = fixture.Service.ExportSnapshot();
            StrongboxOpeningSnapshotV1 missingOpenedContext =
                StrongboxOpeningSnapshotV1.CreateCanonical(
                    source.DefinitionCatalogFingerprint,
                    source.Sequence,
                    source.Contexts.Where(context =>
                        context.InstanceStableId != fixture.OpenedBoxId),
                    source.Openings);

            AssertRejected(
                fixture.Holdings.ExportSnapshot(),
                missingOpenedContext,
                fixture,
                "strongbox-opening-context-missing");
        }

        private static void AssertRejected(
            PlayerHoldingsSnapshotV1 holdings,
            StrongboxOpeningSnapshotV1 strongboxes,
            AcceptedStrongboxFixture fixture,
            string expectedPrefix)
        {
            SaveComponentValidationResultV1 result =
                PlayerAccountComponentSemanticsV1.ValidateCharacter(
                    Character(holdings, strongboxes),
                    fixture.ExpectedDefinitionFingerprint);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionCode, Does.StartWith(expectedPrefix));
        }

        private static PlayerAccountSnapshotV1 Account(
            params SaveComponentSnapshotV1[] components)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshotV1(
                Id("character.accepted-strongbox"),
                Id("class.striker"),
                0,
                "Accepted Strongbox",
                1L,
                components);
            return new PlayerAccountSnapshotV1(
                Id("account.accepted-strongbox"),
                1L,
                slots,
                null);
        }

        private static CharacterInstanceSnapshotV1 Character(
            PlayerHoldingsSnapshotV1 holdings,
            StrongboxOpeningSnapshotV1 strongboxes)
        {
            return new CharacterInstanceSnapshotV1(
                Id("character.accepted-strongbox"),
                Id("class.striker"),
                0,
                "Accepted Strongbox",
                1L,
                new[]
                {
                    new SaveComponentSnapshotV1(
                        KnownSaveComponentDefinitionsV1.PlayerHoldings()
                            .ComponentStableId,
                        1,
                        KnownSaveComponentDefinitionsV1.PlayerHoldings()
                            .ContentVersion,
                        KnownSaveComponentCodecsV1.PlayerHoldings.Encode(holdings)),
                    new SaveComponentSnapshotV1(
                        KnownSaveComponentDefinitionsV1.StrongboxState(true)
                            .ComponentStableId,
                        1,
                        KnownSaveComponentDefinitionsV1.StrongboxState(true)
                            .ContentVersion,
                        KnownSaveComponentCodecsV1.StrongboxState.Encode(strongboxes)),
                });
        }

        private sealed class AcceptedStrongboxFixture
        {
            private static readonly StableId MoneyAuthority =
                MoneyWalletIdsV1.AuthorityStableId;
            private static readonly StableId ScrapAuthority =
                Id("authority.accepted-strongbox-scrap");
            private static readonly StableId ScrapCurrency =
                Id("currency.scrap");
            private static readonly StableId HoldingsAuthority =
                Id("authority.accepted-strongbox-holdings");
            private static readonly StableId RapAuthority =
                Id("authority.accepted-strongbox-rap");
            private static readonly StableId TierId =
                Id("strongbox.tier.accepted-save");
            private static readonly StableId EquipmentDefinition =
                Id("equipment-definition.accepted-save");

            public AcceptedStrongboxFixture()
            {
                UnopenedBoxId = Id("strongbox.instance.accepted-unopened");
                OpenedBoxId = Id("strongbox.instance.accepted-opened");
                Definition = CreateDefinition();
                Catalog = new StrongboxDefinitionCatalogV1(
                    new[] { Definition });
                Generator = new CountingGenerator();
                Money = new MoneyWalletService();
                Scrap = new ScrapWalletServiceV1(
                    ScrapAuthority,
                    ScrapCurrency);
                Holdings = new PlayerHoldingsService(
                    HoldingsAuthority,
                    1000L,
                    new AcceptingEquipmentValidator());
                MoneyChild = new CountingChildAuthority(
                    new MoneyRewardChildAuthorityV1(Money));
                ScrapChild = new CountingChildAuthority(
                    new ScrapRewardChildAuthorityV1(Scrap));
                HoldingsChild = new CountingChildAuthority(
                    new PlayerHoldingsRewardChildAuthorityV1(
                        Holdings,
                        new AcceptingEquipmentValidator()));
                Rap = new RewardApplicationServiceV1(
                    RapAuthority,
                    MoneyChild,
                    ScrapChild,
                    HoldingsChild);
                Service = new StrongboxOpeningServiceV1(
                    Catalog,
                    Generator,
                    Holdings,
                    Rap,
                    new DeterministicStrongboxGrantPayloadResolverV1(
                        new FixedEquipmentResolver()));
            }

            public StableId UnopenedBoxId { get; }
            public StableId OpenedBoxId { get; }
            public StrongboxDefinitionV1 Definition { get; }
            public StrongboxDefinitionCatalogV1 Catalog { get; }
            public CountingGenerator Generator { get; }
            public MoneyWalletService Money { get; }
            public ScrapWalletServiceV1 Scrap { get; }
            public PlayerHoldingsService Holdings { get; }
            public CountingChildAuthority MoneyChild { get; }
            public CountingChildAuthority ScrapChild { get; }
            public CountingChildAuthority HoldingsChild { get; }
            public RewardApplicationServiceV1 Rap { get; }
            public StrongboxOpeningServiceV1 Service { get; }

            public int TotalChildApplyCount
            {
                get
                {
                    return MoneyChild.ApplyCount
                        + ScrapChild.ApplyCount
                        + HoldingsChild.ApplyCount;
                }
            }

            public string ExpectedDefinitionFingerprint(StableId tierId)
            {
                return tierId == TierId ? Definition.Fingerprint : null;
            }

            public void AddAndRegister(StableId boxId, string suffix)
            {
                AddBox(boxId, suffix);
                Register(boxId, suffix);
            }

            public void AddBox(
                StableId boxId,
                string suffix,
                StableId tierId = null)
            {
                PlayerHoldingsMutationResultV1 result = Holdings.Apply(
                    PlayerHoldingsCommandV1.AddStrongbox(
                        Id("transaction.box." + suffix),
                        Id("operation.box." + suffix),
                        HoldingsAuthority,
                        tierId ?? TierId,
                        boxId,
                        HoldingProvenanceV1.Create(
                            GrantId(suffix),
                            Id("source.box." + suffix)),
                        Holdings.Sequence));
                Assert.That(result.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            }

            public void Register(
                StableId boxId,
                string suffix,
                StableId collectionProvenance = null)
            {
                StrongboxRegistrationResultV1 result = Service.RegisterInstance(
                    StrongboxInstanceContextV1.Create(
                        boxId,
                        TierId,
                        suffix == "opened" ? 222UL : 111UL,
                        1,
                        ProgressionContext.Create(
                            5,
                            2,
                            Id("difficulty.normal"),
                            1),
                        Id("source-context.box." + suffix),
                        collectionProvenance ?? GrantId(suffix),
                        Definition.Fingerprint));
                Assert.That(result.Status,
                    Is.EqualTo(StrongboxRegistrationStatusV1.Registered));
            }

            public StrongboxOpeningResultRuntimeV1 OpenOpenedBox()
            {
                return Service.Open(Command(OpenedBoxId));
            }

            public bool HasBox(StableId boxId)
            {
                UniqueHoldingSnapshotV1 holding;
                return Holdings.TryGetUnique(boxId, out holding)
                    && holding.RewardKind == RewardGrantKindV1.Strongbox;
            }

            public bool HasEquipment(StableId equipmentId)
            {
                UniqueHoldingSnapshotV1 holding;
                return Holdings.TryGetUnique(equipmentId, out holding)
                    && holding.RewardKind
                        == RewardGrantKindV1.EquipmentReference;
            }

            public PlayerHoldingsSnapshotV1 EmptyHoldingsSnapshot()
            {
                return new PlayerHoldingsService(
                    HoldingsAuthority,
                    1000L,
                    new AcceptingEquipmentValidator())
                    .ExportSnapshot();
            }

            public ISaveComponentAdapterV1 HoldingsAdapter()
            {
                return KnownSaveComponentAdaptersV1.PlayerHoldings(
                    Holdings.ExportSnapshot,
                    snapshot =>
                    {
                        PlayerHoldingsImportResultV1 result =
                            new PlayerHoldingsService(
                                HoldingsAuthority,
                                1000L,
                                new AcceptingEquipmentValidator())
                                .ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentValidationResultV1.Accept()
                            : SaveComponentValidationResultV1.Reject(
                                result.RejectionCode);
                    },
                    snapshot =>
                    {
                        PlayerHoldingsImportResultV1 result =
                            Holdings.ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentApplyResultV1.Applied()
                            : SaveComponentApplyResultV1.Rejected(
                                result.RejectionCode);
                    });
            }

            public ISaveComponentAdapterV1 StrongboxAdapter()
            {
                return KnownSaveComponentAdaptersV1.StrongboxState(
                    Service.ExportSnapshot,
                    snapshot =>
                    {
                        var shadow = new AcceptedStrongboxFixture();
                        StrongboxOpeningImportResultV1 result =
                            shadow.Service.ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentValidationResultV1.Accept()
                            : SaveComponentValidationResultV1.Reject(
                                result.RejectionCode);
                    },
                    snapshot =>
                    {
                        StrongboxOpeningImportResultV1 result =
                            Service.ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentApplyResultV1.Applied()
                            : SaveComponentApplyResultV1.Rejected(
                                result.RejectionCode);
                    },
                    true);
            }

            private StrongboxOpenCommandV1 Command(StableId boxId)
            {
                return StrongboxOpenCommandV1.Create(
                    Id("opening.accepted-save"),
                    Id("run.accepted-save"),
                    boxId,
                    Id("player.accepted-save"),
                    MoneyAuthority,
                    ScrapAuthority,
                    HoldingsAuthority);
            }

            private static StableId GrantId(string suffix)
            {
                return Id("grant.box." + suffix);
            }

            private static StrongboxDefinitionV1 CreateDefinition()
            {
                RewardGrantSpecificationV1 equipment =
                    RewardGrantSpecificationV1.CreateFixed(
                        Id("grant-spec.accepted-equipment"),
                        RewardGrantKindV1.EquipmentReference,
                        EquipmentDefinition,
                        1L);
                RewardProfileV1 profile = RewardProfileV1.Create(
                    Id("profile.accepted-strongbox"),
                    new[] { equipment },
                    Array.Empty<IndependentRewardRollV1>(),
                    Array.Empty<ExclusiveRewardGroupV1>());
                return StrongboxDefinitionV1.Create(
                    TierId,
                    0,
                    1L,
                    1L,
                    0L,
                    StrongboxRewardCountPolicyV1.Create(2, 2),
                    StrongboxMandatoryScrapPolicyV1.Create(
                        ScrapCurrency,
                        4L,
                        4L),
                    Id("generation-policy.accepted-save"),
                    profile,
                    Id("scaling.source-tier"),
                    Id("scaling.exceptional"));
            }
        }

        private sealed class CountingGenerator : IStrongboxRewardGeneratorV1
        {
            private readonly SharedStrongboxRewardGeneratorV1 inner =
                new SharedStrongboxRewardGeneratorV1(
                    new RewardGenerationServiceV1());

            public int CallCount { get; private set; }

            public RewardGenerationResultEnvelopeV1 Generate(
                RewardGenerationRequestV1 request)
            {
                CallCount++;
                return inner.Generate(request);
            }
        }

        private sealed class CountingChildAuthority : IRewardChildAuthorityV1
        {
            private readonly IRewardChildAuthorityV1 inner;

            public CountingChildAuthority(IRewardChildAuthorityV1 inner)
            {
                this.inner = inner;
            }

            public StableId AuthorityStableId
            {
                get { return inner.AuthorityStableId; }
            }

            public long Sequence { get { return inner.Sequence; } }

            public int ApplyCount { get; private set; }

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResultV1 Apply(
                RewardChildGrantCommandV1 command)
            {
                ApplyCount++;
                return inner.Apply(command);
            }
        }

        private sealed class FixedEquipmentResolver :
            IStrongboxEquipmentPayloadResolverV1
        {
            public bool TryResolve(
                StrongboxDefinitionV1 definition,
                StrongboxInstanceContextV1 boxContext,
                RewardOperationRequestV1 operation,
                RewardGrantV1 equipmentGrant,
                out IReadOnlyList<EquipmentInstance> equipmentInstances,
                out string rejectionCode)
            {
                var values = new List<EquipmentInstance>();
                for (long unit = 0L; unit < equipmentGrant.Quantity; unit++)
                {
                    StableId instanceId = StrongboxCanonicalV1.DeriveId(
                        "boxequipment",
                        operation.SourceOperationStableId.ToString(),
                        equipmentGrant.GrantStableId.ToString(),
                        unit.ToString());
                    values.Add(EquipmentInstance.Create(
                        instanceId,
                        equipmentGrant.ContentStableId,
                        5,
                        Id("equipment-quality.common"),
                        Array.Empty<AugmentInstance>()));
                }
                equipmentInstances = values;
                rejectionCode = null;
                return true;
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
                    "accepted-strongbox-save-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}

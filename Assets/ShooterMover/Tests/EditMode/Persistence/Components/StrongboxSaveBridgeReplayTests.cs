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
    public sealed class StrongboxSaveBridgeReplayTests
    {
        [Test]
        public void AcceptedOpeningAndGeneratedIdentitiesSurviveRestoreWithoutSecondAward()
        {
            var source = new AcceptedStrongboxFixture();
            source.AddAndRegister(source.UnopenedBoxId, "unopened");
            source.AddAndRegister(source.OpenedBoxId, "opened");
            StrongboxOpeningResultLive opened = source.OpenOpenedBox();
            Assert.That(opened.Status,
                Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(source.Generator.CallCount, Is.EqualTo(1));
            Assert.That(source.TotalChildApplyCount, Is.GreaterThan(0));
            Assert.That(source.HasBox(source.UnopenedBoxId), Is.True);
            Assert.That(source.HasBox(source.OpenedBoxId), Is.False);
            StableId generatedEquipmentId = opened.GeneratedOutcome.Payloads
                .Single(payload => payload.Grant.Kind
                    == RewardGrantKind.EquipmentReference)
                .EquipmentInstances.Single().InstanceId;
            Assert.That(source.HasEquipment(generatedEquipmentId), Is.True);

            SaveComponentSnapshot holdingsComponent =
                source.HoldingsAdapter().ExportComponent();
            SaveComponentSnapshot strongboxComponent =
                source.StrongboxAdapter().ExportComponent();
            PlayerAccountSnapshot encodedAccount = Account(
                holdingsComponent,
                strongboxComponent);
            string file = PlayerAccountFileCodec.Encode(encodedAccount);
            PlayerAccountSnapshot decoded;
            string rejection;
            Assert.That(PlayerAccountFileCodec.TryDecode(
                file,
                out decoded,
                out rejection), Is.True, rejection);

            var target = new AcceptedStrongboxFixture();
            PlayerAccountRestoreResult restore =
                new PlayerAccountRestoreFlow(
                    validateAggregate: account =>
                        PlayerAccountComponentSemantics.Validate(
                            account,
                            target.ExpectedDefinitionFingerprint))
                    .Restore(
                        decoded,
                        new[]
                        {
                            new CharacterSaveRestoreBinding(
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
            StrongboxOpeningResultLive replay = target.OpenOpenedBox();

            Assert.That(replay.Status,
                Is.EqualTo(StrongboxOpeningLiveStatus.ExactDuplicateNoChange));
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
            StrongboxOpeningSnapshot snapshot = fixture.Service.ExportSnapshot();

            string first = KnownSaveComponentCodecs.StrongboxState.Encode(snapshot);
            StrongboxOpeningSnapshot decoded;
            string rejection;
            Assert.That(KnownSaveComponentCodecs.StrongboxState.TryDecode(
                first,
                out decoded,
                out rejection), Is.True, rejection);
            string second = KnownSaveComponentCodecs.StrongboxState.Encode(decoded);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(decoded.Fingerprint, Is.EqualTo(snapshot.Fingerprint));
            Assert.That(first, Does.StartWith("O5:"));
            Assert.That(first, Does.Not.Contain("StrongboxOpeningSnapshot"));
            Assert.That(first, Does.Not.Contain("System."));
        }

        [Test]
        public void HeldBoxWithoutRegistrationRejects()
        {
            var fixture = new AcceptedStrongboxFixture();
            fixture.AddBox(fixture.UnopenedBoxId, "unopened");
            StrongboxOpeningSnapshot emptyOpening =
                StrongboxOpeningSnapshot.CreateCanonical(
                    fixture.Catalog.Fingerprint,
                    0L,
                    Array.Empty<StrongboxInstanceContext>(),
                    Array.Empty<StrongboxOpeningRecordSnapshot>());

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
            CharacterInstanceSnapshot character = Character(
                fixture.Holdings.ExportSnapshot(),
                fixture.Service.ExportSnapshot());

            SaveComponentValidationResult result =
                PlayerAccountComponentSemantics.ValidateCharacter(
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
            StrongboxOpeningSnapshot source = fixture.Service.ExportSnapshot();
            StrongboxOpeningSnapshot missingOpenedContext =
                StrongboxOpeningSnapshot.CreateCanonical(
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
            PlayerHoldingsSnapshot holdings,
            StrongboxOpeningSnapshot strongboxes,
            AcceptedStrongboxFixture fixture,
            string expectedPrefix)
        {
            SaveComponentValidationResult result =
                PlayerAccountComponentSemantics.ValidateCharacter(
                    Character(holdings, strongboxes),
                    fixture.ExpectedDefinitionFingerprint);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RejectionCode, Does.StartWith(expectedPrefix));
        }

        private static PlayerAccountSnapshot Account(
            params SaveComponentSnapshot[] components)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshot(
                Id("character.accepted-strongbox"),
                Id("class.striker"),
                0,
                "Accepted Strongbox",
                1L,
                components);
            return new PlayerAccountSnapshot(
                Id("account.accepted-strongbox"),
                1L,
                slots,
                null);
        }

        private static CharacterInstanceSnapshot Character(
            PlayerHoldingsSnapshot holdings,
            StrongboxOpeningSnapshot strongboxes)
        {
            return new CharacterInstanceSnapshot(
                Id("character.accepted-strongbox"),
                Id("class.striker"),
                0,
                "Accepted Strongbox",
                1L,
                new[]
                {
                    new SaveComponentSnapshot(
                        KnownSaveComponentDefinitions.PlayerHoldings()
                            .ComponentStableId,
                        1,
                        KnownSaveComponentDefinitions.PlayerHoldings()
                            .ContentVersion,
                        KnownSaveComponentCodecs.PlayerHoldings.Encode(holdings)),
                    new SaveComponentSnapshot(
                        KnownSaveComponentDefinitions.StrongboxState(true)
                            .ComponentStableId,
                        1,
                        KnownSaveComponentDefinitions.StrongboxState(true)
                            .ContentVersion,
                        KnownSaveComponentCodecs.StrongboxState.Encode(strongboxes)),
                });
        }

        private sealed class AcceptedStrongboxFixture
        {
            private static readonly StableId MoneyAuthority =
                MoneyWalletIds.AuthorityStableId;
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
                Catalog = new StrongboxDefinitionCatalog(
                    new[] { Definition });
                Generator = new CountingGenerator();
                Money = new MoneyWalletActions();
                Scrap = new ScrapWalletActions(
                    ScrapAuthority,
                    ScrapCurrency);
                Holdings = new PlayerHoldingsActions(
                    HoldingsAuthority,
                    1000L,
                    new AcceptingEquipmentValidator());
                MoneyChild = new CountingChildState(
                    new MoneyRewardChildState(Money));
                ScrapChild = new CountingChildState(
                    new ScrapRewardChildState(Scrap));
                HoldingsChild = new CountingChildState(
                    new PlayerHoldingsRewardChildState(
                        Holdings,
                        new AcceptingEquipmentValidator()));
                Rap = new RewardApplicationActions(
                    RapAuthority,
                    MoneyChild,
                    ScrapChild,
                    HoldingsChild);
                Service = new StrongboxOpeningActions(
                    Catalog,
                    Generator,
                    Holdings,
                    Rap,
                    new DeterministicStrongboxGrantPayloadResolver(
                        new FixedEquipmentResolver()));
            }

            public StableId UnopenedBoxId { get; }
            public StableId OpenedBoxId { get; }
            public StrongboxDefinition Definition { get; }
            public StrongboxDefinitionCatalog Catalog { get; }
            public CountingGenerator Generator { get; }
            public MoneyWalletActions Money { get; }
            public ScrapWalletActions Scrap { get; }
            public PlayerHoldingsActions Holdings { get; }
            public CountingChildState MoneyChild { get; }
            public CountingChildState ScrapChild { get; }
            public CountingChildState HoldingsChild { get; }
            public RewardApplicationActions Rap { get; }
            public StrongboxOpeningActions Service { get; }

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
                PlayerHoldingsMutationResult result = Holdings.Apply(
                    PlayerHoldingsCommand.AddStrongbox(
                        Id("transaction.box." + suffix),
                        Id("operation.box." + suffix),
                        HoldingsAuthority,
                        tierId ?? TierId,
                        boxId,
                        HoldingProvenance.Create(
                            GrantId(suffix),
                            Id("source.box." + suffix)),
                        Holdings.Sequence));
                Assert.That(result.Status,
                    Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            }

            public void Register(
                StableId boxId,
                string suffix,
                StableId collectionProvenance = null)
            {
                StrongboxRegistrationResult result = Service.RegisterInstance(
                    StrongboxInstanceContext.Create(
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
                    Is.EqualTo(StrongboxRegistrationStatus.Registered));
            }

            public StrongboxOpeningResultLive OpenOpenedBox()
            {
                return Service.Open(Command(OpenedBoxId));
            }

            public bool HasBox(StableId boxId)
            {
                UniqueHoldingSnapshot holding;
                return Holdings.TryGetUnique(boxId, out holding)
                    && holding.RewardKind == RewardGrantKind.Strongbox;
            }

            public bool HasEquipment(StableId equipmentId)
            {
                UniqueHoldingSnapshot holding;
                return Holdings.TryGetUnique(equipmentId, out holding)
                    && holding.RewardKind
                        == RewardGrantKind.EquipmentReference;
            }

            public PlayerHoldingsSnapshot EmptyHoldingsSnapshot()
            {
                return new PlayerHoldingsActions(
                    HoldingsAuthority,
                    1000L,
                    new AcceptingEquipmentValidator())
                    .ExportSnapshot();
            }

            public ISaveComponentBridge HoldingsAdapter()
            {
                return KnownSaveComponentAdapters.PlayerHoldings(
                    Holdings.ExportSnapshot,
                    snapshot =>
                    {
                        PlayerHoldingsImportResult result =
                            new PlayerHoldingsActions(
                                HoldingsAuthority,
                                1000L,
                                new AcceptingEquipmentValidator())
                                .ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentValidationResult.Accept()
                            : SaveComponentValidationResult.Reject(
                                result.RejectionCode);
                    },
                    snapshot =>
                    {
                        PlayerHoldingsImportResult result =
                            Holdings.ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentApplyResult.Applied()
                            : SaveComponentApplyResult.Rejected(
                                result.RejectionCode);
                    });
            }

            public ISaveComponentBridge StrongboxAdapter()
            {
                return KnownSaveComponentAdapters.StrongboxState(
                    Service.ExportSnapshot,
                    snapshot =>
                    {
                        var shadow = new AcceptedStrongboxFixture();
                        StrongboxOpeningImportResult result =
                            shadow.Service.ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentValidationResult.Accept()
                            : SaveComponentValidationResult.Reject(
                                result.RejectionCode);
                    },
                    snapshot =>
                    {
                        StrongboxOpeningImportResult result =
                            Service.ImportSnapshot(snapshot);
                        return result.Succeeded
                            ? SaveComponentApplyResult.Applied()
                            : SaveComponentApplyResult.Rejected(
                                result.RejectionCode);
                    },
                    true);
            }

            private StrongboxOpenCommand Command(StableId boxId)
            {
                return StrongboxOpenCommand.Create(
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

            private static StrongboxDefinition CreateDefinition()
            {
                RewardGrantSpecification equipment =
                    RewardGrantSpecification.CreateFixed(
                        Id("grant-spec.accepted-equipment"),
                        RewardGrantKind.EquipmentReference,
                        EquipmentDefinition,
                        1L);
                RewardProfile profile = RewardProfile.Create(
                    Id("profile.accepted-strongbox"),
                    new[] { equipment },
                    Array.Empty<IndependentRewardRoll>(),
                    Array.Empty<ExclusiveRewardGroup>());
                return StrongboxDefinition.Create(
                    TierId,
                    0,
                    1L,
                    1L,
                    0L,
                    StrongboxRewardCountPolicy.Create(2, 2),
                    StrongboxMandatoryScrapPolicy.Create(
                        ScrapCurrency,
                        4L,
                        4L),
                    Id("generation-policy.accepted-save"),
                    profile,
                    Id("scaling.source-tier"),
                    Id("scaling.exceptional"));
            }
        }

        private sealed class CountingGenerator : IStrongboxRewardGenerator
        {
            private readonly SharedStrongboxRewardGenerator inner =
                new SharedStrongboxRewardGenerator(
                    new RewardGenerationActions());

            public int CallCount { get; private set; }

            public RewardGenerationResultEnvelope Generate(
                RewardGenerationRequest request)
            {
                CallCount++;
                return inner.Generate(request);
            }
        }

        private sealed class CountingChildState : IRewardChildState
        {
            private readonly IRewardChildState inner;

            public CountingChildState(IRewardChildState inner)
            {
                this.inner = inner;
            }

            public StableId AuthorityStableId
            {
                get { return inner.AuthorityStableId; }
            }

            public long Sequence { get { return inner.Sequence; } }

            public int ApplyCount { get; private set; }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                return inner.Preflight(commands);
            }

            public RewardChildApplyResult Apply(
                RewardChildGrantCommand command)
            {
                ApplyCount++;
                return inner.Apply(command);
            }
        }

        private sealed class FixedEquipmentResolver :
            IStrongboxEquipmentPayloadResolver
        {
            public bool TryResolve(
                StrongboxDefinition definition,
                StrongboxInstanceContext boxContext,
                RewardOperationRequest operation,
                RewardGrant equipmentGrant,
                out IReadOnlyList<EquipmentInstance> equipmentInstances,
                out string rejectionCode)
            {
                var values = new List<EquipmentInstance>();
                for (long unit = 0L; unit < equipmentGrant.Quantity; unit++)
                {
                    StableId instanceId = Strongbox.DeriveId(
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

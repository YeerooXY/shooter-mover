using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Rewards.Strongboxes
{
    public sealed class StrongboxOpeningServiceV1Tests
    {
        private static readonly StableId ScrapAuthority = Id("authority.scrap");
        private static readonly StableId ScrapCurrency = Id("currency.scrap");
        private static readonly StableId HoldingsAuthority = Id("holdings.player");
        private static readonly StableId RapAuthority = Id("authority.rap");
        private static readonly StableId PlayerId = Id("player.primary");
        private static readonly StableId BoxId = Id("strongbox.instance-one");
        private static readonly StableId TierId = Id("strongbox.tier-one");

        [Test]
        public void SuccessfulOpeningConsumesOneOwnedBox()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            UniqueHoldingSnapshotV1 ignored;
            Assert.That(fixture.Holdings.TryGetUnique(BoxId, out ignored), Is.False);
            Assert.That(fixture.Service.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void SuccessfulOpeningAlwaysGrantsPositiveScrap()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(fixture.Scrap.Balance, Is.GreaterThan(0L));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void SuccessfulOpeningGrantsDeterministicGeneratedRewards()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.GeneratedOutcome.RewardResult.Grants.Count, Is.EqualTo(2));
            Assert.That(fixture.Holdings.GetStackQuantity(
                RewardGrantKindV1.Miscellaneous,
                Id("misc.strongbox-part")), Is.EqualTo(10L));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void SameDefinitionContextAndSeedGiveSameResultAndTrace()
        {
            Fixture left = new Fixture();
            Fixture right = new Fixture();
            left.AddAndRegisterBox();
            right.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 first = left.Open();
            StrongboxOpeningResultRuntimeV1 second = right.Open();

            Assert.That(second.GeneratedOutcome.RewardResult.Fingerprint,
                Is.EqualTo(first.GeneratedOutcome.RewardResult.Fingerprint));
            Assert.That(second.GeneratedOutcome.GenerationTrace.Fingerprint,
                Is.EqualTo(first.GeneratedOutcome.GenerationTrace.Fingerprint));
            Assert.That(second.GeneratedOutcome.RewardTrace.Fingerprint,
                Is.EqualTo(first.GeneratedOutcome.RewardTrace.Fingerprint));
        }

        [Test]
        public void ExactDuplicateOpeningGivesNoAdditionalReward()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();
            StrongboxOpeningResultRuntimeV1 first = fixture.Open();
            long scrapSequence = fixture.Scrap.Sequence;
            long holdingsSequence = fixture.Holdings.Sequence;

            StrongboxOpeningResultRuntimeV1 duplicate = fixture.Open();

            Assert.That(duplicate.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(duplicate.TerminalFact.Fingerprint, Is.EqualTo(first.TerminalFact.Fingerprint));
            Assert.That(fixture.Scrap.Sequence, Is.EqualTo(scrapSequence));
            Assert.That(fixture.Holdings.Sequence, Is.EqualTo(holdingsSequence));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void ConflictingDuplicateOpeningIsRejected()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();
            fixture.Open();
            StrongboxOpenCommandV1 conflict = fixture.Command(Id("player.other"));

            StrongboxOpeningResultRuntimeV1 result = fixture.Service.Open(conflict);

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ConflictingDuplicate));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void UnknownBoxInstanceIsRejected()
        {
            Fixture fixture = new Fixture();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.UnknownBoxInstance));
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        [Test]
        public void BoxNotOwnedIsRejected()
        {
            Fixture fixture = new Fixture();
            fixture.RegisterBox();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.StrongboxNotOwned));
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        [Test]
        public void UnknownTierIsRejectedWithoutMutation()
        {
            Fixture fixture = new Fixture();
            StableId unknownTier = Id("strongbox.unknown-tier");
            fixture.RegisterBox(unknownTier);
            fixture.AddBox(unknownTier);

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.InvalidDefinition));
            UniqueHoldingSnapshotV1 owned;
            Assert.That(fixture.Holdings.TryGetUnique(BoxId, out owned), Is.True);
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        [Test]
        public void GeneratorFailureLeavesBoxOwned()
        {
            Fixture fixture = new Fixture(generator: new ThrowingGenerator());
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.GeneratorRejected));
            UniqueHoldingSnapshotV1 owned;
            Assert.That(fixture.Holdings.TryGetUnique(BoxId, out owned), Is.True);
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        [Test]
        public void RapPreflightRejectionLeavesStateRetryable()
        {
            RejectFirstPreflightAuthority scrapGate = new RejectFirstPreflightAuthority();
            Fixture fixture = new Fixture(scrapDecorator: scrapGate);
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 rejected = fixture.Open();
            StrongboxOpeningResultRuntimeV1 retried = fixture.Open();

            Assert.That(rejected.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.RewardRejected));
            Assert.That(retried.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(retried.GeneratedOutcome.Fingerprint, Is.EqualTo(rejected.GeneratedOutcome.Fingerprint));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void InterruptedClaimRetriesWithIdenticalIds()
        {
            RejectFirstApplyAuthority scrapGate = new RejectFirstApplyAuthority();
            Fixture fixture = new Fixture(scrapDecorator: scrapGate);
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 pending = fixture.Open();
            List<string> pendingIds = ChildIds(pending.RewardApplicationResult.CommitmentSnapshot);
            StrongboxOpeningSnapshotV1 captured = fixture.Service.ExportSnapshot();
            StrongboxOpeningRecordSnapshotV1 original = captured.Openings[0];
            StrongboxOpeningRecordSnapshotV1 staleRecord = new StrongboxOpeningRecordSnapshotV1(
                original.Command,
                StrongboxOpeningStageV1.RewardCommitted,
                original.GeneratedOutcome,
                original.CommitCommand,
                original.ClaimCommand,
                original.ConsumeCommand,
                null,
                null);
            StrongboxOpeningSnapshotV1 staleSnapshot = StrongboxOpeningSnapshotV1.CreateCanonical(
                captured.DefinitionCatalogFingerprint,
                captured.Sequence,
                captured.Contexts,
                new[] { staleRecord });
            StrongboxOpeningServiceV1 restored = fixture.CreateOpeningService();
            Assert.That(restored.ImportSnapshot(staleSnapshot).Succeeded, Is.True);

            StrongboxOpeningResultRuntimeV1 retried = restored.Open(fixture.Command());
            List<string> appliedIds = ChildIds(retried.RewardApplicationResult.CommitmentSnapshot);

            Assert.That(pending.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ClaimedPendingApplication));
            Assert.That(retried.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(appliedIds, Is.EqualTo(pendingIds));
            Assert.That(retried.GeneratedOutcome.Fingerprint, Is.EqualTo(pending.GeneratedOutcome.Fingerprint));
        }

        [Test]
        public void ConsumeInterruptionRetriesSameRemovalCommand()
        {
            Fixture fixture = new Fixture(throwOnceOnConsume: true);
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 pending = fixture.Open();
            StrongboxOpeningRecordSnapshotV1 before = fixture.Service.ExportSnapshot().Openings[0];
            StrongboxOpeningResultRuntimeV1 retried = fixture.Open();
            StrongboxOpeningRecordSnapshotV1 after = fixture.Service.ExportSnapshot().Openings[0];

            Assert.That(pending.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ConsumePending));
            Assert.That(retried.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(after.ConsumeCommand.PayloadFingerprint, Is.EqualTo(before.ConsumeCommand.PayloadFingerprint));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void AlreadyAppliedOpeningSurvivesSnapshotReplay()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();
            StrongboxOpeningResultRuntimeV1 opened = fixture.Open();
            StrongboxOpeningSnapshotV1 snapshot = fixture.Service.ExportSnapshot();
            StrongboxOpeningServiceV1 restored = fixture.CreateOpeningService();

            StrongboxOpeningImportResultV1 imported = restored.ImportSnapshot(snapshot);
            StrongboxOpeningResultRuntimeV1 replay = restored.Open(fixture.Command());

            Assert.That(imported.Succeeded, Is.True);
            Assert.That(replay.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.TerminalFact.Fingerprint, Is.EqualTo(opened.TerminalFact.Fingerprint));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void ArbitraryTierDefinitionsRequireNoCodeChange()
        {
            List<StrongboxDefinitionV1> definitions = new List<StrongboxDefinitionV1>();
            for (int index = 0; index < 17; index++)
            {
                definitions.Add(Fixture.CreateDefinition(
                    Id("strongbox.tier-" + index.ToString("D2")),
                    index,
                    1L + index,
                    2L + index,
                    index));
            }

            StrongboxDefinitionCatalogV1 catalog = new StrongboxDefinitionCatalogV1(definitions);

            Assert.That(catalog.Definitions.Count, Is.EqualTo(17));
            StrongboxDefinitionV1 found;
            Assert.That(catalog.TryGet(Id("strongbox.tier-16"), out found), Is.True);
            Assert.That(found.DisplayOrder, Is.EqualTo(16));
        }

        [Test]
        public void TierBiasReachesSharedGenerator()
        {
            CapturingGenerator capture = new CapturingGenerator();
            Fixture fixture = new Fixture(generator: capture);
            fixture.AddAndRegisterBox();

            fixture.Open();

            long value;
            Assert.That(capture.LastRequest.TryGetScalingValue(Id("scaling.source-tier"), out value), Is.True);
            Assert.That(value, Is.EqualTo(2L));
        }

        [Test]
        public void ExceptionalSourceBiasCanBeRepresented()
        {
            CapturingGenerator capture = new CapturingGenerator();
            Fixture fixture = new Fixture(generator: capture);
            fixture.AddAndRegisterBox();

            fixture.Open();

            long value;
            Assert.That(capture.LastRequest.TryGetScalingValue(Id("scaling.exceptional"), out value), Is.True);
            Assert.That(value, Is.EqualTo(5L));
            Assert.That(fixture.Definition.ExceptionalRollBias, Is.EqualTo(2L));
        }

        [Test]
        public void DuplicateBoxInstanceIdentityIsRejected()
        {
            Fixture fixture = new Fixture();
            StrongboxRegistrationResultV1 first = fixture.RegisterBox();
            StrongboxInstanceContextV1 conflict = fixture.Context(TierId, 999UL);

            StrongboxRegistrationResultV1 second = fixture.Service.RegisterInstance(conflict);

            Assert.That(first.Status, Is.EqualTo(StrongboxRegistrationStatusV1.Registered));
            Assert.That(second.Status, Is.EqualTo(StrongboxRegistrationStatusV1.ConflictingDuplicate));
        }

        [Test]
        public void CanonicalDefinitionOrderingAndFingerprintsAreStable()
        {
            StrongboxDefinitionV1 first = Fixture.CreateDefinition(Id("strongbox.alpha"), 2, 2L, 3L, 4L);
            StrongboxDefinitionV1 second = Fixture.CreateDefinition(Id("strongbox.beta"), 1, 5L, 6L, 7L);
            StrongboxDefinitionCatalogV1 left = new StrongboxDefinitionCatalogV1(new[] { first, second });
            StrongboxDefinitionCatalogV1 right = new StrongboxDefinitionCatalogV1(new[] { second, first });

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(left.Definitions[0].TierStableId, Is.EqualTo(second.TierStableId));
        }

        [Test]
        public void InvalidZeroScrapPolicyIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                StrongboxMandatoryScrapPolicyV1.Create(ScrapCurrency, 0L, 1L);
            });
        }

        [Test]
        public void RealIntegrationExercisesGenInvScrapAndRap()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();
            RewardCommitmentSnapshotV1 commitment;
            bool found = fixture.Rap.TryGetCommitment(
                result.GeneratedOutcome.Operation.CommitmentStableId,
                out commitment);

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(found, Is.True);
            Assert.That(commitment.State, Is.EqualTo(RewardCommitmentStateV1.Applied));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
            Assert.That(fixture.Holdings.GetStackQuantity(
                RewardGrantKindV1.Miscellaneous,
                Id("misc.strongbox-part")), Is.EqualTo(10L));
        }

        [Test]
        public void EquipmentRewardUsesResolverAndInvThroughRap()
        {
            StrongboxDefinitionV1 equipmentDefinition = Fixture.EquipmentDefinition();
            FixedEquipmentResolver equipmentResolver = new FixedEquipmentResolver();
            Fixture fixture = new Fixture(
                definition: equipmentDefinition,
                payloadResolver: new DeterministicStrongboxGrantPayloadResolverV1(equipmentResolver));
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultRuntimeV1 result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            UniqueHoldingSnapshotV1 equipment;
            Assert.That(fixture.Holdings.TryGetUnique(equipmentResolver.LastInstanceId, out equipment), Is.True);
            Assert.That(equipment.RewardKind, Is.EqualTo(RewardGrantKindV1.EquipmentReference));
            Assert.That(equipmentDefinition.CompatibleGenerationPolicyStableId,
                Is.EqualTo(equipmentResolver.LastPolicyId));
        }

        [Test]
        public void ExpectedOpeningSequenceConflictDoesNotGenerateOrMutate()
        {
            CapturingGenerator capture = new CapturingGenerator();
            Fixture fixture = new Fixture(generator: capture);
            fixture.AddAndRegisterBox();
            StrongboxOpenCommandV1 command = fixture.Command(PlayerId, 7L);

            StrongboxOpeningResultRuntimeV1 result = fixture.Service.Open(command);

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ExpectedSequenceConflict));
            Assert.That(capture.CallCount, Is.Zero);
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        private static List<string> ChildIds(RewardCommitmentSnapshotV1 snapshot)
        {
            List<string> ids = new List<string>();
            for (int index = 0; index < snapshot.Children.Count; index++)
            {
                ids.Add(snapshot.Children[index].Command.TransactionStableId.ToString()
                    + "|" + snapshot.Children[index].Command.OperationStableId.ToString());
            }
            return ids;
        }

        private static StableId Id(string value) { return StableId.Parse(value); }

        private sealed class Fixture
        {
            private readonly IStrongboxRewardGeneratorV1 generator;
            private readonly IStrongboxGrantPayloadResolverV1 payloadResolver;
            private readonly IPlayerHoldingsAuthorityV1 openingHoldings;

            public Fixture(
                IStrongboxRewardGeneratorV1 generator = null,
                IStrongboxGrantPayloadResolverV1 payloadResolver = null,
                StrongboxDefinitionV1 definition = null,
                RewardAuthorityDecorator scrapDecorator = null,
                bool throwOnceOnConsume = false)
            {
                Definition = definition ?? CreateDefinition(TierId, 0, 2L, 3L, 2L);
                Catalog = new StrongboxDefinitionCatalogV1(new[] { Definition });
                this.generator = generator ?? new SharedStrongboxRewardGeneratorV1(new RewardGenerationServiceV1());
                this.payloadResolver = payloadResolver ?? new DeterministicStrongboxGrantPayloadResolverV1();
                Money = new MoneyWalletService();
                Scrap = new ScrapWalletServiceV1(ScrapAuthority, ScrapCurrency);
                Holdings = new PlayerHoldingsService(HoldingsAuthority, 1000L, new AcceptingEquipmentValidator());
                IRewardChildAuthorityV1 moneyAuthority = new MoneyRewardChildAuthorityV1(Money);
                IRewardChildAuthorityV1 scrapAuthority = new ScrapRewardChildAuthorityV1(Scrap);
                if (scrapDecorator != null)
                {
                    scrapDecorator.Inner = scrapAuthority;
                    scrapAuthority = scrapDecorator;
                }
                IRewardChildAuthorityV1 holdingsAuthority =
                    new PlayerHoldingsRewardChildAuthorityV1(Holdings, new AcceptingEquipmentValidator());
                Rap = new RewardApplicationServiceV1(
                    RapAuthority,
                    moneyAuthority,
                    scrapAuthority,
                    holdingsAuthority);
                if (throwOnceOnConsume)
                {
                    openingHoldings = new ThrowOnceRemoveHoldings(Holdings);
                }
                else
                {
                    openingHoldings = Holdings;
                }
                Service = CreateOpeningService();
            }

            public StrongboxDefinitionV1 Definition { get; }
            public StrongboxDefinitionCatalogV1 Catalog { get; }
            public MoneyWalletService Money { get; }
            public ScrapWalletServiceV1 Scrap { get; }
            public PlayerHoldingsService Holdings { get; }
            public RewardApplicationServiceV1 Rap { get; }
            public StrongboxOpeningServiceV1 Service { get; }

            public StrongboxOpeningServiceV1 CreateOpeningService()
            {
                return new StrongboxOpeningServiceV1(
                    Catalog,
                    generator,
                    openingHoldings,
                    Rap,
                    payloadResolver);
            }

            public StrongboxInstanceContextV1 Context(StableId tierId, ulong seed = 123456UL)
            {
                return StrongboxInstanceContextV1.Create(
                    BoxId,
                    tierId,
                    seed,
                    1,
                    ProgressionContext.Create(5, 2, Id("difficulty.normal"), 1),
                    Id("source.strongbox-test"),
                    Id("provenance.strongbox-test"),
                    tierId == Definition.TierStableId ? Definition.Fingerprint : null);
            }

            public StrongboxRegistrationResultV1 RegisterBox(StableId tierId = null)
            {
                return Service.RegisterInstance(Context(tierId ?? Definition.TierStableId));
            }

            public void AddBox(StableId tierId = null)
            {
                StableId definitionId = tierId ?? Definition.TierStableId;
                PlayerHoldingsMutationResultV1 result = Holdings.Apply(
                    PlayerHoldingsCommandV1.AddStrongbox(
                        Id("holdtx.add-box"),
                        Id("holdop.add-box"),
                        HoldingsAuthority,
                        definitionId,
                        BoxId,
                        HoldingProvenanceV1.Create(
                            Id("grant.add-box"),
                            Id("source.add-box"))));
                Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatusV1.Applied));
            }

            public void AddAndRegisterBox()
            {
                AddBox();
                Assert.That(RegisterBox().Status, Is.EqualTo(StrongboxRegistrationStatusV1.Registered));
            }

            public StrongboxOpenCommandV1 Command(
                StableId player = null,
                long? expectedSequence = null)
            {
                return StrongboxOpenCommandV1.Create(
                    Id("opening.primary"),
                    Id("run.primary"),
                    BoxId,
                    player ?? PlayerId,
                    MoneyWalletIdsV1.AuthorityStableId,
                    ScrapAuthority,
                    HoldingsAuthority,
                    expectedSequence);
            }

            public StrongboxOpeningResultRuntimeV1 Open()
            {
                return Service.Open(Command());
            }

            public static StrongboxDefinitionV1 CreateDefinition(
                StableId tierId,
                int displayOrder,
                long generationBias,
                long qualityBias,
                long exceptionalBias)
            {
                RewardGrantSpecificationV1 misc = RewardGrantSpecificationV1.Create(
                    Id("grant.strongbox-part"),
                    RewardGrantKindV1.Miscellaneous,
                    Id("misc.strongbox-part"),
                    RewardQuantityRangeV1.Fixed(3L),
                    new[]
                    {
                        RewardScalingInputDescriptorV1.Create(
                            Id("scaling.source-tier"),
                            RewardScalingInputKindV1.SourceTier),
                        RewardScalingInputDescriptorV1.Create(
                            Id("scaling.exceptional"),
                            RewardScalingInputKindV1.Custom),
                    });
                RewardProfileV1 profile = RewardProfileV1.Create(
                    Id("profile.strongbox-base"),
                    new[] { misc },
                    Array.Empty<IndependentRewardRollV1>(),
                    Array.Empty<ExclusiveRewardGroupV1>());
                return StrongboxDefinitionV1.Create(
                    tierId,
                    displayOrder,
                    generationBias,
                    qualityBias,
                    exceptionalBias,
                    StrongboxRewardCountPolicyV1.Create(2, 2),
                    StrongboxMandatoryScrapPolicyV1.Create(ScrapCurrency, 4L, 4L),
                    Id("generation-policy.default"),
                    profile,
                    Id("scaling.source-tier"),
                    Id("scaling.exceptional"));
            }

            public static StrongboxDefinitionV1 EquipmentDefinition()
            {
                RewardGrantSpecificationV1 equipment = RewardGrantSpecificationV1.CreateFixed(
                    Id("grant.strongbox-equipment"),
                    RewardGrantKindV1.EquipmentReference,
                    Id("equipment.blaster"),
                    1L);
                RewardProfileV1 profile = RewardProfileV1.Create(
                    Id("profile.strongbox-equipment"),
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
                    StrongboxMandatoryScrapPolicyV1.Create(ScrapCurrency, 1L, 1L),
                    Id("generation-policy.equipment"),
                    profile,
                    Id("scaling.source-tier"),
                    Id("scaling.exceptional"));
            }
        }

        private sealed class CapturingGenerator : IStrongboxRewardGeneratorV1
        {
            private readonly SharedStrongboxRewardGeneratorV1 inner =
                new SharedStrongboxRewardGeneratorV1(new RewardGenerationServiceV1());
            public RewardGenerationRequestV1 LastRequest { get; private set; }
            public int CallCount { get; private set; }
            public RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request)
            {
                LastRequest = request;
                CallCount++;
                return inner.Generate(request);
            }
        }

        private sealed class ThrowingGenerator : IStrongboxRewardGeneratorV1
        {
            public RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request)
            {
                throw new InvalidOperationException("forced-generator-failure");
            }
        }

        private abstract class RewardAuthorityDecorator : IRewardChildAuthorityV1
        {
            public IRewardChildAuthorityV1 Inner { protected get; set; }
            public StableId AuthorityStableId { get { return Inner.AuthorityStableId; } }
            public long Sequence { get { return Inner.Sequence; } }
            public abstract RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands);
            public virtual RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
            {
                return Inner.Apply(command);
            }
        }

        private sealed class RejectFirstPreflightAuthority : RewardAuthorityDecorator
        {
            private bool reject = true;
            public override RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                if (!reject) { return Inner.Preflight(commands); }
                reject = false;
                List<RewardAuthorityPreflightFactV1> facts = new List<RewardAuthorityPreflightFactV1>();
                for (int index = 0; index < commands.Count; index++)
                {
                    facts.Add(new RewardAuthorityPreflightFactV1(
                        commands[index].TransactionStableId,
                        RewardAuthorityAdmissionStatusV1.CapacityRejected,
                        "forced-preflight-rejection"));
                }
                return new RewardAuthorityPreflightResultV1(facts);
            }
        }

        private sealed class RejectFirstApplyAuthority : RewardAuthorityDecorator
        {
            private bool reject = true;
            public override RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                return Inner.Preflight(commands);
            }
            public override RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
            {
                if (!reject) { return Inner.Apply(command); }
                reject = false;
                return new RewardChildApplyResultV1(
                    command.TransactionStableId,
                    RewardChildApplyStatusV1.Rejected,
                    false,
                    "forced-apply-interruption");
            }
        }

        private sealed class ThrowOnceRemoveHoldings : IPlayerHoldingsAuthorityV1
        {
            private readonly IPlayerHoldingsAuthorityV1 inner;
            private bool throwNext = true;
            public ThrowOnceRemoveHoldings(IPlayerHoldingsAuthorityV1 inner)
            {
                this.inner = inner;
            }
            public StableId AuthorityStableId { get { return inner.AuthorityStableId; } }
            public long Sequence { get { return inner.Sequence; } }
            public PlayerHoldingsMutationResultV1 Apply(PlayerHoldingsCommandV1 command)
            {
                if (throwNext
                    && command != null
                    && command.RewardKind == RewardGrantKindV1.Strongbox
                    && command.Transaction.Operation == EconomyTransactionOperationV1.RemoveUnique)
                {
                    throwNext = false;
                    throw new InvalidOperationException("forced-consume-interruption");
                }
                return inner.Apply(command);
            }
            public PlayerHoldingsSnapshotV1 ExportSnapshot() { return inner.ExportSnapshot(); }
            public PlayerHoldingsImportResultV1 ImportSnapshot(PlayerHoldingsSnapshotV1 snapshot)
            {
                return inner.ImportSnapshot(snapshot);
            }
        }

        private sealed class FixedEquipmentResolver : IStrongboxEquipmentPayloadResolverV1
        {
            public StableId LastInstanceId { get; private set; }
            public StableId LastPolicyId { get; private set; }
            public bool TryResolve(
                StrongboxDefinitionV1 definition,
                StrongboxInstanceContextV1 boxContext,
                RewardOperationRequestV1 operation,
                RewardGrantV1 equipmentGrant,
                out IReadOnlyList<EquipmentInstance> equipmentInstances,
                out string rejectionCode)
            {
                LastPolicyId = definition.CompatibleGenerationPolicyStableId;
                List<EquipmentInstance> values = new List<EquipmentInstance>();
                for (long unit = 0L; unit < equipmentGrant.Quantity; unit++)
                {
                    LastInstanceId = StrongboxCanonicalV1.DeriveId(
                        "boxequipment",
                        operation.SourceOperationStableId.ToString(),
                        equipmentGrant.GrantStableId.ToString(),
                        unit.ToString());
                    values.Add(EquipmentInstance.Create(
                        LastInstanceId,
                        equipmentGrant.ContentStableId,
                        1,
                        Id("quality.common"),
                        Array.Empty<AugmentInstance>()));
                }
                equipmentInstances = values;
                rejectionCode = null;
                return true;
            }
        }

        private sealed class AcceptingEquipmentValidator : IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    request != null && request.Instance != null,
                    "strongbox-test-catalog",
                    request == null || request.Instance == null ? null : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }
    }
}

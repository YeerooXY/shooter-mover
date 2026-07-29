using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Rewards.Strongboxes.Persistence;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
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
    public sealed class StrongboxOpeningActionsTests
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

            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            UniqueHoldingSnapshot ignored;
            Assert.That(fixture.Holdings.TryGetUnique(BoxId, out ignored), Is.False);
            Assert.That(fixture.Service.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void DurableOpeningRefreshSynchronizesDistinctRunScopeWithoutSecondPersist()
        {
            StableId untouchedBoxId = Id("strongbox.instance-run-scope-untouched");
            var runScope = new Fixture();
            var characterScope = new Fixture();
            runScope.AddAndRegisterBox();
            runScope.AddAndRegisterBox(untouchedBoxId, 654321UL);
            characterScope.AddAndRegisterBox();
            characterScope.AddAndRegisterBox(untouchedBoxId, 654321UL);

            Assert.That(
                characterScope.Open(),
                Has.Property("Status").EqualTo(
                    StrongboxOpeningLiveStatus.Opened));

            MissionRunStrongboxCollection selectedCollection =
                Collection("run-scope-selected", BoxId);
            MissionRunStrongboxCollection untouchedCollection =
                Collection("run-scope-untouched", untouchedBoxId);
            var selected = new MissionRunStrongboxResult(
                selectedCollection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            var untouched = new MissionRunStrongboxResult(
                untouchedCollection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            PlayerRouteProfilePayload route = PlayerRouteProfilePayload.Create(
                Id("character.run-scope"),
                Id("loadout.run-scope"),
                new[] { Id("equipment.run-scope") });
            MissionResultPayload before = MissionResultPayload.Create(
                Id("run.run-scope"),
                route,
                MissionRunCompletionState.Completed,
                new[] { selected, untouched },
                1L,
                1L,
                MissionRun.Fingerprint("holdings.run-scope-before"),
                1L,
                MissionRun.Fingerprint("opening.run-scope-before"));
            var bridge = new RecordingCharacterStrongboxes(
                characterScope.Service);
            CharacterStrongboxesRegistry.Configure(bridge);
            try
            {
                var context = new ResultsContext(
                    before,
                    runScope.Service,
                    value => characterScope.Command(),
                    null,
                    () => BuildRunScopeResult(
                        before,
                        selectedCollection,
                        untouchedCollection,
                        runScope.Service));

                ResultsContext refreshed =
                    context.RefreshAfterExactOpening(
                        selected,
                        true,
                        durablePersistenceAlreadyCompleted: true);

                Assert.That(bridge.PersistCallCount, Is.Zero,
                    "Durable opening has already saved the character authority.");
                StrongboxOpeningSnapshot runSnapshot =
                    runScope.Service.ExportSnapshot();
                Assert.That(IsOpened(runSnapshot, BoxId), Is.True);
                Assert.That(IsOpened(runSnapshot, untouchedBoxId), Is.False);
                Assert.That(refreshed.Result.OpenedStrongboxes,
                    Has.Count.EqualTo(1));
                Assert.That(refreshed.Result.OpenedStrongboxes[0].InstanceStableId,
                    Is.EqualTo(BoxId));
                Assert.That(refreshed.Result.UnopenedStrongboxes,
                    Has.Count.EqualTo(1));
                Assert.That(refreshed.Result.UnopenedStrongboxes[0].InstanceStableId,
                    Is.EqualTo(untouchedBoxId));

                string fingerprintAfterRefresh = runSnapshot.Fingerprint;
                var reentered = new ResultsContext(
                    refreshed.Result,
                    runScope.Service,
                    value => characterScope.Command(),
                    null,
                    () => refreshed.Result);
                Assert.That(reentered.Result.UnopenedStrongboxes,
                    Has.Count.EqualTo(1));
                Assert.That(reentered.Result.UnopenedStrongboxes[0].InstanceStableId,
                    Is.EqualTo(untouchedBoxId));
                Assert.That(runScope.Service.ExportSnapshot().Fingerprint,
                    Is.EqualTo(fingerprintAfterRefresh));
            }
            finally
            {
                CharacterStrongboxesRegistry.Clear(bridge);
            }
        }

        [Test]
        public void SuccessfulOpeningAlwaysGrantsPositiveScrap()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(fixture.Scrap.Balance, Is.GreaterThan(0L));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void SuccessfulOpeningGrantsDeterministicGeneratedRewards()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(result.GeneratedOutcome.RewardResult.Grants.Count, Is.EqualTo(2));
            Assert.That(fixture.Holdings.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
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

            StrongboxOpeningResultLive first = left.Open();
            StrongboxOpeningResultLive second = right.Open();

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
            StrongboxOpeningResultLive first = fixture.Open();
            long scrapSequence = fixture.Scrap.Sequence;
            long holdingsSequence = fixture.Holdings.Sequence;

            StrongboxOpeningResultLive duplicate = fixture.Open();

            Assert.That(duplicate.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ExactDuplicateNoChange));
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
            StrongboxOpenCommand conflict = fixture.Command(Id("player.other"));

            StrongboxOpeningResultLive result = fixture.Service.Open(conflict);

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ConflictingDuplicate));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void UnknownBoxInstanceIsRejected()
        {
            Fixture fixture = new Fixture();

            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.UnknownBoxInstance));
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        [Test]
        public void BoxNotOwnedIsRejected()
        {
            Fixture fixture = new Fixture();
            fixture.RegisterBox();

            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.StrongboxNotOwned));
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        [Test]
        public void UnknownTierIsRejectedWithoutMutation()
        {
            Fixture fixture = new Fixture();
            StableId unknownTier = Id("strongbox.unknown-tier");
            fixture.AddBox(unknownTier);

            StrongboxRegistrationResult registration = fixture.RegisterBox(unknownTier);
            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(registration.Status, Is.EqualTo(StrongboxRegistrationStatus.UnknownDefinition));
            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.UnknownBoxInstance));
            UniqueHoldingSnapshot owned;
            Assert.That(fixture.Holdings.TryGetUnique(BoxId, out owned), Is.True);
            Assert.That(fixture.Scrap.Balance, Is.Zero);
            Assert.That(fixture.Service.Sequence, Is.Zero);
        }

        [Test]
        public void GeneratorFailureLeavesBoxOwned()
        {
            Fixture fixture = new Fixture(generator: new ThrowingGenerator());
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.GeneratorRejected));
            UniqueHoldingSnapshot owned;
            Assert.That(fixture.Holdings.TryGetUnique(BoxId, out owned), Is.True);
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        [Test]
        public void RapPreflightRejectionLeavesStateRetryable()
        {
            RejectFirstPreflightState scrapGate = new RejectFirstPreflightState();
            Fixture fixture = new Fixture(scrapDecorator: scrapGate);
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive rejected = fixture.Open();
            StrongboxOpeningResultLive retried = fixture.Open();

            Assert.That(rejected.Status, Is.EqualTo(StrongboxOpeningLiveStatus.RewardRejected));
            Assert.That(retried.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(retried.GeneratedOutcome.Fingerprint, Is.EqualTo(rejected.GeneratedOutcome.Fingerprint));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void InterruptedClaimRetriesWithIdenticalIds()
        {
            RejectFirstApplyState scrapGate = new RejectFirstApplyState();
            Fixture fixture = new Fixture(scrapDecorator: scrapGate);
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive pending = fixture.Open();
            List<string> pendingIds = ChildIds(pending.RewardApplicationResult.CommitmentSnapshot);
            StrongboxOpeningSnapshot captured = fixture.Service.ExportSnapshot();
            StrongboxOpeningRecordSnapshot original = captured.Openings[0];
            StrongboxOpeningRecordSnapshot staleRecord = new StrongboxOpeningRecordSnapshot(
                original.Command,
                StrongboxOpeningStage.RewardCommitted,
                original.GeneratedOutcome,
                original.CommitCommand,
                original.ClaimCommand,
                original.ConsumeCommand,
                null,
                null);
            StrongboxOpeningSnapshot staleSnapshot = StrongboxOpeningSnapshot.CreateCanonical(
                captured.DefinitionCatalogFingerprint,
                captured.Sequence,
                captured.Contexts,
                new[] { staleRecord });
            StrongboxOpeningActions restored = fixture.CreateOpeningService();
            Assert.That(restored.ImportSnapshot(staleSnapshot).Succeeded, Is.True);

            StrongboxOpeningResultLive retried = restored.Open(fixture.Command());
            List<string> appliedIds = ChildIds(retried.RewardApplicationResult.CommitmentSnapshot);

            Assert.That(pending.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ClaimedPendingApplication));
            Assert.That(retried.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(appliedIds, Is.EqualTo(pendingIds));
            Assert.That(retried.GeneratedOutcome.Fingerprint, Is.EqualTo(pending.GeneratedOutcome.Fingerprint));
        }

        [Test]
        public void ConsumeInterruptionRetriesSameRemovalCommand()
        {
            Fixture fixture = new Fixture(throwOnceOnConsume: true);
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive pending = fixture.Open();
            StrongboxOpeningRecordSnapshot before = fixture.Service.ExportSnapshot().Openings[0];
            StrongboxOpeningResultLive retried = fixture.Open();
            StrongboxOpeningRecordSnapshot after = fixture.Service.ExportSnapshot().Openings[0];

            Assert.That(pending.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ConsumePending));
            Assert.That(retried.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(after.ConsumeCommand.PayloadFingerprint, Is.EqualTo(before.ConsumeCommand.PayloadFingerprint));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void SecondOpeningIdentityWhileConsumePendingCannotAwardAgain()
        {
            CapturingGenerator capture = new CapturingGenerator();
            Fixture fixture = new Fixture(generator: capture, throwOnceOnConsume: true);
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive pending = fixture.Open();
            StrongboxOpeningResultLive conflict = fixture.Service.Open(
                fixture.CommandWithOpening(Id("opening.secondary")));

            Assert.That(pending.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ConsumePending));
            Assert.That(conflict.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ConflictingDuplicate));
            Assert.That(capture.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void AlreadyAppliedOpeningSurvivesSnapshotReplay()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();
            StrongboxOpeningResultLive opened = fixture.Open();
            StrongboxOpeningSnapshot snapshot = fixture.Service.ExportSnapshot();
            StrongboxOpeningActions restored = fixture.CreateOpeningService();

            StrongboxOpeningImportResult imported = restored.ImportSnapshot(snapshot);
            StrongboxOpeningResultLive replay = restored.Open(fixture.Command());

            Assert.That(imported.Succeeded, Is.True);
            Assert.That(replay.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ExactDuplicateNoChange));
            Assert.That(replay.TerminalFact.Fingerprint, Is.EqualTo(opened.TerminalFact.Fingerprint));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
        }

        [Test]
        public void ArbitraryTierDefinitionsRequireNoCodeChange()
        {
            List<StrongboxDefinition> definitions = new List<StrongboxDefinition>();
            for (int index = 0; index < 17; index++)
            {
                definitions.Add(Fixture.CreateDefinition(
                    Id("strongbox.tier-" + index.ToString("D2")),
                    index,
                    1L + index,
                    2L + index,
                    index));
            }

            StrongboxDefinitionCatalog catalog = new StrongboxDefinitionCatalog(definitions);

            Assert.That(catalog.Definitions.Count, Is.EqualTo(17));
            StrongboxDefinition found;
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
            StrongboxRegistrationResult first = fixture.RegisterBox();
            StrongboxInstanceContext conflict = fixture.Context(TierId, 999UL);

            StrongboxRegistrationResult second = fixture.Service.RegisterInstance(conflict);

            Assert.That(first.Status, Is.EqualTo(StrongboxRegistrationStatus.Registered));
            Assert.That(second.Status, Is.EqualTo(StrongboxRegistrationStatus.ConflictingDuplicate));
        }

        [Test]
        public void CanonicalDefinitionOrderingAndFingerprintsAreStable()
        {
            StrongboxDefinition first = Fixture.CreateDefinition(Id("strongbox.alpha"), 2, 2L, 3L, 4L);
            StrongboxDefinition second = Fixture.CreateDefinition(Id("strongbox.beta"), 1, 5L, 6L, 7L);
            StrongboxDefinitionCatalog left = new StrongboxDefinitionCatalog(new[] { first, second });
            StrongboxDefinitionCatalog right = new StrongboxDefinitionCatalog(new[] { second, first });

            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(left.Definitions[0].TierStableId, Is.EqualTo(second.TierStableId));
        }

        [Test]
        public void InvalidZeroScrapPolicyIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                StrongboxMandatoryScrapPolicy.Create(ScrapCurrency, 0L, 1L);
            });
        }

        [Test]
        public void RealIntegrationExercisesGenInvScrapAndRap()
        {
            Fixture fixture = new Fixture();
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive result = fixture.Open();
            RewardCommitmentSnapshot commitment;
            bool found = fixture.Rap.TryGetCommitment(
                result.GeneratedOutcome.Operation.CommitmentStableId,
                out commitment);

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(found, Is.True);
            Assert.That(commitment.State, Is.EqualTo(RewardCommitmentState.Applied));
            Assert.That(fixture.Scrap.Balance, Is.EqualTo(4L));
            Assert.That(fixture.Holdings.GetStackQuantity(
                RewardGrantKind.Miscellaneous,
                Id("misc.strongbox-part")), Is.EqualTo(10L));
        }

        [Test]
        public void EquipmentRewardUsesResolverAndInvThroughRap()
        {
            StrongboxDefinition equipmentDefinition = Fixture.EquipmentDefinition();
            FixedEquipmentResolver equipmentResolver = new FixedEquipmentResolver();
            Fixture fixture = new Fixture(
                definition: equipmentDefinition,
                payloadResolver: new DeterministicStrongboxGrantPayloadResolver(equipmentResolver));
            fixture.AddAndRegisterBox();

            StrongboxOpeningResultLive result = fixture.Open();

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            UniqueHoldingSnapshot equipment;
            Assert.That(fixture.Holdings.TryGetUnique(equipmentResolver.LastInstanceId, out equipment), Is.True);
            Assert.That(equipment.RewardKind, Is.EqualTo(RewardGrantKind.EquipmentReference));
            Assert.That(equipmentDefinition.CompatibleGenerationPolicyStableId,
                Is.EqualTo(equipmentResolver.LastPolicyId));
        }

        [Test]
        public void ExpectedOpeningSequenceConflictDoesNotGenerateOrMutate()
        {
            CapturingGenerator capture = new CapturingGenerator();
            Fixture fixture = new Fixture(generator: capture);
            fixture.AddAndRegisterBox();
            StrongboxOpenCommand command = fixture.Command(PlayerId, 7L);

            StrongboxOpeningResultLive result = fixture.Service.Open(command);

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningLiveStatus.ExpectedSequenceConflict));
            Assert.That(capture.CallCount, Is.Zero);
            Assert.That(fixture.Scrap.Balance, Is.Zero);
        }

        private static List<string> ChildIds(RewardCommitmentSnapshot snapshot)
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

        private static MissionRunStrongboxCollection Collection(
            string suffix,
            StableId instanceStableId)
        {
            return new MissionRunStrongboxCollection(
                Id("strongbox.definition." + suffix),
                instanceStableId,
                Id("grant." + suffix),
                Id("source." + suffix),
                Id("operation." + suffix),
                1L,
                MissionRun.Fingerprint("collection." + suffix));
        }

        private static MissionResultPayload BuildRunScopeResult(
            MissionResultPayload before,
            MissionRunStrongboxCollection selectedCollection,
            MissionRunStrongboxCollection untouchedCollection,
            StrongboxOpeningActions runScope)
        {
            StrongboxOpeningSnapshot snapshot = runScope.ExportSnapshot();
            StrongboxOpeningRecordSnapshot selectedOpening = snapshot.Openings
                .SingleOrDefault(item => item.Command.StrongboxInstanceStableId
                    == selectedCollection.InstanceStableId);
            MissionRunStrongboxResult selected = selectedOpening == null
                ? new MissionRunStrongboxResult(
                    selectedCollection,
                    MissionRunStrongboxState.Unopened,
                    null,
                    null)
                : new MissionRunStrongboxResult(
                    selectedCollection,
                    MissionRunStrongboxState.Opened,
                    selectedOpening.Command.OpeningStableId,
                    selectedOpening.Fingerprint);
            var untouched = new MissionRunStrongboxResult(
                untouchedCollection,
                MissionRunStrongboxState.Unopened,
                null,
                null);
            return MissionResultPayload.Create(
                before.RunStableId,
                before.RoutePayload,
                before.CompletionState,
                new[] { selected, untouched },
                before.HoldingsSequence + 1L,
                before.HoldingsSequence + 1L,
                before.HoldingsFingerprint,
                before.StrongboxOpeningSequence + 1L,
                snapshot.Fingerprint);
        }

        private static bool IsOpened(
            StrongboxOpeningSnapshot snapshot,
            StableId strongboxInstanceStableId)
        {
            return snapshot.Openings.Any(item =>
                item.Command.StrongboxInstanceStableId == strongboxInstanceStableId
                && item.Stage == StrongboxOpeningStage.Opened);
        }

        private sealed class RecordingCharacterStrongboxes :
            ICharacterStrongboxes
        {
            private readonly StrongboxOpeningActions authority;

            public RecordingCharacterStrongboxes(
                StrongboxOpeningActions authority)
            {
                this.authority = authority
                    ?? throw new ArgumentNullException(nameof(authority));
            }

            public int PersistCallCount { get; private set; }

            public bool TryResolve(
                out StrongboxOpeningActions resolved,
                out string rejectionCode)
            {
                resolved = authority;
                rejectionCode = string.Empty;
                return true;
            }

            public bool TryResolveDurableOpeningExecutor(
                out IStrongboxDurableOpeningExecutor executor,
                out string rejectionCode)
            {
                executor = null;
                rejectionCode = "not-needed-for-refresh";
                return false;
            }

            public bool TryPersist(
                string strongboxSnapshotFingerprint,
                out string rejectionCode)
            {
                PersistCallCount++;
                rejectionCode = string.Empty;
                return true;
            }
        }

        private sealed class Fixture
        {
            private readonly IStrongboxRewardGenerator generator;
            private readonly IStrongboxGrantPayloadResolver payloadResolver;
            private readonly IPlayerHoldingsState openingHoldings;

            public Fixture(
                IStrongboxRewardGenerator generator = null,
                IStrongboxGrantPayloadResolver payloadResolver = null,
                StrongboxDefinition definition = null,
                RewardStateDecorator scrapDecorator = null,
                bool throwOnceOnConsume = false)
            {
                Definition = definition ?? CreateDefinition(TierId, 0, 2L, 3L, 2L);
                Catalog = new StrongboxDefinitionCatalog(new[] { Definition });
                this.generator = generator ?? new SharedStrongboxRewardGenerator(new RewardGenerationActions());
                this.payloadResolver = payloadResolver ?? new DeterministicStrongboxGrantPayloadResolver();
                Money = new MoneyWalletActions();
                Scrap = new ScrapWalletActions(ScrapAuthority, ScrapCurrency);
                Holdings = new PlayerHoldingsActions(HoldingsAuthority, 1000L, new AcceptingEquipmentValidator());
                IRewardChildState moneyAuthority = new MoneyRewardChildState(Money);
                IRewardChildState scrapAuthority = new ScrapRewardChildState(Scrap);
                if (scrapDecorator != null)
                {
                    scrapDecorator.Inner = scrapAuthority;
                    scrapAuthority = scrapDecorator;
                }
                IRewardChildState holdingsAuthority =
                    new PlayerHoldingsRewardChildState(Holdings, new AcceptingEquipmentValidator());
                Rap = new RewardApplicationActions(
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

            public StrongboxDefinition Definition { get; }
            public StrongboxDefinitionCatalog Catalog { get; }
            public MoneyWalletActions Money { get; }
            public ScrapWalletActions Scrap { get; }
            public PlayerHoldingsActions Holdings { get; }
            public RewardApplicationActions Rap { get; }
            public StrongboxOpeningActions Service { get; }

            public StrongboxOpeningActions CreateOpeningService()
            {
                return new StrongboxOpeningActions(
                    Catalog,
                    generator,
                    openingHoldings,
                    Rap,
                    payloadResolver);
            }

            public StrongboxInstanceContext Context(
                StableId tierId,
                ulong seed = 123456UL,
                StableId instanceStableId = null)
            {
                return StrongboxInstanceContext.Create(
                    instanceStableId ?? BoxId,
                    tierId,
                    seed,
                    1,
                    ProgressionContext.Create(5, 2, Id("difficulty.normal"), 1),
                    Id("source.strongbox-test"),
                    Id("provenance.strongbox-test"),
                    tierId == Definition.TierStableId ? Definition.Fingerprint : null);
            }

            public StrongboxRegistrationResult RegisterBox(
                StableId tierId = null,
                ulong seed = 123456UL,
                StableId instanceStableId = null)
            {
                return Service.RegisterInstance(Context(
                    tierId ?? Definition.TierStableId,
                    seed,
                    instanceStableId));
            }

            public void AddBox(
                StableId tierId = null,
                StableId instanceStableId = null)
            {
                StableId definitionId = tierId ?? Definition.TierStableId;
                StableId strongboxInstanceStableId = instanceStableId ?? BoxId;
                PlayerHoldingsMutationResult result = Holdings.Apply(
                    PlayerHoldingsCommand.AddStrongbox(
                        Id("holdtx.add-box"),
                        Id("holdop.add-box"),
                        HoldingsAuthority,
                        definitionId,
                        strongboxInstanceStableId,
                        HoldingProvenance.Create(
                            Id("grant.add-box"),
                            Id("source.add-box"))));
                Assert.That(result.Status, Is.EqualTo(PlayerHoldingsMutationStatus.Applied));
            }

            public void AddAndRegisterBox(
                StableId instanceStableId = null,
                ulong seed = 123456UL)
            {
                AddBox(instanceStableId: instanceStableId);
                Assert.That(
                    RegisterBox(seed: seed, instanceStableId: instanceStableId).Status,
                    Is.EqualTo(StrongboxRegistrationStatus.Registered));
            }

            public StrongboxOpenCommand Command(
                StableId player = null,
                long? expectedSequence = null)
            {
                return StrongboxOpenCommand.Create(
                    Id("opening.primary"),
                    Id("run.primary"),
                    BoxId,
                    player ?? PlayerId,
                    MoneyWalletIds.AuthorityStableId,
                    ScrapAuthority,
                    HoldingsAuthority,
                    expectedSequence);
            }

            public StrongboxOpenCommand CommandWithOpening(StableId openingStableId)
            {
                return StrongboxOpenCommand.Create(
                    openingStableId,
                    Id("run.primary"),
                    BoxId,
                    PlayerId,
                    MoneyWalletIds.AuthorityStableId,
                    ScrapAuthority,
                    HoldingsAuthority);
            }

            public StrongboxOpeningResultLive Open()
            {
                return Service.Open(Command());
            }

            public static StrongboxDefinition CreateDefinition(
                StableId tierId,
                int displayOrder,
                long generationBias,
                long qualityBias,
                long exceptionalBias)
            {
                RewardGrantSpecification misc = RewardGrantSpecification.Create(
                    Id("grant.strongbox-part"),
                    RewardGrantKind.Miscellaneous,
                    Id("misc.strongbox-part"),
                    RewardQuantityRange.Fixed(3L),
                    new[]
                    {
                        RewardScalingInputDescriptor.Create(
                            Id("scaling.source-tier"),
                            RewardScalingInputKind.SourceTier),
                        RewardScalingInputDescriptor.Create(
                            Id("scaling.exceptional"),
                            RewardScalingInputKind.Custom),
                    });
                RewardProfile profile = RewardProfile.Create(
                    Id("profile.strongbox-base"),
                    new[] { misc },
                    Array.Empty<IndependentRewardRoll>(),
                    Array.Empty<ExclusiveRewardGroup>());
                return StrongboxDefinition.Create(
                    tierId,
                    displayOrder,
                    generationBias,
                    qualityBias,
                    exceptionalBias,
                    StrongboxRewardCountPolicy.Create(2, 2),
                    StrongboxMandatoryScrapPolicy.Create(ScrapCurrency, 4L, 4L),
                    Id("generation-policy.default"),
                    profile,
                    Id("scaling.source-tier"),
                    Id("scaling.exceptional"));
            }

            public static StrongboxDefinition EquipmentDefinition()
            {
                RewardGrantSpecification equipment = RewardGrantSpecification.CreateFixed(
                    Id("grant.strongbox-equipment"),
                    RewardGrantKind.EquipmentReference,
                    Id("equipment.blaster"),
                    1L);
                RewardProfile profile = RewardProfile.Create(
                    Id("profile.strongbox-equipment"),
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
                    StrongboxMandatoryScrapPolicy.Create(ScrapCurrency, 1L, 1L),
                    Id("generation-policy.equipment"),
                    profile,
                    Id("scaling.source-tier"),
                    Id("scaling.exceptional"));
            }
        }

        private sealed class CapturingGenerator : IStrongboxRewardGenerator
        {
            private readonly SharedStrongboxRewardGenerator inner =
                new SharedStrongboxRewardGenerator(new RewardGenerationActions());
            public RewardGenerationRequest LastRequest { get; private set; }
            public int CallCount { get; private set; }
            public RewardGenerationResultEnvelope Generate(RewardGenerationRequest request)
            {
                LastRequest = request;
                CallCount++;
                return inner.Generate(request);
            }
        }

        private sealed class ThrowingGenerator : IStrongboxRewardGenerator
        {
            public RewardGenerationResultEnvelope Generate(RewardGenerationRequest request)
            {
                throw new InvalidOperationException("forced-generator-failure");
            }
        }

        private abstract class RewardStateDecorator : IRewardChildState
        {
            public IRewardChildState Inner { protected get; set; }
            public StableId AuthorityStableId { get { return Inner.AuthorityStableId; } }
            public long Sequence { get { return Inner.Sequence; } }
            public abstract RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands);
            public virtual RewardChildApplyResult Apply(RewardChildGrantCommand command)
            {
                return Inner.Apply(command);
            }
        }

        private sealed class RejectFirstPreflightState : RewardStateDecorator
        {
            private bool reject = true;
            public override RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                if (!reject) { return Inner.Preflight(commands); }
                reject = false;
                List<RewardStatePreflightFact> facts = new List<RewardStatePreflightFact>();
                for (int index = 0; index < commands.Count; index++)
                {
                    facts.Add(new RewardStatePreflightFact(
                        commands[index].TransactionStableId,
                        RewardStateAdmissionStatus.CapacityRejected,
                        "forced-preflight-rejection"));
                }
                return new RewardStatePreflightResult(facts);
            }
        }

        private sealed class RejectFirstApplyState : RewardStateDecorator
        {
            private bool reject = true;
            public override RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                return Inner.Preflight(commands);
            }
            public override RewardChildApplyResult Apply(RewardChildGrantCommand command)
            {
                if (!reject) { return Inner.Apply(command); }
                reject = false;
                return new RewardChildApplyResult(
                    command.TransactionStableId,
                    RewardChildApplyStatus.Rejected,
                    false,
                    "forced-apply-interruption");
            }
        }

        private sealed class ThrowOnceRemoveHoldings : IPlayerHoldingsState
        {
            private readonly IPlayerHoldingsState inner;
            private bool throwNext = true;
            public ThrowOnceRemoveHoldings(IPlayerHoldingsState inner)
            {
                this.inner = inner;
            }
            public StableId AuthorityStableId { get { return inner.AuthorityStableId; } }
            public long Sequence { get { return inner.Sequence; } }
            public PlayerHoldingsMutationResult Apply(PlayerHoldingsCommand command)
            {
                if (throwNext
                    && command != null
                    && command.RewardKind == RewardGrantKind.Strongbox
                    && command.Transaction.Operation == EconomyTransactionOperation.RemoveUnique)
                {
                    throwNext = false;
                    throw new InvalidOperationException("forced-consume-interruption");
                }
                return inner.Apply(command);
            }
            public PlayerHoldingsSnapshot ExportSnapshot() { return inner.ExportSnapshot(); }
            public PlayerHoldingsImportResult ImportSnapshot(PlayerHoldingsSnapshot snapshot)
            {
                return inner.ImportSnapshot(snapshot);
            }
        }

        private sealed class FixedEquipmentResolver : IStrongboxEquipmentPayloadResolver
        {
            public StableId LastInstanceId { get; private set; }
            public StableId LastPolicyId { get; private set; }
            public bool TryResolve(
                StrongboxDefinition definition,
                StrongboxInstanceContext boxContext,
                RewardOperationRequest operation,
                RewardGrant equipmentGrant,
                out IReadOnlyList<EquipmentInstance> equipmentInstances,
                out string rejectionCode)
            {
                LastPolicyId = definition.CompatibleGenerationPolicyStableId;
                List<EquipmentInstance> values = new List<EquipmentInstance>();
                for (long unit = 0L; unit < equipmentGrant.Quantity; unit++)
                {
                    LastInstanceId = Strongbox.DeriveId(
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

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class RewardClaimPreparationAndPlanTests
    {
        [Test]
        public void MixedJournalBuildsOneExactMoneyScrapEquipmentAndStrongboxPlan()
        {
            Fixture fixture = Fixture.Create("mixed");
            EquipmentInstance equipment = fixture.Equipment("mixed-a", "shared");
            IReadOnlyList<RunSessionCollectedReward> journal = new[]
            {
                fixture.Reward("money", RewardGrantKind.Money,
                    MoneyWalletIds.CurrencyStableId, 25L, 1L),
                fixture.Reward("scrap", RewardGrantKind.Scrap,
                    fixture.Graph.ScrapWallet.CurrencyStableId, 9L, 2L),
                fixture.Reward("equipment", RewardGrantKind.EquipmentReference,
                    equipment.DefinitionId, 1L, 3L, equipment.InstanceId),
                fixture.Reward("box", RewardGrantKind.Strongbox,
                    fixture.StrongboxTier, 1L, 4L),
            };
            fixture.Payloads.Add(equipment);

            RewardClaimPreparedTransfer awaiting =
                fixture.CreateAwaiting(journal);
            RewardClaimAtomicPlan plan =
                fixture.AcceptAndBuild(awaiting);

            Assert.That(awaiting.Rewards.Count, Is.EqualTo(4));
            Assert.That(awaiting.Equipment.Single().InstanceId,
                Is.EqualTo(equipment.InstanceId));
            Assert.That(awaiting.Strongboxes.Count, Is.EqualTo(1));
            Assert.That(plan.Payloads.Count, Is.EqualTo(4));
            Assert.That(plan.StrongboxContexts.Count, Is.EqualTo(1));
            Assert.That(plan.Rewards.Select(item => item.RewardKind),
                Is.EquivalentTo(new[]
                {
                    RewardGrantKind.Money,
                    RewardGrantKind.Scrap,
                    RewardGrantKind.EquipmentReference,
                    RewardGrantKind.Strongbox,
                }));
            Assert.That(plan.Fingerprint,
                Is.EqualTo(plan.PreparedTransfer.ApplicationPlanFingerprint));
        }

        [Test]
        public void RewardsAbsentFromCollectedJournalAreExcluded()
        {
            Fixture fixture = Fixture.Create("uncollected");
            RunSessionCollectedReward collected = fixture.Reward(
                "collected",
                RewardGrantKind.Money,
                MoneyWalletIds.CurrencyStableId,
                7L,
                1L);
            RunSessionCollectedReward uncollected = fixture.Reward(
                "left-on-floor",
                RewardGrantKind.Money,
                MoneyWalletIds.CurrencyStableId,
                99L,
                2L);

            RewardClaimPreparedTransfer awaiting =
                fixture.CreateAwaiting(new[] { collected });
            RewardClaimAtomicPlan plan =
                fixture.AcceptAndBuild(awaiting);

            Assert.That(plan.Rewards.Count, Is.EqualTo(1));
            Assert.That(plan.Rewards[0].RewardInstanceStableId,
                Is.EqualTo(collected.GeneratedRewardChildStableId));
            Assert.That(plan.Rewards.Any(item =>
                item.RewardInstanceStableId
                    == uncollected.GeneratedRewardChildStableId), Is.False);
        }

        [Test]
        public void WrongRunOrLifecycleIsRejectedBeforeAcceptedEnd()
        {
            Fixture fixture = Fixture.Create("wrong-run");
            RunSessionCollectedReward wrongRun = fixture.Reward(
                "wrong-run",
                RewardGrantKind.Money,
                MoneyWalletIds.CurrencyStableId,
                1L,
                1L,
                runOverride: Id("run-instance.somewhere-else"));
            RunSessionCollectedReward wrongLifecycle = fixture.Reward(
                "wrong-lifecycle",
                RewardGrantKind.Money,
                MoneyWalletIds.CurrencyStableId,
                1L,
                2L,
                lifecycleOverride: fixture.Lifecycle + 1L);

            string wrongRunDiagnostic = fixture.TryCreateAwaiting(
                new[] { wrongRun },
                out _);
            string wrongLifecycleDiagnostic = fixture.TryCreateAwaiting(
                new[] { wrongLifecycle },
                out _);

            Assert.That(wrongRunDiagnostic,
                Is.EqualTo(
                    "collected-run-transfer-preparation-journal-run-or-lifecycle-mismatch"));
            Assert.That(wrongLifecycleDiagnostic,
                Is.EqualTo(
                    "collected-run-transfer-preparation-journal-run-or-lifecycle-mismatch"));
        }

        [Test]
        public void UnsupportedRewardKindIsRejectedBeforeAcceptedEnd()
        {
            Fixture fixture = Fixture.Create("unsupported");
            RunSessionCollectedReward unsupported = fixture.Reward(
                "misc",
                RewardGrantKind.Miscellaneous,
                Id("misc.future-widget"),
                1L,
                1L);

            string diagnostic = fixture.TryCreateAwaiting(
                new[] { unsupported },
                out _);

            Assert.That(diagnostic,
                Does.StartWith(
                    "collected-run-transfer-reward-kind-unsupported:"));
        }

        [Test]
        public void MissingEquipmentPayloadIsRejectedBeforeAcceptedEnd()
        {
            Fixture fixture = Fixture.Create("equipment-missing");
            EquipmentInstance equipment = fixture.Equipment("missing", "shared");
            RunSessionCollectedReward reward = fixture.Reward(
                "equipment-missing",
                RewardGrantKind.EquipmentReference,
                equipment.DefinitionId,
                1L,
                1L,
                equipment.InstanceId);

            string diagnostic = fixture.TryCreateAwaiting(
                new[] { reward },
                out _);

            Assert.That(diagnostic,
                Does.StartWith(
                    "fixture-equipment-payload-missing:"));
        }

        [Test]
        public void EquipmentDefinitionMismatchIsRejectedBeforeAcceptedEnd()
        {
            Fixture fixture = Fixture.Create("equipment-mismatch");
            EquipmentInstance retained = fixture.Equipment(
                "mismatch",
                "wrong-definition");
            fixture.Payloads.Add(retained);
            RunSessionCollectedReward reward = fixture.Reward(
                "equipment-mismatch",
                RewardGrantKind.EquipmentReference,
                Id("equipment-definition.expected"),
                1L,
                1L,
                retained.InstanceId);

            string diagnostic = fixture.TryCreateAwaiting(
                new[] { reward },
                out _);

            Assert.That(diagnostic,
                Does.StartWith(
                    "fixture-equipment-definition-mismatch:"));
        }

        [Test]
        public void IdenticalEquipmentDefinitionsRetainSeparateInstanceIdentities()
        {
            Fixture fixture = Fixture.Create("duplicate-equipment-definition");
            EquipmentInstance first = fixture.Equipment("first", "shared");
            EquipmentInstance second = fixture.Equipment("second", "shared");
            fixture.Payloads.Add(first);
            fixture.Payloads.Add(second);
            IReadOnlyList<RunSessionCollectedReward> journal = new[]
            {
                fixture.Reward("first", RewardGrantKind.EquipmentReference,
                    first.DefinitionId, 1L, 1L, first.InstanceId),
                fixture.Reward("second", RewardGrantKind.EquipmentReference,
                    second.DefinitionId, 1L, 2L, second.InstanceId),
            };

            RewardClaimPreparedTransfer awaiting =
                fixture.CreateAwaiting(journal);
            RewardClaimAtomicPlan plan =
                fixture.AcceptAndBuild(awaiting);

            Assert.That(awaiting.Equipment.Select(item => item.DefinitionId)
                .Distinct().Single(), Is.EqualTo(first.DefinitionId));
            Assert.That(awaiting.Equipment.Select(item => item.InstanceId)
                .Distinct().Count(), Is.EqualTo(2));
            Assert.That(plan.Payloads.SelectMany(item => item.EquipmentInstances)
                .Select(item => item.InstanceId).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void EqualTierStrongboxesRetainSeparateInstanceIdentities()
        {
            Fixture fixture = Fixture.Create("duplicate-box-tier");
            IReadOnlyList<RunSessionCollectedReward> journal = new[]
            {
                fixture.Reward("box-first", RewardGrantKind.Strongbox,
                    fixture.StrongboxTier, 1L, 1L),
                fixture.Reward("box-second", RewardGrantKind.Strongbox,
                    fixture.StrongboxTier, 1L, 2L),
            };

            RewardClaimPreparedTransfer awaiting =
                fixture.CreateAwaiting(journal);
            RewardClaimAtomicPlan plan =
                fixture.AcceptAndBuild(awaiting);

            Assert.That(awaiting.Strongboxes.Select(item => item.TierStableId)
                .Distinct().Single(), Is.EqualTo(fixture.StrongboxTier));
            Assert.That(awaiting.Strongboxes.Select(item => item.InstanceStableId)
                .Distinct().Count(), Is.EqualTo(2));
            Assert.That(plan.StrongboxContexts.Select(item => item.InstanceStableId)
                .Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void ReorderedJournalProducesSameBatchFingerprint()
        {
            Fixture fixture = Fixture.Create("canonical-journal");
            EquipmentInstance equipment = fixture.Equipment("canonical", "shared");
            fixture.Payloads.Add(equipment);
            var journal = new List<RunSessionCollectedReward>
            {
                fixture.Reward("money", RewardGrantKind.Money,
                    MoneyWalletIds.CurrencyStableId, 3L, 1L),
                fixture.Reward("equipment", RewardGrantKind.EquipmentReference,
                    equipment.DefinitionId, 1L, 2L, equipment.InstanceId),
                fixture.Reward("box", RewardGrantKind.Strongbox,
                    fixture.StrongboxTier, 1L, 3L),
            };

            RewardClaimAtomicPlan forward = fixture.AcceptAndBuild(
                fixture.CreateAwaiting(journal));
            journal.Reverse();
            RewardClaimAtomicPlan reversed = fixture.AcceptAndBuild(
                fixture.CreateAwaiting(journal));

            Assert.That(reversed.BatchFingerprint,
                Is.EqualTo(forward.BatchFingerprint));
            Assert.That(reversed.Fingerprint, Is.EqualTo(forward.Fingerprint));
        }

        [Test]
        public void RecoveryReconstructionMatchesOriginalPlanFingerprint()
        {
            Fixture fixture = Fixture.Create("recovery-plan");
            EquipmentInstance equipment = fixture.Equipment("recovery", "shared");
            fixture.Payloads.Add(equipment);
            RewardClaimPreparedTransfer awaiting =
                fixture.CreateAwaiting(new[]
                {
                    fixture.Reward("equipment",
                        RewardGrantKind.EquipmentReference,
                        equipment.DefinitionId,
                        1L,
                        1L,
                        equipment.InstanceId),
                    fixture.Reward("box",
                        RewardGrantKind.Strongbox,
                        fixture.StrongboxTier,
                        1L,
                        2L),
                });
            RewardClaimPreparedTransfer prepared;
            RewardClaimAtomicPlan original = fixture.AcceptAndBuild(
                awaiting,
                out prepared);

            RewardClaimAtomicPlan rebuilt;
            string diagnostic;
            bool accepted = RewardClaimTransferPreparationFactory
                .TryBuildPlanFromPrepared(
                    prepared,
                    fixture.Graph,
                    fixture.RewardApplication,
                    out rebuilt,
                    out diagnostic);

            Assert.That(accepted, Is.True, diagnostic);
            Assert.That(rebuilt, Is.Not.Null);
            Assert.That(rebuilt.BatchFingerprint,
                Is.EqualTo(original.BatchFingerprint));
            Assert.That(rebuilt.Fingerprint, Is.EqualTo(original.Fingerprint));
            Assert.That(rebuilt.PreparedTransfer.Fingerprint,
                Is.EqualTo(prepared.Fingerprint));
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private static string Fingerprint(string material)
        {
            return Strongbox.Fingerprint(material);
        }

        private sealed class Fixture
        {
            private Fixture(
                string suffix,
                CharacterLiveGraph graph,
                RewardApplicationActions rewardApplication)
            {
                Suffix = suffix;
                Graph = graph;
                RewardApplication = rewardApplication;
                Receipts = new RewardClaimTransferReceiptState();
                PreparedTransfers =
                    new RewardClaimPreparedTransferStore();
                Payloads = new EquipmentPayloadSource();
                RunStableId = Id("run-instance." + suffix);
                Lifecycle = 1L;
                EndCommand = new EndRunSessionCommand(
                    Id("operation.end-" + suffix),
                    RunStableId,
                    Lifecycle,
                    MissionRunCompletionState.Completed,
                    100L);
                GenerationContext = new RewardClaimGenerationContext(
                    0xC0FFEEUL,
                    2,
                    ProgressionContext.Create(
                        20,
                        17,
                        Id("difficulty.veteran"),
                        3,
                        new[] { Id("progression-tag.campaign") }),
                    Fingerprint("event-modifiers-" + suffix));
                StrongboxTier = graph.StrongboxCatalog.Definitions[0]
                    .TierStableId;
            }

            public string Suffix { get; }
            public CharacterLiveGraph Graph { get; }
            public RewardApplicationActions RewardApplication { get; }
            public RewardClaimTransferReceiptState Receipts { get; }
            public RewardClaimPreparedTransferStore PreparedTransfers { get; }
            public EquipmentPayloadSource Payloads { get; }
            public StableId RunStableId { get; }
            public long Lifecycle { get; }
            public EndRunSessionCommand EndCommand { get; }
            public RewardClaimGenerationContext GenerationContext { get; }
            public StableId StrongboxTier { get; }

            public static Fixture Create(string suffix)
            {
                StableId characterId = Id("character-instance." + suffix);
                StableId classId = Id("loadout-profile.striker");
                PlayerRouteProfilePayload route =
                    PlayerRouteProfilePayload.Create(
                        characterId,
                        classId,
                        new StableId[] { null, null, null, null });
                CharacterLiveGraphFactory factory =
                    CharacterLiveGraphFactory
                        .CreateVerticalSliceDefaults();
                var graph = (CharacterLiveGraph)
                    factory.CreateStarter(
                        0,
                        characterId,
                        classId,
                        "Preparation Pilot " + suffix,
                        route);
                var rewardApplication = new RewardApplicationActions(
                    Id("authority.reward-application-" + suffix),
                    new MoneyRewardChildState(graph.MoneyWallet),
                    new ScrapRewardChildState(graph.ScrapWallet),
                    new PlayerHoldingsRewardChildState(
                        graph.LoadoutRuntime.Holdings,
                        graph.LoadoutRuntime.CatalogBridge));
                return new Fixture(suffix, graph, rewardApplication);
            }

            public EquipmentInstance Equipment(
                string instanceSuffix,
                string definitionSuffix)
            {
                return EquipmentInstance.Create(
                    Id("equipment-instance." + Suffix + "-" + instanceSuffix),
                    Id("equipment-definition." + definitionSuffix),
                    20,
                    Id("quality.epic"),
                    new[]
                    {
                        AugmentInstance.Create(
                            Id("augment-instance." + Suffix + "-" + instanceSuffix),
                            Id("augment-definition.damage"),
                            2,
                            11),
                    });
            }

            public RunSessionCollectedReward Reward(
                string rewardSuffix,
                RewardGrantKind kind,
                StableId content,
                long quantity,
                long collectionOrder,
                StableId exactInstance = null,
                StableId runOverride = null,
                long? lifecycleOverride = null)
            {
                StableId child = exactInstance
                    ?? Id("reward-instance." + Suffix + "-" + rewardSuffix);
                return new RunSessionCollectedReward(
                    Id("pickup." + Suffix + "-" + rewardSuffix),
                    child,
                    Id("grant." + Suffix + "-" + rewardSuffix),
                    Id("operation.drop-" + Suffix + "-" + rewardSuffix),
                    Id("terminal-event." + Suffix + "-" + rewardSuffix),
                    null,
                    runOverride ?? RunStableId,
                    lifecycleOverride ?? Lifecycle,
                    Id("source-entity." + Suffix + "-" + rewardSuffix),
                    Id("source-placement." + Suffix + "-" + rewardSuffix),
                    1L,
                    Id("source-definition." + Suffix + "-" + rewardSuffix),
                    Id("participant." + Suffix),
                    kind,
                    content,
                    quantity,
                    Fingerprint("generated-batch-" + Suffix),
                    Fingerprint("generated-reward-" + Suffix + "-" + rewardSuffix),
                    Id("room." + Suffix),
                    2.5d,
                    -4d,
                    Fingerprint("spawn-" + Suffix + "-" + rewardSuffix),
                    Fingerprint("available-" + Suffix + "-" + rewardSuffix),
                    Id("collector-entity." + Suffix),
                    Id("participant." + Suffix),
                    Id("operation.collect-" + Suffix + "-" + rewardSuffix),
                    collectionOrder,
                    70L + collectionOrder);
            }

            public RewardClaimPreparedTransfer CreateAwaiting(
                IReadOnlyList<RunSessionCollectedReward> journal)
            {
                RewardClaimPreparedTransfer awaiting;
                string diagnostic = TryCreateAwaiting(journal, out awaiting);
                Assert.That(awaiting, Is.Not.Null, diagnostic);
                Assert.That(diagnostic, Is.Empty);
                return awaiting;
            }

            public string TryCreateAwaiting(
                IReadOnlyList<RunSessionCollectedReward> journal,
                out RewardClaimPreparedTransfer awaiting)
            {
                string diagnostic;
                bool accepted = RewardClaimTransferPreparationFactory
                    .TryCreateAwaitingAcceptedEnd(
                        EndCommand,
                        journal,
                        Graph,
                        RewardApplication,
                        Receipts,
                        PreparedTransfers,
                        GenerationContext,
                        Payloads,
                        out awaiting,
                        out diagnostic);
                if (accepted)
                {
                    Assert.That(awaiting, Is.Not.Null);
                    return string.Empty;
                }
                Assert.That(awaiting, Is.Null);
                return diagnostic;
            }

            public RewardClaimAtomicPlan AcceptAndBuild(
                RewardClaimPreparedTransfer awaiting)
            {
                RewardClaimPreparedTransfer prepared;
                return AcceptAndBuild(awaiting, out prepared);
            }

            public RewardClaimAtomicPlan AcceptAndBuild(
                RewardClaimPreparedTransfer awaiting,
                out RewardClaimPreparedTransfer prepared)
            {
                RunSessionEndResult acceptedEnd = AcceptedEnd();
                RewardClaimAtomicPlan plan;
                string diagnostic;
                bool accepted = RewardClaimTransferPreparationFactory
                    .TryAcceptEndAndBuildPlan(
                        acceptedEnd,
                        awaiting,
                        Graph,
                        RewardApplication,
                        out prepared,
                        out plan,
                        out diagnostic);
                Assert.That(accepted, Is.True, diagnostic);
                Assert.That(prepared, Is.Not.Null);
                Assert.That(plan, Is.Not.Null);
                return plan;
            }

            private RunSessionEndResult AcceptedEnd()
            {
                MissionResultPayload mission = MissionResultPayload.Create(
                    RunStableId,
                    Graph.RoutePayload,
                    MissionRunCompletionState.Completed,
                    Array.Empty<MissionRunStrongboxResult>(),
                    1L,
                    Graph.LoadoutRuntime.Holdings.Sequence,
                    Graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint,
                    Graph.StrongboxAuthority.Sequence,
                    Graph.StrongboxAuthority.ExportSnapshot().Fingerprint);
                var local = new RunLocalStateSnapshot(
                    0L,
                    new Dictionary<string, long>(),
                    new Dictionary<string, long>(),
                    new Dictionary<string, long>());
                var receipt = new RunSessionEndReceipt(
                    RunStableId,
                    Graph.Character.CharacterInstanceStableId,
                    Graph.Character.Revision,
                    Graph.Character.Fingerprint,
                    Id("mission-layout.level-1"),
                    Id("difficulty.normal"),
                    42L,
                    Fingerprint("frozen-inputs-" + Suffix),
                    Fingerprint("combat-profile-" + Suffix),
                    local,
                    mission);
                return new RunSessionEndResult(
                    RunSessionEndStatus.Ended,
                    EndCommand,
                    receipt,
                    string.Empty);
            }
        }

        private sealed class EquipmentPayloadSource :
            ICollectedRunGunPayloadSource
        {
            private readonly Dictionary<StableId, EquipmentInstance> equipment =
                new Dictionary<StableId, EquipmentInstance>();

            public void Add(EquipmentInstance value)
            {
                equipment.Add(value.InstanceId, value);
            }

            public bool TryResolveExact(
                StableId rewardInstanceStableId,
                StableId equipmentDefinitionStableId,
                out EquipmentInstance value,
                out string diagnostic)
            {
                if (!equipment.TryGetValue(rewardInstanceStableId, out value))
                {
                    diagnostic =
                        "fixture-equipment-payload-missing:"
                        + rewardInstanceStableId;
                    return false;
                }
                if (value.DefinitionId != equipmentDefinitionStableId)
                {
                    diagnostic =
                        "fixture-equipment-definition-mismatch:"
                        + rewardInstanceStableId;
                    return false;
                }
                diagnostic = string.Empty;
                return true;
            }
        }
    }
}

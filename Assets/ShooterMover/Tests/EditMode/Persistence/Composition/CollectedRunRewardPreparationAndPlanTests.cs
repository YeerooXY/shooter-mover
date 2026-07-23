using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CollectedRunRewardPreparationAndPlanTests
    {
        [Test]
        public void MixedJournalBuildsOneExactMoneyScrapEquipmentAndStrongboxPlan()
        {
            Fixture fixture = Fixture.Create("mixed");
            EquipmentInstance equipment = fixture.Equipment("mixed-a", "shared");
            IReadOnlyList<RunSessionCollectedRewardV1> journal = new[]
            {
                fixture.Reward("money", RewardGrantKindV1.Money,
                    MoneyWalletIdsV1.CurrencyStableId, 25L, 1L),
                fixture.Reward("scrap", RewardGrantKindV1.Scrap,
                    fixture.Graph.ScrapWallet.CurrencyStableId, 9L, 2L),
                fixture.Reward("equipment", RewardGrantKindV1.EquipmentReference,
                    equipment.DefinitionId, 1L, 3L, equipment.InstanceId),
                fixture.Reward("box", RewardGrantKindV1.Strongbox,
                    fixture.StrongboxTier, 1L, 4L),
            };
            fixture.Payloads.Add(equipment);

            CollectedRunRewardPreparedTransferV1 awaiting =
                fixture.CreateAwaiting(journal);
            CollectedRunRewardAtomicPlanV2 plan =
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
                    RewardGrantKindV1.Money,
                    RewardGrantKindV1.Scrap,
                    RewardGrantKindV1.EquipmentReference,
                    RewardGrantKindV1.Strongbox,
                }));
            Assert.That(plan.Fingerprint,
                Is.EqualTo(plan.PreparedTransfer.ApplicationPlanFingerprint));
        }

        [Test]
        public void RewardsAbsentFromCollectedJournalAreExcluded()
        {
            Fixture fixture = Fixture.Create("uncollected");
            RunSessionCollectedRewardV1 collected = fixture.Reward(
                "collected",
                RewardGrantKindV1.Money,
                MoneyWalletIdsV1.CurrencyStableId,
                7L,
                1L);
            RunSessionCollectedRewardV1 uncollected = fixture.Reward(
                "left-on-floor",
                RewardGrantKindV1.Money,
                MoneyWalletIdsV1.CurrencyStableId,
                99L,
                2L);

            CollectedRunRewardPreparedTransferV1 awaiting =
                fixture.CreateAwaiting(new[] { collected });
            CollectedRunRewardAtomicPlanV2 plan =
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
            RunSessionCollectedRewardV1 wrongRun = fixture.Reward(
                "wrong-run",
                RewardGrantKindV1.Money,
                MoneyWalletIdsV1.CurrencyStableId,
                1L,
                1L,
                runOverride: Id("run-instance.somewhere-else"));
            RunSessionCollectedRewardV1 wrongLifecycle = fixture.Reward(
                "wrong-lifecycle",
                RewardGrantKindV1.Money,
                MoneyWalletIdsV1.CurrencyStableId,
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
            RunSessionCollectedRewardV1 unsupported = fixture.Reward(
                "misc",
                RewardGrantKindV1.Miscellaneous,
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
            RunSessionCollectedRewardV1 reward = fixture.Reward(
                "equipment-missing",
                RewardGrantKindV1.EquipmentReference,
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
            RunSessionCollectedRewardV1 reward = fixture.Reward(
                "equipment-mismatch",
                RewardGrantKindV1.EquipmentReference,
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
            IReadOnlyList<RunSessionCollectedRewardV1> journal = new[]
            {
                fixture.Reward("first", RewardGrantKindV1.EquipmentReference,
                    first.DefinitionId, 1L, 1L, first.InstanceId),
                fixture.Reward("second", RewardGrantKindV1.EquipmentReference,
                    second.DefinitionId, 1L, 2L, second.InstanceId),
            };

            CollectedRunRewardPreparedTransferV1 awaiting =
                fixture.CreateAwaiting(journal);
            CollectedRunRewardAtomicPlanV2 plan =
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
            IReadOnlyList<RunSessionCollectedRewardV1> journal = new[]
            {
                fixture.Reward("box-first", RewardGrantKindV1.Strongbox,
                    fixture.StrongboxTier, 1L, 1L),
                fixture.Reward("box-second", RewardGrantKindV1.Strongbox,
                    fixture.StrongboxTier, 1L, 2L),
            };

            CollectedRunRewardPreparedTransferV1 awaiting =
                fixture.CreateAwaiting(journal);
            CollectedRunRewardAtomicPlanV2 plan =
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
            var journal = new List<RunSessionCollectedRewardV1>
            {
                fixture.Reward("money", RewardGrantKindV1.Money,
                    MoneyWalletIdsV1.CurrencyStableId, 3L, 1L),
                fixture.Reward("equipment", RewardGrantKindV1.EquipmentReference,
                    equipment.DefinitionId, 1L, 2L, equipment.InstanceId),
                fixture.Reward("box", RewardGrantKindV1.Strongbox,
                    fixture.StrongboxTier, 1L, 3L),
            };

            CollectedRunRewardAtomicPlanV2 forward = fixture.AcceptAndBuild(
                fixture.CreateAwaiting(journal));
            journal.Reverse();
            CollectedRunRewardAtomicPlanV2 reversed = fixture.AcceptAndBuild(
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
            CollectedRunRewardPreparedTransferV1 awaiting =
                fixture.CreateAwaiting(new[]
                {
                    fixture.Reward("equipment",
                        RewardGrantKindV1.EquipmentReference,
                        equipment.DefinitionId,
                        1L,
                        1L,
                        equipment.InstanceId),
                    fixture.Reward("box",
                        RewardGrantKindV1.Strongbox,
                        fixture.StrongboxTier,
                        1L,
                        2L),
                });
            CollectedRunRewardPreparedTransferV1 prepared;
            CollectedRunRewardAtomicPlanV2 original = fixture.AcceptAndBuild(
                awaiting,
                out prepared);

            CollectedRunRewardAtomicPlanV2 rebuilt;
            string diagnostic;
            bool accepted = CollectedRunRewardTransferPreparationFactoryV2
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
            return StrongboxCanonicalV1.Fingerprint(material);
        }

        private sealed class Fixture
        {
            private Fixture(
                string suffix,
                ProductionCharacterRuntimeGraphV1 graph,
                RewardApplicationServiceV1 rewardApplication)
            {
                Suffix = suffix;
                Graph = graph;
                RewardApplication = rewardApplication;
                Receipts = new CollectedRunRewardTransferReceiptAuthorityV1();
                PreparedTransfers =
                    new CollectedRunRewardPreparedTransferAuthorityV1();
                Payloads = new EquipmentPayloadSource();
                RunStableId = Id("run-instance." + suffix);
                Lifecycle = 1L;
                EndCommand = new EndRunSessionCommandV1(
                    Id("operation.end-" + suffix),
                    RunStableId,
                    Lifecycle,
                    MissionRunCompletionStateV1.Completed,
                    100L);
                GenerationContext = new CollectedRunRewardGenerationContextV2(
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
            public ProductionCharacterRuntimeGraphV1 Graph { get; }
            public RewardApplicationServiceV1 RewardApplication { get; }
            public CollectedRunRewardTransferReceiptAuthorityV1 Receipts { get; }
            public CollectedRunRewardPreparedTransferAuthorityV1 PreparedTransfers { get; }
            public EquipmentPayloadSource Payloads { get; }
            public StableId RunStableId { get; }
            public long Lifecycle { get; }
            public EndRunSessionCommandV1 EndCommand { get; }
            public CollectedRunRewardGenerationContextV2 GenerationContext { get; }
            public StableId StrongboxTier { get; }

            public static Fixture Create(string suffix)
            {
                StableId characterId = Id("character-instance." + suffix);
                StableId classId = Id("loadout-profile.striker");
                PlayerRouteProfilePayloadV1 route =
                    PlayerRouteProfilePayloadV1.Create(
                        characterId,
                        classId,
                        new StableId[] { null, null, null, null });
                ProductionCharacterRuntimeGraphFactoryV1 factory =
                    ProductionCharacterRuntimeGraphFactoryV1
                        .CreateVerticalSliceDefaults();
                var graph = (ProductionCharacterRuntimeGraphV1)
                    factory.CreateStarter(
                        0,
                        characterId,
                        classId,
                        "Preparation Pilot " + suffix,
                        route);
                var rewardApplication = new RewardApplicationServiceV1(
                    Id("authority.reward-application-" + suffix),
                    new MoneyRewardChildAuthorityV1(graph.MoneyWallet),
                    new ScrapRewardChildAuthorityV1(graph.ScrapWallet),
                    new PlayerHoldingsRewardChildAuthorityV1(
                        graph.LoadoutRuntime.Holdings,
                        graph.LoadoutRuntime.CatalogAdapter));
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

            public RunSessionCollectedRewardV1 Reward(
                string rewardSuffix,
                RewardGrantKindV1 kind,
                StableId content,
                long quantity,
                long collectionOrder,
                StableId exactInstance = null,
                StableId runOverride = null,
                long? lifecycleOverride = null)
            {
                StableId child = exactInstance
                    ?? Id("reward-instance." + Suffix + "-" + rewardSuffix);
                return new RunSessionCollectedRewardV1(
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

            public CollectedRunRewardPreparedTransferV1 CreateAwaiting(
                IReadOnlyList<RunSessionCollectedRewardV1> journal)
            {
                CollectedRunRewardPreparedTransferV1 awaiting;
                string diagnostic = TryCreateAwaiting(journal, out awaiting);
                Assert.That(awaiting, Is.Not.Null, diagnostic);
                Assert.That(diagnostic, Is.Empty);
                return awaiting;
            }

            public string TryCreateAwaiting(
                IReadOnlyList<RunSessionCollectedRewardV1> journal,
                out CollectedRunRewardPreparedTransferV1 awaiting)
            {
                string diagnostic;
                bool accepted = CollectedRunRewardTransferPreparationFactoryV2
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

            public CollectedRunRewardAtomicPlanV2 AcceptAndBuild(
                CollectedRunRewardPreparedTransferV1 awaiting)
            {
                CollectedRunRewardPreparedTransferV1 prepared;
                return AcceptAndBuild(awaiting, out prepared);
            }

            public CollectedRunRewardAtomicPlanV2 AcceptAndBuild(
                CollectedRunRewardPreparedTransferV1 awaiting,
                out CollectedRunRewardPreparedTransferV1 prepared)
            {
                RunSessionEndResultV1 acceptedEnd = AcceptedEnd();
                CollectedRunRewardAtomicPlanV2 plan;
                string diagnostic;
                bool accepted = CollectedRunRewardTransferPreparationFactoryV2
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

            private RunSessionEndResultV1 AcceptedEnd()
            {
                MissionResultPayloadV1 mission = MissionResultPayloadV1.Create(
                    RunStableId,
                    Graph.RoutePayload,
                    MissionRunCompletionStateV1.Completed,
                    Array.Empty<MissionRunStrongboxResultV1>(),
                    1L,
                    Graph.LoadoutRuntime.Holdings.Sequence,
                    Graph.LoadoutRuntime.Holdings.ExportSnapshot().Fingerprint,
                    Graph.StrongboxAuthority.Sequence,
                    Graph.StrongboxAuthority.ExportSnapshot().Fingerprint);
                var local = new RunLocalStateSnapshotV1(
                    0L,
                    new Dictionary<string, long>(),
                    new Dictionary<string, long>(),
                    new Dictionary<string, long>());
                var receipt = new RunSessionEndReceiptV1(
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
                return new RunSessionEndResultV1(
                    RunSessionEndStatusV1.Ended,
                    EndCommand,
                    receipt,
                    string.Empty);
            }
        }

        private sealed class EquipmentPayloadSource :
            ICollectedRunEquipmentPayloadSourceV2
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

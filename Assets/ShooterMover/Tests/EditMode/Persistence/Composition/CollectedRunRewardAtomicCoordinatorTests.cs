using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CollectedRunRewardAtomicCoordinatorTests
    {
        [Test]
        public void FirstApplicationSucceeds()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("first");
            var authority = new FakeAtomicAuthority();
            var persistence = new FakePersistence();
            var coordinator = new CollectedRunRewardTransferCoordinatorV2(
                authority,
                persistence);

            CollectedRunRewardTransferResultV1 result = coordinator.Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.Applied));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Receipt, Is.Not.Null);
            Assert.That(result.Receipt.AppliedRewardStableIds,
                Is.EquivalentTo(plan.Rewards.Select(
                    item => item.RewardInstanceStableId)));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RecordCallCount, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.Zero);
            Assert.That(persistence.PreparedCallCount, Is.EqualTo(1));
            Assert.That(persistence.FinalCallCount, Is.EqualTo(1));
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void ExactReplayGrantsNothingTwice()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("replay");
            var authority = new FakeAtomicAuthority();
            var persistence = new FakePersistence();
            var coordinator = new CollectedRunRewardTransferCoordinatorV2(
                authority,
                persistence);

            CollectedRunRewardTransferResultV1 first = coordinator.Apply(plan);
            int mutations = authority.LiveMutationVersion;
            CollectedRunRewardTransferResultV1 replay = coordinator.Apply(plan);

            Assert.That(first.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.ExactReplay));
            Assert.That(replay.Receipt.Fingerprint,
                Is.EqualTo(first.Receipt.Fingerprint));
            Assert.That(authority.LiveMutationVersion, Is.EqualTo(mutations));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RecordCallCount, Is.EqualTo(1));
            Assert.That(persistence.PreparedCallCount, Is.EqualTo(1));
            Assert.That(persistence.FinalCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ExistingOperationWithDifferentPlanRejectsAsConflict()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("operation-conflict");
            var authority = new FakeAtomicAuthority();
            authority.SeedReceipt(ReceiptFor(
                plan,
                Fingerprint("different-batch"),
                Fingerprint("different-plan")));
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatusV1.ConflictingDuplicate));
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(persistence.PreparedCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void PartialRewardOverlapRejectsBeforePersistenceOrMutation()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("overlap");
            var authority = new FakeAtomicAuthority();
            authority.SeedReceipt(ReceiptForOtherOperation(plan));
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatusV1.ConflictingDuplicate));
            Assert.That(result.Diagnostic,
                Does.StartWith(
                    "collected-run-transfer-partial-or-cross-operation-overlap:"));
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(persistence.PreparedCallCount, Is.Zero);
        }

        [TestCase("rap-commit-failed")]
        [TestCase("rap-claim-failed")]
        [TestCase("strongbox-registration-failed")]
        public void AtomicSubsystemFailureCompensatesAllLiveMutation(
            string diagnostic)
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan(diagnostic);
            var authority = new FakeAtomicAuthority
            {
                ApplyStatus = CollectedRunRewardTransferAuthorityStatusV1.Rejected,
                ApplyDiagnostic = diagnostic,
                MutateBeforeRejectedApply = true,
            };
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.Rejected));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(persistence.FinalCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.True);
        }

        [Test]
        public void ReceiptFailureCompensatesAppliedAuthorities()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("receipt-failure");
            var authority = new FakeAtomicAuthority
            {
                RecordStatus =
                    CollectedRunRewardTransferAuthorityStatusV1.Rejected,
                RecordDiagnostic = "fixture-receipt-rejected",
            };
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.Rejected));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RecordCallCount, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(persistence.FinalCallCount, Is.Zero);
        }

        [Test]
        public void SafeFinalPersistenceRejectionCompensates()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("safe-final-reject");
            var authority = new FakeAtomicAuthority();
            var persistence = new FakePersistence
            {
                FinalResult = RejectedBeforeReplacement(
                    "fixture-final-before-replacement"),
            };

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatusV1.Rejected));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(result.Persistence.RejectedBeforeReplacement, Is.True);
            Assert.That(result.ExactRetryAllowed, Is.True);
        }

        [Test]
        public void CompensationFailureBecomesFatal()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("restore-failure");
            var authority = new FakeAtomicAuthority
            {
                ApplyStatus = CollectedRunRewardTransferAuthorityStatusV1.Rejected,
                ApplyDiagnostic = "fixture-apply-rejected",
                MutateBeforeRejectedApply = true,
                RestoreSucceeds = false,
            };
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatusV1
                        .FatalCompensationFailure));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.EqualTo(1));
            Assert.That(result.CompensationDiagnostic,
                Is.EqualTo("fixture-restore-failed"));
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PreparedPersistenceThrowOrNullIsFatalUncertainty(
            bool throwInsteadOfNull)
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan(
                throwInsteadOfNull ? "prepared-throw" : "prepared-null");
            var authority = new FakeAtomicAuthority();
            var persistence = new FakePersistence
            {
                ThrowPrepared = throwInsteadOfNull,
                ReturnNullPrepared = !throwInsteadOfNull,
            };

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatusV1
                        .FatalCompensationFailure));
            Assert.That(result.Persistence.DurableStateUncertain, Is.True);
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(authority.RestoreCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void UncertainFinalPersistencePerformsNoLiveRollback()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("uncertain-final");
            var authority = new FakeAtomicAuthority();
            var persistence = new FakePersistence
            {
                FinalResult = Uncertain("fixture-final-uncertain"),
            };

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatusV1
                        .FatalCompensationFailure));
            Assert.That(result.Persistence.DurableStateUncertain, Is.True);
            Assert.That(authority.LiveMutationVersion, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.Zero);
            Assert.That(result.CompensationDiagnostic,
                Is.EqualTo("live-compensation-intentionally-not-attempted"));
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void UncertainPreparedPersistenceDisablesExactRetry()
        {
            CollectedRunRewardAtomicPlanV2 plan = BuildPlan("uncertain-prepared");
            var authority = new FakeAtomicAuthority();
            var persistence = new FakePersistence
            {
                PreparedResult = Uncertain("fixture-prepared-uncertain"),
            };

            CollectedRunRewardTransferResultV1 result =
                new CollectedRunRewardTransferCoordinatorV2(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Persistence.DurableStateUncertain, Is.True);
            Assert.That(result.ExactRetryAllowed, Is.False);
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(authority.RestoreCallCount, Is.Zero);
        }

        private static CollectedRunRewardAtomicPlanV2 BuildPlan(string suffix)
        {
            StableId run = Id("run-instance." + suffix);
            StableId character = Id("character-instance." + suffix);
            StableId rewardId = Id("reward-instance." + suffix);
            var reward = new CollectedRunRewardTransferItemV1(
                rewardId,
                RewardGrantKindV1.Money,
                MoneyWalletIdsV1.CurrencyStableId,
                5L,
                Id("pickup." + suffix),
                Id("grant." + suffix),
                Id("operation.drop-" + suffix),
                Id("terminal-event." + suffix),
                null,
                run,
                1L,
                Id("source-entity." + suffix),
                Id("source-placement." + suffix),
                1L,
                Id("source-definition." + suffix),
                Id("participant." + suffix),
                Fingerprint("generated-batch-" + suffix),
                Fingerprint("generated-reward-" + suffix),
                Id("room." + suffix),
                0d,
                0d,
                Fingerprint("spawn-" + suffix),
                Fingerprint("available-" + suffix),
                Id("collector-entity." + suffix),
                Id("collector-participant." + suffix),
                Id("operation.collect-" + suffix),
                1L,
                10L,
                Fingerprint("collected-" + suffix));
            ProgressionContext progression = ProgressionContext.Create(
                10,
                8,
                Id("difficulty.normal"),
                0,
                new[] { Id("progression-tag.campaign") });
            CollectedRunRewardPreparedTransferV1 awaiting =
                CollectedRunRewardPreparedTransferV1.AwaitingAcceptedEnd(
                    Id("custody." + suffix),
                    Id("operation.prepare-" + suffix),
                    run,
                    1L,
                    character,
                    2L,
                    Fingerprint("character-" + suffix),
                    Id("operation.end-" + suffix),
                    Fingerprint("end-command-" + suffix),
                    123UL,
                    1,
                    progression,
                    Fingerprint("event-" + suffix),
                    0L,
                    0L,
                    0L,
                    new Dictionary<string, string>
                    {
                        { "money", Fingerprint("money-" + suffix) },
                        { "scrap", Fingerprint("scrap-" + suffix) },
                        { "holdings", Fingerprint("holdings-" + suffix) },
                    },
                    new[] { reward },
                    Array.Empty<ShooterMover.Domain.Equipment.EquipmentInstance>(),
                    Array.Empty<ShooterMover.Domain.Rewards.Strongboxes.StrongboxInstanceContextV1>());
            StableId transferOperation = Id("operation.transfer-" + suffix);
            StableId missionResult = Id("mission-result." + suffix);
            string missionFingerprint = Fingerprint("mission-" + suffix);
            string batch = CollectedRunRewardAtomicPlanV2.ComputeBatchFingerprint(
                transferOperation,
                run,
                1L,
                missionResult,
                missionFingerprint,
                character,
                2L,
                awaiting.ExpectedCharacterFingerprint,
                awaiting.Rewards);
            RewardGrantV1 grant = RewardGrantV1.Create(
                rewardId,
                RewardGrantKindV1.Money,
                MoneyWalletIdsV1.CurrencyStableId,
                5L);
            RewardGrantApplicationPayloadV1 payload =
                RewardGrantApplicationPayloadV1.ForValue(grant);
            StableId commitment = Id("commitment." + suffix);
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                run,
                run,
                transferOperation,
                commitment,
                Id("reward-profile.collected-run-transfer"),
                batch);
            RewardCommitCommandV1 commit = RewardCommitCommandV1.Create(
                operation,
                RewardResultV1.CreateGrants(
                    commitment,
                    transferOperation,
                    new[] { grant }),
                Fingerprint("generation-" + suffix),
                new[] { payload });
            RewardClaimCommandV1 claim = RewardClaimCommandV1.Create(
                Id("claim." + suffix),
                commitment,
                character,
                MoneyWalletIdsV1.AuthorityStableId,
                Id("authority.scrap-" + suffix),
                Id("authority.holdings-" + suffix),
                0L,
                0L,
                0L);
            string planFingerprint =
                CollectedRunRewardAtomicPlanV2.ComputeFingerprint(
                    batch,
                    commit,
                    claim,
                    new[] { payload },
                    Array.Empty<StrongboxInstanceContextV1>());
            CollectedRunRewardPreparedTransferV1 prepared = awaiting.AcceptEnd(
                transferOperation,
                missionResult,
                missionFingerprint,
                batch,
                planFingerprint);
            return new CollectedRunRewardAtomicPlanV2(
                prepared,
                commit,
                claim,
                new[] { payload },
                Array.Empty<StrongboxInstanceContextV1>());
        }

        private static CollectedRunRewardTransferReceiptV1 ReceiptFor(
            CollectedRunRewardAtomicPlanV2 plan,
            string batchFingerprint,
            string planFingerprint)
        {
            return new CollectedRunRewardTransferReceiptV1(
                plan.TransferOperationStableId,
                batchFingerprint,
                plan.RunStableId,
                plan.PreparedTransfer.LifecycleGeneration,
                plan.PreparedTransfer.AcceptedMissionResultStableId,
                plan.PreparedTransfer.AcceptedMissionResultFingerprint,
                plan.SelectedCharacterStableId,
                plan.Rewards.Select(item => item.RewardInstanceStableId),
                new Dictionary<string, string>
                {
                    {
                        CollectedRunRewardTransferCoordinatorV2
                            .ApplicationPlanAuthorityKey,
                        planFingerprint
                    },
                });
        }

        private static CollectedRunRewardTransferReceiptV1
            ReceiptForOtherOperation(CollectedRunRewardAtomicPlanV2 plan)
        {
            return new CollectedRunRewardTransferReceiptV1(
                Id("operation.other-" + plan.RunStableId.Value),
                Fingerprint("other-batch"),
                plan.RunStableId,
                plan.PreparedTransfer.LifecycleGeneration,
                Id("mission-result.other-" + plan.RunStableId.Value),
                Fingerprint("other-mission"),
                plan.SelectedCharacterStableId,
                new[] { plan.Rewards[0].RewardInstanceStableId },
                new Dictionary<string, string>
                {
                    {
                        CollectedRunRewardTransferCoordinatorV2
                            .ApplicationPlanAuthorityKey,
                        Fingerprint("other-plan")
                    },
                });
        }

        private static CollectedRunRewardTransferPersistenceResultV1
            RejectedBeforeReplacement(string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1
                    .RejectedBeforeReplacement,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }

        private static CollectedRunRewardTransferPersistenceResultV1
            Uncertain(string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1
                    .DurableStateUncertain,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }

        private static CollectedRunRewardTransferPersistenceResultV1
            PreparedSuccess()
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1
                    .PreparedAndVerified,
                1L,
                Fingerprint("account-prepared"),
                1L,
                Fingerprint("character-prepared"),
                string.Empty);
        }

        private static CollectedRunRewardTransferPersistenceResultV1
            FinalSuccess()
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1
                    .PersistedAndVerified,
                2L,
                Fingerprint("account-final"),
                2L,
                Fingerprint("character-final"),
                string.Empty);
        }

        private static string Fingerprint(string material)
        {
            return StrongboxCanonicalV1.Fingerprint(material);
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private sealed class FakeCompensation :
            ICollectedRunRewardTransferCompensationV1
        {
            public FakeCompensation(int liveMutationVersion)
            {
                LiveMutationVersion = liveMutationVersion;
                Fingerprint = FingerprintFor(liveMutationVersion);
            }

            public int LiveMutationVersion { get; }
            public string Fingerprint { get; }

            private static string FingerprintFor(int value)
            {
                return StrongboxCanonicalV1.Fingerprint(
                    "compensation-" + value);
            }
        }

        private sealed class FakeAtomicAuthority :
            ICollectedRunRewardAtomicBatchAuthorityPortV1
        {
            private readonly Dictionary<StableId,
                CollectedRunRewardTransferReceiptV1> byOperation =
                    new Dictionary<StableId,
                        CollectedRunRewardTransferReceiptV1>();
            private readonly Dictionary<StableId,
                CollectedRunRewardTransferReceiptV1> byReward =
                    new Dictionary<StableId,
                        CollectedRunRewardTransferReceiptV1>();

            public CollectedRunRewardTransferAuthorityStatusV1 ApplyStatus
            {
                get;
                set;
            } = CollectedRunRewardTransferAuthorityStatusV1.Applied;
            public string ApplyDiagnostic { get; set; } = string.Empty;
            public bool MutateBeforeRejectedApply { get; set; }
            public CollectedRunRewardTransferAuthorityStatusV1 RecordStatus
            {
                get;
                set;
            } = CollectedRunRewardTransferAuthorityStatusV1.Applied;
            public string RecordDiagnostic { get; set; } = string.Empty;
            public bool RestoreSucceeds { get; set; } = true;
            public int LiveMutationVersion { get; private set; }
            public int ApplyCallCount { get; private set; }
            public int RecordCallCount { get; private set; }
            public int RestoreCallCount { get; private set; }

            public void SeedReceipt(CollectedRunRewardTransferReceiptV1 receipt)
            {
                byOperation[receipt.OperationStableId] = receipt;
                foreach (StableId reward in receipt.AppliedRewardStableIds)
                    byReward[reward] = receipt;
            }

            public PermanentRewardTransferStateV1 ExportState()
            {
                return new PermanentRewardTransferStateV1(
                    Id("character-instance.fake-authority"),
                    LiveMutationVersion,
                    Fingerprint("character-state-" + LiveMutationVersion),
                    LiveMutationVersion,
                    Fingerprint("account-state-" + LiveMutationVersion),
                    new Dictionary<string, string>
                    {
                        {
                            "fake-authority",
                            Fingerprint("authority-" + LiveMutationVersion)
                        },
                    });
            }

            public bool TryGetDurableReceipt(
                StableId transferOperationStableId,
                out CollectedRunRewardTransferReceiptV1 receipt)
            {
                return byOperation.TryGetValue(
                    transferOperationStableId,
                    out receipt);
            }

            public bool TryGetDurableReceiptForReward(
                StableId rewardInstanceStableId,
                out CollectedRunRewardTransferReceiptV1 receipt)
            {
                return byReward.TryGetValue(rewardInstanceStableId, out receipt);
            }

            public CollectedRunRewardTransferPreflightResultV1 Preflight(
                CollectedRunRewardAtomicPlanV2 plan)
            {
                return CollectedRunRewardTransferPreflightResultV1.Accepted();
            }

            public ICollectedRunRewardTransferCompensationV1 CaptureCompensation()
            {
                return new FakeCompensation(LiveMutationVersion);
            }

            public CollectedRunRewardAtomicApplyResultV1 ApplyAtomicBatch(
                CollectedRunRewardAtomicPlanV2 plan)
            {
                ApplyCallCount++;
                if (ApplyStatus
                        == CollectedRunRewardTransferAuthorityStatusV1.Applied
                    || MutateBeforeRejectedApply)
                {
                    LiveMutationVersion++;
                }
                return new CollectedRunRewardAtomicApplyResultV1(
                    ApplyStatus,
                    ApplyStatus
                            == CollectedRunRewardTransferAuthorityStatusV1.Applied
                        ? plan.Rewards.Select(
                            item => item.RewardInstanceStableId)
                        : Array.Empty<StableId>(),
                    new Dictionary<string, string>
                    {
                        {
                            "fake-authority",
                            Fingerprint("applied-" + LiveMutationVersion)
                        },
                    },
                    ApplyDiagnostic);
            }

            public CollectedRunRewardTransferReceiptRecordResultV1 RecordReceipt(
                CollectedRunRewardTransferReceiptV1 receipt)
            {
                RecordCallCount++;
                if (RecordStatus
                    != CollectedRunRewardTransferAuthorityStatusV1.Applied)
                {
                    return new CollectedRunRewardTransferReceiptRecordResultV1(
                        RecordStatus,
                        null,
                        RecordDiagnostic);
                }
                SeedReceipt(receipt);
                return new CollectedRunRewardTransferReceiptRecordResultV1(
                    RecordStatus,
                    receipt,
                    string.Empty);
            }

            public CollectedRunRewardTransferRestoreResultV1 Restore(
                ICollectedRunRewardTransferCompensationV1 compensation)
            {
                RestoreCallCount++;
                if (!RestoreSucceeds)
                {
                    return new CollectedRunRewardTransferRestoreResultV1(
                        false,
                        "fixture-restore-failed");
                }
                LiveMutationVersion =
                    ((FakeCompensation)compensation).LiveMutationVersion;
                byOperation.Clear();
                byReward.Clear();
                return new CollectedRunRewardTransferRestoreResultV1(
                    true,
                    string.Empty);
            }
        }

        private sealed class FakePersistence :
            ICollectedRunRewardTransferPersistencePortV1
        {
            public bool IsAvailable { get; set; } = true;
            public bool ThrowPrepared { get; set; }
            public bool ReturnNullPrepared { get; set; }
            public bool ThrowFinal { get; set; }
            public bool ReturnNullFinal { get; set; }
            public CollectedRunRewardTransferPersistenceResultV1 PreparedResult
            {
                get;
                set;
            } = PreparedSuccess();
            public CollectedRunRewardTransferPersistenceResultV1 FinalResult
            {
                get;
                set;
            } = FinalSuccess();
            public int PreparedCallCount { get; private set; }
            public int FinalCallCount { get; private set; }

            public CollectedRunRewardTransferPersistenceResultV1
                PersistPreparedCustody(
                    CollectedRunRewardPreparedTransferV1 prepared)
            {
                PreparedCallCount++;
                if (ThrowPrepared) throw new InvalidOperationException("fixture");
                return ReturnNullPrepared ? null : PreparedResult;
            }

            public CollectedRunRewardTransferPersistenceResultV1
                PersistAppliedAndVerify(
                    CollectedRunRewardPreparedTransferV1 persisted,
                    CollectedRunRewardTransferReceiptV1 receipt)
            {
                FinalCallCount++;
                if (ThrowFinal) throw new InvalidOperationException("fixture");
                return ReturnNullFinal ? null : FinalResult;
            }
        }
    }
}

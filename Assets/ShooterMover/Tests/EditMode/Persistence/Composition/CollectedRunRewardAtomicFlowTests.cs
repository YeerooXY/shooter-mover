using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CollectedRunRewardAtomicFlowTests
    {
        [Test]
        public void FirstApplicationSucceeds()
        {
            CollectedRunRewardAtomicPlan plan = BuildPlan("first");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence();
            var coordinator = new CollectedRunRewardTransferFlow(
                authority,
                persistence);

            CollectedRunRewardTransferResult result = coordinator.Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatus.Applied));
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
            CollectedRunRewardAtomicPlan plan = BuildPlan("replay");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence();
            var coordinator = new CollectedRunRewardTransferFlow(
                authority,
                persistence);

            CollectedRunRewardTransferResult first = coordinator.Apply(plan);
            int mutations = authority.LiveMutationVersion;
            CollectedRunRewardTransferResult replay = coordinator.Apply(plan);

            Assert.That(first.Status,
                Is.EqualTo(CollectedRunRewardTransferStatus.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(CollectedRunRewardTransferStatus.ExactReplay));
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
            CollectedRunRewardAtomicPlan plan = BuildPlan("operation-conflict");
            var authority = new FakeAtomicState();
            authority.SeedReceipt(ReceiptFor(
                plan,
                Fingerprint("different-batch"),
                Fingerprint("different-plan")));
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatus.ConflictingDuplicate));
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(persistence.PreparedCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void PartialRewardOverlapRejectsBeforePersistenceOrMutation()
        {
            CollectedRunRewardAtomicPlan plan = BuildPlan("overlap");
            var authority = new FakeAtomicState();
            authority.SeedReceipt(ReceiptForOtherOperation(plan));
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatus.ConflictingDuplicate));
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
            CollectedRunRewardAtomicPlan plan = BuildPlan(diagnostic);
            var authority = new FakeAtomicState
            {
                ApplyStatus = CollectedRunRewardTransferStateStatus.Rejected,
                ApplyDiagnostic = diagnostic,
                MutateBeforeRejectedApply = true,
            };
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatus.Rejected));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(persistence.FinalCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.True);
        }

        [Test]
        public void ReceiptFailureCompensatesAppliedAuthorities()
        {
            CollectedRunRewardAtomicPlan plan = BuildPlan("receipt-failure");
            var authority = new FakeAtomicState
            {
                RecordStatus =
                    CollectedRunRewardTransferStateStatus.Rejected,
                RecordDiagnostic = "fixture-receipt-rejected",
            };
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatus.Rejected));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RecordCallCount, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(persistence.FinalCallCount, Is.Zero);
        }

        [Test]
        public void SafeFinalPersistenceRejectionCompensates()
        {
            CollectedRunRewardAtomicPlan plan = BuildPlan("safe-final-reject");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                FinalResult = RejectedBeforeReplacement(
                    "fixture-final-before-replacement"),
            };

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(CollectedRunRewardTransferStatus.Rejected));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(result.Persistence.RejectedBeforeReplacement, Is.True);
            Assert.That(result.ExactRetryAllowed, Is.True);
        }

        [Test]
        public void CompensationFailureBecomesFatal()
        {
            CollectedRunRewardAtomicPlan plan = BuildPlan("restore-failure");
            var authority = new FakeAtomicState
            {
                ApplyStatus = CollectedRunRewardTransferStateStatus.Rejected,
                ApplyDiagnostic = "fixture-apply-rejected",
                MutateBeforeRejectedApply = true,
                RestoreSucceeds = false,
            };
            var persistence = new FakePersistence();

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatus
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
            CollectedRunRewardAtomicPlan plan = BuildPlan(
                throwInsteadOfNull ? "prepared-throw" : "prepared-null");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                ThrowPrepared = throwInsteadOfNull,
                ReturnNullPrepared = !throwInsteadOfNull,
            };

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatus
                        .FatalCompensationFailure));
            Assert.That(result.Persistence.DurableStateUncertain, Is.True);
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(authority.RestoreCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void UncertainFinalPersistencePerformsNoLiveRollback()
        {
            CollectedRunRewardAtomicPlan plan = BuildPlan("uncertain-final");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                FinalResult = Uncertain("fixture-final-uncertain"),
            };

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    CollectedRunRewardTransferStatus
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
            CollectedRunRewardAtomicPlan plan = BuildPlan("uncertain-prepared");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                PreparedResult = Uncertain("fixture-prepared-uncertain"),
            };

            CollectedRunRewardTransferResult result =
                new CollectedRunRewardTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Persistence.DurableStateUncertain, Is.True);
            Assert.That(result.ExactRetryAllowed, Is.False);
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(authority.RestoreCallCount, Is.Zero);
        }

        private static CollectedRunRewardAtomicPlan BuildPlan(string suffix)
        {
            StableId run = Id("run-instance." + suffix);
            StableId character = Id("character-instance." + suffix);
            StableId rewardId = Id("reward-instance." + suffix);
            var reward = new CollectedRunRewardTransferItem(
                rewardId,
                RewardGrantKind.Money,
                MoneyWalletIds.CurrencyStableId,
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
            CollectedRunRewardPreparedTransfer awaiting =
                CollectedRunRewardPreparedTransfer.AwaitingAcceptedEnd(
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
                    Array.Empty<ShooterMover.Domain.Rewards.Strongboxes.StrongboxInstanceContext>());
            StableId transferOperation = Id("operation.transfer-" + suffix);
            StableId missionResult = Id("mission-result." + suffix);
            string missionFingerprint = Fingerprint("mission-" + suffix);
            string batch = CollectedRunRewardAtomicPlan.ComputeBatchFingerprint(
                transferOperation,
                run,
                1L,
                missionResult,
                missionFingerprint,
                character,
                2L,
                awaiting.ExpectedCharacterFingerprint,
                awaiting.Rewards);
            RewardGrant grant = RewardGrant.Create(
                rewardId,
                RewardGrantKind.Money,
                MoneyWalletIds.CurrencyStableId,
                5L);
            RewardGrantApplicationPayload payload =
                RewardGrantApplicationPayload.ForValue(grant);
            StableId commitment = Id("commitment." + suffix);
            RewardOperationRequest operation = RewardOperationRequest.Create(
                run,
                run,
                transferOperation,
                commitment,
                Id("reward-profile.collected-run-transfer"),
                batch);
            RewardCommitCommand commit = RewardCommitCommand.Create(
                operation,
                RewardResult.CreateGrants(
                    commitment,
                    transferOperation,
                    new[] { grant }),
                Fingerprint("generation-" + suffix),
                new[] { payload });
            RewardClaimCommand claim = RewardClaimCommand.Create(
                Id("claim." + suffix),
                commitment,
                character,
                MoneyWalletIds.AuthorityStableId,
                Id("authority.scrap-" + suffix),
                Id("authority.holdings-" + suffix),
                0L,
                0L,
                0L);
            string planFingerprint =
                CollectedRunRewardAtomicPlan.ComputeFingerprint(
                    batch,
                    commit,
                    claim,
                    new[] { payload },
                    Array.Empty<StrongboxInstanceContext>());
            CollectedRunRewardPreparedTransfer prepared = awaiting.AcceptEnd(
                transferOperation,
                missionResult,
                missionFingerprint,
                batch,
                planFingerprint);
            return new CollectedRunRewardAtomicPlan(
                prepared,
                commit,
                claim,
                new[] { payload },
                Array.Empty<StrongboxInstanceContext>());
        }

        private static CollectedRunRewardTransferReceipt ReceiptFor(
            CollectedRunRewardAtomicPlan plan,
            string batchFingerprint,
            string planFingerprint)
        {
            return new CollectedRunRewardTransferReceipt(
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
                        CollectedRunRewardTransferFlow
                            .ApplicationPlanAuthorityKey,
                        planFingerprint
                    },
                });
        }

        private static CollectedRunRewardTransferReceipt
            ReceiptForOtherOperation(CollectedRunRewardAtomicPlan plan)
        {
            return new CollectedRunRewardTransferReceipt(
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
                        CollectedRunRewardTransferFlow
                            .ApplicationPlanAuthorityKey,
                        Fingerprint("other-plan")
                    },
                });
        }

        private static CollectedRunRewardTransferPersistenceResult
            RejectedBeforeReplacement(string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResult(
                CollectedRunRewardTransferPersistenceStatus
                    .RejectedBeforeReplacement,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }

        private static CollectedRunRewardTransferPersistenceResult
            Uncertain(string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResult(
                CollectedRunRewardTransferPersistenceStatus
                    .DurableStateUncertain,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }

        private static CollectedRunRewardTransferPersistenceResult
            PreparedSuccess()
        {
            return new CollectedRunRewardTransferPersistenceResult(
                CollectedRunRewardTransferPersistenceStatus
                    .PreparedAndVerified,
                1L,
                Fingerprint("account-prepared"),
                1L,
                Fingerprint("character-prepared"),
                string.Empty);
        }

        private static CollectedRunRewardTransferPersistenceResult
            FinalSuccess()
        {
            return new CollectedRunRewardTransferPersistenceResult(
                CollectedRunRewardTransferPersistenceStatus
                    .PersistedAndVerified,
                2L,
                Fingerprint("account-final"),
                2L,
                Fingerprint("character-final"),
                string.Empty);
        }

        private static string Fingerprint(string material)
        {
            return Strongbox.Fingerprint(material);
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private sealed class FakeCompensation :
            ICollectedRunRewardTransferCompensation
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
                return Strongbox.Fingerprint(
                    "compensation-" + value);
            }
        }

        private sealed class FakeAtomicState :
            ICollectedRunRewardAtomicBatchStatePort
        {
            private readonly Dictionary<StableId,
                CollectedRunRewardTransferReceipt> byOperation =
                    new Dictionary<StableId,
                        CollectedRunRewardTransferReceipt>();
            private readonly Dictionary<StableId,
                CollectedRunRewardTransferReceipt> byReward =
                    new Dictionary<StableId,
                        CollectedRunRewardTransferReceipt>();

            public CollectedRunRewardTransferStateStatus ApplyStatus
            {
                get;
                set;
            } = CollectedRunRewardTransferStateStatus.Applied;
            public string ApplyDiagnostic { get; set; } = string.Empty;
            public bool MutateBeforeRejectedApply { get; set; }
            public CollectedRunRewardTransferStateStatus RecordStatus
            {
                get;
                set;
            } = CollectedRunRewardTransferStateStatus.Applied;
            public string RecordDiagnostic { get; set; } = string.Empty;
            public bool RestoreSucceeds { get; set; } = true;
            public int LiveMutationVersion { get; private set; }
            public int ApplyCallCount { get; private set; }
            public int RecordCallCount { get; private set; }
            public int RestoreCallCount { get; private set; }

            public void SeedReceipt(CollectedRunRewardTransferReceipt receipt)
            {
                byOperation[receipt.OperationStableId] = receipt;
                foreach (StableId reward in receipt.AppliedRewardStableIds)
                    byReward[reward] = receipt;
            }

            public PermanentRewardTransferState ExportState()
            {
                return new PermanentRewardTransferState(
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
                out CollectedRunRewardTransferReceipt receipt)
            {
                return byOperation.TryGetValue(
                    transferOperationStableId,
                    out receipt);
            }

            public bool TryGetDurableReceiptForReward(
                StableId rewardInstanceStableId,
                out CollectedRunRewardTransferReceipt receipt)
            {
                return byReward.TryGetValue(rewardInstanceStableId, out receipt);
            }

            public CollectedRunRewardTransferPreflightResult Preflight(
                CollectedRunRewardAtomicPlan plan)
            {
                return CollectedRunRewardTransferPreflightResult.Accepted();
            }

            public ICollectedRunRewardTransferCompensation CaptureCompensation()
            {
                return new FakeCompensation(LiveMutationVersion);
            }

            public CollectedRunRewardAtomicApplyResult ApplyAtomicBatch(
                CollectedRunRewardAtomicPlan plan)
            {
                ApplyCallCount++;
                if (ApplyStatus
                        == CollectedRunRewardTransferStateStatus.Applied
                    || MutateBeforeRejectedApply)
                {
                    LiveMutationVersion++;
                }
                return new CollectedRunRewardAtomicApplyResult(
                    ApplyStatus,
                    ApplyStatus
                            == CollectedRunRewardTransferStateStatus.Applied
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

            public CollectedRunRewardTransferReceiptRecordResult RecordReceipt(
                CollectedRunRewardTransferReceipt receipt)
            {
                RecordCallCount++;
                if (RecordStatus
                    != CollectedRunRewardTransferStateStatus.Applied)
                {
                    return new CollectedRunRewardTransferReceiptRecordResult(
                        RecordStatus,
                        null,
                        RecordDiagnostic);
                }
                SeedReceipt(receipt);
                return new CollectedRunRewardTransferReceiptRecordResult(
                    RecordStatus,
                    receipt,
                    string.Empty);
            }

            public CollectedRunRewardTransferRestoreResult Restore(
                ICollectedRunRewardTransferCompensation compensation)
            {
                RestoreCallCount++;
                if (!RestoreSucceeds)
                {
                    return new CollectedRunRewardTransferRestoreResult(
                        false,
                        "fixture-restore-failed");
                }
                LiveMutationVersion =
                    ((FakeCompensation)compensation).LiveMutationVersion;
                byOperation.Clear();
                byReward.Clear();
                return new CollectedRunRewardTransferRestoreResult(
                    true,
                    string.Empty);
            }
        }

        private sealed class FakePersistence :
            ICollectedRunRewardTransferPersistencePort
        {
            public bool IsAvailable { get; set; } = true;
            public bool ThrowPrepared { get; set; }
            public bool ReturnNullPrepared { get; set; }
            public bool ThrowFinal { get; set; }
            public bool ReturnNullFinal { get; set; }
            public CollectedRunRewardTransferPersistenceResult PreparedResult
            {
                get;
                set;
            } = PreparedSuccess();
            public CollectedRunRewardTransferPersistenceResult FinalResult
            {
                get;
                set;
            } = FinalSuccess();
            public int PreparedCallCount { get; private set; }
            public int FinalCallCount { get; private set; }

            public CollectedRunRewardTransferPersistenceResult
                PersistPreparedCustody(
                    CollectedRunRewardPreparedTransfer prepared)
            {
                PreparedCallCount++;
                if (ThrowPrepared) throw new InvalidOperationException("fixture");
                return ReturnNullPrepared ? null : PreparedResult;
            }

            public CollectedRunRewardTransferPersistenceResult
                PersistAppliedAndVerify(
                    CollectedRunRewardPreparedTransfer persisted,
                    CollectedRunRewardTransferReceipt receipt)
            {
                FinalCallCount++;
                if (ThrowFinal) throw new InvalidOperationException("fixture");
                return ReturnNullFinal ? null : FinalResult;
            }
        }
    }
}

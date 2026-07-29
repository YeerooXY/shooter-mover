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
    public sealed class RewardClaimAtomicFlowTests
    {
        [Test]
        public void FirstApplicationSucceeds()
        {
            RewardClaimAtomicPlan plan = BuildPlan("first");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence();
            var coordinator = new RewardClaimTransferFlow(
                authority,
                persistence);

            RewardClaimTransferResult result = coordinator.Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(RewardClaimTransferStatus.Applied));
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
            RewardClaimAtomicPlan plan = BuildPlan("replay");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence();
            var coordinator = new RewardClaimTransferFlow(
                authority,
                persistence);

            RewardClaimTransferResult first = coordinator.Apply(plan);
            int mutations = authority.LiveMutationVersion;
            RewardClaimTransferResult replay = coordinator.Apply(plan);

            Assert.That(first.Status,
                Is.EqualTo(RewardClaimTransferStatus.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(RewardClaimTransferStatus.ExactReplay));
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
            RewardClaimAtomicPlan plan = BuildPlan("operation-conflict");
            var authority = new FakeAtomicState();
            authority.SeedReceipt(ReceiptFor(
                plan,
                Fingerprint("different-batch"),
                Fingerprint("different-plan")));
            var persistence = new FakePersistence();

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    RewardClaimTransferStatus.ConflictingDuplicate));
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(persistence.PreparedCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void PartialRewardOverlapRejectsBeforePersistenceOrMutation()
        {
            RewardClaimAtomicPlan plan = BuildPlan("overlap");
            var authority = new FakeAtomicState();
            authority.SeedReceipt(ReceiptForOtherOperation(plan));
            var persistence = new FakePersistence();

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    RewardClaimTransferStatus.ConflictingDuplicate));
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
            RewardClaimAtomicPlan plan = BuildPlan(diagnostic);
            var authority = new FakeAtomicState
            {
                ApplyStatus = RewardClaimTransferStateStatus.Rejected,
                ApplyDiagnostic = diagnostic,
                MutateBeforeRejectedApply = true,
            };
            var persistence = new FakePersistence();

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(RewardClaimTransferStatus.Rejected));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(persistence.FinalCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.True);
        }

        [Test]
        public void ReceiptFailureCompensatesAppliedAuthorities()
        {
            RewardClaimAtomicPlan plan = BuildPlan("receipt-failure");
            var authority = new FakeAtomicState
            {
                RecordStatus =
                    RewardClaimTransferStateStatus.Rejected,
                RecordDiagnostic = "fixture-receipt-rejected",
            };
            var persistence = new FakePersistence();

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(RewardClaimTransferStatus.Rejected));
            Assert.That(authority.ApplyCallCount, Is.EqualTo(1));
            Assert.That(authority.RecordCallCount, Is.EqualTo(1));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(persistence.FinalCallCount, Is.Zero);
        }

        [Test]
        public void SafeFinalPersistenceRejectionCompensates()
        {
            RewardClaimAtomicPlan plan = BuildPlan("safe-final-reject");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                FinalResult = RejectedBeforeReplacement(
                    "fixture-final-before-replacement"),
            };

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(RewardClaimTransferStatus.Rejected));
            Assert.That(authority.RestoreCallCount, Is.EqualTo(1));
            Assert.That(authority.LiveMutationVersion, Is.Zero);
            Assert.That(result.Persistence.RejectedBeforeReplacement, Is.True);
            Assert.That(result.ExactRetryAllowed, Is.True);
        }

        [Test]
        public void CompensationFailureBecomesFatal()
        {
            RewardClaimAtomicPlan plan = BuildPlan("restore-failure");
            var authority = new FakeAtomicState
            {
                ApplyStatus = RewardClaimTransferStateStatus.Rejected,
                ApplyDiagnostic = "fixture-apply-rejected",
                MutateBeforeRejectedApply = true,
                RestoreSucceeds = false,
            };
            var persistence = new FakePersistence();

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    RewardClaimTransferStatus
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
            RewardClaimAtomicPlan plan = BuildPlan(
                throwInsteadOfNull ? "prepared-throw" : "prepared-null");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                ThrowPrepared = throwInsteadOfNull,
                ReturnNullPrepared = !throwInsteadOfNull,
            };

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    RewardClaimTransferStatus
                        .FatalCompensationFailure));
            Assert.That(result.Persistence.DurableStateUncertain, Is.True);
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(authority.RestoreCallCount, Is.Zero);
            Assert.That(result.ExactRetryAllowed, Is.False);
        }

        [Test]
        public void UncertainFinalPersistencePerformsNoLiveRollback()
        {
            RewardClaimAtomicPlan plan = BuildPlan("uncertain-final");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                FinalResult = Uncertain("fixture-final-uncertain"),
            };

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Status,
                Is.EqualTo(
                    RewardClaimTransferStatus
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
            RewardClaimAtomicPlan plan = BuildPlan("uncertain-prepared");
            var authority = new FakeAtomicState();
            var persistence = new FakePersistence
            {
                PreparedResult = Uncertain("fixture-prepared-uncertain"),
            };

            RewardClaimTransferResult result =
                new RewardClaimTransferFlow(
                    authority,
                    persistence).Apply(plan);

            Assert.That(result.Persistence.DurableStateUncertain, Is.True);
            Assert.That(result.ExactRetryAllowed, Is.False);
            Assert.That(authority.ApplyCallCount, Is.Zero);
            Assert.That(authority.RestoreCallCount, Is.Zero);
        }

        private static RewardClaimAtomicPlan BuildPlan(string suffix)
        {
            StableId run = Id("run-instance." + suffix);
            StableId character = Id("character-instance." + suffix);
            StableId rewardId = Id("reward-instance." + suffix);
            var reward = new RewardClaimTransferItem(
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
            RewardClaimPreparedTransfer awaiting =
                RewardClaimPreparedTransfer.AwaitingAcceptedEnd(
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
            string batch = RewardClaimAtomicPlan.ComputeBatchFingerprint(
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
                RewardClaimAtomicPlan.ComputeFingerprint(
                    batch,
                    commit,
                    claim,
                    new[] { payload },
                    Array.Empty<StrongboxInstanceContext>());
            RewardClaimPreparedTransfer prepared = awaiting.AcceptEnd(
                transferOperation,
                missionResult,
                missionFingerprint,
                batch,
                planFingerprint);
            return new RewardClaimAtomicPlan(
                prepared,
                commit,
                claim,
                new[] { payload },
                Array.Empty<StrongboxInstanceContext>());
        }

        private static RewardClaimTransferReceipt ReceiptFor(
            RewardClaimAtomicPlan plan,
            string batchFingerprint,
            string planFingerprint)
        {
            return new RewardClaimTransferReceipt(
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
                        RewardClaimTransferFlow
                            .ApplicationPlanAuthorityKey,
                        planFingerprint
                    },
                });
        }

        private static RewardClaimTransferReceipt
            ReceiptForOtherOperation(RewardClaimAtomicPlan plan)
        {
            return new RewardClaimTransferReceipt(
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
                        RewardClaimTransferFlow
                            .ApplicationPlanAuthorityKey,
                        Fingerprint("other-plan")
                    },
                });
        }

        private static RewardClaimTransferPersistenceResult
            RejectedBeforeReplacement(string diagnostic)
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus
                    .RejectedBeforeReplacement,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }

        private static RewardClaimTransferPersistenceResult
            Uncertain(string diagnostic)
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus
                    .DurableStateUncertain,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }

        private static RewardClaimTransferPersistenceResult
            PreparedSuccess()
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus
                    .PreparedAndVerified,
                1L,
                Fingerprint("account-prepared"),
                1L,
                Fingerprint("character-prepared"),
                string.Empty);
        }

        private static RewardClaimTransferPersistenceResult
            FinalSuccess()
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus
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
            IRewardClaimTransferCompensation
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
            IRewardClaimAtomicBatchStatePort
        {
            private readonly Dictionary<StableId,
                RewardClaimTransferReceipt> byOperation =
                    new Dictionary<StableId,
                        RewardClaimTransferReceipt>();
            private readonly Dictionary<StableId,
                RewardClaimTransferReceipt> byReward =
                    new Dictionary<StableId,
                        RewardClaimTransferReceipt>();

            public RewardClaimTransferStateStatus ApplyStatus
            {
                get;
                set;
            } = RewardClaimTransferStateStatus.Applied;
            public string ApplyDiagnostic { get; set; } = string.Empty;
            public bool MutateBeforeRejectedApply { get; set; }
            public RewardClaimTransferStateStatus RecordStatus
            {
                get;
                set;
            } = RewardClaimTransferStateStatus.Applied;
            public string RecordDiagnostic { get; set; } = string.Empty;
            public bool RestoreSucceeds { get; set; } = true;
            public int LiveMutationVersion { get; private set; }
            public int ApplyCallCount { get; private set; }
            public int RecordCallCount { get; private set; }
            public int RestoreCallCount { get; private set; }

            public void SeedReceipt(RewardClaimTransferReceipt receipt)
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
                out RewardClaimTransferReceipt receipt)
            {
                return byOperation.TryGetValue(
                    transferOperationStableId,
                    out receipt);
            }

            public bool TryGetDurableReceiptForReward(
                StableId rewardInstanceStableId,
                out RewardClaimTransferReceipt receipt)
            {
                return byReward.TryGetValue(rewardInstanceStableId, out receipt);
            }

            public RewardClaimTransferPreflightResult Preflight(
                RewardClaimAtomicPlan plan)
            {
                return RewardClaimTransferPreflightResult.Accepted();
            }

            public IRewardClaimTransferCompensation CaptureCompensation()
            {
                return new FakeCompensation(LiveMutationVersion);
            }

            public RewardClaimAtomicApplyResult ApplyAtomicBatch(
                RewardClaimAtomicPlan plan)
            {
                ApplyCallCount++;
                if (ApplyStatus
                        == RewardClaimTransferStateStatus.Applied
                    || MutateBeforeRejectedApply)
                {
                    LiveMutationVersion++;
                }
                return new RewardClaimAtomicApplyResult(
                    ApplyStatus,
                    ApplyStatus
                            == RewardClaimTransferStateStatus.Applied
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

            public RewardClaimTransferReceiptRecordResult RecordReceipt(
                RewardClaimTransferReceipt receipt)
            {
                RecordCallCount++;
                if (RecordStatus
                    != RewardClaimTransferStateStatus.Applied)
                {
                    return new RewardClaimTransferReceiptRecordResult(
                        RecordStatus,
                        null,
                        RecordDiagnostic);
                }
                SeedReceipt(receipt);
                return new RewardClaimTransferReceiptRecordResult(
                    RecordStatus,
                    receipt,
                    string.Empty);
            }

            public RewardClaimTransferRestoreResult Restore(
                IRewardClaimTransferCompensation compensation)
            {
                RestoreCallCount++;
                if (!RestoreSucceeds)
                {
                    return new RewardClaimTransferRestoreResult(
                        false,
                        "fixture-restore-failed");
                }
                LiveMutationVersion =
                    ((FakeCompensation)compensation).LiveMutationVersion;
                byOperation.Clear();
                byReward.Clear();
                return new RewardClaimTransferRestoreResult(
                    true,
                    string.Empty);
            }
        }

        private sealed class FakePersistence :
            IRewardClaimTransferPersistencePort
        {
            public bool IsAvailable { get; set; } = true;
            public bool ThrowPrepared { get; set; }
            public bool ReturnNullPrepared { get; set; }
            public bool ThrowFinal { get; set; }
            public bool ReturnNullFinal { get; set; }
            public RewardClaimTransferPersistenceResult PreparedResult
            {
                get;
                set;
            } = PreparedSuccess();
            public RewardClaimTransferPersistenceResult FinalResult
            {
                get;
                set;
            } = FinalSuccess();
            public int PreparedCallCount { get; private set; }
            public int FinalCallCount { get; private set; }

            public RewardClaimTransferPersistenceResult
                PersistPreparedCustody(
                    RewardClaimPreparedTransfer prepared)
            {
                PreparedCallCount++;
                if (ThrowPrepared) throw new InvalidOperationException("fixture");
                return ReturnNullPrepared ? null : PreparedResult;
            }

            public RewardClaimTransferPersistenceResult
                PersistAppliedAndVerify(
                    RewardClaimPreparedTransfer persisted,
                    RewardClaimTransferReceipt receipt)
            {
                FinalCallCount++;
                if (ThrowFinal) throw new InvalidOperationException("fixture");
                return ReturnNullFinal ? null : FinalResult;
            }
        }
    }
}

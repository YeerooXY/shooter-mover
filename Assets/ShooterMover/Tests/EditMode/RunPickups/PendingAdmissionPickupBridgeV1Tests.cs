#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed class PendingAdmissionPickupBridgeV1Tests
    {
        private static readonly StableId RunId = Id("run", "shared-stage1");
        private static readonly StableId RoomId = Id("room", "entry");
        private static readonly StableId SourceEntityId = Id("enemy-entity", "droid");
        private static readonly StableId SourcePlacementId = Id("placement", "droid");
        private static readonly StableId ParticipantId = Id("participant", "player");
        private static readonly StableId ActorId = Id("actor", "player");

        private sealed class FakeSourceResolver :
            IPickupSourcePositionResolverV1
        {
            public int UnavailableAttempts;
            public int ThrowAttempts;
            public int Attempts;

            public bool TryResolve(
                out PickupSourcePositionV1 position,
                out string diagnostic)
            {
                Attempts++;
                if (ThrowAttempts > 0)
                {
                    ThrowAttempts--;
                    throw new InvalidOperationException("source-fail-once");
                }
                if (UnavailableAttempts > 0)
                {
                    UnavailableAttempts--;
                    position = null;
                    diagnostic = "fake-source-unavailable";
                    return false;
                }
                position = new PickupSourcePositionV1(
                    RoomId,
                    new Vector2(4.5f, -2.25f),
                    "fake-source-position-fingerprint");
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class FakeRuntime : IPickupAdmissionRuntimeV1
        {
            private readonly HashSet<StableId> realizedOperations =
                new HashSet<StableId>();

            public int RegisterFailures;
            public int RealizeFailures;
            public int PresentationFailures;
            public int RealizeThrows;
            public int TerminalRejects;
            public int ConflictingRejects;
            public int RegisterCalls;
            public int RealizeCalls;
            public int PresentationCalls;
            public int AcceptedRealizationCount;

            public bool TryRegisterPosition(
                TerminalDropSourceFactV1 source,
                PickupSourcePositionV1 position,
                out string diagnostic)
            {
                RegisterCalls++;
                if (RegisterFailures > 0)
                {
                    RegisterFailures--;
                    diagnostic = "fake-position-registration-unavailable";
                    return false;
                }
                diagnostic = string.Empty;
                return true;
            }

            public RunPickupRealizationResultV1 Realize(
                PendingTerminalDropAdmissionResultV1 admission)
            {
                RealizeCalls++;
                if (RealizeThrows > 0)
                {
                    RealizeThrows--;
                    throw new InvalidOperationException("realize-fail-once");
                }
                if (RealizeFailures > 0)
                {
                    RealizeFailures--;
                    return new RunPickupRealizationResultV1(
                        RunPickupRealizationStatusV1.Rejected,
                        null,
                        Array.Empty<RunPickupSnapshotV1>(),
                        "run-pickup-session-context-unavailable");
                }
                if (TerminalRejects > 0)
                {
                    TerminalRejects--;
                    return new RunPickupRealizationResultV1(
                        RunPickupRealizationStatusV1.Rejected,
                        null,
                        Array.Empty<RunPickupSnapshotV1>(),
                        "run-pickup-realization-participant-mismatch");
                }
                if (ConflictingRejects > 0)
                {
                    ConflictingRejects--;
                    return new RunPickupRealizationResultV1(
                        RunPickupRealizationStatusV1.ConflictingDuplicate,
                        null,
                        Array.Empty<RunPickupSnapshotV1>(),
                        "run-pickup-drop-operation-conflict");
                }

                bool first = realizedOperations.Add(admission.OperationStableId);
                if (first) AcceptedRealizationCount++;
                return new RunPickupRealizationResultV1(
                    first
                        ? RunPickupRealizationStatusV1.Realized
                        : RunPickupRealizationStatusV1.ExactReplay,
                    null,
                    Array.Empty<RunPickupSnapshotV1>(),
                    string.Empty);
            }

            public RunPickupPresentationSyncResultV1 Synchronize(
                StableId roomStableId)
            {
                PresentationCalls++;
                if (PresentationFailures > 0)
                {
                    PresentationFailures--;
                    return new RunPickupPresentationSyncResultV1(
                        1, 0, 0, 0, 0, 1,
                        "fake-presenter-unavailable");
                }
                return new RunPickupPresentationSyncResultV1(
                    1, 1, 1, 0, 0, 0, string.Empty);
            }
        }

        [Test]
        public void SourcePositionUnavailableOnce_ThenSucceeds()
        {
            var resolver = new FakeSourceResolver { UnavailableAttempts = 1 };
            var runtime = new FakeRuntime();
            PendingAdmissionPickupBridgeV1 queue = Queue(runtime, resolver);
            queue.TryEnqueue(Admission());

            Assert.That(queue.ProcessPending(), Is.EqualTo(0));
            Assert.That(queue.PendingCount, Is.EqualTo(1));
            Assert.That(queue.LastDiagnostic, Is.EqualTo("fake-source-unavailable"));
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(queue.PendingCount, Is.Zero);
            Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        [Test]
        public void RunSessionContextUnavailableOnce_ThenSucceeds()
        {
            var runtime = new FakeRuntime { RealizeFailures = 1 };
            PendingAdmissionPickupBridgeV1 queue = Queue(
                runtime,
                new FakeSourceResolver());
            queue.TryEnqueue(Admission());

            Assert.That(queue.ProcessPending(), Is.EqualTo(0));
            Assert.That(queue.PendingCount, Is.EqualTo(1));
            Assert.That(queue.QuarantinedCount, Is.Zero);
            Assert.That(queue.LastDiagnostic,
                Is.EqualTo("run-pickup-session-context-unavailable"));
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        [Test]
        public void PresenterUnavailableOnce_ThenExactRealizationReplayCompletes()
        {
            var runtime = new FakeRuntime { PresentationFailures = 1 };
            PendingAdmissionPickupBridgeV1 queue = Queue(
                runtime,
                new FakeSourceResolver());
            queue.TryEnqueue(Admission());

            Assert.That(queue.ProcessPending(), Is.EqualTo(0));
            Assert.That(queue.PendingCount, Is.EqualTo(1));
            Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(runtime.RealizeCalls, Is.EqualTo(2));
            Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        [Test]
        public void ExceptionDuringFirstDelivery_RetainsExactAdmissionForRetry()
        {
            var runtime = new FakeRuntime { RealizeThrows = 1 };
            PendingAdmissionPickupBridgeV1 queue = Queue(
                runtime,
                new FakeSourceResolver());
            queue.TryEnqueue(Admission());

            Assert.That(queue.ProcessPending(), Is.EqualTo(0));
            Assert.That(queue.PendingCount, Is.EqualTo(1));
            Assert.That(queue.LastDiagnostic,
                Does.StartWith("pickup-realization-exception:"));
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        [Test]
        public void TerminalRejection_IsQuarantinedAndNeverRetried()
        {
            var runtime = new FakeRuntime { TerminalRejects = 1 };
            PendingTerminalDropAdmissionResultV1 admission = Admission();
            PendingAdmissionPickupBridgeV1 queue = Queue(
                runtime,
                new FakeSourceResolver());
            queue.TryEnqueue(admission);

            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(queue.PendingCount, Is.Zero);
            Assert.That(queue.QuarantinedCount, Is.EqualTo(1));
            Assert.That(runtime.RealizeCalls, Is.EqualTo(1));
            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(runtime.RealizeCalls, Is.EqualTo(1));
            Assert.That(queue.TryEnqueue(admission).Disposition,
                Is.EqualTo(PickupDeliveryDispositionV1.Rejected));
        }

        [Test]
        public void ConflictingRealization_IsQuarantinedAndNeverRetried()
        {
            var runtime = new FakeRuntime { ConflictingRejects = 1 };
            PendingAdmissionPickupBridgeV1 queue = Queue(
                runtime,
                new FakeSourceResolver());
            queue.TryEnqueue(Admission());

            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(queue.PendingCount, Is.Zero);
            Assert.That(queue.QuarantinedCount, Is.EqualTo(1));
            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(runtime.RealizeCalls, Is.EqualTo(1));
        }

        [Test]
        public void ExactAdmissionRedelivery_NeverCreatesSecondRealization()
        {
            PendingTerminalDropAdmissionResultV1 admission = Admission();
            var runtime = new FakeRuntime();
            PendingAdmissionPickupBridgeV1 queue = Queue(
                runtime,
                new FakeSourceResolver());

            Assert.That(queue.TryEnqueue(admission).Disposition,
                Is.EqualTo(PickupDeliveryDispositionV1.Applied));
            Assert.That(queue.TryEnqueue(admission).Disposition,
                Is.EqualTo(PickupDeliveryDispositionV1.ExactReplay));
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(queue.TryEnqueue(admission).Disposition,
                Is.EqualTo(PickupDeliveryDispositionV1.ExactReplay));
            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeReleaseAndRecomposition_DoNotLoseAdmittedReward()
        {
            var firstRuntime = new FakeRuntime();
            PendingAdmissionPickupBridgeV1 queue = Queue(
                firstRuntime,
                new FakeSourceResolver());
            queue.TryEnqueue(Admission());

            queue.ReleaseRuntime();
            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(queue.PendingCount, Is.EqualTo(1));
            var replacementRuntime = new FakeRuntime();
            queue.ConfigureRuntime(replacementRuntime);
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(replacementRuntime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        [Test]
        public void ProductionPickupUsesCanonicalProvenanceAndNoSyntheticFacts()
        {
            string stage1Path = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover",
                "Production",
                "Stage1");
            string bootstrap = File.ReadAllText(Path.Combine(
                stage1Path,
                "Stage1RunPickupBootstrap2D.cs"));
            string provenance = File.ReadAllText(Path.Combine(
                stage1Path,
                "Stage1PlayableLoopCompositionV1.TerminalDropProvenance.cs"));
            string props = File.ReadAllText(Path.Combine(
                stage1Path,
                "Stage1RunPickupPropBootstrap2D.cs"));
            string support = File.ReadAllText(Path.Combine(
                stage1Path,
                "Stage1RunPickupBootstrapSupportV1.cs"));

            Assert.That(bootstrap, Does.Not.Contain(
                "DeterministicEnemyRuntimeIdentityDeriverV1"));
            Assert.That(bootstrap, Does.Not.Contain("BuildPickupEnemyCatalog"));
            Assert.That(bootstrap, Does.Not.Contain("BuildRewardProfiles"));
            Assert.That(bootstrap, Does.Not.Contain("new EnemyDeathFactV1"));
            Assert.That(support, Does.Not.Contain(
                "Stage1EnemyTerminalDropObserver2D"));
            Assert.That(support, Does.Not.Contain("difficulty.normal"));
            Assert.That(support, Does.Not.Contain(
                "RunPickupExperience.CurrentState.Level"));
            Assert.That(support, Does.Contain(
                "run.StartCommand.DifficultyStableId"));
            Assert.That(support, Does.Contain(
                "run.FrozenInputs.CharacterStats.Level"));

            Assert.That(provenance, Does.Contain("pendingEnemyRewards"));
            Assert.That(provenance, Does.Contain("notification.EventId"));
            Assert.That(provenance, Does.Contain("notification.SourceId"));
            Assert.That(provenance, Does.Contain(
                "liveSource.SourceEntityStableId"));
            Assert.That(provenance, Does.Contain(
                "LoadProductionEnemyCatalog"));

            Assert.That(props, Does.Not.Contain(
                "DestroyedState.MaximumHealth"));
            Assert.That(props, Does.Not.Contain(
                "controller.CurrentRoomStableId"));
            Assert.That(props, Does.Not.Contain("BuildPropRewardProfiles"));
            Assert.That(props, Does.Contain(
                "TryResolveCanonicalPropTerminalSource"));
            Assert.That(props, Does.Contain("DefinitionFingerprint"));
        }

        [Test]
        public void ProductionPickupBootstrap_ConsumesSharedRunAndOwnsNoShadowGraph()
        {
            string stage1Path = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover",
                "Production",
                "Stage1");
            string bootstrap = File.ReadAllText(Path.Combine(
                stage1Path,
                "Stage1RunPickupBootstrap2D.cs"));
            string access = File.ReadAllText(Path.Combine(
                stage1Path,
                "Stage1PlayableLoopCompositionV1.RunPickupAccess.cs"));

            Assert.That(bootstrap, Does.Contain("TryResolveSharedRunSession"));
            Assert.That(bootstrap, Does.Not.Contain("new RunSessionAuthorityV1"));
            Assert.That(bootstrap, Does.Not.Contain("StartRunSessionCommandV1"));
            Assert.That(bootstrap,
                Does.Not.Contain("Stage1PickupRunSessionRuntimePortFactoryV1"));
            Assert.That(access, Does.Not.Contain("RunPickupMissionResults"));
            Assert.That(access, Does.Not.Contain("RunPickupEffectEmitter"));
            Assert.That(File.Exists(Path.Combine(
                stage1Path,
                "Stage1PickupRunSessionPortsV1.cs")),
                Is.False);
        }

        private static PendingAdmissionPickupBridgeV1 Queue(
            FakeRuntime runtime,
            IPickupSourcePositionResolverV1 resolver)
        {
            var queue = new PendingAdmissionPickupBridgeV1();
            queue.ConfigureRuntime(runtime);
            queue.RegisterSource(
                RunId,
                1L,
                SourceEntityId,
                SourcePlacementId,
                resolver);
            return queue;
        }

        private static PendingTerminalDropAdmissionResultV1 Admission()
        {
            return new PendingTerminalDropAdmissionAuthorityV1().Admit(
                GeneratedDrop());
        }

        private static GeneratedTerminalDropResultV1 GeneratedDrop()
        {
            StableId operationId = Id(
                "terminaldropoperation",
                "retained-delivery");
            StableId profileId = Id("drop-profile", "money");
            var source = new TerminalDropSourceFactV1(
                TerminalDropFactKindIdsV1.EnemyDeath,
                Id("terminal", "enemy-death"),
                Id("trigger", "final-hit"),
                RunId,
                1L,
                SourceEntityId,
                SourcePlacementId,
                1L,
                Id("enemy", "mobile-blaster-droid"),
                ParticipantId,
                ActorId,
                Id("damage", "kinetic"),
                profileId,
                "source-context-fingerprint",
                "definition-fingerprint",
                "upstream-fingerprint");
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                RunId,
                SourceEntityId,
                operationId,
                Id("commitment", "money"),
                profileId,
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            var child = new GeneratedTerminalDropRewardV1(
                Id("terminaldropchild", "money-child"),
                0,
                Id("grant", "money"),
                RewardGrantKindV1.Money,
                Id("currency", "credits"),
                5L);
            return new GeneratedTerminalDropResultV1(
                TerminalDropBindingStatusV1.Accepted,
                TerminalDropRejectionCodeV1.None,
                source,
                profileId,
                operation,
                123UL,
                null,
                new[] { child },
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                string.Empty);
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif

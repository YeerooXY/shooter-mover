#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    public sealed class PendingAdmissionPickupBridgeTests
    {
        private static readonly StableId RunId = Id("run", "shared-run");
        private static readonly StableId RoomId = Id("room", "entry");
        private static readonly StableId SourceEntityId = Id("enemy-entity", "droid");
        private static readonly StableId SourcePlacementId = Id("placement", "droid");
        private static readonly StableId ParticipantId = Id("participant", "player");
        private static readonly StableId ActorId = Id("actor", "player");

        private sealed class FakeSourceResolver :
            IPickupSourcePositionResolver
        {
            public int UnavailableAttempts;
            public int Attempts;

            public bool TryResolve(
                out PickupSourcePosition position,
                out string diagnostic)
            {
                Attempts++;
                if (UnavailableAttempts > 0)
                {
                    UnavailableAttempts--;
                    position = null;
                    diagnostic = "fake-source-unavailable";
                    return false;
                }

                position = new PickupSourcePosition(
                    RoomId,
                    new Vector2(4.5f, -2.25f),
                    "fake-source-position-fingerprint");
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class FakeLive : IPickupAdmissionLive
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
            public PickupSourcePosition RegisteredPosition;

            public bool TryRegisterPosition(
                TerminalDropSourceFact source,
                PickupSourcePosition position,
                out string diagnostic)
            {
                RegisterCalls++;
                if (RegisterFailures > 0)
                {
                    RegisterFailures--;
                    diagnostic = "fake-position-registration-unavailable";
                    return false;
                }

                if (RegisteredPosition != null
                    && !string.Equals(
                        RegisteredPosition.Fingerprint,
                        position.Fingerprint,
                        StringComparison.Ordinal))
                {
                    diagnostic = "fake-position-registration-conflict";
                    return false;
                }

                RegisteredPosition = position;
                diagnostic = string.Empty;
                return true;
            }

            public RunPickupRealizationResult Realize(
                PendingTerminalDropAdmissionResult admission)
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
                    return new RunPickupRealizationResult(
                        RunPickupRealizationStatus.Rejected,
                        null,
                        Array.Empty<RunPickupSnapshot>(),
                        "run-pickup-session-context-unavailable");
                }

                if (TerminalRejects > 0)
                {
                    TerminalRejects--;
                    return new RunPickupRealizationResult(
                        RunPickupRealizationStatus.Rejected,
                        null,
                        Array.Empty<RunPickupSnapshot>(),
                        "run-pickup-realization-participant-mismatch");
                }

                if (ConflictingRejects > 0)
                {
                    ConflictingRejects--;
                    return new RunPickupRealizationResult(
                        RunPickupRealizationStatus.ConflictingDuplicate,
                        null,
                        Array.Empty<RunPickupSnapshot>(),
                        "run-pickup-drop-operation-conflict");
                }

                bool first = realizedOperations.Add(admission.OperationStableId);
                if (first) AcceptedRealizationCount++;
                return new RunPickupRealizationResult(
                    first
                        ? RunPickupRealizationStatus.Realized
                        : RunPickupRealizationStatus.ExactReplay,
                    null,
                    Array.Empty<RunPickupSnapshot>(),
                    string.Empty);
            }

            public RunPickupPresentationSyncResult Synchronize(
                StableId roomStableId)
            {
                PresentationCalls++;
                if (PresentationFailures > 0)
                {
                    PresentationFailures--;
                    return new RunPickupPresentationSyncResult(
                        1, 0, 0, 0, 0, 1,
                        "fake-presenter-unavailable");
                }

                return new RunPickupPresentationSyncResult(
                    1, 1, 1, 0, 0, 0, string.Empty);
            }
        }

        [Test]
        public void SourcePositionUnavailableOnce_ThenSucceeds()
        {
            var resolver = new FakeSourceResolver { UnavailableAttempts = 1 };
            var runtime = new FakeLive();
            PendingAdmissionPickupBridge queue = Queue(runtime, resolver);
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
            var runtime = new FakeLive { RealizeFailures = 1 };
            PendingAdmissionPickupBridge queue = Queue(
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
            var runtime = new FakeLive { PresentationFailures = 1 };
            PendingAdmissionPickupBridge queue = Queue(
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
        public void FrozenEnemyDeathPosition_SurvivesTransformMovementDuringRetry()
        {
            GameObject enemy = new GameObject("Enemy Position Source");
            try
            {
                Vector2 deathPosition = new Vector2(4.5f, -2.25f);
                enemy.transform.position = deathPosition;
                var fixedPosition = new PickupSourcePosition(
                    RoomId,
                    enemy.transform.position,
                    "enemy-terminal-position-at-death");
                var runtime = new FakeLive { PresentationFailures = 1 };
                PendingAdmissionPickupBridge queue = Queue(
                    runtime,
                    new FixedPickupSourcePositionResolver(fixedPosition));
                queue.TryEnqueue(Admission());

                Assert.That(queue.ProcessPending(), Is.Zero);
                Assert.That(queue.PendingCount, Is.EqualTo(1));
                enemy.transform.position = new Vector2(-50f, 75f);

                Assert.That(queue.ProcessPending(), Is.EqualTo(1));
                Assert.That(queue.QuarantinedCount, Is.Zero);
                Assert.That(runtime.RegisteredPosition.Position,
                    Is.EqualTo(deathPosition));
                Assert.That(runtime.RegisterCalls, Is.EqualTo(2));
                Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void ExceptionDuringFirstDelivery_RetainsExactAdmissionForRetry()
        {
            var runtime = new FakeLive { RealizeThrows = 1 };
            PendingAdmissionPickupBridge queue = Queue(
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
            var runtime = new FakeLive { TerminalRejects = 1 };
            PendingTerminalDropAdmissionResult admission = Admission();
            PendingAdmissionPickupBridge queue = Queue(
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
                Is.EqualTo(PickupDeliveryDisposition.Rejected));
        }

        [Test]
        public void ConflictingRealization_IsQuarantinedAndNeverRetried()
        {
            var runtime = new FakeLive { ConflictingRejects = 1 };
            PendingAdmissionPickupBridge queue = Queue(
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
            PendingTerminalDropAdmissionResult admission = Admission();
            var runtime = new FakeLive();
            PendingAdmissionPickupBridge queue = Queue(
                runtime,
                new FakeSourceResolver());

            Assert.That(queue.TryEnqueue(admission).Disposition,
                Is.EqualTo(PickupDeliveryDisposition.Applied));
            Assert.That(queue.TryEnqueue(admission).Disposition,
                Is.EqualTo(PickupDeliveryDisposition.ExactReplay));
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(queue.TryEnqueue(admission).Disposition,
                Is.EqualTo(PickupDeliveryDisposition.ExactReplay));
            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(runtime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeReleaseAndRecomposition_DoNotLoseAdmittedReward()
        {
            var firstRuntime = new FakeLive();
            PendingAdmissionPickupBridge queue = Queue(
                firstRuntime,
                new FakeSourceResolver());
            queue.TryEnqueue(Admission());

            queue.ReleaseRuntime();
            Assert.That(queue.ProcessPending(), Is.Zero);
            Assert.That(queue.PendingCount, Is.EqualTo(1));
            var replacementRuntime = new FakeLive();
            queue.ConfigureRuntime(replacementRuntime);
            Assert.That(queue.ProcessPending(), Is.EqualTo(1));
            Assert.That(replacementRuntime.AcceptedRealizationCount, Is.EqualTo(1));
        }

        private static PendingAdmissionPickupBridge Queue(
            FakeLive runtime,
            IPickupSourcePositionResolver resolver)
        {
            var queue = new PendingAdmissionPickupBridge();
            queue.ConfigureRuntime(runtime);
            queue.RegisterSource(
                RunId,
                1L,
                SourceEntityId,
                SourcePlacementId,
                resolver);
            return queue;
        }

        private static PendingTerminalDropAdmissionResult Admission()
        {
            return new PendingTerminalDropAdmissionState().Admit(
                GeneratedDrop());
        }

        private static GeneratedTerminalDropResult GeneratedDrop()
        {
            StableId operationId = Id(
                "terminaldropoperation",
                "retained-delivery");
            StableId profileId = Id("drop-profile", "money");
            var source = new TerminalDropSourceFact(
                TerminalDropFactKindIds.EnemyDeath,
                Id("terminal", "enemy-death"),
                Id("trigger", "final-hit"),
                RunId,
                1L,
                SourceEntityId,
                SourcePlacementId,
                1L,
                Id("enemy", "generic-droid"),
                ParticipantId,
                ActorId,
                Id("damage", "kinetic"),
                profileId,
                "source-context-fingerprint",
                "definition-fingerprint",
                "upstream-fingerprint");
            RewardOperationRequest operation = RewardOperationRequest.Create(
                RunId,
                SourceEntityId,
                operationId,
                Id("commitment", "money"),
                profileId,
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            var child = new GeneratedTerminalDropReward(
                Id("terminaldropchild", "money-child"),
                0,
                Id("grant", "money"),
                RewardGrantKind.Money,
                Id("currency", "credits"),
                5L);
            return new GeneratedTerminalDropResult(
                TerminalDropBindingStatus.Accepted,
                TerminalDropRejectionCode.None,
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

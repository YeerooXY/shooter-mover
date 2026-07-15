#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Movement
{
    public sealed class MovementThrusterEvidenceScenarioTests : InputTestFixture
    {
        private const double Tolerance = 0.00001d;
        private Keyboard keyboard;
        private MovementThrusterEvidenceProfile profile;
        private MovementThrusterEvidenceFixture fixture;

        [UnitySetUp]
        public IEnumerator SetUpArena()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            profile = MovementThrusterEvidenceFixture.LoadProfile();
            yield return MovementThrusterEvidenceFixture.LoadArena();
        }

        [UnityTearDown]
        public IEnumerator TearDownArena()
        {
            DisposeFixture();
            if (keyboard != null && keyboard.added)
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.Update();
            }
            yield return MovementThrusterEvidenceFixture.UnloadArena();
        }

        [UnityTest]
        public IEnumerator FrozenProfile_BindsConfigurationPerformanceManifestAndReview()
        {
            Assert.That(profile.BuildTuning().Fingerprint,
                Is.EqualTo(MovementThrusterEvidenceFixture.ExpectedTuningFingerprint));
            Assert.That(profile.scenarios.Length, Is.EqualTo(10));
            Assert.That(profile.evidence.technicalValidity, Is.EqualTo("cs-012-monotonic"));
            Assert.That(profile.evidence.performanceCapture,
                Is.EqualTo("eh-007-bounded-observation"));
            Assert.That(profile.evidence.manifestSchema,
                Is.EqualTo("shooter-mover.evidence-manifest"));
            Assert.That(profile.evidence.reviewProtocol,
                Is.EqualTo("shooter-mover.stage1-evidence-protocol"));

            string arena = MovementThrusterEvidenceFixture.CaptureArenaSnapshot();
            Assert.That(arena, Does.Contain("socket.player.primary"));
            string audit = MovementThrusterEvidenceFixture.CaptureRestartAudit();
            Assert.That(audit, Does.Contain("state=Ended"));
            Assert.That(audit, Does.Contain("current_attempt_id=attempt.mt-012-2"));
            Assert.That(audit, Does.Contain("parent_attempt_id=attempt.mt-012-1"));
            string performance =
                MovementThrusterEvidenceFixture.CapturePerformanceSummary(10, 512);
            Assert.That(performance, Does.StartWith("state=completed\n"));
            Assert.That(performance, Does.Contain("frame_sample_count=5"));
            Assert.That(performance,
                Does.Contain("counter_id=objects.movement-evidence-steps"));
            yield break;
        }

        [UnityTest]
        public IEnumerator
            LocomotionBurstChainSteeringForgivenessAndExit_ProduceDeterministicTrace()
        {
            string first = RunMotionScenario(out string firstEvidence);
            string second = RunMotionScenario(out string secondEvidence);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(firstEvidence, Does.Contain("technical_validity=valid"));
            Assert.That(firstEvidence, Does.Contain("gameplay_outcome=negative"));
            Assert.That(firstEvidence,
                Does.Contain("manifest_schema=shooter-mover.evidence-manifest"));
            Assert.That(firstEvidence, Does.Contain("eh007_summary_begin"));
            Assert.That(firstEvidence, Does.Contain("payload_sha256=sha256:"));
            Assert.That(secondEvidence, Does.Contain("within_bounds=true"));
            TestContext.WriteLine("MT-012-EVIDENCE-BEGIN core-motion");
            TestContext.WriteLine(firstEvidence);
            TestContext.WriteLine("MT-012-EVIDENCE-END core-motion");
            yield break;
        }

        [UnityTest]
        public IEnumerator ReflectionShoveBlockFocusAndRestart_CoverContactBoundaries()
        {
            fixture = NewFixture();
            Stopwatch timer = Stopwatch.StartNew();

            fixture.Queue(Key.A);
            ThrusterStatusSnapshot state = null;
            for (int i = 0; i < 10; i++) state = fixture.Step("wall-approach-" + i);
            Assert.That(state.VelocityX, Is.LessThan(0d));
            Collider2D wall = fixture.Wall("Wall West");
            Assert.That(
                fixture.Process(1L, wall, Vector2.right, 1d, "wall-reflection"),
                Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(fixture.Lifecycle.Actor.CurrentVelocityX, Is.GreaterThan(0d));

            fixture.Restart();
            Neutralize();
            fixture.Queue(Key.A);
            for (int i = 0; i < 10; i++) fixture.Step("light-approach-" + i);
            double incoming = fixture.Lifecycle.Actor.CurrentVelocityX;
            Collider2D light = fixture.Weighted(
                "light", CombatWeightClass.Heavy, CombatWeightClass.Light);
            Assert.That(
                fixture.Process(2L, light, Vector2.right, 2d, "light-shove"),
                Is.EqualTo(MovementContact2DProcessResult.EnemyResolved));
            Assert.That(fixture.Lifecycle.Actor.CurrentVelocityX, Is.LessThan(0d));
            Assert.That(Math.Abs(fixture.Lifecycle.Actor.CurrentVelocityX),
                Is.LessThan(Math.Abs(incoming)));

            fixture.Restart();
            Neutralize();
            fixture.Queue(Key.A);
            for (int i = 0; i < 10; i++) fixture.Step("heavy-approach-" + i);
            Collider2D heavy = fixture.Weighted(
                "heavy", CombatWeightClass.Light, CombatWeightClass.Heavy);
            Assert.That(
                fixture.Process(3L, heavy, Vector2.right, 3d, "heavy-block"),
                Is.EqualTo(MovementContact2DProcessResult.EnemyResolved));
            Assert.That(fixture.Lifecycle.Actor.CurrentVelocityX,
                Is.EqualTo(0d).Within(Tolerance));

            fixture.Restart();
            Neutralize();
            fixture.Queue(Key.D, Key.Space);
            state = fixture.Step("before-focus-loss");
            int charges = state.AvailableCharges;
            fixture.SetFocus(false);
            state = fixture.Step("focus-loss-boundary");
            Assert.That(fixture.Input.IsAcceptingInput, Is.False);
            Assert.That(state.SteeringIntentX, Is.Zero);
            Assert.That(state.SteeringIntentY, Is.Zero);
            Assert.That(state.AvailableCharges, Is.EqualTo(charges));

            Assert.That(fixture.Contact.BeginFixedStep(7L), Is.True);
            long generation = fixture.Lifecycle.Actor.Generation;
            fixture.Restart();
            Assert.That(fixture.Lifecycle.Actor.Generation, Is.EqualTo(generation + 1));
            MovementContact2DProcessResult pending =
                fixture.Contact.TryProcessContact(wall, Vector2.right, 7d);
            fixture.Event("restart-pending-contact", pending);
            Assert.That(pending, Is.EqualTo(MovementContact2DProcessResult.AuthorityUnavailable));
            Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));

            fixture.SetFocus(true);
            fixture.Step("stale-held-suppressed");
            Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Neutralize();
            fixture.Queue(Key.D, Key.Space);
            state = fixture.Step("fresh-after-restart");
            Assert.That(state.IsBursting, Is.True);
            Assert.That(state.AvailableCharges, Is.EqualTo(state.MaximumCharges - 1));

            timer.Stop();
            string evidence = fixture.BuildEvidence(
                "contact-and-restart.matrix",
                "valid", "none", "mixed",
                "observation.contact-and-restart-review",
                timer.Elapsed.TotalMilliseconds);
            Assert.That(evidence, Does.Contain("wall-reflection|value=WallReflected"));
            Assert.That(evidence, Does.Contain("light-shove|value=EnemyResolved"));
            Assert.That(evidence, Does.Contain("heavy-block|value=EnemyResolved"));
            Assert.That(evidence, Does.Contain("AuthorityUnavailable"));
            Assert.That(evidence, Does.Contain("technical_validity=valid"));
            Assert.That(evidence, Does.Contain("gameplay_outcome=mixed"));
            Assert.That(evidence, Does.Contain("within_bounds=true"));
            TestContext.WriteLine("MT-012-EVIDENCE-BEGIN contact-restart");
            TestContext.WriteLine(evidence);
            TestContext.WriteLine("MT-012-EVIDENCE-END contact-restart");
            yield break;
        }

        [UnityTest]
        public IEnumerator TechnicalInvalidity_RemainsMonotonicAndSeparateFromFunObservation()
        {
            fixture = NewFixture();
            string evidence = fixture.BuildEvidence(
                "technical.invalid-session",
                "invalid",
                "missing-required-asset,timeout",
                "positive",
                "observation.movement-felt-responsive",
                0d);
            Assert.That(evidence, Does.Contain("technical_validity=invalid"));
            Assert.That(evidence, Does.Contain("missing-required-asset,timeout"));
            Assert.That(evidence, Does.Contain("gameplay_outcome=positive"));
            Assert.That(evidence, Does.Not.Contain("automatic_approval=true"));
            Assert.That(evidence,
                Does.Contain("review_protocol=shooter-mover.stage1-evidence-protocol"));
            TestContext.WriteLine("MT-012-EVIDENCE-BEGIN intentional-invalidity");
            TestContext.WriteLine(evidence);
            TestContext.WriteLine("MT-012-EVIDENCE-END intentional-invalidity");
            yield break;
        }

        private string RunMotionScenario(out string evidence)
        {
            DisposeFixture();
            fixture = NewFixture();
            Stopwatch timer = Stopwatch.StartNew();

            fixture.Queue(Key.D);
            ThrusterStatusSnapshot state = null;
            for (int i = 0; i < 20; i++) state = fixture.Step("accelerate-" + i);
            fixture.Capture("locomotion-accelerated");
            Assert.That(state.VelocityX, Is.GreaterThan(0d));
            Assert.That(state.VelocityX, Is.LessThanOrEqualTo(fixture.Tuning.BaseMaximumSpeed));

            double accelerated = state.VelocityX;
            fixture.Queue();
            state = fixture.Step("locomotion-braking");
            Assert.That(state.VelocityX, Is.LessThan(accelerated));

            fixture.Queue(Key.D, Key.Space);
            state = fixture.Step("burst-started");
            Assert.That(state.IsBursting, Is.True);
            Assert.That(state.AvailableCharges, Is.EqualTo(state.MaximumCharges - 1));

            fixture.Queue(Key.W);
            state = fixture.Step("startup-forgiveness");
            Assert.That(state.BurstDirectionY, Is.GreaterThan(0.99d));
            Assert.That(Math.Abs(state.BurstDirectionX), Is.LessThan(0.01d));

            fixture.Queue(Key.W);
            fixture.Step("chain-window", 0.04d);
            fixture.Queue(Key.W, Key.Space);
            state = fixture.Step("chain-activated");
            Assert.That(state.IsBursting, Is.True);
            Assert.That(state.AvailableCharges, Is.EqualTo(0));

            fixture.Queue(Key.W);
            fixture.Step("steering-window-closed", 0.06d);
            fixture.Queue(Key.A);
            state = fixture.Step("bounded-steering");
            Assert.That(state.BurstDirectionX, Is.LessThan(0d));
            Assert.That(state.BurstDirectionY, Is.GreaterThan(0.9d));

            fixture.Queue();
            state = fixture.Step("exit-momentum", 0.25d);
            Assert.That(state.BurstPhase, Is.EqualTo(ThrusterBurstPhase.ExitMomentum));
            state = fixture.Step("ready-after-exit", 0.2d);
            Assert.That(state.BurstPhase, Is.EqualTo(ThrusterBurstPhase.Ready));
            Assert.That(Math.Sqrt(state.VelocityX * state.VelocityX
                + state.VelocityY * state.VelocityY),
                Is.LessThanOrEqualTo(fixture.Tuning.BaseMaximumSpeed + Tolerance));

            timer.Stop();
            string trace = fixture.Trace;
            evidence = fixture.BuildEvidence(
                "movement.core-motion",
                "valid", "none", "negative",
                "observation.core-motion-feel-not-accepted",
                timer.Elapsed.TotalMilliseconds);
            DisposeFixture();
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            InputSystem.Update();
            return trace;
        }

        private MovementThrusterEvidenceFixture NewFixture()
        {
            Scene arena = SceneManager.GetSceneByName(MovementThrusterEvidenceFixture.ArenaName);
            Assert.That(arena.IsValid() && arena.isLoaded, Is.True);
            return new MovementThrusterEvidenceFixture(arena, keyboard, profile);
        }

        private void Neutralize()
        {
            fixture.Queue();
            fixture.Step("neutral-boundary");
        }

        private void DisposeFixture()
        {
            if (fixture == null) return;
            fixture.Dispose();
            fixture = null;
        }
    }
}
#endif

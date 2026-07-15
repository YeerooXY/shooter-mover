#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Combat;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class FourMountCombatEvidenceScenarioTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private FourMountCombatEvidenceProfile profile;
        private FourMountCombatEvidenceFixture fixture;

        [UnitySetUp]
        public IEnumerator SetUpArena()
        {
            profile = FourMountCombatEvidenceFixture.LoadProfile();
            yield return FourMountCombatEvidenceFixture.LoadArena();
        }

        [UnityTearDown]
        public IEnumerator TearDownArena()
        {
            DisposeFixture();
            keyboard = null;
            mouse = null;
            yield return FourMountCombatEvidenceFixture.UnloadArena();
        }

        [UnityTest]
        public IEnumerator FrozenProfile_BindsArenaLifecyclePerformanceManifestAndReview()
        {
            Assert.That(profile.BuildRuntimeProfiles().Length, Is.EqualTo(4));
            Assert.That(profile.scenarios.Length, Is.EqualTo(11));
            Assert.That(profile.evidence.technicalValidity, Is.EqualTo("cs-012-monotonic"));
            Assert.That(
                profile.evidence.performanceCapture,
                Is.EqualTo("eh-007-bounded-observation"));
            Assert.That(
                profile.evidence.manifestSchema,
                Is.EqualTo("shooter-mover.evidence-manifest"));
            Assert.That(
                profile.evidence.reviewProtocol,
                Is.EqualTo("shooter-mover.stage1-evidence-protocol"));

            string arena = FourMountCombatEvidenceFixture.CaptureArenaSnapshot();
            Assert.That(arena, Does.Contain("socket.player.primary"));
            string audit = FourMountCombatEvidenceFixture.CaptureRestartAudit();
            Assert.That(audit, Does.Contain("state=Ended"));
            Assert.That(audit, Does.Contain("current_attempt_id=attempt.cb-011-2"));
            Assert.That(audit, Does.Contain("parent_attempt_id=attempt.cb-011-1"));
            string performance =
                FourMountCombatEvidenceFixture.CapturePerformanceSummary(16, 256);
            Assert.That(performance, Does.StartWith("state=completed\n"));
            Assert.That(performance, Does.Contain("frame_sample_count=5"));
            Assert.That(
                performance,
                Does.Contain("counter_id=objects.combat-evidence-steps"));
            yield break;
        }

        [UnityTest]
        public IEnumerator SharedIntentCadencePowerFaultAndNoRecoil_AreDeterministic()
        {
            byte[] firstTrace = RunCoreScenario(out string firstEvidence);
            byte[] secondTrace = RunCoreScenario(out string secondEvidence);

            CollectionAssert.AreEqual(firstTrace, secondTrace);
            Assert.That(firstEvidence, Does.Contain("technical_validity=valid"));
            Assert.That(firstEvidence, Does.Contain("within_bounds=true"));
            Assert.That(firstEvidence, Does.Contain("diagnostics_begin"));
            Assert.That(firstEvidence, Does.Contain("eh007_summary_begin"));
            Assert.That(firstEvidence, Does.Contain("manifest_schema=shooter-mover.evidence-manifest"));
            Assert.That(firstEvidence, Does.Contain("payload_sha256=sha256:"));
            Assert.That(secondEvidence, Does.Contain("trace_sha256=sha256:"));
            TestContext.WriteLine("CB-011-EVIDENCE-BEGIN combat-core");
            TestContext.WriteLine(firstEvidence);
            TestContext.WriteLine("CB-011-EVIDENCE-END combat-core");
            yield break;
        }

        [UnityTest]
        public IEnumerator FocusLossRapidRestartAndStalePlan_AreLifecycleSafe()
        {
            fixture = NewFixture();
            Stopwatch timer = Stopwatch.StartNew();

            fixture.Queue(new Vector2(0.6f, 0.8f), Key.LeftCtrl, Key.LeftShift);
            PlayerIntentFrame active = fixture.ReadCombatIntent();
            Assert.That(active.Fire.IsHeld, Is.True);
            Assert.That(active.PowerModifier.IsHeld, Is.True);

            fixture.SetFocus(false);
            PlayerIntentFrame boundary = fixture.ReadCombatIntent();
            Assert.That(boundary.WasFocusLost, Is.True);
            Assert.That(boundary.Fire, Is.EqualTo(ButtonIntent.Released));
            Assert.That(boundary.PowerModifier, Is.EqualTo(ButtonIntent.Released));
            AssertNeutralCombat(fixture.ReadCombatIntent());

            fixture.SetFocus(true);
            AssertNeutralCombat(fixture.ReadCombatIntent());
            fixture.Queue(Vector2.zero);
            AssertNeutralCombat(fixture.ReadCombatIntent());

            fixture.Queue(new Vector2(0.6f, 0.8f), Key.LeftCtrl);
            FourMountCombatStepResult fired = fixture.Step("pre-restart-fire", 0d);
            WeaponFireExecutionPlan stalePlan =
                fired.GetLaneByStableIndex(0).ExecutionPlan;
            Assert.That(stalePlan, Is.Not.Null);

            WeaponMount2DExecutionStatus staleStatus =
                fixture.RestartRejectingStalePlan(stalePlan);
            Assert.That(staleStatus, Is.EqualTo(WeaponMount2DExecutionStatus.NotConfigured));
            Assert.That(fixture.CaptureFrozenState(), Is.EqualTo(fixture.FrozenInitialState));
            AssertNeutralCombat(fixture.ReadCombatIntent());

            for (int restart = 1; restart < 50; restart++)
            {
                fixture.RestartRejectingStalePlan(null);
                Assert.That(
                    fixture.CaptureFrozenState(),
                    Is.EqualTo(fixture.FrozenInitialState),
                    "Restart " + (restart + 1) + " drifted from the frozen state.");
                AssertNeutralCombat(fixture.ReadCombatIntent());
            }

            fixture.Queue(new Vector2(0.6f, 0.8f), Key.LeftCtrl);
            PlayerIntentFrame fresh = fixture.ReadCombatIntent();
            Assert.That(fresh.Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(fixture.RestartCount, Is.EqualTo(50));

            timer.Stop();
            string evidence = fixture.BuildEvidence(
                "session.rapid-restart-frozen-state",
                "valid",
                "none",
                "not-reviewed",
                "observation.lifecycle-only",
                timer.Elapsed.TotalMilliseconds);
            Assert.That(evidence, Does.Contain("restart_count=50"));
            Assert.That(evidence, Does.Contain("stale_plan=NotConfigured"));
            Assert.That(evidence, Does.Contain("technical_validity=valid"));
            Assert.That(evidence, Does.Contain("human_playable_review=not-executed"));
            TestContext.WriteLine("CB-011-EVIDENCE-BEGIN lifecycle");
            TestContext.WriteLine(evidence);
            TestContext.WriteLine("CB-011-EVIDENCE-END lifecycle");
            yield break;
        }

        [UnityTest]
        public IEnumerator ValidAndIntentionallyInvalidSessions_RemainSeparatelyClassified()
        {
            fixture = NewFixture();
            string valid = fixture.BuildEvidence(
                "validity.valid-session",
                "valid",
                "none",
                "negative",
                "observation.combat-feel-not-accepted",
                0d);
            string invalid = fixture.BuildEvidence(
                "validity.intentional-invalid-session",
                "invalid",
                "checksum-drift,missing-required-proof,conflicting-validity",
                "positive",
                "observation.fun-result-cannot-override-technical-invalidity",
                0d);

            Assert.That(valid, Does.Contain("technical_validity=valid"));
            Assert.That(valid, Does.Contain("gameplay_outcome=negative"));
            Assert.That(invalid, Does.Contain("technical_validity=invalid"));
            Assert.That(invalid, Does.Contain("technical_invalid_requires_rerun=true"));
            Assert.That(
                invalid,
                Does.Contain("checksum-drift,missing-required-proof,conflicting-validity"));
            Assert.That(invalid, Does.Contain("gameplay_outcome=positive"));
            Assert.That(invalid, Does.Not.Contain("automatic_approval=true"));
            Assert.That(
                invalid,
                Does.Contain("review_protocol=shooter-mover.stage1-evidence-protocol"));
            TestContext.WriteLine("CB-011-EVIDENCE-BEGIN intentional-invalidity");
            TestContext.WriteLine(invalid);
            TestContext.WriteLine("CB-011-EVIDENCE-END intentional-invalidity");
            yield break;
        }

        [UnityTest]
        public IEnumerator EvidenceExplicitlyLeavesContentVisualHudAndBalanceWorkIncomplete()
        {
            fixture = NewFixture();
            string[] expected =
            {
                "five-stage-1-weapon-packages-not-complete",
                "final-audiovisual-identity-not-complete",
                "hud-visuals-not-complete",
                "encounter-balance-not-complete"
            };
            CollectionAssert.AreEquivalent(expected, profile.evidence.incompleteClaims);

            string evidence = fixture.BuildEvidence(
                "scope.incomplete-stage1-claims",
                "valid",
                "none",
                "not-reviewed",
                "observation.scope-only",
                0d);
            Assert.That(evidence, Does.Contain("five_stage1_weapon_packages=not-complete"));
            Assert.That(evidence, Does.Contain("final_audiovisual_identity=not-complete"));
            Assert.That(evidence, Does.Contain("hud_visuals=not-complete"));
            Assert.That(evidence, Does.Contain("encounter_balance=not-complete"));
            Assert.That(evidence, Does.Contain("formal_manifested_package_proof=not-executed"));
            Assert.That(evidence, Does.Contain("windows_player_proof=not-executed"));
            Assert.That(evidence, Does.Contain("human_playable_review=not-executed"));
            yield break;
        }

        private byte[] RunCoreScenario(out string evidence)
        {
            DisposeFixture();
            fixture = NewFixture();
            Stopwatch timer = Stopwatch.StartNew();
            string movementBefore = fixture.CaptureMovementAuthority();

            fixture.Queue(
                new Vector2(
                    (float)profile.combat.sharedAimX,
                    (float)profile.combat.sharedAimY),
                Key.LeftCtrl,
                Key.LeftShift);
            FourMountCombatStepResult simultaneous =
                fixture.Step("shared-fire-power", 0d);

            Assert.That(simultaneous.State.ToTraceString().Split('\n').Length, Is.EqualTo(4));
            for (int index = 0; index < FourMountCombatState.MountCount; index++)
            {
                Assert.That(
                    simultaneous.GetLaneByStableIndex(index).StableSlotNumber,
                    Is.EqualTo(index + 1));
                Assert.That(simultaneous.GetLaneByStableIndex(index).ShotsFired, Is.EqualTo(1));
                Assert.That(simultaneous.GetLaneByStableIndex(index).ExecutionPlan, Is.Not.Null);
            }

            Assert.That(
                simultaneous.GetLaneByStableIndex(0).PowerDecision.Kind,
                Is.EqualTo(WeaponPowerFireDecisionKind.EmpoweredFired));
            Assert.That(
                simultaneous.GetLaneByStableIndex(1).PowerDecision.Kind,
                Is.EqualTo(WeaponPowerFireDecisionKind.EmpoweredFired));
            Assert.That(
                simultaneous.GetLaneByStableIndex(2).PowerDecision.Kind,
                Is.EqualTo(WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(
                simultaneous.GetLaneByStableIndex(3).PowerDecision.Kind,
                Is.EqualTo(WeaponPowerFireDecisionKind.EmpoweredFired));
            Assert.That(simultaneous.State.GetPowerBankByStableIndex(0).AvailableUnits, Is.EqualTo(5d));
            Assert.That(simultaneous.State.GetPowerBankByStableIndex(1).AvailableUnits, Is.EqualTo(0d));
            Assert.That(simultaneous.State.GetPowerBankByStableIndex(2).AvailableUnits, Is.EqualTo(0d));
            Assert.That(simultaneous.State.GetPowerBankByStableIndex(3).AvailableUnits, Is.EqualTo(5d));

            fixture.Queue(
                new Vector2(
                    (float)profile.combat.sharedAimX,
                    (float)profile.combat.sharedAimY),
                Key.LeftCtrl,
                Key.LeftShift);
            FourMountCombatStepResult mixed = fixture.Step("mixed-readiness", 0.075d);
            Assert.That(mixed.GetLaneByStableIndex(0).ShotsFired, Is.EqualTo(1));
            Assert.That(mixed.GetLaneByStableIndex(1).ShotsFired, Is.EqualTo(0));
            Assert.That(mixed.GetLaneByStableIndex(2).ShotsFired, Is.EqualTo(0));
            Assert.That(mixed.GetLaneByStableIndex(3).ShotsFired, Is.EqualTo(0));

            string[] faults = new string[FourMountCombatState.MountCount];
            faults[1] = "CB-011 injected slot-two evidence fault";
            fixture.Queue(
                new Vector2(
                    (float)profile.combat.sharedAimX,
                    (float)profile.combat.sharedAimY),
                Key.LeftCtrl);
            FourMountCombatStepResult isolated = fixture.Step("single-fault", 0.25d, faults);
            Assert.That(isolated.GetLaneByStableIndex(1).IsFaulted, Is.True);
            Assert.That(isolated.GetLaneByStableIndex(1).ShotsFired, Is.EqualTo(0));
            Assert.That(isolated.GetLaneByStableIndex(0).ShotsFired, Is.GreaterThanOrEqualTo(1));
            Assert.That(isolated.GetLaneByStableIndex(2).ShotsFired, Is.GreaterThanOrEqualTo(1));
            Assert.That(isolated.GetLaneByStableIndex(3).ShotsFired, Is.GreaterThanOrEqualTo(1));

            string movementAfter = fixture.CaptureMovementAuthority();
            Assert.That(movementAfter, Is.EqualTo(movementBefore));
            Assert.That(fixture.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.PlayerCollider.enabled, Is.True);

            timer.Stop();
            byte[] trace = Encoding.UTF8.GetBytes(fixture.Trace);
            evidence = fixture.BuildEvidence(
                "determinism.byte-identical-bounded-trace",
                "valid",
                "none",
                "not-reviewed",
                "observation.combat-foundation-only",
                timer.Elapsed.TotalMilliseconds);
            DisposeFixture();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueDeltaStateEvent(mouse.delta, Vector2.zero);
            InputSystem.Update();
            return trace;
        }

        private FourMountCombatEvidenceFixture NewFixture()
        {
            Scene arena = SceneManager.GetSceneByName(FourMountCombatEvidenceFixture.ArenaName);
            Assert.That(arena.IsValid() && arena.isLoaded, Is.True);
            if (keyboard == null || !keyboard.added)
                keyboard = InputSystem.AddDevice<Keyboard>();
            if (mouse == null || !mouse.added)
                mouse = InputSystem.AddDevice<Mouse>();
            return new FourMountCombatEvidenceFixture(arena, keyboard, mouse, profile);
        }

        private static void AssertNeutralCombat(PlayerIntentFrame frame)
        {
            Assert.That(frame.Aim, Is.EqualTo(NormalizedIntentVector2.Zero));
            Assert.That(frame.Fire, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.PowerModifier, Is.EqualTo(ButtonIntent.Inactive));
            Assert.That(frame.WasFocusLost, Is.False);
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

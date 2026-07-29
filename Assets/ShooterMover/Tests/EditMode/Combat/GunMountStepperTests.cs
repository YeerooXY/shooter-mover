using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ContractCombat = ShooterMover.Contracts.Combat;
using DomainMountState = ShooterMover.Domain.Combat.GunMountState;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class GunMountStepperTests
    {
        private static readonly StableId AutomaticModule = StableId.Parse("behavior.automatic");

        [Test]
        public void MixedCadence_HeldFireAdvancesMountsIndependently()
        {
            GunLiveProfile fast = BuildProfile(
                profileSuffix: "fast",
                cadenceSeconds: 0.1d,
                recoverySeconds: 0d);
            GunLiveProfile slow = BuildProfile(
                profileSuffix: "slow",
                cadenceSeconds: 0.25d,
                recoverySeconds: 0d);

            GunMountStepResult fastResult = GunMountStepper.Step(
                fast,
                DomainMountState.Initial(fast),
                0.5d,
                true);
            GunMountStepResult slowResult = GunMountStepper.Step(
                slow,
                DomainMountState.Initial(slow),
                0.5d,
                true);

            AssertSequentially(() =>
            {
                Assert.That(fastResult.ShotsFired, Is.EqualTo(6));
                Assert.That(slowResult.ShotsFired, Is.EqualTo(3));
                Assert.That(fastResult.State.TotalShotsFired, Is.EqualTo(6L));
                Assert.That(slowResult.State.TotalShotsFired, Is.EqualTo(3L));
                Assert.That(fastResult.State, Is.Not.SameAs(slowResult.State));
                Assert.That(fastResult.State.IsFaulted, Is.False);
                Assert.That(slowResult.State.IsFaulted, Is.False);
            });
        }

        [Test]
        public void BurstRelease_InterruptsRemainingShotsAndEntersRecovery()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "burst",
                cadenceSeconds: 0.4d,
                burstShotCount: 3,
                burstShotIntervalSeconds: 0.1d,
                recoverySeconds: 0.2d);

            GunMountStepResult started = GunMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            GunMountStepResult released = GunMountStepper.Step(
                profile,
                started.State,
                0.05d,
                false);

            AssertSequentially(() =>
            {
                Assert.That(started.ShotsFired, Is.EqualTo(1));
                Assert.That(started.State.Phase, Is.EqualTo(GunMountPhase.Firing));
                Assert.That(started.State.BurstShotsRemaining, Is.EqualTo(2));
                Assert.That(released.ShotsFired, Is.Zero);
                Assert.That(released.BurstInterrupted, Is.True);
                Assert.That(released.State.BurstShotsRemaining, Is.Zero);
                Assert.That(released.State.BurstIntervalRemainingSeconds, Is.Zero);
                Assert.That(released.State.RecoveryRemainingSeconds, Is.EqualTo(0.15d).Within(0.000000001d));
                Assert.That(released.State.Phase, Is.EqualTo(GunMountPhase.Recovering));
            });
        }

        [Test]
        public void HeatOverheat_DepletesUntilFullCooldownBoundary()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "heat",
                cadenceSeconds: 0.1d,
                burstShotCount: 3,
                burstShotIntervalSeconds: 0.05d,
                recoverySeconds: 0.2d,
                cycleMode: GunCycleMode.Heat,
                heatCapacityUnits: 4d,
                heatPerShotUnits: 4d,
                heatRecoveryUnitsPerSecond: 2d);

            GunMountStepResult overheated = GunMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            GunMountStepResult almostCool = GunMountStepper.Step(
                profile,
                overheated.State,
                1.999d,
                false);
            GunMountStepResult cooled = GunMountStepper.Step(
                profile,
                almostCool.State,
                0.001d,
                false);

            AssertSequentially(() =>
            {
                Assert.That(overheated.ShotsFired, Is.EqualTo(1));
                Assert.That(overheated.BurstInterrupted, Is.True);
                Assert.That(overheated.State.Phase, Is.EqualTo(GunMountPhase.Depleted));
                Assert.That(overheated.State.HeatUnits, Is.EqualTo(4d));
                Assert.That(overheated.State.HeatRecoveryLocked, Is.True);
                Assert.That(almostCool.State.Phase, Is.EqualTo(GunMountPhase.Depleted));
                Assert.That(almostCool.State.HeatUnits, Is.GreaterThan(0d));
                Assert.That(cooled.State.HeatUnits, Is.Zero);
                Assert.That(cooled.State.HeatRecoveryLocked, Is.False);
                Assert.That(cooled.State.Phase, Is.EqualTo(GunMountPhase.Ready));
            });
        }

        [Test]
        public void ChargeCompletion_TransitionsAtExactBoundary()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "charge",
                cadenceSeconds: 0.1d,
                recoverySeconds: 0d,
                cycleMode: GunCycleMode.Charge,
                chargeSeconds: 0.5d);

            GunMountStepResult fired = GunMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            GunMountStepResult beforeBoundary = GunMountStepper.Step(
                profile,
                fired.State,
                0.499d,
                false);
            GunMountStepResult atBoundary = GunMountStepper.Step(
                profile,
                beforeBoundary.State,
                0.001d,
                false);

            AssertSequentially(() =>
            {
                Assert.That(fired.State.Phase, Is.EqualTo(GunMountPhase.Depleted));
                Assert.That(fired.State.ChargeProgressSeconds, Is.Zero);
                Assert.That(beforeBoundary.State.Phase, Is.EqualTo(GunMountPhase.Depleted));
                Assert.That(beforeBoundary.State.ChargeProgressSeconds, Is.EqualTo(0.499d).Within(0.000000001d));
                Assert.That(atBoundary.State.ChargeProgressSeconds, Is.EqualTo(0.5d));
                Assert.That(atBoundary.State.Phase, Is.EqualTo(GunMountPhase.Ready));
            });
        }

        [Test]
        public void RecoveryAndCadence_BlockUntilBothComplete()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "recovery",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0.5d);

            GunMountStepResult fired = GunMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            GunMountStepResult cadenceComplete = GunMountStepper.Step(
                profile,
                fired.State,
                0.2d,
                false);
            GunMountStepResult recoveryComplete = GunMountStepper.Step(
                profile,
                cadenceComplete.State,
                0.3d,
                false);

            AssertSequentially(() =>
            {
                Assert.That(fired.State.Phase, Is.EqualTo(GunMountPhase.Recovering));
                Assert.That(cadenceComplete.State.CadenceRemainingSeconds, Is.Zero);
                Assert.That(cadenceComplete.State.RecoveryRemainingSeconds, Is.EqualTo(0.3d).Within(0.000000001d));
                Assert.That(cadenceComplete.State.Phase, Is.EqualTo(GunMountPhase.Recovering));
                Assert.That(recoveryComplete.State.Phase, Is.EqualTo(GunMountPhase.Ready));
            });
        }

        [Test]
        public void RapidInput_DoesNotQueueARequestWhileBlocked()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "rapid",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0.4d);
            DomainMountState state = DomainMountState.Initial(profile);

            GunMountStepResult initialFire = GunMountStepper.Step(profile, state, 0d, true);
            GunMountStepResult released = GunMountStepper.Step(profile, initialFire.State, 0.1d, false);
            GunMountStepResult tappedWhileBlocked = GunMountStepper.Step(profile, released.State, 0.05d, true);
            GunMountStepResult becameReady = GunMountStepper.Step(profile, tappedWhileBlocked.State, 0.25d, false);
            GunMountStepResult newRequest = GunMountStepper.Step(profile, becameReady.State, 0d, true);

            AssertSequentially(() =>
            {
                Assert.That(initialFire.ShotsFired, Is.EqualTo(1));
                Assert.That(tappedWhileBlocked.ShotsFired, Is.Zero);
                Assert.That(becameReady.ShotsFired, Is.Zero);
                Assert.That(becameReady.State.TotalShotsFired, Is.EqualTo(1L));
                Assert.That(becameReady.State.Phase, Is.EqualTo(GunMountPhase.Ready));
                Assert.That(newRequest.ShotsFired, Is.EqualTo(1));
                Assert.That(newRequest.State.TotalShotsFired, Is.EqualTo(2L));
            });
        }

        [Test]
        public void InvalidElapsedTime_FaultsClosedWithActionableDiagnostic()
        {
            GunLiveProfile profile = BuildProfile(profileSuffix: "elapsed");
            double[] invalidValues =
            {
                -0.01d,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            };

            foreach (double invalid in invalidValues)
            {
                GunMountStepResult result = GunMountStepper.Step(
                    profile,
                    DomainMountState.Initial(profile),
                    invalid,
                    true);

                AssertSequentially(() =>
                {
                    Assert.That(result.Succeeded, Is.False);
                    Assert.That(result.ShotsFired, Is.Zero);
                    Assert.That(result.State.Phase, Is.EqualTo(GunMountPhase.Faulted));
                    Assert.That(result.Fault.Kind, Is.EqualTo(GunMountFaultKind.InvalidElapsedTime));
                    Assert.That(result.Fault.Detail, Does.Contain("elapsedSeconds"));
                });
            }
        }

        [Test]
        public void MalformedState_FaultsClosedInsteadOfAdvancingImpossiblePhase()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "malformed",
                cadenceSeconds: 0.5d);
            ConstructorInfo constructor = typeof(DomainMountState)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();

            DomainMountState malformed = (DomainMountState)constructor.Invoke(
                new object[]
                {
                    GunMountPhase.Ready,
                    0.25d,
                    0,
                    0d,
                    0d,
                    0d,
                    false,
                    0d,
                    0L,
                    0L,
                    null,
                });

            GunMountStepResult result = GunMountStepper.Step(
                profile,
                malformed,
                0.1d,
                true);

            AssertSequentially(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.ShotsFired, Is.Zero);
                Assert.That(result.State.TotalShotsFired, Is.Zero);
                Assert.That(result.Fault.Kind, Is.EqualTo(GunMountFaultKind.MalformedState));
                Assert.That(result.Fault.Detail, Does.Contain("Impossible phase transition"));
            });
        }

        [Test]
        public void FaultDuringRecovery_IsTerminalAndDoesNotChangeAnotherMount()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "fault",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0.5d);
            GunMountStepResult firstFired = GunMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            GunMountStepResult secondFired = GunMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);

            GunMountStepResult faulted = GunMountStepper.Step(
                profile,
                firstFired.State,
                0.1d,
                GunMountStepInput.Fault("synthetic actuator fault"));
            GunMountStepResult independent = GunMountStepper.Step(
                profile,
                secondFired.State,
                0.5d,
                false);
            GunMountStepResult terminal = GunMountStepper.Step(
                profile,
                faulted.State,
                10d,
                true);

            AssertSequentially(() =>
            {
                Assert.That(faulted.State.Phase, Is.EqualTo(GunMountPhase.Faulted));
                Assert.That(faulted.Fault.Kind, Is.EqualTo(GunMountFaultKind.ExternalFault));
                Assert.That(faulted.Fault.Detail, Does.Contain("actuator"));
                Assert.That(independent.State.Phase, Is.EqualTo(GunMountPhase.Ready));
                Assert.That(independent.State.TotalShotsFired, Is.EqualTo(1L));
                Assert.That(terminal.State, Is.SameAs(faulted.State));
                Assert.That(terminal.ShotsFired, Is.Zero);
            });
        }

        [Test]
        public void LargeFixedStepCatchUp_MatchesPartitionedFixedSteps()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "catchup",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0d);

            GunMountStepResult single = GunMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                1d,
                true);

            DomainMountState partitionedState = DomainMountState.Initial(profile);
            int partitionedShots = 0;
            for (int index = 0; index < 10; index++)
            {
                GunMountStepResult step = GunMountStepper.Step(
                    profile,
                    partitionedState,
                    0.1d,
                    true);
                partitionedState = step.State;
                partitionedShots += step.ShotsFired;
            }

            AssertSequentially(() =>
            {
                Assert.That(single.ShotsFired, Is.EqualTo(6));
                Assert.That(partitionedShots, Is.EqualTo(single.ShotsFired));
                AssertStatesEquivalent(partitionedState, single.State);
            });
        }

        [Test]
        public void FixedProfileAndInputSequence_ProducesByteStableTraceRows()
        {
            GunLiveProfile profile = BuildProfile(
                profileSuffix: "deterministic",
                cadenceSeconds: 0.3d,
                burstShotCount: 3,
                burstShotIntervalSeconds: 0.05d,
                recoverySeconds: 0.2d);
            double[] elapsed = { 0d, 0.05d, 0.05d, 0.2d, 0.4d };
            bool[] fire = { true, true, false, false, true };

            string[] first = RunTrace(profile, elapsed, fire);
            string[] second = RunTrace(profile, elapsed, fire);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void StateFields_ProjectToFourMountGunsV1HeatAndChargeSnapshots()
        {
            GunLiveProfile heatProfile = BuildProfile(
                profileSuffix: "contractheat",
                cadenceSeconds: 0.2d,
                cycleMode: GunCycleMode.Heat,
                heatCapacityUnits: 5d,
                heatPerShotUnits: 5d,
                heatRecoveryUnitsPerSecond: 1d);
            DomainMountState heatState = GunMountStepper.Step(
                heatProfile,
                DomainMountState.Initial(heatProfile),
                0d,
                true).State;

            ContractCombat.GunMountState heatSnapshot = new ContractCombat.GunMountState(
                ContractCombat.GunMountSlot.MountOne,
                heatProfile.ProfileId,
                ContractCombat.GunMountReadiness.Overheated,
                new ContractCombat.GunCadenceState(
                    heatState.CadenceRemainingSeconds,
                    heatState.BurstShotsRemaining),
                new ContractCombat.GunCycleResourceState(
                    ContractCombat.GunCycleResourceKind.Heat,
                    heatState.HeatUnits,
                    heatProfile.HeatCapacityUnits),
                ContractCombat.GunRecoilState.None,
                ContractCombat.GunPowerBankState.None);

            GunLiveProfile chargeProfile = BuildProfile(
                profileSuffix: "contractcharge",
                cadenceSeconds: 0.2d,
                cycleMode: GunCycleMode.Charge,
                chargeSeconds: 0.5d);
            DomainMountState chargeState = GunMountStepper.Step(
                chargeProfile,
                DomainMountState.Initial(chargeProfile),
                0d,
                true).State;

            ContractCombat.GunMountState chargeSnapshot = new ContractCombat.GunMountState(
                ContractCombat.GunMountSlot.MountTwo,
                chargeProfile.ProfileId,
                ContractCombat.GunMountReadiness.Charging,
                new ContractCombat.GunCadenceState(
                    chargeState.CadenceRemainingSeconds,
                    chargeState.BurstShotsRemaining),
                new ContractCombat.GunCycleResourceState(
                    ContractCombat.GunCycleResourceKind.Charge,
                    chargeState.ChargeProgressSeconds,
                    chargeProfile.ChargeSeconds),
                ContractCombat.GunRecoilState.None,
                ContractCombat.GunPowerBankState.None);

            AssertSequentially(() =>
            {
                Assert.That(heatSnapshot.Readiness, Is.EqualTo(ContractCombat.GunMountReadiness.Overheated));
                Assert.That(heatSnapshot.CycleResource.Current, Is.EqualTo(5d));
                Assert.That(chargeSnapshot.Readiness, Is.EqualTo(ContractCombat.GunMountReadiness.Charging));
                Assert.That(chargeSnapshot.CycleResource.Current, Is.Zero);
                Assert.That(chargeSnapshot.CycleResource.Maximum, Is.EqualTo(0.5d));
            });
        }

        [Test]
        public void FourSyntheticMountTraces_AreIndependentAndLogged()
        {
            GunLiveProfile[] profiles =
            {
                BuildProfile("traceone", 0.1d, recoverySeconds: 0d),
                BuildProfile("tracetwo", 0.5d, 3, 0.05d, 0.1d),
                BuildProfile(
                    "tracethree",
                    0.2d,
                    recoverySeconds: 0.25d,
                    cycleMode: GunCycleMode.Heat,
                    heatCapacityUnits: 3d,
                    heatPerShotUnits: 3d,
                    heatRecoveryUnitsPerSecond: 3d),
                BuildProfile(
                    "tracefour",
                    0.4d,
                    recoverySeconds: 0.35d,
                    cycleMode: GunCycleMode.Charge,
                    chargeSeconds: 0.6d),
            };
            DomainMountState[] states = profiles.Select(DomainMountState.Initial).ToArray();
            double[] elapsed = { 0d, 0.1d, 0.2d, 0.4d };
            bool[] fire = { true, true, false, true };

            for (int tick = 0; tick < elapsed.Length; tick++)
            {
                for (int mount = 0; mount < profiles.Length; mount++)
                {
                    GunMountStepInput input = mount == 3 && tick == 2
                        ? GunMountStepInput.Fault("synthetic mount-four fault")
                        : new GunMountStepInput(fire[tick]);
                    GunMountStepResult result = GunMountStepper.Step(
                        profiles[mount],
                        states[mount],
                        elapsed[tick],
                        input);
                    states[mount] = result.State;

                    TestContext.WriteLine(
                        "mount=" + (mount + 1)
                        + ";tick=" + tick
                        + ";step_shots=" + result.ShotsFired
                        + ";" + result.State.ToTraceString());
                }
            }

            AssertSequentially(() =>
            {
                Assert.That(states[0].TotalShotsFired, Is.GreaterThan(states[1].TotalShotsFired));
                Assert.That(states[2].HeatUnits, Is.GreaterThanOrEqualTo(0d));
                Assert.That(states[3].Phase, Is.EqualTo(GunMountPhase.Faulted));
                Assert.That(states.Take(3).All(state => !state.IsFaulted), Is.True);
                Assert.That(states.Select(state => state.ToTraceString()).Distinct().Count(), Is.EqualTo(4));
            });
        }

        [Test]
        public void StateResultAndStepper_AreImmutableEngineFreeAndSingleMountOnly()
        {
            Type[] immutableTypes =
            {
                typeof(DomainMountState),
                typeof(GunMountStepResult),
                typeof(GunMountFault),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(type.IsSealed, Is.True, type.Name + " must remain sealed.");
                Assert.That(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(property => property.CanWrite),
                    Is.Empty,
                    type.Name + " exposes a writable property.");
            }

            AssertSequentially(() =>
            {
                Assert.That(
                    typeof(DomainMountState).Assembly.GetReferencedAssemblies()
                        .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                    Is.False);
                Assert.That(
                    typeof(DomainMountState).Assembly.GetReferencedAssemblies()
                        .Any(name => name.Name == "ShooterMover.Contracts"),
                    Is.False,
                    "Domain must consume CS-005 semantics through an outward projection, not a reverse assembly reference.");
                Assert.That(
                    typeof(GunMountStepper)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .SelectMany(method => method.GetParameters())
                        .Any(parameter => parameter.ParameterType.IsArray),
                    Is.False,
                    "The one-mount stepper must not accept another mount or a mount collection.");
            });
        }

        [Test]
        public void ZeroLengthBurst_IsRejectedByTheValidatedProfileBoundary()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BuildProfile(
                    profileSuffix: "zeroburst",
                    burstShotCount: 0,
                    burstShotIntervalSeconds: 0d));
        }

        private static void AssertSequentially(Action assertions)
        {
            if (assertions == null)
            {
                throw new ArgumentNullException(nameof(assertions));
            }

            assertions();
        }

        private static string[] RunTrace(
            GunLiveProfile profile,
            double[] elapsed,
            bool[] fire)
        {
            DomainMountState state = DomainMountState.Initial(profile);
            List<string> rows = new List<string>();
            for (int index = 0; index < elapsed.Length; index++)
            {
                GunMountStepResult result = GunMountStepper.Step(
                    profile,
                    state,
                    elapsed[index],
                    fire[index]);
                state = result.State;
                rows.Add(
                    "tick=" + index
                    + ";step_shots=" + result.ShotsFired
                    + ";interrupt=" + (result.BurstInterrupted ? "true" : "false")
                    + ";" + state.ToTraceString());
            }

            return rows.ToArray();
        }

        private static void AssertStatesEquivalent(
            DomainMountState expected,
            DomainMountState actual)
        {
            AssertSequentially(() =>
            {
                Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
                Assert.That(actual.CadenceRemainingSeconds, Is.EqualTo(expected.CadenceRemainingSeconds).Within(0.000000001d));
                Assert.That(actual.BurstShotsRemaining, Is.EqualTo(expected.BurstShotsRemaining));
                Assert.That(actual.BurstIntervalRemainingSeconds, Is.EqualTo(expected.BurstIntervalRemainingSeconds).Within(0.000000001d));
                Assert.That(actual.RecoveryRemainingSeconds, Is.EqualTo(expected.RecoveryRemainingSeconds).Within(0.000000001d));
                Assert.That(actual.HeatUnits, Is.EqualTo(expected.HeatUnits).Within(0.000000001d));
                Assert.That(actual.HeatRecoveryLocked, Is.EqualTo(expected.HeatRecoveryLocked));
                Assert.That(actual.ChargeProgressSeconds, Is.EqualTo(expected.ChargeProgressSeconds).Within(0.000000001d));
                Assert.That(actual.TotalShotsFired, Is.EqualTo(expected.TotalShotsFired));
                Assert.That(actual.TotalCyclesStarted, Is.EqualTo(expected.TotalCyclesStarted));
            });
        }

        private static GunLiveProfile BuildProfile(
            string profileSuffix = "standard",
            double cadenceSeconds = 0.2d,
            int burstShotCount = 1,
            double burstShotIntervalSeconds = 0d,
            double recoverySeconds = 0d,
            GunCycleMode cycleMode = GunCycleMode.None,
            double heatCapacityUnits = 0d,
            double heatPerShotUnits = 0d,
            double heatRecoveryUnitsPerSecond = 0d,
            double chargeSeconds = 0d)
        {
            return GunLiveProfile.Create(
                GunLiveProfile.CurrentProfileVersion,
                StableId.Parse("gun-profile." + profileSuffix),
                cadenceSeconds,
                burstShotCount,
                burstShotIntervalSeconds,
                recoverySeconds,
                cycleMode,
                heatCapacityUnits,
                heatPerShotUnits,
                heatRecoveryUnitsPerSecond,
                chargeSeconds,
                false,
                0d,
                0d,
                0d,
                new[] { AutomaticModule },
                new[] { AutomaticModule },
                0);
        }
    }
}

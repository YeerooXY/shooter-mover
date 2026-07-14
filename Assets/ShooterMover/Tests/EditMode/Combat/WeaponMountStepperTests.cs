using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ContractCombat = ShooterMover.Contracts.Combat;
using DomainMountState = ShooterMover.Domain.Combat.WeaponMountState;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class WeaponMountStepperTests
    {
        private static readonly StableId AutomaticModule = StableId.Parse("behavior.automatic");

        [Test]
        public void MixedCadence_HeldFireAdvancesMountsIndependently()
        {
            WeaponRuntimeProfile fast = BuildProfile(
                profileSuffix: "fast",
                cadenceSeconds: 0.1d,
                recoverySeconds: 0d);
            WeaponRuntimeProfile slow = BuildProfile(
                profileSuffix: "slow",
                cadenceSeconds: 0.25d,
                recoverySeconds: 0d);

            WeaponMountStepResult fastResult = WeaponMountStepper.Step(
                fast,
                DomainMountState.Initial(fast),
                0.5d,
                true);
            WeaponMountStepResult slowResult = WeaponMountStepper.Step(
                slow,
                DomainMountState.Initial(slow),
                0.5d,
                true);

            Assert.Multiple(() =>
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
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "burst",
                cadenceSeconds: 0.4d,
                burstShotCount: 3,
                burstShotIntervalSeconds: 0.1d,
                recoverySeconds: 0.2d);

            WeaponMountStepResult started = WeaponMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            WeaponMountStepResult released = WeaponMountStepper.Step(
                profile,
                started.State,
                0.05d,
                false);

            Assert.Multiple(() =>
            {
                Assert.That(started.ShotsFired, Is.EqualTo(1));
                Assert.That(started.State.Phase, Is.EqualTo(WeaponMountPhase.Firing));
                Assert.That(started.State.BurstShotsRemaining, Is.EqualTo(2));
                Assert.That(released.ShotsFired, Is.Zero);
                Assert.That(released.BurstInterrupted, Is.True);
                Assert.That(released.State.BurstShotsRemaining, Is.Zero);
                Assert.That(released.State.BurstIntervalRemainingSeconds, Is.Zero);
                Assert.That(released.State.RecoveryRemainingSeconds, Is.EqualTo(0.15d).Within(0.000000001d));
                Assert.That(released.State.Phase, Is.EqualTo(WeaponMountPhase.Recovering));
            });
        }

        [Test]
        public void HeatOverheat_DepletesUntilFullCooldownBoundary()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "heat",
                cadenceSeconds: 0.1d,
                burstShotCount: 3,
                burstShotIntervalSeconds: 0.05d,
                recoverySeconds: 0.2d,
                cycleMode: WeaponCycleMode.Heat,
                heatCapacityUnits: 4d,
                heatPerShotUnits: 4d,
                heatRecoveryUnitsPerSecond: 2d);

            WeaponMountStepResult overheated = WeaponMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            WeaponMountStepResult almostCool = WeaponMountStepper.Step(
                profile,
                overheated.State,
                1.999d,
                false);
            WeaponMountStepResult cooled = WeaponMountStepper.Step(
                profile,
                almostCool.State,
                0.001d,
                false);

            Assert.Multiple(() =>
            {
                Assert.That(overheated.ShotsFired, Is.EqualTo(1));
                Assert.That(overheated.BurstInterrupted, Is.True);
                Assert.That(overheated.State.Phase, Is.EqualTo(WeaponMountPhase.Depleted));
                Assert.That(overheated.State.HeatUnits, Is.EqualTo(4d));
                Assert.That(overheated.State.HeatRecoveryLocked, Is.True);
                Assert.That(almostCool.State.Phase, Is.EqualTo(WeaponMountPhase.Depleted));
                Assert.That(almostCool.State.HeatUnits, Is.GreaterThan(0d));
                Assert.That(cooled.State.HeatUnits, Is.Zero);
                Assert.That(cooled.State.HeatRecoveryLocked, Is.False);
                Assert.That(cooled.State.Phase, Is.EqualTo(WeaponMountPhase.Ready));
            });
        }

        [Test]
        public void ChargeCompletion_TransitionsAtExactBoundary()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "charge",
                cadenceSeconds: 0.1d,
                recoverySeconds: 0d,
                cycleMode: WeaponCycleMode.Charge,
                chargeSeconds: 0.5d);

            WeaponMountStepResult fired = WeaponMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            WeaponMountStepResult beforeBoundary = WeaponMountStepper.Step(
                profile,
                fired.State,
                0.499d,
                false);
            WeaponMountStepResult atBoundary = WeaponMountStepper.Step(
                profile,
                beforeBoundary.State,
                0.001d,
                false);

            Assert.Multiple(() =>
            {
                Assert.That(fired.State.Phase, Is.EqualTo(WeaponMountPhase.Depleted));
                Assert.That(fired.State.ChargeProgressSeconds, Is.Zero);
                Assert.That(beforeBoundary.State.Phase, Is.EqualTo(WeaponMountPhase.Depleted));
                Assert.That(beforeBoundary.State.ChargeProgressSeconds, Is.EqualTo(0.499d).Within(0.000000001d));
                Assert.That(atBoundary.State.ChargeProgressSeconds, Is.EqualTo(0.5d));
                Assert.That(atBoundary.State.Phase, Is.EqualTo(WeaponMountPhase.Ready));
            });
        }

        [Test]
        public void RecoveryAndCadence_BlockUntilBothComplete()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "recovery",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0.5d);

            WeaponMountStepResult fired = WeaponMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            WeaponMountStepResult cadenceComplete = WeaponMountStepper.Step(
                profile,
                fired.State,
                0.2d,
                false);
            WeaponMountStepResult recoveryComplete = WeaponMountStepper.Step(
                profile,
                cadenceComplete.State,
                0.3d,
                false);

            Assert.Multiple(() =>
            {
                Assert.That(fired.State.Phase, Is.EqualTo(WeaponMountPhase.Recovering));
                Assert.That(cadenceComplete.State.CadenceRemainingSeconds, Is.Zero);
                Assert.That(cadenceComplete.State.RecoveryRemainingSeconds, Is.EqualTo(0.3d).Within(0.000000001d));
                Assert.That(cadenceComplete.State.Phase, Is.EqualTo(WeaponMountPhase.Recovering));
                Assert.That(recoveryComplete.State.Phase, Is.EqualTo(WeaponMountPhase.Ready));
            });
        }

        [Test]
        public void RapidInput_DoesNotQueueARequestWhileBlocked()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "rapid",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0.4d);
            DomainMountState state = DomainMountState.Initial(profile);

            WeaponMountStepResult initialFire = WeaponMountStepper.Step(profile, state, 0d, true);
            WeaponMountStepResult released = WeaponMountStepper.Step(profile, initialFire.State, 0.1d, false);
            WeaponMountStepResult tappedWhileBlocked = WeaponMountStepper.Step(profile, released.State, 0.05d, true);
            WeaponMountStepResult becameReady = WeaponMountStepper.Step(profile, tappedWhileBlocked.State, 0.25d, false);
            WeaponMountStepResult newRequest = WeaponMountStepper.Step(profile, becameReady.State, 0d, true);

            Assert.Multiple(() =>
            {
                Assert.That(initialFire.ShotsFired, Is.EqualTo(1));
                Assert.That(tappedWhileBlocked.ShotsFired, Is.Zero);
                Assert.That(becameReady.ShotsFired, Is.Zero);
                Assert.That(becameReady.State.TotalShotsFired, Is.EqualTo(1L));
                Assert.That(becameReady.State.Phase, Is.EqualTo(WeaponMountPhase.Ready));
                Assert.That(newRequest.ShotsFired, Is.EqualTo(1));
                Assert.That(newRequest.State.TotalShotsFired, Is.EqualTo(2L));
            });
        }

        [Test]
        public void InvalidElapsedTime_FaultsClosedWithActionableDiagnostic()
        {
            WeaponRuntimeProfile profile = BuildProfile(profileSuffix: "elapsed");
            double[] invalidValues =
            {
                -0.01d,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            };

            foreach (double invalid in invalidValues)
            {
                WeaponMountStepResult result = WeaponMountStepper.Step(
                    profile,
                    DomainMountState.Initial(profile),
                    invalid,
                    true);

                Assert.Multiple(() =>
                {
                    Assert.That(result.Succeeded, Is.False);
                    Assert.That(result.ShotsFired, Is.Zero);
                    Assert.That(result.State.Phase, Is.EqualTo(WeaponMountPhase.Faulted));
                    Assert.That(result.Fault.Kind, Is.EqualTo(WeaponMountFaultKind.InvalidElapsedTime));
                    Assert.That(result.Fault.Detail, Does.Contain("elapsedSeconds"));
                });
            }
        }

        [Test]
        public void MalformedState_FaultsClosedInsteadOfAdvancingImpossiblePhase()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "malformed",
                cadenceSeconds: 0.5d);
            ConstructorInfo constructor = typeof(DomainMountState)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();

            DomainMountState malformed = (DomainMountState)constructor.Invoke(
                new object[]
                {
                    WeaponMountPhase.Ready,
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

            WeaponMountStepResult result = WeaponMountStepper.Step(
                profile,
                malformed,
                0.1d,
                true);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.ShotsFired, Is.Zero);
                Assert.That(result.State.TotalShotsFired, Is.Zero);
                Assert.That(result.Fault.Kind, Is.EqualTo(WeaponMountFaultKind.MalformedState));
                Assert.That(result.Fault.Detail, Does.Contain("Impossible phase transition"));
            });
        }

        [Test]
        public void FaultDuringRecovery_IsTerminalAndDoesNotChangeAnotherMount()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "fault",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0.5d);
            WeaponMountStepResult firstFired = WeaponMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);
            WeaponMountStepResult secondFired = WeaponMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                0d,
                true);

            WeaponMountStepResult faulted = WeaponMountStepper.Step(
                profile,
                firstFired.State,
                0.1d,
                WeaponMountStepInput.Fault("synthetic actuator fault"));
            WeaponMountStepResult independent = WeaponMountStepper.Step(
                profile,
                secondFired.State,
                0.5d,
                false);
            WeaponMountStepResult terminal = WeaponMountStepper.Step(
                profile,
                faulted.State,
                10d,
                true);

            Assert.Multiple(() =>
            {
                Assert.That(faulted.State.Phase, Is.EqualTo(WeaponMountPhase.Faulted));
                Assert.That(faulted.Fault.Kind, Is.EqualTo(WeaponMountFaultKind.ExternalFault));
                Assert.That(faulted.Fault.Detail, Does.Contain("actuator"));
                Assert.That(independent.State.Phase, Is.EqualTo(WeaponMountPhase.Ready));
                Assert.That(independent.State.TotalShotsFired, Is.EqualTo(1L));
                Assert.That(terminal.State, Is.SameAs(faulted.State));
                Assert.That(terminal.ShotsFired, Is.Zero);
            });
        }

        [Test]
        public void LargeFixedStepCatchUp_MatchesPartitionedFixedSteps()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                profileSuffix: "catchup",
                cadenceSeconds: 0.2d,
                recoverySeconds: 0d);

            WeaponMountStepResult single = WeaponMountStepper.Step(
                profile,
                DomainMountState.Initial(profile),
                1d,
                true);

            DomainMountState partitionedState = DomainMountState.Initial(profile);
            int partitionedShots = 0;
            for (int index = 0; index < 10; index++)
            {
                WeaponMountStepResult step = WeaponMountStepper.Step(
                    profile,
                    partitionedState,
                    0.1d,
                    true);
                partitionedState = step.State;
                partitionedShots += step.ShotsFired;
            }

            Assert.Multiple(() =>
            {
                Assert.That(single.ShotsFired, Is.EqualTo(6));
                Assert.That(partitionedShots, Is.EqualTo(single.ShotsFired));
                AssertStatesEquivalent(partitionedState, single.State);
            });
        }

        [Test]
        public void FixedProfileAndInputSequence_ProducesByteStableTraceRows()
        {
            WeaponRuntimeProfile profile = BuildProfile(
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
        public void StateFields_ProjectToFourMountWeaponsV1HeatAndChargeSnapshots()
        {
            WeaponRuntimeProfile heatProfile = BuildProfile(
                profileSuffix: "contractheat",
                cadenceSeconds: 0.2d,
                cycleMode: WeaponCycleMode.Heat,
                heatCapacityUnits: 5d,
                heatPerShotUnits: 5d,
                heatRecoveryUnitsPerSecond: 1d);
            DomainMountState heatState = WeaponMountStepper.Step(
                heatProfile,
                DomainMountState.Initial(heatProfile),
                0d,
                true).State;

            ContractCombat.WeaponMountState heatSnapshot = new ContractCombat.WeaponMountState(
                ContractCombat.WeaponMountSlot.MountOne,
                heatProfile.ProfileId,
                ContractCombat.WeaponMountReadiness.Overheated,
                new ContractCombat.WeaponCadenceState(
                    heatState.CadenceRemainingSeconds,
                    heatState.BurstShotsRemaining),
                new ContractCombat.WeaponCycleResourceState(
                    ContractCombat.WeaponCycleResourceKind.Heat,
                    heatState.HeatUnits,
                    heatProfile.HeatCapacityUnits),
                ContractCombat.WeaponRecoilState.None,
                ContractCombat.WeaponPowerBankState.None);

            WeaponRuntimeProfile chargeProfile = BuildProfile(
                profileSuffix: "contractcharge",
                cadenceSeconds: 0.2d,
                cycleMode: WeaponCycleMode.Charge,
                chargeSeconds: 0.5d);
            DomainMountState chargeState = WeaponMountStepper.Step(
                chargeProfile,
                DomainMountState.Initial(chargeProfile),
                0d,
                true).State;

            ContractCombat.WeaponMountState chargeSnapshot = new ContractCombat.WeaponMountState(
                ContractCombat.WeaponMountSlot.MountTwo,
                chargeProfile.ProfileId,
                ContractCombat.WeaponMountReadiness.Charging,
                new ContractCombat.WeaponCadenceState(
                    chargeState.CadenceRemainingSeconds,
                    chargeState.BurstShotsRemaining),
                new ContractCombat.WeaponCycleResourceState(
                    ContractCombat.WeaponCycleResourceKind.Charge,
                    chargeState.ChargeProgressSeconds,
                    chargeProfile.ChargeSeconds),
                ContractCombat.WeaponRecoilState.None,
                ContractCombat.WeaponPowerBankState.None);

            Assert.Multiple(() =>
            {
                Assert.That(heatSnapshot.Readiness, Is.EqualTo(ContractCombat.WeaponMountReadiness.Overheated));
                Assert.That(heatSnapshot.CycleResource.Current, Is.EqualTo(5d));
                Assert.That(chargeSnapshot.Readiness, Is.EqualTo(ContractCombat.WeaponMountReadiness.Charging));
                Assert.That(chargeSnapshot.CycleResource.Current, Is.Zero);
                Assert.That(chargeSnapshot.CycleResource.Maximum, Is.EqualTo(0.5d));
            });
        }

        [Test]
        public void FourSyntheticMountTraces_AreIndependentAndLogged()
        {
            WeaponRuntimeProfile[] profiles =
            {
                BuildProfile("traceone", 0.1d, recoverySeconds: 0d),
                BuildProfile("tracetwo", 0.3d, 3, 0.05d, 0.1d),
                BuildProfile(
                    "tracethree",
                    0.2d,
                    recoverySeconds: 0.25d,
                    cycleMode: WeaponCycleMode.Heat,
                    heatCapacityUnits: 3d,
                    heatPerShotUnits: 3d,
                    heatRecoveryUnitsPerSecond: 3d),
                BuildProfile(
                    "tracefour",
                    0.4d,
                    recoverySeconds: 0.35d,
                    cycleMode: WeaponCycleMode.Charge,
                    chargeSeconds: 0.6d),
            };
            DomainMountState[] states = profiles.Select(DomainMountState.Initial).ToArray();
            double[] elapsed = { 0d, 0.1d, 0.2d, 0.4d };
            bool[] fire = { true, true, false, true };

            for (int tick = 0; tick < elapsed.Length; tick++)
            {
                for (int mount = 0; mount < profiles.Length; mount++)
                {
                    WeaponMountStepInput input = mount == 3 && tick == 2
                        ? WeaponMountStepInput.Fault("synthetic mount-four fault")
                        : new WeaponMountStepInput(fire[tick]);
                    WeaponMountStepResult result = WeaponMountStepper.Step(
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

            Assert.Multiple(() =>
            {
                Assert.That(states[0].TotalShotsFired, Is.GreaterThan(states[1].TotalShotsFired));
                Assert.That(states[2].HeatUnits, Is.GreaterThanOrEqualTo(0d));
                Assert.That(states[3].Phase, Is.EqualTo(WeaponMountPhase.Faulted));
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
                typeof(WeaponMountStepResult),
                typeof(WeaponMountFault),
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

            Assert.Multiple(() =>
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
                    typeof(WeaponMountStepper)
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

        private static string[] RunTrace(
            WeaponRuntimeProfile profile,
            double[] elapsed,
            bool[] fire)
        {
            DomainMountState state = DomainMountState.Initial(profile);
            List<string> rows = new List<string>();
            for (int index = 0; index < elapsed.Length; index++)
            {
                WeaponMountStepResult result = WeaponMountStepper.Step(
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
            Assert.Multiple(() =>
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

        private static WeaponRuntimeProfile BuildProfile(
            string profileSuffix = "standard",
            double cadenceSeconds = 0.2d,
            int burstShotCount = 1,
            double burstShotIntervalSeconds = 0d,
            double recoverySeconds = 0d,
            WeaponCycleMode cycleMode = WeaponCycleMode.None,
            double heatCapacityUnits = 0d,
            double heatPerShotUnits = 0d,
            double heatRecoveryUnitsPerSecond = 0d,
            double chargeSeconds = 0d)
        {
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse("weapon-profile." + profileSuffix),
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

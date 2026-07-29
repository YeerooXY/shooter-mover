using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class FourMountCombatStepperTests
    {
        private static readonly StableId ModuleId = StableId.Parse("gun-module.cb006-empty");

        [Test]
        public void Step_SimultaneousFireUsesStableFourSlotOrder()
        {
            Fixture fixture = CreateFixture(new[] { 0.1d, 0.1d, 0.1d, 0.1d }, new[] { 0d, 0d, 0d, 0d });

            FourMountCombatStepResult result = fixture.Stepper.Step(
                fixture.State,
                fixture.Input(0L, 0d, true, false));

            for (int index = 0; index < FourMountCombatState.MountCount; index++)
            {
                Assert.That(result.GetLaneByStableIndex(index).StableSlotNumber, Is.EqualTo(index + 1));
                Assert.That(result.GetLaneByStableIndex(index).ShotsFired, Is.EqualTo(1));
                Assert.That(result.GetLaneByStableIndex(index).ExecutionPlan, Is.Not.Null);
            }
        }

        [Test]
        public void Step_MixedCadenceAndReadinessRemainIndependent()
        {
            Fixture fixture = CreateFixture(new[] { 0.1d, 0.2d, 0.3d, 0.4d }, new[] { 0d, 0d, 0d, 0d });
            FourMountCombatStepResult first = fixture.Stepper.Step(
                fixture.State,
                fixture.Input(0L, 0d, true, false));
            FourMountCombatStepResult second = fixture.Stepper.Step(
                first.State,
                fixture.Input(1L, 0.15d, true, false));

            Assert.That(second.GetLaneByStableIndex(0).ShotsFired, Is.EqualTo(1));
            Assert.That(second.GetLaneByStableIndex(1).ShotsFired, Is.EqualTo(0));
            Assert.That(second.GetLaneByStableIndex(2).ShotsFired, Is.EqualTo(0));
            Assert.That(second.GetLaneByStableIndex(3).ShotsFired, Is.EqualTo(0));
            Assert.That(second.State.GetMountByStableIndex(0).TotalShotsFired, Is.EqualTo(2L));
            Assert.That(second.State.GetMountByStableIndex(3).TotalShotsFired, Is.EqualTo(1L));
        }

        [Test]
        public void Step_MixedPowerEmpowersOrFallsBackPerMount()
        {
            Fixture fixture = CreateFixture(new[] { 0.2d, 0.2d, 0.2d, 0.2d }, new[] { 10d, 0d, 5d, 0d }, true);

            FourMountCombatStepResult result = fixture.Stepper.Step(
                fixture.State,
                fixture.Input(0L, 0d, true, true));

            Assert.That(result.GetLaneByStableIndex(0).PowerDecision.Kind, Is.EqualTo(GunPowerFireDecisionKind.EmpoweredFired));
            Assert.That(result.GetLaneByStableIndex(1).PowerDecision.Kind, Is.EqualTo(GunPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(result.GetLaneByStableIndex(2).PowerDecision.Kind, Is.EqualTo(GunPowerFireDecisionKind.EmpoweredFired));
            Assert.That(result.GetLaneByStableIndex(3).PowerDecision.Kind, Is.EqualTo(GunPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(result.State.GetPowerBankByStableIndex(0).AvailableUnits, Is.EqualTo(5d));
            Assert.That(result.State.GetPowerBankByStableIndex(1).AvailableUnits, Is.EqualTo(0d));
            Assert.That(result.State.GetPowerBankByStableIndex(2).AvailableUnits, Is.EqualTo(0d));
            Assert.That(result.GetLaneByStableIndex(3).ShotsFired, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Step_OneOrTwoFaultsDoNotSuppressHealthyMounts(int faultCount)
        {
            Fixture fixture = CreateFixture(new[] { 0.2d, 0.2d, 0.2d, 0.2d }, new[] { 0d, 0d, 0d, 0d });
            string[] faults = new string[FourMountCombatState.MountCount];
            for (int index = 0; index < faultCount; index++)
            {
                faults[index] = "synthetic fault " + index;
            }

            FourMountCombatStepResult result = fixture.Stepper.Step(
                fixture.State,
                fixture.Input(0L, 0d, true, false, faults));

            for (int index = 0; index < FourMountCombatState.MountCount; index++)
            {
                if (index < faultCount)
                {
                    Assert.That(result.GetLaneByStableIndex(index).IsFaulted, Is.True);
                    Assert.That(result.GetLaneByStableIndex(index).ShotsFired, Is.EqualTo(0));
                }
                else
                {
                    Assert.That(result.GetLaneByStableIndex(index).IsFaulted, Is.False);
                    Assert.That(result.GetLaneByStableIndex(index).ShotsFired, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void RapidIntentReplay_IsDeterministicAndProducesInspectableTimeline()
        {
            Fixture left = CreateFixture(new[] { 0.05d, 0.1d, 0.15d, 0.2d }, new[] { 10d, 5d, 0d, 10d }, true);
            Fixture right = CreateFixture(new[] { 0.05d, 0.1d, 0.15d, 0.2d }, new[] { 10d, 5d, 0d, 10d }, true);
            List<string> leftTimeline = RunRapidTimeline(left);
            List<string> rightTimeline = RunRapidTimeline(right);

            CollectionAssert.AreEqual(leftTimeline, rightTimeline);
            Assert.That(left.State.ToTraceString(), Is.EqualTo(right.State.ToTraceString()));
            TestContext.WriteLine(string.Join("\n", leftTimeline));
        }

        private static List<string> RunRapidTimeline(Fixture fixture)
        {
            List<string> timeline = new List<string>();
            for (int step = 0; step < 12; step++)
            {
                bool fire = step % 3 != 2;
                bool power = step % 2 == 0;
                FourMountCombatStepResult result = fixture.Stepper.Step(
                    fixture.State,
                    fixture.Input(step, 0.05d, fire, power));
                fixture.State = result.State;
                timeline.Add("T" + step + " " + result.ToTimelineRow());
            }

            return timeline;
        }

        private static Fixture CreateFixture(
            double[] cadences,
            double[] initialPower,
            bool configuredPower = false)
        {
            GunLiveProfile[] profiles = new GunLiveProfile[FourMountCombatState.MountCount];
            StableId[] gunIds = new StableId[FourMountCombatState.MountCount];
            StableId[] mountIds = new StableId[FourMountCombatState.MountCount];
            GunMountOrigin[] origins = new GunMountOrigin[FourMountCombatState.MountCount];

            for (int index = 0; index < profiles.Length; index++)
            {
                profiles[index] = GunLiveProfile.Create(
                    GunLiveProfile.CurrentProfileVersion,
                    StableId.Parse("gun-profile.cb006-slot-" + (index + 1)),
                    cadences[index],
                    1,
                    0d,
                    0d,
                    GunCycleMode.None,
                    0d,
                    0d,
                    0d,
                    0d,
                    configuredPower,
                    configuredPower ? 10d : 0d,
                    configuredPower ? 5d : 0d,
                    0d,
                    new[] { ModuleId },
                    new[] { ModuleId },
                    index);
                gunIds[index] = StableId.Parse("gun.cb006-slot-" + (index + 1));
                mountIds[index] = StableId.Parse("mount.cb006-slot-" + (index + 1));
                origins[index] = new GunMountOrigin(index + 1, new AimVector2(index - 1.5d, 0d));
            }

            GunBehaviorPipeline pipeline = new GunBehaviorPipeline(
                new IGunBehaviorModule[] { new EmptyModule() });
            return new Fixture(
                profiles,
                gunIds,
                mountIds,
                origins,
                FourMountCombatState.Initial(profiles, initialPower),
                new FourMountCombatStepper(new FourMountAimResolver(), pipeline));
        }

        private sealed class Fixture
        {
            public Fixture(
                GunLiveProfile[] profiles,
                StableId[] gunIds,
                StableId[] mountIds,
                GunMountOrigin[] origins,
                FourMountCombatState state,
                FourMountCombatStepper stepper)
            {
                Profiles = profiles;
                GunIds = gunIds;
                MountIds = mountIds;
                Origins = origins;
                State = state;
                Stepper = stepper;
            }

            public GunLiveProfile[] Profiles { get; }
            public StableId[] GunIds { get; }
            public StableId[] MountIds { get; }
            public GunMountOrigin[] Origins { get; }
            public FourMountCombatState State { get; set; }
            public FourMountCombatStepper Stepper { get; }

            public FourMountCombatStepInput Input(
                long step,
                double elapsed,
                bool fire,
                bool power,
                string[] faults = null)
            {
                return new FourMountCombatStepInput(
                    step,
                    elapsed,
                    fire,
                    power,
                    AimVector2.UnitX,
                    new AimVector2(20d, 0d),
                    Profiles,
                    GunIds,
                    MountIds,
                    Origins,
                    faults);
            }
        }

        private sealed class EmptyModule : IGunBehaviorModule
        {
            public StableId ModuleId => FourMountCombatStepperTests.ModuleId;

            public GunBehaviorModulePlan BuildExecutionPlan(GunBehaviorInput input)
            {
                return new GunBehaviorModulePlan(ModuleId);
            }
        }
    }
}

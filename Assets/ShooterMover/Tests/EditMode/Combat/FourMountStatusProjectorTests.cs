using System;
using NUnit.Framework;
using ShooterMover.Application.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class FourMountStatusProjectorTests
    {
        private static readonly StableId ModuleId = StableId.Parse("weapon-module.cb010-status");

        [Test]
        public void MixedState_ExposesStableOrderReadinessResourcesRecoveryAndPower()
        {
            Fixture fixture = CreateMixedFixture();
            FourMountStatusProjector projector = new FourMountStatusProjector();

            FourMountStatusSnapshot snapshot = projector.Project(
                fixture.State,
                fixture.Profiles,
                fixture.WeaponIds);

            Assert.That(snapshot.Count, Is.EqualTo(4));
            for (int stableIndex = 0; stableIndex < snapshot.Count; stableIndex++)
            {
                Assert.That(
                    snapshot.GetByStableIndex(stableIndex).StableSlotNumber,
                    Is.EqualTo(stableIndex + 1));
            }

            FourMountSlotStatusSnapshot heat = snapshot.GetByStableIndex(0);
            Assert.That(heat.CycleMode, Is.EqualTo(WeaponCycleMode.Heat));
            Assert.That(heat.CycleCurrent, Is.EqualTo(4d));
            Assert.That(heat.CycleMaximum, Is.EqualTo(10d));
            Assert.That(heat.PowerLevel, Is.EqualTo(0.8d).Within(0.000000001d));

            FourMountSlotStatusSnapshot charge = snapshot.GetByStableIndex(1);
            Assert.That(charge.CycleMode, Is.EqualTo(WeaponCycleMode.Charge));
            Assert.That(charge.CycleCurrent, Is.Zero);
            Assert.That(charge.CycleMaximum, Is.EqualTo(0.5d));
            Assert.That(charge.IsReady, Is.False);

            FourMountSlotStatusSnapshot recovering = snapshot.GetByStableIndex(2);
            Assert.That(recovering.Phase, Is.EqualTo(WeaponMountPhase.Recovering));
            Assert.That(recovering.RecoveryRemainingSeconds, Is.EqualTo(0.5d));

            FourMountSlotStatusSnapshot ready = snapshot.GetByStableIndex(3);
            Assert.That(ready.IsReady, Is.True);
            Assert.That(ready.Phase, Is.EqualTo(WeaponMountPhase.Ready));
            Assert.That(ready.HasPowerBank, Is.False);

            TestContext.WriteLine("MIXED FOUR-MOUNT STATUS\n" + snapshot.ToTraceString());
        }

        [Test]
        public void StableOrder_IsCanonicalWhenSnapshotSlotsArriveOutOfOrder()
        {
            Fixture fixture = CreateMixedFixture();
            FourMountStatusSnapshot projected = new FourMountStatusProjector().Project(
                fixture.State,
                fixture.Profiles,
                fixture.WeaponIds);

            FourMountStatusSnapshot reordered = new FourMountStatusSnapshot(
                projected.GetByStableIndex(3),
                projected.GetByStableIndex(1),
                projected.GetByStableIndex(0),
                projected.GetByStableIndex(2));

            for (int stableIndex = 0; stableIndex < reordered.Count; stableIndex++)
            {
                Assert.That(
                    reordered.GetByStableIndex(stableIndex).StableSlotNumber,
                    Is.EqualTo(stableIndex + 1));
            }

            Assert.That(reordered.ToTraceString(), Is.EqualTo(projected.ToTraceString()));
        }

        [Test]
        public void NormalMode_ComesFromCoordinatorDecision()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture(
                new[] { 10d, 10d, 10d, 10d });
            FourMountCombatStepResult result = fixture.Step(
                fireRequested: true,
                empoweredRequested: false);

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                result.State,
                fixture.Profiles,
                fixture.WeaponIds,
                result);

            for (int stableIndex = 0; stableIndex < snapshot.Count; stableIndex++)
            {
                Assert.That(
                    snapshot.GetByStableIndex(stableIndex).FireMode,
                    Is.EqualTo(FourMountFireMode.Normal));
                Assert.That(snapshot.GetByStableIndex(stableIndex).IsFallback, Is.False);
            }

            TestContext.WriteLine("NORMAL FIRE STATUS\n" + snapshot.ToTraceString());
        }

        [Test]
        public void FallbackMode_RemainsIndependentPerStableSlot()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture(
                new[] { 10d, 0d, 5d, 0d });
            FourMountCombatStepResult result = fixture.Step(
                fireRequested: true,
                empoweredRequested: true);

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                result.State,
                fixture.Profiles,
                fixture.WeaponIds,
                result);

            Assert.That(snapshot.GetByStableIndex(0).FireMode, Is.EqualTo(FourMountFireMode.Empowered));
            Assert.That(snapshot.GetByStableIndex(0).IsFallback, Is.False);
            Assert.That(
                snapshot.GetByStableIndex(1).FireMode,
                Is.EqualTo(FourMountFireMode.NormalFallbackPowerUnavailable));
            Assert.That(snapshot.GetByStableIndex(1).IsFallback, Is.True);
            Assert.That(snapshot.GetByStableIndex(2).FireMode, Is.EqualTo(FourMountFireMode.Empowered));
            Assert.That(
                snapshot.GetByStableIndex(3).FireMode,
                Is.EqualTo(FourMountFireMode.NormalFallbackPowerUnavailable));
            Assert.That(snapshot.GetByStableIndex(3).IsFallback, Is.True);

            TestContext.WriteLine("MIXED EMPOWERED/FALLBACK STATUS\n" + snapshot.ToTraceString());
        }

        [Test]
        public void FaultState_ExposesKindDetailAndPowerWithoutHidingHealthySlots()
        {
            CoordinatorFixture fixture = CreateCoordinatorFixture(
                new[] { 10d, 10d, 10d, 10d });
            FourMountCombatStepResult result = fixture.Step(
                fireRequested: true,
                empoweredRequested: true,
                externalFaultDetails: new[] { null, "synthetic mount bus fault", null, null });

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                result.State,
                fixture.Profiles,
                fixture.WeaponIds,
                result);

            FourMountSlotStatusSnapshot faulted = snapshot.GetByStableIndex(1);
            Assert.That(faulted.IsFaulted, Is.True);
            Assert.That(faulted.Phase, Is.EqualTo(WeaponMountPhase.Faulted));
            Assert.That(faulted.FireMode, Is.EqualTo(FourMountFireMode.Faulted));
            Assert.That(faulted.FaultKind, Is.EqualTo(WeaponMountFaultKind.ExternalFault));
            Assert.That(faulted.FaultDetail, Is.EqualTo("synthetic mount bus fault"));
            Assert.That(faulted.HasPowerBank, Is.True);
            Assert.That(faulted.PowerAvailableUnits, Is.EqualTo(10d));

            Assert.That(snapshot.GetByStableIndex(0).IsFaulted, Is.False);
            Assert.That(snapshot.GetByStableIndex(2).IsFaulted, Is.False);
            Assert.That(snapshot.GetByStableIndex(3).IsFaulted, Is.False);

            TestContext.WriteLine("FAULT-ISOLATED STATUS\n" + snapshot.ToTraceString());
        }

        [Test]
        public void Immutability_ProjectionDoesNotMutateOrRetainCallerArrays()
        {
            Fixture fixture = CreateMixedFixture();
            FourMountStatusProjector projector = new FourMountStatusProjector();
            string sourceBefore = fixture.State.ToTraceString();

            FourMountStatusSnapshot snapshot = projector.Project(
                fixture.State,
                fixture.Profiles,
                fixture.WeaponIds);
            string projectedBefore = snapshot.ToTraceString();

            fixture.Profiles[0] = fixture.Profiles[3];
            fixture.WeaponIds[0] = StableId.Parse("weapon.cb010-array-replaced");

            Assert.That(fixture.State.ToTraceString(), Is.EqualTo(sourceBefore));
            Assert.That(snapshot.ToTraceString(), Is.EqualTo(projectedBefore));
            Assert.That(
                snapshot.GetByStableIndex(0).WeaponId,
                Is.EqualTo(StableId.Parse("weapon.cb010-mixed-slot-1")));
        }

        [Test]
        public void MissingSlot_FailsVisiblyInsteadOfFabricatingState()
        {
            Fixture fixture = CreateMixedFixture();
            WeaponRuntimeProfile[] onlyThreeProfiles =
            {
                fixture.Profiles[0],
                fixture.Profiles[1],
                fixture.Profiles[2],
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new FourMountStatusProjector().Project(
                    fixture.State,
                    onlyThreeProfiles,
                    fixture.WeaponIds));

            Assert.That(exception.Message, Does.Contain("Exactly four"));
        }

        [Test]
        public void MissingSnapshotSlotAndDuplicateSlot_AreRejected()
        {
            Fixture fixture = CreateMixedFixture();
            FourMountStatusSnapshot projected = new FourMountStatusProjector().Project(
                fixture.State,
                fixture.Profiles,
                fixture.WeaponIds);

            Assert.Throws<ArgumentException>(() => new FourMountStatusSnapshot(
                projected.GetByStableIndex(0),
                projected.GetByStableIndex(1),
                projected.GetByStableIndex(2)));

            Assert.Throws<ArgumentException>(() => new FourMountStatusSnapshot(
                projected.GetByStableIndex(0),
                projected.GetByStableIndex(0),
                projected.GetByStableIndex(2),
                projected.GetByStableIndex(3)));
        }

        [Test]
        public void RepeatedProjection_IsDeterministicAndReturnsDetachedSnapshots()
        {
            Fixture fixture = CreateMixedFixture();
            FourMountStatusProjector projector = new FourMountStatusProjector();

            FourMountStatusSnapshot first = projector.Project(
                fixture.State,
                fixture.Profiles,
                fixture.WeaponIds);
            FourMountStatusSnapshot second = projector.Project(
                fixture.State,
                fixture.Profiles,
                fixture.WeaponIds);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.GetByStableIndex(0), Is.Not.SameAs(first.GetByStableIndex(0)));
            Assert.That(second.ToTraceString(), Is.EqualTo(first.ToTraceString()));
        }

        [Test]
        public void UnequippedSlot_RemainsPresentWithNeutralReadableState()
        {
            Fixture fixture = CreateMixedFixture();
            fixture.Profiles[3] = null;
            fixture.WeaponIds[3] = null;

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                fixture.State,
                fixture.Profiles,
                fixture.WeaponIds);

            FourMountSlotStatusSnapshot unequipped = snapshot.GetByStableIndex(3);
            Assert.That(unequipped.StableSlotNumber, Is.EqualTo(4));
            Assert.That(unequipped.IsEquipped, Is.False);
            Assert.That(unequipped.Phase, Is.Null);
            Assert.That(unequipped.IsReady, Is.False);
            Assert.That(unequipped.FireMode, Is.EqualTo(FourMountFireMode.NoRecentAttempt));
            Assert.That(unequipped.ToTraceString(), Does.Contain("phase=Unequipped"));
        }

        private static Fixture CreateMixedFixture()
        {
            WeaponRuntimeProfile[] profiles =
            {
                BuildProfile(
                    "mixed-slot-1",
                    cycleMode: WeaponCycleMode.Heat,
                    heatCapacityUnits: 10d,
                    heatPerShotUnits: 4d,
                    heatRecoveryUnitsPerSecond: 2d,
                    recoverySeconds: 0.25d,
                    hasPowerBank: true),
                BuildProfile(
                    "mixed-slot-2",
                    cycleMode: WeaponCycleMode.Charge,
                    chargeSeconds: 0.5d,
                    hasPowerBank: true),
                BuildProfile(
                    "mixed-slot-3",
                    recoverySeconds: 0.5d,
                    hasPowerBank: true),
                BuildProfile("mixed-slot-4"),
            };

            WeaponMountState[] mounts =
            {
                WeaponMountStepper.Step(
                    profiles[0],
                    WeaponMountState.Initial(profiles[0]),
                    0d,
                    true).State,
                WeaponMountStepper.Step(
                    profiles[1],
                    WeaponMountState.Initial(profiles[1]),
                    0d,
                    true).State,
                WeaponMountStepper.Step(
                    profiles[2],
                    WeaponMountState.Initial(profiles[2]),
                    0d,
                    true).State,
                WeaponMountState.Initial(profiles[3]),
            };

            WeaponPowerBankState[] banks =
            {
                WeaponPowerBankState.FromProfile(profiles[0], 8d),
                WeaponPowerBankState.FromProfile(profiles[1], 4d),
                WeaponPowerBankState.FromProfile(profiles[2], 0d),
                WeaponPowerBankState.None,
            };

            StableId[] weaponIds =
            {
                StableId.Parse("weapon.cb010-mixed-slot-1"),
                StableId.Parse("weapon.cb010-mixed-slot-2"),
                StableId.Parse("weapon.cb010-mixed-slot-3"),
                StableId.Parse("weapon.cb010-mixed-slot-4"),
            };

            return new Fixture(
                profiles,
                weaponIds,
                new FourMountCombatState(mounts, banks));
        }

        private static CoordinatorFixture CreateCoordinatorFixture(double[] initialPower)
        {
            WeaponRuntimeProfile[] profiles = new WeaponRuntimeProfile[FourMountCombatState.MountCount];
            StableId[] weaponIds = new StableId[FourMountCombatState.MountCount];
            StableId[] mountIds = new StableId[FourMountCombatState.MountCount];
            WeaponMountOrigin[] origins = new WeaponMountOrigin[FourMountCombatState.MountCount];

            for (int stableIndex = 0; stableIndex < FourMountCombatState.MountCount; stableIndex++)
            {
                profiles[stableIndex] = BuildProfile(
                    "coordinator-slot-" + (stableIndex + 1),
                    hasPowerBank: true,
                    presentationPriority: stableIndex);
                weaponIds[stableIndex] = StableId.Parse(
                    "weapon.cb010-coordinator-slot-" + (stableIndex + 1));
                mountIds[stableIndex] = StableId.Parse(
                    "mount.cb010-coordinator-slot-" + (stableIndex + 1));
                origins[stableIndex] = new WeaponMountOrigin(
                    stableIndex + 1,
                    new AimVector2(stableIndex, 0d));
            }

            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[] { new EmptyModule() });
            return new CoordinatorFixture(
                profiles,
                weaponIds,
                mountIds,
                origins,
                FourMountCombatState.Initial(profiles, initialPower),
                new FourMountCombatStepper(new FourMountAimResolver(), pipeline));
        }

        private static WeaponRuntimeProfile BuildProfile(
            string suffix,
            double cadenceSeconds = 0.2d,
            double recoverySeconds = 0d,
            WeaponCycleMode cycleMode = WeaponCycleMode.None,
            double heatCapacityUnits = 0d,
            double heatPerShotUnits = 0d,
            double heatRecoveryUnitsPerSecond = 0d,
            double chargeSeconds = 0d,
            bool hasPowerBank = false,
            int presentationPriority = 0)
        {
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse("weapon-profile.cb010-" + suffix),
                cadenceSeconds,
                1,
                0d,
                recoverySeconds,
                cycleMode,
                heatCapacityUnits,
                heatPerShotUnits,
                heatRecoveryUnitsPerSecond,
                chargeSeconds,
                hasPowerBank,
                hasPowerBank ? 10d : 0d,
                hasPowerBank ? 5d : 0d,
                0d,
                new[] { ModuleId },
                new[] { ModuleId },
                presentationPriority);
        }

        private sealed class Fixture
        {
            public Fixture(
                WeaponRuntimeProfile[] profiles,
                StableId[] weaponIds,
                FourMountCombatState state)
            {
                Profiles = profiles;
                WeaponIds = weaponIds;
                State = state;
            }

            public WeaponRuntimeProfile[] Profiles { get; }

            public StableId[] WeaponIds { get; }

            public FourMountCombatState State { get; }
        }

        private sealed class CoordinatorFixture
        {
            private long simulationStep;

            public CoordinatorFixture(
                WeaponRuntimeProfile[] profiles,
                StableId[] weaponIds,
                StableId[] mountIds,
                WeaponMountOrigin[] origins,
                FourMountCombatState state,
                FourMountCombatStepper stepper)
            {
                Profiles = profiles;
                WeaponIds = weaponIds;
                MountIds = mountIds;
                Origins = origins;
                State = state;
                Stepper = stepper;
            }

            public WeaponRuntimeProfile[] Profiles { get; }

            public StableId[] WeaponIds { get; }

            public StableId[] MountIds { get; }

            public WeaponMountOrigin[] Origins { get; }

            public FourMountCombatState State { get; private set; }

            public FourMountCombatStepper Stepper { get; }

            public FourMountCombatStepResult Step(
                bool fireRequested,
                bool empoweredRequested,
                string[] externalFaultDetails = null)
            {
                FourMountCombatStepInput input = new FourMountCombatStepInput(
                    simulationStep++,
                    0d,
                    fireRequested,
                    empoweredRequested,
                    AimVector2.UnitX,
                    new AimVector2(20d, 0d),
                    Profiles,
                    WeaponIds,
                    MountIds,
                    Origins,
                    externalFaultDetails);
                FourMountCombatStepResult result = Stepper.Step(State, input);
                State = result.State;
                return result;
            }
        }

        private sealed class EmptyModule : IWeaponBehaviorModule
        {
            public StableId ModuleId => FourMountStatusProjectorTests.ModuleId;

            public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
            {
                return new WeaponBehaviorModulePlan(ModuleId);
            }
        }
    }
}

using System;
using NUnit.Framework;
using ShooterMover.Application.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ContractCombat = ShooterMover.Contracts.Combat;
using ContractPresentation = ShooterMover.Contracts.Presentation;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class FourMountStatusProjectorTests
    {
        private static readonly StableId ModuleId = StableId.Parse("gun-module.cb010-status");

        [Test]
        public void MixedState_ExposesStableOrderReadinessResourcesRecoveryAndPower()
        {
            Fixture fixture = CreateMixedFixture();
            FourMountStatusProjector projector = new FourMountStatusProjector();

            FourMountStatusSnapshot snapshot = projector.Project(
                fixture.State,
                fixture.Profiles,
                fixture.GunIds);

            Assert.That(snapshot.Count, Is.EqualTo(4));
            for (int stableIndex = 0; stableIndex < snapshot.Count; stableIndex++)
            {
                Assert.That(
                    snapshot.GetByStableIndex(stableIndex).StableSlotNumber,
                    Is.EqualTo(stableIndex + 1));
            }

            FourMountSlotStatusSnapshot heat = snapshot.GetByStableIndex(0);
            Assert.That(heat.CycleMode, Is.EqualTo(GunCycleMode.Heat));
            Assert.That(heat.CycleCurrent, Is.EqualTo(4d));
            Assert.That(heat.CycleMaximum, Is.EqualTo(10d));
            Assert.That(heat.PowerLevel, Is.EqualTo(0.8d).Within(0.000000001d));

            FourMountSlotStatusSnapshot charge = snapshot.GetByStableIndex(1);
            Assert.That(charge.CycleMode, Is.EqualTo(GunCycleMode.Charge));
            Assert.That(charge.CycleCurrent, Is.Zero);
            Assert.That(charge.CycleMaximum, Is.EqualTo(0.5d));
            Assert.That(charge.IsReady, Is.False);

            FourMountSlotStatusSnapshot recovering = snapshot.GetByStableIndex(2);
            Assert.That(recovering.Phase, Is.EqualTo(GunMountPhase.Recovering));
            Assert.That(recovering.RecoveryRemainingSeconds, Is.EqualTo(0.5d));

            FourMountSlotStatusSnapshot ready = snapshot.GetByStableIndex(3);
            Assert.That(ready.IsReady, Is.True);
            Assert.That(ready.Phase, Is.EqualTo(GunMountPhase.Ready));
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
                fixture.GunIds);

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
        public void AcceptedHudContract_ProjectsCanonicalRowsWithoutFabricatingFireResults()
        {
            Fixture fixture = CreateMixedFixture();
            ContractPresentation.GunHudState hud =
                new FourMountStatusProjector().ProjectAcceptedHudState(
                    fixture.State,
                    fixture.Profiles,
                    fixture.GunIds);

            Assert.That(hud.Count, Is.EqualTo(ContractCombat.GunMountContractRules.MountCount));
            for (int stableIndex = 0; stableIndex < hud.Count; stableIndex++)
            {
                ContractPresentation.GunHudSlotState row = hud.GetByHudIndex(stableIndex);
                Assert.That(
                    row.Slot,
                    Is.EqualTo(ContractCombat.GunMountContractRules.GetSlotAtHudIndex(stableIndex)));
                Assert.That(row.LatestFireResult, Is.Null);
            }

            Assert.That(
                hud.GetByHudIndex(0).CycleResource.Kind,
                Is.EqualTo(ContractCombat.GunCycleResourceKind.Heat));
            Assert.That(hud.GetByHudIndex(0).CycleResource.Current, Is.EqualTo(4d));
            Assert.That(hud.GetByHudIndex(0).PowerBank.AvailableUnits, Is.EqualTo(8d));
            Assert.That(
                hud.GetByHudIndex(1).Readiness,
                Is.EqualTo(ContractCombat.GunMountReadiness.Charging));
            Assert.That(
                hud.GetByHudIndex(2).Readiness,
                Is.EqualTo(ContractCombat.GunMountReadiness.Recovering));
            Assert.That(
                hud.GetByHudIndex(3).Readiness,
                Is.EqualTo(ContractCombat.GunMountReadiness.Ready));
        }

        [Test]
        public void NormalMode_ComesFromCoordinatorDecision()
        {
            FlowFixture fixture = CreateCoordinatorFixture(
                new[] { 10d, 10d, 10d, 10d });
            FourMountCombatStepResult result = fixture.Step(
                fireRequested: true,
                empoweredRequested: false);

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                result.State,
                fixture.Profiles,
                fixture.GunIds,
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
            FlowFixture fixture = CreateCoordinatorFixture(
                new[] { 10d, 0d, 5d, 0d });
            FourMountCombatStepResult result = fixture.Step(
                fireRequested: true,
                empoweredRequested: true);

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                result.State,
                fixture.Profiles,
                fixture.GunIds,
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
            FlowFixture fixture = CreateCoordinatorFixture(
                new[] { 10d, 10d, 10d, 10d });
            FourMountCombatStepResult result = fixture.Step(
                fireRequested: true,
                empoweredRequested: true,
                externalFaultDetails: new[] { null, "synthetic mount bus fault", null, null });

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                result.State,
                fixture.Profiles,
                fixture.GunIds,
                result);

            FourMountSlotStatusSnapshot faulted = snapshot.GetByStableIndex(1);
            Assert.That(faulted.IsFaulted, Is.True);
            Assert.That(faulted.Phase, Is.EqualTo(GunMountPhase.Faulted));
            Assert.That(faulted.FireMode, Is.EqualTo(FourMountFireMode.Faulted));
            Assert.That(faulted.FaultKind, Is.EqualTo(GunMountFaultKind.ExternalFault));
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
                fixture.GunIds);
            string projectedBefore = snapshot.ToTraceString();

            fixture.Profiles[0] = fixture.Profiles[3];
            fixture.GunIds[0] = StableId.Parse("gun.cb010-array-replaced");

            Assert.That(fixture.State.ToTraceString(), Is.EqualTo(sourceBefore));
            Assert.That(snapshot.ToTraceString(), Is.EqualTo(projectedBefore));
            Assert.That(
                snapshot.GetByStableIndex(0).GunId,
                Is.EqualTo(StableId.Parse("gun.cb010-mixed-slot-1")));
        }

        [Test]
        public void MissingSlot_FailsVisiblyInsteadOfFabricatingState()
        {
            Fixture fixture = CreateMixedFixture();
            GunLiveProfile[] onlyThreeProfiles =
            {
                fixture.Profiles[0],
                fixture.Profiles[1],
                fixture.Profiles[2],
            };

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                new FourMountStatusProjector().Project(
                    fixture.State,
                    onlyThreeProfiles,
                    fixture.GunIds));

            Assert.That(exception.Message, Does.Contain("Exactly four"));
        }

        [Test]
        public void MissingSnapshotSlotAndDuplicateSlot_AreRejected()
        {
            Fixture fixture = CreateMixedFixture();
            FourMountStatusSnapshot projected = new FourMountStatusProjector().Project(
                fixture.State,
                fixture.Profiles,
                fixture.GunIds);

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
                fixture.GunIds);
            FourMountStatusSnapshot second = projector.Project(
                fixture.State,
                fixture.Profiles,
                fixture.GunIds);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.GetByStableIndex(0), Is.Not.SameAs(first.GetByStableIndex(0)));
            Assert.That(second.ToTraceString(), Is.EqualTo(first.ToTraceString()));
        }

        [Test]
        public void UnequippedSlot_RemainsPresentWithNeutralReadableState()
        {
            Fixture fixture = CreateMixedFixture();
            fixture.Profiles[3] = null;
            fixture.GunIds[3] = null;

            FourMountStatusSnapshot snapshot = new FourMountStatusProjector().Project(
                fixture.State,
                fixture.Profiles,
                fixture.GunIds);

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
            GunLiveProfile[] profiles =
            {
                BuildProfile(
                    "mixed-slot-1",
                    cycleMode: GunCycleMode.Heat,
                    heatCapacityUnits: 10d,
                    heatPerShotUnits: 4d,
                    heatRecoveryUnitsPerSecond: 2d,
                    recoverySeconds: 0.25d,
                    hasPowerBank: true),
                BuildProfile(
                    "mixed-slot-2",
                    cycleMode: GunCycleMode.Charge,
                    chargeSeconds: 0.5d,
                    hasPowerBank: true),
                BuildProfile(
                    "mixed-slot-3",
                    recoverySeconds: 0.5d,
                    hasPowerBank: true),
                BuildProfile("mixed-slot-4"),
            };

            GunMountState[] mounts =
            {
                GunMountStepper.Step(
                    profiles[0],
                    GunMountState.Initial(profiles[0]),
                    0d,
                    true).State,
                GunMountStepper.Step(
                    profiles[1],
                    GunMountState.Initial(profiles[1]),
                    0d,
                    true).State,
                GunMountStepper.Step(
                    profiles[2],
                    GunMountState.Initial(profiles[2]),
                    0d,
                    true).State,
                GunMountState.Initial(profiles[3]),
            };

            GunPowerBankState[] banks =
            {
                GunPowerBankState.FromProfile(profiles[0], 8d),
                GunPowerBankState.FromProfile(profiles[1], 4d),
                GunPowerBankState.FromProfile(profiles[2], 0d),
                GunPowerBankState.None,
            };

            StableId[] gunIds =
            {
                StableId.Parse("gun.cb010-mixed-slot-1"),
                StableId.Parse("gun.cb010-mixed-slot-2"),
                StableId.Parse("gun.cb010-mixed-slot-3"),
                StableId.Parse("gun.cb010-mixed-slot-4"),
            };

            return new Fixture(
                profiles,
                gunIds,
                new FourMountCombatState(mounts, banks));
        }

        private static FlowFixture CreateCoordinatorFixture(double[] initialPower)
        {
            GunLiveProfile[] profiles = new GunLiveProfile[FourMountCombatState.MountCount];
            StableId[] gunIds = new StableId[FourMountCombatState.MountCount];
            StableId[] mountIds = new StableId[FourMountCombatState.MountCount];
            GunMountOrigin[] origins = new GunMountOrigin[FourMountCombatState.MountCount];

            for (int stableIndex = 0; stableIndex < FourMountCombatState.MountCount; stableIndex++)
            {
                profiles[stableIndex] = BuildProfile(
                    "coordinator-slot-" + (stableIndex + 1),
                    hasPowerBank: true,
                    presentationPriority: stableIndex);
                gunIds[stableIndex] = StableId.Parse(
                    "gun.cb010-coordinator-slot-" + (stableIndex + 1));
                mountIds[stableIndex] = StableId.Parse(
                    "mount.cb010-coordinator-slot-" + (stableIndex + 1));
                origins[stableIndex] = new GunMountOrigin(
                    stableIndex + 1,
                    new AimVector2(stableIndex, 0d));
            }

            GunBehaviorPipeline pipeline = new GunBehaviorPipeline(
                new IGunBehaviorModule[] { new EmptyModule() });
            return new FlowFixture(
                profiles,
                gunIds,
                mountIds,
                origins,
                FourMountCombatState.Initial(profiles, initialPower),
                new FourMountCombatStepper(new FourMountAimResolver(), pipeline));
        }

        private static GunLiveProfile BuildProfile(
            string suffix,
            double cadenceSeconds = 0.2d,
            double recoverySeconds = 0d,
            GunCycleMode cycleMode = GunCycleMode.None,
            double heatCapacityUnits = 0d,
            double heatPerShotUnits = 0d,
            double heatRecoveryUnitsPerSecond = 0d,
            double chargeSeconds = 0d,
            bool hasPowerBank = false,
            int presentationPriority = 0)
        {
            return GunLiveProfile.Create(
                GunLiveProfile.CurrentProfileVersion,
                StableId.Parse("gun-profile.cb010-" + suffix),
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
                GunLiveProfile[] profiles,
                StableId[] gunIds,
                FourMountCombatState state)
            {
                Profiles = profiles;
                GunIds = gunIds;
                State = state;
            }

            public GunLiveProfile[] Profiles { get; }

            public StableId[] GunIds { get; }

            public FourMountCombatState State { get; }
        }

        private sealed class FlowFixture
        {
            private long simulationStep;

            public FlowFixture(
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
                    GunIds,
                    MountIds,
                    Origins,
                    externalFaultDetails);
                FourMountCombatStepResult result = Stepper.Step(State, input);
                State = result.State;
                return result;
            }
        }

        private sealed class EmptyModule : IGunBehaviorModule
        {
            public StableId ModuleId => FourMountStatusProjectorTests.ModuleId;

            public GunBehaviorModulePlan BuildExecutionPlan(GunBehaviorInput input)
            {
                return new GunBehaviorModulePlan(ModuleId);
            }
        }
    }
}

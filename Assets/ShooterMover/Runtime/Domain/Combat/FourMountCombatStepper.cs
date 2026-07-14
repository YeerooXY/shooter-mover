using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    public sealed class FourMountCombatStepInput
    {
        private readonly WeaponRuntimeProfile[] profiles;
        private readonly StableId[] weaponIds;
        private readonly StableId[] mountIds;
        private readonly WeaponMountOrigin[] mountOrigins;
        private readonly string[] externalFaultDetails;

        public FourMountCombatStepInput(
            long simulationStep,
            double elapsedSeconds,
            bool fireRequested,
            bool empoweredRequested,
            AimVector2 sharedAimIntent,
            AimVector2 sharedAimPoint,
            WeaponRuntimeProfile[] profiles,
            StableId[] weaponIds,
            StableId[] mountIds,
            WeaponMountOrigin[] mountOrigins,
            string[] externalFaultDetails = null)
        {
            if (simulationStep < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationStep));
            }

            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            FourMountCombatState.ValidateFour(profiles, nameof(profiles));
            FourMountCombatState.ValidateFour(weaponIds, nameof(weaponIds));
            FourMountCombatState.ValidateFour(mountIds, nameof(mountIds));
            if (mountOrigins == null || mountOrigins.Length != FourMountCombatState.MountCount)
            {
                throw new ArgumentException("Exactly four mount origins are required.", nameof(mountOrigins));
            }

            if (externalFaultDetails != null
                && externalFaultDetails.Length != FourMountCombatState.MountCount)
            {
                throw new ArgumentException("Fault details must be null or contain exactly four entries.", nameof(externalFaultDetails));
            }

            SimulationStep = simulationStep;
            ElapsedSeconds = elapsedSeconds;
            FireRequested = fireRequested;
            EmpoweredRequested = empoweredRequested;
            SharedAimIntent = sharedAimIntent;
            SharedAimPoint = sharedAimPoint;
            this.profiles = (WeaponRuntimeProfile[])profiles.Clone();
            this.weaponIds = (StableId[])weaponIds.Clone();
            this.mountIds = (StableId[])mountIds.Clone();
            this.mountOrigins = (WeaponMountOrigin[])mountOrigins.Clone();
            this.externalFaultDetails = externalFaultDetails == null
                ? new string[FourMountCombatState.MountCount]
                : (string[])externalFaultDetails.Clone();
        }

        public long SimulationStep { get; }
        public double ElapsedSeconds { get; }
        public bool FireRequested { get; }
        public bool EmpoweredRequested { get; }
        public AimVector2 SharedAimIntent { get; }
        public AimVector2 SharedAimPoint { get; }

        public WeaponRuntimeProfile GetProfile(int index) { return profiles[index]; }
        public StableId GetWeaponId(int index) { return weaponIds[index]; }
        public StableId GetMountId(int index) { return mountIds[index]; }
        public WeaponMountOrigin GetMountOrigin(int index) { return mountOrigins[index]; }
        public string GetExternalFaultDetail(int index) { return externalFaultDetails[index]; }

        internal WeaponMountOrigin[] CopyOrigins()
        {
            return (WeaponMountOrigin[])mountOrigins.Clone();
        }
    }

    public sealed class FourMountCombatLaneResult
    {
        internal FourMountCombatLaneResult(
            int stableSlotNumber,
            WeaponMountStepResult mountResult,
            WeaponPowerFireDecision powerDecision,
            WeaponFireExecutionPlan executionPlan)
        {
            StableSlotNumber = stableSlotNumber;
            MountResult = mountResult ?? throw new ArgumentNullException(nameof(mountResult));
            PowerDecision = powerDecision;
            ExecutionPlan = executionPlan;
        }

        public int StableSlotNumber { get; }
        public WeaponMountStepResult MountResult { get; }
        public WeaponPowerFireDecision PowerDecision { get; }
        public WeaponFireExecutionPlan ExecutionPlan { get; }
        public bool IsFaulted => MountResult.State.IsFaulted;
        public int ShotsFired => MountResult.ShotsFired;
    }

    public sealed class FourMountCombatStepResult
    {
        private readonly FourMountCombatLaneResult[] lanes;

        internal FourMountCombatStepResult(
            FourMountCombatState state,
            FourMountAimSolution aimSolution,
            FourMountCombatLaneResult[] lanes)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            AimSolution = aimSolution ?? throw new ArgumentNullException(nameof(aimSolution));
            this.lanes = lanes ?? throw new ArgumentNullException(nameof(lanes));
        }

        public FourMountCombatState State { get; }
        public FourMountAimSolution AimSolution { get; }
        public FourMountCombatLaneResult GetLaneByStableIndex(int index) { return lanes[index]; }

        public string ToTimelineRow()
        {
            string[] values = new string[FourMountCombatState.MountCount];
            for (int index = 0; index < values.Length; index++)
            {
                FourMountCombatLaneResult lane = lanes[index];
                string power = lane.PowerDecision == null ? "fault" : lane.PowerDecision.Kind.ToString();
                values[index] = string.Format(
                    CultureInfo.InvariantCulture,
                    "S{0}[shots={1};phase={2};power={3};fault={4}]",
                    lane.StableSlotNumber,
                    lane.ShotsFired,
                    lane.MountResult.State.Phase,
                    power,
                    lane.IsFaulted ? lane.MountResult.Fault.Kind.ToString() : "none");
            }

            return string.Join(" | ", values);
        }
    }

    /// <summary>
    /// Applies one sampled input stream to four independent mount lanes in fixed slot order.
    /// Every lane is guarded separately so a profile, policy, behavior, or external fault
    /// can fail closed without suppressing results from the other three lanes.
    /// </summary>
    public sealed class FourMountCombatStepper
    {
        private readonly FourMountAimResolver aimResolver;
        private readonly WeaponBehaviorPipeline behaviorPipeline;

        public FourMountCombatStepper(
            FourMountAimResolver aimResolver,
            WeaponBehaviorPipeline behaviorPipeline)
        {
            this.aimResolver = aimResolver ?? throw new ArgumentNullException(nameof(aimResolver));
            this.behaviorPipeline = behaviorPipeline ?? throw new ArgumentNullException(nameof(behaviorPipeline));
        }

        public FourMountCombatStepResult Step(
            FourMountCombatState state,
            FourMountCombatStepInput input)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (input == null) throw new ArgumentNullException(nameof(input));

            FourMountAimSolution aim = aimResolver.Resolve(
                input.SharedAimIntent,
                input.SharedAimPoint,
                input.CopyOrigins());

            WeaponMountState[] nextMounts = new WeaponMountState[FourMountCombatState.MountCount];
            WeaponPowerBankState[] nextBanks = new WeaponPowerBankState[FourMountCombatState.MountCount];
            FourMountCombatLaneResult[] lanes = new FourMountCombatLaneResult[FourMountCombatState.MountCount];

            for (int index = 0; index < FourMountCombatState.MountCount; index++)
            {
                StepLane(state, input, aim, index, out nextMounts[index], out nextBanks[index], out lanes[index]);
            }

            return new FourMountCombatStepResult(
                new FourMountCombatState(nextMounts, nextBanks),
                aim,
                lanes);
        }

        private void StepLane(
            FourMountCombatState state,
            FourMountCombatStepInput input,
            FourMountAimSolution aim,
            int index,
            out WeaponMountState nextMount,
            out WeaponPowerBankState nextBank,
            out FourMountCombatLaneResult lane)
        {
            WeaponMountState mountBefore = state.GetMountByStableIndex(index);
            WeaponPowerBankState bankBefore = state.GetPowerBankByStableIndex(index);

            try
            {
                WeaponRuntimeProfile profile = input.GetProfile(index);
                string externalFault = input.GetExternalFaultDetail(index);
                if (!string.IsNullOrWhiteSpace(externalFault))
                {
                    WeaponMountStepResult faulted = WeaponMountStepper.Step(
                        profile,
                        mountBefore,
                        0d,
                        WeaponMountStepInput.Fault(externalFault));
                    nextMount = faulted.State;
                    nextBank = bankBefore;
                    lane = new FourMountCombatLaneResult(index + 1, faulted, null, null);
                    return;
                }

                bool requestsCycle = input.FireRequested && mountBefore.IsReady;
                WeaponPowerFireDecision power = WeaponPowerBankPolicy.ResolveFire(
                    bankBefore,
                    requestsCycle,
                    input.EmpoweredRequested);

                WeaponMountStepResult mountResult = WeaponMountStepper.Step(
                    profile,
                    mountBefore,
                    input.ElapsedSeconds,
                    new WeaponMountStepInput(input.FireRequested && power.Fires));

                WeaponFireExecutionPlan plan = null;
                if (mountResult.CyclesStarted > 0 && !mountResult.State.IsFaulted)
                {
                    SharedAimSolution solution = aim.GetByStableIndex(index);
                    StableId combatEventId = StableId.Parse(
                        "combat-event.cb006-step-"
                        + input.SimulationStep.ToString(CultureInfo.InvariantCulture)
                        + "-slot-"
                        + (index + 1).ToString(CultureInfo.InvariantCulture));
                    WeaponBehaviorInput behaviorInput = new WeaponBehaviorInput(
                        combatEventId,
                        input.GetWeaponId(index),
                        input.GetMountId(index),
                        input.SimulationStep,
                        profile,
                        power.FiresEmpowered,
                        solution.Origin.X,
                        solution.Origin.Y,
                        solution.Direction.X,
                        solution.Direction.Y,
                        1d);
                    plan = behaviorPipeline.BuildExecutionPlan(behaviorInput);
                }

                nextMount = mountResult.State;
                nextBank = power.UpdatedState;
                lane = new FourMountCombatLaneResult(index + 1, mountResult, power, plan);
            }
            catch (Exception exception)
            {
                WeaponMountStepResult faulted = WeaponMountStepper.Step(
                    input.GetProfile(index),
                    mountBefore,
                    0d,
                    WeaponMountStepInput.Fault(
                        "CB-006 isolated slot "
                        + (index + 1).ToString(CultureInfo.InvariantCulture)
                        + ": "
                        + exception.GetType().Name));
                nextMount = faulted.State;
                nextBank = bankBefore;
                lane = new FourMountCombatLaneResult(index + 1, faulted, null, null);
            }
        }
    }
}

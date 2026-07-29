using System;
using ShooterMover.Domain.Common;
using ContractCombat = ShooterMover.Contracts.Combat;
using ContractPresentation = ShooterMover.Contracts.Presentation;
using DomainCombat = ShooterMover.Domain.Combat;

namespace ShooterMover.Application.Combat
{
    /// <summary>
    /// Pure read-only projection from authoritative four-mount domain state into a
    /// detached HUD-facing snapshot. The accepted CS-005 contract remains the source
    /// of truth for four-slot count and canonical HUD order.
    /// </summary>
    public sealed class FourMountStatusProjector
    {
        public DomainCombat.FourMountStatusSnapshot Project(
            DomainCombat.FourMountCombatState combatState,
            DomainCombat.GunLiveProfile[] profiles,
            StableId[] gunIds,
            DomainCombat.FourMountCombatStepResult latestStepResult = null)
        {
            if (combatState == null)
            {
                throw new ArgumentNullException(nameof(combatState));
            }

            ValidateContractCompatibility();
            ValidateFour(profiles, nameof(profiles), allowNullElements: true);
            ValidateFour(gunIds, nameof(gunIds), allowNullElements: true);

            if (latestStepResult != null)
            {
                ValidateLatestResultMatchesState(combatState, latestStepResult);
            }

            DomainCombat.FourMountSlotStatusSnapshot[] slots =
                new DomainCombat.FourMountSlotStatusSnapshot[ContractCombat.GunMountContractRules.MountCount];

            for (int stableIndex = 0; stableIndex < slots.Length; stableIndex++)
            {
                ContractCombat.GunMountSlot contractSlot =
                    ContractCombat.GunMountContractRules.GetSlotAtHudIndex(stableIndex);
                int stableSlotNumber = (int)contractSlot;
                DomainCombat.GunLiveProfile profile = profiles[stableIndex];
                StableId gunId = gunIds[stableIndex];
                DomainCombat.GunMountState mountState =
                    combatState.GetMountByStableIndex(stableIndex);
                DomainCombat.GunPowerBankState powerBank =
                    combatState.GetPowerBankByStableIndex(stableIndex);

                if (profile == null || gunId == null)
                {
                    if (profile != null || gunId != null)
                    {
                        throw new ArgumentException(
                            "An unequipped slot requires both a null profile and null gun identity.");
                    }

                    if (latestStepResult != null)
                    {
                        throw new ArgumentException(
                            "A latest coordinator result cannot be attached to an unequipped slot.",
                            nameof(latestStepResult));
                    }

                    ValidateNeutralUnequippedSource(mountState, powerBank, stableSlotNumber);
                    slots[stableIndex] =
                        DomainCombat.FourMountSlotStatusSnapshot.Unequipped(stableSlotNumber);
                    continue;
                }

                ValidateProfileAndState(profile, mountState, powerBank, stableSlotNumber);

                double cycleCurrent;
                double cycleMaximum;
                GetCycleValues(profile, mountState, out cycleCurrent, out cycleMaximum);

                DomainCombat.FourMountFireMode fireMode = ResolveFireMode(
                    latestStepResult,
                    stableIndex,
                    stableSlotNumber,
                    gunId,
                    mountState,
                    powerBank);

                DomainCombat.GunMountFault fault = mountState.Fault;
                slots[stableIndex] = new DomainCombat.FourMountSlotStatusSnapshot(
                    stableSlotNumber,
                    true,
                    gunId,
                    mountState.Phase,
                    mountState.IsReady,
                    mountState.CadenceRemainingSeconds,
                    mountState.BurstShotsRemaining,
                    mountState.RecoveryRemainingSeconds,
                    profile.CycleMode,
                    cycleCurrent,
                    cycleMaximum,
                    powerBank.IsConfigured,
                    powerBank.AvailableUnits,
                    powerBank.CapacityUnits,
                    powerBank.CanAffordEmpoweredFire,
                    fireMode,
                    fault == null ? (DomainCombat.GunMountFaultKind?)null : fault.Kind,
                    fault == null ? null : fault.Detail);
            }

            return new DomainCombat.FourMountStatusSnapshot(slots);
        }

        /// <summary>
        /// Projects the same authoritative source into the accepted CS-005 mount/HUD
        /// contract. Its optional latest-fire result is intentionally left empty because
        /// CB-006 does not own a CombatChannel and this projector must not fabricate one.
        /// Exact coordinator mode, fallback, recovery duration, and fault detail remain
        /// available on the richer FourMountStatusSnapshot returned by Project.
        /// </summary>
        public ContractPresentation.GunHudState ProjectAcceptedHudState(
            DomainCombat.FourMountCombatState combatState,
            DomainCombat.GunLiveProfile[] profiles,
            StableId[] gunIds)
        {
            DomainCombat.FourMountStatusSnapshot status = Project(
                combatState,
                profiles,
                gunIds);
            ContractCombat.GunMountState[] mounts =
                new ContractCombat.GunMountState[ContractCombat.GunMountContractRules.MountCount];

            for (int stableIndex = 0; stableIndex < mounts.Length; stableIndex++)
            {
                DomainCombat.FourMountSlotStatusSnapshot slot = status.GetByStableIndex(stableIndex);
                ContractCombat.GunMountSlot contractSlot =
                    ContractCombat.GunMountContractRules.GetSlotAtHudIndex(stableIndex);

                if (!slot.IsEquipped)
                {
                    mounts[stableIndex] = ContractCombat.GunMountState.Unequipped(contractSlot);
                    continue;
                }

                DomainCombat.GunLiveProfile profile = profiles[stableIndex];
                DomainCombat.GunMountState sourceMount =
                    combatState.GetMountByStableIndex(stableIndex);
                DomainCombat.GunPowerBankState sourcePower =
                    combatState.GetPowerBankByStableIndex(stableIndex);
                ContractCombat.GunMountReadiness readiness =
                    MapAcceptedReadiness(profile, sourceMount);
                double cadenceRemaining = sourceMount.CadenceRemainingSeconds;

                if (readiness == ContractCombat.GunMountReadiness.CadenceBlocked)
                {
                    cadenceRemaining = Math.Max(
                        sourceMount.CadenceRemainingSeconds,
                        sourceMount.BurstIntervalRemainingSeconds);
                    if (cadenceRemaining == 0d)
                    {
                        throw new ArgumentException(
                            "A firing mount must expose a positive cadence or burst interval.",
                            nameof(combatState));
                    }
                }

                mounts[stableIndex] = new ContractCombat.GunMountState(
                    contractSlot,
                    slot.GunId,
                    readiness,
                    new ContractCombat.GunCadenceState(
                        cadenceRemaining,
                        sourceMount.BurstShotsRemaining),
                    BuildAcceptedCycleResource(profile, sourceMount),
                    new ContractCombat.GunRecoilState(0d, profile.RecoilInfluence),
                    BuildAcceptedPowerBank(sourcePower));
            }

            return new ContractPresentation.GunHudState(
                new ContractCombat.FourMountGunState(mounts));
        }

        private static ContractCombat.GunMountReadiness MapAcceptedReadiness(
            DomainCombat.GunLiveProfile profile,
            DomainCombat.GunMountState mountState)
        {
            switch (mountState.Phase)
            {
                case DomainCombat.GunMountPhase.Ready:
                    return ContractCombat.GunMountReadiness.Ready;
                case DomainCombat.GunMountPhase.Firing:
                    return ContractCombat.GunMountReadiness.CadenceBlocked;
                case DomainCombat.GunMountPhase.Recovering:
                    return ContractCombat.GunMountReadiness.Recovering;
                case DomainCombat.GunMountPhase.Depleted:
                    if (profile.CycleMode == DomainCombat.GunCycleMode.Charge)
                    {
                        return ContractCombat.GunMountReadiness.Charging;
                    }

                    if (profile.CycleMode == DomainCombat.GunCycleMode.Heat
                        && mountState.HeatUnits == profile.HeatCapacityUnits)
                    {
                        return ContractCombat.GunMountReadiness.Overheated;
                    }

                    return ContractCombat.GunMountReadiness.Recovering;
                case DomainCombat.GunMountPhase.Faulted:
                    return ContractCombat.GunMountReadiness.Faulted;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(mountState),
                        mountState.Phase,
                        "Unknown gun-mount phase.");
            }
        }

        private static ContractCombat.GunCycleResourceState BuildAcceptedCycleResource(
            DomainCombat.GunLiveProfile profile,
            DomainCombat.GunMountState mountState)
        {
            switch (profile.CycleMode)
            {
                case DomainCombat.GunCycleMode.None:
                    return ContractCombat.GunCycleResourceState.None;
                case DomainCombat.GunCycleMode.Heat:
                    return new ContractCombat.GunCycleResourceState(
                        ContractCombat.GunCycleResourceKind.Heat,
                        mountState.HeatUnits,
                        profile.HeatCapacityUnits);
                case DomainCombat.GunCycleMode.Charge:
                    return new ContractCombat.GunCycleResourceState(
                        ContractCombat.GunCycleResourceKind.Charge,
                        mountState.ChargeProgressSeconds,
                        profile.ChargeSeconds);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(profile),
                        profile.CycleMode,
                        "Unknown gun cycle mode.");
            }
        }

        private static ContractCombat.GunPowerBankState BuildAcceptedPowerBank(
            DomainCombat.GunPowerBankState powerBank)
        {
            return powerBank.IsConfigured
                ? new ContractCombat.GunPowerBankState(
                    true,
                    powerBank.AvailableUnits,
                    powerBank.CapacityUnits,
                    powerBank.EmpoweredCostUnits)
                : ContractCombat.GunPowerBankState.None;
        }

        private static void ValidateContractCompatibility()
        {
            if (DomainCombat.FourMountCombatState.MountCount
                != ContractCombat.GunMountContractRules.MountCount)
            {
                throw new InvalidOperationException(
                    "The domain and accepted HUD contract disagree on four-mount slot count.");
            }
        }

        private static void ValidateFour<T>(
            T[] values,
            string parameterName,
            bool allowNullElements)
            where T : class
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (values.Length != ContractCombat.GunMountContractRules.MountCount)
            {
                throw new ArgumentException(
                    "Exactly four stable-slot values are required.",
                    parameterName);
            }

            if (allowNullElements)
            {
                return;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == null)
                {
                    throw new ArgumentException(
                        "Stable-slot values cannot contain null.",
                        parameterName);
                }
            }
        }

        private static void ValidateNeutralUnequippedSource(
            DomainCombat.GunMountState mountState,
            DomainCombat.GunPowerBankState powerBank,
            int stableSlotNumber)
        {
            if (mountState == null || powerBank == null)
            {
                throw new ArgumentException(
                    "Unequipped slots still require neutral authoritative source objects.");
            }

            bool neutralMount = mountState.Phase == DomainCombat.GunMountPhase.Ready
                && mountState.CadenceRemainingSeconds == 0d
                && mountState.BurstShotsRemaining == 0
                && mountState.BurstIntervalRemainingSeconds == 0d
                && mountState.RecoveryRemainingSeconds == 0d
                && mountState.HeatUnits == 0d
                && !mountState.HeatRecoveryLocked
                && mountState.ChargeProgressSeconds == 0d
                && mountState.TotalShotsFired == 0L
                && mountState.TotalCyclesStarted == 0L
                && mountState.Fault == null;

            if (!neutralMount || powerBank.IsConfigured)
            {
                throw new ArgumentException(
                    "Unequipped slot "
                    + stableSlotNumber
                    + " must use neutral mount and power-bank source state.");
            }
        }

        private static void ValidateProfileAndState(
            DomainCombat.GunLiveProfile profile,
            DomainCombat.GunMountState mountState,
            DomainCombat.GunPowerBankState powerBank,
            int stableSlotNumber)
        {
            if (mountState == null)
            {
                throw new ArgumentException(
                    "Equipped slot " + stableSlotNumber + " is missing mount state.");
            }

            if (powerBank == null)
            {
                throw new ArgumentException(
                    "Equipped slot " + stableSlotNumber + " is missing power-bank state.");
            }

            if (profile.CycleMode == DomainCombat.GunCycleMode.None)
            {
                if (mountState.HeatUnits != 0d || mountState.ChargeProgressSeconds != 0d)
                {
                    throw new ArgumentException(
                        "A no-resource profile cannot expose heat or charge state.");
                }
            }
            else if (profile.CycleMode == DomainCombat.GunCycleMode.Heat)
            {
                if (mountState.HeatUnits > profile.HeatCapacityUnits
                    || mountState.ChargeProgressSeconds != 0d)
                {
                    throw new ArgumentException(
                        "Heat state is inconsistent with the validated runtime profile.");
                }
            }
            else if (profile.CycleMode == DomainCombat.GunCycleMode.Charge)
            {
                if (mountState.ChargeProgressSeconds > profile.ChargeSeconds
                    || mountState.HeatUnits != 0d
                    || mountState.HeatRecoveryLocked)
                {
                    throw new ArgumentException(
                        "Charge state is inconsistent with the validated runtime profile.");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(profile),
                    profile.CycleMode,
                    "Unknown gun cycle mode.");
            }

            if (profile.HasIndependentPowerBank != powerBank.IsConfigured)
            {
                throw new ArgumentException(
                    "Power-bank configuration is inconsistent with the runtime profile.");
            }

            if (powerBank.IsConfigured
                && (powerBank.CapacityUnits != profile.PowerBankCapacityUnits
                    || powerBank.EmpoweredCostUnits != profile.EmpoweredCostUnits))
            {
                throw new ArgumentException(
                    "Power-bank capacity or empowered cost is inconsistent with the runtime profile.");
            }
        }

        private static void GetCycleValues(
            DomainCombat.GunLiveProfile profile,
            DomainCombat.GunMountState mountState,
            out double current,
            out double maximum)
        {
            switch (profile.CycleMode)
            {
                case DomainCombat.GunCycleMode.None:
                    current = 0d;
                    maximum = 0d;
                    return;
                case DomainCombat.GunCycleMode.Heat:
                    current = mountState.HeatUnits;
                    maximum = profile.HeatCapacityUnits;
                    return;
                case DomainCombat.GunCycleMode.Charge:
                    current = mountState.ChargeProgressSeconds;
                    maximum = profile.ChargeSeconds;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile.CycleMode, "Unknown cycle mode.");
            }
        }

        private static DomainCombat.FourMountFireMode ResolveFireMode(
            DomainCombat.FourMountCombatStepResult latestStepResult,
            int stableIndex,
            int stableSlotNumber,
            StableId gunId,
            DomainCombat.GunMountState mountState,
            DomainCombat.GunPowerBankState powerBank)
        {
            if (mountState.IsFaulted)
            {
                return DomainCombat.FourMountFireMode.Faulted;
            }

            if (latestStepResult == null)
            {
                return DomainCombat.FourMountFireMode.NoRecentAttempt;
            }

            DomainCombat.FourMountCombatLaneResult lane =
                latestStepResult.GetLaneByStableIndex(stableIndex);
            if (lane == null)
            {
                throw new ArgumentException(
                    "Latest coordinator result is missing slot " + stableSlotNumber + ".",
                    nameof(latestStepResult));
            }

            if (lane.StableSlotNumber != stableSlotNumber)
            {
                throw new ArgumentException(
                    "Latest coordinator result does not preserve stable slot order.",
                    nameof(latestStepResult));
            }

            if (!SameMountState(lane.MountResult.State, mountState))
            {
                throw new ArgumentException(
                    "Latest lane mount state does not match the projected authoritative state.",
                    nameof(latestStepResult));
            }

            DomainCombat.GunPowerFireDecision decision = lane.PowerDecision;
            if (decision == null)
            {
                throw new ArgumentException(
                    "A healthy equipped lane requires an explicit latest power/fire decision.",
                    nameof(latestStepResult));
            }

            if (!SamePowerState(decision.UpdatedState, powerBank))
            {
                throw new ArgumentException(
                    "Latest lane power state does not match the projected authoritative state.",
                    nameof(latestStepResult));
            }

            if (lane.ExecutionPlan != null && lane.ExecutionPlan.GunId != gunId)
            {
                throw new ArgumentException(
                    "Latest execution-plan gun identity does not match its stable slot.",
                    nameof(gunId));
            }

            switch (decision.Kind)
            {
                case DomainCombat.GunPowerFireDecisionKind.NormalFired:
                    return DomainCombat.FourMountFireMode.Normal;
                case DomainCombat.GunPowerFireDecisionKind.EmpoweredFired:
                    return DomainCombat.FourMountFireMode.Empowered;
                case DomainCombat.GunPowerFireDecisionKind.NormalFallbackPowerUnavailable:
                    return DomainCombat.FourMountFireMode.NormalFallbackPowerUnavailable;
                case DomainCombat.GunPowerFireDecisionKind.NotReady:
                    return DomainCombat.FourMountFireMode.NotReady;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(latestStepResult),
                        decision.Kind,
                        "Unknown latest power/fire decision.");
            }
        }

        private static void ValidateLatestResultMatchesState(
            DomainCombat.FourMountCombatState combatState,
            DomainCombat.FourMountCombatStepResult latestStepResult)
        {
            if (latestStepResult.State == null)
            {
                throw new ArgumentException(
                    "Latest coordinator result is missing its authoritative state.",
                    nameof(latestStepResult));
            }

            for (int stableIndex = 0; stableIndex < DomainCombat.FourMountCombatState.MountCount; stableIndex++)
            {
                if (!SameMountState(
                        combatState.GetMountByStableIndex(stableIndex),
                        latestStepResult.State.GetMountByStableIndex(stableIndex))
                    || !SamePowerState(
                        combatState.GetPowerBankByStableIndex(stableIndex),
                        latestStepResult.State.GetPowerBankByStableIndex(stableIndex)))
                {
                    throw new ArgumentException(
                        "Latest coordinator result does not describe the supplied authoritative state.",
                        nameof(latestStepResult));
                }
            }
        }

        private static bool SameMountState(
            DomainCombat.GunMountState left,
            DomainCombat.GunMountState right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            bool sameFault = left.Fault == null && right.Fault == null;
            if (left.Fault != null && right.Fault != null)
            {
                sameFault = left.Fault.Kind == right.Fault.Kind
                    && string.Equals(left.Fault.Detail, right.Fault.Detail, StringComparison.Ordinal);
            }

            return left.Phase == right.Phase
                && left.CadenceRemainingSeconds == right.CadenceRemainingSeconds
                && left.BurstShotsRemaining == right.BurstShotsRemaining
                && left.BurstIntervalRemainingSeconds == right.BurstIntervalRemainingSeconds
                && left.RecoveryRemainingSeconds == right.RecoveryRemainingSeconds
                && left.HeatUnits == right.HeatUnits
                && left.HeatRecoveryLocked == right.HeatRecoveryLocked
                && left.ChargeProgressSeconds == right.ChargeProgressSeconds
                && left.TotalShotsFired == right.TotalShotsFired
                && left.TotalCyclesStarted == right.TotalCyclesStarted
                && sameFault;
        }

        private static bool SamePowerState(
            DomainCombat.GunPowerBankState left,
            DomainCombat.GunPowerBankState right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return left != null
                && right != null
                && left.IsConfigured == right.IsConfigured
                && left.AvailableUnits == right.AvailableUnits
                && left.CapacityUnits == right.CapacityUnits
                && left.EmpoweredCostUnits == right.EmpoweredCostUnits;
        }
    }
}

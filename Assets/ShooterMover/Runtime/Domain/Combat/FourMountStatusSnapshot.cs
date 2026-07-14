using System;
using System.Globalization;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Text-independent firing mode exposed to later presentation work. The value is
    /// copied from the latest coordinator decision and never inferred by the HUD.
    /// </summary>
    public enum FourMountFireMode
    {
        NoRecentAttempt = 1,
        Normal = 2,
        Empowered = 3,
        NormalFallbackPowerUnavailable = 4,
        NotReady = 5,
        Faulted = 6,
    }

    /// <summary>
    /// Detached immutable read model for one stable weapon-mount slot.
    /// </summary>
    public sealed class FourMountSlotStatusSnapshot
    {
        public FourMountSlotStatusSnapshot(
            int stableSlotNumber,
            bool isEquipped,
            StableId weaponId,
            WeaponMountPhase? phase,
            bool isReady,
            double cadenceRemainingSeconds,
            int burstShotsRemaining,
            double recoveryRemainingSeconds,
            WeaponCycleMode cycleMode,
            double cycleCurrent,
            double cycleMaximum,
            bool hasPowerBank,
            double powerAvailableUnits,
            double powerCapacityUnits,
            bool canAffordEmpoweredFire,
            FourMountFireMode fireMode,
            WeaponMountFaultKind? faultKind,
            string faultDetail)
        {
            if (stableSlotNumber < 1 || stableSlotNumber > FourMountStatusSnapshot.SlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stableSlotNumber),
                    stableSlotNumber,
                    "Stable slot numbers must be one through four.");
            }

            RequireFiniteNonNegative(cadenceRemainingSeconds, nameof(cadenceRemainingSeconds));
            RequireFiniteNonNegative(recoveryRemainingSeconds, nameof(recoveryRemainingSeconds));
            RequireFiniteNonNegative(cycleCurrent, nameof(cycleCurrent));
            RequireFiniteNonNegative(cycleMaximum, nameof(cycleMaximum));
            RequireFiniteNonNegative(powerAvailableUnits, nameof(powerAvailableUnits));
            RequireFiniteNonNegative(powerCapacityUnits, nameof(powerCapacityUnits));

            if (burstShotsRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(burstShotsRemaining),
                    burstShotsRemaining,
                    "Burst shots remaining cannot be negative.");
            }

            if (!Enum.IsDefined(typeof(WeaponCycleMode), cycleMode))
            {
                throw new ArgumentOutOfRangeException(nameof(cycleMode), cycleMode, "Unknown cycle mode.");
            }

            if (!Enum.IsDefined(typeof(FourMountFireMode), fireMode))
            {
                throw new ArgumentOutOfRangeException(nameof(fireMode), fireMode, "Unknown fire mode.");
            }

            ValidateResource(cycleMode, cycleCurrent, cycleMaximum);
            ValidatePower(
                hasPowerBank,
                powerAvailableUnits,
                powerCapacityUnits,
                canAffordEmpoweredFire);
            ValidateFault(phase, fireMode, faultKind, faultDetail);

            if (!isEquipped)
            {
                ValidateUnequipped(
                    weaponId,
                    phase,
                    isReady,
                    cadenceRemainingSeconds,
                    burstShotsRemaining,
                    recoveryRemainingSeconds,
                    cycleMode,
                    cycleCurrent,
                    cycleMaximum,
                    hasPowerBank,
                    powerAvailableUnits,
                    powerCapacityUnits,
                    canAffordEmpoweredFire,
                    fireMode,
                    faultKind,
                    faultDetail);
            }
            else
            {
                if (weaponId == null)
                {
                    throw new ArgumentNullException(
                        nameof(weaponId),
                        "An equipped status slot requires a stable weapon identity.");
                }

                if (!phase.HasValue)
                {
                    throw new ArgumentNullException(
                        nameof(phase),
                        "An equipped status slot requires an operational phase.");
                }

                if (!Enum.IsDefined(typeof(WeaponMountPhase), phase.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown weapon-mount phase.");
                }

                if (isReady != (phase.Value == WeaponMountPhase.Ready))
                {
                    throw new ArgumentException(
                        "Readiness must agree with the authoritative mount phase.",
                        nameof(isReady));
                }
            }

            StableSlotNumber = stableSlotNumber;
            IsEquipped = isEquipped;
            WeaponId = weaponId;
            Phase = phase;
            IsReady = isReady;
            CadenceRemainingSeconds = cadenceRemainingSeconds;
            BurstShotsRemaining = burstShotsRemaining;
            RecoveryRemainingSeconds = recoveryRemainingSeconds;
            CycleMode = cycleMode;
            CycleCurrent = cycleCurrent;
            CycleMaximum = cycleMaximum;
            HasPowerBank = hasPowerBank;
            PowerAvailableUnits = powerAvailableUnits;
            PowerCapacityUnits = powerCapacityUnits;
            CanAffordEmpoweredFire = canAffordEmpoweredFire;
            FireMode = fireMode;
            FaultKind = faultKind;
            FaultDetail = faultDetail;
        }

        public int StableSlotNumber { get; }

        public int StableIndex => StableSlotNumber - 1;

        public bool IsEquipped { get; }

        public StableId WeaponId { get; }

        public WeaponMountPhase? Phase { get; }

        public bool IsReady { get; }

        public double CadenceRemainingSeconds { get; }

        public int BurstShotsRemaining { get; }

        public double RecoveryRemainingSeconds { get; }

        public WeaponCycleMode CycleMode { get; }

        public double CycleCurrent { get; }

        public double CycleMaximum { get; }

        public double CycleLevel => CycleMode == WeaponCycleMode.None ? 0d : CycleCurrent / CycleMaximum;

        public bool HasPowerBank { get; }

        public double PowerAvailableUnits { get; }

        public double PowerCapacityUnits { get; }

        public double PowerLevel => HasPowerBank ? PowerAvailableUnits / PowerCapacityUnits : 0d;

        public bool CanAffordEmpoweredFire { get; }

        public FourMountFireMode FireMode { get; }

        public bool IsFallback => FireMode == FourMountFireMode.NormalFallbackPowerUnavailable;

        public WeaponMountFaultKind? FaultKind { get; }

        public string FaultDetail { get; }

        public bool IsFaulted => FaultKind.HasValue;

        public static FourMountSlotStatusSnapshot Unequipped(int stableSlotNumber)
        {
            return new FourMountSlotStatusSnapshot(
                stableSlotNumber,
                false,
                null,
                null,
                false,
                0d,
                0,
                0d,
                WeaponCycleMode.None,
                0d,
                0d,
                false,
                0d,
                0d,
                false,
                FourMountFireMode.NoRecentAttempt,
                null,
                null);
        }

        public string ToTraceString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "S{0}[equipped={1};weapon={2};ready={3};phase={4};cadence={5:R};burst={6};recovery={7:R};resource={8}:{9:R}/{10:R};power={11:R}/{12:R};can_empower={13};mode={14};fallback={15};fault={16}]",
                StableSlotNumber,
                IsEquipped ? "true" : "false",
                WeaponId == null ? "none" : WeaponId.ToString(),
                IsReady ? "true" : "false",
                Phase.HasValue ? Phase.Value.ToString() : "Unequipped",
                CadenceRemainingSeconds,
                BurstShotsRemaining,
                RecoveryRemainingSeconds,
                CycleMode,
                CycleCurrent,
                CycleMaximum,
                PowerAvailableUnits,
                PowerCapacityUnits,
                CanAffordEmpoweredFire ? "true" : "false",
                FireMode,
                IsFallback ? "true" : "false",
                FaultKind.HasValue ? FaultKind.Value + ":" + FaultDetail : "none");
        }

        public override string ToString()
        {
            return ToTraceString();
        }

        private static void ValidateResource(
            WeaponCycleMode cycleMode,
            double cycleCurrent,
            double cycleMaximum)
        {
            if (cycleMode == WeaponCycleMode.None)
            {
                if (cycleCurrent != 0d || cycleMaximum != 0d)
                {
                    throw new ArgumentException(
                        "A slot without heat or charge must expose zero cycle values.");
                }

                return;
            }

            if (cycleMaximum <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cycleMaximum),
                    cycleMaximum,
                    "Heat and charge require a positive maximum.");
            }

            if (cycleCurrent > cycleMaximum)
            {
                throw new ArgumentException(
                    "Cycle current cannot exceed its maximum.",
                    nameof(cycleCurrent));
            }
        }

        private static void ValidatePower(
            bool hasPowerBank,
            double powerAvailableUnits,
            double powerCapacityUnits,
            bool canAffordEmpoweredFire)
        {
            if (!hasPowerBank)
            {
                if (powerAvailableUnits != 0d
                    || powerCapacityUnits != 0d
                    || canAffordEmpoweredFire)
                {
                    throw new ArgumentException(
                        "A slot without a power bank must expose neutral power state.");
                }

                return;
            }

            if (powerCapacityUnits <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(powerCapacityUnits),
                    powerCapacityUnits,
                    "A configured power bank requires positive capacity.");
            }

            if (powerAvailableUnits > powerCapacityUnits)
            {
                throw new ArgumentException(
                    "Available power cannot exceed capacity.",
                    nameof(powerAvailableUnits));
            }
        }

        private static void ValidateFault(
            WeaponMountPhase? phase,
            FourMountFireMode fireMode,
            WeaponMountFaultKind? faultKind,
            string faultDetail)
        {
            if (faultKind.HasValue)
            {
                if (!Enum.IsDefined(typeof(WeaponMountFaultKind), faultKind.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(faultKind), faultKind, "Unknown fault kind.");
                }

                if (string.IsNullOrWhiteSpace(faultDetail))
                {
                    throw new ArgumentException(
                        "A faulted status slot requires actionable detail.",
                        nameof(faultDetail));
                }

                if (!phase.HasValue || phase.Value != WeaponMountPhase.Faulted)
                {
                    throw new ArgumentException(
                        "Fault data requires the authoritative Faulted phase.",
                        nameof(phase));
                }

                if (fireMode != FourMountFireMode.Faulted)
                {
                    throw new ArgumentException(
                        "A faulted status slot must expose the Faulted mode.",
                        nameof(fireMode));
                }
            }
            else
            {
                if (faultDetail != null)
                {
                    throw new ArgumentException(
                        "Fault detail cannot be supplied without a fault kind.",
                        nameof(faultDetail));
                }

                if (phase.HasValue && phase.Value == WeaponMountPhase.Faulted)
                {
                    throw new ArgumentException(
                        "The Faulted phase requires fault data.",
                        nameof(phase));
                }

                if (fireMode == FourMountFireMode.Faulted)
                {
                    throw new ArgumentException(
                        "The Faulted mode requires fault data.",
                        nameof(fireMode));
                }
            }
        }

        private static void ValidateUnequipped(
            StableId weaponId,
            WeaponMountPhase? phase,
            bool isReady,
            double cadenceRemainingSeconds,
            int burstShotsRemaining,
            double recoveryRemainingSeconds,
            WeaponCycleMode cycleMode,
            double cycleCurrent,
            double cycleMaximum,
            bool hasPowerBank,
            double powerAvailableUnits,
            double powerCapacityUnits,
            bool canAffordEmpoweredFire,
            FourMountFireMode fireMode,
            WeaponMountFaultKind? faultKind,
            string faultDetail)
        {
            if (weaponId != null
                || phase.HasValue
                || isReady
                || cadenceRemainingSeconds != 0d
                || burstShotsRemaining != 0
                || recoveryRemainingSeconds != 0d
                || cycleMode != WeaponCycleMode.None
                || cycleCurrent != 0d
                || cycleMaximum != 0d
                || hasPowerBank
                || powerAvailableUnits != 0d
                || powerCapacityUnits != 0d
                || canAffordEmpoweredFire
                || fireMode != FourMountFireMode.NoRecentAttempt
                || faultKind.HasValue
                || faultDetail != null)
            {
                throw new ArgumentException(
                    "An unequipped slot must remain present with fully neutral status.");
            }
        }

        private static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Status values must be finite and non-negative.");
            }
        }
    }

    /// <summary>
    /// Canonically ordered immutable four-slot combat read model.
    /// </summary>
    public sealed class FourMountStatusSnapshot
    {
        public const int SlotCount = WeaponRuntimeProfile.SupportedMountCount;

        private readonly FourMountSlotStatusSnapshot[] slots;

        public FourMountStatusSnapshot(params FourMountSlotStatusSnapshot[] slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            if (slots.Length != SlotCount)
            {
                throw new ArgumentException("Exactly four status slots are required.", nameof(slots));
            }

            this.slots = new FourMountSlotStatusSnapshot[SlotCount];
            for (int index = 0; index < slots.Length; index++)
            {
                FourMountSlotStatusSnapshot slot = slots[index];
                if (slot == null)
                {
                    throw new ArgumentException("Status slots cannot contain null.", nameof(slots));
                }

                int stableIndex = slot.StableIndex;
                if (this.slots[stableIndex] != null)
                {
                    throw new ArgumentException(
                        "Each stable status slot must appear exactly once.",
                        nameof(slots));
                }

                this.slots[stableIndex] = slot;
            }

            for (int index = 0; index < this.slots.Length; index++)
            {
                if (this.slots[index] == null)
                {
                    throw new ArgumentException(
                        "Every stable status slot from one through four is required.",
                        nameof(slots));
                }
            }
        }

        public int Count => SlotCount;

        public FourMountSlotStatusSnapshot GetByStableIndex(int stableIndex)
        {
            if (stableIndex < 0 || stableIndex >= slots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(stableIndex));
            }

            return slots[stableIndex];
        }

        public FourMountSlotStatusSnapshot GetByStableSlotNumber(int stableSlotNumber)
        {
            if (stableSlotNumber < 1 || stableSlotNumber > SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stableSlotNumber));
            }

            return slots[stableSlotNumber - 1];
        }

        public string ToTraceString()
        {
            string[] rows = new string[SlotCount];
            for (int index = 0; index < rows.Length; index++)
            {
                rows[index] = slots[index].ToTraceString();
            }

            return string.Join("\n", rows);
        }

        public override string ToString()
        {
            return ToTraceString();
        }
    }
}

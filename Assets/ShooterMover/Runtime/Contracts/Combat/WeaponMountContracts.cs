using System;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Combat
{
    public enum WeaponMountSlot
    {
        MountOne = 1,
        MountTwo = 2,
        MountThree = 3,
        MountFour = 4,
    }

    public enum WeaponMountReadiness
    {
        Unequipped = 1,
        Ready = 2,
        CadenceBlocked = 3,
        Recovering = 4,
        Overheated = 5,
        Charging = 6,
        Faulted = 7,
    }

    public enum WeaponCycleResourceKind
    {
        None = 1,
        Heat = 2,
        Charge = 3,
    }

    public enum WeaponMountFireResultKind
    {
        NormalFired = 1,
        EmpoweredFired = 2,
        NormalFallbackPowerUnavailable = 3,
        NotReady = 4,
        Unequipped = 5,
        Faulted = 6,
    }

    /// <summary>
    /// Stable rules shared by weapon and presentation consumers.
    /// </summary>
    public static class WeaponMountContractRules
    {
        public const int MountCount = 4;

        /// <summary>
        /// Normal fire never consumes ammunition or another finite consumable.
        /// </summary>
        public const bool NormalFireConsumesConsumable = false;

        public static WeaponMountSlot GetSlotAtHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= MountCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return (WeaponMountSlot)(hudIndex + 1);
        }

        public static int GetHudIndex(WeaponMountSlot slot)
        {
            RequireDefined(typeof(WeaponMountSlot), slot, nameof(slot));
            return (int)slot - 1;
        }

        internal static void RequireDefined(Type enumType, object value, string parameterName)
        {
            if (!Enum.IsDefined(enumType, value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Unknown contract enum value.");
            }
        }

        internal static void RequireFiniteNonNegative(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and non-negative.");
            }
        }

        internal static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and positive.");
            }
        }
    }

    public sealed class WeaponCadenceState
    {
        public WeaponCadenceState(double secondsUntilNextShot, int burstShotsRemaining)
        {
            WeaponMountContractRules.RequireFiniteNonNegative(
                secondsUntilNextShot,
                nameof(secondsUntilNextShot));

            if (burstShotsRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(burstShotsRemaining),
                    burstShotsRemaining,
                    "Burst shots remaining cannot be negative.");
            }

            SecondsUntilNextShot = secondsUntilNextShot;
            BurstShotsRemaining = burstShotsRemaining;
        }

        public double SecondsUntilNextShot { get; }

        public int BurstShotsRemaining { get; }

        public bool IsReady => SecondsUntilNextShot == 0d;

        public static WeaponCadenceState Ready
        {
            get { return new WeaponCadenceState(0d, 0); }
        }
    }

    /// <summary>
    /// One mount may expose heat, charge, or no cycle resource. V1 never combines
    /// heat and charge into one mount snapshot.
    /// </summary>
    public sealed class WeaponCycleResourceState
    {
        public WeaponCycleResourceState(
            WeaponCycleResourceKind kind,
            double current,
            double maximum)
        {
            WeaponMountContractRules.RequireDefined(
                typeof(WeaponCycleResourceKind),
                kind,
                nameof(kind));
            WeaponMountContractRules.RequireFiniteNonNegative(current, nameof(current));
            WeaponMountContractRules.RequireFiniteNonNegative(maximum, nameof(maximum));

            if (kind == WeaponCycleResourceKind.None)
            {
                if (current != 0d || maximum != 0d)
                {
                    throw new ArgumentException(
                        "A mount without a cycle resource must use zero current and maximum values.");
                }
            }
            else
            {
                if (maximum == 0d)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(maximum),
                        maximum,
                        "Heat and charge resources require a positive maximum.");
                }

                if (current > maximum)
                {
                    throw new ArgumentException(
                        "Cycle resource current cannot exceed its maximum.",
                        nameof(current));
                }
            }

            Kind = kind;
            Current = current;
            Maximum = maximum;
        }

        public WeaponCycleResourceKind Kind { get; }

        public double Current { get; }

        public double Maximum { get; }

        public bool IsAtMaximum => Kind != WeaponCycleResourceKind.None && Current == Maximum;

        public double Normalized
        {
            get { return Kind == WeaponCycleResourceKind.None ? 0d : Current / Maximum; }
        }

        public static WeaponCycleResourceState None
        {
            get { return new WeaponCycleResourceState(WeaponCycleResourceKind.None, 0d, 0d); }
        }
    }

    public sealed class WeaponRecoilState
    {
        public WeaponRecoilState(double currentImpulse, double movementInfluence)
        {
            WeaponMountContractRules.RequireFiniteNonNegative(currentImpulse, nameof(currentImpulse));
            WeaponMountContractRules.RequireFiniteNonNegative(movementInfluence, nameof(movementInfluence));

            CurrentImpulse = currentImpulse;
            MovementInfluence = movementInfluence;
        }

        public double CurrentImpulse { get; }

        public double MovementInfluence { get; }

        public static WeaponRecoilState None
        {
            get { return new WeaponRecoilState(0d, 0d); }
        }
    }

    /// <summary>
    /// Independent empowered-fire resource for one mount. It is deliberately
    /// separate from unlimited normal fire.
    /// </summary>
    public sealed class WeaponPowerBankState
    {
        public WeaponPowerBankState(
            bool isConfigured,
            double availableUnits,
            double capacityUnits,
            double empoweredCostUnits)
        {
            WeaponMountContractRules.RequireFiniteNonNegative(
                availableUnits,
                nameof(availableUnits));
            WeaponMountContractRules.RequireFiniteNonNegative(
                capacityUnits,
                nameof(capacityUnits));
            WeaponMountContractRules.RequireFiniteNonNegative(
                empoweredCostUnits,
                nameof(empoweredCostUnits));

            if (!isConfigured)
            {
                if (availableUnits != 0d || capacityUnits != 0d || empoweredCostUnits != 0d)
                {
                    throw new ArgumentException(
                        "An unconfigured power bank must use zero available, capacity, and cost values.");
                }
            }
            else
            {
                if (capacityUnits == 0d)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(capacityUnits),
                        capacityUnits,
                        "A configured power bank requires positive capacity.");
                }

                if (availableUnits > capacityUnits)
                {
                    throw new ArgumentException(
                        "Available power cannot exceed capacity.",
                        nameof(availableUnits));
                }
            }

            IsConfigured = isConfigured;
            AvailableUnits = availableUnits;
            CapacityUnits = capacityUnits;
            EmpoweredCostUnits = empoweredCostUnits;
        }

        public bool IsConfigured { get; }

        public double AvailableUnits { get; }

        public double CapacityUnits { get; }

        public double EmpoweredCostUnits { get; }

        public bool CanEmpower => IsConfigured && AvailableUnits >= EmpoweredCostUnits;

        public static WeaponPowerBankState None
        {
            get { return new WeaponPowerBankState(false, 0d, 0d, 0d); }
        }
    }

    /// <summary>
    /// Immutable state for one stable mount slot. Weapon identity may repeat across
    /// slots because identical base copies are valid; slot identity may not repeat.
    /// </summary>
    public sealed class WeaponMountState
    {
        public WeaponMountState(
            WeaponMountSlot slot,
            StableId weaponId,
            WeaponMountReadiness readiness,
            WeaponCadenceState cadence,
            WeaponCycleResourceState cycleResource,
            WeaponRecoilState recoil,
            WeaponPowerBankState powerBank)
        {
            WeaponMountContractRules.RequireDefined(typeof(WeaponMountSlot), slot, nameof(slot));
            WeaponMountContractRules.RequireDefined(
                typeof(WeaponMountReadiness),
                readiness,
                nameof(readiness));

            if (cadence == null)
            {
                throw new ArgumentNullException(nameof(cadence));
            }

            if (cycleResource == null)
            {
                throw new ArgumentNullException(nameof(cycleResource));
            }

            if (recoil == null)
            {
                throw new ArgumentNullException(nameof(recoil));
            }

            if (powerBank == null)
            {
                throw new ArgumentNullException(nameof(powerBank));
            }

            if (readiness == WeaponMountReadiness.Unequipped)
            {
                ValidateUnequipped(weaponId, cadence, cycleResource, recoil, powerBank);
            }
            else if (weaponId == null)
            {
                throw new ArgumentNullException(
                    nameof(weaponId),
                    "An equipped mount requires a StableId.");
            }

            if (readiness == WeaponMountReadiness.Ready)
            {
                if (!cadence.IsReady)
                {
                    throw new ArgumentException(
                        "A ready mount cannot have cadence time remaining.",
                        nameof(cadence));
                }

                if (cycleResource.Kind == WeaponCycleResourceKind.Heat
                    && cycleResource.IsAtMaximum)
                {
                    throw new ArgumentException(
                        "A ready mount cannot be at maximum heat.",
                        nameof(cycleResource));
                }
            }

            if (readiness == WeaponMountReadiness.CadenceBlocked && cadence.IsReady)
            {
                throw new ArgumentException(
                    "CadenceBlocked requires positive time until the next shot.",
                    nameof(cadence));
            }

            if (readiness == WeaponMountReadiness.Overheated
                && (cycleResource.Kind != WeaponCycleResourceKind.Heat
                    || !cycleResource.IsAtMaximum))
            {
                throw new ArgumentException(
                    "Overheated requires a heat resource at its maximum.",
                    nameof(cycleResource));
            }

            if (readiness == WeaponMountReadiness.Charging
                && (cycleResource.Kind != WeaponCycleResourceKind.Charge
                    || cycleResource.IsAtMaximum))
            {
                throw new ArgumentException(
                    "Charging requires a charge resource below its maximum.",
                    nameof(cycleResource));
            }

            Slot = slot;
            WeaponId = weaponId;
            Readiness = readiness;
            Cadence = cadence;
            CycleResource = cycleResource;
            Recoil = recoil;
            PowerBank = powerBank;
        }

        public WeaponMountSlot Slot { get; }

        public StableId WeaponId { get; }

        public WeaponMountReadiness Readiness { get; }

        public WeaponCadenceState Cadence { get; }

        public WeaponCycleResourceState CycleResource { get; }

        public WeaponRecoilState Recoil { get; }

        public WeaponPowerBankState PowerBank { get; }

        public bool IsEquipped => Readiness != WeaponMountReadiness.Unequipped;

        public static WeaponMountState Unequipped(WeaponMountSlot slot)
        {
            return new WeaponMountState(
                slot,
                null,
                WeaponMountReadiness.Unequipped,
                WeaponCadenceState.Ready,
                WeaponCycleResourceState.None,
                WeaponRecoilState.None,
                WeaponPowerBankState.None);
        }

        private static void ValidateUnequipped(
            StableId weaponId,
            WeaponCadenceState cadence,
            WeaponCycleResourceState cycleResource,
            WeaponRecoilState recoil,
            WeaponPowerBankState powerBank)
        {
            if (weaponId != null)
            {
                throw new ArgumentException(
                    "An unequipped mount cannot carry a weapon identity.",
                    nameof(weaponId));
            }

            if (!cadence.IsReady || cadence.BurstShotsRemaining != 0)
            {
                throw new ArgumentException(
                    "An unequipped mount must use neutral cadence state.",
                    nameof(cadence));
            }

            if (cycleResource.Kind != WeaponCycleResourceKind.None)
            {
                throw new ArgumentException(
                    "An unequipped mount cannot carry heat or charge state.",
                    nameof(cycleResource));
            }

            if (recoil.CurrentImpulse != 0d || recoil.MovementInfluence != 0d)
            {
                throw new ArgumentException(
                    "An unequipped mount must use neutral recoil state.",
                    nameof(recoil));
            }

            if (powerBank.IsConfigured)
            {
                throw new ArgumentException(
                    "An unequipped mount cannot carry a power bank.",
                    nameof(powerBank));
            }
        }
    }

    /// <summary>
    /// The single aim/fire/power intent shared by all four mounts.
    /// </summary>
    public readonly struct WeaponArrayIntent
    {
        public WeaponArrayIntent(
            NormalizedIntentVector2 aim,
            ButtonIntent fire,
            ButtonIntent powerModifier)
        {
            Aim = aim;
            Fire = fire;
            PowerModifier = powerModifier;
        }

        public NormalizedIntentVector2 Aim { get; }

        public ButtonIntent Fire { get; }

        public ButtonIntent PowerModifier { get; }

        public bool IsFireRequested => Fire.IsHeld || Fire.WasPressed;

        public bool IsPowerRequested => PowerModifier.IsHeld || PowerModifier.WasPressed;

        public static WeaponArrayIntent FromPlayerIntent(PlayerIntentFrame frame)
        {
            return new WeaponArrayIntent(frame.Aim, frame.Fire, frame.PowerModifier);
        }
    }

    public sealed class FourMountWeaponState
    {
        private readonly WeaponMountState[] mounts;

        public FourMountWeaponState(params WeaponMountState[] mounts)
        {
            this.mounts = CanonicalizeMounts(mounts, nameof(mounts));
        }

        public int Count => WeaponMountContractRules.MountCount;

        public WeaponMountState GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= mounts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return mounts[hudIndex];
        }

        public WeaponMountState GetBySlot(WeaponMountSlot slot)
        {
            return mounts[WeaponMountContractRules.GetHudIndex(slot)];
        }

        private static WeaponMountState[] CanonicalizeMounts(
            WeaponMountState[] source,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (source.Length != WeaponMountContractRules.MountCount)
            {
                throw new ArgumentException(
                    "Exactly four mount states are required.",
                    parameterName);
            }

            WeaponMountState[] canonical = new WeaponMountState[WeaponMountContractRules.MountCount];
            for (int index = 0; index < source.Length; index++)
            {
                WeaponMountState mount = source[index];
                if (mount == null)
                {
                    throw new ArgumentException("Mount states cannot contain null.", parameterName);
                }

                int hudIndex = WeaponMountContractRules.GetHudIndex(mount.Slot);
                if (canonical[hudIndex] != null)
                {
                    throw new ArgumentException(
                        "Each stable weapon mount slot must appear exactly once.",
                        parameterName);
                }

                canonical[hudIndex] = mount;
            }

            return canonical;
        }
    }

    public sealed class WeaponMountFireResult
    {
        public WeaponMountFireResult(
            WeaponMountSlot slot,
            StableId weaponId,
            WeaponMountFireResultKind kind,
            StableId combatEventId,
            CombatChannel? channel)
        {
            WeaponMountContractRules.RequireDefined(typeof(WeaponMountSlot), slot, nameof(slot));
            WeaponMountContractRules.RequireDefined(
                typeof(WeaponMountFireResultKind),
                kind,
                nameof(kind));

            bool fired = kind == WeaponMountFireResultKind.NormalFired
                || kind == WeaponMountFireResultKind.EmpoweredFired
                || kind == WeaponMountFireResultKind.NormalFallbackPowerUnavailable;

            if (kind == WeaponMountFireResultKind.Unequipped)
            {
                if (weaponId != null)
                {
                    throw new ArgumentException(
                        "An unequipped result cannot identify a weapon.",
                        nameof(weaponId));
                }
            }
            else if (weaponId == null)
            {
                throw new ArgumentNullException(nameof(weaponId));
            }

            if (fired)
            {
                if (combatEventId == null)
                {
                    throw new ArgumentNullException(
                        nameof(combatEventId),
                        "A fired result requires a combat event identity.");
                }

                if (!channel.HasValue)
                {
                    throw new ArgumentNullException(
                        nameof(channel),
                        "A fired result requires a combat channel.");
                }

                WeaponMountContractRules.RequireDefined(
                    typeof(CombatChannel),
                    channel.Value,
                    nameof(channel));

                if (channel.Value == CombatChannel.System)
                {
                    throw new ArgumentException(
                        "System is not a weapon-fire combat channel.",
                        nameof(channel));
                }
            }
            else if (combatEventId != null || channel.HasValue)
            {
                throw new ArgumentException(
                    "A non-fired result cannot publish a combat event or channel.");
            }

            Slot = slot;
            WeaponId = weaponId;
            Kind = kind;
            CombatEventId = combatEventId;
            Channel = channel;
        }

        public WeaponMountSlot Slot { get; }

        public StableId WeaponId { get; }

        public WeaponMountFireResultKind Kind { get; }

        public StableId CombatEventId { get; }

        public CombatChannel? Channel { get; }
    }

    /// <summary>
    /// Results for one shared fire attempt. Every slot is represented once and
    /// validated independently, so a fault or empty bank on one mount cannot alter
    /// another mount's result.
    /// </summary>
    public sealed class FourMountFireResult
    {
        private readonly WeaponMountFireResult[] results;

        public FourMountFireResult(
            WeaponArrayIntent intent,
            FourMountWeaponState mounts,
            params WeaponMountFireResult[] results)
        {
            if (!intent.IsFireRequested)
            {
                throw new ArgumentException(
                    "A four-mount fire result requires an active shared fire intent.",
                    nameof(intent));
            }

            if (mounts == null)
            {
                throw new ArgumentNullException(nameof(mounts));
            }

            this.results = CanonicalizeResults(results);
            ValidateResults(intent, mounts, this.results);

            Intent = intent;
            Mounts = mounts;
        }

        public WeaponArrayIntent Intent { get; }

        public FourMountWeaponState Mounts { get; }

        public int Count => WeaponMountContractRules.MountCount;

        public WeaponMountFireResult GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= results.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return results[hudIndex];
        }

        public WeaponMountFireResult GetBySlot(WeaponMountSlot slot)
        {
            return results[WeaponMountContractRules.GetHudIndex(slot)];
        }

        private static WeaponMountFireResult[] CanonicalizeResults(
            WeaponMountFireResult[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Length != WeaponMountContractRules.MountCount)
            {
                throw new ArgumentException("Exactly four mount results are required.", nameof(source));
            }

            WeaponMountFireResult[] canonical =
                new WeaponMountFireResult[WeaponMountContractRules.MountCount];

            for (int index = 0; index < source.Length; index++)
            {
                WeaponMountFireResult result = source[index];
                if (result == null)
                {
                    throw new ArgumentException("Mount results cannot contain null.", nameof(source));
                }

                int hudIndex = WeaponMountContractRules.GetHudIndex(result.Slot);
                if (canonical[hudIndex] != null)
                {
                    throw new ArgumentException(
                        "Each stable weapon mount slot must have exactly one result.",
                        nameof(source));
                }

                canonical[hudIndex] = result;
            }

            return canonical;
        }

        private static void ValidateResults(
            WeaponArrayIntent intent,
            FourMountWeaponState mounts,
            WeaponMountFireResult[] canonicalResults)
        {
            for (int index = 0; index < WeaponMountContractRules.MountCount; index++)
            {
                WeaponMountState mount = mounts.GetByHudIndex(index);
                WeaponMountFireResult result = canonicalResults[index];

                if (mount.WeaponId != result.WeaponId)
                {
                    throw new ArgumentException(
                        "A mount result weapon identity must match its mount snapshot.",
                        nameof(canonicalResults));
                }

                WeaponMountFireResultKind expected = GetExpectedResult(intent, mount);
                if (result.Kind != expected)
                {
                    throw new ArgumentException(
                        "Mount result is inconsistent with shared intent, readiness, or power-bank state.",
                        nameof(canonicalResults));
                }
            }
        }

        private static WeaponMountFireResultKind GetExpectedResult(
            WeaponArrayIntent intent,
            WeaponMountState mount)
        {
            if (mount.Readiness == WeaponMountReadiness.Unequipped)
            {
                return WeaponMountFireResultKind.Unequipped;
            }

            if (mount.Readiness == WeaponMountReadiness.Faulted)
            {
                return WeaponMountFireResultKind.Faulted;
            }

            if (mount.Readiness != WeaponMountReadiness.Ready)
            {
                return WeaponMountFireResultKind.NotReady;
            }

            if (!intent.IsPowerRequested)
            {
                return WeaponMountFireResultKind.NormalFired;
            }

            return mount.PowerBank.CanEmpower
                ? WeaponMountFireResultKind.EmpoweredFired
                : WeaponMountFireResultKind.NormalFallbackPowerUnavailable;
        }
    }
}

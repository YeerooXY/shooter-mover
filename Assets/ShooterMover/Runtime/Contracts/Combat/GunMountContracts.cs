using System;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Combat
{
    public enum GunMountSlot
    {
        MountOne = 1,
        MountTwo = 2,
        MountThree = 3,
        MountFour = 4,
    }

    public enum GunMountReadiness
    {
        Unequipped = 1,
        Ready = 2,
        CadenceBlocked = 3,
        Recovering = 4,
        Overheated = 5,
        Charging = 6,
        Faulted = 7,
    }

    public enum GunCycleResourceKind
    {
        None = 1,
        Heat = 2,
        Charge = 3,
    }

    public enum GunMountFireResultKind
    {
        NormalFired = 1,
        EmpoweredFired = 2,
        NormalFallbackPowerUnavailable = 3,
        NotReady = 4,
        Unequipped = 5,
        Faulted = 6,
    }

    /// <summary>
    /// Stable rules shared by gun and presentation consumers.
    /// </summary>
    public static class GunMountContractRules
    {
        public const int MountCount = 4;

        /// <summary>
        /// Normal fire never consumes ammunition or another finite consumable.
        /// </summary>
        public const bool NormalFireConsumesConsumable = false;

        public static GunMountSlot GetSlotAtHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= MountCount)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return (GunMountSlot)(hudIndex + 1);
        }

        public static int GetHudIndex(GunMountSlot slot)
        {
            RequireDefined(typeof(GunMountSlot), slot, nameof(slot));
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

    public sealed class GunCadenceState
    {
        public GunCadenceState(double secondsUntilNextShot, int burstShotsRemaining)
        {
            GunMountContractRules.RequireFiniteNonNegative(
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

        public static GunCadenceState Ready
        {
            get { return new GunCadenceState(0d, 0); }
        }
    }

    /// <summary>
    /// One mount may expose heat, charge, or no cycle resource. V1 never combines
    /// heat and charge into one mount snapshot.
    /// </summary>
    public sealed class GunCycleResourceState
    {
        public GunCycleResourceState(
            GunCycleResourceKind kind,
            double current,
            double maximum)
        {
            GunMountContractRules.RequireDefined(
                typeof(GunCycleResourceKind),
                kind,
                nameof(kind));
            GunMountContractRules.RequireFiniteNonNegative(current, nameof(current));
            GunMountContractRules.RequireFiniteNonNegative(maximum, nameof(maximum));

            if (kind == GunCycleResourceKind.None)
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

        public GunCycleResourceKind Kind { get; }

        public double Current { get; }

        public double Maximum { get; }

        public bool IsAtMaximum => Kind != GunCycleResourceKind.None && Current == Maximum;

        public double Normalized
        {
            get { return Kind == GunCycleResourceKind.None ? 0d : Current / Maximum; }
        }

        public static GunCycleResourceState None
        {
            get { return new GunCycleResourceState(GunCycleResourceKind.None, 0d, 0d); }
        }
    }

    public sealed class GunRecoilState
    {
        public GunRecoilState(double currentImpulse, double movementInfluence)
        {
            GunMountContractRules.RequireFiniteNonNegative(currentImpulse, nameof(currentImpulse));
            GunMountContractRules.RequireFiniteNonNegative(movementInfluence, nameof(movementInfluence));

            CurrentImpulse = currentImpulse;
            MovementInfluence = movementInfluence;
        }

        public double CurrentImpulse { get; }

        public double MovementInfluence { get; }

        public static GunRecoilState None
        {
            get { return new GunRecoilState(0d, 0d); }
        }
    }

    /// <summary>
    /// Independent empowered-fire resource for one mount. It is deliberately
    /// separate from unlimited normal fire.
    /// </summary>
    public sealed class GunPowerBankState
    {
        public GunPowerBankState(
            bool isConfigured,
            double availableUnits,
            double capacityUnits,
            double empoweredCostUnits)
        {
            GunMountContractRules.RequireFiniteNonNegative(
                availableUnits,
                nameof(availableUnits));
            GunMountContractRules.RequireFiniteNonNegative(
                capacityUnits,
                nameof(capacityUnits));
            GunMountContractRules.RequireFiniteNonNegative(
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

        public static GunPowerBankState None
        {
            get { return new GunPowerBankState(false, 0d, 0d, 0d); }
        }
    }

    /// <summary>
    /// Immutable state for one stable mount slot. Gun identity may repeat across
    /// slots because identical base copies are valid; slot identity may not repeat.
    /// </summary>
    public sealed class GunMountState
    {
        public GunMountState(
            GunMountSlot slot,
            StableId gunId,
            GunMountReadiness readiness,
            GunCadenceState cadence,
            GunCycleResourceState cycleResource,
            GunRecoilState recoil,
            GunPowerBankState powerBank)
        {
            GunMountContractRules.RequireDefined(typeof(GunMountSlot), slot, nameof(slot));
            GunMountContractRules.RequireDefined(
                typeof(GunMountReadiness),
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

            if (readiness == GunMountReadiness.Unequipped)
            {
                ValidateUnequipped(gunId, cadence, cycleResource, recoil, powerBank);
            }
            else if (gunId == null)
            {
                throw new ArgumentNullException(
                    nameof(gunId),
                    "An equipped mount requires a StableId.");
            }

            if (readiness == GunMountReadiness.Ready)
            {
                if (!cadence.IsReady)
                {
                    throw new ArgumentException(
                        "A ready mount cannot have cadence time remaining.",
                        nameof(cadence));
                }

                if (cycleResource.Kind == GunCycleResourceKind.Heat
                    && cycleResource.IsAtMaximum)
                {
                    throw new ArgumentException(
                        "A ready mount cannot be at maximum heat.",
                        nameof(cycleResource));
                }
            }

            if (readiness == GunMountReadiness.CadenceBlocked && cadence.IsReady)
            {
                throw new ArgumentException(
                    "CadenceBlocked requires positive time until the next shot.",
                    nameof(cadence));
            }

            if (readiness == GunMountReadiness.Overheated
                && (cycleResource.Kind != GunCycleResourceKind.Heat
                    || !cycleResource.IsAtMaximum))
            {
                throw new ArgumentException(
                    "Overheated requires a heat resource at its maximum.",
                    nameof(cycleResource));
            }

            if (readiness == GunMountReadiness.Charging
                && (cycleResource.Kind != GunCycleResourceKind.Charge
                    || cycleResource.IsAtMaximum))
            {
                throw new ArgumentException(
                    "Charging requires a charge resource below its maximum.",
                    nameof(cycleResource));
            }

            Slot = slot;
            GunId = gunId;
            Readiness = readiness;
            Cadence = cadence;
            CycleResource = cycleResource;
            Recoil = recoil;
            PowerBank = powerBank;
        }

        public GunMountSlot Slot { get; }

        public StableId GunId { get; }

        public GunMountReadiness Readiness { get; }

        public GunCadenceState Cadence { get; }

        public GunCycleResourceState CycleResource { get; }

        public GunRecoilState Recoil { get; }

        public GunPowerBankState PowerBank { get; }

        public bool IsEquipped => Readiness != GunMountReadiness.Unequipped;

        public static GunMountState Unequipped(GunMountSlot slot)
        {
            return new GunMountState(
                slot,
                null,
                GunMountReadiness.Unequipped,
                GunCadenceState.Ready,
                GunCycleResourceState.None,
                GunRecoilState.None,
                GunPowerBankState.None);
        }

        private static void ValidateUnequipped(
            StableId gunId,
            GunCadenceState cadence,
            GunCycleResourceState cycleResource,
            GunRecoilState recoil,
            GunPowerBankState powerBank)
        {
            if (gunId != null)
            {
                throw new ArgumentException(
                    "An unequipped mount cannot carry a gun identity.",
                    nameof(gunId));
            }

            if (!cadence.IsReady || cadence.BurstShotsRemaining != 0)
            {
                throw new ArgumentException(
                    "An unequipped mount must use neutral cadence state.",
                    nameof(cadence));
            }

            if (cycleResource.Kind != GunCycleResourceKind.None)
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
    public readonly struct GunArrayIntent
    {
        public GunArrayIntent(
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

        public static GunArrayIntent FromPlayerIntent(PlayerIntentFrame frame)
        {
            return new GunArrayIntent(frame.Aim, frame.Fire, frame.PowerModifier);
        }
    }

    public sealed class FourMountGunState
    {
        private readonly GunMountState[] mounts;

        public FourMountGunState(params GunMountState[] mounts)
        {
            this.mounts = CanonicalizeMounts(mounts, nameof(mounts));
        }

        public int Count => GunMountContractRules.MountCount;

        public GunMountState GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= mounts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return mounts[hudIndex];
        }

        public GunMountState GetBySlot(GunMountSlot slot)
        {
            return mounts[GunMountContractRules.GetHudIndex(slot)];
        }

        private static GunMountState[] CanonicalizeMounts(
            GunMountState[] source,
            string parameterName)
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (source.Length != GunMountContractRules.MountCount)
            {
                throw new ArgumentException(
                    "Exactly four mount states are required.",
                    parameterName);
            }

            GunMountState[] canonical = new GunMountState[GunMountContractRules.MountCount];
            for (int index = 0; index < source.Length; index++)
            {
                GunMountState mount = source[index];
                if (mount == null)
                {
                    throw new ArgumentException("Mount states cannot contain null.", parameterName);
                }

                int hudIndex = GunMountContractRules.GetHudIndex(mount.Slot);
                if (canonical[hudIndex] != null)
                {
                    throw new ArgumentException(
                        "Each stable gun mount slot must appear exactly once.",
                        parameterName);
                }

                canonical[hudIndex] = mount;
            }

            return canonical;
        }
    }

    public sealed class GunMountFireResult
    {
        public GunMountFireResult(
            GunMountSlot slot,
            StableId gunId,
            GunMountFireResultKind kind,
            StableId combatEventId,
            CombatChannel? channel)
        {
            GunMountContractRules.RequireDefined(typeof(GunMountSlot), slot, nameof(slot));
            GunMountContractRules.RequireDefined(
                typeof(GunMountFireResultKind),
                kind,
                nameof(kind));

            bool fired = kind == GunMountFireResultKind.NormalFired
                || kind == GunMountFireResultKind.EmpoweredFired
                || kind == GunMountFireResultKind.NormalFallbackPowerUnavailable;

            if (kind == GunMountFireResultKind.Unequipped)
            {
                if (gunId != null)
                {
                    throw new ArgumentException(
                        "An unequipped result cannot identify a gun.",
                        nameof(gunId));
                }
            }
            else if (gunId == null)
            {
                throw new ArgumentNullException(nameof(gunId));
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

                GunMountContractRules.RequireDefined(
                    typeof(CombatChannel),
                    channel.Value,
                    nameof(channel));

                if (channel.Value == CombatChannel.System)
                {
                    throw new ArgumentException(
                        "System is not a gun-fire combat channel.",
                        nameof(channel));
                }
            }
            else if (combatEventId != null || channel.HasValue)
            {
                throw new ArgumentException(
                    "A non-fired result cannot publish a combat event or channel.");
            }

            Slot = slot;
            GunId = gunId;
            Kind = kind;
            CombatEventId = combatEventId;
            Channel = channel;
        }

        public GunMountSlot Slot { get; }

        public StableId GunId { get; }

        public GunMountFireResultKind Kind { get; }

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
        private readonly GunMountFireResult[] results;

        public FourMountFireResult(
            GunArrayIntent intent,
            FourMountGunState mounts,
            params GunMountFireResult[] results)
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

        public GunArrayIntent Intent { get; }

        public FourMountGunState Mounts { get; }

        public int Count => GunMountContractRules.MountCount;

        public GunMountFireResult GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= results.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return results[hudIndex];
        }

        public GunMountFireResult GetBySlot(GunMountSlot slot)
        {
            return results[GunMountContractRules.GetHudIndex(slot)];
        }

        private static GunMountFireResult[] CanonicalizeResults(
            GunMountFireResult[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Length != GunMountContractRules.MountCount)
            {
                throw new ArgumentException("Exactly four mount results are required.", nameof(source));
            }

            GunMountFireResult[] canonical =
                new GunMountFireResult[GunMountContractRules.MountCount];

            for (int index = 0; index < source.Length; index++)
            {
                GunMountFireResult result = source[index];
                if (result == null)
                {
                    throw new ArgumentException("Mount results cannot contain null.", nameof(source));
                }

                int hudIndex = GunMountContractRules.GetHudIndex(result.Slot);
                if (canonical[hudIndex] != null)
                {
                    throw new ArgumentException(
                        "Each stable gun mount slot must have exactly one result.",
                        nameof(source));
                }

                canonical[hudIndex] = result;
            }

            return canonical;
        }

        private static void ValidateResults(
            GunArrayIntent intent,
            FourMountGunState mounts,
            GunMountFireResult[] canonicalResults)
        {
            for (int index = 0; index < GunMountContractRules.MountCount; index++)
            {
                GunMountState mount = mounts.GetByHudIndex(index);
                GunMountFireResult result = canonicalResults[index];

                if (mount.GunId != result.GunId)
                {
                    throw new ArgumentException(
                        "A mount result gun identity must match its mount snapshot.",
                        nameof(canonicalResults));
                }

                GunMountFireResultKind expected = GetExpectedResult(intent, mount);
                if (result.Kind != expected)
                {
                    throw new ArgumentException(
                        "Mount result is inconsistent with shared intent, readiness, or power-bank state.",
                        nameof(canonicalResults));
                }
            }
        }

        private static GunMountFireResultKind GetExpectedResult(
            GunArrayIntent intent,
            GunMountState mount)
        {
            if (mount.Readiness == GunMountReadiness.Unequipped)
            {
                return GunMountFireResultKind.Unequipped;
            }

            if (mount.Readiness == GunMountReadiness.Faulted)
            {
                return GunMountFireResultKind.Faulted;
            }

            if (mount.Readiness != GunMountReadiness.Ready)
            {
                return GunMountFireResultKind.NotReady;
            }

            if (!intent.IsPowerRequested)
            {
                return GunMountFireResultKind.NormalFired;
            }

            return mount.PowerBank.CanEmpower
                ? GunMountFireResultKind.EmpoweredFired
                : GunMountFireResultKind.NormalFallbackPowerUnavailable;
        }
    }
}

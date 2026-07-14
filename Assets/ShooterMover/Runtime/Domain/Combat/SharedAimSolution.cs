using System;
using System.Globalization;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Immutable engine-independent planar value used for authored mount origins,
    /// shared aim points, and resolved firing directions.
    /// </summary>
    public readonly struct AimVector2 : IEquatable<AimVector2>
    {
        public AimVector2(double x, double y)
        {
            if (!IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Aim components must be finite.");
            }

            if (!IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), "Aim components must be finite.");
            }

            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public static AimVector2 Zero
        {
            get { return new AimVector2(0d, 0d); }
        }

        public static AimVector2 UnitX
        {
            get { return new AimVector2(1d, 0d); }
        }

        public bool Equals(AimVector2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is AimVector2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return "("
                + X.ToString("R", CultureInfo.InvariantCulture)
                + ", "
                + Y.ToString("R", CultureInfo.InvariantCulture)
                + ")";
        }

        public static bool operator ==(AimVector2 left, AimVector2 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AimVector2 left, AimVector2 right)
        {
            return !left.Equals(right);
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// One immutable mount origin identified by the stable v1 slot number. Slot
    /// numbers intentionally match the accepted MountOne-through-MountFour order
    /// without introducing a Domain-to-Contracts assembly dependency.
    /// </summary>
    public readonly struct WeaponMountOrigin : IEquatable<WeaponMountOrigin>
    {
        public WeaponMountOrigin(int stableSlotNumber, AimVector2 origin)
        {
            if (stableSlotNumber < 1 || stableSlotNumber > FourMountAimSolution.MountCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stableSlotNumber),
                    stableSlotNumber,
                    "Stable mount slot must be in the inclusive range 1 through 4.");
            }

            StableSlotNumber = stableSlotNumber;
            Origin = origin;
        }

        public int StableSlotNumber { get; }

        public AimVector2 Origin { get; }

        public bool Equals(WeaponMountOrigin other)
        {
            return StableSlotNumber == other.StableSlotNumber && Origin.Equals(other.Origin);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponMountOrigin other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StableSlotNumber * 397) ^ Origin.GetHashCode();
            }
        }

        public static bool operator ==(WeaponMountOrigin left, WeaponMountOrigin right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeaponMountOrigin left, WeaponMountOrigin right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Immutable firing solution for one mount. The requested shared aim point is
    /// preserved even when unsafe close geometry requires a bounded fallback ray.
    /// </summary>
    public sealed class SharedAimSolution
    {
        internal SharedAimSolution(
            int stableSlotNumber,
            AimVector2 origin,
            AimVector2 sharedAimPoint,
            AimVector2 direction,
            bool usedFallbackDirection)
        {
            if (stableSlotNumber < 1 || stableSlotNumber > FourMountAimSolution.MountCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stableSlotNumber));
            }

            if (direction.X == 0d && direction.Y == 0d)
            {
                throw new ArgumentException("Resolved firing direction cannot be zero.", nameof(direction));
            }

            StableSlotNumber = stableSlotNumber;
            Origin = origin;
            SharedAimPoint = sharedAimPoint;
            Direction = direction;
            UsedFallbackDirection = usedFallbackDirection;
        }

        public int StableSlotNumber { get; }

        public AimVector2 Origin { get; }

        public AimVector2 SharedAimPoint { get; }

        public AimVector2 Direction { get; }

        public bool UsedFallbackDirection { get; }
    }

    /// <summary>
    /// Exactly four per-mount solutions stored in accepted stable slot order.
    /// </summary>
    public sealed class FourMountAimSolution
    {
        public const int MountCount = WeaponRuntimeProfile.SupportedMountCount;

        private readonly SharedAimSolution[] solutions;

        internal FourMountAimSolution(SharedAimSolution[] canonicalSolutions)
        {
            if (canonicalSolutions == null)
            {
                throw new ArgumentNullException(nameof(canonicalSolutions));
            }

            if (canonicalSolutions.Length != MountCount)
            {
                throw new ArgumentException(
                    "Exactly four shared aim solutions are required.",
                    nameof(canonicalSolutions));
            }

            solutions = new SharedAimSolution[MountCount];
            for (int index = 0; index < MountCount; index++)
            {
                SharedAimSolution solution = canonicalSolutions[index];
                if (solution == null || solution.StableSlotNumber != index + 1)
                {
                    throw new ArgumentException(
                        "Shared aim solutions must appear once in stable slot order.",
                        nameof(canonicalSolutions));
                }

                solutions[index] = solution;
            }
        }

        public int Count
        {
            get { return MountCount; }
        }

        public SharedAimSolution GetByStableIndex(int stableIndex)
        {
            if (stableIndex < 0 || stableIndex >= MountCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stableIndex));
            }

            return solutions[stableIndex];
        }

        public SharedAimSolution GetByStableSlotNumber(int stableSlotNumber)
        {
            if (stableSlotNumber < 1 || stableSlotNumber > MountCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stableSlotNumber));
            }

            return solutions[stableSlotNumber - 1];
        }
    }
}

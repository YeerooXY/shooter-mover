using System;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Resolves one engine-independent shared aim into four stable per-mount rays.
    /// Unsafe close, inside, coincident, or behind geometry falls back as one array
    /// so mounts cannot split between crossing and non-crossing solutions.
    /// </summary>
    public sealed class FourMountAimResolver
    {
        public const double DefaultMinimumDirectionLength = 0.000001d;
        public const double DefaultConvergenceClearance = 0.001d;

        private readonly double minimumDirectionLength;
        private readonly double convergenceClearance;
        private readonly double minimumFreeDistance;

        public FourMountAimResolver()
            : this(DefaultMinimumDirectionLength, DefaultConvergenceClearance)
        {
        }

        public FourMountAimResolver(
            double minimumDirectionLength,
            double convergenceClearance)
        {
            if (!AimVector2.IsFinite(minimumDirectionLength) || minimumDirectionLength <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumDirectionLength),
                    minimumDirectionLength,
                    "Minimum direction length must be finite and positive.");
            }

            if (!AimVector2.IsFinite(convergenceClearance) || convergenceClearance < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(convergenceClearance),
                    convergenceClearance,
                    "Convergence clearance must be finite and non-negative.");
            }

            double combinedFreeDistance = minimumDirectionLength + convergenceClearance;
            if (!AimVector2.IsFinite(combinedFreeDistance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(convergenceClearance),
                    convergenceClearance,
                    "Minimum direction length plus convergence clearance must remain finite.");
            }

            this.minimumDirectionLength = minimumDirectionLength;
            this.convergenceClearance = convergenceClearance;
            minimumFreeDistance = combinedFreeDistance;
        }

        public double MinimumDirectionLength
        {
            get { return minimumDirectionLength; }
        }

        public double ConvergenceClearance
        {
            get { return convergenceClearance; }
        }

        public FourMountAimSolution Resolve(
            AimVector2 sharedAimIntent,
            AimVector2 sharedAimPoint,
            params WeaponMountOrigin[] mountOrigins)
        {
            WeaponMountOrigin[] canonicalOrigins = CanonicalizeOrigins(mountOrigins);
            AimVector2 geometryCenter = AverageOrigins(canonicalOrigins);
            AimVector2 fallbackDirection = ResolveFallbackDirection(
                sharedAimIntent,
                sharedAimPoint,
                geometryCenter);

            AimVector2[] convergedDirections = new AimVector2[FourMountAimSolution.MountCount];
            bool canConverge = TryResolveConvergedDirections(
                sharedAimPoint,
                geometryCenter,
                fallbackDirection,
                canonicalOrigins,
                convergedDirections);

            SharedAimSolution[] solutions =
                new SharedAimSolution[FourMountAimSolution.MountCount];
            for (int index = 0; index < FourMountAimSolution.MountCount; index++)
            {
                WeaponMountOrigin mount = canonicalOrigins[index];
                solutions[index] = new SharedAimSolution(
                    mount.StableSlotNumber,
                    mount.Origin,
                    sharedAimPoint,
                    canConverge ? convergedDirections[index] : fallbackDirection,
                    !canConverge);
            }

            return new FourMountAimSolution(solutions);
        }

        private bool TryResolveConvergedDirections(
            AimVector2 sharedAimPoint,
            AimVector2 geometryCenter,
            AimVector2 fallbackDirection,
            WeaponMountOrigin[] canonicalOrigins,
            AimVector2[] convergedDirections)
        {
            AimVector2 centerDirection;
            double aimDistanceFromCenter;
            if (!TryNormalizeDifference(
                sharedAimPoint,
                geometryCenter,
                minimumDirectionLength,
                out centerDirection,
                out aimDistanceFromCenter))
            {
                return false;
            }

            if (Dot(centerDirection, fallbackDirection) <= 0d)
            {
                return false;
            }

            double geometryRadius = 0d;
            for (int index = 0; index < canonicalOrigins.Length; index++)
            {
                AimVector2 ignoredDirection;
                double radius;
                if (TryNormalizeDifference(
                    canonicalOrigins[index].Origin,
                    geometryCenter,
                    0d,
                    out ignoredDirection,
                    out radius)
                    && radius > geometryRadius)
                {
                    geometryRadius = radius;
                }
            }

            if (double.IsInfinity(geometryRadius))
            {
                return false;
            }

            if (!double.IsInfinity(aimDistanceFromCenter))
            {
                if (aimDistanceFromCenter <= geometryRadius)
                {
                    return false;
                }

                double freeDistance = aimDistanceFromCenter - geometryRadius;
                if (freeDistance <= minimumFreeDistance)
                {
                    return false;
                }
            }

            for (int index = 0; index < canonicalOrigins.Length; index++)
            {
                AimVector2 direction;
                double ignoredDistance;
                if (!TryNormalizeDifference(
                    sharedAimPoint,
                    canonicalOrigins[index].Origin,
                    minimumDirectionLength,
                    out direction,
                    out ignoredDistance))
                {
                    return false;
                }

                if (Dot(direction, fallbackDirection) <= 0d)
                {
                    return false;
                }

                convergedDirections[index] = direction;
            }

            return true;
        }

        private AimVector2 ResolveFallbackDirection(
            AimVector2 sharedAimIntent,
            AimVector2 sharedAimPoint,
            AimVector2 geometryCenter)
        {
            AimVector2 normalized;
            if (TryNormalizeVector(sharedAimIntent, minimumDirectionLength, out normalized))
            {
                return normalized;
            }

            double ignoredDistance;
            if (TryNormalizeDifference(
                sharedAimPoint,
                geometryCenter,
                minimumDirectionLength,
                out normalized,
                out ignoredDistance))
            {
                return normalized;
            }

            return AimVector2.UnitX;
        }

        private static WeaponMountOrigin[] CanonicalizeOrigins(WeaponMountOrigin[] source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.Length != FourMountAimSolution.MountCount)
            {
                throw new ArgumentException(
                    "Exactly four mount origins are required.",
                    nameof(source));
            }

            WeaponMountOrigin[] canonical =
                new WeaponMountOrigin[FourMountAimSolution.MountCount];
            bool[] occupied = new bool[FourMountAimSolution.MountCount];

            for (int index = 0; index < source.Length; index++)
            {
                WeaponMountOrigin mount = source[index];
                int stableIndex = mount.StableSlotNumber - 1;
                if (stableIndex < 0 || stableIndex >= FourMountAimSolution.MountCount)
                {
                    throw new ArgumentException(
                        "Every mount origin must identify stable slot 1 through 4.",
                        nameof(source));
                }

                if (occupied[stableIndex])
                {
                    throw new ArgumentException(
                        "Each stable mount slot must appear exactly once.",
                        nameof(source));
                }

                canonical[stableIndex] = mount;
                occupied[stableIndex] = true;
            }

            return canonical;
        }

        private static AimVector2 AverageOrigins(WeaponMountOrigin[] canonicalOrigins)
        {
            double[] xValues = new double[FourMountAimSolution.MountCount];
            double[] yValues = new double[FourMountAimSolution.MountCount];
            for (int index = 0; index < canonicalOrigins.Length; index++)
            {
                xValues[index] = canonicalOrigins[index].Origin.X;
                yValues[index] = canonicalOrigins[index].Origin.Y;
            }

            return new AimVector2(AverageCoordinate(xValues), AverageCoordinate(yValues));
        }

        private static double AverageCoordinate(double[] values)
        {
            double scale = 0d;
            for (int index = 0; index < values.Length; index++)
            {
                double absolute = Math.Abs(values[index]);
                if (absolute > scale)
                {
                    scale = absolute;
                }
            }

            if (scale == 0d)
            {
                return 0d;
            }

            double scaledSum = 0d;
            for (int index = 0; index < values.Length; index++)
            {
                scaledSum += values[index] / scale;
            }

            double scaledAverage = scaledSum / values.Length;
            if (scaledAverage > 1d)
            {
                scaledAverage = 1d;
            }
            else if (scaledAverage < -1d)
            {
                scaledAverage = -1d;
            }

            return scaledAverage * scale;
        }

        private static bool TryNormalizeVector(
            AimVector2 value,
            double minimumLength,
            out AimVector2 direction)
        {
            double scale = Math.Max(Math.Abs(value.X), Math.Abs(value.Y));
            if (scale == 0d)
            {
                direction = AimVector2.UnitX;
                return false;
            }

            double scaledX = value.X / scale;
            double scaledY = value.Y / scale;
            double scaledLength = Math.Sqrt((scaledX * scaledX) + (scaledY * scaledY));
            if (!ExceedsMinimum(scale, scaledLength, minimumLength))
            {
                direction = AimVector2.UnitX;
                return false;
            }

            direction = new AimVector2(scaledX / scaledLength, scaledY / scaledLength);
            return true;
        }

        private static bool TryNormalizeDifference(
            AimVector2 target,
            AimVector2 origin,
            double minimumLength,
            out AimVector2 direction,
            out double distance)
        {
            double scale = Math.Max(
                Math.Max(Math.Abs(target.X), Math.Abs(origin.X)),
                Math.Max(Math.Abs(target.Y), Math.Abs(origin.Y)));
            if (scale == 0d)
            {
                direction = AimVector2.UnitX;
                distance = 0d;
                return false;
            }

            double scaledX = (target.X / scale) - (origin.X / scale);
            double scaledY = (target.Y / scale) - (origin.Y / scale);
            double componentScale = Math.Max(Math.Abs(scaledX), Math.Abs(scaledY));
            if (componentScale == 0d)
            {
                direction = AimVector2.UnitX;
                distance = 0d;
                return false;
            }

            double unitX = scaledX / componentScale;
            double unitY = scaledY / componentScale;
            double componentLength = Math.Sqrt((unitX * unitX) + (unitY * unitY));
            double scaledLength = componentScale * componentLength;
            distance = scale * scaledLength;

            if (!ExceedsMinimum(scale, scaledLength, minimumLength))
            {
                direction = AimVector2.UnitX;
                return false;
            }

            direction = new AimVector2(unitX / componentLength, unitY / componentLength);
            return true;
        }

        private static bool ExceedsMinimum(
            double scale,
            double scaledLength,
            double minimumLength)
        {
            if (minimumLength == 0d)
            {
                return scaledLength > 0d;
            }

            return scale > minimumLength / scaledLength;
        }

        private static double Dot(AimVector2 left, AimVector2 right)
        {
            return (left.X * right.X) + (left.Y * right.Y);
        }
    }
}

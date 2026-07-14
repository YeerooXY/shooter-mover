using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Combat;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class FourMountAimResolverTests
    {
        [Test]
        public void Resolve_DistantAim_ConvergesAllFourSolutionsOnSharedPoint()
        {
            FourMountAimResolver resolver = new FourMountAimResolver(0.01d, 0.5d);
            AimVector2 target = new AimVector2(40d, 10d);

            FourMountAimSolution result = resolver.Resolve(
                ContractAim(1f, 0.25f),
                target,
                StandardOrigins());

            Assert.That(result.Count, Is.EqualTo(WeaponMountContractRules.MountCount));
            for (int index = 0; index < result.Count; index++)
            {
                SharedAimSolution solution = result.GetByStableIndex(index);
                Assert.That(solution.UsedFallbackDirection, Is.False);
                Assert.That(solution.SharedAimPoint, Is.EqualTo(target));
                AssertUnitDirection(solution.Direction);
                AssertRayReachesTarget(solution);
            }

            WriteConvergencePlot("far", result, true, 5d);
        }

        [Test]
        public void Resolve_CloseAim_UsesOneParallelFallbackWithoutCrossOver()
        {
            FourMountAimResolver resolver = new FourMountAimResolver(0.1d, 0.5d);
            AimVector2 target = new AimVector2(1.6d, 0d);

            FourMountAimSolution result = resolver.Resolve(
                ContractAim(1f, 0f),
                target,
                StandardOrigins());

            for (int index = 0; index < result.Count; index++)
            {
                SharedAimSolution solution = result.GetByStableIndex(index);
                Assert.That(solution.UsedFallbackDirection, Is.True);
                Assert.That(solution.SharedAimPoint, Is.EqualTo(target));
                Assert.That(solution.Direction.X, Is.EqualTo(1d).Within(1e-12d));
                Assert.That(solution.Direction.Y, Is.EqualTo(0d).Within(1e-12d));
                AssertUnitDirection(solution.Direction);
            }

            WriteConvergencePlot("near", result, false, 5d);
        }

        [Test]
        public void Resolve_AimInsideMountGeometry_UsesBoundedSharedFallback()
        {
            FourMountAimSolution result = new FourMountAimResolver().Resolve(
                ContractAim(0f, 1f),
                AimVector2.Zero,
                StandardOrigins());

            AssertAllFallback(result, new AimVector2(0d, 1d), AimVector2.Zero);
        }

        [Test]
        public void Resolve_AimCoincidentWithOneOrigin_FallsBackForWholeArray()
        {
            AimVector2 coincidentPoint = new AimVector2(-1d, 1d);
            FourMountAimSolution result = new FourMountAimResolver().Resolve(
                ContractAim(1f, 1f),
                coincidentPoint,
                StandardOrigins());

            AimVector2 expected = Normalized(new AimVector2(1d, 1d));
            AssertAllFallback(result, expected, coincidentPoint);
        }

        [Test]
        public void Resolve_BehindPlayerAim_UsesForwardIntentInsteadOfReversingRays()
        {
            AimVector2 behindPoint = new AimVector2(-20d, 0d);
            FourMountAimSolution result = new FourMountAimResolver().Resolve(
                ContractAim(1f, 0f),
                behindPoint,
                StandardOrigins());

            AssertAllFallback(result, new AimVector2(1d, 0d), behindPoint);
        }

        [Test]
        public void Resolve_MirroredOrigins_ProducesMirroredConvergedDirections()
        {
            WeaponMountOrigin[] origins =
            {
                Origin(WeaponMountSlot.MountOne, -2d, 1d),
                Origin(WeaponMountSlot.MountTwo, 2d, 1d),
                Origin(WeaponMountSlot.MountThree, -2d, -1d),
                Origin(WeaponMountSlot.MountFour, 2d, -1d),
            };

            FourMountAimSolution result = new FourMountAimResolver().Resolve(
                ContractAim(0f, 1f),
                new AimVector2(0d, 100d),
                origins);

            SharedAimSolution one = result.GetByStableSlotNumber((int)WeaponMountSlot.MountOne);
            SharedAimSolution two = result.GetByStableSlotNumber((int)WeaponMountSlot.MountTwo);
            SharedAimSolution three = result.GetByStableSlotNumber((int)WeaponMountSlot.MountThree);
            SharedAimSolution four = result.GetByStableSlotNumber((int)WeaponMountSlot.MountFour);

            Assert.That(one.UsedFallbackDirection, Is.False);
            Assert.That(two.UsedFallbackDirection, Is.False);
            Assert.That(three.UsedFallbackDirection, Is.False);
            Assert.That(four.UsedFallbackDirection, Is.False);
            Assert.That(one.Direction.X, Is.EqualTo(-two.Direction.X).Within(1e-12d));
            Assert.That(one.Direction.Y, Is.EqualTo(two.Direction.Y).Within(1e-12d));
            Assert.That(three.Direction.X, Is.EqualTo(-four.Direction.X).Within(1e-12d));
            Assert.That(three.Direction.Y, Is.EqualTo(four.Direction.Y).Within(1e-12d));
        }

        [Test]
        public void Resolve_CanonicalizesStableContractSlotOrderAndCopiesInputs()
        {
            WeaponMountOrigin[] shuffled =
            {
                Origin(WeaponMountSlot.MountFour, 4d, -1d),
                Origin(WeaponMountSlot.MountTwo, 2d, 1d),
                Origin(WeaponMountSlot.MountOne, 1d, 1d),
                Origin(WeaponMountSlot.MountThree, 3d, -1d),
            };

            FourMountAimSolution result = new FourMountAimResolver().Resolve(
                ContractAim(1f, 0f),
                new AimVector2(100d, 0d),
                shuffled);

            for (int index = 0; index < result.Count; index++)
            {
                WeaponMountSlot expectedSlot = WeaponMountContractRules.GetSlotAtHudIndex(index);
                Assert.That(result.GetByStableIndex(index).StableSlotNumber, Is.EqualTo((int)expectedSlot));
            }

            AimVector2 preservedMountTwoOrigin = result
                .GetByStableSlotNumber((int)WeaponMountSlot.MountTwo)
                .Origin;
            shuffled[1] = Origin(WeaponMountSlot.MountTwo, 999d, 999d);

            Assert.That(
                result.GetByStableSlotNumber((int)WeaponMountSlot.MountTwo).Origin,
                Is.EqualTo(preservedMountTwoOrigin));
        }

        [Test]
        public void Resolve_ChangingOneOriginDoesNotChangeOtherFarMountSolutions()
        {
            FourMountAimResolver resolver = new FourMountAimResolver();
            AimVector2 target = new AimVector2(100d, 30d);
            AimVector2 aim = ContractAim(1f, 0.3f);
            WeaponMountOrigin[] baselineOrigins = StandardOrigins();

            FourMountAimSolution baseline = resolver.Resolve(aim, target, baselineOrigins);
            baselineOrigins[0] = Origin(WeaponMountSlot.MountOne, -10d, 4d);
            FourMountAimSolution changed = resolver.Resolve(aim, target, baselineOrigins);

            for (int slotNumber = 2; slotNumber <= FourMountAimSolution.MountCount; slotNumber++)
            {
                SharedAimSolution before = baseline.GetByStableSlotNumber(slotNumber);
                SharedAimSolution after = changed.GetByStableSlotNumber(slotNumber);
                Assert.That(after.Origin, Is.EqualTo(before.Origin));
                Assert.That(after.Direction.X, Is.EqualTo(before.Direction.X).Within(1e-15d));
                Assert.That(after.Direction.Y, Is.EqualTo(before.Direction.Y).Within(1e-15d));
            }
        }

        [Test]
        public void Resolve_ZeroIntentAndCoincidentOrigins_UsesDeterministicUnitXFallback()
        {
            WeaponMountOrigin[] coincident =
            {
                Origin(WeaponMountSlot.MountOne, 5d, 5d),
                Origin(WeaponMountSlot.MountTwo, 5d, 5d),
                Origin(WeaponMountSlot.MountThree, 5d, 5d),
                Origin(WeaponMountSlot.MountFour, 5d, 5d),
            };
            AimVector2 coincidentPoint = new AimVector2(5d, 5d);

            FourMountAimSolution result = new FourMountAimResolver().Resolve(
                AimVector2.Zero,
                coincidentPoint,
                coincident);

            AssertAllFallback(result, AimVector2.UnitX, coincidentPoint);
        }

        [Test]
        public void Resolve_ExtremeFiniteCoordinates_NeverProducesNaNOrZeroDirection()
        {
            const double originScale = 1e300d;
            const double offset = 1e290d;
            WeaponMountOrigin[] origins =
            {
                Origin(WeaponMountSlot.MountOne, originScale - offset, offset),
                Origin(WeaponMountSlot.MountTwo, originScale + offset, offset),
                Origin(WeaponMountSlot.MountThree, originScale - offset, -offset),
                Origin(WeaponMountSlot.MountFour, originScale + offset, -offset),
            };

            FourMountAimSolution result = new FourMountAimResolver().Resolve(
                ContractAim(1f, 0f),
                new AimVector2(1.2e300d, 0d),
                origins);

            for (int index = 0; index < result.Count; index++)
            {
                AimVector2 direction = result.GetByStableIndex(index).Direction;
                Assert.That(double.IsNaN(direction.X), Is.False);
                Assert.That(double.IsNaN(direction.Y), Is.False);
                Assert.That(double.IsInfinity(direction.X), Is.False);
                Assert.That(double.IsInfinity(direction.Y), Is.False);
                AssertUnitDirection(direction);
            }
        }

        [Test]
        public void Resolve_RejectsWrongCountDuplicateSlotsAndDefaultOrigins()
        {
            FourMountAimResolver resolver = new FourMountAimResolver();

            Assert.Throws<ArgumentException>(
                () => resolver.Resolve(ContractAim(1f, 0f), new AimVector2(10d, 0d)));
            Assert.Throws<ArgumentException>(
                () => resolver.Resolve(
                    ContractAim(1f, 0f),
                    new AimVector2(10d, 0d),
                    Origin(WeaponMountSlot.MountOne, 0d, 0d),
                    Origin(WeaponMountSlot.MountOne, 1d, 0d),
                    Origin(WeaponMountSlot.MountThree, 2d, 0d),
                    Origin(WeaponMountSlot.MountFour, 3d, 0d)));
            Assert.Throws<ArgumentException>(
                () => resolver.Resolve(
                    ContractAim(1f, 0f),
                    new AimVector2(10d, 0d),
                    default(WeaponMountOrigin),
                    Origin(WeaponMountSlot.MountTwo, 1d, 0d),
                    Origin(WeaponMountSlot.MountThree, 2d, 0d),
                    Origin(WeaponMountSlot.MountFour, 3d, 0d)));
        }

        [Test]
        public void ValuesAndResolver_RejectNonFiniteOrInvalidSafeguards()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AimVector2(double.NaN, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AimVector2(0d, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FourMountAimResolver(0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FourMountAimResolver(0.1d, -0.1d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FourMountAimResolver(double.MaxValue, double.MaxValue));
        }

        [Test]
        public void SolutionTypes_AreImmutableAndDomainAssemblyIsEngineFree()
        {
            Type[] immutableTypes =
            {
                typeof(AimVector2),
                typeof(WeaponMountOrigin),
                typeof(SharedAimSolution),
                typeof(FourMountAimSolution),
                typeof(FourMountAimResolver),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(property => property.CanWrite),
                    Is.Empty,
                    type.Name + " exposed a writable property.");
            }

            Assert.That(typeof(SharedAimSolution).IsSealed, Is.True);
            Assert.That(typeof(FourMountAimSolution).IsSealed, Is.True);
            Assert.That(typeof(FourMountAimResolver).IsSealed, Is.True);
            Assert.That(
                typeof(FourMountAimResolver).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static WeaponMountOrigin[] StandardOrigins()
        {
            return new[]
            {
                Origin(WeaponMountSlot.MountOne, -1d, 1d),
                Origin(WeaponMountSlot.MountTwo, 1d, 1d),
                Origin(WeaponMountSlot.MountThree, -1d, -1d),
                Origin(WeaponMountSlot.MountFour, 1d, -1d),
            };
        }

        private static WeaponMountOrigin Origin(
            WeaponMountSlot slot,
            double x,
            double y)
        {
            return new WeaponMountOrigin((int)slot, new AimVector2(x, y));
        }

        private static AimVector2 ContractAim(float x, float y)
        {
            NormalizedIntentVector2 contractValue = NormalizedIntentVector2.Create(x, y);
            return new AimVector2(contractValue.X, contractValue.Y);
        }

        private static void AssertAllFallback(
            FourMountAimSolution result,
            AimVector2 expectedDirection,
            AimVector2 expectedAimPoint)
        {
            for (int index = 0; index < result.Count; index++)
            {
                SharedAimSolution solution = result.GetByStableIndex(index);
                Assert.That(solution.UsedFallbackDirection, Is.True);
                Assert.That(solution.SharedAimPoint, Is.EqualTo(expectedAimPoint));
                Assert.That(solution.Direction.X, Is.EqualTo(expectedDirection.X).Within(1e-12d));
                Assert.That(solution.Direction.Y, Is.EqualTo(expectedDirection.Y).Within(1e-12d));
                AssertUnitDirection(solution.Direction);
            }
        }

        private static void AssertRayReachesTarget(SharedAimSolution solution)
        {
            double deltaX = solution.SharedAimPoint.X - solution.Origin.X;
            double deltaY = solution.SharedAimPoint.Y - solution.Origin.Y;
            double distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            double reachedX = solution.Origin.X + (solution.Direction.X * distance);
            double reachedY = solution.Origin.Y + (solution.Direction.Y * distance);

            Assert.That(reachedX, Is.EqualTo(solution.SharedAimPoint.X).Within(1e-10d));
            Assert.That(reachedY, Is.EqualTo(solution.SharedAimPoint.Y).Within(1e-10d));
        }

        private static void AssertUnitDirection(AimVector2 direction)
        {
            double magnitude = Math.Sqrt(
                (direction.X * direction.X) + (direction.Y * direction.Y));
            Assert.That(magnitude, Is.EqualTo(1d).Within(1e-12d));
            Assert.That(direction.X, Is.InRange(-1d, 1d));
            Assert.That(direction.Y, Is.InRange(-1d, 1d));
        }

        private static AimVector2 Normalized(AimVector2 value)
        {
            double magnitude = Math.Sqrt((value.X * value.X) + (value.Y * value.Y));
            return new AimVector2(value.X / magnitude, value.Y / magnitude);
        }

        private static void WriteConvergencePlot(
            string label,
            FourMountAimSolution result,
            bool terminateAtSharedPoint,
            double fallbackRayLength)
        {
            TestContext.WriteLine("PLOT " + label);
            for (int index = 0; index < result.Count; index++)
            {
                SharedAimSolution solution = result.GetByStableIndex(index);
                double rayLength = fallbackRayLength;
                if (terminateAtSharedPoint)
                {
                    double deltaX = solution.SharedAimPoint.X - solution.Origin.X;
                    double deltaY = solution.SharedAimPoint.Y - solution.Origin.Y;
                    rayLength = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                }

                double rayEndX = solution.Origin.X + (solution.Direction.X * rayLength);
                double rayEndY = solution.Origin.Y + (solution.Direction.Y * rayLength);
                TestContext.WriteLine(
                    "slot=" + solution.StableSlotNumber
                    + " origin=" + solution.Origin
                    + " direction=" + solution.Direction
                    + " ray_end=(" + rayEndX.ToString("R") + ", " + rayEndY.ToString("R") + ")"
                    + " shared_point=" + solution.SharedAimPoint
                    + " fallback=" + solution.UsedFallbackDirection);
            }
        }
    }
}

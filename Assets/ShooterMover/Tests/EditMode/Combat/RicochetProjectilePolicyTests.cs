#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ShooterMover.Tests.EditMode.Combat
{
    /// <summary>
    /// ContentPackages compile into Assembly-CSharp, so this asmdef-backed fixture
    /// exercises the real package policy through the accepted reflection bridge.
    /// </summary>
    public sealed class RicochetProjectilePolicyTests
    {
        [Test]
        public void FirstBounce_ReflectsAndIncrementsExactlyOnce()
        {
            object policy = CreatePolicy(8d);
            object result = Resolve(
                policy,
                Vector(1d, -1d),
                Vector(0d, 1d));

            Assert.That(EnumName(result, "Kind"), Is.EqualTo("Reflected"));
            Assert.That(GetProperty<int>(result, "WallBounceCount"), Is.EqualTo(1));
            AssertVector(GetProperty<object>(result, "Direction"), 1d, 1d);
            Assert.That(GetProperty<int>(policy, "WallBounceCount"), Is.EqualTo(1));
            Assert.That(GetProperty<bool>(policy, "IsTerminated"), Is.False);
            TestContext.WriteLine(
                "first-bounce count=1 direction=(0.70710678,0.70710678)");
        }

        [Test]
        public void SecondBounce_ReflectsAndReachesFixedCap()
        {
            object policy = CreatePolicy(8d);
            object first = Resolve(
                policy,
                Vector(1d, -1d),
                Vector(0d, 1d));
            object second = Resolve(
                policy,
                GetProperty<object>(first, "Direction"),
                Vector(-1d, 0d));

            Assert.That(EnumName(second, "Kind"), Is.EqualTo("Reflected"));
            Assert.That(GetProperty<int>(second, "WallBounceCount"), Is.EqualTo(2));
            AssertVector(GetProperty<object>(second, "Direction"), -1d, 1d);
            Assert.That(GetProperty<int>(policy, "WallBounceCount"), Is.EqualTo(2));
            Assert.That(GetProperty<bool>(policy, "IsTerminated"), Is.False);
            TestContext.WriteLine(
                "second-bounce count=2 direction=(-0.70710678,0.70710678)");
        }

        [Test]
        public void ThirdWallCollision_TerminatesWithoutThirdReflection()
        {
            object policy = CreatePolicy(8d);
            object first = Resolve(
                policy,
                Vector(1d, -1d),
                Vector(0d, 1d));
            object second = Resolve(
                policy,
                GetProperty<object>(first, "Direction"),
                Vector(-1d, 0d));
            object third = Resolve(
                policy,
                GetProperty<object>(second, "Direction"),
                Vector(0d, -1d));

            Assert.That(
                EnumName(third, "Kind"),
                Is.EqualTo("TerminatedBounceLimit"));
            Assert.That(GetProperty<int>(third, "WallBounceCount"), Is.EqualTo(2));
            Assert.That(GetProperty<int>(policy, "WallBounceCount"), Is.EqualTo(2));
            Assert.That(GetProperty<bool>(policy, "IsTerminated"), Is.True);
            Assert.That(
                EnumName(policy, "TerminationReason"),
                Is.EqualTo("ThirdWallCollision"));
            TestContext.WriteLine(
                "third-collision terminated=true reflected=false count=2");
        }

        [Test]
        public void CornerCollision_IsIndependentOfContactOrdering()
        {
            object firstPolicy = CreatePolicy(8d);
            object secondPolicy = CreatePolicy(8d);
            object incoming = Vector(1d, 1d);
            object normalX = Vector(-1d, 0d);
            object normalY = Vector(0d, -1d);

            object first = Resolve(firstPolicy, incoming, normalX, normalY);
            object second = Resolve(secondPolicy, incoming, normalY, normalX);
            object firstDirection = GetProperty<object>(first, "Direction");
            object secondDirection = GetProperty<object>(second, "Direction");

            AssertVector(firstDirection, -1d, -1d);
            Assert.That(
                GetProperty<double>(firstDirection, "X"),
                Is.EqualTo(GetProperty<double>(secondDirection, "X"))
                    .Within(0.000000001d));
            Assert.That(
                GetProperty<double>(firstDirection, "Y"),
                Is.EqualTo(GetProperty<double>(secondDirection, "Y"))
                    .Within(0.000000001d));
            Assert.That(GetProperty<int>(first, "WallBounceCount"), Is.EqualTo(1));
            Assert.That(GetProperty<int>(second, "WallBounceCount"), Is.EqualTo(1));

            TestContext.WriteLine(
                "bounce-trace fixture=corner incoming=(1,1) normals=[(-1,0),(0,-1)] outgoing=(-0.70710678,-0.70710678) count=1 order-independent=true");
        }

        [Test]
        public void GrazingContact_DoesNotReflectOrConsumeBounce()
        {
            object policy = CreatePolicy(8d);
            object result = Resolve(
                policy,
                Vector(1d, 0d),
                Vector(0d, 1d));

            Assert.That(EnumName(result, "Kind"), Is.EqualTo("GrazingIgnored"));
            AssertVector(GetProperty<object>(result, "Direction"), 1d, 0d);
            Assert.That(GetProperty<int>(policy, "WallBounceCount"), Is.Zero);
            Assert.That(GetProperty<bool>(policy, "IsTerminated"), Is.False);
            TestContext.WriteLine(
                "grazing-contact reflected=false count=0 direction=(1,0)");
        }

        [Test]
        public void LifetimeExpiry_IsFiniteAndWinsBeforeLaterWallContact()
        {
            object policy = CreatePolicy(8d);

            Assert.That(AdvanceLifetime(policy, 7.5d), Is.True);
            Assert.That(AdvanceLifetime(policy, 0.5d), Is.False);
            Assert.That(
                GetProperty<double>(policy, "RemainingLifetimeSeconds"),
                Is.Zero);
            Assert.That(GetProperty<bool>(policy, "IsTerminated"), Is.True);
            Assert.That(
                EnumName(policy, "TerminationReason"),
                Is.EqualTo("LifetimeExpired"));

            object afterExpiry = Resolve(
                policy,
                Vector(1d, -1d),
                Vector(0d, 1d));
            Assert.That(
                EnumName(afterExpiry, "Kind"),
                Is.EqualTo("AlreadyTerminated"));
            Assert.That(GetProperty<int>(policy, "WallBounceCount"), Is.Zero);
            TestContext.WriteLine(
                "lifetime-expiry seconds=8 terminated=true later-bounce=false");
        }

        [Test]
        public void PackageDescriptor_FixesTopologyAndAllowsNumericOnlyEmpowerment()
        {
            object descriptor = InvokeStatic(RuntimeTypes.Package, "CreateDescriptor");
            object normal = GetProperty<object>(descriptor, "NormalFire");
            object empowered = GetProperty<object>(descriptor, "EmpoweredFire");
            object normalTopology = GetProperty<object>(normal, "Topology");
            object empoweredTopology = GetProperty<object>(empowered, "Topology");

            Assert.That(
                GetProperty<int>(normalTopology, "WallBounceCount"),
                Is.EqualTo(2));
            Assert.That(
                GetProperty<int>(empoweredTopology, "WallBounceCount"),
                Is.EqualTo(2));
            Assert.That(
                normalTopology.ToString(),
                Is.EqualTo(empoweredTopology.ToString()));
            Assert.That(
                GetStaticProperty<int>(RuntimeTypes.Package, "MaximumWallBounces"),
                Is.EqualTo(2));

            List<object> normalCoefficients =
                GetObjectList(normal, "NumericCoefficients");
            List<object> empoweredCoefficients =
                GetObjectList(empowered, "NumericCoefficients");
            Assert.That(
                normalCoefficients.Select(value => EnumName(value, "Kind")),
                Is.EqualTo(
                    empoweredCoefficients.Select(value => EnumName(value, "Kind"))));
            Assert.That(
                normalCoefficients.Select(value => GetProperty<double>(value, "Value")),
                Is.Not.EqualTo(
                    empoweredCoefficients.Select(value => GetProperty<double>(value, "Value"))));

            double normalLifetime = CoefficientValue(
                normalCoefficients,
                "ProjectileLifetimeSeconds");
            double empoweredLifetime = CoefficientValue(
                empoweredCoefficients,
                "ProjectileLifetimeSeconds");
            Assert.That(normalLifetime, Is.GreaterThan(0d).And.LessThanOrEqualTo(30d));
            Assert.That(empoweredLifetime, Is.GreaterThan(0d).And.LessThanOrEqualTo(30d));
            TestContext.WriteLine(
                "numeric-only normal-lifetime=8 empowered-lifetime=10 topology-bounces=2 persistent=false");
        }

        private static object CreatePolicy(double lifetimeSeconds)
        {
            return Activator.CreateInstance(RuntimeTypes.Policy, lifetimeSeconds);
        }

        private static object Vector(double x, double y)
        {
            return Activator.CreateInstance(RuntimeTypes.Vector, x, y);
        }

        private static object Resolve(
            object policy,
            object incomingDirection,
            params object[] normals)
        {
            Array typedNormals = Array.CreateInstance(RuntimeTypes.Vector, normals.Length);
            for (int index = 0; index < normals.Length; index++)
            {
                typedNormals.SetValue(normals[index], index);
            }

            return InvokeInstance(
                policy,
                "ResolveWallContact",
                incomingDirection,
                typedNormals);
        }

        private static bool AdvanceLifetime(object policy, double deltaSeconds)
        {
            return (bool)InvokeInstance(policy, "AdvanceLifetime", deltaSeconds);
        }

        private static void AssertVector(
            object vector,
            double expectedX,
            double expectedY)
        {
            double expectedLength = Math.Sqrt(
                (expectedX * expectedX) + (expectedY * expectedY));
            Assert.That(
                GetProperty<double>(vector, "X"),
                Is.EqualTo(expectedX / expectedLength).Within(0.000000001d));
            Assert.That(
                GetProperty<double>(vector, "Y"),
                Is.EqualTo(expectedY / expectedLength).Within(0.000000001d));
        }

        private static double CoefficientValue(
            IEnumerable<object> coefficients,
            string kindName)
        {
            object coefficient = coefficients.Single(
                value => EnumName(value, "Kind") == kindName);
            return GetProperty<double>(coefficient, "Value");
        }

        private static List<object> GetObjectList(object instance, string propertyName)
        {
            object value = GetProperty<object>(instance, propertyName);
            return ((System.Collections.IEnumerable)value).Cast<object>().ToList();
        }

        private static string EnumName(object instance, string propertyName)
        {
            return GetProperty<object>(instance, propertyName).ToString();
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static T GetStaticProperty<T>(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(null, null);
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }

        private static class RuntimeTypes
        {
            public static readonly Type Policy = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetProjectilePolicy");
            public static readonly Type Vector = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetVector2");
            public static readonly Type Package = Find(
                "ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime.RicochetGunPackage");

            private static Type Find(string fullName)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .FirstOrDefault(candidate => candidate != null);
                if (type == null)
                {
                    throw new InvalidOperationException(
                        "Runtime type not found: " + fullName);
                }

                return type;
            }
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Combat
{
    /// <summary>
    /// ContentPackages compile into Assembly-CSharp, so this fixture uses the same
    /// narrow reflection bridge as the accepted Stage 1 package validator tests.
    /// </summary>
    public sealed class ArcGunChainTests
    {
        [Test]
        public void ZeroToThreeChain_SelectsExactlyTheAvailableEligibleCount()
        {
            for (int expected = 0; expected <= 3; expected++)
            {
                object primary = Target("enemy.arc-primary-" + expected, 0d, 0d, true, true);
                object[] candidates = Enumerable.Range(0, expected)
                    .Select(index => Target(
                        "enemy.arc-candidate-" + expected + "-" + index,
                        index + 1d,
                        0d,
                        true,
                        true))
                    .ToArray();

                object result = Resolve(primary, candidates, 2d, id => true);
                Assert.That(Get<int>(result, "AdditionalHitCount"), Is.EqualTo(expected));
            }

            object skipPrimary = Target("enemy.arc-primary-skip", 0d, 0d, true, true);
            object[] mixed =
            {
                Target("enemy.arc-dead", 1d, 0d, false, true),
                Target("enemy.arc-invalid", 1d, 1d, true, false),
                Target("enemy.arc-out-of-range", 20d, 0d, true, true),
                Target("enemy.arc-valid", 2d, 0d, true, true),
            };
            object mixedResult = Resolve(skipPrimary, mixed, 3d, id => true);
            Assert.That(Get<int>(mixedResult, "AdditionalHitCount"), Is.EqualTo(1));
            Assert.That(GetTargetIds(mixedResult), Is.EqualTo(new[] { Id("enemy.arc-valid") }));

            object invalidPrimary = Target(
                "enemy.arc-invalid-primary",
                0d,
                0d,
                true,
                false);
            object invalidPrimaryResult = Resolve(invalidPrimary, mixed, 3d, id => true);
            Assert.That(Get<int>(invalidPrimaryResult, "AdditionalHitCount"), Is.Zero);

            TestContext.WriteLine(
                "zero-to-three counts=0,1,2,3 skipped=dead,invalid,out-of-range invalid-primary=0");
        }

        [Test]
        public void FourthTarget_IsRejectedByThePackageTopologyCap()
        {
            object primary = Target("enemy.arc-primary", 0d, 0d, true, true);
            object[] candidates =
            {
                Target("enemy.arc-a", 1d, 0d, true, true),
                Target("enemy.arc-b", 2d, 0d, true, true),
                Target("enemy.arc-c", 3d, 0d, true, true),
                Target("enemy.arc-d", 4d, 0d, true, true),
            };

            object result = Resolve(primary, candidates, 10d, id => true);

            Assert.That(Get<int>(result, "AdditionalHitCount"), Is.EqualTo(3));
            Assert.That(
                GetTargetIds(result),
                Is.EqualTo(
                    new[]
                    {
                        Id("enemy.arc-a"),
                        Id("enemy.arc-b"),
                        Id("enemy.arc-c"),
                    }));
            Assert.That(
                GetStatic<int>(RuntimeTypes.Resolver, "MaximumAdditionalTargets"),
                Is.EqualTo(3));
            TestContext.WriteLine("fourth-target candidates=4 selected=3 rejected=enemy.arc-d");
        }

        [Test]
        public void EqualDistance_UsesStableTargetIdentityAsTieBreak()
        {
            object primary = Target("enemy.arc-primary", 0d, 0d, true, true);
            object[] candidates =
            {
                Target("enemy.arc-b", 1d, 0d, true, true),
                Target("enemy.arc-a", -1d, 0d, true, true),
            };

            object result = Resolve(primary, candidates, 5d, id => true);

            Assert.That(GetTargetIds(result).First(), Is.EqualTo(Id("enemy.arc-a")));
            TestContext.WriteLine("stable-tie distance=1 winner=enemy.arc-a");
        }

        [Test]
        public void DisappearingTarget_IsSkippedAfterRanking()
        {
            object primary = Target("enemy.arc-primary", 0d, 0d, true, true);
            object[] candidates =
            {
                Target("enemy.arc-a", 1d, 0d, true, true),
                Target("enemy.arc-b", 2d, 0d, true, true),
                Target("enemy.arc-c", 3d, 0d, true, true),
            };
            StableId disappearing = Id("enemy.arc-a");
            List<StableId> confirmationAttempts = new List<StableId>();

            object result = Resolve(
                primary,
                candidates,
                5d,
                id =>
                {
                    confirmationAttempts.Add(id);
                    return id != disappearing;
                });

            Assert.That(GetTargetIds(result).Contains(disappearing), Is.False);
            Assert.That(GetTargetIds(result).First(), Is.EqualTo(Id("enemy.arc-b")));
            Assert.That(confirmationAttempts, Does.Contain(disappearing));
            Assert.That(confirmationAttempts.Count(id => id == disappearing), Is.EqualTo(1));
            TestContext.WriteLine(
                "disappearing-target ranked=enemy.arc-a confirmed=false first-hit=enemy.arc-b attempts=1");
        }

        [Test]
        public void DuplicateAndVisitedTargets_AreNeverHitTwice()
        {
            object primary = Target("enemy.arc-primary", 0d, 0d, true, true);
            object duplicateA = Target("enemy.arc-a", 1d, 0d, true, true);
            object[] candidates =
            {
                primary,
                duplicateA,
                duplicateA,
                Target("enemy.arc-a", 1d, 0d, true, true),
                Target("enemy.arc-b", 2d, 0d, true, true),
                Target("enemy.arc-c", 3d, 0d, true, true),
            };

            object result = Resolve(primary, candidates, 5d, id => true);
            List<StableId> ids = GetTargetIds(result);

            Assert.That(ids, Is.Unique);
            Assert.That(ids.Contains(Id("enemy.arc-primary")), Is.False);
            Assert.That(ids.Count(id => id == Id("enemy.arc-a")), Is.EqualTo(1));
            TestContext.WriteLine("no-repeat primary=false duplicate-a-hits=1 unique=true");
        }

        [Test]
        public void ChainOrderFixture_IsCanonicalAndReadable()
        {
            object primary = Target("enemy.arc-primary", 0d, 0d, true, true);
            object[] candidates =
            {
                Target("enemy.arc-d", 5d, 0d, true, true),
                Target("enemy.arc-c", 4d, 0d, true, true),
                Target("enemy.arc-b", 3d, 0d, true, true),
                Target("enemy.arc-a", 2d, 0d, true, true),
            };

            object result = Resolve(primary, candidates, 3d, id => true);
            string canonical = (string)Invoke(result, "ToCanonicalString");

            Assert.That(
                GetTargetIds(result),
                Is.EqualTo(
                    new[]
                    {
                        Id("enemy.arc-a"),
                        Id("enemy.arc-b"),
                        Id("enemy.arc-c"),
                    }));
            Assert.That(canonical, Does.Contain("enemy.arc-primary->enemy.arc-a@4"));
            Assert.That(canonical, Does.Contain("enemy.arc-a->enemy.arc-b@1"));
            Assert.That(canonical, Does.Contain("enemy.arc-b->enemy.arc-c@1"));
            TestContext.WriteLine("chain-order-fixture\n" + canonical);
        }

        private static StableId Id(string text)
        {
            return StableId.Parse(text);
        }

        private static object Target(
            string id,
            double x,
            double y,
            bool alive,
            bool valid)
        {
            return InvokeStatic(
                RuntimeTypes.Target,
                "Create",
                Id(id),
                x,
                y,
                alive,
                valid);
        }

        private static object Resolve(
            object primary,
            IEnumerable<object> candidates,
            double range,
            Func<StableId, bool> confirmation)
        {
            object[] copy = candidates.ToArray();
            Array typed = Array.CreateInstance(RuntimeTypes.Target, copy.Length);
            for (int index = 0; index < copy.Length; index++)
            {
                typed.SetValue(copy[index], index);
            }

            return InvokeStatic(
                RuntimeTypes.Resolver,
                "Resolve",
                primary,
                typed,
                range,
                confirmation);
        }

        private static List<StableId> GetTargetIds(object result)
        {
            int count = Get<int>(result, "AdditionalHitCount");
            List<StableId> ids = new List<StableId>();
            for (int index = 0; index < count; index++)
            {
                ids.Add((StableId)Invoke(result, "GetAdditionalTargetId", index));
            }

            return ids;
        }

        private static T Get<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static T GetStatic<T>(Type type, string propertyName)
        {
            FieldInfo field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                return (T)field.GetValue(null);
            }

            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(null, null);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return InvokeMethod(method, null, arguments);
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return InvokeMethod(method, instance, arguments);
        }

        private static object InvokeMethod(MethodInfo method, object instance, object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }

                throw;
            }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Target = Find(
                "ShooterMover.ContentPackages.Weapons.ArcGun.ArcTargetSnapshot");
            public static readonly Type Resolver = Find(
                "ShooterMover.ContentPackages.Weapons.ArcGun.ArcGunChainResolver");

            private static Type Find(string fullName)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }

                throw new InvalidOperationException("Production type was not loaded: " + fullName);
            }
        }
    }
}

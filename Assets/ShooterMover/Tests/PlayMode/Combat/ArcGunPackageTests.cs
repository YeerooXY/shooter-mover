#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class ArcGunPackageTests
    {
        [Test]
        public void Descriptor_PreservesStableIdentityAndNumericOnlyEmpowerment()
        {
            object descriptor = InvokeStatic(RuntimeTypes.Package, "CreateDescriptor");
            object normal = Get<object>(descriptor, "NormalFire");
            object empowered = Get<object>(descriptor, "EmpoweredFire");
            object normalTopology = Get<object>(normal, "Topology");
            object empoweredTopology = Get<object>(empowered, "Topology");

            Assert.That(
                Get<StableId>(descriptor, "DefinitionId"),
                Is.EqualTo(StableId.Parse("weapon.arc-gun")));
            Assert.That(Get<bool>(descriptor, "IsDefaultStartingWeapon"), Is.False);
            Assert.That(Convert.ToInt32(Get<object>(normalTopology, "Kind")), Is.EqualTo(4));
            Assert.That(Get<int>(normalTopology, "AdditionalTargetCount"), Is.EqualTo(3));
            Assert.That(Get<int>(empoweredTopology, "AdditionalTargetCount"), Is.EqualTo(3));
            Assert.That(normalTopology.ToString(), Is.EqualTo(empoweredTopology.ToString()));

            Assert.That(Coefficient(normal, 1), Is.EqualTo(12d));
            Assert.That(Coefficient(empowered, 1), Is.EqualTo(16d));
            Assert.That(Coefficient(normal, 6), Is.EqualTo(6d));
            Assert.That(Coefficient(empowered, 6), Is.EqualTo(7d));

            WeaponRuntimeProfile normalRuntime = Get<WeaponRuntimeProfile>(normal, "RuntimeProfile");
            WeaponRuntimeProfile empoweredRuntime =
                Get<WeaponRuntimeProfile>(empowered, "RuntimeProfile");
            Assert.That(normalRuntime.BehaviorModuleCount, Is.EqualTo(1));
            Assert.That(empoweredRuntime.BehaviorModuleCount, Is.EqualTo(1));
            Assert.That(
                normalRuntime.GetBehaviorModuleId(0),
                Is.EqualTo(StableId.Parse("module.weapon-arc-chain")));
            Assert.That(
                empoweredRuntime.GetBehaviorModuleId(0),
                Is.EqualTo(normalRuntime.GetBehaviorModuleId(0)));

            TestContext.WriteLine(
                "descriptor id=weapon.arc-gun topology=primary+3 normal=damage12/range6 empowered=damage16/range7");
        }

        [Test]
        public void BehaviorPipeline_EmitsOneBoundedChainOperationForEachPowerMode()
        {
            object descriptor = InvokeStatic(RuntimeTypes.Package, "CreateDescriptor");
            object normal = Get<object>(descriptor, "NormalFire");
            object empowered = Get<object>(descriptor, "EmpoweredFire");
            IWeaponBehaviorModule module =
                (IWeaponBehaviorModule)Activator.CreateInstance(RuntimeTypes.Module);
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(new[] { module });

            object normalOperation = BuildOperation(
                pipeline,
                Get<WeaponRuntimeProfile>(normal, "RuntimeProfile"),
                false,
                "combat-event.arc-normal");
            object empoweredOperation = BuildOperation(
                pipeline,
                Get<WeaponRuntimeProfile>(empowered, "RuntimeProfile"),
                true,
                "combat-event.arc-empowered");

            Assert.That(Get<int>(normalOperation, "MaximumAdditionalTargets"), Is.EqualTo(3));
            Assert.That(Get<int>(empoweredOperation, "MaximumAdditionalTargets"), Is.EqualTo(3));
            Assert.That(Get<double>(normalOperation, "Damage"), Is.EqualTo(12d));
            Assert.That(Get<double>(empoweredOperation, "Damage"), Is.EqualTo(16d));
            Assert.That(Get<double>(normalOperation, "EffectRange"), Is.EqualTo(6d));
            Assert.That(Get<double>(empoweredOperation, "EffectRange"), Is.EqualTo(7d));
            Assert.That(
                Get<StableId>(normalOperation, "OperationKindId"),
                Is.EqualTo(StableId.Parse("operation-kind.arc-chain")));
            Assert.That(
                Get<StableId>(normalOperation, "OperationId"),
                Is.Not.EqualTo(Get<StableId>(empoweredOperation, "OperationId")));

            TestContext.WriteLine(
                "pipeline operations=1 cap-normal=3 cap-empowered=3 topology-change=false");
        }

        [Test]
        public void EmptyPower_ImmediatelySelectsNormalFallbackProfile()
        {
            object descriptor = InvokeStatic(RuntimeTypes.Package, "CreateDescriptor");
            object normal = Get<object>(descriptor, "NormalFire");
            object empowered = Get<object>(descriptor, "EmpoweredFire");
            WeaponRuntimeProfile normalRuntime = Get<WeaponRuntimeProfile>(normal, "RuntimeProfile");

            WeaponPowerBankState empty = WeaponPowerBankState.FromProfile(normalRuntime, 0d);
            WeaponPowerFireDecision fallback = WeaponPowerBankPolicy.ResolveFire(empty, true, true);
            object selectedFallback = InvokeStatic(
                RuntimeTypes.Package,
                "SelectFireProfile",
                fallback);

            WeaponPowerBankState full = WeaponPowerBankState.FullFromProfile(normalRuntime);
            WeaponPowerFireDecision empoweredDecision =
                WeaponPowerBankPolicy.ResolveFire(full, true, true);
            object selectedEmpowered = InvokeStatic(
                RuntimeTypes.Package,
                "SelectFireProfile",
                empoweredDecision);

            Assert.That(
                fallback.Kind,
                Is.EqualTo(WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(selectedFallback, Is.EqualTo(normal));
            Assert.That(empoweredDecision.Kind, Is.EqualTo(WeaponPowerFireDecisionKind.EmpoweredFired));
            Assert.That(selectedEmpowered, Is.EqualTo(empowered));
            Assert.That(
                Get<int>(Get<object>(selectedFallback, "Topology"), "AdditionalTargetCount"),
                Is.EqualTo(3));
            Assert.That(
                Get<int>(Get<object>(selectedEmpowered, "Topology"), "AdditionalTargetCount"),
                Is.EqualTo(3));

            TestContext.WriteLine(
                "power-fallback requested=empowered available=0 selected=normal cap=3; full-bank selected=empowered cap=3");
        }

        [Test]
        public void Restart_FiftyCyclesLeaveNoVisitedOrPreviousResultState()
        {
            object session = Activator.CreateInstance(RuntimeTypes.Session);
            object primary = Target("enemy.arc-restart-primary", 0d, 0d);
            object[] candidates =
            {
                Target("enemy.arc-restart-a", 1d, 0d),
                Target("enemy.arc-restart-b", 2d, 0d),
                Target("enemy.arc-restart-c", 3d, 0d),
                Target("enemy.arc-restart-d", 4d, 0d),
            };
            Array typedCandidates = TypedTargets(candidates);
            Func<StableId, bool> confirm = id => true;

            for (int cycle = 0; cycle < 50; cycle++)
            {
                object result = Invoke(
                    session,
                    "Resolve",
                    primary,
                    typedCandidates,
                    10d,
                    confirm);
                Assert.That(Get<int>(result, "AdditionalHitCount"), Is.EqualTo(3));
                Assert.That(Get<object>(session, "LastResult"), Is.SameAs(result));

                Invoke(session, "Reset");
                Assert.That(Get<object>(session, "LastResult"), Is.Null);
                Assert.That(Get<int>(session, "Generation"), Is.EqualTo(cycle + 1));
            }

            object afterRestart = Invoke(
                session,
                "Resolve",
                primary,
                typedCandidates,
                10d,
                confirm);
            Assert.That(Get<int>(afterRestart, "AdditionalHitCount"), Is.EqualTo(3));
            Assert.That(TargetIds(afterRestart), Is.Unique);
            TestContext.WriteLine(
                "restart cycles=50 generation=50 last-result-cleared=true visited-leak=false");
        }

        private static object BuildOperation(
            WeaponBehaviorPipeline pipeline,
            WeaponRuntimeProfile profile,
            bool empowered,
            string eventId)
        {
            WeaponBehaviorInput input = new WeaponBehaviorInput(
                StableId.Parse(eventId),
                StableId.Parse("weapon.arc-gun"),
                StableId.Parse("weapon-mount.arc-fixture"),
                empowered ? 2L : 1L,
                profile,
                empowered,
                0d,
                0d,
                1d,
                0d,
                1d);
            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(input);

            Assert.That(plan.OperationCount, Is.EqualTo(1));
            Assert.That(plan.FaultCount, Is.Zero);
            return plan.GetOperation(0).Operation;
        }

        private static object Target(string id, double x, double y)
        {
            return InvokeStatic(
                RuntimeTypes.Target,
                "Create",
                StableId.Parse(id),
                x,
                y,
                true,
                true);
        }

        private static Array TypedTargets(object[] targets)
        {
            Array typed = Array.CreateInstance(RuntimeTypes.Target, targets.Length);
            for (int index = 0; index < targets.Length; index++)
            {
                typed.SetValue(targets[index], index);
            }

            return typed;
        }

        private static List<StableId> TargetIds(object result)
        {
            int count = Get<int>(result, "AdditionalHitCount");
            List<StableId> ids = new List<StableId>();
            for (int index = 0; index < count; index++)
            {
                ids.Add((StableId)Invoke(result, "GetAdditionalTargetId", index));
            }

            return ids;
        }

        private static double Coefficient(object fireProfile, int kind)
        {
            IEnumerable coefficients = (IEnumerable)Get<object>(
                fireProfile,
                "NumericCoefficients");
            return coefficients.Cast<object>()
                .Where(item => Convert.ToInt32(Get<object>(item, "Kind")) == kind)
                .Select(item => Get<double>(item, "Value"))
                .Single();
        }

        private static T Get<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return Invoke(method, null, arguments);
        }

        private static object Invoke(object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate => candidate.Name == methodName
                    && candidate.GetParameters().Length == arguments.Length);
            return Invoke(method, instance, arguments);
        }

        private static object Invoke(MethodInfo method, object instance, object[] arguments)
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
            public static readonly Type Package = Find(
                "ShooterMover.ContentPackages.Weapons.ArcGun.ArcGunPackage");
            public static readonly Type Module = Find(
                "ShooterMover.ContentPackages.Weapons.ArcGun.ArcGunBehaviorModule");
            public static readonly Type Session = Find(
                "ShooterMover.ContentPackages.Weapons.ArcGun.ArcGunChainSession");
            public static readonly Type Target = Find(
                "ShooterMover.ContentPackages.Weapons.ArcGun.ArcTargetSnapshot");

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
#endif

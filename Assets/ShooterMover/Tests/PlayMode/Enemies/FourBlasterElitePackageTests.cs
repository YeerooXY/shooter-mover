#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Enemies
{
    public sealed class FourBlasterElitePackageTests
    {
        private const string RuntimePackageTypeName =
            "ShooterMover.ContentPackages.Enemies.FourBlasterElite.FourBlasterElitePackage";
        private const string PackageRoot =
            "Assets/ShooterMover/ContentPackages/Enemies/FourBlasterElite/";
        private const string PackageNotePath = PackageRoot + "PACKAGE.md";
        private const string ReadabilityCapturePath =
            PackageRoot + "BOSS_READABILITY_CAPTURE.md";

        private static readonly StableId ActorId =
            StableId.Parse("actor.four-blaster-elite-test");
        private static readonly StableId DamageSourceId =
            StableId.Parse("actor.player-test");
        private static readonly StableId ExpectedEnemyId =
            StableId.Parse("enemy.four-blaster-elite");
        private static readonly StableId ExpectedWeaponId =
            StableId.Parse("weapon.blaster-machine-gun");
        private static readonly StableId ExpectedOperationKindId =
            StableId.Parse("operation-kind.bounded-projectile-2d");

        [Test]
        public void FourOrigins_AreOrderedBoundedAndUseAcceptedBlasterPlans()
        {
            object descriptor = InvokeStatic(PackageType, "CreateDescriptor");
            Assert.That(
                GetProperty<StableId>(descriptor, "DefinitionId"),
                Is.EqualTo(ExpectedEnemyId));
            Assert.That(GetProperty<object>(descriptor, "Classification").ToString(), Is.EqualTo("Elite"));
            Assert.That(GetProperty<CombatChannel>(descriptor, "DamageChannel"), Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(GetProperty<CombatWeightClass>(descriptor, "WeightClass"), Is.EqualTo(CombatWeightClass.Heavy));

            ContentReference attack = GetProperty<ContentReference>(descriptor, "AttackReference");
            Assert.That(attack.DefinitionId, Is.EqualTo(ExpectedWeaponId));
            Assert.That(attack.ExpectedKind, Is.EqualTo(ContentDefinitionKind.Weapon));

            string capabilities = GetProperty<object>(descriptor, "Capabilities").ToString();
            Assert.That(capabilities, Does.Contain("BlasterProjectile"));
            Assert.That(capabilities, Does.Contain("FourBlasterOrigins"));
            Assert.That(capabilities, Does.Contain("MildBoundedSpread"));
            Assert.That(capabilities, Does.Contain("SafeRecoveryWindow"));
            Assert.That(capabilities, Does.Not.Contain("PhaseTransition"));
            Assert.That(capabilities, Does.Not.Contain("BulletHell"));

            object session = CreateSession();
            List<object> shots = new List<object>();
            shots.AddRange(GetShots(Advance(session, TelegraphSeconds, 0d, 0d, 20d, 0d)));
            shots.AddRange(GetShots(Advance(session, InterOriginSeconds * 3d, 0d, 0d, 20d, 0d)));

            Assert.That(shots.Count, Is.EqualTo(4));
            Assert.That(
                shots.Select(shot => GetProperty<int>(shot, "OriginIndex")),
                Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(
                shots.Select(shot => GetProperty<double>(shot, "SpreadDegrees")),
                Is.EqualTo(new[] { -6d, -2d, 2d, 6d }));
            Assert.That(
                shots.Max(shot => Math.Abs(GetProperty<double>(shot, "SpreadDegrees"))),
                Is.LessThanOrEqualTo(MaximumSpreadDegrees));
            Assert.That(
                shots.Select(shot => GetProperty<StableId>(shot, "OriginId")).Distinct().Count(),
                Is.EqualTo(4));

            StringBuilder trace = new StringBuilder();
            for (int index = 0; index < shots.Count; index++)
            {
                object shot = shots[index];
                WeaponFireExecutionPlan plan =
                    GetProperty<WeaponFireExecutionPlan>(shot, "ExecutionPlan");
                Assert.That(plan.WeaponId, Is.EqualTo(ExpectedWeaponId));
                Assert.That(plan.Input.IsEmpowered, Is.False);
                Assert.That(plan.FaultCount, Is.Zero);
                Assert.That(plan.OperationCount, Is.EqualTo(1));
                Assert.That(
                    plan.GetOperation(0).OperationKindId,
                    Is.EqualTo(ExpectedOperationKindId));
                Assert.That(
                    Math.Sqrt(
                        (plan.Input.DirectionX * plan.Input.DirectionX)
                        + (plan.Input.DirectionY * plan.Input.DirectionY)),
                    Is.EqualTo(1d).Within(0.000000000001d));

                trace.Append("origin=")
                    .Append(GetProperty<int>(shot, "OriginIndex"))
                    .Append(";spread=")
                    .Append(GetProperty<double>(shot, "SpreadDegrees"))
                    .Append(";mount=")
                    .Append(plan.MountId)
                    .Append(";event=")
                    .Append(plan.CombatEventId)
                    .AppendLine();
            }

            TestContext.WriteLine("four-origin-order=0,1,2,3 spread-degrees=-6,-2,2,6 cap=8");
            TestContext.WriteLine(trace.ToString());
        }

        [Test]
        public void Cadence_TelegraphAndGenerousRecoveryRemainDeterministic()
        {
            object session = CreateSession();

            object almostTelegraphed = Advance(
                session,
                TelegraphSeconds - 0.01d,
                0d,
                0d,
                10d,
                0d);
            Assert.That(GetShots(almostTelegraphed), Is.Empty);
            object warning = GetProperty<object>(almostTelegraphed, "WarningCue");
            Assert.That(GetProperty<bool>(warning, "IsVisible"), Is.True);
            Assert.That(GetProperty<double>(warning, "NormalizedProgress"), Is.GreaterThan(0.9d));

            object firstShot = Advance(session, 0.01d, 0d, 0d, 10d, 0d);
            Assert.That(GetShots(firstShot).Length, Is.EqualTo(1));
            Assert.That(GetProperty<object>(firstShot, "Stage").ToString(), Is.EqualTo("Volley"));

            object restOfVolley = Advance(
                session,
                InterOriginSeconds * 3d,
                0d,
                0d,
                10d,
                0d);
            Assert.That(GetShots(restOfVolley).Length, Is.EqualTo(3));
            Assert.That(GetProperty<object>(restOfVolley, "Stage").ToString(), Is.EqualTo("Recovery"));

            object safeWindow = Advance(
                session,
                RecoverySeconds - 0.01d,
                0d,
                0d,
                10d,
                0d);
            Assert.That(GetShots(safeWindow), Is.Empty);
            Assert.That(GetProperty<object>(safeWindow, "Stage").ToString(), Is.EqualTo("Recovery"));
            Assert.That(
                GetProperty<bool>(GetProperty<object>(safeWindow, "WarningCue"), "IsVisible"),
                Is.False);

            object nextTelegraph = Advance(session, 0.01d, 0d, 0d, 10d, 0d);
            Assert.That(GetShots(nextTelegraph), Is.Empty);
            Assert.That(GetProperty<object>(nextTelegraph, "Stage").ToString(), Is.EqualTo("Telegraph"));
            Assert.That(GetProperty<long>(nextTelegraph, "CycleIndex"), Is.EqualTo(1L));
            Assert.That(
                GetProperty<bool>(GetProperty<object>(nextTelegraph, "WarningCue"), "IsVisible"),
                Is.True);
            Assert.That(RecoverySeconds, Is.GreaterThan(TelegraphSeconds));
            Assert.That(RecoverySeconds, Is.GreaterThan(InterOriginSeconds * 3d));

            object replay = Advance(session, TelegraphSeconds, 0d, 0d, 10d, 0d);
            Assert.That(GetShots(replay).Length, Is.EqualTo(1));
            Assert.That(GetProperty<int>(GetShots(replay)[0], "OriginIndex"), Is.Zero);

            TestContext.WriteLine(
                "cadence telegraph=0.75 shots=0.75,0.90,1.05,1.20 recovery=1.50 next-telegraph=2.70 next-shot=3.45");
        }

        [Test]
        public void Completion_EmitsOnceAndStopsEveryOrigin()
        {
            object session = CreateSession();
            EnemyActorStepResult lethal = ApplyDamage(
                session,
                "en008-lethal-1",
                500d,
                0L);
            Assert.That(lethal.State.IsDestroyed, Is.True);
            Assert.That(
                lethal.Notifications.OfType<EnemyEncounterResolutionNotification>().Count(),
                Is.EqualTo(1));
            Assert.That(GetProperty<bool>(session, "CompletionEmitted"), Is.True);
            Assert.That(GetProperty<int>(session, "CompletionCount"), Is.EqualTo(1));
            Assert.That(GetProperty<object>(session, "Stage").ToString(), Is.EqualTo("Complete"));

            EnemyActorStepResult late = ApplyDamage(
                session,
                "en008-late-2",
                5d,
                1L);
            Assert.That(
                late.Notifications.OfType<EnemyEncounterResolutionNotification>().Count(),
                Is.Zero);
            Assert.That(GetProperty<int>(session, "CompletionCount"), Is.EqualTo(1));

            object stopped = Advance(session, 20d, 0d, 0d, 10d, 0d);
            Assert.That(GetShots(stopped), Is.Empty);
            Assert.That(GetProperty<object>(stopped, "Stage").ToString(), Is.EqualTo("Complete"));

            TestContext.WriteLine(
                "completion enemy-resolution-notifications=1 package-completion-count=1 late-shots=0");
        }

        [Test]
        public void DeathAndRestart_RestoreOneHealthModelAndReplayWithoutLeakage()
        {
            object session = CreateSession();
            string expectedTrace = null;

            for (int cycle = 0; cycle < 25; cycle++)
            {
                Assert.That((bool)InvokeInstance(session, "RestartSession"), Is.True);
                EnemyActorState initial = GetProperty<EnemyActorState>(session, "ActorState");
                Assert.That(initial.ActorId, Is.EqualTo(ActorId));
                Assert.That(initial.RoleId, Is.EqualTo(ExpectedEnemyId));
                Assert.That(initial.Health, Is.EqualTo(initial.MaximumHealth));
                Assert.That(initial.IsActive, Is.True);
                Assert.That(GetProperty<object>(session, "Stage").ToString(), Is.EqualTo("Telegraph"));
                Assert.That(GetProperty<long>(session, "CycleIndex"), Is.Zero);
                Assert.That(GetProperty<bool>(session, "CompletionEmitted"), Is.False);
                Assert.That(GetProperty<int>(session, "CompletionCount"), Is.Zero);

                string trace = RunOneVolleyTrace(session);
                if (expectedTrace == null)
                {
                    expectedTrace = trace;
                }
                else
                {
                    Assert.That(trace, Is.EqualTo(expectedTrace), "restart cycle " + cycle);
                }

                EnemyActorStepResult damaged = ApplyDamage(
                    session,
                    "en008-cycle-" + cycle + "-damage",
                    20d,
                    0L);
                Assert.That(damaged.State.Health, Is.EqualTo(140d));
                EnemyActorStepResult destroyed = ApplyDamage(
                    session,
                    "en008-cycle-" + cycle + "-lethal",
                    500d,
                    1L);
                Assert.That(destroyed.State.IsDestroyed, Is.True);
                Assert.That(GetProperty<int>(session, "CompletionCount"), Is.EqualTo(1));
            }

            Assert.That(expectedTrace, Is.Not.Null.And.Not.Empty);
            TestContext.WriteLine(
                "restart-cycles=25 health-models-per-session=1 deterministic-volley=true stale-completion=false");
            TestContext.WriteLine(expectedTrace);
        }

        [Test]
        public void Warning_IsColorIndependentAndReducedEffectsReadable()
        {
            object session = CreateSession();
            object warning = GetProperty<object>(session, "WarningCue");

            Assert.That(GetProperty<bool>(warning, "IsVisible"), Is.True);
            Assert.That(GetProperty<int>(warning, "OriginMarkerCount"), Is.EqualTo(4));
            Assert.That(GetProperty<string>(warning, "ShapeToken"), Is.EqualTo("four-spoke-dashed-outline"));
            Assert.That(
                GetProperty<string>(warning, "ReducedEffectsToken"),
                Is.EqualTo("four-static-spokes-with-countdown-bars"));
            Assert.That(GetProperty<bool>(warning, "UsesColorOnly"), Is.False);
            Assert.That(GetProperty<bool>(warning, "IsReducedEffectsReadable"), Is.True);
            Assert.That(
                GetProperty<double>(warning, "MaximumSpreadDegrees"),
                Is.EqualTo(MaximumSpreadDegrees));

            string snapshot = (string)InvokeStatic(PackageType, "CreateReadabilitySnapshot");
            Assert.That(snapshot, Does.Contain("origin_markers=4"));
            Assert.That(snapshot, Does.Contain("color_only=false"));
            Assert.That(snapshot, Does.Contain("reduced_effects=four-static-spokes-with-countdown-bars"));

            string packageNote = ReadProjectFile(PackageNotePath);
            string capture = ReadProjectFile(ReadabilityCapturePath);
            Assert.That(packageNote, Does.Contain("easy first boss"));
            Assert.That(packageNote, Does.Contain("Prototype Overseer"));
            Assert.That(capture, Does.Contain("shape and count"));
            Assert.That(capture, Does.Contain("playable screenshot remains a manual acceptance gate"));

            TestContext.WriteLine(snapshot);
        }

        private static object CreateSession()
        {
            return InvokeStatic(PackageType, "CreateSession", ActorId);
        }

        private static object Advance(
            object session,
            double deltaTimeSeconds,
            double centerX,
            double centerY,
            double targetX,
            double targetY)
        {
            return InvokeInstance(
                session,
                "Advance",
                deltaTimeSeconds,
                centerX,
                centerY,
                targetX,
                targetY);
        }

        private static EnemyActorStepResult ApplyDamage(
            object session,
            string eventValue,
            double amount,
            long order)
        {
            return (EnemyActorStepResult)InvokeInstance(
                session,
                "ApplyDamage",
                StableId.Create("event", eventValue),
                DamageSourceId,
                CombatChannel.Kinetic,
                amount,
                order);
        }

        private static string RunOneVolleyTrace(object session)
        {
            List<object> shots = new List<object>();
            shots.AddRange(GetShots(Advance(session, TelegraphSeconds, 2d, -1d, 12d, 4d)));
            shots.AddRange(
                GetShots(
                    Advance(
                        session,
                        InterOriginSeconds * 3d,
                        2d,
                        -1d,
                        12d,
                        4d)));

            StringBuilder trace = new StringBuilder();
            foreach (object shot in shots)
            {
                WeaponFireExecutionPlan plan =
                    GetProperty<WeaponFireExecutionPlan>(shot, "ExecutionPlan");
                trace.Append(GetProperty<int>(shot, "OriginIndex"))
                    .Append('|')
                    .Append(GetProperty<double>(shot, "SpreadDegrees").ToString("R"))
                    .Append('|')
                    .Append(plan.Input.OriginX.ToString("R"))
                    .Append(',')
                    .Append(plan.Input.OriginY.ToString("R"))
                    .Append('|')
                    .Append(plan.Input.DirectionX.ToString("R"))
                    .Append(',')
                    .Append(plan.Input.DirectionY.ToString("R"))
                    .Append('|')
                    .Append(plan.DeterministicIdentity)
                    .AppendLine();
            }

            return trace.ToString();
        }

        private static object[] GetShots(object cadenceResult)
        {
            IEnumerable values = (IEnumerable)GetProperty<object>(cadenceResult, "Shots");
            return values.Cast<object>().ToArray();
        }

        private static Type PackageType
        {
            get
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(RuntimePackageTypeName, false))
                    .FirstOrDefault(candidate => candidate != null);
                Assert.That(type, Is.Not.Null, RuntimePackageTypeName + " must compile into the player domain.");
                return type;
            }
        }

        private static double TelegraphSeconds
        {
            get { return GetConstant<double>(PackageType, "TelegraphSeconds"); }
        }

        private static double InterOriginSeconds
        {
            get { return GetConstant<double>(PackageType, "InterOriginSeconds"); }
        }

        private static double RecoverySeconds
        {
            get { return GetConstant<double>(PackageType, "RecoverySeconds"); }
        }

        private static double MaximumSpreadDegrees
        {
            get { return GetConstant<double>(PackageType, "MaximumSpreadDegrees"); }
        }

        private static T GetConstant<T>(Type type, string fieldName)
        {
            FieldInfo field = type.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, type.FullName + "." + fieldName);
            return (T)field.GetRawConstantValue();
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            Assert.That(property, Is.Not.Null, target.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(target, null);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = ResolveMethod(
                type,
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                arguments.Length);
            return method.Invoke(null, arguments);
        }

        private static object InvokeInstance(
            object target,
            string methodName,
            params object[] arguments)
        {
            Assert.That(target, Is.Not.Null);
            MethodInfo method = ResolveMethod(
                target.GetType(),
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                arguments.Length);
            return method.Invoke(target, arguments);
        }

        private static MethodInfo ResolveMethod(
            Type type,
            string methodName,
            BindingFlags flags,
            int argumentCount)
        {
            MethodInfo method = type.GetMethods(flags)
                .SingleOrDefault(
                    candidate => candidate.Name == methodName
                        && candidate.GetParameters().Length == argumentCount);
            Assert.That(method, Is.Not.Null, type.FullName + "." + methodName);
            return method;
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }
    }
}
#endif

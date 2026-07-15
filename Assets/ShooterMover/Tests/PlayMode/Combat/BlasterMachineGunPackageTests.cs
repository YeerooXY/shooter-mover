#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class BlasterMachineGunPackageTests
    {
        private const string WeaponManifestPath =
            "Assets/ShooterMover/ContentPackages/Weapons/BlasterMachineGun/blaster-machine-gun.content-descriptor.json";
        private const string ModuleManifestPath =
            "Assets/ShooterMover/ContentPackages/Weapons/BlasterMachineGun/automatic-projectile.content-descriptor.json";
        private const string ManualNotePath =
            "Assets/ShooterMover/ContentPackages/Weapons/BlasterMachineGun/README.md";

        private static readonly StableId ExpectedWeaponId =
            StableId.Parse("weapon.blaster-machine-gun");
        private static readonly StableId ExpectedModuleId =
            StableId.Parse("module.weapon-automatic-projectile");
        private static readonly StableId ExpectedOperationKindId =
            StableId.Parse("operation-kind.bounded-projectile-2d");

        [Test]
        public void Cadence_HeldFireProducesStraightforwardIndependentAutomaticCycles()
        {
            WeaponRuntimeProfile[] profiles = CreateProfiles();
            FourMountCombatState state = FourMountCombatState.Initial(
                profiles,
                new[] { 0d, 0d, 0d, 0d });
            FourMountCombatStepper stepper = CreateStepper();

            FourMountCombatStepResult first = stepper.Step(
                state,
                CreateInput(0L, 0d, true, false, profiles));
            state = first.State;
            FourMountCombatStepResult halfCadence = stepper.Step(
                state,
                CreateInput(1L, 0.05d, true, false, profiles));
            state = halfCadence.State;
            FourMountCombatStepResult fullCadence = stepper.Step(
                state,
                CreateInput(2L, 0.05d, true, false, profiles));
            state = fullCadence.State;

            for (int index = 0; index < FourMountCombatState.MountCount; index++)
            {
                Assert.That(first.GetLaneByStableIndex(index).ShotsFired, Is.EqualTo(1));
                Assert.That(first.GetLaneByStableIndex(index).ExecutionPlan, Is.Not.Null);
                Assert.That(first.GetLaneByStableIndex(index).ExecutionPlan.OperationCount, Is.EqualTo(1));
                Assert.That(halfCadence.GetLaneByStableIndex(index).ShotsFired, Is.Zero);
                Assert.That(halfCadence.GetLaneByStableIndex(index).ExecutionPlan, Is.Null);
                Assert.That(fullCadence.GetLaneByStableIndex(index).ShotsFired, Is.EqualTo(1));
                Assert.That(fullCadence.GetLaneByStableIndex(index).ExecutionPlan.OperationCount, Is.EqualTo(1));
                Assert.That(
                    state.GetMountByStableIndex(index).TotalShotsFired,
                    Is.EqualTo(2L));
                Assert.That(
                    state.GetMountByStableIndex(index).TotalCyclesStarted,
                    Is.EqualTo(2L));
            }

            TestContext.WriteLine(
                "cadence held=true cadence-seconds=0.1 samples=0,0.05,0.10 shots-per-slot=2 topology=one-projectile");
        }

        [Test]
        public void Aim_ExecutionPlansPreserveTheSharedResolvedPerMountDirections()
        {
            WeaponRuntimeProfile[] profiles = CreateProfiles();
            FourMountCombatState state = FourMountCombatState.Initial(
                profiles,
                new[] { 0d, 0d, 0d, 0d });
            FourMountCombatStepper stepper = CreateStepper();
            AimVector2 aimPoint = new AimVector2(20d, 6d);

            FourMountCombatStepResult result = stepper.Step(
                state,
                CreateInput(
                    12L,
                    0d,
                    true,
                    false,
                    profiles,
                    AimVector2.UnitX,
                    aimPoint));

            for (int index = 0; index < FourMountCombatState.MountCount; index++)
            {
                SharedAimSolution aim = result.AimSolution.GetByStableIndex(index);
                WeaponFireExecutionPlan plan =
                    result.GetLaneByStableIndex(index).ExecutionPlan;

                Assert.That(plan, Is.Not.Null);
                Assert.That(plan.Input.OriginX, Is.EqualTo(aim.Origin.X).Within(1e-12d));
                Assert.That(plan.Input.OriginY, Is.EqualTo(aim.Origin.Y).Within(1e-12d));
                Assert.That(plan.Input.DirectionX, Is.EqualTo(aim.Direction.X).Within(1e-12d));
                Assert.That(plan.Input.DirectionY, Is.EqualTo(aim.Direction.Y).Within(1e-12d));
                Assert.That(plan.OperationCount, Is.EqualTo(1));
                Assert.That(
                    plan.GetOperation(0).OperationKindId,
                    Is.EqualTo(ExpectedOperationKindId));
            }

            TestContext.WriteLine(
                "aim shared-point=(20,6) slots=4 plan-directions=resolved-per-mount operation-count=1");
        }

        [Test]
        public void UnlimitedNormalFire_ZeroPowerNeverConsumesAmmunitionOrSuppressesHeldFire()
        {
            object descriptor = CreateDescriptor();
            object normalFire = GetProperty<object>(descriptor, "NormalFire");
            Assert.That(
                GetProperty<bool>(normalFire, "ConsumesConsumableAmmunition"),
                Is.False);
            Assert.That(WeaponRuntimeProfile.NormalFireConsumesConsumable, Is.False);

            WeaponRuntimeProfile[] profiles = CreateProfiles();
            FourMountCombatState state = FourMountCombatState.Initial(
                profiles,
                new[] { 0d, 0d, 0d, 0d });
            FourMountCombatStepper stepper = CreateStepper();
            int laneZeroPlanCount = 0;

            for (int step = 0; step < 50; step++)
            {
                FourMountCombatStepResult result = stepper.Step(
                    state,
                    CreateInput(step, step == 0 ? 0d : 0.05d, true, false, profiles));
                state = result.State;
                FourMountCombatLaneResult lane = result.GetLaneByStableIndex(0);
                if (lane.ExecutionPlan != null)
                {
                    laneZeroPlanCount++;
                    Assert.That(
                        lane.PowerDecision.Kind,
                        Is.Not.EqualTo(WeaponPowerFireDecisionKind.EmpoweredFired));
                    Assert.That(lane.PowerDecision.SpentUnits, Is.Zero);
                }
            }

            long shots = state.GetMountByStableIndex(0).TotalShotsFired;
            Assert.That(shots, Is.GreaterThan(BlasterMachineGunConstants.PowerCapacity));
            Assert.That(shots, Is.EqualTo(laneZeroPlanCount));
            Assert.That(state.GetPowerBankByStableIndex(0).AvailableUnits, Is.Zero);
            Assert.That(state.GetMountByStableIndex(0).IsFaulted, Is.False);

            TestContext.WriteLine(
                "unlimited-normal held-steps=50 shots=" + shots
                + " initial-power=0 final-power=0 consumable-ammunition=false");
        }

        [Test]
        public void IndependentPowerFallback_EmptyLaneImmediatelyUsesIdenticalNormalTopology()
        {
            WeaponRuntimeProfile[] profiles = CreateProfiles();
            FourMountCombatState state = FourMountCombatState.Initial(
                profiles,
                new[] { 0d, 4d, 2d, 1d });
            FourMountCombatStepResult result = CreateStepper().Step(
                state,
                CreateInput(7L, 0d, true, true, profiles));

            FourMountCombatLaneResult emptyLane = result.GetLaneByStableIndex(0);
            FourMountCombatLaneResult poweredLane = result.GetLaneByStableIndex(1);

            Assert.That(
                emptyLane.PowerDecision.Kind,
                Is.EqualTo(WeaponPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(emptyLane.PowerDecision.FiresNormally, Is.True);
            Assert.That(emptyLane.PowerDecision.FiresEmpowered, Is.False);
            Assert.That(emptyLane.PowerDecision.SpentUnits, Is.Zero);
            Assert.That(emptyLane.ExecutionPlan.Input.IsEmpowered, Is.False);

            Assert.That(
                poweredLane.PowerDecision.Kind,
                Is.EqualTo(WeaponPowerFireDecisionKind.EmpoweredFired));
            Assert.That(poweredLane.PowerDecision.FiresEmpowered, Is.True);
            Assert.That(poweredLane.PowerDecision.SpentUnits, Is.EqualTo(1d));
            Assert.That(poweredLane.ExecutionPlan.Input.IsEmpowered, Is.True);

            AssertSameSingleProjectileTopology(
                emptyLane.ExecutionPlan,
                poweredLane.ExecutionPlan);

            object normalOperation = emptyLane.ExecutionPlan.GetOperation(0).Operation;
            object empoweredOperation = poweredLane.ExecutionPlan.GetOperation(0).Operation;
            Assert.That(
                GetProperty<double>(normalOperation, "ProjectileSpeed"),
                Is.EqualTo(20d));
            Assert.That(
                GetProperty<double>(empoweredOperation, "ProjectileSpeed"),
                Is.EqualTo(24d));
            Assert.That(
                result.State.GetPowerBankByStableIndex(0).AvailableUnits,
                Is.Zero);
            Assert.That(
                result.State.GetPowerBankByStableIndex(1).AvailableUnits,
                Is.EqualTo(3d));

            object descriptor = CreateDescriptor();
            Assert.That(
                GetProperty<object>(GetProperty<object>(descriptor, "NormalFire"), "Topology")
                    .ToString(),
                Is.EqualTo(
                    GetProperty<object>(GetProperty<object>(descriptor, "EmpoweredFire"), "Topology")
                        .ToString()));

            TestContext.WriteLine(
                "power-fallback slot1=normal-fallback spent=0 slot2=empowered spent=1 banks=0,3 topology=one-projectile-identical");
        }

        [Test]
        public void Restart_TwentyFiveFreshSessionsReplayWithoutPackageStateLeakage()
        {
            string expected = RunDeterministicSession();
            for (int cycle = 0; cycle < 25; cycle++)
            {
                Assert.That(
                    RunDeterministicSession(),
                    Is.EqualTo(expected),
                    "restart cycle " + cycle);
            }

            TestContext.WriteLine(
                "restart cycles=25 deterministic=true leaked-package-state=false trace-fingerprint="
                + DeterministicTextHash(expected));
        }

        [Test]
        public void StableIdentity_DescriptorManifestsAndNumericOnlyEmpowermentAreCanonical()
        {
            object first = CreateDescriptor();
            object second = CreateDescriptor();
            StableId definitionId = GetProperty<StableId>(first, "DefinitionId");

            Assert.That(definitionId, Is.EqualTo(ExpectedWeaponId));
            Assert.That(GetProperty<bool>(first, "IsDefaultStartingWeapon"), Is.True);
            Assert.That(first.ToString(), Is.EqualTo(second.ToString()));

            object normalFire = GetProperty<object>(first, "NormalFire");
            object empoweredFire = GetProperty<object>(first, "EmpoweredFire");
            WeaponRuntimeProfile normalRuntime =
                GetProperty<WeaponRuntimeProfile>(normalFire, "RuntimeProfile");
            WeaponRuntimeProfile empoweredRuntime =
                GetProperty<WeaponRuntimeProfile>(empoweredFire, "RuntimeProfile");

            Assert.That(normalRuntime.BehaviorModuleCount, Is.EqualTo(1));
            Assert.That(empoweredRuntime.BehaviorModuleCount, Is.EqualTo(1));
            Assert.That(normalRuntime.GetBehaviorModuleId(0), Is.EqualTo(ExpectedModuleId));
            Assert.That(empoweredRuntime.GetBehaviorModuleId(0), Is.EqualTo(ExpectedModuleId));
            Assert.That(normalRuntime.CadenceSeconds, Is.EqualTo(0.1d));
            Assert.That(empoweredRuntime.CadenceSeconds, Is.EqualTo(0.1d));
            Assert.That(normalRuntime.BurstShotCount, Is.EqualTo(1));
            Assert.That(empoweredRuntime.BurstShotCount, Is.EqualTo(1));
            Assert.That(normalRuntime.HasIndependentPowerBank, Is.True);
            Assert.That(empoweredRuntime.HasIndependentPowerBank, Is.True);

            Dictionary<int, double> normalCoefficients = CoefficientsByKind(normalFire);
            Dictionary<int, double> empoweredCoefficients = CoefficientsByKind(empoweredFire);
            Assert.That(
                normalCoefficients.Keys.OrderBy(value => value),
                Is.EqualTo(empoweredCoefficients.Keys.OrderBy(value => value)));
            Assert.That(
                normalCoefficients.Any(
                    pair => empoweredCoefficients[pair.Key] != pair.Value),
                Is.True,
                "Empowerment must change authored numbers without changing coefficient kinds.");
            Assert.That(
                GetProperty<object>(normalFire, "Topology").ToString(),
                Is.EqualTo(GetProperty<object>(empoweredFire, "Topology").ToString()));

            string weaponManifest = ReadProjectFile(WeaponManifestPath);
            string moduleManifest = ReadProjectFile(ModuleManifestPath);
            string manualNote = ReadProjectFile(ManualNotePath);
            Assert.That(weaponManifest, Does.Contain("\"definition_id\": \"weapon.blaster-machine-gun\""));
            Assert.That(weaponManifest, Does.Contain("\"definition_id\": \"module.weapon-automatic-projectile\""));
            Assert.That(moduleManifest, Does.Contain("\"definition_kind\": \"shared-module\""));
            Assert.That(moduleManifest, Does.Contain("\"references\": [  ]"));
            Assert.That(manualNote, Does.Contain("uncomplicated Stage 1 reference weapon"));
            Assert.That(manualNote, Does.Contain("same one-projectile topology"));

            string packageSnapshot = (string)InvokeStatic(
                RuntimeTypes.Package,
                "CreateManifestSnapshot");
            Assert.That(packageSnapshot, Does.Contain("default_starting_weapon=true"));
            Assert.That(packageSnapshot, Does.Contain("definition_id=weapon.blaster-machine-gun"));
            Assert.That(packageSnapshot, Does.Contain("definition_id=module.weapon-automatic-projectile"));

            TestContext.WriteLine(
                "manifest-snapshot weapon=" + Compact(weaponManifest)
                + " module=" + Compact(moduleManifest));
            TestContext.WriteLine(
                "manual-baseline steady-single-projectile=true alternate-fire=false randomized=false bespoke-target-selection=false final-art=false");
        }

        private static void AssertSameSingleProjectileTopology(
            WeaponFireExecutionPlan normal,
            WeaponFireExecutionPlan empowered)
        {
            Assert.That(normal, Is.Not.Null);
            Assert.That(empowered, Is.Not.Null);
            Assert.That(normal.FaultCount, Is.Zero);
            Assert.That(empowered.FaultCount, Is.Zero);
            Assert.That(normal.ModuleExecutionCount, Is.EqualTo(1));
            Assert.That(empowered.ModuleExecutionCount, Is.EqualTo(1));
            Assert.That(normal.OperationCount, Is.EqualTo(1));
            Assert.That(empowered.OperationCount, Is.EqualTo(1));
            Assert.That(
                normal.GetModuleExecution(0).ModuleId,
                Is.EqualTo(empowered.GetModuleExecution(0).ModuleId));
            Assert.That(
                normal.GetOperation(0).SourceModuleId,
                Is.EqualTo(empowered.GetOperation(0).SourceModuleId));
            Assert.That(
                normal.GetOperation(0).OperationKindId,
                Is.EqualTo(empowered.GetOperation(0).OperationKindId));
            Assert.That(
                normal.GetOperation(0).Operation.GetType(),
                Is.EqualTo(empowered.GetOperation(0).Operation.GetType()));
            Assert.That(
                GetProperty<CombatChannel>(normal.GetOperation(0).Operation, "Channel"),
                Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(
                GetProperty<CombatChannel>(empowered.GetOperation(0).Operation, "Channel"),
                Is.EqualTo(CombatChannel.Kinetic));
        }

        private static string RunDeterministicSession()
        {
            WeaponRuntimeProfile[] profiles = CreateProfiles();
            FourMountCombatState state = FourMountCombatState.Initial(
                profiles,
                new[] { 2d, 0d, 4d, 1d });
            FourMountCombatStepper stepper = CreateStepper();
            StringBuilder trace = new StringBuilder();

            for (int step = 0; step < 12; step++)
            {
                FourMountCombatStepResult result = stepper.Step(
                    state,
                    CreateInput(
                        step,
                        0.05d,
                        true,
                        true,
                        profiles,
                        new AimVector2(1d, 0.25d),
                        new AimVector2(30d, 8d)));
                state = result.State;
                trace.Append("step=").Append(step).Append('\n')
                    .Append(result.ToTimelineRow()).Append('\n')
                    .Append(state.ToTraceString()).Append('\n');
                for (int index = 0; index < FourMountCombatState.MountCount; index++)
                {
                    WeaponFireExecutionPlan plan =
                        result.GetLaneByStableIndex(index).ExecutionPlan;
                    trace.Append("slot=").Append(index + 1).Append(";plan=")
                        .Append(plan == null ? "none" : plan.DeterministicIdentity.ToString())
                        .Append('\n');
                }
            }

            return trace.ToString();
        }

        private static FourMountCombatStepper CreateStepper()
        {
            IWeaponBehaviorModule module = (IWeaponBehaviorModule)InvokeStatic(
                RuntimeTypes.Package,
                "CreateBehaviorModule");
            return new FourMountCombatStepper(
                new FourMountAimResolver(),
                new WeaponBehaviorPipeline(new[] { module }));
        }

        private static FourMountCombatStepInput CreateInput(
            long simulationStep,
            double elapsedSeconds,
            bool fireRequested,
            bool empoweredRequested,
            WeaponRuntimeProfile[] profiles,
            AimVector2? sharedAimIntent = null,
            AimVector2? sharedAimPoint = null)
        {
            return new FourMountCombatStepInput(
                simulationStep,
                elapsedSeconds,
                fireRequested,
                empoweredRequested,
                sharedAimIntent ?? AimVector2.UnitX,
                sharedAimPoint ?? new AimVector2(20d, 0d),
                profiles,
                new[]
                {
                    ExpectedWeaponId,
                    ExpectedWeaponId,
                    ExpectedWeaponId,
                    ExpectedWeaponId,
                },
                new[]
                {
                    StableId.Parse("weapon-mount.blaster-one"),
                    StableId.Parse("weapon-mount.blaster-two"),
                    StableId.Parse("weapon-mount.blaster-three"),
                    StableId.Parse("weapon-mount.blaster-four"),
                },
                new[]
                {
                    new WeaponMountOrigin(1, new AimVector2(-0.6d, 0.4d)),
                    new WeaponMountOrigin(2, new AimVector2(0.6d, 0.4d)),
                    new WeaponMountOrigin(3, new AimVector2(-0.6d, -0.4d)),
                    new WeaponMountOrigin(4, new AimVector2(0.6d, -0.4d)),
                });
        }

        private static WeaponRuntimeProfile[] CreateProfiles()
        {
            WeaponRuntimeProfile normal = (WeaponRuntimeProfile)InvokeStatic(
                RuntimeTypes.Package,
                "GetNormalRuntimeProfile");
            return new[] { normal, normal, normal, normal };
        }

        private static object CreateDescriptor()
        {
            return InvokeStatic(RuntimeTypes.Package, "CreateDescriptor");
        }

        private static Dictionary<int, double> CoefficientsByKind(object fireProfile)
        {
            object value = GetProperty<object>(fireProfile, "NumericCoefficients");
            Dictionary<int, double> result = new Dictionary<int, double>();
            foreach (object coefficient in (System.Collections.IEnumerable)value)
            {
                int kind = Convert.ToInt32(GetProperty<object>(coefficient, "Kind"));
                result.Add(kind, GetProperty<double>(coefficient, "Value"));
            }

            return result;
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            Assert.That(instance, Is.Not.Null, propertyName);
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, instance.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static object InvokeStatic(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null, type.FullName + "." + methodName);
            try
            {
                return method.Invoke(null, null);
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

        private static string ReadProjectFile(string projectPath)
        {
            string root = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string localPath = projectPath.Replace('/', Path.DirectorySeparatorChar);
            return File.ReadAllText(Path.Combine(root, localPath));
        }

        private static string Compact(string text)
        {
            return string.Join(
                " ",
                text.Split(
                    new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries));
        }

        private static string DeterministicTextHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }

                return hash.ToString("x8");
            }
        }

        private static class BlasterMachineGunConstants
        {
            public const long PowerCapacity = 4L;
        }

        private static class RuntimeTypes
        {
            public static readonly Type Package = Type.GetType(
                "ShooterMover.ContentPackages.Weapons.BlasterMachineGun.BlasterMachineGunPackage, Assembly-CSharp",
                true);
        }
    }
}
#endif

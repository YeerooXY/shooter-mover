using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class WeaponBehaviorPipelineTests
    {
        private static readonly StableId AutomaticModule = StableId.Parse("behavior.automatic");
        private static readonly StableId ProjectileModule = StableId.Parse("behavior.projectile");
        private static readonly StableId SpreadModule = StableId.Parse("behavior.spread");
        private static readonly StableId FaultingModule = StableId.Parse("behavior.faulting");
        private static readonly StableId EmptyModule = StableId.Parse("behavior.empty");
        private static readonly StableId MissingModule = StableId.Parse("behavior.missing");
        private static readonly StableId OperationKind = StableId.Parse("operation-kind.synthetic");

        [Test]
        public void BuildExecutionPlan_UsesProfileOrderAndMapsToExistingCombatContracts()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                SpreadModule,
                ProjectileModule,
                AutomaticModule);

            SyntheticModule automatic = Module(
                AutomaticModule,
                Operation("operation.automatic"));
            SyntheticModule projectile = Module(
                ProjectileModule,
                Operation("operation.projectile"));
            SyntheticModule spread = Module(
                SpreadModule,
                Operation("operation.spread"));

            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[]
                {
                    automatic,
                    spread,
                    projectile,
                });

            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(BuildInput(profile));

            Assert.That(plan.ModuleExecutionCount, Is.EqualTo(3));
            Assert.That(plan.GetModuleExecution(0).ModuleId, Is.EqualTo(SpreadModule));
            Assert.That(plan.GetModuleExecution(1).ModuleId, Is.EqualTo(ProjectileModule));
            Assert.That(plan.GetModuleExecution(2).ModuleId, Is.EqualTo(AutomaticModule));
            Assert.That(plan.OperationCount, Is.EqualTo(3));
            Assert.That(plan.GetOperation(0).SourceModuleId, Is.EqualTo(SpreadModule));
            Assert.That(plan.GetOperation(0).OperationId, Is.EqualTo(StableId.Parse("operation.spread")));
            Assert.That(plan.GetOperation(1).SourceModuleId, Is.EqualTo(ProjectileModule));
            Assert.That(plan.GetOperation(2).SourceModuleId, Is.EqualTo(AutomaticModule));

            WeaponMountFireResult contractResult = new WeaponMountFireResult(
                WeaponMountSlot.MountOne,
                plan.WeaponId,
                WeaponMountFireResultKind.EmpoweredFired,
                plan.CombatEventId,
                CombatChannel.Kinetic);

            Assert.That(contractResult.CombatEventId, Is.EqualTo(plan.CombatEventId));
            Assert.That(contractResult.WeaponId, Is.EqualTo(plan.WeaponId));
            Assert.That(contractResult.Kind, Is.EqualTo(WeaponMountFireResultKind.EmpoweredFired));
        }

        [Test]
        public void Constructor_RejectsDuplicateOrMissingModuleIds()
        {
            SyntheticModule first = Module(
                AutomaticModule,
                Operation("operation.first"));
            SyntheticModule duplicate = Module(
                AutomaticModule,
                Operation("operation.second"));
            SyntheticModule missingId = new SyntheticModule(
                null,
                input => new WeaponBehaviorModulePlan(
                    AutomaticModule,
                    Operation("operation.unreachable")));

            Assert.Throws<ArgumentException>(
                () => new WeaponBehaviorPipeline(
                    new IWeaponBehaviorModule[] { first, duplicate }));
            Assert.Throws<ArgumentException>(
                () => new WeaponBehaviorPipeline(
                    new IWeaponBehaviorModule[] { missingId }));
            Assert.Throws<ArgumentException>(
                () => new WeaponBehaviorPipeline(
                    new IWeaponBehaviorModule[] { first, null }));
        }

        [Test]
        public void BuildExecutionPlan_RejectsUnknownModuleBeforeInvokingAnyModule()
        {
            WeaponRuntimeProfile profile = BuildProfile(AutomaticModule, MissingModule);
            SyntheticModule automatic = Module(
                AutomaticModule,
                Operation("operation.automatic"));
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new[] { automatic });

            Assert.Throws<InvalidOperationException>(
                () => pipeline.BuildExecutionPlan(BuildInput(profile)));
            Assert.That(automatic.InvocationCount, Is.Zero);
        }

        [Test]
        public void BuildExecutionPlan_IsBoundedAndFaultsOnlyTheOverflowingModule()
        {
            List<StableId> moduleIds = new List<StableId>();
            List<IWeaponBehaviorModule> modules = new List<IWeaponBehaviorModule>();

            for (int moduleIndex = 0; moduleIndex < 4; moduleIndex++)
            {
                StableId moduleId = StableId.Parse("behavior.limit-" + moduleIndex);
                moduleIds.Add(moduleId);
                IWeaponFireExecutionOperation[] operations = BuildOperations(
                    "limit-" + moduleIndex,
                    WeaponBehaviorModulePlan.MaximumOperationCount);
                modules.Add(Module(moduleId, operations));
            }

            StableId overflowingModuleId = StableId.Parse("behavior.limit-overflow");
            moduleIds.Add(overflowingModuleId);
            modules.Add(
                Module(
                    overflowingModuleId,
                    Operation("operation.limit-overflow")));

            moduleIds.Add(EmptyModule);
            SyntheticModule empty = Module(EmptyModule);
            modules.Add(empty);

            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(modules);
            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(
                BuildInput(BuildProfile(moduleIds.ToArray())));

            Assert.That(
                plan.OperationCount,
                Is.EqualTo(WeaponFireExecutionPlan.MaximumOperationCount));
            for (int index = 0; index < 4; index++)
            {
                Assert.That(
                    plan.GetModuleExecution(index).Status,
                    Is.EqualTo(WeaponBehaviorModuleExecutionStatus.Succeeded));
                Assert.That(
                    plan.GetModuleExecution(index).OperationCount,
                    Is.EqualTo(WeaponBehaviorModulePlan.MaximumOperationCount));
            }

            Assert.That(
                plan.GetModuleExecution(4).Status,
                Is.EqualTo(WeaponBehaviorModuleExecutionStatus.Faulted));
            Assert.That(
                plan.GetModuleExecution(4).FaultKind,
                Is.EqualTo(WeaponBehaviorModuleFaultKind.PlanLimitExceeded));
            Assert.That(
                plan.GetModuleExecution(5).Status,
                Is.EqualTo(WeaponBehaviorModuleExecutionStatus.Empty));
            Assert.That(empty.InvocationCount, Is.EqualTo(1));
            Assert.That(plan.FaultCount, Is.EqualTo(1));
        }

        [Test]
        public void BuildExecutionPlan_DeterministicReplayProducesIdenticalPlanAndLog()
        {
            WeaponRuntimeProfile profile = BuildProfile(AutomaticModule, ProjectileModule);
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[]
                {
                    Module(
                        AutomaticModule,
                        Operation("operation.auto-primary"),
                        Operation("operation.auto-followup")),
                    Module(
                        ProjectileModule,
                        Operation("operation.projectile")),
                });
            WeaponBehaviorInput input = BuildInput(profile);

            WeaponFireExecutionPlan first = pipeline.BuildExecutionPlan(input);
            WeaponFireExecutionPlan second = pipeline.BuildExecutionPlan(input);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.DeterministicIdentity, Is.EqualTo(first.DeterministicIdentity));
            Assert.That(second.ToCanonicalString(), Is.EqualTo(first.ToCanonicalString()));
            Assert.That(first.Fingerprint, Does.StartWith("sha256:"));
            Assert.That(
                first.DeterministicIdentity.Namespace,
                Is.EqualTo(WeaponFireExecutionPlan.DeterministicIdentityNamespace));

            TestContext.WriteLine(first.ToCanonicalString());
            TestContext.WriteLine("fingerprint=" + first.Fingerprint);
            TestContext.WriteLine("identity=" + first.DeterministicIdentity);
        }

        [Test]
        public void BuildExecutionPlan_IsolatesThrowingModuleAndContinuesInOrder()
        {
            WeaponRuntimeProfile profile = BuildProfile(
                AutomaticModule,
                FaultingModule,
                ProjectileModule);
            SyntheticModule later = Module(
                ProjectileModule,
                Operation("operation.after-fault"));
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[]
                {
                    Module(
                        AutomaticModule,
                        Operation("operation.before-fault")),
                    new SyntheticModule(
                        FaultingModule,
                        input => throw new InvalidOperationException("nondeterministic detail is not recorded")),
                    later,
                });

            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(BuildInput(profile));

            Assert.That(plan.OperationCount, Is.EqualTo(2));
            Assert.That(plan.GetOperation(0).OperationId, Is.EqualTo(StableId.Parse("operation.before-fault")));
            Assert.That(plan.GetOperation(1).OperationId, Is.EqualTo(StableId.Parse("operation.after-fault")));
            Assert.That(
                plan.GetModuleExecution(1).Status,
                Is.EqualTo(WeaponBehaviorModuleExecutionStatus.Faulted));
            Assert.That(
                plan.GetModuleExecution(1).FaultKind,
                Is.EqualTo(WeaponBehaviorModuleFaultKind.ModuleException));
            Assert.That(plan.GetModuleExecution(1).OperationCount, Is.Zero);
            Assert.That(later.InvocationCount, Is.EqualTo(1));
            Assert.That(plan.FaultCount, Is.EqualTo(1));
        }

        [Test]
        public void BuildExecutionPlan_DuplicateOperationIdFaultsAtomicModuleContribution()
        {
            StableId laterModuleId = StableId.Parse("behavior.later");
            WeaponRuntimeProfile profile = BuildProfile(
                AutomaticModule,
                ProjectileModule,
                laterModuleId);
            StableId sharedOperationId = StableId.Parse("operation.shared");

            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[]
                {
                    Module(
                        AutomaticModule,
                        new SyntheticOperation(OperationKind, sharedOperationId)),
                    Module(
                        ProjectileModule,
                        new SyntheticOperation(OperationKind, sharedOperationId),
                        Operation("operation.must-not-leak")),
                    Module(
                        laterModuleId,
                        Operation("operation.later")),
                });

            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(BuildInput(profile));

            Assert.That(plan.OperationCount, Is.EqualTo(2));
            Assert.That(plan.GetOperation(0).OperationId, Is.EqualTo(sharedOperationId));
            Assert.That(plan.GetOperation(1).OperationId, Is.EqualTo(StableId.Parse("operation.later")));
            Assert.That(
                plan.GetModuleExecution(1).FaultKind,
                Is.EqualTo(WeaponBehaviorModuleFaultKind.DuplicateOperationId));
            Assert.That(plan.GetModuleExecution(1).OperationCount, Is.Zero);
        }

        [Test]
        public void BuildExecutionPlan_ClassifiesNullMismatchedAndEmptyModuleResults()
        {
            StableId mismatchedModuleId = StableId.Parse("behavior.mismatched");
            StableId nullModuleId = StableId.Parse("behavior.null-plan");
            WeaponRuntimeProfile profile = BuildProfile(
                mismatchedModuleId,
                nullModuleId,
                EmptyModule);

            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[]
                {
                    new SyntheticModule(
                        mismatchedModuleId,
                        input => new WeaponBehaviorModulePlan(
                            AutomaticModule,
                            Operation("operation.wrong-owner"))),
                    new SyntheticModule(nullModuleId, input => null),
                    Module(EmptyModule),
                });

            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(BuildInput(profile));

            Assert.That(plan.OperationCount, Is.Zero);
            Assert.That(plan.FaultCount, Is.EqualTo(2));
            Assert.That(
                plan.GetModuleExecution(0).FaultKind,
                Is.EqualTo(WeaponBehaviorModuleFaultKind.ModuleIdMismatch));
            Assert.That(
                plan.GetModuleExecution(1).FaultKind,
                Is.EqualTo(WeaponBehaviorModuleFaultKind.NullPlan));
            Assert.That(
                plan.GetModuleExecution(2).Status,
                Is.EqualTo(WeaponBehaviorModuleExecutionStatus.Empty));
        }

        [Test]
        public void AddingIsolatedSyntheticModule_RequiresOnlyExplicitRegistration()
        {
            StableId novelModuleId = StableId.Parse("behavior.novel-test-module");
            WeaponRuntimeProfile profile = BuildProfile(novelModuleId);
            SyntheticModule novelModule = Module(
                novelModuleId,
                Operation("operation.novel-test-operation"));
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[] { novelModule });

            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(BuildInput(profile));

            Assert.That(pipeline.RegisteredModuleCount, Is.EqualTo(1));
            Assert.That(novelModule.InvocationCount, Is.EqualTo(1));
            Assert.That(plan.OperationCount, Is.EqualTo(1));
            Assert.That(plan.GetOperation(0).SourceModuleId, Is.EqualTo(novelModuleId));
        }

        [Test]
        public void InputAndPlanTypes_AreValidatedImmutableAndEngineFree()
        {
            WeaponRuntimeProfile profile = BuildProfile(AutomaticModule);
            WeaponBehaviorInput valid = BuildInput(profile);

            Assert.Throws<ArgumentNullException>(
                () => new WeaponBehaviorInput(
                    null,
                    valid.WeaponId,
                    valid.MountId,
                    valid.SimulationStep,
                    profile,
                    false,
                    0d,
                    0d,
                    1d,
                    0d,
                    1d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponBehaviorInput(
                    valid.CombatEventId,
                    valid.WeaponId,
                    valid.MountId,
                    -1L,
                    profile,
                    false,
                    0d,
                    0d,
                    1d,
                    0d,
                    1d));
            Assert.Throws<ArgumentException>(
                () => new WeaponBehaviorInput(
                    valid.CombatEventId,
                    valid.WeaponId,
                    valid.MountId,
                    0L,
                    profile,
                    false,
                    0d,
                    0d,
                    0d,
                    0d,
                    1d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponBehaviorInput(
                    valid.CombatEventId,
                    valid.WeaponId,
                    valid.MountId,
                    0L,
                    profile,
                    false,
                    0d,
                    0d,
                    1d,
                    0d,
                    1.01d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponBehaviorInput(
                    valid.CombatEventId,
                    valid.WeaponId,
                    valid.MountId,
                    0L,
                    profile,
                    false,
                    double.NaN,
                    0d,
                    1d,
                    0d,
                    1d));

            IWeaponFireExecutionOperation[] source =
            {
                Operation("operation.copied"),
            };
            WeaponBehaviorModulePlan modulePlan = new WeaponBehaviorModulePlan(
                AutomaticModule,
                source);
            source[0] = Operation("operation.replaced");
            Assert.That(
                modulePlan.GetOperation(0).OperationId,
                Is.EqualTo(StableId.Parse("operation.copied")));

            Type[] immutableTypes =
            {
                typeof(WeaponBehaviorInput),
                typeof(WeaponBehaviorModulePlan),
                typeof(WeaponFireExecutionPlan),
                typeof(WeaponBehaviorModuleExecution),
                typeof(WeaponFireExecutionOperationEntry),
            };
            foreach (Type type in immutableTypes)
            {
                Assert.That(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(property => property.CanWrite),
                    Is.Empty,
                    type.FullName + " exposes a writable public property.");
            }

            Assert.That(
                typeof(WeaponBehaviorPipeline).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static WeaponRuntimeProfile BuildProfile(params StableId[] moduleIds)
        {
            StableId[] copied = (StableId[])moduleIds.Clone();
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse("weapon-profile.pipeline-fixture"),
                0.1d,
                1,
                0d,
                0d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                false,
                0d,
                0d,
                0.25d,
                copied,
                copied,
                0);
        }

        private static WeaponBehaviorInput BuildInput(WeaponRuntimeProfile profile)
        {
            return new WeaponBehaviorInput(
                StableId.Parse("combat-event.pipeline-fixture"),
                StableId.Parse("weapon.synthetic"),
                StableId.Parse("weapon-mount.mount-one"),
                42L,
                profile,
                true,
                10d,
                -4d,
                0.6d,
                0.8d,
                0.75d);
        }

        private static SyntheticModule Module(
            StableId moduleId,
            params IWeaponFireExecutionOperation[] operations)
        {
            return new SyntheticModule(
                moduleId,
                input => new WeaponBehaviorModulePlan(moduleId, operations));
        }

        private static SyntheticOperation Operation(string operationId)
        {
            return new SyntheticOperation(OperationKind, StableId.Parse(operationId));
        }

        private static IWeaponFireExecutionOperation[] BuildOperations(
            string prefix,
            int count)
        {
            IWeaponFireExecutionOperation[] operations =
                new IWeaponFireExecutionOperation[count];
            for (int index = 0; index < count; index++)
            {
                operations[index] = Operation(
                    "operation." + prefix + "-" + index.ToString("d2"));
            }

            return operations;
        }

        private sealed class SyntheticOperation : IWeaponFireExecutionOperation
        {
            public SyntheticOperation(StableId operationKindId, StableId operationId)
            {
                OperationKindId = operationKindId;
                OperationId = operationId;
            }

            public StableId OperationKindId { get; }

            public StableId OperationId { get; }
        }

        private sealed class SyntheticModule : IWeaponBehaviorModule
        {
            private readonly Func<WeaponBehaviorInput, WeaponBehaviorModulePlan> build;

            public SyntheticModule(
                StableId moduleId,
                Func<WeaponBehaviorInput, WeaponBehaviorModulePlan> build)
            {
                ModuleId = moduleId;
                this.build = build;
            }

            public StableId ModuleId { get; }

            public int InvocationCount { get; private set; }

            public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
            {
                InvocationCount++;
                return build(input);
            }
        }
    }
}

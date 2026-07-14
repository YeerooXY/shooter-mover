using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Explicit registry and deterministic composer for reusable weapon behavior modules.
    /// Registration order is irrelevant; the validated WeaponRuntimeProfile order is authoritative.
    /// </summary>
    public sealed class WeaponBehaviorPipeline
    {
        private readonly Dictionary<StableId, IWeaponBehaviorModule> modulesById;

        public WeaponBehaviorPipeline(IEnumerable<IWeaponBehaviorModule> modules)
        {
            if (modules == null)
            {
                throw new ArgumentNullException(nameof(modules));
            }

            modulesById = new Dictionary<StableId, IWeaponBehaviorModule>();
            foreach (IWeaponBehaviorModule module in modules)
            {
                if (module == null)
                {
                    throw new ArgumentException(
                        "The behavior-module registry cannot contain null.",
                        nameof(modules));
                }

                if (module.ModuleId == null)
                {
                    throw new ArgumentException(
                        "Every behavior module requires a stable module ID.",
                        nameof(modules));
                }

                if (modulesById.ContainsKey(module.ModuleId))
                {
                    throw new ArgumentException(
                        "Duplicate behavior-module StableId: " + module.ModuleId + ".",
                        nameof(modules));
                }

                modulesById.Add(module.ModuleId, module);
            }
        }

        public int RegisteredModuleCount
        {
            get { return modulesById.Count; }
        }

        public WeaponFireExecutionPlan BuildExecutionPlan(WeaponBehaviorInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            IWeaponBehaviorModule[] orderedModules = ResolveOrderedModules(input.RuntimeProfile);
            List<WeaponFireExecutionOperationEntry> operations =
                new List<WeaponFireExecutionOperationEntry>();
            List<WeaponBehaviorModuleExecution> executions =
                new List<WeaponBehaviorModuleExecution>();
            HashSet<StableId> operationIds = new HashSet<StableId>();

            for (int moduleIndex = 0; moduleIndex < orderedModules.Length; moduleIndex++)
            {
                IWeaponBehaviorModule module = orderedModules[moduleIndex];
                int operationStartIndex = operations.Count;
                WeaponBehaviorModulePlan modulePlan;

                try
                {
                    modulePlan = module.BuildExecutionPlan(input);
                }
                catch (Exception)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            WeaponBehaviorModuleFaultKind.ModuleException,
                            operationStartIndex));
                    continue;
                }

                if (modulePlan == null)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            WeaponBehaviorModuleFaultKind.NullPlan,
                            operationStartIndex));
                    continue;
                }

                if (modulePlan.ModuleId != module.ModuleId)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            WeaponBehaviorModuleFaultKind.ModuleIdMismatch,
                            operationStartIndex));
                    continue;
                }

                bool duplicateOperationId = false;
                for (int operationIndex = 0;
                    operationIndex < modulePlan.OperationCount;
                    operationIndex++)
                {
                    StableId operationId = modulePlan.GetOperation(operationIndex).OperationId;
                    if (operationIds.Contains(operationId))
                    {
                        duplicateOperationId = true;
                        break;
                    }
                }

                if (duplicateOperationId)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            WeaponBehaviorModuleFaultKind.DuplicateOperationId,
                            operationStartIndex));
                    continue;
                }

                if (operations.Count + modulePlan.OperationCount
                    > WeaponFireExecutionPlan.MaximumOperationCount)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            WeaponBehaviorModuleFaultKind.PlanLimitExceeded,
                            operationStartIndex));
                    continue;
                }

                for (int operationIndex = 0;
                    operationIndex < modulePlan.OperationCount;
                    operationIndex++)
                {
                    IWeaponFireExecutionOperation operation =
                        modulePlan.GetOperation(operationIndex);
                    WeaponFireExecutionOperationEntry entry =
                        new WeaponFireExecutionOperationEntry(
                            module.ModuleId,
                            operationIndex,
                            operations.Count,
                            operation);

                    operations.Add(entry);
                    operationIds.Add(entry.OperationId);
                }

                WeaponBehaviorModuleExecutionStatus status =
                    modulePlan.OperationCount == 0
                        ? WeaponBehaviorModuleExecutionStatus.Empty
                        : WeaponBehaviorModuleExecutionStatus.Succeeded;

                executions.Add(
                    new WeaponBehaviorModuleExecution(
                        module.ModuleId,
                        status,
                        WeaponBehaviorModuleFaultKind.None,
                        operationStartIndex,
                        modulePlan.OperationCount));
            }

            return new WeaponFireExecutionPlan(
                input,
                operations.ToArray(),
                executions.ToArray());
        }

        private IWeaponBehaviorModule[] ResolveOrderedModules(WeaponRuntimeProfile runtimeProfile)
        {
            if (runtimeProfile == null)
            {
                throw new ArgumentNullException(nameof(runtimeProfile));
            }

            IWeaponBehaviorModule[] ordered =
                new IWeaponBehaviorModule[runtimeProfile.BehaviorModuleCount];
            HashSet<StableId> requestedIds = new HashSet<StableId>();

            for (int index = 0; index < runtimeProfile.BehaviorModuleCount; index++)
            {
                StableId moduleId = runtimeProfile.GetBehaviorModuleId(index);
                if (moduleId == null)
                {
                    throw new ArgumentException(
                        "A runtime profile cannot contain a null behavior-module ID.",
                        nameof(runtimeProfile));
                }

                if (!requestedIds.Add(moduleId))
                {
                    throw new ArgumentException(
                        "A runtime profile cannot repeat behavior-module ID " + moduleId + ".",
                        nameof(runtimeProfile));
                }

                IWeaponBehaviorModule module;
                if (!modulesById.TryGetValue(moduleId, out module))
                {
                    throw new InvalidOperationException(
                        "Unknown behavior-module StableId: " + moduleId + ".");
                }

                ordered[index] = module;
            }

            return ordered;
        }

        private static WeaponBehaviorModuleExecution CreateFault(
            StableId moduleId,
            WeaponBehaviorModuleFaultKind faultKind,
            int operationStartIndex)
        {
            return new WeaponBehaviorModuleExecution(
                moduleId,
                WeaponBehaviorModuleExecutionStatus.Faulted,
                faultKind,
                operationStartIndex,
                0);
        }
    }
}

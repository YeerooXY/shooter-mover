using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    /// <summary>
    /// Explicit registry and deterministic composer for reusable gun behavior modules.
    /// Registration order is irrelevant; the validated GunLiveProfile order is authoritative.
    /// </summary>
    public sealed class GunBehaviorPipeline
    {
        private readonly Dictionary<StableId, IGunBehaviorModule> modulesById;

        public GunBehaviorPipeline(IEnumerable<IGunBehaviorModule> modules)
        {
            if (modules == null)
            {
                throw new ArgumentNullException(nameof(modules));
            }

            modulesById = new Dictionary<StableId, IGunBehaviorModule>();
            foreach (IGunBehaviorModule module in modules)
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

        public GunFireExecutionPlan BuildExecutionPlan(GunBehaviorInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            IGunBehaviorModule[] orderedModules = ResolveOrderedModules(input.RuntimeProfile);
            List<GunFireExecutionOperationEntry> operations =
                new List<GunFireExecutionOperationEntry>();
            List<GunBehaviorModuleExecution> executions =
                new List<GunBehaviorModuleExecution>();
            HashSet<StableId> operationIds = new HashSet<StableId>();

            for (int moduleIndex = 0; moduleIndex < orderedModules.Length; moduleIndex++)
            {
                IGunBehaviorModule module = orderedModules[moduleIndex];
                int operationStartIndex = operations.Count;
                GunBehaviorModulePlan modulePlan;

                try
                {
                    modulePlan = module.BuildExecutionPlan(input);
                }
                catch (Exception)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            GunBehaviorModuleFaultKind.ModuleException,
                            operationStartIndex));
                    continue;
                }

                if (modulePlan == null)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            GunBehaviorModuleFaultKind.NullPlan,
                            operationStartIndex));
                    continue;
                }

                if (modulePlan.ModuleId != module.ModuleId)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            GunBehaviorModuleFaultKind.ModuleIdMismatch,
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
                            GunBehaviorModuleFaultKind.DuplicateOperationId,
                            operationStartIndex));
                    continue;
                }

                if (operations.Count + modulePlan.OperationCount
                    > GunFireExecutionPlan.MaximumOperationCount)
                {
                    executions.Add(
                        CreateFault(
                            module.ModuleId,
                            GunBehaviorModuleFaultKind.PlanLimitExceeded,
                            operationStartIndex));
                    continue;
                }

                for (int operationIndex = 0;
                    operationIndex < modulePlan.OperationCount;
                    operationIndex++)
                {
                    IGunFireExecutionOperation operation =
                        modulePlan.GetOperation(operationIndex);
                    GunFireExecutionOperationEntry entry =
                        new GunFireExecutionOperationEntry(
                            module.ModuleId,
                            operationIndex,
                            operations.Count,
                            operation);

                    operations.Add(entry);
                    operationIds.Add(entry.OperationId);
                }

                GunBehaviorModuleExecutionStatus status =
                    modulePlan.OperationCount == 0
                        ? GunBehaviorModuleExecutionStatus.Empty
                        : GunBehaviorModuleExecutionStatus.Succeeded;

                executions.Add(
                    new GunBehaviorModuleExecution(
                        module.ModuleId,
                        status,
                        GunBehaviorModuleFaultKind.None,
                        operationStartIndex,
                        modulePlan.OperationCount));
            }

            return new GunFireExecutionPlan(
                input,
                operations.ToArray(),
                executions.ToArray());
        }

        private IGunBehaviorModule[] ResolveOrderedModules(GunLiveProfile runtimeProfile)
        {
            if (runtimeProfile == null)
            {
                throw new ArgumentNullException(nameof(runtimeProfile));
            }

            IGunBehaviorModule[] ordered =
                new IGunBehaviorModule[runtimeProfile.BehaviorModuleCount];
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

                IGunBehaviorModule module;
                if (!modulesById.TryGetValue(moduleId, out module))
                {
                    throw new InvalidOperationException(
                        "Unknown behavior-module StableId: " + moduleId + ".");
                }

                ordered[index] = module;
            }

            return ordered;
        }

        private static GunBehaviorModuleExecution CreateFault(
            StableId moduleId,
            GunBehaviorModuleFaultKind faultKind,
            int operationStartIndex)
        {
            return new GunBehaviorModuleExecution(
                moduleId,
                GunBehaviorModuleExecutionStatus.Faulted,
                faultKind,
                operationStartIndex,
                0);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Combat
{
    public enum WeaponBehaviorModuleExecutionStatus
    {
        Succeeded = 1,
        Empty = 2,
        Faulted = 3,
    }

    public enum WeaponBehaviorModuleFaultKind
    {
        None = 0,
        ModuleException = 1,
        NullPlan = 2,
        ModuleIdMismatch = 3,
        DuplicateOperationId = 4,
        PlanLimitExceeded = 5,
    }

    /// <summary>
    /// Immutable snapshot of one typed operation and the module/order that emitted it.
    /// The stable IDs are captured when the plan is built so later identity cannot drift.
    /// </summary>
    public sealed class WeaponFireExecutionOperationEntry
    {
        internal WeaponFireExecutionOperationEntry(
            StableId sourceModuleId,
            int moduleOperationIndex,
            int planOperationIndex,
            IWeaponFireExecutionOperation operation)
        {
            if (sourceModuleId == null)
            {
                throw new ArgumentNullException(nameof(sourceModuleId));
            }

            if (moduleOperationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(moduleOperationIndex));
            }

            if (planOperationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(planOperationIndex));
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (operation.OperationKindId == null)
            {
                throw new ArgumentException(
                    "Execution operations require a stable operation-kind ID.",
                    nameof(operation));
            }

            if (operation.OperationId == null)
            {
                throw new ArgumentException(
                    "Execution operations require a deterministic operation ID.",
                    nameof(operation));
            }

            SourceModuleId = sourceModuleId;
            ModuleOperationIndex = moduleOperationIndex;
            PlanOperationIndex = planOperationIndex;
            Operation = operation;
            OperationKindId = operation.OperationKindId;
            OperationId = operation.OperationId;
        }

        public StableId SourceModuleId { get; }

        public int ModuleOperationIndex { get; }

        public int PlanOperationIndex { get; }

        public IWeaponFireExecutionOperation Operation { get; }

        public StableId OperationKindId { get; }

        public StableId OperationId { get; }
    }

    /// <summary>
    /// Result of invoking one profile module. Faults are classified without exception
    /// messages or stack traces so replay output remains deterministic.
    /// </summary>
    public sealed class WeaponBehaviorModuleExecution
    {
        internal WeaponBehaviorModuleExecution(
            StableId moduleId,
            WeaponBehaviorModuleExecutionStatus status,
            WeaponBehaviorModuleFaultKind faultKind,
            int operationStartIndex,
            int operationCount)
        {
            if (moduleId == null)
            {
                throw new ArgumentNullException(nameof(moduleId));
            }

            if (!Enum.IsDefined(typeof(WeaponBehaviorModuleExecutionStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (!Enum.IsDefined(typeof(WeaponBehaviorModuleFaultKind), faultKind))
            {
                throw new ArgumentOutOfRangeException(nameof(faultKind));
            }

            if (operationStartIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationStartIndex));
            }

            if (operationCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationCount));
            }

            if (status == WeaponBehaviorModuleExecutionStatus.Faulted)
            {
                if (faultKind == WeaponBehaviorModuleFaultKind.None || operationCount != 0)
                {
                    throw new ArgumentException(
                        "A faulted module requires one fault kind and cannot contribute operations.");
                }
            }
            else
            {
                if (faultKind != WeaponBehaviorModuleFaultKind.None)
                {
                    throw new ArgumentException(
                        "A non-faulted module cannot carry a fault kind.",
                        nameof(faultKind));
                }

                if (status == WeaponBehaviorModuleExecutionStatus.Empty && operationCount != 0)
                {
                    throw new ArgumentException(
                        "An empty module execution cannot contribute operations.",
                        nameof(operationCount));
                }

                if (status == WeaponBehaviorModuleExecutionStatus.Succeeded && operationCount == 0)
                {
                    throw new ArgumentException(
                        "A succeeded module execution must contribute at least one operation.",
                        nameof(operationCount));
                }
            }

            ModuleId = moduleId;
            Status = status;
            FaultKind = faultKind;
            OperationStartIndex = operationStartIndex;
            OperationCount = operationCount;
        }

        public StableId ModuleId { get; }

        public WeaponBehaviorModuleExecutionStatus Status { get; }

        public WeaponBehaviorModuleFaultKind FaultKind { get; }

        public int OperationStartIndex { get; }

        public int OperationCount { get; }
    }

    /// <summary>
    /// Bounded immutable result of applying the ordered reusable modules from one
    /// validated WeaponRuntimeProfile to one fire input.
    /// </summary>
    public sealed class WeaponFireExecutionPlan : IEquatable<WeaponFireExecutionPlan>
    {
        public const int CurrentPlanVersion = 1;
        public const int MaximumOperationCount = 256;
        public const string FingerprintPrefix = "sha256:";
        public const string DeterministicIdentityNamespace = "weapon-plan";

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private readonly WeaponFireExecutionOperationEntry[] operations;
        private readonly WeaponBehaviorModuleExecution[] moduleExecutions;
        private readonly string canonicalText;

        internal WeaponFireExecutionPlan(
            WeaponBehaviorInput input,
            WeaponFireExecutionOperationEntry[] operations,
            WeaponBehaviorModuleExecution[] moduleExecutions)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (operations == null)
            {
                throw new ArgumentNullException(nameof(operations));
            }

            if (moduleExecutions == null)
            {
                throw new ArgumentNullException(nameof(moduleExecutions));
            }

            if (operations.Length > MaximumOperationCount)
            {
                throw new ArgumentException(
                    "A fire execution plan cannot exceed "
                    + MaximumOperationCount.ToString(CultureInfo.InvariantCulture)
                    + " operations.",
                    nameof(operations));
            }

            if (moduleExecutions.Length != input.RuntimeProfile.BehaviorModuleCount)
            {
                throw new ArgumentException(
                    "Every authored behavior module must have exactly one execution result.",
                    nameof(moduleExecutions));
            }

            this.operations = (WeaponFireExecutionOperationEntry[])operations.Clone();
            this.moduleExecutions = (WeaponBehaviorModuleExecution[])moduleExecutions.Clone();
            ValidateComposition(input, this.operations, this.moduleExecutions);

            Input = input;
            canonicalText = BuildCanonicalText();
            Fingerprint = ComputeSha256(canonicalText);
            DeterministicIdentity = StableId.Create(
                DeterministicIdentityNamespace,
                Fingerprint.Substring(FingerprintPrefix.Length));
        }

        public int PlanVersion
        {
            get { return CurrentPlanVersion; }
        }

        public WeaponBehaviorInput Input { get; }

        public StableId CombatEventId
        {
            get { return Input.CombatEventId; }
        }

        public StableId WeaponId
        {
            get { return Input.WeaponId; }
        }

        public StableId MountId
        {
            get { return Input.MountId; }
        }

        public int OperationCount
        {
            get { return operations.Length; }
        }

        public int ModuleExecutionCount
        {
            get { return moduleExecutions.Length; }
        }

        public int FaultCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < moduleExecutions.Length; index++)
                {
                    if (moduleExecutions[index].Status == WeaponBehaviorModuleExecutionStatus.Faulted)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public string Fingerprint { get; }

        public StableId DeterministicIdentity { get; }

        public WeaponFireExecutionOperationEntry GetOperation(int index)
        {
            if (index < 0 || index >= operations.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return operations[index];
        }

        public WeaponBehaviorModuleExecution GetModuleExecution(int index)
        {
            if (index < 0 || index >= moduleExecutions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return moduleExecutions[index];
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(WeaponFireExecutionPlan other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponFireExecutionPlan);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        public override string ToString()
        {
            return canonicalText;
        }

        private static void ValidateComposition(
            WeaponBehaviorInput input,
            WeaponFireExecutionOperationEntry[] operations,
            WeaponBehaviorModuleExecution[] moduleExecutions)
        {
            HashSet<StableId> operationIds = new HashSet<StableId>();
            int expectedOperationStart = 0;

            for (int moduleIndex = 0; moduleIndex < moduleExecutions.Length; moduleIndex++)
            {
                WeaponBehaviorModuleExecution execution = moduleExecutions[moduleIndex];
                if (execution == null)
                {
                    throw new ArgumentException(
                        "Module executions cannot contain null.",
                        nameof(moduleExecutions));
                }

                StableId expectedModuleId = input.RuntimeProfile.GetBehaviorModuleId(moduleIndex);
                if (execution.ModuleId != expectedModuleId)
                {
                    throw new ArgumentException(
                        "Module execution order must match the validated runtime profile.",
                        nameof(moduleExecutions));
                }

                if (execution.OperationStartIndex != expectedOperationStart)
                {
                    throw new ArgumentException(
                        "Module operation ranges must be contiguous and canonical.",
                        nameof(moduleExecutions));
                }

                for (int localIndex = 0; localIndex < execution.OperationCount; localIndex++)
                {
                    int operationIndex = execution.OperationStartIndex + localIndex;
                    if (operationIndex >= operations.Length)
                    {
                        throw new ArgumentException(
                            "A module operation range exceeds the plan operation list.",
                            nameof(moduleExecutions));
                    }

                    WeaponFireExecutionOperationEntry entry = operations[operationIndex];
                    if (entry == null)
                    {
                        throw new ArgumentException(
                            "Plan operations cannot contain null.",
                            nameof(operations));
                    }

                    if (entry.PlanOperationIndex != operationIndex
                        || entry.ModuleOperationIndex != localIndex
                        || entry.SourceModuleId != execution.ModuleId)
                    {
                        throw new ArgumentException(
                            "Plan operation ordering metadata is inconsistent.",
                            nameof(operations));
                    }

                    if (!operationIds.Add(entry.OperationId))
                    {
                        throw new ArgumentException(
                            "Plan operation IDs must be unique.",
                            nameof(operations));
                    }
                }

                expectedOperationStart += execution.OperationCount;
            }

            if (expectedOperationStart != operations.Length)
            {
                throw new ArgumentException(
                    "Every plan operation must belong to exactly one module execution.",
                    nameof(operations));
            }
        }

        private string BuildCanonicalText()
        {
            List<string> lines = new List<string>();
            lines.Add("plan_version=" + CurrentPlanVersion.ToString(CultureInfo.InvariantCulture));
            lines.AddRange(Input.ToCanonicalString().Split('\n'));
            lines.Add("module_execution_count=" + moduleExecutions.Length.ToString(CultureInfo.InvariantCulture));
            lines.Add("operation_count=" + operations.Length.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < moduleExecutions.Length; index++)
            {
                WeaponBehaviorModuleExecution execution = moduleExecutions[index];
                string prefix = "module_" + index.ToString(CultureInfo.InvariantCulture) + "_";
                lines.Add(prefix + "id=" + execution.ModuleId);
                lines.Add(prefix + "status=" + FormatStatus(execution.Status));
                lines.Add(prefix + "fault=" + FormatFault(execution.FaultKind));
                lines.Add(
                    prefix + "operation_start="
                    + execution.OperationStartIndex.ToString(CultureInfo.InvariantCulture));
                lines.Add(
                    prefix + "operation_count="
                    + execution.OperationCount.ToString(CultureInfo.InvariantCulture));
            }

            for (int index = 0; index < operations.Length; index++)
            {
                WeaponFireExecutionOperationEntry operation = operations[index];
                string prefix = "operation_" + index.ToString(CultureInfo.InvariantCulture) + "_";
                lines.Add(prefix + "source_module_id=" + operation.SourceModuleId);
                lines.Add(
                    prefix + "module_index="
                    + operation.ModuleOperationIndex.ToString(CultureInfo.InvariantCulture));
                lines.Add(prefix + "kind_id=" + operation.OperationKindId);
                lines.Add(prefix + "id=" + operation.OperationId);
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string FormatStatus(WeaponBehaviorModuleExecutionStatus status)
        {
            switch (status)
            {
                case WeaponBehaviorModuleExecutionStatus.Succeeded:
                    return "succeeded";
                case WeaponBehaviorModuleExecutionStatus.Empty:
                    return "empty";
                case WeaponBehaviorModuleExecutionStatus.Faulted:
                    return "faulted";
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
        }

        private static string FormatFault(WeaponBehaviorModuleFaultKind faultKind)
        {
            switch (faultKind)
            {
                case WeaponBehaviorModuleFaultKind.None:
                    return "none";
                case WeaponBehaviorModuleFaultKind.ModuleException:
                    return "module-exception";
                case WeaponBehaviorModuleFaultKind.NullPlan:
                    return "null-plan";
                case WeaponBehaviorModuleFaultKind.ModuleIdMismatch:
                    return "module-id-mismatch";
                case WeaponBehaviorModuleFaultKind.DuplicateOperationId:
                    return "duplicate-operation-id";
                case WeaponBehaviorModuleFaultKind.PlanLimitExceeded:
                    return "plan-limit-exceeded";
                default:
                    throw new ArgumentOutOfRangeException(nameof(faultKind));
            }
        }

        private static string ComputeSha256(string text)
        {
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            }

            return FingerprintPrefix + string.Concat(digest.Select(value => value.ToString("x2")));
        }
    }
}

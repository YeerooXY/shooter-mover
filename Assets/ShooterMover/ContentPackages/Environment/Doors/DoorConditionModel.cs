using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Environment.Doors
{
    public enum DoorConditionComposition
    {
        All = 1,
        Any = 2,
    }

    public enum DoorConditionKind
    {
        Always = 1,
        TriggerEntered = 2,
        InteractionRequested = 3,
        EncounterResolved = 4,
        TargetDestroyed = 5,
        WalletAmountAtLeast = 6,
        KeyOwned = 7,
    }

    public enum DoorConditionDiagnosticCode
    {
        None = 0,
        EmptyConditionSet = 1,
        MissingReader = 2,
        FactNotSatisfied = 3,
        InvalidCondition = 4,
    }

    public interface IDoorEncounterConditionReader
    {
        bool IsEncounterResolved(StableId encounterId);
    }

    public interface IDoorTargetConditionReader
    {
        bool IsTargetDestroyed(StableId targetId);
    }

    public interface IDoorWalletReadPort
    {
        bool TryReadAmount(StableId currencyId, out long amount);
    }

    public interface IDoorKeyReadPort
    {
        bool IsKeyOwned(StableId keyId);
    }

    [Serializable]
    public sealed class DoorConditionAuthoring
    {
        [SerializeField] private DoorConditionKind kind = DoorConditionKind.Always;
        [SerializeField] private string subjectId = "condition.unassigned";
        [SerializeField] private long requiredAmount = 1;

        public DoorConditionKind Kind
        {
            get { return kind; }
        }

        public DoorConditionRequirement BuildRequirement()
        {
            switch (kind)
            {
                case DoorConditionKind.Always:
                    return DoorConditionRequirement.Always();
                case DoorConditionKind.TriggerEntered:
                    return DoorConditionRequirement.TriggerEntered();
                case DoorConditionKind.InteractionRequested:
                    return DoorConditionRequirement.InteractionRequested();
                case DoorConditionKind.EncounterResolved:
                    return DoorConditionRequirement.EncounterResolved(
                        StableId.Parse(subjectId));
                case DoorConditionKind.TargetDestroyed:
                    return DoorConditionRequirement.TargetDestroyed(
                        StableId.Parse(subjectId));
                case DoorConditionKind.WalletAmountAtLeast:
                    return DoorConditionRequirement.WalletAmountAtLeast(
                        StableId.Parse(subjectId),
                        requiredAmount);
                case DoorConditionKind.KeyOwned:
                    return DoorConditionRequirement.KeyOwned(
                        StableId.Parse(subjectId));
                default:
                    throw new InvalidOperationException(
                        "Door condition kind is not supported: " + kind);
            }
        }

        public static DoorConditionAuthoring CreateRuntime(
            DoorConditionKind kind,
            string subjectId = "condition.unassigned",
            long requiredAmount = 1)
        {
            return new DoorConditionAuthoring
            {
                kind = kind,
                subjectId = subjectId ?? string.Empty,
                requiredAmount = requiredAmount,
            };
        }
    }

    public sealed class DoorConditionRequirement
    {
        private DoorConditionRequirement(
            DoorConditionKind kind,
            StableId subjectId,
            long requiredAmount)
        {
            if (!Enum.IsDefined(typeof(DoorConditionKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (RequiresSubject(kind) && subjectId == null)
            {
                throw new ArgumentNullException(nameof(subjectId));
            }

            if (kind == DoorConditionKind.WalletAmountAtLeast
                && requiredAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredAmount),
                    requiredAmount,
                    "Wallet condition amount must be positive.");
            }

            Kind = kind;
            SubjectId = subjectId;
            RequiredAmount = requiredAmount;
        }

        public DoorConditionKind Kind { get; }

        public StableId SubjectId { get; }

        public long RequiredAmount { get; }

        public static DoorConditionRequirement Always()
        {
            return new DoorConditionRequirement(
                DoorConditionKind.Always,
                null,
                0);
        }

        public static DoorConditionRequirement TriggerEntered()
        {
            return new DoorConditionRequirement(
                DoorConditionKind.TriggerEntered,
                null,
                0);
        }

        public static DoorConditionRequirement InteractionRequested()
        {
            return new DoorConditionRequirement(
                DoorConditionKind.InteractionRequested,
                null,
                0);
        }

        public static DoorConditionRequirement EncounterResolved(StableId encounterId)
        {
            return new DoorConditionRequirement(
                DoorConditionKind.EncounterResolved,
                encounterId,
                0);
        }

        public static DoorConditionRequirement TargetDestroyed(StableId targetId)
        {
            return new DoorConditionRequirement(
                DoorConditionKind.TargetDestroyed,
                targetId,
                0);
        }

        public static DoorConditionRequirement WalletAmountAtLeast(
            StableId currencyId,
            long requiredAmount)
        {
            return new DoorConditionRequirement(
                DoorConditionKind.WalletAmountAtLeast,
                currencyId,
                requiredAmount);
        }

        public static DoorConditionRequirement KeyOwned(StableId keyId)
        {
            return new DoorConditionRequirement(
                DoorConditionKind.KeyOwned,
                keyId,
                0);
        }

        internal static bool RequiresSubject(DoorConditionKind kind)
        {
            return kind == DoorConditionKind.EncounterResolved
                || kind == DoorConditionKind.TargetDestroyed
                || kind == DoorConditionKind.WalletAmountAtLeast
                || kind == DoorConditionKind.KeyOwned;
        }

        internal string ToCanonicalString()
        {
            return "kind="
                + ((int)Kind).ToString(CultureInfo.InvariantCulture)
                + "|subject="
                + (SubjectId == null ? string.Empty : SubjectId.ToString())
                + "|amount="
                + RequiredAmount.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class DoorConditionEvaluationContext
    {
        public DoorConditionEvaluationContext(
            bool triggerEntered,
            bool interactionRequested,
            IDoorEncounterConditionReader encounterReader,
            IDoorTargetConditionReader targetReader,
            IDoorWalletReadPort walletReader,
            IDoorKeyReadPort keyReader)
        {
            TriggerEntered = triggerEntered;
            InteractionRequested = interactionRequested;
            EncounterReader = encounterReader;
            TargetReader = targetReader;
            WalletReader = walletReader;
            KeyReader = keyReader;
        }

        public bool TriggerEntered { get; }

        public bool InteractionRequested { get; }

        public IDoorEncounterConditionReader EncounterReader { get; }

        public IDoorTargetConditionReader TargetReader { get; }

        public IDoorWalletReadPort WalletReader { get; }

        public IDoorKeyReadPort KeyReader { get; }
    }

    public sealed class DoorConditionLeafResult
    {
        internal DoorConditionLeafResult(
            int index,
            DoorConditionRequirement requirement,
            bool isConfigurationValid,
            bool isSatisfied,
            DoorConditionDiagnosticCode diagnosticCode,
            string diagnostic)
        {
            Index = index;
            Requirement = requirement;
            IsConfigurationValid = isConfigurationValid;
            IsSatisfied = isSatisfied;
            DiagnosticCode = diagnosticCode;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public int Index { get; }

        public DoorConditionRequirement Requirement { get; }

        public bool IsConfigurationValid { get; }

        public bool IsSatisfied { get; }

        public DoorConditionDiagnosticCode DiagnosticCode { get; }

        public string Diagnostic { get; }
    }

    public sealed class DoorConditionEvaluationResult
    {
        private readonly ReadOnlyCollection<DoorConditionLeafResult> leafResults;

        internal DoorConditionEvaluationResult(
            DoorConditionComposition composition,
            bool isConfigurationValid,
            bool isSatisfied,
            IEnumerable<DoorConditionLeafResult> leafResults,
            string diagnosticFingerprint)
        {
            Composition = composition;
            IsConfigurationValid = isConfigurationValid;
            IsSatisfied = isSatisfied;
            this.leafResults = new ReadOnlyCollection<DoorConditionLeafResult>(
                new List<DoorConditionLeafResult>(
                    leafResults ?? Array.Empty<DoorConditionLeafResult>()));
            DiagnosticFingerprint = diagnosticFingerprint ?? string.Empty;
        }

        public DoorConditionComposition Composition { get; }

        public bool IsConfigurationValid { get; }

        public bool IsSatisfied { get; }

        public IReadOnlyList<DoorConditionLeafResult> LeafResults
        {
            get { return leafResults; }
        }

        public string DiagnosticFingerprint { get; }
    }

    public sealed class DoorConditionSet
    {
        private readonly ReadOnlyCollection<DoorConditionRequirement> requirements;

        public DoorConditionSet(
            DoorConditionComposition composition,
            IEnumerable<DoorConditionRequirement> requirements)
        {
            if (composition != DoorConditionComposition.All
                && composition != DoorConditionComposition.Any)
            {
                throw new ArgumentOutOfRangeException(nameof(composition));
            }

            List<DoorConditionRequirement> copied =
                new List<DoorConditionRequirement>();
            if (requirements != null)
            {
                foreach (DoorConditionRequirement requirement in requirements)
                {
                    if (requirement == null)
                    {
                        throw new ArgumentException(
                            "Door conditions cannot contain null requirements.",
                            nameof(requirements));
                    }

                    copied.Add(requirement);
                }
            }

            Composition = composition;
            this.requirements =
                new ReadOnlyCollection<DoorConditionRequirement>(copied);
        }

        public DoorConditionComposition Composition { get; }

        public IReadOnlyList<DoorConditionRequirement> Requirements
        {
            get { return requirements; }
        }

        public DoorConditionEvaluationResult Evaluate(
            DoorConditionEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            List<DoorConditionLeafResult> results =
                new List<DoorConditionLeafResult>(requirements.Count);

            if (requirements.Count == 0)
            {
                results.Add(
                    new DoorConditionLeafResult(
                        -1,
                        null,
                        false,
                        false,
                        DoorConditionDiagnosticCode.EmptyConditionSet,
                        "A door condition set must contain at least one condition."));
                return BuildResult(false, false, results);
            }

            bool allValid = true;
            bool aggregate = Composition == DoorConditionComposition.All;
            for (int index = 0; index < requirements.Count; index++)
            {
                DoorConditionLeafResult result = EvaluateLeaf(
                    index,
                    requirements[index],
                    context);
                results.Add(result);
                allValid &= result.IsConfigurationValid;

                if (Composition == DoorConditionComposition.All)
                {
                    aggregate &= result.IsSatisfied;
                }
                else
                {
                    aggregate |= result.IsSatisfied;
                }
            }

            return BuildResult(allValid, allValid && aggregate, results);
        }

        private DoorConditionEvaluationResult BuildResult(
            bool valid,
            bool satisfied,
            IEnumerable<DoorConditionLeafResult> results)
        {
            List<DoorConditionLeafResult> copied =
                new List<DoorConditionLeafResult>(results);
            StringBuilder canonical = new StringBuilder();
            canonical.Append("composition=")
                .Append((int)Composition)
                .Append("|valid=")
                .Append(valid ? "1" : "0")
                .Append("|satisfied=")
                .Append(satisfied ? "1" : "0");

            for (int index = 0; index < copied.Count; index++)
            {
                DoorConditionLeafResult leaf = copied[index];
                canonical.Append("|index=")
                    .Append(leaf.Index.ToString(CultureInfo.InvariantCulture))
                    .Append("|kind=")
                    .Append(
                        leaf.Requirement == null
                            ? 0
                            : (int)leaf.Requirement.Kind)
                    .Append("|requirement=")
                    .Append(
                        leaf.Requirement == null
                            ? string.Empty
                            : leaf.Requirement.ToCanonicalString())
                    .Append("|code=")
                    .Append((int)leaf.DiagnosticCode)
                    .Append("|leaf-valid=")
                    .Append(leaf.IsConfigurationValid ? "1" : "0")
                    .Append("|leaf-satisfied=")
                    .Append(leaf.IsSatisfied ? "1" : "0");
            }

            return new DoorConditionEvaluationResult(
                Composition,
                valid,
                satisfied,
                copied,
                DeterministicFingerprint(canonical.ToString()));
        }

        private static DoorConditionLeafResult EvaluateLeaf(
            int index,
            DoorConditionRequirement requirement,
            DoorConditionEvaluationContext context)
        {
            switch (requirement.Kind)
            {
                case DoorConditionKind.Always:
                    return Satisfied(index, requirement, "Always condition is satisfied.");
                case DoorConditionKind.TriggerEntered:
                    return context.TriggerEntered
                        ? Satisfied(index, requirement, "Door trigger was entered.")
                        : Unsatisfied(index, requirement, "Door trigger has not been entered.");
                case DoorConditionKind.InteractionRequested:
                    return context.InteractionRequested
                        ? Satisfied(index, requirement, "Door interaction was requested.")
                        : Unsatisfied(index, requirement, "Door interaction has not been requested.");
                case DoorConditionKind.EncounterResolved:
                    if (context.EncounterReader == null)
                    {
                        return MissingReader(
                            index,
                            requirement,
                            "Encounter condition requires IDoorEncounterConditionReader.");
                    }

                    return context.EncounterReader.IsEncounterResolved(
                        requirement.SubjectId)
                        ? Satisfied(index, requirement, "Encounter is resolved.")
                        : Unsatisfied(index, requirement, "Encounter is not resolved.");
                case DoorConditionKind.TargetDestroyed:
                    if (context.TargetReader == null)
                    {
                        return MissingReader(
                            index,
                            requirement,
                            "Target condition requires IDoorTargetConditionReader.");
                    }

                    return context.TargetReader.IsTargetDestroyed(requirement.SubjectId)
                        ? Satisfied(index, requirement, "Target is destroyed.")
                        : Unsatisfied(index, requirement, "Target is not destroyed.");
                case DoorConditionKind.WalletAmountAtLeast:
                    if (context.WalletReader == null)
                    {
                        return MissingReader(
                            index,
                            requirement,
                            "Wallet condition requires IDoorWalletReadPort.");
                    }

                    long amount;
                    if (!context.WalletReader.TryReadAmount(
                        requirement.SubjectId,
                        out amount))
                    {
                        return Unsatisfied(
                            index,
                            requirement,
                            "Wallet reader has no value for the requested currency.");
                    }

                    return amount >= requirement.RequiredAmount
                        ? Satisfied(index, requirement, "Visible wallet amount is sufficient.")
                        : Unsatisfied(index, requirement, "Visible wallet amount is insufficient.");
                case DoorConditionKind.KeyOwned:
                    if (context.KeyReader == null)
                    {
                        return MissingReader(
                            index,
                            requirement,
                            "Key condition requires IDoorKeyReadPort.");
                    }

                    return context.KeyReader.IsKeyOwned(requirement.SubjectId)
                        ? Satisfied(index, requirement, "Required key is owned.")
                        : Unsatisfied(index, requirement, "Required key is not owned.");
                default:
                    return new DoorConditionLeafResult(
                        index,
                        requirement,
                        false,
                        false,
                        DoorConditionDiagnosticCode.InvalidCondition,
                        "Door condition kind is invalid.");
            }
        }

        private static DoorConditionLeafResult Satisfied(
            int index,
            DoorConditionRequirement requirement,
            string diagnostic)
        {
            return new DoorConditionLeafResult(
                index,
                requirement,
                true,
                true,
                DoorConditionDiagnosticCode.None,
                diagnostic);
        }

        private static DoorConditionLeafResult Unsatisfied(
            int index,
            DoorConditionRequirement requirement,
            string diagnostic)
        {
            return new DoorConditionLeafResult(
                index,
                requirement,
                true,
                false,
                DoorConditionDiagnosticCode.FactNotSatisfied,
                diagnostic);
        }

        private static DoorConditionLeafResult MissingReader(
            int index,
            DoorConditionRequirement requirement,
            string diagnostic)
        {
            return new DoorConditionLeafResult(
                index,
                requirement,
                false,
                false,
                DoorConditionDiagnosticCode.MissingReader,
                diagnostic);
        }

        private static string DeterministicFingerprint(string text)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis;

                for (int index = 0; index < text.Length; index++)
                {
                    char value = text[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>
    /// Mutable test/composition fixture implementing only read ports. It is not a
    /// wallet, key inventory, mission authority, or encounter authority.
    /// </summary>
    public sealed class DoorConditionFactSnapshot :
        IDoorEncounterConditionReader,
        IDoorTargetConditionReader,
        IDoorWalletReadPort,
        IDoorKeyReadPort
    {
        private readonly HashSet<StableId> resolvedEncounters =
            new HashSet<StableId>();
        private readonly HashSet<StableId> destroyedTargets =
            new HashSet<StableId>();
        private readonly Dictionary<StableId, long> walletAmounts =
            new Dictionary<StableId, long>();
        private readonly HashSet<StableId> ownedKeys =
            new HashSet<StableId>();

        public void SetEncounterResolved(StableId encounterId, bool resolved)
        {
            SetMembership(resolvedEncounters, encounterId, resolved);
        }

        public void SetTargetDestroyed(StableId targetId, bool destroyed)
        {
            SetMembership(destroyedTargets, targetId, destroyed);
        }

        public void SetWalletAmount(StableId currencyId, long amount)
        {
            if (currencyId == null)
            {
                throw new ArgumentNullException(nameof(currencyId));
            }

            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            walletAmounts[currencyId] = amount;
        }

        public void ClearWalletAmount(StableId currencyId)
        {
            if (currencyId != null)
            {
                walletAmounts.Remove(currencyId);
            }
        }

        public void SetKeyOwned(StableId keyId, bool owned)
        {
            SetMembership(ownedKeys, keyId, owned);
        }

        public bool IsEncounterResolved(StableId encounterId)
        {
            return encounterId != null && resolvedEncounters.Contains(encounterId);
        }

        public bool IsTargetDestroyed(StableId targetId)
        {
            return targetId != null && destroyedTargets.Contains(targetId);
        }

        public bool TryReadAmount(StableId currencyId, out long amount)
        {
            amount = 0;
            return currencyId != null
                && walletAmounts.TryGetValue(currencyId, out amount);
        }

        public bool IsKeyOwned(StableId keyId)
        {
            return keyId != null && ownedKeys.Contains(keyId);
        }

        private static void SetMembership(
            HashSet<StableId> values,
            StableId id,
            bool included)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (included)
            {
                values.Add(id);
            }
            else
            {
                values.Remove(id);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Canonical persistent state for one exact owned weapon.
    ///
    /// Authored weapon data is resolved through WeaponDefinitionId and is never copied into
    /// the owned instance. Acquisition and transaction facts belong to the operation that grants
    /// the instance, not to this value.
    /// </summary>
    public sealed class WeaponEquipmentInstance : IEquatable<WeaponEquipmentInstance>
    {
        private readonly ReadOnlyCollection<StableId> augmentAssignments;
        private readonly ReadOnlyCollection<StableId> overclockAssignments;

        private WeaponEquipmentInstance(
            StableId instanceId,
            WeaponDefinitionId weaponDefinitionId,
            IEnumerable<StableId> augmentAssignments,
            IEnumerable<StableId> overclockAssignments)
        {
            InstanceId = instanceId
                ?? throw new ArgumentNullException(nameof(instanceId));
            WeaponDefinitionId = weaponDefinitionId
                ?? throw new ArgumentNullException(nameof(weaponDefinitionId));
            this.augmentAssignments = CopyAssignments(
                augmentAssignments,
                nameof(augmentAssignments));
            this.overclockAssignments = CopyAssignments(
                overclockAssignments,
                nameof(overclockAssignments));
        }

        public StableId InstanceId { get; }

        public WeaponDefinitionId WeaponDefinitionId { get; }

        public IReadOnlyList<StableId> AugmentAssignments
        {
            get { return augmentAssignments; }
        }

        public IReadOnlyList<StableId> OverclockAssignments
        {
            get { return overclockAssignments; }
        }

        public static WeaponEquipmentInstance Create(
            StableId instanceId,
            WeaponDefinitionId weaponDefinitionId,
            IEnumerable<StableId> augmentAssignments,
            IEnumerable<StableId> overclockAssignments)
        {
            return new WeaponEquipmentInstance(
                instanceId,
                weaponDefinitionId,
                augmentAssignments,
                overclockAssignments);
        }

        public static WeaponEquipmentInstance CreateUnmodified(
            StableId instanceId,
            WeaponDefinitionId weaponDefinitionId)
        {
            return new WeaponEquipmentInstance(
                instanceId,
                weaponDefinitionId,
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
        }

        public WeaponEquipmentInstance WithAssignments(
            IEnumerable<StableId> augments,
            IEnumerable<StableId> overclocks)
        {
            return new WeaponEquipmentInstance(
                InstanceId,
                WeaponDefinitionId,
                augments,
                overclocks);
        }

        public bool Equals(WeaponEquipmentInstance other)
        {
            return !ReferenceEquals(other, null)
                && InstanceId.Equals(other.InstanceId)
                && WeaponDefinitionId.Equals(other.WeaponDefinitionId)
                && AssignmentsEqual(augmentAssignments, other.augmentAssignments)
                && AssignmentsEqual(overclockAssignments, other.overclockAssignments);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponEquipmentInstance);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + InstanceId.GetHashCode();
                hash = (hash * 31) + WeaponDefinitionId.GetHashCode();
                hash = AppendAssignmentHashes(hash, augmentAssignments);
                return AppendAssignmentHashes(hash, overclockAssignments);
            }
        }

        private static ReadOnlyCollection<StableId> CopyAssignments(
            IEnumerable<StableId> assignments,
            string parameterName)
        {
            var copy = new List<StableId>(
                assignments ?? throw new ArgumentNullException(parameterName));
            copy.Sort();

            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                {
                    throw new ArgumentException(
                        "Weapon assignments cannot contain null references.",
                        parameterName);
                }
                if (index > 0 && copy[index - 1].Equals(copy[index]))
                {
                    throw new ArgumentException(
                        "The same assignment reference cannot be installed twice.",
                        parameterName);
                }
            }

            return new ReadOnlyCollection<StableId>(copy);
        }

        private static bool AssignmentsEqual(
            IReadOnlyList<StableId> left,
            IReadOnlyList<StableId> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static int AppendAssignmentHashes(
            int seed,
            IReadOnlyList<StableId> assignments)
        {
            unchecked
            {
                int hash = seed;
                for (int index = 0; index < assignments.Count; index++)
                {
                    hash = (hash * 31) + assignments[index].GetHashCode();
                }
                return hash;
            }
        }
    }

    /// <summary>
    /// Structured fail-closed result shared by trusted weapon boundaries and Inventory presentation.
    /// It reports availability only; it never mutates weapon, receipt, wallet or mount state.
    /// </summary>
    public sealed class WeaponOperationAvailability
    {
        private WeaponOperationAvailability(
            bool isAvailable,
            string rejectionCode,
            string message)
        {
            IsAvailable = isAvailable;
            RejectionCode = rejectionCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsAvailable { get; }
        public string RejectionCode { get; }
        public string Message { get; }

        public static WeaponOperationAvailability Available()
        {
            return new WeaponOperationAvailability(
                true,
                string.Empty,
                string.Empty);
        }

        public static WeaponOperationAvailability Rejected(
            string rejectionCode,
            string message)
        {
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                throw new ArgumentException(
                    "A rejected canonical weapon operation requires a code.",
                    nameof(rejectionCode));
            }
            return new WeaponOperationAvailability(
                false,
                rejectionCode.Trim(),
                message);
        }
    }

    /// <summary>
    /// Safety policy for canonical weapon operations whose product contract is not implemented yet.
    /// Compatibility receipts are observations only and never make an operation available.
    /// </summary>
    public static class WeaponSafetyPolicy
    {
        public static WeaponOperationAvailability EvaluateGenericUpgrade(
            bool isWeaponReceipt,
            bool canonicalDefinitionResolved)
        {
            if (!isWeaponReceipt)
            {
                return WeaponOperationAvailability.Available();
            }
            if (!canonicalDefinitionResolved)
            {
                return DefinitionUnresolved();
            }
            return WeaponOperationAvailability.Rejected(
                "canonical-weapon-upgrade-route-unsupported",
                "Canonical weapon upgrades require the future canonical transaction; the generic equipment replacement route is disabled.");
        }

        public static WeaponOperationAvailability EvaluateRewardAcceptance(
            WeaponEquipmentInstance instance,
            bool canonicalDefinitionResolved)
        {
            if (instance == null || !canonicalDefinitionResolved)
            {
                return DefinitionUnresolved();
            }
            if (instance.OverclockAssignments.Count != 0)
            {
                return OverclockUnsupported();
            }
            return WeaponOperationAvailability.Available();
        }

        public static WeaponOperationAvailability EvaluateLiveExecution(
            WeaponEquipmentInstance instance,
            bool canonicalDefinitionResolved)
        {
            if (instance == null || !canonicalDefinitionResolved)
            {
                return DefinitionUnresolved();
            }
            if (instance.OverclockAssignments.Count != 0)
            {
                return OverclockUnsupported();
            }
            return WeaponOperationAvailability.Available();
        }

        public static WeaponOperationAvailability EvaluateOverclockInstallation()
        {
            return OverclockUnsupported();
        }

        private static WeaponOperationAvailability DefinitionUnresolved()
        {
            return WeaponOperationAvailability.Rejected(
                "canonical-weapon-definition-unresolved",
                "The canonical weapon definition could not be resolved; destructive or replacement operations are blocked.");
        }

        private static WeaponOperationAvailability OverclockUnsupported()
        {
            return WeaponOperationAvailability.Rejected(
                "canonical-weapon-overclock-policy-unsupported",
                "Overclock installation and live execution are not available until a canonical ownership, slot/capacity and runtime policy exists.");
        }
    }

    /// <summary>
    /// Creates opaque globally unique identities for newly accepted owned instances.
    /// The value deliberately carries no weapon, owner, source, slot, or ordering semantics.
    /// </summary>
    public static class OwnedEquipmentInstanceIdFactory
    {
        public static StableId Create()
        {
            return StableId.Create(
                "instance",
                Guid.NewGuid().ToString("N"));
        }
    }
}

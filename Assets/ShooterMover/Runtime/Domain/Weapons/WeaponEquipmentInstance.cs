using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

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

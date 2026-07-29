using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns
{
    /// <summary>
    /// Canonical persistent state for one exact owned gun.
    ///
    /// Authored gun data is resolved through GunDefinitionId and is never copied into
    /// the owned instance. Acquisition and transaction facts belong to the operation that grants
    /// the instance, not to this value.
    /// </summary>
    public sealed class GunItem : IEquatable<GunItem>
    {
        private readonly ReadOnlyCollection<StableId> augmentAssignments;
        private readonly ReadOnlyCollection<StableId> overclockAssignments;

        private GunItem(
            StableId instanceId,
            GunDefinitionId gunDefinitionId,
            IEnumerable<StableId> augmentAssignments,
            IEnumerable<StableId> overclockAssignments)
        {
            InstanceId = instanceId
                ?? throw new ArgumentNullException(nameof(instanceId));
            GunDefinitionId = gunDefinitionId
                ?? throw new ArgumentNullException(nameof(gunDefinitionId));
            this.augmentAssignments = CopyAssignments(
                augmentAssignments,
                nameof(augmentAssignments));
            this.overclockAssignments = CopyAssignments(
                overclockAssignments,
                nameof(overclockAssignments));
        }

        public StableId InstanceId { get; }

        public GunDefinitionId GunDefinitionId { get; }

        public IReadOnlyList<StableId> AugmentAssignments
        {
            get { return augmentAssignments; }
        }

        public IReadOnlyList<StableId> OverclockAssignments
        {
            get { return overclockAssignments; }
        }

        public static GunItem Create(
            StableId instanceId,
            GunDefinitionId gunDefinitionId,
            IEnumerable<StableId> augmentAssignments,
            IEnumerable<StableId> overclockAssignments)
        {
            return new GunItem(
                instanceId,
                gunDefinitionId,
                augmentAssignments,
                overclockAssignments);
        }

        public static GunItem CreateUnmodified(
            StableId instanceId,
            GunDefinitionId gunDefinitionId)
        {
            return new GunItem(
                instanceId,
                gunDefinitionId,
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
        }

        public GunItem WithAssignments(
            IEnumerable<StableId> augments,
            IEnumerable<StableId> overclocks)
        {
            return new GunItem(
                InstanceId,
                GunDefinitionId,
                augments,
                overclocks);
        }

        public bool Equals(GunItem other)
        {
            return !ReferenceEquals(other, null)
                && InstanceId.Equals(other.InstanceId)
                && GunDefinitionId.Equals(other.GunDefinitionId)
                && AssignmentsEqual(augmentAssignments, other.augmentAssignments)
                && AssignmentsEqual(overclockAssignments, other.overclockAssignments);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GunItem);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + InstanceId.GetHashCode();
                hash = (hash * 31) + GunDefinitionId.GetHashCode();
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
                        "Gun assignments cannot contain null references.",
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
    /// Structured fail-closed result shared by trusted gun boundaries and Inventory presentation.
    /// It reports availability only; it never mutates gun, receipt, wallet or mount state.
    /// </summary>
    public sealed class GunOperationAvailability
    {
        private GunOperationAvailability(
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

        public static GunOperationAvailability Available()
        {
            return new GunOperationAvailability(
                true,
                string.Empty,
                string.Empty);
        }

        public static GunOperationAvailability Rejected(
            string rejectionCode,
            string message)
        {
            if (string.IsNullOrWhiteSpace(rejectionCode))
            {
                throw new ArgumentException(
                    "A rejected canonical gun operation requires a code.",
                    nameof(rejectionCode));
            }
            return new GunOperationAvailability(
                false,
                rejectionCode.Trim(),
                message);
        }
    }

    /// <summary>
    /// Safety policy for canonical gun operations whose product contract is not implemented yet.
    /// Compatibility receipts are observations only and never make an operation available.
    /// </summary>
    public static class GunSafetyPolicy
    {
        public static GunOperationAvailability EvaluateGenericUpgrade(
            bool isGunReceipt,
            bool canonicalDefinitionResolved)
        {
            if (!isGunReceipt)
            {
                return GunOperationAvailability.Available();
            }
            if (!canonicalDefinitionResolved)
            {
                return DefinitionUnresolved();
            }
            return GunOperationAvailability.Rejected(
                "canonical-gun-upgrade-route-unsupported",
                "Canonical gun upgrades require the future canonical transaction; the generic equipment replacement route is disabled.");
        }

        public static GunOperationAvailability EvaluateRewardAcceptance(
            GunItem instance,
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
            return GunOperationAvailability.Available();
        }

        public static GunOperationAvailability EvaluateLiveExecution(
            GunItem instance,
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
            return GunOperationAvailability.Available();
        }

        public static GunOperationAvailability EvaluateOverclockInstallation()
        {
            return OverclockUnsupported();
        }

        private static GunOperationAvailability DefinitionUnresolved()
        {
            return GunOperationAvailability.Rejected(
                "canonical-gun-definition-unresolved",
                "The canonical gun definition could not be resolved; destructive or replacement operations are blocked.");
        }

        private static GunOperationAvailability OverclockUnsupported()
        {
            return GunOperationAvailability.Rejected(
                "canonical-gun-overclock-policy-unsupported",
                "Overclock installation and live execution are not available until a canonical ownership, slot/capacity and runtime policy exists.");
        }
    }

    /// <summary>
    /// Creates opaque globally unique identities for newly accepted owned instances.
    /// The value deliberately carries no gun, owner, source, slot, or ordering semantics.
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

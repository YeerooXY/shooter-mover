using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomAccessReferenceKind
    {
        Holding = 1,
        Objective = 2,
        Switch = 3,
        CollectedDrop = 4,
    }

    public enum RoomAccessReferenceSource
    {
        RunHolding = 1,
        ObjectiveDefinition = 2,
        SwitchDefinition = 3,
        AuthoredDropInstance = 4,
        ExternalDropReference = 5,
    }

    public sealed class RoomAccessReferenceRegistration
    {
        public RoomAccessReferenceRegistration(
            StableId referenceStableId,
            RoomAccessReferenceKind kind,
            RoomAccessReferenceSource source)
        {
            ReferenceStableId = referenceStableId
                ?? throw new ArgumentNullException(nameof(referenceStableId));
            if (!Enum.IsDefined(typeof(RoomAccessReferenceKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (!Enum.IsDefined(typeof(RoomAccessReferenceSource), source))
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }
            if (!IsCompatible(kind, source))
            {
                throw new ArgumentException(
                    "room-access-reference-source-incompatible:"
                    + kind
                    + ":"
                    + source
                    + ":"
                    + referenceStableId);
            }

            Kind = kind;
            Source = source;
        }

        public StableId ReferenceStableId { get; }

        public RoomAccessReferenceKind Kind { get; }

        public RoomAccessReferenceSource Source { get; }

        internal static bool IsCompatible(
            RoomAccessReferenceKind kind,
            RoomAccessReferenceSource source)
        {
            switch (kind)
            {
                case RoomAccessReferenceKind.Holding:
                    return source == RoomAccessReferenceSource.RunHolding;
                case RoomAccessReferenceKind.Objective:
                    return source == RoomAccessReferenceSource.ObjectiveDefinition;
                case RoomAccessReferenceKind.Switch:
                    return source == RoomAccessReferenceSource.SwitchDefinition;
                case RoomAccessReferenceKind.CollectedDrop:
                    return source == RoomAccessReferenceSource.AuthoredDropInstance
                        || source == RoomAccessReferenceSource.ExternalDropReference;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Immutable authoring-time validation boundary for non-room references used by
    /// room access conditions. It is not an inventory, objective, switch, reward,
    /// drop, or room runtime authority.
    /// </summary>
    public interface IRoomAccessReferenceRegistry
    {
        IReadOnlyList<RoomAccessReferenceRegistration> Registrations { get; }

        string CanonicalJson { get; }

        string Fingerprint { get; }

        bool ContainsHolding(StableId referenceStableId);

        bool ContainsObjective(StableId referenceStableId);

        bool ContainsSwitch(StableId referenceStableId);

        bool ContainsCollectedDrop(StableId referenceStableId);
    }

    public sealed class RoomAccessReferenceCatalog : IRoomAccessReferenceRegistry
    {
        private readonly ReadOnlyCollection<RoomAccessReferenceRegistration>
            registrations;
        private readonly HashSet<StableId> holdings = new HashSet<StableId>();
        private readonly HashSet<StableId> objectives = new HashSet<StableId>();
        private readonly HashSet<StableId> switches = new HashSet<StableId>();
        private readonly HashSet<StableId> collectedDrops = new HashSet<StableId>();

        public RoomAccessReferenceCatalog(
            IEnumerable<RoomAccessReferenceRegistration> registrations)
        {
            if (registrations == null)
            {
                throw new ArgumentNullException(nameof(registrations));
            }

            var copy = new List<RoomAccessReferenceRegistration>(registrations);
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                {
                    throw new ArgumentException(
                        "Room access reference catalogs cannot contain null registrations.",
                        nameof(registrations));
                }
            }
            copy.Sort(CompareRegistrations);

            for (int index = 0; index < copy.Count; index++)
            {
                RoomAccessReferenceRegistration registration = copy[index];
                HashSet<StableId> target = SetFor(registration.Kind);
                if (!target.Add(registration.ReferenceStableId))
                {
                    throw new ArgumentException(
                        "room-access-reference-duplicate:"
                        + registration.Kind
                        + ":"
                        + registration.ReferenceStableId,
                        nameof(registrations));
                }
            }

            this.registrations =
                new ReadOnlyCollection<RoomAccessReferenceRegistration>(copy);
            CanonicalJson = BuildCanonicalJson();
            Fingerprint = ComputeSha256(CanonicalJson);
        }

        public static RoomAccessReferenceCatalog Empty { get; } =
            new RoomAccessReferenceCatalog(
                Array.Empty<RoomAccessReferenceRegistration>());

        public static RoomAccessReferenceCatalog Snapshot(
            IRoomAccessReferenceRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }
            RoomAccessReferenceCatalog immutable =
                registry as RoomAccessReferenceCatalog;
            if (immutable != null) return immutable;

            var copy = new RoomAccessReferenceCatalog(
                registry.Registrations
                    ?? throw new ArgumentException(
                        "room-access-reference-registry-registrations-missing",
                        nameof(registry)));
            if (!string.Equals(
                registry.Fingerprint,
                copy.Fingerprint,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "room-access-reference-registry-fingerprint-inconsistent",
                    nameof(registry));
            }
            return copy;
        }

        public IReadOnlyList<RoomAccessReferenceRegistration> Registrations =>
            registrations;

        public string CanonicalJson { get; }

        public string Fingerprint { get; }

        public bool ContainsHolding(StableId referenceStableId)
        {
            return referenceStableId != null && holdings.Contains(referenceStableId);
        }

        public bool ContainsObjective(StableId referenceStableId)
        {
            return referenceStableId != null && objectives.Contains(referenceStableId);
        }

        public bool ContainsSwitch(StableId referenceStableId)
        {
            return referenceStableId != null && switches.Contains(referenceStableId);
        }

        public bool ContainsCollectedDrop(StableId referenceStableId)
        {
            return referenceStableId != null && collectedDrops.Contains(referenceStableId);
        }

        private HashSet<StableId> SetFor(RoomAccessReferenceKind kind)
        {
            switch (kind)
            {
                case RoomAccessReferenceKind.Holding:
                    return holdings;
                case RoomAccessReferenceKind.Objective:
                    return objectives;
                case RoomAccessReferenceKind.Switch:
                    return switches;
                case RoomAccessReferenceKind.CollectedDrop:
                    return collectedDrops;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private string BuildCanonicalJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\"version\":1,\"registrations\":[");
            for (int index = 0; index < registrations.Count; index++)
            {
                if (index != 0) builder.Append(',');
                RoomAccessReferenceRegistration value = registrations[index];
                builder.Append("{\"kind\":")
                    .Append(((int)value.Kind).ToString(CultureInfo.InvariantCulture))
                    .Append(",\"reference\":");
                AppendString(builder, value.ReferenceStableId.ToString());
                builder.Append(",\"source\":")
                    .Append(((int)value.Source).ToString(CultureInfo.InvariantCulture))
                    .Append('}');
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private static int CompareRegistrations(
            RoomAccessReferenceRegistration left,
            RoomAccessReferenceRegistration right)
        {
            int kind = ((int)left.Kind).CompareTo((int)right.Kind);
            if (kind != 0) return kind;
            int reference = left.ReferenceStableId.CompareTo(right.ReferenceStableId);
            if (reference != 0) return reference;
            return ((int)left.Source).CompareTo((int)right.Source);
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if (character == '"') builder.Append("\\\"");
                else if (character == '\\') builder.Append("\\\\");
                else if (character == '\n') builder.Append("\\n");
                else if (character == '\r') builder.Append("\\r");
                else if (character == '\t') builder.Append("\\t");
                else builder.Append(character);
            }
            builder.Append('"');
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }
    }
}

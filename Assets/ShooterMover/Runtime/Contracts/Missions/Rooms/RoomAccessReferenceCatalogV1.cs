using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomAccessReferenceKindV1
    {
        Holding = 1,
        Objective = 2,
        Switch = 3,
        CollectedDrop = 4,
    }

    public enum RoomAccessReferenceSourceV1
    {
        RunHolding = 1,
        ObjectiveDefinition = 2,
        SwitchDefinition = 3,
        AuthoredDropInstance = 4,
        ExternalDropReference = 5,
    }

    public sealed class RoomAccessReferenceRegistrationV1
    {
        public RoomAccessReferenceRegistrationV1(
            StableId referenceStableId,
            RoomAccessReferenceKindV1 kind,
            RoomAccessReferenceSourceV1 source)
        {
            ReferenceStableId = referenceStableId
                ?? throw new ArgumentNullException(nameof(referenceStableId));
            if (!Enum.IsDefined(typeof(RoomAccessReferenceKindV1), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (!Enum.IsDefined(typeof(RoomAccessReferenceSourceV1), source))
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

        public RoomAccessReferenceKindV1 Kind { get; }

        public RoomAccessReferenceSourceV1 Source { get; }

        internal static bool IsCompatible(
            RoomAccessReferenceKindV1 kind,
            RoomAccessReferenceSourceV1 source)
        {
            switch (kind)
            {
                case RoomAccessReferenceKindV1.Holding:
                    return source == RoomAccessReferenceSourceV1.RunHolding;
                case RoomAccessReferenceKindV1.Objective:
                    return source == RoomAccessReferenceSourceV1.ObjectiveDefinition;
                case RoomAccessReferenceKindV1.Switch:
                    return source == RoomAccessReferenceSourceV1.SwitchDefinition;
                case RoomAccessReferenceKindV1.CollectedDrop:
                    return source == RoomAccessReferenceSourceV1.AuthoredDropInstance
                        || source == RoomAccessReferenceSourceV1.ExternalDropReference;
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
    public interface IRoomAccessReferenceRegistryV1
    {
        IReadOnlyList<RoomAccessReferenceRegistrationV1> Registrations { get; }

        string CanonicalJson { get; }

        string Fingerprint { get; }

        bool ContainsHolding(StableId referenceStableId);

        bool ContainsObjective(StableId referenceStableId);

        bool ContainsSwitch(StableId referenceStableId);

        bool ContainsCollectedDrop(StableId referenceStableId);
    }

    public sealed class RoomAccessReferenceCatalogV1 : IRoomAccessReferenceRegistryV1
    {
        private readonly ReadOnlyCollection<RoomAccessReferenceRegistrationV1>
            registrations;
        private readonly HashSet<StableId> holdings = new HashSet<StableId>();
        private readonly HashSet<StableId> objectives = new HashSet<StableId>();
        private readonly HashSet<StableId> switches = new HashSet<StableId>();
        private readonly HashSet<StableId> collectedDrops = new HashSet<StableId>();

        public RoomAccessReferenceCatalogV1(
            IEnumerable<RoomAccessReferenceRegistrationV1> registrations)
        {
            if (registrations == null)
            {
                throw new ArgumentNullException(nameof(registrations));
            }

            var copy = new List<RoomAccessReferenceRegistrationV1>(registrations);
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
                RoomAccessReferenceRegistrationV1 registration = copy[index];
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
                new ReadOnlyCollection<RoomAccessReferenceRegistrationV1>(copy);
            CanonicalJson = BuildCanonicalJson();
            Fingerprint = ComputeSha256(CanonicalJson);
        }

        public static RoomAccessReferenceCatalogV1 Empty { get; } =
            new RoomAccessReferenceCatalogV1(
                Array.Empty<RoomAccessReferenceRegistrationV1>());

        public static RoomAccessReferenceCatalogV1 Snapshot(
            IRoomAccessReferenceRegistryV1 registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }
            RoomAccessReferenceCatalogV1 immutable =
                registry as RoomAccessReferenceCatalogV1;
            if (immutable != null) return immutable;

            var copy = new RoomAccessReferenceCatalogV1(
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

        public IReadOnlyList<RoomAccessReferenceRegistrationV1> Registrations =>
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

        private HashSet<StableId> SetFor(RoomAccessReferenceKindV1 kind)
        {
            switch (kind)
            {
                case RoomAccessReferenceKindV1.Holding:
                    return holdings;
                case RoomAccessReferenceKindV1.Objective:
                    return objectives;
                case RoomAccessReferenceKindV1.Switch:
                    return switches;
                case RoomAccessReferenceKindV1.CollectedDrop:
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
                RoomAccessReferenceRegistrationV1 value = registrations[index];
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
            RoomAccessReferenceRegistrationV1 left,
            RoomAccessReferenceRegistrationV1 right)
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

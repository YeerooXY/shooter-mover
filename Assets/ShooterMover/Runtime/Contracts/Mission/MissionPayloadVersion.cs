using System;
using System.Globalization;
using ShooterMover.Contracts.Identity;

namespace ShooterMover.Contracts.Mission
{
    /// <summary>
    /// Immutable mission payload identity. The contract version identifies the
    /// envelope schema while ContentVersion identifies the content definitions
    /// whose StableIds the payload may reference.
    /// </summary>
    public sealed class MissionPayloadVersion : IEquatable<MissionPayloadVersion>
    {
        public MissionPayloadVersion(int contractVersion, ContentVersion contentVersion)
        {
            if (contractVersion < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(contractVersion),
                    contractVersion,
                    "Mission payload contract versions must be positive integers.");
            }

            if (contentVersion == null)
            {
                throw new ArgumentNullException(nameof(contentVersion));
            }

            ContractVersion = contractVersion;
            ContentVersion = contentVersion;
        }

        public int ContractVersion { get; }

        public ContentVersion ContentVersion { get; }

        public string ToCanonicalString()
        {
            return "mission_contract_version="
                + ContractVersion.ToString(CultureInfo.InvariantCulture)
                + "\ncontent_catalog_version="
                + ContentVersion.CatalogVersion.ToString(CultureInfo.InvariantCulture)
                + "\ncontent_definition_fingerprint="
                + ContentVersion.DefinitionFingerprint;
        }

        public bool Equals(MissionPayloadVersion other)
        {
            return !ReferenceEquals(other, null)
                && ContractVersion == other.ContractVersion
                && ContentVersion.Equals(other.ContentVersion);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MissionPayloadVersion);
        }

        public override int GetHashCode()
        {
            return MissionContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        public static bool operator ==(MissionPayloadVersion left, MissionPayloadVersion right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(MissionPayloadVersion left, MissionPayloadVersion right)
        {
            return !(left == right);
        }
    }
}

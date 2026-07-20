using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public enum PropRuntimeCreationStatusV1
    {
        Created = 0,
        MissingDefinition = 1,
        MissingDamagePolicy = 2,
        InvalidRequest = 3
    }

    public sealed class PropRuntimeCreationResultV1
    {
        internal PropRuntimeCreationResultV1(
            PropRuntimeCreationStatusV1 status,
            PropRuntimeV1 runtime,
            string diagnostic)
        {
            Status = status;
            Runtime = runtime;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PropRuntimeCreationStatusV1 Status { get; }
        public PropRuntimeV1 Runtime { get; }
        public string Diagnostic { get; }

        public bool IsCreated
        {
            get { return Status == PropRuntimeCreationStatusV1.Created; }
        }
    }

    public interface IPropRuntimeFactoryV1
    {
        PropRuntimeCreationResultV1 Create(
            PropCatalogV1 catalog,
            PropPlacementV1 placement,
            IPropDamageEligibilityPolicyV1 damagePolicy);
    }

    public sealed class PropRuntimeFactoryV1 : IPropRuntimeFactoryV1
    {
        public PropRuntimeCreationResultV1 Create(
            PropCatalogV1 catalog,
            PropPlacementV1 placement,
            IPropDamageEligibilityPolicyV1 damagePolicy)
        {
            if (catalog == null || placement == null)
            {
                return new PropRuntimeCreationResultV1(
                    PropRuntimeCreationStatusV1.InvalidRequest,
                    null,
                    "Catalog and placement are required.");
            }

            PropDefinitionV1 definition;
            if (!catalog.TryGet(placement.DefinitionId, out definition))
            {
                return new PropRuntimeCreationResultV1(
                    PropRuntimeCreationStatusV1.MissingDefinition,
                    null,
                    "Prop definition '" + placement.DefinitionId
                    + "' is missing from the catalog.");
            }

            if (PropCatalogV1.Has(
                    definition,
                    PropCapabilityIdsV1.Destructibility)
                && damagePolicy == null)
            {
                return new PropRuntimeCreationResultV1(
                    PropRuntimeCreationStatusV1.MissingDamagePolicy,
                    null,
                    "Combat-capable props require an injected damage policy.");
            }

            return new PropRuntimeCreationResultV1(
                PropRuntimeCreationStatusV1.Created,
                new PropRuntimeV1(placement, definition, damagePolicy),
                "Prop runtime created from immutable definition and placement.");
        }
    }

    internal static class PropFingerprintV1
    {
        public static string Compute64Hex(string text)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
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
}

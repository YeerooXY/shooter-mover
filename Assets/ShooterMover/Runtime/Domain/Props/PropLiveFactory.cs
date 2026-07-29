using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Props
{
    public enum PropLiveCreationStatus
    {
        Created = 0,
        MissingDefinition = 1,
        MissingDamagePolicy = 2,
        InvalidRequest = 3
    }

    public sealed class PropLiveCreationResult
    {
        internal PropLiveCreationResult(
            PropLiveCreationStatus status,
            PropLive runtime,
            string diagnostic)
        {
            Status = status;
            Runtime = runtime;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public PropLiveCreationStatus Status { get; }
        public PropLive Runtime { get; }
        public string Diagnostic { get; }

        public bool IsCreated
        {
            get { return Status == PropLiveCreationStatus.Created; }
        }
    }

    public interface IPropLiveFactory
    {
        PropLiveCreationResult Create(
            PropCatalog catalog,
            PropPlacement placement,
            IPropDamageEligibilityPolicy damagePolicy);
    }

    public sealed class PropLiveFactory : IPropLiveFactory
    {
        public PropLiveCreationResult Create(
            PropCatalog catalog,
            PropPlacement placement,
            IPropDamageEligibilityPolicy damagePolicy)
        {
            if (catalog == null || placement == null)
            {
                return new PropLiveCreationResult(
                    PropLiveCreationStatus.InvalidRequest,
                    null,
                    "Catalog and placement are required.");
            }

            PropDefinition definition;
            if (!catalog.TryGet(placement.DefinitionId, out definition))
            {
                return new PropLiveCreationResult(
                    PropLiveCreationStatus.MissingDefinition,
                    null,
                    "Prop definition '" + placement.DefinitionId
                    + "' is missing from the catalog.");
            }

            if (PropCatalog.Has(
                    definition,
                    PropCapabilityIds.Destructibility)
                && damagePolicy == null)
            {
                return new PropLiveCreationResult(
                    PropLiveCreationStatus.MissingDamagePolicy,
                    null,
                    "Combat-capable props require an injected damage policy.");
            }

            return new PropLiveCreationResult(
                PropLiveCreationStatus.Created,
                new PropLive(placement, definition, damagePolicy),
                "Prop runtime created from immutable definition and placement.");
        }
    }

    internal static class PropFingerprint
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

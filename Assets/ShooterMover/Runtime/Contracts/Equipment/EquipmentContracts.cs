using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Contracts.Equipment
{
    /// <summary>
    /// Supplies one validated immutable equipment catalog to generation, holdings,
    /// crafting, shop, upgrade, and simulation consumers.
    /// </summary>
    public interface IEquipmentCatalogProvider
    {
        EquipmentCatalog Catalog { get; }
    }

    /// <summary>
    /// Engine-independent validation port. Consumers submit immutable instances and
    /// receive deterministic rejection details without mutating the catalog or item.
    /// </summary>
    public interface IEquipmentInstanceValidator
    {
        EquipmentInstanceValidationResponse Validate(EquipmentInstanceValidationRequest request);
    }

    public sealed class EquipmentInstanceValidationRequest
    {
        public EquipmentInstanceValidationRequest(EquipmentInstance instance)
        {
            Instance = instance;
        }

        public EquipmentInstance Instance { get; }
    }

    public sealed class EquipmentInstanceValidationResponse
    {
        private readonly ReadOnlyCollection<EquipmentModelIssue> issues;

        public EquipmentInstanceValidationResponse(
            bool isValid,
            string catalogFingerprint,
            string instanceFingerprint,
            IEnumerable<EquipmentModelIssue> issues)
        {
            if (catalogFingerprint == null)
            {
                throw new ArgumentNullException(nameof(catalogFingerprint));
            }

            IsValid = isValid;
            CatalogFingerprint = catalogFingerprint;
            InstanceFingerprint = instanceFingerprint;
            this.issues = new ReadOnlyCollection<EquipmentModelIssue>(
                new List<EquipmentModelIssue>(issues ?? new EquipmentModelIssue[0]));
        }

        public bool IsValid { get; }
        public string CatalogFingerprint { get; }
        public string InstanceFingerprint { get; }
        public IReadOnlyList<EquipmentModelIssue> Issues { get { return issues; } }

        public static EquipmentInstanceValidationResponse From(
            EquipmentCatalog catalog,
            EquipmentInstance instance,
            EquipmentValidationResult validation)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (validation == null)
            {
                throw new ArgumentNullException(nameof(validation));
            }

            return new EquipmentInstanceValidationResponse(
                validation.IsValid,
                catalog.Fingerprint,
                instance == null ? null : instance.Fingerprint,
                validation.Issues);
        }
    }

    /// <summary>
    /// Canonical immutable catalog projection suitable for diagnostics and
    /// persistence/version envelopes. It does not expose mutable Unity assets.
    /// </summary>
    public sealed class EquipmentCatalogSnapshot
    {
        private readonly ReadOnlyCollection<string> equipmentDefinitionIds;
        private readonly ReadOnlyCollection<string> augmentDefinitionIds;

        private EquipmentCatalogSnapshot(
            string fingerprint,
            IEnumerable<string> equipmentDefinitionIds,
            IEnumerable<string> augmentDefinitionIds)
        {
            Fingerprint = fingerprint;
            this.equipmentDefinitionIds = new ReadOnlyCollection<string>(new List<string>(equipmentDefinitionIds));
            this.augmentDefinitionIds = new ReadOnlyCollection<string>(new List<string>(augmentDefinitionIds));
        }

        public string Fingerprint { get; }
        public IReadOnlyList<string> EquipmentDefinitionIds { get { return equipmentDefinitionIds; } }
        public IReadOnlyList<string> AugmentDefinitionIds { get { return augmentDefinitionIds; } }

        public static EquipmentCatalogSnapshot FromCatalog(EquipmentCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            List<string> equipmentIds = new List<string>();
            for (int index = 0; index < catalog.EquipmentDefinitions.Count; index++)
            {
                equipmentIds.Add(catalog.EquipmentDefinitions[index].DefinitionId.ToString());
            }

            List<string> augmentIds = new List<string>();
            for (int index = 0; index < catalog.AugmentDefinitions.Count; index++)
            {
                augmentIds.Add(catalog.AugmentDefinitions[index].DefinitionId.ToString());
            }

            return new EquipmentCatalogSnapshot(catalog.Fingerprint, equipmentIds, augmentIds);
        }
    }
}

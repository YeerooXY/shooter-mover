using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;

namespace ShooterMover.Application.Flow.Hub
{
    public enum StarterRouteProfileStatusV1
    {
        Created = 1,
        ExactRetry = 2,
        InvalidRequest = 3,
        MissingEquipmentDefinition = 4,
        HoldingsRejected = 5,
    }

    public sealed class StarterRouteProfileRequestV1
    {
        private readonly ReadOnlyCollection<StableId> equipmentDefinitionStableIds;

        public StarterRouteProfileRequestV1(
            StableId characterStableId,
            StableId loadoutProfileStableId,
            IEnumerable<StableId> equipmentDefinitionStableIds,
            StableId qualityStableId,
            int equipmentLevel)
        {
            CharacterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));
            QualityStableId = qualityStableId
                ?? throw new ArgumentNullException(nameof(qualityStableId));
            if (equipmentLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(equipmentLevel));
            }

            var definitions = new List<StableId>(
                equipmentDefinitionStableIds
                ?? throw new ArgumentNullException(nameof(equipmentDefinitionStableIds)));
            if (definitions.Count != PlayerRouteProfilePayloadV1.WeaponSlotCount)
            {
                throw new ArgumentException(
                    "Exactly four starter equipment definitions are required.",
                    nameof(equipmentDefinitionStableIds));
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] == null)
                {
                    throw new ArgumentException(
                        "Starter equipment definition identities cannot contain null.",
                        nameof(equipmentDefinitionStableIds));
                }
            }

            equipmentDefinitionStableIds = new ReadOnlyCollection<StableId>(definitions);
            this.equipmentDefinitionStableIds =
                (ReadOnlyCollection<StableId>)equipmentDefinitionStableIds;
            EquipmentLevel = equipmentLevel;
        }

        public StableId CharacterStableId { get; }
        public StableId LoadoutProfileStableId { get; }
        public IReadOnlyList<StableId> EquipmentDefinitionStableIds
        {
            get { return equipmentDefinitionStableIds; }
        }
        public StableId QualityStableId { get; }
        public int EquipmentLevel { get; }
    }

    public sealed class StarterRouteProfileResultV1
    {
        internal StarterRouteProfileResultV1(
            StarterRouteProfileStatusV1 status,
            string rejectionCode,
            PlayerRouteProfilePayloadV1 routePayload,
            IEnumerable<EquipmentInstance> equipmentInstances)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            RoutePayload = routePayload;
            EquipmentInstances = new ReadOnlyCollection<EquipmentInstance>(
                new List<EquipmentInstance>(
                    equipmentInstances ?? Array.Empty<EquipmentInstance>()));
        }

        public StarterRouteProfileStatusV1 Status { get; }
        public string RejectionCode { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public IReadOnlyList<EquipmentInstance> EquipmentInstances { get; }
        public bool Succeeded
        {
            get
            {
                return Status == StarterRouteProfileStatusV1.Created
                    || Status == StarterRouteProfileStatusV1.ExactRetry;
            }
        }
    }

    /// <summary>
    /// Creates the canonical four-slot starter profile through the existing holdings
    /// authority. Stable operation and instance identities make repeated composition an
    /// exact no-op rather than a second grant.
    /// </summary>
    public sealed class StarterRouteProfileFactoryV1
    {
        public StarterRouteProfileResultV1 CreateOrRestore(
            PlayerHoldingsService holdings,
            EquipmentCatalog catalog,
            StarterRouteProfileRequestV1 request)
        {
            if (holdings == null || catalog == null || request == null)
            {
                return Reject(
                    StarterRouteProfileStatusV1.InvalidRequest,
                    "starter-route-request-invalid");
            }

            var instances = new List<EquipmentInstance>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            var instanceIds = new List<StableId>(
                PlayerRouteProfilePayloadV1.WeaponSlotCount);
            bool exactRetry = true;

            for (int index = 0;
                index < request.EquipmentDefinitionStableIds.Count;
                index++)
            {
                StableId definitionId = request.EquipmentDefinitionStableIds[index];
                if (!ContainsDefinition(catalog, definitionId))
                {
                    return Reject(
                        StarterRouteProfileStatusV1.MissingEquipmentDefinition,
                        "starter-route-equipment-definition-missing");
                }

                string identitySuffix = request.LoadoutProfileStableId.Value
                    + "-slot-"
                    + (index + 1);
                StableId instanceId = StableId.Create(
                    "equipment-instance",
                    identitySuffix);
                EquipmentInstance instance = EquipmentInstance.Create(
                    instanceId,
                    definitionId,
                    request.EquipmentLevel,
                    request.QualityStableId,
                    Array.Empty<AugmentInstance>());
                StableId operationId = StableId.Create(
                    "operation",
                    identitySuffix + "-starter-grant");
                PlayerHoldingsMutationResultV1 mutation = holdings.Apply(
                    PlayerHoldingsCommandV1.AddEquipment(
                        StableId.Create(
                            "transaction",
                            identitySuffix + "-starter-grant"),
                        operationId,
                        holdings.AuthorityStableId,
                        instance,
                        HoldingProvenanceV1.Create(
                            StableId.Create(
                                "grant",
                                identitySuffix + "-starter-grant"),
                            StableId.Parse("source.character-starter"))));

                if (mutation.Status != PlayerHoldingsMutationStatusV1.Applied
                    && mutation.Status
                        != PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange)
                {
                    return Reject(
                        StarterRouteProfileStatusV1.HoldingsRejected,
                        string.IsNullOrWhiteSpace(mutation.RejectionCode)
                            ? "starter-route-holdings-rejected"
                            : mutation.RejectionCode);
                }

                exactRetry = exactRetry
                    && mutation.Status
                        == PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange;
                instances.Add(instance);
                instanceIds.Add(instanceId);
            }

            PlayerRouteProfilePayloadV1 payload = PlayerRouteProfilePayloadV1.Create(
                request.CharacterStableId,
                request.LoadoutProfileStableId,
                instanceIds);
            return new StarterRouteProfileResultV1(
                exactRetry
                    ? StarterRouteProfileStatusV1.ExactRetry
                    : StarterRouteProfileStatusV1.Created,
                string.Empty,
                payload,
                instances);
        }

        private static bool ContainsDefinition(
            EquipmentCatalog catalog,
            StableId definitionId)
        {
            for (int index = 0; index < catalog.EquipmentDefinitions.Count; index++)
            {
                if (catalog.EquipmentDefinitions[index].DefinitionId == definitionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static StarterRouteProfileResultV1 Reject(
            StarterRouteProfileStatusV1 status,
            string rejectionCode)
        {
            return new StarterRouteProfileResultV1(
                status,
                rejectionCode,
                null,
                Array.Empty<EquipmentInstance>());
        }
    }
}

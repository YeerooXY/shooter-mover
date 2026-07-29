using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Compatibility boundary for the retained scheduler contract. Resolution always begins with
    /// the exact canonical instance and its GunDefinitionId. It never substitutes another held
    /// gun or fabricates a fallback definition. For migrated augment assignments, the immutable
    /// generic reward receipt may supply the existing augment-instance payload, but canonical
    /// holdings remain the sole ownership and selection authority.
    /// </summary>
    public sealed class GunEquipmentViewLookup :
        IPlayerEquipmentInstanceLookup
    {
        private readonly GunInventoryState holdings;
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly IPlayerHoldingsState immutableReceipts;

        public GunEquipmentViewLookup(
            GunInventoryState gunHoldings,
            EquipmentCatalog equipmentCatalog)
            : this(gunHoldings, equipmentCatalog, null)
        {
        }

        public GunEquipmentViewLookup(
            GunInventoryState gunHoldings,
            EquipmentCatalog equipmentCatalog,
            IPlayerHoldingsState immutableGenericReceipts)
        {
            holdings = gunHoldings
                ?? throw new ArgumentNullException(nameof(gunHoldings));
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            immutableReceipts = immutableGenericReceipts;
            LastAvailability = GunOperationAvailability.Available();
        }

        public GunOperationAvailability LastAvailability
        {
            get;
            private set;
        }

        public bool TryResolve(
            EquipmentInstanceId equipmentInstanceId,
            out EquipmentInstance equipmentInstance)
        {
            equipmentInstance = null;
            if (equipmentInstanceId == null)
            {
                LastAvailability = GunOperationAvailability.Rejected(
                    "canonical-gun-instance-id-missing",
                    "A canonical gun instance ID is required for live execution.");
                return false;
            }

            GunItem canonical = holdings.Find(
                equipmentInstanceId.Value);
            if (canonical == null)
            {
                LastAvailability = GunOperationAvailability.Rejected(
                    "canonical-gun-instance-missing",
                    "The exact canonical gun instance is not owned.");
                return false;
            }

            GunMark mark;
            bool definitionResolved = GunCatalogProvider.Current.TryGetMark(
                    canonical.GunDefinitionId.Value,
                    out mark)
                && mark != null;
            LastAvailability = GunSafetyPolicy.EvaluateLiveExecution(
                canonical,
                definitionResolved);
            if (!LastAvailability.IsAvailable)
            {
                return false;
            }

            EquipmentDefinition definition =
                equipmentCatalog.FindEquipmentDefinition(
                    mark.EquipmentDefinitionId);
            if (!DefinitionMatchesCanonical(definition, canonical)
                || definition.QualityTiers == null
                || definition.QualityTiers.Count == 0)
            {
                LastAvailability = GunOperationAvailability.Rejected(
                    "canonical-gun-compatibility-definition-mismatch",
                    "The compatibility equipment definition does not match the canonical gun definition.");
                return false;
            }

            EquipmentInstance projection;
            if (canonical.AugmentAssignments.Count == 0)
            {
                projection = EquipmentInstance.Create(
                    canonical.InstanceId,
                    definition.DefinitionId,
                    definition.ItemLevelRange.Minimum,
                    definition.QualityTiers[0].QualityId,
                    Array.Empty<AugmentInstance>());
            }
            else if (!TryResolveReceiptProjection(
                canonical,
                definition,
                out projection))
            {
                LastAvailability = GunOperationAvailability.Rejected(
                    "canonical-gun-augment-receipt-unresolved",
                    "The exact immutable augment receipt could not be reconciled with canonical assignments.");
                return false;
            }

            EquipmentValidationResult validation =
                equipmentCatalog.ValidateInstance(projection);
            if (validation == null || !validation.IsValid)
            {
                LastAvailability = GunOperationAvailability.Rejected(
                    "canonical-gun-compatibility-projection-invalid",
                    "The compatibility projection failed equipment validation.");
                return false;
            }

            equipmentInstance = projection;
            LastAvailability = GunOperationAvailability.Available();
            return true;
        }

        private bool TryResolveReceiptProjection(
            GunItem canonical,
            EquipmentDefinition expectedDefinition,
            out EquipmentInstance projection)
        {
            projection = null;
            if (immutableReceipts == null)
            {
                return false;
            }

            PlayerHoldingsSnapshot snapshot =
                immutableReceipts.ExportSnapshot();
            if (snapshot == null)
            {
                return false;
            }

            for (int index = 0;
                 index < snapshot.UniqueHoldings.Count;
                 index++)
            {
                UniqueHoldingSnapshot holding =
                    snapshot.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind
                        != RewardGrantKind.EquipmentReference
                    || holding.InstanceStableId != canonical.InstanceId
                    || holding.EquipmentInstance == null)
                {
                    continue;
                }

                EquipmentInstance candidate = holding.EquipmentInstance;
                if (candidate.InstanceId != canonical.InstanceId
                    || candidate.DefinitionId
                        != expectedDefinition.DefinitionId
                    || !AssignmentsMatch(
                        canonical.AugmentAssignments,
                        candidate.Augments))
                {
                    return false;
                }

                projection = candidate;
                return true;
            }
            return false;
        }

        private static bool DefinitionMatchesCanonical(
            EquipmentDefinition definition,
            GunItem canonical)
        {
            return definition != null
                && definition.RuntimeGunReferenceId != null
                && string.Equals(
                    GunDefinitionId.FromRuntimeReference(
                        definition.RuntimeGunReferenceId).Value,
                    canonical.GunDefinitionId.Value,
                    StringComparison.Ordinal);
        }

        private static bool AssignmentsMatch(
            IReadOnlyList<ShooterMover.Domain.Common.StableId> canonical,
            IReadOnlyList<AugmentInstance> receiptAugments)
        {
            if (canonical == null
                || receiptAugments == null
                || canonical.Count != receiptAugments.Count)
            {
                return false;
            }

            var expected = new HashSet<ShooterMover.Domain.Common.StableId>(
                canonical);
            for (int index = 0; index < receiptAugments.Count; index++)
            {
                AugmentInstance augment = receiptAugments[index];
                if (augment == null || !expected.Remove(augment.InstanceId))
                {
                    return false;
                }
            }
            return expected.Count == 0;
        }
    }
}

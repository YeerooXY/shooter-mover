using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Compatibility boundary for the retained scheduler contract. Resolution always begins with
    /// the exact canonical instance and its WeaponDefinitionId. It never substitutes another held
    /// weapon or fabricates a fallback definition. For migrated augment assignments, the immutable
    /// generic reward receipt may supply the existing augment-instance payload, but canonical
    /// holdings remain the sole ownership and selection authority.
    /// </summary>
    public sealed class WeaponEquipmentViewLookup :
        IPlayerEquipmentInstanceLookup
    {
        private readonly WeaponHoldingsState holdings;
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly IPlayerHoldingsState immutableReceipts;

        public WeaponEquipmentViewLookup(
            WeaponHoldingsState weaponHoldings,
            EquipmentCatalog equipmentCatalog)
            : this(weaponHoldings, equipmentCatalog, null)
        {
        }

        public WeaponEquipmentViewLookup(
            WeaponHoldingsState weaponHoldings,
            EquipmentCatalog equipmentCatalog,
            IPlayerHoldingsState immutableGenericReceipts)
        {
            holdings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            this.equipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));
            immutableReceipts = immutableGenericReceipts;
            LastAvailability = WeaponOperationAvailability.Available();
        }

        public WeaponOperationAvailability LastAvailability
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
                LastAvailability = WeaponOperationAvailability.Rejected(
                    "canonical-weapon-instance-id-missing",
                    "A canonical weapon instance ID is required for live execution.");
                return false;
            }

            WeaponEquipmentInstance canonical = holdings.Find(
                equipmentInstanceId.Value);
            if (canonical == null)
            {
                LastAvailability = WeaponOperationAvailability.Rejected(
                    "canonical-weapon-instance-missing",
                    "The exact canonical weapon instance is not owned.");
                return false;
            }

            WeaponMark mark;
            bool definitionResolved = WeaponCatalogProvider.Current.TryGetMark(
                    canonical.WeaponDefinitionId.Value,
                    out mark)
                && mark != null;
            LastAvailability = WeaponSafetyPolicy.EvaluateLiveExecution(
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
                LastAvailability = WeaponOperationAvailability.Rejected(
                    "canonical-weapon-compatibility-definition-mismatch",
                    "The compatibility equipment definition does not match the canonical weapon definition.");
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
                LastAvailability = WeaponOperationAvailability.Rejected(
                    "canonical-weapon-augment-receipt-unresolved",
                    "The exact immutable augment receipt could not be reconciled with canonical assignments.");
                return false;
            }

            EquipmentValidationResult validation =
                equipmentCatalog.ValidateInstance(projection);
            if (validation == null || !validation.IsValid)
            {
                LastAvailability = WeaponOperationAvailability.Rejected(
                    "canonical-weapon-compatibility-projection-invalid",
                    "The compatibility projection failed equipment validation.");
                return false;
            }

            equipmentInstance = projection;
            LastAvailability = WeaponOperationAvailability.Available();
            return true;
        }

        private bool TryResolveReceiptProjection(
            WeaponEquipmentInstance canonical,
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
            WeaponEquipmentInstance canonical)
        {
            return definition != null
                && definition.RuntimeWeaponReferenceId != null
                && string.Equals(
                    WeaponDefinitionId.FromRuntimeReference(
                        definition.RuntimeWeaponReferenceId).Value,
                    canonical.WeaponDefinitionId.Value,
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

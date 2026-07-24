using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public interface IWeaponBlueprintMappingPolicyResolver
    {
        bool TryResolve(
            WeaponDefinitionId definitionId,
            out WeaponCatalogBlueprintMappingIntent mappingIntent);
    }

    /// <summary>
    /// One explicit composition-level mapping authority keyed by canonical weapon definition ID.
    /// It stores semantic decisions that cannot be inferred losslessly from WeaponDefinitionData.
    /// </summary>
    public sealed class WeaponBlueprintMappingPolicyRegistry :
        IWeaponBlueprintMappingPolicyResolver
    {
        private readonly Dictionary<string, WeaponCatalogBlueprintMappingIntent>
            intents = new Dictionary<string, WeaponCatalogBlueprintMappingIntent>(
                StringComparer.Ordinal);

        public WeaponBlueprintMappingPolicyRegistry(
            IEnumerable<WeaponCatalogBlueprintMappingIntent> mappingIntents)
        {
            if (mappingIntents == null)
            {
                throw new ArgumentNullException(nameof(mappingIntents));
            }

            foreach (WeaponCatalogBlueprintMappingIntent intent in mappingIntents)
            {
                if (intent == null || intent.ExpectedDefinitionId == null)
                {
                    throw new ArgumentException(
                        "Every mapping policy requires a canonical definition identity.",
                        nameof(mappingIntents));
                }
                if (intents.ContainsKey(intent.ExpectedDefinitionId.Value))
                {
                    throw new ArgumentException(
                        "Only one mapping policy may own a weapon definition.",
                        nameof(mappingIntents));
                }

                intents.Add(intent.ExpectedDefinitionId.Value, intent);
            }
        }

        public int Count { get { return intents.Count; } }

        public bool TryResolve(
            WeaponDefinitionId definitionId,
            out WeaponCatalogBlueprintMappingIntent mappingIntent)
        {
            if (definitionId == null)
            {
                mappingIntent = null;
                return false;
            }

            return intents.TryGetValue(definitionId.Value, out mappingIntent);
        }
    }

    public interface IWeaponAugmentModifierSetResolver
    {
        bool TryResolve(
            EquipmentInstance equipmentInstance,
            EquipmentCatalog equipmentCatalog,
            out IReadOnlyList<WeaponAugmentModifierSet> modifierSets,
            out string rejectionCode);
    }

    /// <summary>
    /// Default prototype composition for unaugmented equipment. Installed augments are rejected
    /// until the caller supplies the canonical application policy that maps each exact augment
    /// instance to one WeaponAugmentModifierSet.
    /// </summary>
    public sealed class UnaugmentedWeaponModifierSetResolver :
        IWeaponAugmentModifierSetResolver
    {
        private static readonly ReadOnlyCollection<WeaponAugmentModifierSet> Empty =
            new ReadOnlyCollection<WeaponAugmentModifierSet>(
                new List<WeaponAugmentModifierSet>());

        public bool TryResolve(
            EquipmentInstance equipmentInstance,
            EquipmentCatalog equipmentCatalog,
            out IReadOnlyList<WeaponAugmentModifierSet> modifierSets,
            out string rejectionCode)
        {
            modifierSets = null;
            if (equipmentInstance == null || equipmentCatalog == null)
            {
                rejectionCode = "weapon-live-augment-resolution-input-invalid";
                return false;
            }
            if (equipmentInstance.Augments.Count != 0)
            {
                rejectionCode = "weapon-live-augment-policy-missing";
                return false;
            }

            modifierSets = Empty;
            rejectionCode = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Resolves one exact immutable EffectiveWeapon from the existing equipment and catalog
    /// authorities. It never substitutes equipment, guesses missing mapping semantics, or applies
    /// item-level combat scaling.
    /// </summary>
    public sealed class InventoryWeaponEffectiveResolver
    {
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly WeaponCatalog weaponCatalog;
        private readonly IWeaponBlueprintMappingPolicyResolver mappingPolicies;
        private readonly IWeaponAugmentModifierSetResolver augmentModifiers;

        public InventoryWeaponEffectiveResolver(
            EquipmentCatalog equipmentDefinitions,
            WeaponCatalog weaponDefinitions,
            IWeaponBlueprintMappingPolicyResolver mappingPolicyResolver,
            IWeaponAugmentModifierSetResolver augmentModifierResolver)
        {
            equipmentCatalog = equipmentDefinitions
                ?? throw new ArgumentNullException(nameof(equipmentDefinitions));
            weaponCatalog = weaponDefinitions
                ?? throw new ArgumentNullException(nameof(weaponDefinitions));
            mappingPolicies = mappingPolicyResolver
                ?? throw new ArgumentNullException(nameof(mappingPolicyResolver));
            augmentModifiers = augmentModifierResolver
                ?? throw new ArgumentNullException(nameof(augmentModifierResolver));
        }

        public bool TryResolve(
            EquipmentInstance equipmentInstance,
            out EffectiveWeapon effectiveWeapon,
            out string rejectionCode)
        {
            effectiveWeapon = null;
            if (equipmentInstance == null)
            {
                rejectionCode = "weapon-live-equipment-unresolved";
                return false;
            }

            EquipmentValidationResult validation =
                equipmentCatalog.ValidateInstance(equipmentInstance);
            if (!validation.IsValid)
            {
                rejectionCode = "weapon-live-equipment-invalid";
                return false;
            }

            EquipmentDefinition equipmentDefinition =
                equipmentCatalog.FindEquipmentDefinition(
                    equipmentInstance.DefinitionId);
            if (equipmentDefinition == null
                || !EquipmentCategoryIds.Weapon.Equals(
                    equipmentDefinition.CategoryId)
                || equipmentDefinition.RuntimeWeaponReferenceId == null)
            {
                rejectionCode = "weapon-live-equipment-definition-invalid";
                return false;
            }

            string definitionValue =
                equipmentDefinition.RuntimeWeaponReferenceId.ToString();
            WeaponDefinitionData catalogDefinition;
            if (!weaponCatalog.TryGetDefinition(
                    definitionValue,
                    out catalogDefinition)
                || catalogDefinition == null)
            {
                rejectionCode =
                    "weapon-live-definition-unresolved:" + definitionValue;
                return false;
            }

            var definitionId = new WeaponDefinitionId(definitionValue);
            WeaponCatalogBlueprintMappingIntent intent;
            if (!mappingPolicies.TryResolve(definitionId, out intent)
                || intent == null)
            {
                rejectionCode =
                    "weapon-live-blueprint-policy-missing:" + definitionValue;
                return false;
            }

            WeaponBlueprintMappingResult mapping =
                WeaponCatalogBlueprintMapper.Map(
                    weaponCatalog,
                    definitionValue,
                    intent);
            if (mapping == null || !mapping.Succeeded || mapping.Blueprint == null)
            {
                string issue = mapping == null || mapping.Issues.Count == 0
                    ? "unknown"
                    : mapping.Issues[0].Code.ToString();
                rejectionCode =
                    "weapon-live-blueprint-mapping-failed:" + issue;
                return false;
            }

            IReadOnlyList<WeaponAugmentModifierSet> modifierSets;
            if (!augmentModifiers.TryResolve(
                    equipmentInstance,
                    equipmentCatalog,
                    out modifierSets,
                    out rejectionCode)
                || modifierSets == null)
            {
                if (string.IsNullOrWhiteSpace(rejectionCode))
                {
                    rejectionCode = "weapon-live-augment-resolution-failed";
                }
                return false;
            }

            try
            {
                effectiveWeapon = EffectiveWeaponFactory.Create(
                    mapping.Blueprint,
                    equipmentCatalog,
                    equipmentInstance,
                    modifierSets);
            }
            catch (ArgumentException)
            {
                rejectionCode = "weapon-live-effective-weapon-invalid";
                return false;
            }
            catch (InvalidOperationException)
            {
                rejectionCode = "weapon-live-effective-weapon-invalid";
                return false;
            }
            catch (OverflowException)
            {
                rejectionCode = "weapon-live-effective-weapon-numerical-failure";
                return false;
            }

            if (effectiveWeapon == null
                || !effectiveWeapon.EquipmentInstanceId.Value.Equals(
                    equipmentInstance.InstanceId)
                || !effectiveWeapon.DefinitionId.Equals(definitionId))
            {
                effectiveWeapon = null;
                rejectionCode = "weapon-live-effective-weapon-identity-mismatch";
                return false;
            }

            rejectionCode = string.Empty;
            return true;
        }
    }
}

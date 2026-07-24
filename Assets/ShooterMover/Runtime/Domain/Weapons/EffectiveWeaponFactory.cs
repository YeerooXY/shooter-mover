using System;
using System.Collections.Generic;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Resolves the existing equipment and augment authorities, then creates one immutable
    /// effective profile without mutating any source definition or instance.
    /// </summary>
    public static class EffectiveWeaponFactory
    {
        public static EffectiveWeapon Create(
            WeaponBlueprint blueprint,
            EquipmentCatalog equipmentCatalog,
            EquipmentInstance equipmentInstance,
            IEnumerable<WeaponAugmentModifierSet> augmentModifierSets)
        {
            if (blueprint == null)
            {
                throw new ArgumentNullException(nameof(blueprint));
            }
            if (equipmentCatalog == null)
            {
                throw new ArgumentNullException(nameof(equipmentCatalog));
            }
            if (equipmentInstance == null)
            {
                throw new ArgumentNullException(nameof(equipmentInstance));
            }
            if (augmentModifierSets == null)
            {
                throw new ArgumentNullException(nameof(augmentModifierSets));
            }

            EquipmentValidationResult validation = equipmentCatalog.ValidateInstance(equipmentInstance);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    BuildEquipmentValidationMessage(validation),
                    nameof(equipmentInstance));
            }

            EquipmentDefinition equipmentDefinition =
                equipmentCatalog.FindEquipmentDefinition(equipmentInstance.DefinitionId);
            ValidateWeaponEquipmentLink(blueprint, equipmentDefinition);

            List<AugmentInstance> installedAugments =
                new List<AugmentInstance>(equipmentInstance.Augments.Count);
            Dictionary<StableId, AugmentInstance> installedById =
                new Dictionary<StableId, AugmentInstance>();
            for (int index = 0; index < equipmentInstance.Augments.Count; index++)
            {
                AugmentInstance installed = equipmentInstance.Augments[index];
                installedAugments.Add(installed);
                installedById.Add(installed.InstanceId, installed);
            }

            Dictionary<StableId, WeaponAugmentModifierSet> modifiersByAugmentId =
                ResolveModifierSets(equipmentCatalog, installedById, augmentModifierSets);
            EffectiveWeaponEvaluatedValues values = EffectiveWeaponStatEvaluator.Evaluate(
                blueprint,
                installedAugments,
                modifiersByAugmentId);

            return new EffectiveWeapon(
                blueprint,
                new EquipmentInstanceId(equipmentInstance.InstanceId),
                equipmentInstance.DefinitionId,
                equipmentInstance.ItemLevel,
                equipmentInstance.QualityId,
                installedAugments,
                values.FireSettings,
                values.ShotPattern,
                values.Projectile,
                values.Guidance,
                values.Impact,
                values.Damage,
                values.Effects);
        }

        private static void ValidateWeaponEquipmentLink(
            WeaponBlueprint blueprint,
            EquipmentDefinition equipmentDefinition)
        {
            if (equipmentDefinition == null)
            {
                throw new InvalidOperationException(
                    "The validated equipment instance has no resolved equipment definition.");
            }
            if (!EquipmentCategoryIds.Weapon.Equals(equipmentDefinition.CategoryId))
            {
                throw new ArgumentException(
                    "EffectiveWeapon requires an equipment definition in the existing weapon category.",
                    nameof(equipmentDefinition));
            }
            if (equipmentDefinition.RuntimeWeaponReferenceId == null
                || !string.Equals(
                    equipmentDefinition.RuntimeWeaponReferenceId.ToString(),
                    blueprint.DefinitionId.Value,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Equipment runtime weapon reference does not match the supplied WeaponBlueprint identity.",
                    nameof(equipmentDefinition));
            }
        }

        private static Dictionary<StableId, WeaponAugmentModifierSet> ResolveModifierSets(
            EquipmentCatalog equipmentCatalog,
            IDictionary<StableId, AugmentInstance> installedById,
            IEnumerable<WeaponAugmentModifierSet> modifierSets)
        {
            Dictionary<StableId, WeaponAugmentModifierSet> result =
                new Dictionary<StableId, WeaponAugmentModifierSet>();

            foreach (WeaponAugmentModifierSet modifierSet in modifierSets)
            {
                if (modifierSet == null)
                {
                    throw new ArgumentException(
                        "Weapon augment modifier collections cannot contain null sets.",
                        nameof(modifierSets));
                }

                AugmentInstance suppliedInstance = modifierSet.Instance;
                if (suppliedInstance.InstanceId == null)
                {
                    throw new ArgumentException(
                        "Weapon augment modifier sets require the existing augment instance identity.",
                        nameof(modifierSets));
                }
                if (result.ContainsKey(suppliedInstance.InstanceId))
                {
                    throw new ArgumentException(
                        "Only one modifier set may be supplied for each installed augment instance.",
                        nameof(modifierSets));
                }

                AugmentInstance installedInstance;
                if (!installedById.TryGetValue(suppliedInstance.InstanceId, out installedInstance))
                {
                    throw new ArgumentException(
                        "A modifier set was supplied for an augment instance that is not installed.",
                        nameof(modifierSets));
                }
                if (!installedInstance.Equals(suppliedInstance))
                {
                    throw new ArgumentException(
                        "The supplied augment instance snapshot does not match the installed instance.",
                        nameof(modifierSets));
                }

                AugmentDefinition catalogDefinition =
                    equipmentCatalog.FindAugmentDefinition(installedInstance.DefinitionId);
                if (catalogDefinition == null || !catalogDefinition.Equals(modifierSet.Definition))
                {
                    throw new ArgumentException(
                        "The supplied augment definition does not match the existing EquipmentCatalog authority.",
                        nameof(modifierSets));
                }

                result.Add(suppliedInstance.InstanceId, modifierSet);
            }

            if (result.Count != installedById.Count)
            {
                throw new ArgumentException(
                    "Every installed augment instance requires exactly one explicit modifier set, including an empty set when it has no weapon-stat effect.",
                    nameof(modifierSets));
            }

            return result;
        }

        private static string BuildEquipmentValidationMessage(
            EquipmentValidationResult validation)
        {
            StringBuilder builder = new StringBuilder(
                "Equipment instance is invalid under the existing EquipmentCatalog authority:");
            for (int index = 0; index < validation.Issues.Count; index++)
            {
                builder.Append(' ')
                    .Append(validation.Issues[index].ToString());
            }
            return builder.ToString();
        }
    }
}

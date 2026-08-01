using System;
using System.Collections.Generic;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns
{
    /// <summary>
    /// Resolves the existing equipment and augment authorities, then creates one immutable
    /// effective profile without mutating any source definition or instance. Modifier sets are
    /// resolved inputs supplied by composition.
    /// </summary>
    public static class EffectiveGunFactory
    {
        public static EffectiveGun Create(
            Gun blueprint,
            EquipmentCatalog equipmentCatalog,
            EquipmentInstance equipmentInstance,
            IEnumerable<GunAugmentModifierSet> augmentModifierSets)
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
            ValidateGunEquipmentLink(blueprint, equipmentDefinition);

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

            Dictionary<StableId, GunAugmentModifierSet> modifiersByAugmentId =
                ResolveModifierSets(equipmentCatalog, installedById, augmentModifierSets);
            EffectiveGunEvaluatedValues values = EffectiveGunStatEvaluator.Evaluate(
                blueprint,
                installedAugments,
                modifiersByAugmentId);

            return new EffectiveGun(
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
                values.Effects,
                values.MaximumAttackDistance,
                values.Pierce,
                values.Ricochet,
                values.MovementPenaltyPercent);
        }

        private static void ValidateGunEquipmentLink(
            Gun blueprint,
            EquipmentDefinition equipmentDefinition)
        {
            if (equipmentDefinition == null)
            {
                throw new InvalidOperationException(
                    "The validated equipment instance has no resolved equipment definition.");
            }
            if (!EquipmentCategoryIds.Gun.Equals(equipmentDefinition.CategoryId))
            {
                throw new ArgumentException(
                    "EffectiveGun requires an equipment definition in the existing gun category.",
                    nameof(equipmentDefinition));
            }
            if (equipmentDefinition.RuntimeGunReferenceId == null
                || !GunDefinitionId.FromRuntimeReference(
                    equipmentDefinition.RuntimeGunReferenceId)
                    .Equals(blueprint.DefinitionId))
            {
                throw new ArgumentException(
                    "Equipment runtime gun reference does not match the supplied Gun identity.",
                    nameof(equipmentDefinition));
            }
        }

        private static Dictionary<StableId, GunAugmentModifierSet> ResolveModifierSets(
            EquipmentCatalog equipmentCatalog,
            IDictionary<StableId, AugmentInstance> installedById,
            IEnumerable<GunAugmentModifierSet> modifierSets)
        {
            Dictionary<StableId, GunAugmentModifierSet> result =
                new Dictionary<StableId, GunAugmentModifierSet>();

            foreach (GunAugmentModifierSet modifierSet in modifierSets)
            {
                if (modifierSet == null)
                {
                    throw new ArgumentException(
                        "Gun augment modifier collections cannot contain null sets.",
                        nameof(modifierSets));
                }

                AugmentInstance suppliedInstance = modifierSet.Instance;
                if (suppliedInstance.InstanceId == null)
                {
                    throw new ArgumentException(
                        "Gun augment modifier sets require the existing augment instance identity.",
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
                    "Every installed augment instance requires exactly one explicit modifier set, including an empty set when it has no gun-stat effect.",
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

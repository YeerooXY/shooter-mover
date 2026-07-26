using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    /// <summary>
    /// One Mark in the production provisional catalogue. Progression metadata is authored beside
    /// the canonical blueprint so strongbox and future crafting projections cannot drift apart.
    /// </summary>
    public sealed class ProductionWeaponMarkV1
    {
        public ProductionWeaponMarkV1(
            int mark,
            int dropAnchorLevel,
            int craftUnlockLevel,
            bool isCombatTuningProvisional,
            WeaponBlueprint blueprint)
        {
            if (mark < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(mark));
            }
            if (dropAnchorLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dropAnchorLevel));
            }
            if (craftUnlockLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(craftUnlockLevel));
            }

            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            if (blueprint.DropMetadata == null
                || blueprint.DropMetadata.PeakDropLevel != dropAnchorLevel)
            {
                throw new ArgumentException(
                    "The canonical drop metadata must carry the authored drop anchor.",
                    nameof(blueprint));
            }

            Mark = mark;
            DropAnchorLevel = dropAnchorLevel;
            CraftUnlockLevel = craftUnlockLevel;
            IsCombatTuningProvisional = isCombatTuningProvisional;
        }

        public int Mark { get; }
        public int DropAnchorLevel { get; }
        public int CraftUnlockLevel { get; }
        public bool IsCombatTuningProvisional { get; }
        public WeaponBlueprint Blueprint { get; }
        public StableId EquipmentDefinitionId
        {
            get { return Blueprint.DropMetadata.EquipmentDefinitionId; }
        }
    }

    /// <summary>
    /// Permanent family identity, category, and rarity. All three Marks inherit this rarity and
    /// cannot author a competing per-Mark value.
    /// </summary>
    public sealed class ProductionWeaponFamilyV1
    {
        private readonly ReadOnlyCollection<ProductionWeaponMarkV1> marks;

        public ProductionWeaponFamilyV1(
            string familyId,
            string displayName,
            StableId weaponCategoryId,
            StableId rarityId,
            string catalogRarity,
            IEnumerable<ProductionWeaponMarkV1> values)
        {
            if (string.IsNullOrWhiteSpace(familyId))
            {
                throw new ArgumentException(
                    "A stable weapon family identity is required.",
                    nameof(familyId));
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A weapon family display name is required.",
                    nameof(displayName));
            }
            if (string.IsNullOrWhiteSpace(catalogRarity))
            {
                throw new ArgumentException(
                    "A catalogue rarity value is required.",
                    nameof(catalogRarity));
            }

            FamilyId = familyId.Trim();
            DisplayName = displayName.Trim();
            WeaponCategoryId = weaponCategoryId
                ?? throw new ArgumentNullException(nameof(weaponCategoryId));
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            CatalogRarity = catalogRarity.Trim();

            var copy = new List<ProductionWeaponMarkV1>(
                values ?? throw new ArgumentNullException(nameof(values)));
            copy.Sort(delegate(ProductionWeaponMarkV1 left, ProductionWeaponMarkV1 right)
            {
                return left.Mark.CompareTo(right.Mark);
            });
            if (copy.Count != 3)
            {
                throw new ArgumentException(
                    "Every production weapon family must contain exactly MK1, MK2, and MK3.",
                    nameof(values));
            }

            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var equipmentIds = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                ProductionWeaponMarkV1 value = copy[index];
                if (value == null || value.Mark != index + 1)
                {
                    throw new ArgumentException(
                        "Weapon family Marks must be the ordered identities MK1, MK2, and MK3.",
                        nameof(values));
                }
                if (!string.Equals(
                        value.Blueprint.WeaponFamily,
                        FamilyId,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Every Mark blueprint must use the owning family identity.",
                        nameof(values));
                }
                if (value.Blueprint.DropMetadata.RarityId != RarityId)
                {
                    throw new ArgumentException(
                        "Every Mark must inherit the family's single rarity identity.",
                        nameof(values));
                }
                if (!definitionIds.Add(value.Blueprint.DefinitionId.ToString())
                    || !equipmentIds.Add(value.EquipmentDefinitionId))
                {
                    throw new ArgumentException(
                        "Weapon definition and equipment identities must be unique.",
                        nameof(values));
                }
            }

            marks = new ReadOnlyCollection<ProductionWeaponMarkV1>(copy);
        }

        public string FamilyId { get; }
        public string DisplayName { get; }
        public StableId WeaponCategoryId { get; }
        public StableId RarityId { get; }
        public string CatalogRarity { get; }
        public IReadOnlyList<ProductionWeaponMarkV1> Marks
        {
            get { return marks; }
        }
    }

    /// <summary>
    /// One immutable production projection. Canonical blueprints are the authored authority;
    /// the flat WeaponCatalog and EquipmentCatalog are compatibility views consumed by the
    /// existing strongbox, inventory, shop, and simulator boundaries.
    /// </summary>
    public sealed class ProductionWeaponCatalogueProjectionV1
    {
        private readonly ReadOnlyCollection<ProductionWeaponFamilyV1> families;
        private readonly ReadOnlyCollection<WeaponBlueprint> blueprints;
        private readonly ReadOnlyCollection<StableId> equipmentDefinitionIds;
        private readonly ReadOnlyDictionary<string, ProductionWeaponMarkV1>
            marksByDefinitionId;

        internal ProductionWeaponCatalogueProjectionV1(
            IEnumerable<ProductionWeaponFamilyV1> values,
            WeaponCatalog weaponCatalog,
            EquipmentCatalog equipmentCatalog)
        {
            var familyCopy = new List<ProductionWeaponFamilyV1>(
                values ?? throw new ArgumentNullException(nameof(values)));
            familyCopy.Sort(delegate(
                ProductionWeaponFamilyV1 left,
                ProductionWeaponFamilyV1 right)
            {
                return string.CompareOrdinal(left.FamilyId, right.FamilyId);
            });
            if (familyCopy.Count == 0)
            {
                throw new ArgumentException(
                    "The production weapon catalogue cannot be empty.",
                    nameof(values));
            }

            var blueprintCopy = new List<WeaponBlueprint>();
            var equipmentIdCopy = new List<StableId>();
            var markMap = new Dictionary<string, ProductionWeaponMarkV1>(
                StringComparer.Ordinal);
            for (int familyIndex = 0;
                 familyIndex < familyCopy.Count;
                 familyIndex++)
            {
                ProductionWeaponFamilyV1 family = familyCopy[familyIndex]
                    ?? throw new ArgumentException(
                        "Weapon catalogue families cannot contain null values.",
                        nameof(values));
                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    ProductionWeaponMarkV1 mark = family.Marks[markIndex];
                    string definitionId = mark.Blueprint.DefinitionId.ToString();
                    if (markMap.ContainsKey(definitionId))
                    {
                        throw new ArgumentException(
                            "Weapon definition identities must be unique: "
                            + definitionId,
                            nameof(values));
                    }
                    markMap.Add(definitionId, mark);
                    blueprintCopy.Add(mark.Blueprint);
                    equipmentIdCopy.Add(mark.EquipmentDefinitionId);
                }
            }
            blueprintCopy.Sort(delegate(WeaponBlueprint left, WeaponBlueprint right)
            {
                return string.CompareOrdinal(
                    left.DefinitionId.ToString(),
                    right.DefinitionId.ToString());
            });
            equipmentIdCopy.Sort();

            families = new ReadOnlyCollection<ProductionWeaponFamilyV1>(familyCopy);
            blueprints = new ReadOnlyCollection<WeaponBlueprint>(blueprintCopy);
            equipmentDefinitionIds =
                new ReadOnlyCollection<StableId>(equipmentIdCopy);
            marksByDefinitionId =
                new ReadOnlyDictionary<string, ProductionWeaponMarkV1>(markMap);
            WeaponCatalog = weaponCatalog
                ?? throw new ArgumentNullException(nameof(weaponCatalog));
            EquipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));

            foreach (KeyValuePair<string, ProductionWeaponMarkV1> pair
                in markMap)
            {
                WeaponDefinitionData flatDefinition;
                if (!WeaponCatalog.TryGetDefinition(
                        pair.Key,
                        out flatDefinition)
                    || flatDefinition == null)
                {
                    throw new ArgumentException(
                        "Canonical weapon is missing its flat strongbox projection: "
                        + pair.Key,
                        nameof(weaponCatalog));
                }

                EquipmentDefinition equipment = EquipmentCatalog
                    .FindEquipmentDefinition(
                        pair.Value.EquipmentDefinitionId);
                if (equipment == null
                    || equipment.RuntimeWeaponReferenceId == null
                    || !string.Equals(
                        equipment.RuntimeWeaponReferenceId.ToString(),
                        pair.Key,
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Canonical weapon is missing its exact equipment projection: "
                        + pair.Key,
                        nameof(equipmentCatalog));
                }
            }
        }

        public IReadOnlyList<ProductionWeaponFamilyV1> Families
        {
            get { return families; }
        }

        public IReadOnlyList<WeaponBlueprint> Blueprints
        {
            get { return blueprints; }
        }

        public IReadOnlyList<StableId> EquipmentDefinitionIds
        {
            get { return equipmentDefinitionIds; }
        }

        public WeaponCatalog WeaponCatalog { get; }
        public EquipmentCatalog EquipmentCatalog { get; }

        public bool TryGetMark(
            string definitionId,
            out ProductionWeaponMarkV1 mark)
        {
            return marksByDefinitionId.TryGetValue(
                definitionId ?? string.Empty,
                out mark);
        }

        public bool TryGetBlueprint(
            string definitionId,
            out WeaponBlueprint blueprint)
        {
            ProductionWeaponMarkV1 mark;
            if (TryGetMark(definitionId, out mark))
            {
                blueprint = mark.Blueprint;
                return true;
            }

            blueprint = null;
            return false;
        }
    }
}

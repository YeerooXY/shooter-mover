using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Catalog
{
    /// <summary>
    /// One Mark in the production provisional catalogue. Progression metadata is authored beside
    /// the canonical blueprint so strongbox and future crafting projections cannot drift apart.
    /// </summary>
    public sealed class WeaponMark
    {
        public WeaponMark(
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
    public sealed class WeaponFamily
    {
        private readonly ReadOnlyCollection<WeaponMark> marks;

        public WeaponFamily(
            string familyId,
            string displayName,
            StableId weaponCategoryId,
            StableId rarityId,
            string catalogRarity,
            IEnumerable<WeaponMark> values)
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

            var copy = new List<WeaponMark>(
                values ?? throw new ArgumentNullException(nameof(values)));
            copy.Sort(delegate(WeaponMark left, WeaponMark right)
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
                WeaponMark value = copy[index];
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

            marks = new ReadOnlyCollection<WeaponMark>(copy);
        }

        public string FamilyId { get; }
        public string DisplayName { get; }
        public StableId WeaponCategoryId { get; }
        public StableId RarityId { get; }
        public string CatalogRarity { get; }
        public IReadOnlyList<WeaponMark> Marks
        {
            get { return marks; }
        }
    }

    /// <summary>
    /// One immutable production projection. Canonical blueprints are the authored authority;
    /// the flat WeaponCatalog and EquipmentCatalog are compatibility views consumed by the
    /// existing strongbox, inventory, shop, and simulator boundaries.
    /// </summary>
    public sealed class WeaponCatalogueView
    {
        private readonly ReadOnlyCollection<WeaponFamily> families;
        private readonly ReadOnlyCollection<WeaponBlueprint> blueprints;
        private readonly ReadOnlyCollection<StableId> equipmentDefinitionIds;
        private readonly ReadOnlyDictionary<string, WeaponMark>
            marksByDefinitionId;

        internal WeaponCatalogueView(
            IEnumerable<WeaponFamily> values,
            WeaponCatalog weaponCatalog,
            EquipmentCatalog equipmentCatalog)
        {
            var familyCopy = new List<WeaponFamily>(
                values ?? throw new ArgumentNullException(nameof(values)));
            familyCopy.Sort(delegate(
                WeaponFamily left,
                WeaponFamily right)
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
            var markMap = new Dictionary<string, WeaponMark>(
                StringComparer.Ordinal);
            for (int familyIndex = 0;
                 familyIndex < familyCopy.Count;
                 familyIndex++)
            {
                WeaponFamily family = familyCopy[familyIndex]
                    ?? throw new ArgumentException(
                        "Weapon catalogue families cannot contain null values.",
                        nameof(values));
                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    WeaponMark mark = family.Marks[markIndex];
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

            families = new ReadOnlyCollection<WeaponFamily>(familyCopy);
            blueprints = new ReadOnlyCollection<WeaponBlueprint>(blueprintCopy);
            equipmentDefinitionIds =
                new ReadOnlyCollection<StableId>(equipmentIdCopy);
            marksByDefinitionId =
                new ReadOnlyDictionary<string, WeaponMark>(markMap);
            WeaponCatalog = weaponCatalog
                ?? throw new ArgumentNullException(nameof(weaponCatalog));
            EquipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));

            foreach (KeyValuePair<string, WeaponMark> pair
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
                    || !WeaponDefinitionId.FromRuntimeReference(
                            equipment.RuntimeWeaponReferenceId)
                        .Equals(new WeaponDefinitionId(pair.Key)))
                {
                    throw new ArgumentException(
                        "Canonical weapon is missing its exact equipment projection: "
                        + pair.Key,
                        nameof(equipmentCatalog));
                }
            }
        }

        public IReadOnlyList<WeaponFamily> Families
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
            out WeaponMark mark)
        {
            return marksByDefinitionId.TryGetValue(
                definitionId ?? string.Empty,
                out mark);
        }

        public bool TryGetBlueprint(
            string definitionId,
            out WeaponBlueprint blueprint)
        {
            WeaponMark mark;
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

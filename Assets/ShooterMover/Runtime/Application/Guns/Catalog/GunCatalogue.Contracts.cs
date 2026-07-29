using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// One Mark in the production provisional catalogue. Progression metadata is authored beside
    /// the canonical blueprint so strongbox and future crafting projections cannot drift apart.
    /// </summary>
    public sealed class GunMark
    {
        public GunMark(
            int mark,
            int dropAnchorLevel,
            int craftUnlockLevel,
            bool isCombatTuningProvisional,
            Gun blueprint)
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
        public Gun Blueprint { get; }
        public StableId EquipmentDefinitionId
        {
            get { return Blueprint.DropMetadata.EquipmentDefinitionId; }
        }
    }

    /// <summary>
    /// Permanent family identity, category, and rarity. All three Marks inherit this rarity and
    /// cannot author a competing per-Mark value.
    /// </summary>
    public sealed class GunFamily
    {
        private readonly ReadOnlyCollection<GunMark> marks;

        public GunFamily(
            string familyId,
            string displayName,
            StableId gunCategoryId,
            StableId rarityId,
            string catalogRarity,
            IEnumerable<GunMark> values)
        {
            if (string.IsNullOrWhiteSpace(familyId))
            {
                throw new ArgumentException(
                    "A stable gun family identity is required.",
                    nameof(familyId));
            }
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A gun family display name is required.",
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
            GunCategoryId = gunCategoryId
                ?? throw new ArgumentNullException(nameof(gunCategoryId));
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            CatalogRarity = catalogRarity.Trim();

            var copy = new List<GunMark>(
                values ?? throw new ArgumentNullException(nameof(values)));
            copy.Sort(delegate(GunMark left, GunMark right)
            {
                return left.Mark.CompareTo(right.Mark);
            });
            if (copy.Count != 3)
            {
                throw new ArgumentException(
                    "Every production gun family must contain exactly MK1, MK2, and MK3.",
                    nameof(values));
            }

            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var equipmentIds = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                GunMark value = copy[index];
                if (value == null || value.Mark != index + 1)
                {
                    throw new ArgumentException(
                        "Gun family Marks must be the ordered identities MK1, MK2, and MK3.",
                        nameof(values));
                }
                if (!string.Equals(
                        value.Blueprint.GunFamily,
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
                        "Gun definition and equipment identities must be unique.",
                        nameof(values));
                }
            }

            marks = new ReadOnlyCollection<GunMark>(copy);
        }

        public string FamilyId { get; }
        public string DisplayName { get; }
        public StableId GunCategoryId { get; }
        public StableId RarityId { get; }
        public string CatalogRarity { get; }
        public IReadOnlyList<GunMark> Marks
        {
            get { return marks; }
        }
    }

    /// <summary>
    /// One immutable production projection. Canonical blueprints are the authored authority;
    /// the flat GunCatalog and EquipmentCatalog are compatibility views consumed by the
    /// existing strongbox, inventory, shop, and simulator boundaries.
    /// </summary>
    public sealed class GunCatalogueView
    {
        private readonly ReadOnlyCollection<GunFamily> families;
        private readonly ReadOnlyCollection<Gun> blueprints;
        private readonly ReadOnlyCollection<StableId> equipmentDefinitionIds;
        private readonly ReadOnlyDictionary<string, GunMark>
            marksByDefinitionId;

        internal GunCatalogueView(
            IEnumerable<GunFamily> values,
            GunCatalog gunCatalog,
            EquipmentCatalog equipmentCatalog)
        {
            var familyCopy = new List<GunFamily>(
                values ?? throw new ArgumentNullException(nameof(values)));
            familyCopy.Sort(delegate(
                GunFamily left,
                GunFamily right)
            {
                return string.CompareOrdinal(left.FamilyId, right.FamilyId);
            });
            if (familyCopy.Count == 0)
            {
                throw new ArgumentException(
                    "The production gun catalogue cannot be empty.",
                    nameof(values));
            }

            var blueprintCopy = new List<Gun>();
            var equipmentIdCopy = new List<StableId>();
            var markMap = new Dictionary<string, GunMark>(
                StringComparer.Ordinal);
            for (int familyIndex = 0;
                 familyIndex < familyCopy.Count;
                 familyIndex++)
            {
                GunFamily family = familyCopy[familyIndex]
                    ?? throw new ArgumentException(
                        "Gun catalogue families cannot contain null values.",
                        nameof(values));
                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    GunMark mark = family.Marks[markIndex];
                    string definitionId = mark.Blueprint.DefinitionId.ToString();
                    if (markMap.ContainsKey(definitionId))
                    {
                        throw new ArgumentException(
                            "Gun definition identities must be unique: "
                            + definitionId,
                            nameof(values));
                    }
                    markMap.Add(definitionId, mark);
                    blueprintCopy.Add(mark.Blueprint);
                    equipmentIdCopy.Add(mark.EquipmentDefinitionId);
                }
            }
            blueprintCopy.Sort(delegate(Gun left, Gun right)
            {
                return string.CompareOrdinal(
                    left.DefinitionId.ToString(),
                    right.DefinitionId.ToString());
            });
            equipmentIdCopy.Sort();

            families = new ReadOnlyCollection<GunFamily>(familyCopy);
            blueprints = new ReadOnlyCollection<Gun>(blueprintCopy);
            equipmentDefinitionIds =
                new ReadOnlyCollection<StableId>(equipmentIdCopy);
            marksByDefinitionId =
                new ReadOnlyDictionary<string, GunMark>(markMap);
            GunCatalog = gunCatalog
                ?? throw new ArgumentNullException(nameof(gunCatalog));
            EquipmentCatalog = equipmentCatalog
                ?? throw new ArgumentNullException(nameof(equipmentCatalog));

            foreach (KeyValuePair<string, GunMark> pair
                in markMap)
            {
                GunDefinitionData flatDefinition;
                if (!GunCatalog.TryGetDefinition(
                        pair.Key,
                        out flatDefinition)
                    || flatDefinition == null)
                {
                    throw new ArgumentException(
                        "Canonical gun is missing its flat strongbox projection: "
                        + pair.Key,
                        nameof(gunCatalog));
                }

                EquipmentDefinition equipment = EquipmentCatalog
                    .FindEquipmentDefinition(
                        pair.Value.EquipmentDefinitionId);
                if (equipment == null
                    || equipment.RuntimeGunReferenceId == null
                    || !GunDefinitionId.FromRuntimeReference(
                            equipment.RuntimeGunReferenceId)
                        .Equals(new GunDefinitionId(pair.Key)))
                {
                    throw new ArgumentException(
                        "Canonical gun is missing its exact equipment projection: "
                        + pair.Key,
                        nameof(equipmentCatalog));
                }
            }
        }

        public IReadOnlyList<GunFamily> Families
        {
            get { return families; }
        }

        public IReadOnlyList<Gun> Blueprints
        {
            get { return blueprints; }
        }

        public IReadOnlyList<StableId> EquipmentDefinitionIds
        {
            get { return equipmentDefinitionIds; }
        }

        public GunCatalog GunCatalog { get; }
        public EquipmentCatalog EquipmentCatalog { get; }

        public bool TryGetMark(
            string definitionId,
            out GunMark mark)
        {
            return marksByDefinitionId.TryGetValue(
                definitionId ?? string.Empty,
                out mark);
        }

        public bool TryGetBlueprint(
            string definitionId,
            out Gun blueprint)
        {
            GunMark mark;
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

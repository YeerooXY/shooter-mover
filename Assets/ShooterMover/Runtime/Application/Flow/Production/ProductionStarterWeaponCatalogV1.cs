
using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Temporary compatibility surface for the five shipped starter identities. The
    /// canonical projection is composed elsewhere and remains the only catalog authority.
    /// </summary>
    public static class ProductionStarterWeaponCatalogV1
    {
        public const string ArcWeaponDefinitionId = "weapon.arc-gun";
        public const string RicochetWeaponDefinitionId = "weapon.ricochet-gun";
        public const string BlasterSideProfileArtId = "weapon-art.blaster.side-v1";
        public const string ShotgunSideProfileArtId = "weapon-art.shotgun-basic.side-v1";
        public const string RocketSideProfileArtId = "weapon-art.rocket-launcher.side-v1";
        public const string ArcSideProfileArtId = "weapon-art.arc-rifle.side-v1";
        public const string RicochetSideProfileArtId = "weapon-art.ricochet-weapon.side-v1";

        public static readonly StableId BlasterEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-blaster");
        public static readonly StableId ShotgunEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-shotgun");
        public static readonly StableId RocketEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-rocket-launcher");
        public static readonly StableId ArcEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-arc-gun");
        public static readonly StableId RicochetEquipmentDefinitionStableId =
            StableId.Parse("equipment.production-starter-ricochet-gun");

        public static readonly StableId BlasterEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-1");
        public static readonly StableId ShotgunEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-2");
        public static readonly StableId RocketEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-3");
        public static readonly StableId ArcEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.flow-draft-slot-4");
        public static readonly StableId RicochetEquipmentInstanceStableId =
            StableId.Parse("equipment-instance.production-starter-ricochet");

        private static readonly object Sync = new object();
        private static CanonicalWeaponCatalogProjectionV1 composedProjection;

        private static readonly StableId[] initialEquipmentDefinitionStableIds =
        {
            BlasterEquipmentDefinitionStableId,
            ShotgunEquipmentDefinitionStableId,
            RocketEquipmentDefinitionStableId,
            ArcEquipmentDefinitionStableId,
        };
        private static readonly StableId[] allEquipmentDefinitionStableIds =
        {
            BlasterEquipmentDefinitionStableId,
            ShotgunEquipmentDefinitionStableId,
            RocketEquipmentDefinitionStableId,
            ArcEquipmentDefinitionStableId,
            RicochetEquipmentDefinitionStableId,
        };
        private static readonly Dictionary<StableId, StableId> knownDefinitionByInstance =
            BuildKnownDefinitionMap();

        public static IReadOnlyList<StableId> InitialEquipmentDefinitionStableIds
        {
            get { return initialEquipmentDefinitionStableIds; }
        }
        public static IReadOnlyList<StableId> AllEquipmentDefinitionStableIds
        {
            get { return allEquipmentDefinitionStableIds; }
        }

        public static void Compose(CanonicalWeaponCatalogProjectionV1 projection)
        {
            if (projection == null) throw new ArgumentNullException(nameof(projection));
            lock (Sync)
            {
                if (composedProjection == null)
                {
                    composedProjection = projection;
                    return;
                }
                if (!string.Equals(composedProjection.Fingerprint, projection.Fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("production-weapon-catalog-already-composed-differently");
                }
            }
        }

        public static bool TryGetCanonicalProjection(out CanonicalWeaponCatalogProjectionV1 projection)
        {
            lock (Sync)
            {
                projection = composedProjection;
                return projection != null;
            }
        }

        public static CanonicalWeaponCatalogProjectionV1 BuildCanonicalProjection()
        {
            CanonicalWeaponCatalogProjectionV1 projection;
            if (!TryGetCanonicalProjection(out projection))
            {
                throw new InvalidOperationException("production-weapon-catalog-not-composed");
            }
            return projection;
        }

        public static EquipmentCatalog BuildEquipmentCatalog()
        {
            return BuildCanonicalProjection().EquipmentCatalog;
        }

        public static WeaponCatalog BuildWeaponCatalog()
        {
            return BuildCanonicalProjection().WeaponCatalog;
        }

        public static bool TryResolveDefinitionForInstance(
            StableId equipmentInstanceStableId,
            out StableId equipmentDefinitionStableId)
        {
            equipmentDefinitionStableId = null;
            return equipmentInstanceStableId != null
                && knownDefinitionByInstance.TryGetValue(equipmentInstanceStableId, out equipmentDefinitionStableId);
        }

        public static StableId ReserveInstanceForDefinition(StableId equipmentDefinitionStableId)
        {
            if (equipmentDefinitionStableId == BlasterEquipmentDefinitionStableId) return BlasterEquipmentInstanceStableId;
            if (equipmentDefinitionStableId == ShotgunEquipmentDefinitionStableId) return ShotgunEquipmentInstanceStableId;
            if (equipmentDefinitionStableId == RocketEquipmentDefinitionStableId) return RocketEquipmentInstanceStableId;
            if (equipmentDefinitionStableId == ArcEquipmentDefinitionStableId) return ArcEquipmentInstanceStableId;
            if (equipmentDefinitionStableId == RicochetEquipmentDefinitionStableId) return RicochetEquipmentInstanceStableId;
            throw new ArgumentException("Unknown production starter equipment definition.", nameof(equipmentDefinitionStableId));
        }

        private static Dictionary<StableId, StableId> BuildKnownDefinitionMap()
        {
            return new Dictionary<StableId, StableId>
            {
                { BlasterEquipmentInstanceStableId, BlasterEquipmentDefinitionStableId },
                { ShotgunEquipmentInstanceStableId, ShotgunEquipmentDefinitionStableId },
                { RocketEquipmentInstanceStableId, RocketEquipmentDefinitionStableId },
                { ArcEquipmentInstanceStableId, ArcEquipmentDefinitionStableId },
                { RicochetEquipmentInstanceStableId, RicochetEquipmentDefinitionStableId },
            };
        }
    }
}

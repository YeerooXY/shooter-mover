using System;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.UI.InventoryLoadout
{
    /// <summary>
    /// Read-only Inventory card projection. Combat values come from the canonical gun
    /// catalogue; the temporary Unity resource key is presentation-only and owns no item state.
    /// </summary>
    public sealed class GunInventoryCardPresentation
    {
        public const string TemporaryImageResourceKey = "blaster_sp";

        private GunInventoryCardPresentation(
            string displayName,
            string sideProfileArtReference,
            string imageResourceKey,
            double damagePerShot,
            int projectilesPerShot,
            double rateOfFire)
        {
            DisplayName = displayName ?? string.Empty;
            SideProfileArtReference = sideProfileArtReference ?? string.Empty;
            ImageResourceKey = imageResourceKey ?? string.Empty;
            DamagePerShot = damagePerShot;
            ProjectilesPerShot = projectilesPerShot;
            RateOfFire = rateOfFire;
        }

        public string DisplayName { get; }
        public string SideProfileArtReference { get; }
        public string ImageResourceKey { get; }
        public double DamagePerShot { get; }
        public int ProjectilesPerShot { get; }
        public double RateOfFire { get; }

        public static bool TryCreate(
            GunCatalog catalog,
            string definitionId,
            out GunInventoryCardPresentation presentation,
            out string rejectionCode)
        {
            presentation = null;
            rejectionCode = string.Empty;
            if (catalog == null)
            {
                rejectionCode = "inventory-gun-card-catalog-missing";
                return false;
            }
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                rejectionCode = "inventory-gun-card-definition-missing";
                return false;
            }

            GunDefinitionData definition;
            if (!catalog.TryGetDefinition(definitionId, out definition)
                || definition == null)
            {
                rejectionCode = "inventory-gun-card-definition-unknown:"
                    + definitionId.Trim();
                return false;
            }

            int projectiles = Math.Max(1, definition.ProjectilesPerTrigger);
            string sideProfile = definition.SideProfileArtReferences.Count == 0
                ? string.Empty
                : definition.SideProfileArtReferences[0];
            presentation = new GunInventoryCardPresentation(
                definition.DisplayName,
                sideProfile,
                TemporaryImageResourceKey,
                definition.DamagePerProjectile * projectiles,
                projectiles,
                definition.FireRate);
            return true;
        }
    }
}


using System;
using ShooterMover.Application.Weapons.Catalog;
using UnityEngine;

namespace ShooterMover.UI.ProductionFlow
{
    public sealed class ResourcesWeaponCatalogSourceV1 : IWeaponCatalogSourceV1
    {
        public const string BaselineResourcePath =
            "WeaponCatalog/weapon_baseline_v01";

        private readonly string resourcePath;

        public ResourcesWeaponCatalogSourceV1(string sourceId, string path)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("A catalog source identity is required.", nameof(sourceId));
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A Resources TextAsset path is required.", nameof(path));
            }
            SourceId = sourceId.Trim();
            resourcePath = path.Trim();
        }

        public string SourceId { get; }

        public string ReadJson()
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "weapon-catalog-text-asset-missing:" + resourcePath);
            }
            return asset.text;
        }
    }

    internal static class ProductionWeaponCatalogCompositionV1
    {
        public static CanonicalWeaponCatalogProjectionV1 CreateBaseline()
        {
            CanonicalWeaponCatalogProjectionV1 projection;
            string diagnostic;
            if (!CanonicalWeaponCatalogProjectionV1.TryCreate(
                    new ResourcesWeaponCatalogSourceV1(
                        "resources:WeaponCatalog/weapon_baseline_v01",
                        ResourcesWeaponCatalogSourceV1.BaselineResourcePath),
                    WeaponRarityNormalizationPolicyV1.CreateBaselineV1(),
                    out projection,
                    out diagnostic))
            {
                throw new InvalidOperationException(
                    "Production weapon catalog composition failed: " + diagnostic);
            }
            return projection;
        }
    }
}

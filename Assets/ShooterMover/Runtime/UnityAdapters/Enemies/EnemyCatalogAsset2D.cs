using ShooterMover.Application.Enemies.Catalog;
using ShooterMover.Content.Definitions.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    [CreateAssetMenu(
        menuName = "Shooter Mover/Enemies/Enemy Catalog",
        fileName = "EnemyCatalog")]
    public sealed class EnemyCatalogAsset2D : ScriptableObject
    {
        [SerializeField] private TextAsset enemyCatalog;

        public TextAsset Source
        {
            get { return enemyCatalog; }
        }

        public EnemyCatalogImportResultV1 Import()
        {
            if (enemyCatalog == null)
            {
                return new EnemyCatalogImportResultV1(
                    null,
                    new[]
                    {
                        new EnemyCatalogIssueV1(
                            "enemy-catalog-asset-missing",
                            "$",
                            "An enemy catalogue TextAsset is required."),
                    });
            }

            return EnemyCatalogJsonImporterV1.Import(
                enemyCatalog.text,
                BuiltInEnemyCatalogRegistryV1.Create());
        }
    }
}

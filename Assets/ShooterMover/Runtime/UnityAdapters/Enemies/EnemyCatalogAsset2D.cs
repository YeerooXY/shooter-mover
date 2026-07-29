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

        public EnemyCatalogImportResult Import()
        {
            if (enemyCatalog == null)
            {
                return new EnemyCatalogImportResult(
                    null,
                    new[]
                    {
                        new EnemyCatalogIssue(
                            "enemy-catalog-asset-missing",
                            "$",
                            "An enemy catalogue TextAsset is required."),
                    });
            }

            return EnemyCatalogJsonImporter.Import(
                enemyCatalog.text,
                BuiltInEnemyCatalogRegistry.Create());
        }
    }
}

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyAuthoringResetTests
    {
        [TestCase("ShooterMover/Content/Definitions/Enemies/Json/enemy_catalog_v1.json")]
        [TestCase("ShooterMover/Resources/EnemyCatalog/enemy_catalog_v2.json")]
        [TestCase("ShooterMover/Resources/Levels/Level1EnemyCatalog.asset")]
        [TestCase("ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonImporter.cs")]
        [TestCase("ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCatalogAsset.cs")]
        [TestCase("ShooterMover/Runtime/EnemyRuntimeComposition/EnemyFactory.cs")]
        [TestCase("ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemies.cs")]
        [TestCase("ShooterMover/Editor/EnemyReadiness/EnemyReadinessWindow.cs")]
        [TestCase("ShooterMover/Resources/Levels/MobileBlasterDroidPresentation.prefab")]
        [TestCase("ShooterMover/Resources/Levels/BlasterTurretPresentation.prefab")]
        public void RetiredEnemyArchitectureRemainsAbsent(string relativeAssetPath)
        {
            string normalized = relativeAssetPath.Replace(
                '/',
                Path.DirectorySeparatorChar);
            string absolute = Path.Combine(Application.dataPath, normalized);

            Assert.That(
                File.Exists(absolute),
                Is.False,
                "The retired enemy architecture was reintroduced: " + relativeAssetPath);
        }
    }
}

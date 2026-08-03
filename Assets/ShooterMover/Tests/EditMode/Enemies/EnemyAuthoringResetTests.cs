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
        public void RetiredEnemyAuthoringAssetsRemainAbsent(string relativeAssetPath)
        {
            string normalized = relativeAssetPath.Replace(
                '/',
                Path.DirectorySeparatorChar);
            string absolute = Path.Combine(Application.dataPath, normalized);

            Assert.That(
                File.Exists(absolute),
                Is.False,
                "The retired enemy authoring asset was reintroduced: " + relativeAssetPath);
        }
    }
}

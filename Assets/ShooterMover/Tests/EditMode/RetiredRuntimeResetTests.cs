using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ShooterMover.Tests.EditMode
{
    public sealed class RetiredRuntimeResetTests
    {
        [TestCase("Assets/ShooterMover/UI/Game/PlayerGuns.cs")]
        [TestCase("Assets/ShooterMover/Runtime/UnityAdapters/Guns/Live/InventoryGunLiveSetup.cs")]
        [TestCase("Assets/ShooterMover/Runtime/UnityAdapters/Guns/Live/InventoryBackedGunExecutionBridge.cs")]
        [TestCase("Assets/ShooterMover/Runtime/UnityAdapters/Guns/Live/BulletSpawner.cs")]
        [TestCase("Assets/ShooterMover/Runtime/Application/Items/ItemPackageCatalog.cs")]
        [TestCase("Assets/ShooterMover/Runtime/Application/Items/Generated/ItemPackageSources.g.cs")]
        [TestCase("tools/item-maker/compile-packages.js")]
        [TestCase("tools/benchmarks/fire_loop_benchmark.py")]
        public void RetiredRuntimeFilesRemainAbsent(string repositoryPath)
        {
            string repositoryRoot = Directory.GetParent(
                UnityEngine.Application.dataPath).FullName;
            string absolute = Path.Combine(
                repositoryRoot,
                repositoryPath.Replace('/', Path.DirectorySeparatorChar));

            Assert.That(
                File.Exists(absolute),
                Is.False,
                "A retired runtime or authoring path was reintroduced: "
                + repositoryPath);
        }
    }
}

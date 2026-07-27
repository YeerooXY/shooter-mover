using System.Collections;
using NUnit.Framework;
using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.ProductionFlow
{
    public sealed class LevelGridV2CompiledAssetPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrackedCombatLoopAsset_LoadsAndImportsAfterRuntimeFrame()
        {
            yield return null;

            JsonRoomContentDefinition2D asset =
                Resources.Load<JsonRoomContentDefinition2D>(
                    "ProductionLevels/CombatLoopTestRoomContent");
            Assert.That(asset, Is.Not.Null);

            RoomContentImportResultV1 imported = asset.Import();
            Assert.That(imported.IsValid, Is.True,
                imported.Issues.Count == 0
                    ? string.Empty
                    : imported.Issues[0].Code + " at " + imported.Issues[0].Path);
            Assert.That(imported.Bundle, Is.Not.Null);
            Assert.That(imported.Bundle.Enemies.Count, Is.EqualTo(3));
        }
    }
}

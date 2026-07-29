#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Levels.Selection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.Tests.EditMode.Flow
{
    public sealed class PlayableLevelCatalogAvailabilityTests
    {
        private const string LevelSelectionScenePath =
            "Assets/ShooterMover/Scenes/Flow/LevelSelection/LevelSelection.unity";

        [Test]
        public void DefaultCatalog_AdvertisesOnlyResolvableProductionContent()
        {
            Assert.That(
                PlayableLevelCatalog.All.Select(
                    value => value.LevelStableId),
                Is.EquivalentTo(new[]
                {
                    PlayableLevelCatalog.FirstLevelStableId,
                }));

            PlayableLevelDefinition definition;
            Assert.That(
                PlayableLevelCatalog.TryResolve(
                    PlayableLevelCatalog.FirstLevelStableId,
                    out definition),
                Is.True);
            Assert.That(definition, Is.Not.Null);

            Assert.That(
                PlayableLevelCatalog.TryResolve(
                    PlayableLevelCatalog
                        .AuthoredCombatLoopTestLevelStableId,
                    out definition),
                Is.False);
            Assert.That(definition, Is.Null);
        }

        [Test]
        public void LevelSelectionScene_UsesProductionDefaultCatalog()
        {
            Scene scene = EditorSceneManager.OpenScene(
                LevelSelectionScenePath,
                OpenSceneMode.Additive);

            try
            {
                MonoBehaviour controller = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Single(behaviour =>
                    {
                        MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
                        System.Type type = script == null ? null : script.GetClass();
                        return type != null
                            && type.FullName ==
                            "ShooterMover.UI.LevelSelection.LevelSelectionController";
                    });

                var serializedController = new SerializedObject(controller);
                SerializedProperty catalogOverride =
                    serializedController.FindProperty("levelCatalog");

                Assert.That(catalogOverride, Is.Not.Null);
                Assert.That(
                    catalogOverride.objectReferenceValue,
                    Is.Null,
                    "The production Level Selection scene must use the canonical "
                    + "default catalog instead of a serialized override asset.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif

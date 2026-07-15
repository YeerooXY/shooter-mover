using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShooterMover.ContentPackages.Rooms.Stage1VisibleSlicePresentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.VisibleSliceRoomPresentation
{
    public sealed class Stage1VisibleSliceRoomPresentationTests
    {
        private const string FloorGuid = "8de5d0331ff04de4d907f37c89c6ffc6";
        private const string CrateGuid = "372cfb91a69c59447b4e786a52011d16";

        private readonly List<UnityEngine.Object> _ownedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _ownedObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object owned = _ownedObjects[index];
                if (owned != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }

            _ownedObjects.Clear();
        }

        [Test]
        public void AssignedFloor_UsesProvidedSpriteAndPrefabBindsAcceptedVs001Assets()
        {
            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();
            Sprite floor = CreateSprite("Assigned Floor", 8, 8);
            Sprite crate = CreateSprite("Assigned Crate", 12, 8);

            presentation.SetFloorSprite(floor);
            presentation.SetCrateSprite(crate);

            Assert.That(presentation.IsUsingFallbackFloor, Is.False);
            Assert.That(presentation.FloorRenderer, Is.Not.Null);
            Assert.That(presentation.FloorRenderer.sprite, Is.SameAs(floor));
            Assert.That(presentation.FloorRenderer.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(presentation.FloorRenderer.size, Is.EqualTo(presentation.RoomSize));
            Assert.That(
                presentation.FloorRenderer.sortingOrder,
                Is.EqualTo(Stage1VisibleSliceRoomPresentation.FloorSortingOrder));
            Assert.That(
                presentation.FloorRenderer.sortingLayerName,
                Is.EqualTo(Stage1VisibleSliceRoomPresentation.SortingLayer));
            Assert.That(presentation.FloorFallbackMarker.activeSelf, Is.False);

            string prefabText = File.ReadAllText(GetOwnedPackagePath("Stage1VisibleSliceRoomPresentation.prefab"));
            Assert.That(prefabText, Does.Contain("guid: " + FloorGuid));
            Assert.That(prefabText, Does.Contain("guid: " + CrateGuid));
            Assert.That(prefabText, Does.Contain("_doorSprite: {fileID: 0}"));
            Assert.That(prefabText, Does.Contain("_explosiveSprite: {fileID: 0}"));
        }

        [Test]
        public void CleanDoorAndExplosiveBindings_AppearOnlyWhenAssigned()
        {
            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();
            Sprite door = CreateSprite("Clean Door", 4, 10);
            Sprite explosive = CreateSprite("Clean Explosive", 8, 8);

            presentation.SetDoorSprite(door);
            presentation.SetExplosiveSprite(explosive);

            Assert.That(presentation.DoorRoot.childCount, Is.EqualTo(2));
            for (int index = 0; index < presentation.DoorRoot.childCount; index++)
            {
                SpriteRenderer renderer =
                    presentation.DoorRoot.GetChild(index).GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sprite, Is.SameAs(door));
            }

            Assert.That(presentation.PropRoot.childCount, Is.EqualTo(4));
            Transform optionalExplosive =
                FindDescendant(presentation.PropRoot, "Explosive_Optional");
            Assert.That(optionalExplosive, Is.Not.Null);
            Assert.That(
                optionalExplosive.GetComponent<SpriteRenderer>().sprite,
                Is.SameAs(explosive));

            presentation.SetDoorSprite(null);
            presentation.SetExplosiveSprite(null);

            Assert.That(presentation.PropRoot.childCount, Is.EqualTo(3));
            Assert.That(
                FindDescendant(presentation.PropRoot, "Explosive_Optional"),
                Is.Null);
            Assert.That(
                presentation.DoorRoot.GetComponentsInChildren<SpriteRenderer>(true).Length,
                Is.EqualTo(4));
        }

        [Test]
        public void MissingFloor_UsesGracefulPresentationOnlyFallback()
        {
            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();

            presentation.SetFloorSprite(null);

            Assert.That(presentation.IsUsingFallbackFloor, Is.True);
            Assert.That(presentation.FloorRenderer, Is.Not.Null);
            Assert.That(presentation.FloorRenderer.sprite, Is.Not.Null);
            Assert.That(presentation.FloorFallbackMarker, Is.Not.Null);
            Assert.That(presentation.FloorFallbackMarker.activeSelf, Is.True);
            Assert.That(presentation.FloorRenderer.drawMode, Is.EqualTo(SpriteDrawMode.Tiled));
            Assert.That(presentation.FloorRenderer.size, Is.EqualTo(new Vector2(32f, 18f)));
        }

        [Test]
        public void Rebuild_ProducesDeterministicHierarchyAndExplicitSorting()
        {
            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();
            Sprite floor = CreateSprite("Floor", 8, 8);
            Sprite crate = CreateSprite("Crate", 12, 8);
            presentation.SetFloorSprite(floor);
            presentation.SetCrateSprite(crate);

            string first = presentation.BuildHierarchySignature();
            presentation.Rebuild();
            string second = presentation.BuildHierarchySignature();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(
                DirectChildNames(presentation.transform),
                Is.EqualTo(new[]
                {
                    "00_Floor",
                    "10_Walls",
                    "20_Doors",
                    "30_Props",
                    "40_IntegrationMarkers",
                    "90_OptionalEffects"
                }));

            AssertGroupSorting(presentation.WallRoot, Stage1VisibleSliceRoomPresentation.WallSortingOrder);
            AssertGroupSorting(presentation.DoorRoot, Stage1VisibleSliceRoomPresentation.DoorSortingOrder);
            AssertGroupSorting(presentation.PropRoot, Stage1VisibleSliceRoomPresentation.PropSortingOrder);
            AssertGroupSorting(presentation.MarkerRoot, Stage1VisibleSliceRoomPresentation.MarkerSortingOrder);
            AssertGroupSorting(
                presentation.OptionalEffectsRoot.transform,
                Stage1VisibleSliceRoomPresentation.OptionalEffectSortingOrder);
        }

        [Test]
        public void MarkersAndSmallPropArrangement_StayVisibleAndInsideRoomBounds()
        {
            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();
            presentation.SetCrateSprite(CreateSprite("Crate", 12, 8));

            string[] markerNames =
            {
                "Marker_RoomCenter",
                "Marker_PlayerSpawn",
                "Marker_EnemySpawn_A",
                "Marker_Door_West",
                "Marker_Door_East"
            };

            Assert.That(presentation.MarkerRoot, Is.Not.Null);
            foreach (string markerName in markerNames)
            {
                Transform marker = FindDescendant(presentation.MarkerRoot, markerName);
                Assert.That(marker, Is.Not.Null, markerName);
                Assert.That(marker.gameObject.activeInHierarchy, Is.True, markerName);
            }

            Assert.That(presentation.PropRoot.childCount, Is.EqualTo(3));
            Rect roomBounds = presentation.RoomBounds;
            for (int index = 0; index < presentation.PropRoot.childCount; index++)
            {
                Transform prop = presentation.PropRoot.GetChild(index);
                Vector2 point = prop.localPosition;
                Assert.That(roomBounds.Contains(point), Is.True, prop.name);
                Assert.That(
                    Mathf.Abs(point.x),
                    Is.LessThan(roomBounds.width * 0.5f - 1f),
                    prop.name);
                Assert.That(
                    Mathf.Abs(point.y),
                    Is.LessThan(roomBounds.height * 0.5f - 1f),
                    prop.name);

                SpriteRenderer renderer = prop.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null, prop.name);
                Bounds visualBounds = renderer.bounds;
                Assert.That(visualBounds.min.x, Is.GreaterThan(roomBounds.xMin), prop.name);
                Assert.That(visualBounds.max.x, Is.LessThan(roomBounds.xMax), prop.name);
                Assert.That(visualBounds.min.y, Is.GreaterThan(roomBounds.yMin), prop.name);
                Assert.That(visualBounds.max.y, Is.LessThan(roomBounds.yMax), prop.name);
            }

            Assert.That(
                Stage1VisibleSliceRoomPresentation.MarkerSortingOrder,
                Is.LessThan(0));
        }

        [Test]
        public void ReducedEffects_RemovesOnlyOptionalAccents()
        {
            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();
            presentation.SetFloorSprite(CreateSprite("Floor", 8, 8));
            presentation.SetCrateSprite(CreateSprite("Crate", 12, 8));

            int essentialRendererCount = CountEssentialRenderers(presentation);
            Assert.That(presentation.OptionalEffectsRoot.activeSelf, Is.True);

            presentation.SetReducedEffects(true);

            Assert.That(presentation.ReducedEffects, Is.True);
            Assert.That(presentation.OptionalEffectsRoot.activeSelf, Is.False);
            Assert.That(presentation.FloorRenderer.gameObject.activeInHierarchy, Is.True);
            Assert.That(presentation.WallRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(presentation.DoorRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(presentation.PropRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(presentation.MarkerRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(CountEssentialRenderers(presentation), Is.EqualTo(essentialRendererCount));

            presentation.SetReducedEffects(false);
            Assert.That(presentation.OptionalEffectsRoot.activeSelf, Is.True);
        }

        [Test]
        public void ComponentAudit_AllowsOnlyPresentationComponents()
        {
            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();
            Component[] components = presentation.GetComponentsInChildren<Component>(true);

            foreach (Component component in components)
            {
                Type type = component.GetType();
                bool allowed =
                    type == typeof(Transform) ||
                    type == typeof(SpriteRenderer) ||
                    type == typeof(Stage1VisibleSliceRoomPresentation);

                Assert.That(allowed, Is.True, "Forbidden component: " + type.FullName);
                Assert.That(component, Is.Not.InstanceOf<Collider2D>());
                Assert.That(component, Is.Not.InstanceOf<Rigidbody2D>());
                Assert.That(component, Is.Not.InstanceOf<Camera>());

                string typeName = type.FullName ?? type.Name;
                Assert.That(typeName, Does.Not.Contain("Mission"));
                Assert.That(typeName, Does.Not.Contain("Encounter"));
                Assert.That(typeName, Does.Not.Contain("Persistence"));
                Assert.That(typeName, Does.Not.Contain("Reward"));
                Assert.That(typeName, Does.Not.Contain("GameplayAuthority"));
            }
        }

        [UnityTest]
        public IEnumerator RemovingPackage_LeavesIndependentGameplayStateUntouched()
        {
            GameObject gameplayObject = new GameObject("Accepted Gameplay Sentinel");
            _ownedObjects.Add(gameplayObject);
            GameplaySentinel sentinel = new GameplaySentinel
            {
                Generation = 17,
                Health = 73f
            };

            Stage1VisibleSliceRoomPresentation presentation = CreatePresentation();
            GameObject packageObject = presentation.gameObject;
            _ownedObjects.Remove(packageObject);
            UnityEngine.Object.Destroy(packageObject);
            yield return null;

            Assert.That(gameplayObject, Is.Not.Null);
            Assert.That(sentinel.Generation, Is.EqualTo(17));
            Assert.That(sentinel.Health, Is.EqualTo(73f));
        }

        [Test]
        public void OwnedPackage_ContainsNoScenesAndUsesOnlyTaskLocalFiles()
        {
            string packageRoot = GetOwnedPackagePath(string.Empty);
            string testRoot = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover",
                "Tests",
                "PlayMode",
                "VisibleSliceRoomPresentation");

            string[] packageFiles = Directory.GetFiles(packageRoot, "*", SearchOption.AllDirectories);
            string[] testFiles = Directory.GetFiles(testRoot, "*", SearchOption.AllDirectories);

            Assert.That(packageFiles.Any(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(testFiles.Any(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(packageFiles.Length, Is.GreaterThan(0));
            Assert.That(testFiles.Length, Is.GreaterThan(0));
        }

        private Stage1VisibleSliceRoomPresentation CreatePresentation()
        {
            GameObject host = new GameObject("VS-002 Room Presentation Test");
            _ownedObjects.Add(host);
            return host.AddComponent<Stage1VisibleSliceRoomPresentation>();
        }

        private Sprite CreateSprite(string name, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name + " Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = Enumerable.Repeat(Color.white, width * height).ToArray();
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _ownedObjects.Add(texture);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                4f,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = name;
            _ownedObjects.Add(sprite);
            return sprite;
        }

        private static string[] DirectChildNames(Transform root)
        {
            string[] names = new string[root.childCount];
            for (int index = 0; index < root.childCount; index++)
            {
                names[index] = root.GetChild(index).name;
            }

            return names;
        }

        private static void AssertGroupSorting(Transform root, int expectedOrder)
        {
            Assert.That(root, Is.Not.Null);
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));

            foreach (SpriteRenderer renderer in renderers)
            {
                Assert.That(
                    renderer.sortingLayerName,
                    Is.EqualTo(Stage1VisibleSliceRoomPresentation.SortingLayer),
                    renderer.name);
                Assert.That(
                    renderer.sortingOrder,
                    Is.EqualTo(expectedOrder).Or.EqualTo(expectedOrder + 1),
                    renderer.name);
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }

                Transform nested = FindDescendant(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static int CountEssentialRenderers(Stage1VisibleSliceRoomPresentation presentation)
        {
            int floorCount = presentation.FloorRenderer != null ? 1 : 0;
            return floorCount
                + presentation.WallRoot.GetComponentsInChildren<SpriteRenderer>(true).Length
                + presentation.DoorRoot.GetComponentsInChildren<SpriteRenderer>(true).Length
                + presentation.PropRoot.GetComponentsInChildren<SpriteRenderer>(true).Length
                + presentation.MarkerRoot.GetComponentsInChildren<SpriteRenderer>(true).Length;
        }

        private static string GetOwnedPackagePath(string relativePath)
        {
            string root = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover",
                "ContentPackages",
                "Rooms",
                "Stage1VisibleSlicePresentation");
            return string.IsNullOrEmpty(relativePath) ? root : Path.Combine(root, relativePath);
        }

        private sealed class GameplaySentinel
        {
            public int Generation { get; set; }
            public float Health { get; set; }
        }
    }
}

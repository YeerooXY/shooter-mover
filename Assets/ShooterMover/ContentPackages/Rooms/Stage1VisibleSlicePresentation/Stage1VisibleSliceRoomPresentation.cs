using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ShooterMover.ContentPackages.Rooms.Stage1VisibleSlicePresentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Stage1VisibleSliceRoomPresentation : MonoBehaviour
    {
        public const string SortingLayer = "Default";
        public const int FloorSortingOrder = -300;
        public const int WallSortingOrder = -220;
        public const int OptionalEffectSortingOrder = -200;
        public const int DoorSortingOrder = -180;
        public const int PropSortingOrder = -120;
        public const int MarkerSortingOrder = -60;

        private static readonly string[] GeneratedRootNames =
        {
            "00_Floor",
            "10_Walls",
            "20_Doors",
            "30_Props",
            "40_IntegrationMarkers",
            "90_OptionalEffects"
        };

        [Header("Replaceable VS-001 sprite bindings")]
        [SerializeField] private Sprite _floorSprite;
        [SerializeField] private Sprite _crateSprite;
        [SerializeField] private Sprite _doorSprite;
        [SerializeField] private Sprite _explosiveSprite;

        [Header("Presentation-only configuration")]
        [SerializeField] private bool _reducedEffects;
        [SerializeField] private Vector2 _roomSize = new Vector2(32f, 18f);
        [SerializeField] private float _wallThickness = 0.75f;
        [SerializeField] private Color _floorTint = new Color(0.34f, 0.38f, 0.42f, 1f);
        [SerializeField] private Color _fallbackFloorTint = new Color(0.10f, 0.12f, 0.15f, 1f);
        [SerializeField] private Color _wallTint = new Color(0.08f, 0.10f, 0.14f, 1f);
        [SerializeField] private Color _doorTint = new Color(0.62f, 0.47f, 0.18f, 1f);
        [SerializeField] private Color _propTint = new Color(0.70f, 0.74f, 0.78f, 1f);
        [SerializeField] private Color _markerTint = new Color(0.20f, 0.72f, 0.82f, 0.65f);
        [SerializeField] private Color _optionalAccentTint = new Color(0.18f, 0.62f, 0.72f, 0.28f);

        private Sprite _generatedPixelSprite;
        private Texture2D _generatedPixelTexture;
        private SpriteRenderer _floorRenderer;
        private GameObject _floorFallbackMarker;
        private Transform _wallRoot;
        private Transform _doorRoot;
        private Transform _propRoot;
        private Transform _markerRoot;
        private GameObject _optionalEffectsRoot;
        private bool _isUsingFallbackFloor;

        public Sprite FloorSprite => _floorSprite;
        public Sprite CrateSprite => _crateSprite;
        public Sprite DoorSprite => _doorSprite;
        public Sprite ExplosiveSprite => _explosiveSprite;
        public bool ReducedEffects => _reducedEffects;
        public bool IsUsingFallbackFloor => _isUsingFallbackFloor;
        public Vector2 RoomSize => _roomSize;
        public float WallThickness => _wallThickness;
        public Rect RoomBounds => new Rect(-_roomSize.x * 0.5f, -_roomSize.y * 0.5f, _roomSize.x, _roomSize.y);
        public SpriteRenderer FloorRenderer => _floorRenderer;
        public GameObject FloorFallbackMarker => _floorFallbackMarker;
        public Transform WallRoot => _wallRoot;
        public Transform DoorRoot => _doorRoot;
        public Transform PropRoot => _propRoot;
        public Transform MarkerRoot => _markerRoot;
        public GameObject OptionalEffectsRoot => _optionalEffectsRoot;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            _roomSize.x = Mathf.Max(8f, _roomSize.x);
            _roomSize.y = Mathf.Max(8f, _roomSize.y);
            _wallThickness = Mathf.Clamp(_wallThickness, 0.25f, 2f);
        }

        private void OnDestroy()
        {
            ReleaseGeneratedPixel();
        }

        public void SetFloorSprite(Sprite sprite)
        {
            _floorSprite = sprite;
            Rebuild();
        }

        public void SetCrateSprite(Sprite sprite)
        {
            _crateSprite = sprite;
            Rebuild();
        }

        public void SetDoorSprite(Sprite sprite)
        {
            _doorSprite = sprite;
            Rebuild();
        }

        public void SetExplosiveSprite(Sprite sprite)
        {
            _explosiveSprite = sprite;
            Rebuild();
        }

        public void SetReducedEffects(bool reducedEffects)
        {
            _reducedEffects = reducedEffects;
            if (_optionalEffectsRoot != null)
            {
                _optionalEffectsRoot.SetActive(!_reducedEffects);
            }
        }

        public void Rebuild()
        {
            ClearGeneratedHierarchy();
            ReleaseGeneratedPixel();
            CreateGeneratedPixel();

            BuildFloor();
            BuildWalls();
            BuildDoors();
            BuildProps();
            BuildMarkers();
            BuildOptionalEffects();
            SetReducedEffects(_reducedEffects);
        }

        public string BuildHierarchySignature()
        {
            StringBuilder builder = new StringBuilder();
            AppendSignature(transform, builder, 0);
            return builder.ToString();
        }

        private void BuildFloor()
        {
            Transform floorRoot = CreateGeneratedRoot("00_Floor");
            _isUsingFallbackFloor = _floorSprite == null;

            GameObject floor = CreateChild(floorRoot, "Floor_Tiled");
            _floorRenderer = floor.AddComponent<SpriteRenderer>();
            _floorRenderer.sprite = _floorSprite != null ? _floorSprite : _generatedPixelSprite;
            _floorRenderer.drawMode = SpriteDrawMode.Tiled;
            _floorRenderer.tileMode = SpriteTileMode.Continuous;
            _floorRenderer.size = _roomSize;
            _floorRenderer.color = _floorSprite != null ? _floorTint : _fallbackFloorTint;
            ConfigureSorting(_floorRenderer, FloorSortingOrder);

            _floorFallbackMarker = CreateChild(floorRoot, "FloorFallback_MissingSprite");
            CreateRect(
                _floorFallbackMarker.transform,
                "Fallback_Bar_Horizontal",
                new Vector2(4.5f, 0.18f),
                Vector2.zero,
                new Color(0.85f, 0.30f, 0.22f, 0.70f),
                FloorSortingOrder + 1,
                0f);
            CreateRect(
                _floorFallbackMarker.transform,
                "Fallback_Bar_Vertical",
                new Vector2(0.18f, 4.5f),
                Vector2.zero,
                new Color(0.85f, 0.30f, 0.22f, 0.70f),
                FloorSortingOrder + 1,
                0f);
            _floorFallbackMarker.SetActive(_isUsingFallbackFloor);
        }

        private void BuildWalls()
        {
            _wallRoot = CreateGeneratedRoot("10_Walls");
            float halfWidth = _roomSize.x * 0.5f;
            float halfHeight = _roomSize.y * 0.5f;
            float halfWall = _wallThickness * 0.5f;

            CreateRect(
                _wallRoot,
                "Wall_North",
                new Vector2(_roomSize.x, _wallThickness),
                new Vector2(0f, halfHeight - halfWall),
                _wallTint,
                WallSortingOrder,
                0f);
            CreateRect(
                _wallRoot,
                "Wall_South",
                new Vector2(_roomSize.x, _wallThickness),
                new Vector2(0f, -halfHeight + halfWall),
                _wallTint,
                WallSortingOrder,
                0f);
            CreateRect(
                _wallRoot,
                "Wall_West",
                new Vector2(_wallThickness, _roomSize.y),
                new Vector2(-halfWidth + halfWall, 0f),
                _wallTint,
                WallSortingOrder,
                0f);
            CreateRect(
                _wallRoot,
                "Wall_East",
                new Vector2(_wallThickness, _roomSize.y),
                new Vector2(halfWidth - halfWall, 0f),
                _wallTint,
                WallSortingOrder,
                0f);
        }

        private void BuildDoors()
        {
            _doorRoot = CreateGeneratedRoot("20_Doors");
            float x = _roomSize.x * 0.5f - (_wallThickness * 0.5f);

            CreateDoor("Door_West", new Vector2(-x, 0f), 90f);
            CreateDoor("Door_East", new Vector2(x, 0f), -90f);
        }

        private void CreateDoor(string name, Vector2 localPosition, float fallbackRotation)
        {
            GameObject door = CreateChild(_doorRoot, name);
            door.transform.localPosition = localPosition;

            if (_doorSprite != null)
            {
                SpriteRenderer renderer = door.AddComponent<SpriteRenderer>();
                renderer.sprite = _doorSprite;
                renderer.color = Color.white;
                ConfigureSorting(renderer, DoorSortingOrder);
                FitSpriteToSize(door.transform, _doorSprite, new Vector2(1.4f, 3.6f));
                return;
            }

            CreateRect(
                door.transform,
                name + "_Panel",
                new Vector2(1.10f, 3.40f),
                Vector2.zero,
                new Color(_doorTint.r, _doorTint.g, _doorTint.b, 0.75f),
                DoorSortingOrder,
                0f);
            CreateRect(
                door.transform,
                name + "_Marker",
                new Vector2(0.55f, 0.55f),
                Vector2.zero,
                new Color(0.90f, 0.72f, 0.24f, 0.90f),
                DoorSortingOrder + 1,
                fallbackRotation + 45f);
        }

        private void BuildProps()
        {
            _propRoot = CreateGeneratedRoot("30_Props");
            CreateCrate("Crate_NorthWest", new Vector2(-10.0f, 5.2f), 0f);
            CreateCrate("Crate_NorthEast", new Vector2(9.6f, 5.0f), 0f);
            CreateCrate("Crate_SouthWest", new Vector2(-8.6f, -5.2f), 0f);

            if (_explosiveSprite != null)
            {
                CreateSpriteProp(
                    "Explosive_Optional",
                    _explosiveSprite,
                    new Vector2(9.0f, -5.0f),
                    new Vector2(1.6f, 1.6f));
            }
        }

        private void CreateCrate(string name, Vector2 localPosition, float rotation)
        {
            GameObject crate = CreateChild(_propRoot, name);
            crate.transform.localPosition = localPosition;
            crate.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

            SpriteRenderer renderer = crate.AddComponent<SpriteRenderer>();
            renderer.sprite = _crateSprite != null ? _crateSprite : _generatedPixelSprite;
            renderer.color = _crateSprite != null ? _propTint : new Color(0.34f, 0.38f, 0.42f, 1f);
            ConfigureSorting(renderer, PropSortingOrder);

            if (_crateSprite != null)
            {
                FitSpriteToSize(crate.transform, _crateSprite, new Vector2(2.7f, 1.8f));
            }
            else
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(2.2f, 1.4f);
            }
        }

        private void CreateSpriteProp(
            string name,
            Sprite sprite,
            Vector2 localPosition,
            Vector2 desiredSize)
        {
            GameObject prop = CreateChild(_propRoot, name);
            prop.transform.localPosition = localPosition;

            SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            ConfigureSorting(renderer, PropSortingOrder);
            FitSpriteToSize(prop.transform, sprite, desiredSize);
        }

        private void BuildMarkers()
        {
            _markerRoot = CreateGeneratedRoot("40_IntegrationMarkers");

            CreateDiamondMarker("Marker_RoomCenter", Vector2.zero, 0.75f, _markerTint);
            CreateCrossMarker("Marker_PlayerSpawn", new Vector2(-10f, 0f), 1.15f, _markerTint);
            CreateCrossMarker("Marker_EnemySpawn_A", new Vector2(7f, 0f), 1.15f, _markerTint);
            CreateDiamondMarker(
                "Marker_Door_West",
                new Vector2(-_roomSize.x * 0.5f + 1.25f, 0f),
                0.85f,
                new Color(_doorTint.r, _doorTint.g, _doorTint.b, 0.75f));
            CreateDiamondMarker(
                "Marker_Door_East",
                new Vector2(_roomSize.x * 0.5f - 1.25f, 0f),
                0.85f,
                new Color(_doorTint.r, _doorTint.g, _doorTint.b, 0.75f));
        }

        private void BuildOptionalEffects()
        {
            Transform optionalRoot = CreateGeneratedRoot("90_OptionalEffects");
            _optionalEffectsRoot = optionalRoot.gameObject;
            float halfHeight = _roomSize.y * 0.5f;

            CreateRect(
                optionalRoot,
                "Accent_North",
                new Vector2(_roomSize.x - 4f, 0.10f),
                new Vector2(0f, halfHeight - _wallThickness - 0.45f),
                _optionalAccentTint,
                OptionalEffectSortingOrder,
                0f);
            CreateRect(
                optionalRoot,
                "Accent_South",
                new Vector2(_roomSize.x - 4f, 0.10f),
                new Vector2(0f, -halfHeight + _wallThickness + 0.45f),
                _optionalAccentTint,
                OptionalEffectSortingOrder,
                0f);
        }

        private void CreateCrossMarker(string name, Vector2 localPosition, float size, Color tint)
        {
            GameObject marker = CreateChild(_markerRoot, name);
            marker.transform.localPosition = localPosition;

            CreateRect(
                marker.transform,
                name + "_Horizontal",
                new Vector2(size, 0.12f),
                Vector2.zero,
                tint,
                MarkerSortingOrder,
                0f);
            CreateRect(
                marker.transform,
                name + "_Vertical",
                new Vector2(0.12f, size),
                Vector2.zero,
                tint,
                MarkerSortingOrder,
                0f);
        }

        private void CreateDiamondMarker(string name, Vector2 localPosition, float size, Color tint)
        {
            GameObject marker = CreateChild(_markerRoot, name);
            marker.transform.localPosition = localPosition;
            CreateRect(
                marker.transform,
                name + "_Diamond",
                new Vector2(size, size),
                Vector2.zero,
                tint,
                MarkerSortingOrder,
                45f);
        }

        private Transform CreateGeneratedRoot(string name)
        {
            GameObject root = CreateChild(transform, name);
            return root.transform;
        }

        private GameObject CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            child.transform.SetParent(parent, false);
            return child;
        }

        private SpriteRenderer CreateRect(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 localPosition,
            Color tint,
            int sortingOrder,
            float rotationDegrees)
        {
            GameObject rectangle = CreateChild(parent, name);
            rectangle.transform.localPosition = localPosition;
            rectangle.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            SpriteRenderer renderer = rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = _generatedPixelSprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
            renderer.color = tint;
            ConfigureSorting(renderer, sortingOrder);
            return renderer;
        }

        private static void ConfigureSorting(SpriteRenderer renderer, int sortingOrder)
        {
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = sortingOrder;
        }

        private static void FitSpriteToSize(Transform target, Sprite sprite, Vector2 desiredSize)
        {
            Vector2 actual = sprite.bounds.size;
            float x = actual.x > 0.0001f ? desiredSize.x / actual.x : 1f;
            float y = actual.y > 0.0001f ? desiredSize.y / actual.y : 1f;
            target.localScale = new Vector3(x, y, 1f);
        }

        private void ClearGeneratedHierarchy()
        {
            HashSet<string> generatedNames = new HashSet<string>(GeneratedRootNames, StringComparer.Ordinal);
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (generatedNames.Contains(child.name))
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            _floorRenderer = null;
            _floorFallbackMarker = null;
            _wallRoot = null;
            _doorRoot = null;
            _propRoot = null;
            _markerRoot = null;
            _optionalEffectsRoot = null;
        }

        private void CreateGeneratedPixel()
        {
            _generatedPixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "VS-002 Presentation Pixel",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _generatedPixelTexture.SetPixel(0, 0, Color.white);
            _generatedPixelTexture.Apply(false, true);

            _generatedPixelSprite = Sprite.Create(
                _generatedPixelTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f,
                0u,
                SpriteMeshType.FullRect);
            _generatedPixelSprite.name = "VS-002 Presentation Pixel";
            _generatedPixelSprite.hideFlags = HideFlags.DontSave;
        }

        private void ReleaseGeneratedPixel()
        {
            if (_generatedPixelSprite != null)
            {
                DestroyImmediate(_generatedPixelSprite);
                _generatedPixelSprite = null;
            }

            if (_generatedPixelTexture != null)
            {
                DestroyImmediate(_generatedPixelTexture);
                _generatedPixelTexture = null;
            }
        }

        private static void AppendSignature(Transform node, StringBuilder builder, int depth)
        {
            builder.Append(depth);
            builder.Append(':');
            builder.Append(node.name);
            builder.Append('@');
            builder.Append(FormatVector(node.localPosition));
            builder.Append('|');
            builder.Append(FormatVector(node.localScale));

            SpriteRenderer renderer = node.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                builder.Append("|sprite=");
                builder.Append(renderer.sprite != null ? renderer.sprite.name : "<null>");
                builder.Append("|layer=");
                builder.Append(renderer.sortingLayerName);
                builder.Append("|order=");
                builder.Append(renderer.sortingOrder.ToString(CultureInfo.InvariantCulture));
                builder.Append("|draw=");
                builder.Append(renderer.drawMode);
                builder.Append("|active=");
                builder.Append(node.gameObject.activeSelf ? '1' : '0');
            }

            builder.AppendLine();

            for (int index = 0; index < node.childCount; index++)
            {
                AppendSignature(node.GetChild(index), builder, depth + 1);
            }
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.###},{1:0.###},{2:0.###}",
                value.x,
                value.y,
                value.z);
        }
    }
}

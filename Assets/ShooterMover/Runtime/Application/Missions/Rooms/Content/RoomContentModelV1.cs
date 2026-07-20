using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public enum RoomContentObjectKindV1
    {
        Enemy = 1,
        Prop = 2,
        Door = 3,
        Tile = 4,
        Background = 5,
        Foreground = 6,
    }

    public enum RoomContentVisualLayerV1
    {
        Tile = 1,
        Background = 2,
        Foreground = 3,
    }

    public sealed class RoomContentObjectDefinitionV1
    {
        public RoomContentObjectDefinitionV1(
            StableId objectStableId,
            RoomContentObjectKindV1 kind,
            StableId runtimeDefinitionStableId,
            StableId presentationStableId)
        {
            ObjectStableId = objectStableId
                ?? throw new ArgumentNullException(nameof(objectStableId));
            RuntimeDefinitionStableId = runtimeDefinitionStableId
                ?? throw new ArgumentNullException(nameof(runtimeDefinitionStableId));
            PresentationStableId = presentationStableId
                ?? throw new ArgumentNullException(nameof(presentationStableId));
            if (!Enum.IsDefined(typeof(RoomContentObjectKindV1), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Kind = kind;
        }

        public StableId ObjectStableId { get; }

        public RoomContentObjectKindV1 Kind { get; }

        public StableId RuntimeDefinitionStableId { get; }

        public StableId PresentationStableId { get; }
    }

    public interface IRoomContentObjectCatalogV1
    {
        bool TryResolve(
            StableId objectStableId,
            RoomContentObjectKindV1 kind,
            out RoomContentObjectDefinitionV1 definition);
    }

    public sealed class RoomContentObjectCatalogV1 : IRoomContentObjectCatalogV1
    {
        private readonly Dictionary<string, RoomContentObjectDefinitionV1> definitions;

        public RoomContentObjectCatalogV1(
            IEnumerable<RoomContentObjectDefinitionV1> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            this.definitions = new Dictionary<string, RoomContentObjectDefinitionV1>(
                StringComparer.Ordinal);
            foreach (RoomContentObjectDefinitionV1 definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Room content object catalogs cannot contain null definitions.",
                        nameof(definitions));
                }

                string key = Key(definition.ObjectStableId, definition.Kind);
                if (this.definitions.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "room-content-object-duplicate:"
                        + definition.Kind
                        + ":"
                        + definition.ObjectStableId,
                        nameof(definitions));
                }

                this.definitions.Add(key, definition);
            }
        }

        public bool TryResolve(
            StableId objectStableId,
            RoomContentObjectKindV1 kind,
            out RoomContentObjectDefinitionV1 definition)
        {
            definition = null;
            return objectStableId != null
                && Enum.IsDefined(typeof(RoomContentObjectKindV1), kind)
                && definitions.TryGetValue(Key(objectStableId, kind), out definition)
                && definition != null;
        }

        private static string Key(
            StableId objectStableId,
            RoomContentObjectKindV1 kind)
        {
            return ((int)kind).ToString() + "|" + objectStableId;
        }
    }

    public sealed class RoomContentJsonPackageV1
    {
        private readonly ReadOnlyDictionary<string, string> documents;

        public RoomContentJsonPackageV1(
            string manifestJson,
            IDictionary<string, string> documents)
        {
            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                throw new ArgumentException(
                    "A room-content manifest JSON document is required.",
                    nameof(manifestJson));
            }
            if (documents == null)
            {
                throw new ArgumentNullException(nameof(documents));
            }

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in documents)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new ArgumentException(
                        "Room-content document keys cannot be blank.",
                        nameof(documents));
                }
                if (string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException(
                        "Room-content documents cannot be blank: " + pair.Key,
                        nameof(documents));
                }
                if (copy.ContainsKey(pair.Key))
                {
                    throw new ArgumentException(
                        "room-content-document-duplicate:" + pair.Key,
                        nameof(documents));
                }

                copy.Add(pair.Key, pair.Value);
            }

            ManifestJson = manifestJson;
            this.documents = new ReadOnlyDictionary<string, string>(copy);
        }

        public string ManifestJson { get; }

        public IReadOnlyDictionary<string, string> Documents
        {
            get { return documents; }
        }

        public bool TryGetDocument(string key, out string json)
        {
            json = null;
            return !string.IsNullOrWhiteSpace(key)
                && documents.TryGetValue(key, out json)
                && !string.IsNullOrWhiteSpace(json);
        }
    }

    public sealed class RoomEnemyPlacementContentV1
    {
        public RoomEnemyPlacementContentV1(
            StableId instanceStableId,
            StableId roomStableId,
            StableId objectStableId,
            int level,
            RoomVector2V1 localPosition,
            double localRotationDegrees,
            string authoredId)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            ObjectStableId = objectStableId
                ?? throw new ArgumentNullException(nameof(objectStableId));
            if (level <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            Level = level;
            LocalPosition = localPosition
                ?? throw new ArgumentNullException(nameof(localPosition));
            LocalRotationDegrees = localRotationDegrees;
            AuthoredId = string.IsNullOrWhiteSpace(authoredId)
                ? null
                : authoredId.Trim();
        }

        public StableId InstanceStableId { get; }

        public StableId RoomStableId { get; }

        public StableId ObjectStableId { get; }

        public int Level { get; }

        public RoomVector2V1 LocalPosition { get; }

        public double LocalRotationDegrees { get; }

        public string AuthoredId { get; }
    }

    public sealed class RoomPropPlacementContentV1
    {
        public RoomPropPlacementContentV1(
            StableId instanceStableId,
            StableId roomStableId,
            StableId objectStableId,
            RoomVector2V1 localPosition,
            double localRotationDegrees,
            string authoredId)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            ObjectStableId = objectStableId
                ?? throw new ArgumentNullException(nameof(objectStableId));
            LocalPosition = localPosition
                ?? throw new ArgumentNullException(nameof(localPosition));
            LocalRotationDegrees = localRotationDegrees;
            AuthoredId = string.IsNullOrWhiteSpace(authoredId)
                ? null
                : authoredId.Trim();
        }

        public StableId InstanceStableId { get; }

        public StableId RoomStableId { get; }

        public StableId ObjectStableId { get; }

        public RoomVector2V1 LocalPosition { get; }

        public double LocalRotationDegrees { get; }

        public string AuthoredId { get; }
    }

    public sealed class RoomVisualPlacementContentV1
    {
        public RoomVisualPlacementContentV1(
            StableId instanceStableId,
            StableId roomStableId,
            StableId objectStableId,
            RoomContentVisualLayerV1 layer,
            RoomVector2V1 localPosition,
            double localRotationDegrees)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            ObjectStableId = objectStableId
                ?? throw new ArgumentNullException(nameof(objectStableId));
            if (!Enum.IsDefined(typeof(RoomContentVisualLayerV1), layer))
            {
                throw new ArgumentOutOfRangeException(nameof(layer));
            }

            Layer = layer;
            LocalPosition = localPosition
                ?? throw new ArgumentNullException(nameof(localPosition));
            LocalRotationDegrees = localRotationDegrees;
        }

        public StableId InstanceStableId { get; }

        public StableId RoomStableId { get; }

        public StableId ObjectStableId { get; }

        public RoomContentVisualLayerV1 Layer { get; }

        public RoomVector2V1 LocalPosition { get; }

        public double LocalRotationDegrees { get; }
    }

    public sealed class RoomContentBundleV1
    {
        private readonly ReadOnlyCollection<RoomEnemyPlacementContentV1> enemies;
        private readonly ReadOnlyCollection<RoomPropPlacementContentV1> props;
        private readonly ReadOnlyCollection<RoomVisualPlacementContentV1> visuals;
        private readonly Dictionary<StableId, RoomEnemyPlacementContentV1> enemiesByInstance;

        public RoomContentBundleV1(
            AuthorableRoomGraphDefinitionV1 runtimeDefinition,
            IEnumerable<RoomEnemyPlacementContentV1> enemies,
            IEnumerable<RoomPropPlacementContentV1> props,
            IEnumerable<RoomVisualPlacementContentV1> visuals)
        {
            RuntimeDefinition = runtimeDefinition
                ?? throw new ArgumentNullException(nameof(runtimeDefinition));
            this.enemies = Copy(enemies, nameof(enemies));
            this.props = Copy(props, nameof(props));
            this.visuals = Copy(visuals, nameof(visuals));
            enemiesByInstance = new Dictionary<StableId, RoomEnemyPlacementContentV1>();
            for (int index = 0; index < this.enemies.Count; index++)
            {
                RoomEnemyPlacementContentV1 enemy = this.enemies[index];
                if (enemiesByInstance.ContainsKey(enemy.InstanceStableId))
                {
                    throw new ArgumentException(
                        "room-content-enemy-instance-duplicate:"
                        + enemy.InstanceStableId,
                        nameof(enemies));
                }
                enemiesByInstance.Add(enemy.InstanceStableId, enemy);
            }

            Fingerprint = BuildFingerprint();
        }

        public AuthorableRoomGraphDefinitionV1 RuntimeDefinition { get; }

        public IReadOnlyList<RoomEnemyPlacementContentV1> Enemies
        {
            get { return enemies; }
        }

        public IReadOnlyList<RoomPropPlacementContentV1> Props
        {
            get { return props; }
        }

        public IReadOnlyList<RoomVisualPlacementContentV1> Visuals
        {
            get { return visuals; }
        }

        public string Fingerprint { get; }

        public bool TryGetEnemy(
            StableId instanceStableId,
            out RoomEnemyPlacementContentV1 enemy)
        {
            enemy = null;
            return instanceStableId != null
                && enemiesByInstance.TryGetValue(instanceStableId, out enemy)
                && enemy != null;
        }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder();
            builder.Append(RuntimeDefinition.Fingerprint);
            AppendEnemies(builder);
            AppendProps(builder);
            AppendVisuals(builder);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    hex.Append(hash[index].ToString("x2"));
                }
                return hex.ToString();
            }
        }

        private void AppendEnemies(StringBuilder builder)
        {
            var ordered = new List<RoomEnemyPlacementContentV1>(enemies);
            ordered.Sort((left, right) => left.InstanceStableId.CompareTo(right.InstanceStableId));
            for (int index = 0; index < ordered.Count; index++)
            {
                RoomEnemyPlacementContentV1 value = ordered[index];
                builder.Append("|enemy|")
                    .Append(value.InstanceStableId)
                    .Append('|')
                    .Append(value.RoomStableId)
                    .Append('|')
                    .Append(value.ObjectStableId)
                    .Append('|')
                    .Append(value.Level)
                    .Append('|')
                    .Append(value.LocalPosition.X.ToString("R"))
                    .Append('|')
                    .Append(value.LocalPosition.Y.ToString("R"))
                    .Append('|')
                    .Append(value.LocalRotationDegrees.ToString("R"));
            }
        }

        private void AppendProps(StringBuilder builder)
        {
            var ordered = new List<RoomPropPlacementContentV1>(props);
            ordered.Sort((left, right) => left.InstanceStableId.CompareTo(right.InstanceStableId));
            for (int index = 0; index < ordered.Count; index++)
            {
                RoomPropPlacementContentV1 value = ordered[index];
                builder.Append("|prop|")
                    .Append(value.InstanceStableId)
                    .Append('|')
                    .Append(value.RoomStableId)
                    .Append('|')
                    .Append(value.ObjectStableId)
                    .Append('|')
                    .Append(value.LocalPosition.X.ToString("R"))
                    .Append('|')
                    .Append(value.LocalPosition.Y.ToString("R"))
                    .Append('|')
                    .Append(value.LocalRotationDegrees.ToString("R"));
            }
        }

        private void AppendVisuals(StringBuilder builder)
        {
            var ordered = new List<RoomVisualPlacementContentV1>(visuals);
            ordered.Sort((left, right) => left.InstanceStableId.CompareTo(right.InstanceStableId));
            for (int index = 0; index < ordered.Count; index++)
            {
                RoomVisualPlacementContentV1 value = ordered[index];
                builder.Append("|visual|")
                    .Append(value.InstanceStableId)
                    .Append('|')
                    .Append(value.RoomStableId)
                    .Append('|')
                    .Append(value.ObjectStableId)
                    .Append('|')
                    .Append((int)value.Layer)
                    .Append('|')
                    .Append(value.LocalPosition.X.ToString("R"))
                    .Append('|')
                    .Append(value.LocalPosition.Y.ToString("R"))
                    .Append('|')
                    .Append(value.LocalRotationDegrees.ToString("R"));
            }
        }

        private static ReadOnlyCollection<T> Copy<T>(
            IEnumerable<T> source,
            string parameterName)
            where T : class
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var result = new List<T>(source);
            for (int index = 0; index < result.Count; index++)
            {
                if (result[index] == null)
                {
                    throw new ArgumentException(
                        "Room-content collections cannot contain null values.",
                        parameterName);
                }
            }
            return new ReadOnlyCollection<T>(result);
        }
    }

    public sealed class RoomContentImportIssueV1
    {
        public RoomContentImportIssueV1(string code, string path, string message)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException(nameof(code));
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(nameof(path));
            }
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(nameof(message));
            }

            Code = code;
            Path = path;
            Message = message;
        }

        public string Code { get; }

        public string Path { get; }

        public string Message { get; }
    }

    public sealed class RoomContentImportResultV1
    {
        private readonly ReadOnlyCollection<RoomContentImportIssueV1> issues;

        public RoomContentImportResultV1(
            RoomContentBundleV1 bundle,
            IEnumerable<RoomContentImportIssueV1> issues)
        {
            Bundle = bundle;
            var copy = issues == null
                ? new List<RoomContentImportIssueV1>()
                : new List<RoomContentImportIssueV1>(issues);
            this.issues = new ReadOnlyCollection<RoomContentImportIssueV1>(copy);
        }

        public RoomContentBundleV1 Bundle { get; }

        public IReadOnlyList<RoomContentImportIssueV1> Issues
        {
            get { return issues; }
        }

        public bool IsValid
        {
            get { return Bundle != null && issues.Count == 0; }
        }
    }
}

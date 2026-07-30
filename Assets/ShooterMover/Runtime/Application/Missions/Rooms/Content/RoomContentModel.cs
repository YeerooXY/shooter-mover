using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public enum RoomContentObjectKind
    {
        Enemy = 1,
        Prop = 2,
        Door = 3,
        Tile = 4,
        Background = 5,
        Foreground = 6,
    }

    public enum RoomContentVisualLayer
    {
        Tile = 1,
        Background = 2,
        Foreground = 3,
    }

    public sealed class RoomContentObjectDefinition
    {
        public RoomContentObjectDefinition(
            StableId objectStableId,
            RoomContentObjectKind kind,
            StableId runtimeDefinitionStableId,
            StableId presentationStableId)
        {
            ObjectStableId = objectStableId
                ?? throw new ArgumentNullException(nameof(objectStableId));
            RuntimeDefinitionStableId = runtimeDefinitionStableId
                ?? throw new ArgumentNullException(nameof(runtimeDefinitionStableId));
            PresentationStableId = presentationStableId
                ?? throw new ArgumentNullException(nameof(presentationStableId));
            if (!Enum.IsDefined(typeof(RoomContentObjectKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Kind = kind;
        }

        public StableId ObjectStableId { get; }

        public RoomContentObjectKind Kind { get; }

        public StableId RuntimeDefinitionStableId { get; }

        public StableId PresentationStableId { get; }
    }

    public interface IRoomContentObjectCatalog
    {
        bool TryResolve(
            StableId objectStableId,
            RoomContentObjectKind kind,
            out RoomContentObjectDefinition definition);
    }

    public sealed class RoomContentObjectCatalog : IRoomContentObjectCatalog
    {
        private readonly Dictionary<string, RoomContentObjectDefinition> definitions;

        public RoomContentObjectCatalog(
            IEnumerable<RoomContentObjectDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            this.definitions = new Dictionary<string, RoomContentObjectDefinition>(
                StringComparer.Ordinal);
            foreach (RoomContentObjectDefinition definition in definitions)
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
            RoomContentObjectKind kind,
            out RoomContentObjectDefinition definition)
        {
            definition = null;
            return objectStableId != null
                && Enum.IsDefined(typeof(RoomContentObjectKind), kind)
                && definitions.TryGetValue(Key(objectStableId, kind), out definition)
                && definition != null;
        }

        private static string Key(
            StableId objectStableId,
            RoomContentObjectKind kind)
        {
            return ((int)kind).ToString(CultureInfo.InvariantCulture)
                + "|"
                + objectStableId;
        }
    }

    public sealed class RoomContentJsonPackage
    {
        private readonly ReadOnlyDictionary<string, string> documents;

        public RoomContentJsonPackage(
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

    public sealed class RoomEnemyPlacementContent
    {
        public RoomEnemyPlacementContent(
            StableId instanceStableId,
            StableId roomStableId,
            StableId objectStableId,
            int tier,
            RoomVector2 localPosition,
            double localRotationDegrees,
            string authoredId)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            ObjectStableId = objectStableId
                ?? throw new ArgumentNullException(nameof(objectStableId));
            if (tier < 1 || tier > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(tier));
            }

            Tier = tier;
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

        public int Tier { get; }

        [Obsolete("Enemy placements use Tier. This alias remains during the room-runtime cutover.")]
        public int Level { get { return Tier; } }

        public RoomVector2 LocalPosition { get; }

        public double LocalRotationDegrees { get; }

        public string AuthoredId { get; }
    }

    public sealed class RoomPropPlacementContent
    {
        public RoomPropPlacementContent(
            StableId instanceStableId,
            StableId roomStableId,
            StableId objectStableId,
            RoomVector2 localPosition,
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

        public RoomVector2 LocalPosition { get; }

        public double LocalRotationDegrees { get; }

        public string AuthoredId { get; }
    }

    public sealed class RoomVisualPlacementContent
    {
        public RoomVisualPlacementContent(
            StableId instanceStableId,
            StableId roomStableId,
            StableId objectStableId,
            StableId presentationStableId,
            RoomContentVisualLayer layer,
            RoomVector2 localPosition,
            double localRotationDegrees)
        {
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            ObjectStableId = objectStableId
                ?? throw new ArgumentNullException(nameof(objectStableId));
            PresentationStableId = presentationStableId
                ?? throw new ArgumentNullException(nameof(presentationStableId));
            if (!Enum.IsDefined(typeof(RoomContentVisualLayer), layer))
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

        public StableId PresentationStableId { get; }

        public RoomContentVisualLayer Layer { get; }

        public RoomVector2 LocalPosition { get; }

        public double LocalRotationDegrees { get; }
    }

    public sealed class RoomContentBundle
    {
        private readonly ReadOnlyCollection<RoomEnemyPlacementContent> enemies;
        private readonly ReadOnlyCollection<RoomPropPlacementContent> props;
        private readonly ReadOnlyCollection<RoomVisualPlacementContent> visuals;
        private readonly Dictionary<StableId, RoomEnemyPlacementContent> enemiesByInstance;

        public RoomContentBundle(
            AuthorableRoomGraphDefinition runtimeDefinition,
            IEnumerable<RoomEnemyPlacementContent> enemies,
            IEnumerable<RoomPropPlacementContent> props,
            IEnumerable<RoomVisualPlacementContent> visuals)
        {
            RuntimeDefinition = runtimeDefinition
                ?? throw new ArgumentNullException(nameof(runtimeDefinition));
            this.enemies = Copy(enemies, nameof(enemies));
            this.props = Copy(props, nameof(props));
            this.visuals = Copy(visuals, nameof(visuals));
            enemiesByInstance = new Dictionary<StableId, RoomEnemyPlacementContent>();
            for (int index = 0; index < this.enemies.Count; index++)
            {
                RoomEnemyPlacementContent enemy = this.enemies[index];
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

        public AuthorableRoomGraphDefinition RuntimeDefinition { get; }

        public IReadOnlyList<RoomEnemyPlacementContent> Enemies
        {
            get { return enemies; }
        }

        public IReadOnlyList<RoomPropPlacementContent> Props
        {
            get { return props; }
        }

        public IReadOnlyList<RoomVisualPlacementContent> Visuals
        {
            get { return visuals; }
        }

        public string Fingerprint { get; }

        public bool TryGetEnemy(
            StableId instanceStableId,
            out RoomEnemyPlacementContent enemy)
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
                    hex.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return hex.ToString();
            }
        }

        private void AppendEnemies(StringBuilder builder)
        {
            var ordered = new List<RoomEnemyPlacementContent>(enemies);
            ordered.Sort((left, right) => left.InstanceStableId.CompareTo(right.InstanceStableId));
            for (int index = 0; index < ordered.Count; index++)
            {
                RoomEnemyPlacementContent value = ordered[index];
                builder.Append("|enemy|")
                    .Append(value.InstanceStableId)
                    .Append('|')
                    .Append(value.RoomStableId)
                    .Append('|')
                    .Append(value.ObjectStableId)
                    .Append('|')
                    .Append(value.Tier.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(Number(value.LocalPosition.X))
                    .Append('|')
                    .Append(Number(value.LocalPosition.Y))
                    .Append('|')
                    .Append(Number(value.LocalRotationDegrees));
            }
        }

        private void AppendProps(StringBuilder builder)
        {
            var ordered = new List<RoomPropPlacementContent>(props);
            ordered.Sort((left, right) => left.InstanceStableId.CompareTo(right.InstanceStableId));
            for (int index = 0; index < ordered.Count; index++)
            {
                RoomPropPlacementContent value = ordered[index];
                builder.Append("|prop|")
                    .Append(value.InstanceStableId)
                    .Append('|')
                    .Append(value.RoomStableId)
                    .Append('|')
                    .Append(value.ObjectStableId)
                    .Append('|')
                    .Append(Number(value.LocalPosition.X))
                    .Append('|')
                    .Append(Number(value.LocalPosition.Y))
                    .Append('|')
                    .Append(Number(value.LocalRotationDegrees));
            }
        }

        private void AppendVisuals(StringBuilder builder)
        {
            var ordered = new List<RoomVisualPlacementContent>(visuals);
            ordered.Sort((left, right) => left.InstanceStableId.CompareTo(right.InstanceStableId));
            for (int index = 0; index < ordered.Count; index++)
            {
                RoomVisualPlacementContent value = ordered[index];
                builder.Append("|visual|")
                    .Append(value.InstanceStableId)
                    .Append('|')
                    .Append(value.RoomStableId)
                    .Append('|')
                    .Append(value.ObjectStableId)
                    .Append('|')
                    .Append(value.PresentationStableId)
                    .Append('|')
                    .Append(((int)value.Layer).ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(Number(value.LocalPosition.X))
                    .Append('|')
                    .Append(Number(value.LocalPosition.Y))
                    .Append('|')
                    .Append(Number(value.LocalRotationDegrees));
            }
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
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

    public sealed class RoomContentImportIssue
    {
        public RoomContentImportIssue(string code, string path, string message)
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

    public sealed class RoomContentImportResult
    {
        private readonly ReadOnlyCollection<RoomContentImportIssue> issues;

        public RoomContentImportResult(
            RoomContentBundle bundle,
            IEnumerable<RoomContentImportIssue> issues)
        {
            Bundle = bundle;
            var copy = issues == null
                ? new List<RoomContentImportIssue>()
                : new List<RoomContentImportIssue>(issues);
            this.issues = new ReadOnlyCollection<RoomContentImportIssue>(copy);
        }

        public RoomContentBundle Bundle { get; }

        public IReadOnlyList<RoomContentImportIssue> Issues
        {
            get { return issues; }
        }

        public bool IsValid
        {
            get { return Bundle != null && issues.Count == 0; }
        }
    }
}

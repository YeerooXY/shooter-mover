using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class LevelGridV2Compiler
    {
        [DataContract] private sealed class LevelDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "level_id", IsRequired = true)] public string LevelId { get; set; }
            [DataMember(Name = "start_room_id", IsRequired = true)] public string StartRoomId { get; set; }
            [DataMember(Name = "final_exit", IsRequired = true)] public EndpointDto FinalExit { get; set; }
            [DataMember(Name = "room_ids", IsRequired = true)] public List<string> RoomIds { get; set; }
            [DataMember(Name = "rooms", IsRequired = true)] public List<RoomIndexDto> Rooms { get; set; }
        }

        [DataContract] private sealed class RoomIndexDto
        {
            [DataMember(Name = "room_id", IsRequired = true)] public string RoomId { get; set; }
            [DataMember(Name = "grid_position", IsRequired = true)] public int[] GridPosition { get; set; }
            [DataMember(Name = "slot", IsRequired = true)] public int Slot { get; set; }
            [DataMember(Name = "folder", IsRequired = true)] public string Folder { get; set; }
        }

        [DataContract] private sealed class MapDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "nodes", IsRequired = true)] public List<MapNodeDto> Nodes { get; set; }
            [DataMember(Name = "connections", IsRequired = true)] public List<ConnectionDto> Connections { get; set; }
        }

        [DataContract] private sealed class MapNodeDto
        {
            [DataMember(Name = "room_id", IsRequired = true)] public string RoomId { get; set; }
            [DataMember(Name = "grid_position", IsRequired = true)] public int[] GridPosition { get; set; }
            [DataMember(Name = "slot", IsRequired = true)] public int Slot { get; set; }
        }

        [DataContract] private sealed class ConnectionDto
        {
            [DataMember(Name = "connection_id", IsRequired = true)] public string ConnectionId { get; set; }
            [DataMember(Name = "from", IsRequired = true)] public EndpointDto From { get; set; }
            [DataMember(Name = "to", IsRequired = true)] public EndpointDto To { get; set; }
            [DataMember(Name = "travel_policy", IsRequired = true)] public string TravelPolicy { get; set; }
        }

        [DataContract] private sealed class EndpointDto
        {
            [DataMember(Name = "room_id", IsRequired = true)] public string RoomId { get; set; }
            [DataMember(Name = "door_id", IsRequired = true)] public string DoorId { get; set; }
        }

        [DataContract] private sealed class RoomDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "room_id", IsRequired = true)] public string RoomId { get; set; }
            [DataMember(Name = "display_name", EmitDefaultValue = false)] public string DisplayName { get; set; }
            [DataMember(Name = "grid_position", IsRequired = true)] public int[] GridPosition { get; set; }
            [DataMember(Name = "slot", IsRequired = true)] public int Slot { get; set; }
            [DataMember(Name = "runtime_bounds", IsRequired = true)] public RuntimeBoundsDto RuntimeBounds { get; set; }
            [DataMember(Name = "player_start", EmitDefaultValue = false)] public PlayerStartDto PlayerStart { get; set; }
        }

        [DataContract] private sealed class RuntimeBoundsDto
        {
            [DataMember(Name = "center", IsRequired = true)] public double[] Center { get; set; }
            [DataMember(Name = "size", IsRequired = true)] public double[] Size { get; set; }
        }

        [DataContract] private sealed class PlayerStartDto
        {
            [DataMember(Name = "position", IsRequired = true)] public double[] Position { get; set; }
            [DataMember(Name = "rotation", IsRequired = true)] public double Rotation { get; set; }
        }

        [DataContract] private sealed class DoorsDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "room_id", IsRequired = true)] public string RoomId { get; set; }
            [DataMember(Name = "doors", IsRequired = true)] public List<DoorDto> Doors { get; set; }
        }

        [DataContract] private sealed class DoorDto
        {
            [DataMember(Name = "door_id", IsRequired = true)] public string DoorId { get; set; }
            [DataMember(Name = "side", IsRequired = true)] public string Side { get; set; }
            [DataMember(Name = "current_local_position", IsRequired = true)] public double[] CurrentLocalPosition { get; set; }
            [DataMember(Name = "traversable", IsRequired = true)] public bool Traversable { get; set; }
            [DataMember(Name = "runtime_object", EmitDefaultValue = false)] public string RuntimeObject { get; set; }
        }

        [DataContract] private sealed class FloorDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "tiles", IsRequired = true)] public List<TileDto> Tiles { get; set; }
        }

        [DataContract] private sealed class EnemiesDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "enemies", IsRequired = true)] public List<EnemyDto> Enemies { get; set; }
        }

        [DataContract] private sealed class PropsDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "props", IsRequired = true)] public List<PropDto> Props { get; set; }
        }

        [DataContract] private sealed class DecorDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "background", IsRequired = true)] public List<VisualDto> Background { get; set; }
            [DataMember(Name = "foreground", IsRequired = true)] public List<VisualDto> Foreground { get; set; }
        }

        [DataContract] private sealed class EncounterDto
        {
            [DataMember(Name = "schema_version", IsRequired = true)] public int SchemaVersion { get; set; }
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "completion", IsRequired = true)] public string Completion { get; set; }
            [DataMember(Name = "optional_enemy_ids", IsRequired = true)] public List<string> OptionalEnemyIds { get; set; }
            [DataMember(Name = "door_rules", IsRequired = true)] public List<DoorRuleDto> DoorRules { get; set; }
        }

        [DataContract] private sealed class EnemyDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "object", IsRequired = true)] public string Object { get; set; }
            [DataMember(Name = "level", IsRequired = true)] public int Level { get; set; }
            [DataMember(Name = "position", IsRequired = true)] public double[] Position { get; set; }
            [DataMember(Name = "rotation", IsRequired = true)] public double Rotation { get; set; }
        }

        [DataContract] private sealed class PropDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "object", IsRequired = true)] public string Object { get; set; }
            [DataMember(Name = "position", IsRequired = true)] public double[] Position { get; set; }
            [DataMember(Name = "rotation", IsRequired = true)] public double Rotation { get; set; }
        }

        [DataContract] private sealed class TileDto
        {
            [DataMember(Name = "object", IsRequired = true)] public string Object { get; set; }
            [DataMember(Name = "fill", IsRequired = true)] public FillDto Fill { get; set; }
        }

        [DataContract] private sealed class FillDto
        {
            [DataMember(Name = "from", IsRequired = true)] public int[] From { get; set; }
            [DataMember(Name = "to", IsRequired = true)] public int[] To { get; set; }
        }

        [DataContract] private sealed class VisualDto
        {
            [DataMember(Name = "object", IsRequired = true)] public string Object { get; set; }
            [DataMember(Name = "position", IsRequired = true)] public double[] Position { get; set; }
            [DataMember(Name = "rotation", IsRequired = true)] public double Rotation { get; set; }
        }

        [DataContract] private sealed class DoorRuleDto
        {
            [DataMember(Name = "match", IsRequired = true)] public DoorMatchDto Match { get; set; }
            [DataMember(Name = "open_when", IsRequired = true)] public string OpenWhen { get; set; }
        }

        [DataContract] private sealed class DoorMatchDto
        {
            [DataMember(Name = "door_id", EmitDefaultValue = false)] public string DoorId { get; set; }
            [DataMember(Name = "exit_type", EmitDefaultValue = false)] public string ExitType { get; set; }
            [DataMember(Name = "link_kind", EmitDefaultValue = false)] public string LinkKind { get; set; }
        }

        [DataContract] private sealed class V1ManifestDto
        {
            [DataMember(Name = "version", IsRequired = true)] public int Version { get; set; }
            [DataMember(Name = "layout", IsRequired = true)] public string Layout { get; set; }
            [DataMember(Name = "start_room", IsRequired = true)] public string StartRoom { get; set; }
            [DataMember(Name = "terminal_room", IsRequired = true)] public string TerminalRoom { get; set; }
            [DataMember(Name = "rooms", IsRequired = true)] public List<V1RoomDocumentsDto> Rooms { get; set; }
        }

        [DataContract] private sealed class V1RoomDocumentsDto
        {
            [DataMember(Name = "layout", IsRequired = true)] public string Layout { get; set; }
            [DataMember(Name = "enemies", IsRequired = true)] public string Enemies { get; set; }
            [DataMember(Name = "props", IsRequired = true)] public string Props { get; set; }
            [DataMember(Name = "decor", IsRequired = true)] public string Decor { get; set; }
            [DataMember(Name = "encounter", IsRequired = true)] public string Encounter { get; set; }
        }

        [DataContract] private sealed class V1RoomLayoutDto
        {
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "order", IsRequired = true)] public int Order { get; set; }
            [DataMember(Name = "display_name", IsRequired = true)] public string DisplayName { get; set; }
            [DataMember(Name = "bounds", IsRequired = true)] public RuntimeBoundsDto Bounds { get; set; }
            [DataMember(Name = "spawns", IsRequired = true)] public List<V1SpawnDto> Spawns { get; set; }
            [DataMember(Name = "doors", IsRequired = true)] public List<V1DoorDto> Doors { get; set; }
        }

        [DataContract] private sealed class V1SpawnDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "kind", IsRequired = true)] public string Kind { get; set; }
            [DataMember(Name = "position", IsRequired = true)] public double[] Position { get; set; }
            [DataMember(Name = "rotation", IsRequired = true)] public double Rotation { get; set; }
        }

        [DataContract] private sealed class V1DoorDto
        {
            [DataMember(Name = "id", IsRequired = true)] public string Id { get; set; }
            [DataMember(Name = "object", IsRequired = true)] public string Object { get; set; }
            [DataMember(Name = "position", IsRequired = true)] public double[] Position { get; set; }
            [DataMember(Name = "rotation", IsRequired = true)] public double Rotation { get; set; }
            [DataMember(Name = "link", IsRequired = true)] public V1DoorLinkDto Link { get; set; }
        }

        [DataContract] private sealed class V1DoorLinkDto
        {
            [DataMember(Name = "kind", IsRequired = true)] public string Kind { get; set; }
            [DataMember(Name = "exit_type", IsRequired = true)] public string ExitType { get; set; }
            [DataMember(Name = "target_room", EmitDefaultValue = false)] public string TargetRoom { get; set; }
            [DataMember(Name = "target_spawn", EmitDefaultValue = false)] public string TargetSpawn { get; set; }
        }

        [DataContract] private sealed class V1EnemiesDto
        {
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "enemies", IsRequired = true)] public List<EnemyDto> Enemies { get; set; }
        }

        [DataContract] private sealed class V1PropsDto
        {
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "props", IsRequired = true)] public List<PropDto> Props { get; set; }
        }

        [DataContract] private sealed class V1DecorDto
        {
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "tiles", IsRequired = true)] public List<TileDto> Tiles { get; set; }
            [DataMember(Name = "background", IsRequired = true)] public List<VisualDto> Background { get; set; }
            [DataMember(Name = "foreground", IsRequired = true)] public List<VisualDto> Foreground { get; set; }
        }

        [DataContract] private sealed class V1EncounterDto
        {
            [DataMember(Name = "room", IsRequired = true)] public string Room { get; set; }
            [DataMember(Name = "completion", IsRequired = true)] public string Completion { get; set; }
            [DataMember(Name = "optional_enemy_ids", IsRequired = true)] public List<string> OptionalEnemyIds { get; set; }
            [DataMember(Name = "door_rules", IsRequired = true)] public List<DoorRuleDto> DoorRules { get; set; }
        }
    }
}

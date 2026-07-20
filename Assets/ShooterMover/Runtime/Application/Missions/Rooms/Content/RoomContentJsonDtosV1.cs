using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ShooterMover.Application.Missions.Rooms.Content
{
    public static partial class RoomContentJsonImporterV1
    {
        [DataContract]
        private sealed class ManifestDto
        {
            [DataMember(Name = "version", IsRequired = true)]
            public int Version { get; set; }

            [DataMember(Name = "layout", IsRequired = true)]
            public string Layout { get; set; }

            [DataMember(Name = "start_room", IsRequired = true)]
            public string StartRoom { get; set; }

            [DataMember(Name = "terminal_room", IsRequired = true)]
            public string TerminalRoom { get; set; }

            [DataMember(Name = "rooms", IsRequired = true)]
            public List<RoomDocumentsDto> Rooms { get; set; }
        }

        [DataContract]
        private sealed class RoomDocumentsDto
        {
            [DataMember(Name = "layout", IsRequired = true)]
            public string Layout { get; set; }

            [DataMember(Name = "enemies", IsRequired = true)]
            public string Enemies { get; set; }

            [DataMember(Name = "props", IsRequired = true)]
            public string Props { get; set; }

            [DataMember(Name = "decor", IsRequired = true)]
            public string Decor { get; set; }

            [DataMember(Name = "encounter", IsRequired = true)]
            public string Encounter { get; set; }
        }

        [DataContract]
        private sealed class RoomLayoutDto
        {
            [DataMember(Name = "room", IsRequired = true)]
            public string Room { get; set; }

            [DataMember(Name = "order", IsRequired = true)]
            public int Order { get; set; }

            [DataMember(Name = "display_name", IsRequired = true)]
            public string DisplayName { get; set; }

            [DataMember(Name = "bounds", IsRequired = true)]
            public BoundsDto Bounds { get; set; }

            [DataMember(Name = "spawns", IsRequired = true)]
            public List<SpawnDto> Spawns { get; set; }

            [DataMember(Name = "doors", IsRequired = true)]
            public List<DoorDto> Doors { get; set; }
        }

        [DataContract]
        private sealed class BoundsDto
        {
            [DataMember(Name = "center", IsRequired = true)]
            public double[] Center { get; set; }

            [DataMember(Name = "size", IsRequired = true)]
            public double[] Size { get; set; }
        }

        [DataContract]
        private sealed class SpawnDto
        {
            [DataMember(Name = "id", EmitDefaultValue = false)]
            public string Id { get; set; }

            [DataMember(Name = "kind", IsRequired = true)]
            public string Kind { get; set; }

            [DataMember(Name = "position", IsRequired = true)]
            public double[] Position { get; set; }

            [DataMember(Name = "rotation", IsRequired = true)]
            public double Rotation { get; set; }
        }

        [DataContract]
        private sealed class DoorDto
        {
            [DataMember(Name = "id", EmitDefaultValue = false)]
            public string Id { get; set; }

            [DataMember(Name = "object", IsRequired = true)]
            public string Object { get; set; }

            [DataMember(Name = "position", IsRequired = true)]
            public double[] Position { get; set; }

            [DataMember(Name = "rotation", IsRequired = true)]
            public double Rotation { get; set; }

            [DataMember(Name = "link", IsRequired = true)]
            public DoorLinkDto Link { get; set; }
        }

        [DataContract]
        private sealed class DoorLinkDto
        {
            [DataMember(Name = "kind", IsRequired = true)]
            public string Kind { get; set; }

            [DataMember(Name = "exit_type", IsRequired = true)]
            public string ExitType { get; set; }

            [DataMember(Name = "target_room", EmitDefaultValue = false)]
            public string TargetRoom { get; set; }

            [DataMember(Name = "target_spawn", EmitDefaultValue = false)]
            public string TargetSpawn { get; set; }

            [DataMember(Name = "target_spawn_kind", EmitDefaultValue = false)]
            public string TargetSpawnKind { get; set; }
        }

        [DataContract]
        private sealed class EnemiesDto
        {
            [DataMember(Name = "room", IsRequired = true)]
            public string Room { get; set; }

            [DataMember(Name = "enemies", IsRequired = true)]
            public List<EnemyDto> Enemies { get; set; }
        }

        [DataContract]
        private sealed class EnemyDto
        {
            [DataMember(Name = "id", EmitDefaultValue = false)]
            public string Id { get; set; }

            [DataMember(Name = "object", IsRequired = true)]
            public string Object { get; set; }

            [DataMember(Name = "level", IsRequired = true)]
            public int Level { get; set; }

            [DataMember(Name = "position", IsRequired = true)]
            public double[] Position { get; set; }

            [DataMember(Name = "rotation", IsRequired = true)]
            public double Rotation { get; set; }
        }

        [DataContract]
        private sealed class PropsDto
        {
            [DataMember(Name = "room", IsRequired = true)]
            public string Room { get; set; }

            [DataMember(Name = "props", IsRequired = true)]
            public List<PropDto> Props { get; set; }
        }

        [DataContract]
        private sealed class PropDto
        {
            [DataMember(Name = "id", EmitDefaultValue = false)]
            public string Id { get; set; }

            [DataMember(Name = "object", IsRequired = true)]
            public string Object { get; set; }

            [DataMember(Name = "position", IsRequired = true)]
            public double[] Position { get; set; }

            [DataMember(Name = "rotation", IsRequired = true)]
            public double Rotation { get; set; }
        }

        [DataContract]
        private sealed class DecorDto
        {
            [DataMember(Name = "room", IsRequired = true)]
            public string Room { get; set; }

            [DataMember(Name = "tiles", IsRequired = true)]
            public List<TileDto> Tiles { get; set; }

            [DataMember(Name = "background", IsRequired = true)]
            public List<VisualDto> Background { get; set; }

            [DataMember(Name = "foreground", IsRequired = true)]
            public List<VisualDto> Foreground { get; set; }
        }

        [DataContract]
        private sealed class TileDto
        {
            [DataMember(Name = "object", IsRequired = true)]
            public string Object { get; set; }

            [DataMember(Name = "fill", IsRequired = true)]
            public FillDto Fill { get; set; }
        }

        [DataContract]
        private sealed class FillDto
        {
            [DataMember(Name = "from", IsRequired = true)]
            public int[] From { get; set; }

            [DataMember(Name = "to", IsRequired = true)]
            public int[] To { get; set; }
        }

        [DataContract]
        private sealed class VisualDto
        {
            [DataMember(Name = "object", IsRequired = true)]
            public string Object { get; set; }

            [DataMember(Name = "position", IsRequired = true)]
            public double[] Position { get; set; }

            [DataMember(Name = "rotation", IsRequired = true)]
            public double Rotation { get; set; }
        }

        [DataContract]
        private sealed class EncounterDto
        {
            [DataMember(Name = "room", IsRequired = true)]
            public string Room { get; set; }

            [DataMember(Name = "completion", IsRequired = true)]
            public string Completion { get; set; }

            [DataMember(Name = "optional_enemy_ids", IsRequired = true)]
            public List<string> OptionalEnemyIds { get; set; }

            [DataMember(Name = "door_rules", IsRequired = true)]
            public List<DoorRuleDto> DoorRules { get; set; }
        }

        [DataContract]
        private sealed class DoorRuleDto
        {
            [DataMember(Name = "match", IsRequired = true)]
            public DoorMatchDto Match { get; set; }

            [DataMember(Name = "open_when", IsRequired = true)]
            public string OpenWhen { get; set; }
        }

        [DataContract]
        private sealed class DoorMatchDto
        {
            [DataMember(Name = "door_id", EmitDefaultValue = false)]
            public string DoorId { get; set; }

            [DataMember(Name = "exit_type", EmitDefaultValue = false)]
            public string ExitType { get; set; }

            [DataMember(Name = "link_kind", EmitDefaultValue = false)]
            public string LinkKind { get; set; }
        }
    }
}

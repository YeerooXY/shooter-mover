using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ShooterMover.Application.Enemies.Catalog
{
    public static partial class EnemyCatalogJsonImporter
    {
        private sealed class EnemyCatalogMappingException : Exception
        {
            public EnemyCatalogMappingException(string code, string path, string message)
                : base(message)
            {
                Code = code;
                Path = path;
            }

            public string Code { get; }
            public string Path { get; }
        }

        [DataContract]
        private sealed class CatalogDto
        {
            [DataMember(Name = "schema_version", IsRequired = true, Order = 0)]
            public int SchemaVersion;

            [DataMember(Name = "content_version", IsRequired = true, Order = 1)]
            public string ContentVersion;

            [DataMember(Name = "definitions", IsRequired = true, Order = 2)]
            public List<DefinitionDto> Definitions;
        }

        [DataContract]
        private sealed class DefinitionDto
        {
            [DataMember(Name = "id", IsRequired = true, Order = 0)]
            public string Id;

            [DataMember(Name = "presentation", IsRequired = true, Order = 1)]
            public string Presentation;

            [DataMember(Name = "base_health", IsRequired = true, Order = 2)]
            public double BaseHealth;

            [DataMember(Name = "level_scaling", IsRequired = true, Order = 3)]
            public LevelScalingDto LevelScaling;

            [DataMember(Name = "faction", IsRequired = true, Order = 4)]
            public string Faction;

            [DataMember(Name = "perception", IsRequired = true, Order = 5)]
            public PerceptionDto Perception;

            [DataMember(Name = "movement_policy", IsRequired = true, Order = 6)]
            public string MovementPolicy;

            [DataMember(Name = "decision_policy", IsRequired = true, Order = 7)]
            public string DecisionPolicy;

            [DataMember(Name = "attacks", IsRequired = true, Order = 8)]
            public List<AttackDto> Attacks;

            [DataMember(Name = "xp_profile", IsRequired = true, Order = 9)]
            public string ExperienceProfile;

            [DataMember(Name = "drop_profile", IsRequired = true, Order = 10)]
            public string DropProfile;

            [DataMember(Name = "room_clear_role", IsRequired = true, Order = 11)]
            public string RoomClearRole;

            [DataMember(Name = "special_capabilities", EmitDefaultValue = false, Order = 12)]
            public List<string> SpecialCapabilities;
        }

        [DataContract]
        private sealed class LevelScalingDto
        {
            [DataMember(Name = "base_level", IsRequired = true, Order = 0)]
            public int BaseLevel;

            [DataMember(Name = "maximum_level", IsRequired = true, Order = 1)]
            public int MaximumLevel;

            [DataMember(Name = "additive_health_per_level", IsRequired = true, Order = 2)]
            public double AdditiveHealthPerLevel;

            [DataMember(Name = "multiplicative_health_per_level", IsRequired = true, Order = 3)]
            public double MultiplicativeHealthPerLevel;
        }

        [DataContract]
        private sealed class PerceptionDto
        {
            [DataMember(Name = "detection_radius", IsRequired = true, Order = 0)]
            public double DetectionRadius;

            [DataMember(Name = "vision_arc_degrees", IsRequired = true, Order = 1)]
            public double VisionArcDegrees;
        }

        [DataContract]
        private sealed class AttackDto
        {
            [DataMember(Name = "id", IsRequired = true, Order = 0)]
            public string Id;

            [DataMember(Name = "capability", IsRequired = true, Order = 1)]
            public string Capability;

            [DataMember(Name = "selection_priority", IsRequired = true, Order = 2)]
            public int SelectionPriority;

            [DataMember(Name = "attack_arc_degrees", IsRequired = true, Order = 3)]
            public double AttackArcDegrees;

            [DataMember(Name = "minimum_range", IsRequired = true, Order = 4)]
            public double MinimumRange;

            [DataMember(Name = "preferred_range", IsRequired = true, Order = 5)]
            public double PreferredRange;

            [DataMember(Name = "maximum_range", IsRequired = true, Order = 6)]
            public double MaximumRange;

            [DataMember(Name = "damage", IsRequired = true, Order = 7)]
            public double Damage;

            [DataMember(Name = "damage_channel", IsRequired = true, Order = 8)]
            public string DamageChannel;

            [DataMember(Name = "shooting_pattern", EmitDefaultValue = false, Order = 9)]
            public ShootingPatternDto ShootingPattern;

            [DataMember(Name = "projectile_payload", EmitDefaultValue = false, Order = 10)]
            public ProjectilePayloadDto ProjectilePayload;

            [DataMember(Name = "melee_pattern", EmitDefaultValue = false, Order = 11)]
            public MeleePatternDto MeleePattern;

            // Schema-v1 migration fields. Schema-v2 content must not author these.
            [DataMember(Name = "cooldown_seconds", EmitDefaultValue = false, Order = 20)]
            public double? CooldownSeconds;

            [DataMember(Name = "projectile", EmitDefaultValue = false, Order = 21)]
            public ProjectileDto Projectile;

            [DataMember(Name = "area", EmitDefaultValue = false, Order = 22)]
            public AreaDto Area;

            [DataMember(Name = "melee", EmitDefaultValue = false, Order = 23)]
            public MeleeDto Melee;
        }

        [DataContract]
        private sealed class ShootingPatternDto
        {
            [DataMember(Name = "shots_per_sequence", IsRequired = true, Order = 0)]
            public int ShotsPerSequence;

            [DataMember(Name = "interval_between_shots_seconds", IsRequired = true, Order = 1)]
            public double IntervalBetweenShotsSeconds;

            [DataMember(Name = "projectiles_per_shot", IsRequired = true, Order = 2)]
            public int ProjectilesPerShot;

            [DataMember(Name = "per_shot_spread_degrees", IsRequired = true, Order = 3)]
            public double PerShotSpreadDegrees;

            [DataMember(Name = "sequence_aim_policy", IsRequired = true, Order = 4)]
            public string SequenceAimPolicy;

            [DataMember(Name = "wind_up_seconds", IsRequired = true, Order = 5)]
            public double WindUpSeconds;

            [DataMember(Name = "post_sequence_recovery_seconds", IsRequired = true, Order = 6)]
            public double PostSequenceRecoverySeconds;

            [DataMember(Name = "interruption_policy", IsRequired = true, Order = 7)]
            public string InterruptionPolicy;
        }

        [DataContract]
        private sealed class ProjectilePayloadDto
        {
            [DataMember(Name = "profile", IsRequired = true, Order = 0)]
            public string Profile;

            [DataMember(Name = "speed", IsRequired = true, Order = 1)]
            public double Speed;

            [DataMember(Name = "maximum_travel_distance", IsRequired = true, Order = 2)]
            public double MaximumTravelDistance;

            [DataMember(Name = "collision_radius", IsRequired = true, Order = 3)]
            public double CollisionRadius;

            [DataMember(Name = "pierce", IsRequired = true, Order = 4)]
            public int Pierce;

            [DataMember(Name = "area_payload", EmitDefaultValue = false, Order = 5)]
            public AreaDto AreaPayload;
        }

        [DataContract]
        private sealed class MeleePatternDto
        {
            [DataMember(Name = "wind_up_seconds", IsRequired = true, Order = 0)]
            public double WindUpSeconds;

            [DataMember(Name = "active_window_seconds", IsRequired = true, Order = 1)]
            public double ActiveWindowSeconds;

            [DataMember(Name = "strike_count", IsRequired = true, Order = 2)]
            public int StrikeCount;

            [DataMember(Name = "interval_between_strikes_seconds", IsRequired = true, Order = 3)]
            public double IntervalBetweenStrikesSeconds;

            [DataMember(Name = "contact_radius", IsRequired = true, Order = 4)]
            public double ContactRadius;

            [DataMember(Name = "lunge_distance", IsRequired = true, Order = 5)]
            public double LungeDistance;

            [DataMember(Name = "aim_commit_policy", IsRequired = true, Order = 6)]
            public string AimCommitPolicy;

            [DataMember(Name = "recovery_seconds", IsRequired = true, Order = 7)]
            public double RecoverySeconds;

            [DataMember(Name = "hits_per_target", IsRequired = true, Order = 8)]
            public int HitsPerTarget;

            [DataMember(Name = "terminal_on_impact_policy", IsRequired = true, Order = 9)]
            public string TerminalOnImpactPolicy;

            [DataMember(Name = "interruption_policy", IsRequired = true, Order = 10)]
            public string InterruptionPolicy;
        }

        [DataContract]
        private sealed class ProjectileDto
        {
            [DataMember(Name = "profile", IsRequired = true, Order = 0)]
            public string Profile;

            [DataMember(Name = "count", IsRequired = true, Order = 1)]
            public int Count;

            [DataMember(Name = "speed", IsRequired = true, Order = 2)]
            public double Speed;

            [DataMember(Name = "maximum_travel_distance", IsRequired = true, Order = 3)]
            public double MaximumTravelDistance;

            [DataMember(Name = "collision_radius", IsRequired = true, Order = 4)]
            public double CollisionRadius;

            [DataMember(Name = "spread_degrees", IsRequired = true, Order = 5)]
            public double SpreadDegrees;

            [DataMember(Name = "pierce", IsRequired = true, Order = 6)]
            public int Pierce;
        }

        [DataContract]
        private sealed class AreaDto
        {
            [DataMember(Name = "radius", IsRequired = true, Order = 0)]
            public double Radius;

            [DataMember(Name = "duration_seconds", IsRequired = true, Order = 1)]
            public double DurationSeconds;

            [DataMember(Name = "maximum_targets", IsRequired = true, Order = 2)]
            public int MaximumTargets;
        }

        [DataContract]
        private sealed class MeleeDto
        {
            [DataMember(Name = "contact_radius", IsRequired = true, Order = 0)]
            public double ContactRadius;

            [DataMember(Name = "pounce_distance", IsRequired = true, Order = 1)]
            public double PounceDistance;

            [DataMember(Name = "wind_up_seconds", IsRequired = true, Order = 2)]
            public double WindUpSeconds;

            [DataMember(Name = "commitment_seconds", IsRequired = true, Order = 3)]
            public double CommitmentSeconds;
        }
    }
}

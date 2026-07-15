using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Mission;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Encounters.Stage1ShortRoute
{
    public enum Stage1ShortRouteRoomKind
    {
        OrdinaryEncounter = 1,
        EliteEndpoint = 2,
        ProjectionOnly = 3,
    }

    public enum Stage1ShortRouteHazardWarningGlyph
    {
        ChevronSweep = 1,
        DoubleBarGate = 2,
    }

    public sealed class Stage1ShortRouteParticipantDefinition
    {
        public Stage1ShortRouteParticipantDefinition(
            StableId roleId,
            StableId spawnAnchorId,
            int order)
        {
            RoleId = roleId ?? throw new ArgumentNullException(nameof(roleId));
            SpawnAnchorId = spawnAnchorId ?? throw new ArgumentNullException(nameof(spawnAnchorId));
            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order));
            }

            Order = order;
        }

        public StableId RoleId { get; }

        public StableId SpawnAnchorId { get; }

        public int Order { get; }

        public string ToCanonicalString()
        {
            return "order=" + Order.ToString(CultureInfo.InvariantCulture)
                + "\nrole_id=" + RoleId
                + "\nspawn_anchor_id=" + SpawnAnchorId;
        }
    }

    public sealed class Stage1ShortRouteHazardDefinition
    {
        public Stage1ShortRouteHazardDefinition(
            StableId hazardId,
            Stage1ShortRouteHazardWarningGlyph warningGlyph,
            string warningText,
            StableId footprintId,
            int telegraphTicks,
            int activeTicks,
            int cooldownTicks,
            int maximumHitsPerActivation,
            double damagePerHit)
        {
            HazardId = hazardId ?? throw new ArgumentNullException(nameof(hazardId));
            FootprintId = footprintId ?? throw new ArgumentNullException(nameof(footprintId));
            if (!Enum.IsDefined(typeof(Stage1ShortRouteHazardWarningGlyph), warningGlyph))
            {
                throw new ArgumentOutOfRangeException(nameof(warningGlyph));
            }

            if (string.IsNullOrWhiteSpace(warningText))
            {
                throw new ArgumentException("A geometry hazard requires a readable text warning.", nameof(warningText));
            }

            if (telegraphTicks <= 0 || activeTicks <= 0 || cooldownTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(telegraphTicks),
                    "Hazard timing windows must be positive and bounded.");
            }

            if (maximumHitsPerActivation != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHitsPerActivation),
                    "Stage 1 route hazards allow exactly one hit per activation.");
            }

            if (double.IsNaN(damagePerHit)
                || double.IsInfinity(damagePerHit)
                || damagePerHit <= 0d
                || damagePerHit > 8d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damagePerHit),
                    "Stage 1 route hazard damage must be finite, positive, and no greater than eight.");
            }

            WarningGlyph = warningGlyph;
            WarningText = warningText.Trim();
            TelegraphTicks = telegraphTicks;
            ActiveTicks = activeTicks;
            CooldownTicks = cooldownTicks;
            MaximumHitsPerActivation = maximumHitsPerActivation;
            DamagePerHit = damagePerHit;
        }

        public StableId HazardId { get; }

        public Stage1ShortRouteHazardWarningGlyph WarningGlyph { get; }

        public string WarningText { get; }

        public StableId FootprintId { get; }

        public int TelegraphTicks { get; }

        public int ActiveTicks { get; }

        public int CooldownTicks { get; }

        public int MaximumHitsPerActivation { get; }

        public double DamagePerHit { get; }

        public string ToCanonicalString()
        {
            return "hazard_id=" + HazardId
                + "\nwarning_glyph=" + WarningGlyph
                + "\nwarning_text=" + WarningText
                + "\nfootprint_id=" + FootprintId
                + "\ntelegraph_ticks=" + TelegraphTicks.ToString(CultureInfo.InvariantCulture)
                + "\nactive_ticks=" + ActiveTicks.ToString(CultureInfo.InvariantCulture)
                + "\ncooldown_ticks=" + CooldownTicks.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_hits_per_activation="
                + MaximumHitsPerActivation.ToString(CultureInfo.InvariantCulture)
                + "\ndamage_per_hit=" + DamagePerHit.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed class Stage1ShortRouteRoomDefinition
    {
        private readonly ReadOnlyCollection<Stage1ShortRouteParticipantDefinition> participants;
        private readonly ReadOnlyCollection<Stage1ShortRouteHazardDefinition> hazards;

        public Stage1ShortRouteRoomDefinition(
            string markerId,
            StableId roomId,
            StableId projectionBaseId,
            StableId encounterId,
            int loadOrder,
            Stage1ShortRouteRoomKind kind,
            bool allowsRetreat,
            bool locksOnEntry,
            IEnumerable<Stage1ShortRouteParticipantDefinition> participantDefinitions,
            IEnumerable<Stage1ShortRouteHazardDefinition> hazardDefinitions)
        {
            if (string.IsNullOrWhiteSpace(markerId))
            {
                throw new ArgumentException("A route marker ID is required.", nameof(markerId));
            }

            if (loadOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(loadOrder));
            }

            if (!Enum.IsDefined(typeof(Stage1ShortRouteRoomKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            MarkerId = markerId.Trim();
            RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            ProjectionBaseId = projectionBaseId ?? throw new ArgumentNullException(nameof(projectionBaseId));
            EncounterId = encounterId;
            LoadOrder = loadOrder;
            Kind = kind;
            AllowsRetreat = allowsRetreat;
            LocksOnEntry = locksOnEntry;
            participants = CopyParticipants(participantDefinitions);
            hazards = CopyHazards(hazardDefinitions);
            ValidateShape();
        }

        public string MarkerId { get; }

        public StableId RoomId { get; }

        public StableId ProjectionBaseId { get; }

        public StableId EncounterId { get; }

        public int LoadOrder { get; }

        public Stage1ShortRouteRoomKind Kind { get; }

        public bool AllowsRetreat { get; }

        public bool LocksOnEntry { get; }

        public IReadOnlyList<Stage1ShortRouteParticipantDefinition> Participants
        {
            get { return participants; }
        }

        public IReadOnlyList<Stage1ShortRouteHazardDefinition> Hazards
        {
            get { return hazards; }
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("marker_id=").Append(MarkerId)
                .Append("\nroom_id=").Append(RoomId)
                .Append("\nprojection_base_id=").Append(ProjectionBaseId)
                .Append("\nencounter_id=").Append(EncounterId == null ? "none" : EncounterId.ToString())
                .Append("\nload_order=").Append(LoadOrder.ToString(CultureInfo.InvariantCulture))
                .Append("\nkind=").Append(Kind)
                .Append("\nallows_retreat=").Append(AllowsRetreat ? "true" : "false")
                .Append("\nlocks_on_entry=").Append(LocksOnEntry ? "true" : "false")
                .Append("\nparticipant_count=").Append(participants.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < participants.Count; index++)
            {
                builder.Append("\nparticipant_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(participants[index].ToCanonicalString());
            }

            builder.Append("\nhazard_count=").Append(hazards.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < hazards.Count; index++)
            {
                builder.Append("\nhazard_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(hazards[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        private static ReadOnlyCollection<Stage1ShortRouteParticipantDefinition> CopyParticipants(
            IEnumerable<Stage1ShortRouteParticipantDefinition> source)
        {
            List<Stage1ShortRouteParticipantDefinition> copy =
                new List<Stage1ShortRouteParticipantDefinition>();
            if (source != null)
            {
                foreach (Stage1ShortRouteParticipantDefinition participant in source)
                {
                    if (participant == null)
                    {
                        throw new ArgumentException("Participant definitions cannot contain null.", nameof(source));
                    }

                    copy.Add(participant);
                }
            }

            copy.Sort(
                delegate(Stage1ShortRouteParticipantDefinition left, Stage1ShortRouteParticipantDefinition right)
                {
                    return left.Order.CompareTo(right.Order);
                });

            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index].Order != index)
                {
                    throw new ArgumentException(
                        "Participant order must be contiguous and start at zero.",
                        nameof(source));
                }
            }

            return new ReadOnlyCollection<Stage1ShortRouteParticipantDefinition>(copy);
        }

        private static ReadOnlyCollection<Stage1ShortRouteHazardDefinition> CopyHazards(
            IEnumerable<Stage1ShortRouteHazardDefinition> source)
        {
            List<Stage1ShortRouteHazardDefinition> copy =
                new List<Stage1ShortRouteHazardDefinition>();
            if (source != null)
            {
                foreach (Stage1ShortRouteHazardDefinition hazard in source)
                {
                    if (hazard == null)
                    {
                        throw new ArgumentException("Hazard definitions cannot contain null.", nameof(source));
                    }

                    copy.Add(hazard);
                }
            }

            if (copy.Count > 1)
            {
                throw new ArgumentException(
                    "A Stage 1 short-route room may project at most one hazard.",
                    nameof(source));
            }

            return new ReadOnlyCollection<Stage1ShortRouteHazardDefinition>(copy);
        }

        private void ValidateShape()
        {
            if (Kind == Stage1ShortRouteRoomKind.ProjectionOnly)
            {
                if (EncounterId != null
                    || participants.Count != 0
                    || hazards.Count != 0
                    || AllowsRetreat
                    || LocksOnEntry)
                {
                    throw new ArgumentException(
                        "Projection-only route markers cannot own encounter, hazard, retreat, or lockdown state.");
                }

                return;
            }

            if (EncounterId == null || participants.Count == 0 || participants.Count > 2)
            {
                throw new ArgumentException(
                    "Encounter rooms require one or two bounded participant entries.");
            }

            if (Kind == Stage1ShortRouteRoomKind.EliteEndpoint)
            {
                if (participants.Count != 1 || AllowsRetreat || !LocksOnEntry || hazards.Count != 0)
                {
                    throw new ArgumentException(
                        "The elite endpoint requires one participant, entry lockdown, no retreat, and no route hazard.");
                }
            }
            else if (LocksOnEntry)
            {
                throw new ArgumentException("Ordinary rooms cannot engage route lockdown.");
            }
        }
    }

    public sealed class Stage1ShortRouteRoomProjection
    {
        public Stage1ShortRouteRoomProjection(
            Stage1ShortRouteRoomDefinition definition,
            int generation,
            RoomProjectionIdentity identity,
            RoomProjectionKey key)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (generation < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            Generation = generation;
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            if (!Identity.RoomId.Equals(Definition.RoomId)
                || !Key.RoomId.Equals(Definition.RoomId))
            {
                throw new ArgumentException("Projection identity and key must match the frozen room definition.");
            }
        }

        public Stage1ShortRouteRoomDefinition Definition { get; }

        public int Generation { get; }

        public RoomProjectionIdentity Identity { get; }

        public RoomProjectionKey Key { get; }

        public string ToCanonicalString()
        {
            return "marker_id=" + Definition.MarkerId
                + "\nload_order=" + Definition.LoadOrder.ToString(CultureInfo.InvariantCulture)
                + "\ngeneration=" + Generation.ToString(CultureInfo.InvariantCulture)
                + "\nidentity:\n" + Identity.ToCanonicalString()
                + "\nkey:\n" + Key.ToCanonicalString();
        }
    }

    /// <summary>
    /// Immutable EN-011 route composition. It projects encounter and hazard inputs
    /// into the EH-005 shell marker order; it never loads a scene or stores mission truth.
    /// </summary>
    public sealed class Stage1ShortRouteComposition
    {
        public const string StartMarkerId = "route.start";
        public const string ArenaEntryMarkerId = "route.arena-entry";
        public const string ConnectorMarkerId = "route.connector";
        public const string ReviewEndMarkerId = "route.review-end";
        public const string RestartMarkerId = "route.restart";

        private const string PursuerRoleId = "enemy.pursuer-drone";
        private const string RamRoleId = "enemy.ram-droid";
        private const string MobileBlasterRoleId = "enemy.mobile-blaster-droid";
        private const string TurretRoleId = "enemy.blaster-turret";
        private const string EliteRoleId = "enemy.four-blaster-elite";

        private static readonly Stage1ShortRouteComposition ApprovedValue = CreateApproved();

        private readonly ReadOnlyCollection<Stage1ShortRouteRoomDefinition> rooms;
        private readonly Dictionary<string, Stage1ShortRouteRoomDefinition> roomByMarker;
        private readonly string canonicalText;

        private Stage1ShortRouteComposition(IEnumerable<Stage1ShortRouteRoomDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<Stage1ShortRouteRoomDefinition> ordered =
                new List<Stage1ShortRouteRoomDefinition>();
            foreach (Stage1ShortRouteRoomDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Route definitions cannot contain null.", nameof(definitions));
                }

                ordered.Add(definition);
            }

            ordered.Sort(
                delegate(Stage1ShortRouteRoomDefinition left, Stage1ShortRouteRoomDefinition right)
                {
                    return left.LoadOrder.CompareTo(right.LoadOrder);
                });
            rooms = new ReadOnlyCollection<Stage1ShortRouteRoomDefinition>(ordered);
            roomByMarker = new Dictionary<string, Stage1ShortRouteRoomDefinition>(StringComparer.Ordinal);
            ValidateRoute();
            canonicalText = BuildCanonicalText();
            Fingerprint = ComputeFingerprint(canonicalText);
        }

        public static Stage1ShortRouteComposition Approved
        {
            get { return ApprovedValue; }
        }

        public IReadOnlyList<Stage1ShortRouteRoomDefinition> Rooms
        {
            get { return rooms; }
        }

        public string Fingerprint { get; }

        public Stage1ShortRouteRoomDefinition GetRoom(string markerId)
        {
            if (string.IsNullOrWhiteSpace(markerId))
            {
                throw new ArgumentException("A route marker ID is required.", nameof(markerId));
            }

            Stage1ShortRouteRoomDefinition room;
            if (!roomByMarker.TryGetValue(markerId, out room))
            {
                throw new KeyNotFoundException("Unknown Stage 1 short-route marker: " + markerId);
            }

            return room;
        }

        public Stage1ShortRouteRoomProjection CreateProjection(
            string markerId,
            StableId runId,
            int generation)
        {
            if (runId == null)
            {
                throw new ArgumentNullException(nameof(runId));
            }

            if (generation < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            Stage1ShortRouteRoomDefinition room = GetRoom(markerId);
            StableId projectionId = StableId.Parse(
                room.ProjectionBaseId + "-g" + generation.ToString(CultureInfo.InvariantCulture));
            RoomProjectionIdentity identity = new RoomProjectionIdentity(room.RoomId, projectionId);
            RoomProjectionKey key = new RoomProjectionKey(
                runId,
                room.RoomId,
                MissionSequence.Initial);
            return new Stage1ShortRouteRoomProjection(room, generation, identity, key);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private static Stage1ShortRouteComposition CreateApproved()
        {
            return new Stage1ShortRouteComposition(
                new[]
                {
                    Room(
                        StartMarkerId,
                        "room.stage1-short-route-start",
                        "projection.stage1-short-route-start",
                        "encounter.stage1-short-route-start",
                        0,
                        Stage1ShortRouteRoomKind.OrdinaryEncounter,
                        false,
                        false,
                        new[]
                        {
                            Participant(PursuerRoleId, "spawn.start-left", 0),
                            Participant(PursuerRoleId, "spawn.start-right", 1),
                        },
                        null),
                    Room(
                        ArenaEntryMarkerId,
                        "room.stage1-short-route-arena-entry",
                        "projection.stage1-short-route-arena-entry",
                        "encounter.stage1-short-route-arena-entry",
                        1,
                        Stage1ShortRouteRoomKind.OrdinaryEncounter,
                        true,
                        false,
                        new[]
                        {
                            Participant(RamRoleId, "spawn.arena-ram", 0),
                            Participant(MobileBlasterRoleId, "spawn.arena-mobile-blaster", 1),
                        },
                        new[]
                        {
                            Hazard(
                                "hazard.stage1-short-route-chevron-sweep",
                                Stage1ShortRouteHazardWarningGlyph.ChevronSweep,
                                "CHEVRON SWEEP",
                                "footprint.stage1-short-route-full-width-chevron",
                                45,
                                30,
                                120,
                                4d),
                        }),
                    Room(
                        ConnectorMarkerId,
                        "room.stage1-short-route-connector",
                        "projection.stage1-short-route-connector",
                        "encounter.stage1-short-route-connector",
                        2,
                        Stage1ShortRouteRoomKind.OrdinaryEncounter,
                        true,
                        false,
                        new[]
                        {
                            Participant(PursuerRoleId, "spawn.connector-pursuer", 0),
                            Participant(TurretRoleId, "spawn.connector-turret", 1),
                        },
                        new[]
                        {
                            Hazard(
                                "hazard.stage1-short-route-double-bar-gate",
                                Stage1ShortRouteHazardWarningGlyph.DoubleBarGate,
                                "DOUBLE BAR GATE",
                                "footprint.stage1-short-route-double-bar-lane",
                                60,
                                24,
                                150,
                                6d),
                        }),
                    Room(
                        ReviewEndMarkerId,
                        "room.stage1-short-route-review-end",
                        "projection.stage1-short-route-review-end",
                        "encounter.stage1-short-route-four-blaster-elite",
                        3,
                        Stage1ShortRouteRoomKind.EliteEndpoint,
                        false,
                        true,
                        new[]
                        {
                            Participant(EliteRoleId, "spawn.review-four-blaster-elite", 0),
                        },
                        null),
                    Room(
                        RestartMarkerId,
                        "room.stage1-short-route-restart",
                        "projection.stage1-short-route-restart",
                        null,
                        4,
                        Stage1ShortRouteRoomKind.ProjectionOnly,
                        false,
                        false,
                        null,
                        null),
                });
        }

        private void ValidateRoute()
        {
            string[] expectedMarkers =
            {
                StartMarkerId,
                ArenaEntryMarkerId,
                ConnectorMarkerId,
                ReviewEndMarkerId,
                RestartMarkerId,
            };

            if (rooms.Count != expectedMarkers.Length)
            {
                throw new InvalidOperationException("The frozen Stage 1 short route requires exactly five projections.");
            }

            HashSet<string> roomIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> projectionIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> encounterIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> hazardIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> ordinaryRoles = new HashSet<string>(StringComparer.Ordinal);
            int eliteCount = 0;

            for (int index = 0; index < rooms.Count; index++)
            {
                Stage1ShortRouteRoomDefinition room = rooms[index];
                if (room.LoadOrder != index
                    || !string.Equals(room.MarkerId, expectedMarkers[index], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Route projection order does not match the EH-005 shell.");
                }

                if (!roomByMarker.TryAdd(room.MarkerId, room)
                    || !roomIds.Add(room.RoomId.ToString())
                    || !projectionIds.Add(room.ProjectionBaseId.ToString()))
                {
                    throw new InvalidOperationException("Route projection identities must be unique.");
                }

                if (room.EncounterId != null && !encounterIds.Add(room.EncounterId.ToString()))
                {
                    throw new InvalidOperationException("Encounter IDs must be unique across the short route.");
                }

                for (int participantIndex = 0; participantIndex < room.Participants.Count; participantIndex++)
                {
                    string role = room.Participants[participantIndex].RoleId.ToString();
                    if (room.Kind == Stage1ShortRouteRoomKind.EliteEndpoint)
                    {
                        eliteCount++;
                        if (!string.Equals(role, EliteRoleId, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("The elite endpoint may contain only the Four-Blaster Elite.");
                        }
                    }
                    else
                    {
                        if (!IsOrdinaryRole(role))
                        {
                            throw new InvalidOperationException("Ordinary route rooms may contain only validated ordinary roles.");
                        }

                        ordinaryRoles.Add(role);
                    }
                }

                for (int hazardIndex = 0; hazardIndex < room.Hazards.Count; hazardIndex++)
                {
                    if (!hazardIds.Add(room.Hazards[hazardIndex].HazardId.ToString()))
                    {
                        throw new InvalidOperationException("Route hazard IDs must be unique.");
                    }
                }
            }

            if (eliteCount != 1
                || ordinaryRoles.Count != 4
                || !ordinaryRoles.Contains(PursuerRoleId)
                || !ordinaryRoles.Contains(RamRoleId)
                || !ordinaryRoles.Contains(MobileBlasterRoleId)
                || !ordinaryRoles.Contains(TurretRoleId))
            {
                throw new InvalidOperationException(
                    "The route must cover all four ordinary roles and exactly one Four-Blaster Elite endpoint.");
            }
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shooter-mover.stage1-short-route-composition")
                .Append("\nversion=1")
                .Append("\nroom_count=").Append(rooms.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < rooms.Count; index++)
            {
                builder.Append("\nroom_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(rooms[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        private static Stage1ShortRouteRoomDefinition Room(
            string markerId,
            string roomId,
            string projectionBaseId,
            string encounterId,
            int loadOrder,
            Stage1ShortRouteRoomKind kind,
            bool allowsRetreat,
            bool locksOnEntry,
            IEnumerable<Stage1ShortRouteParticipantDefinition> participants,
            IEnumerable<Stage1ShortRouteHazardDefinition> hazards)
        {
            return new Stage1ShortRouteRoomDefinition(
                markerId,
                StableId.Parse(roomId),
                StableId.Parse(projectionBaseId),
                encounterId == null ? null : StableId.Parse(encounterId),
                loadOrder,
                kind,
                allowsRetreat,
                locksOnEntry,
                participants,
                hazards);
        }

        private static Stage1ShortRouteParticipantDefinition Participant(
            string roleId,
            string spawnAnchorId,
            int order)
        {
            return new Stage1ShortRouteParticipantDefinition(
                StableId.Parse(roleId),
                StableId.Parse(spawnAnchorId),
                order);
        }

        private static Stage1ShortRouteHazardDefinition Hazard(
            string hazardId,
            Stage1ShortRouteHazardWarningGlyph warningGlyph,
            string warningText,
            string footprintId,
            int telegraphTicks,
            int activeTicks,
            int cooldownTicks,
            double damagePerHit)
        {
            return new Stage1ShortRouteHazardDefinition(
                StableId.Parse(hazardId),
                warningGlyph,
                warningText,
                StableId.Parse(footprintId),
                telegraphTicks,
                activeTicks,
                cooldownTicks,
                1,
                damagePerHit);
        }

        private static bool IsOrdinaryRole(string roleId)
        {
            return string.Equals(roleId, PursuerRoleId, StringComparison.Ordinal)
                || string.Equals(roleId, RamRoleId, StringComparison.Ordinal)
                || string.Equals(roleId, MobileBlasterRoleId, StringComparison.Ordinal)
                || string.Equals(roleId, TurretRoleId, StringComparison.Ordinal);
        }

        private static string ComputeFingerprint(string canonicalText)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= 16777619u;
                }

                return "fnv1a32:" + hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }
    }
}

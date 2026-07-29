using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Characters.Stats;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Runs.Session
{
    public sealed class RunLivePortRestartResult
    {
        public RunLivePortRestartResult(
            bool succeeded,
            string rejectionCode,
            long lifecycleGeneration,
            string snapshotFingerprint)
        {
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            LifecycleGeneration = lifecycleGeneration;
            SnapshotFingerprint = snapshotFingerprint ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public long LifecycleGeneration { get; }
        public string SnapshotFingerprint { get; }
    }

    public interface IRunLifecycleLivePort
    {
        string PortId { get; }
        long LifecycleGeneration { get; }
        string SnapshotFingerprint { get; }
        string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick);
        RunLivePortRestartResult Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick);
    }

    public sealed class RunPlayerSnapshot
    {
        public RunPlayerSnapshot(
            StableId actorInstanceStableId,
            StableId participantStableId,
            long lifecycleGeneration,
            double currentHealth,
            double maximumHealth,
            double positionX,
            double positionY,
            long acceptedSequence)
        {
            ActorInstanceStableId = actorInstanceStableId
                ?? throw new ArgumentNullException(nameof(actorInstanceStableId));
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (!IsFinite(currentHealth)
                || !IsFinite(maximumHealth)
                || maximumHealth <= 0d
                || currentHealth < 0d
                || currentHealth > maximumHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHealth));
            }
            if (!IsFinite(positionX) || !IsFinite(positionY))
            {
                throw new ArgumentOutOfRangeException(nameof(positionX));
            }
            if (acceptedSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedSequence));
            }

            LifecycleGeneration = lifecycleGeneration;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            PositionX = positionX;
            PositionY = positionY;
            AcceptedSequence = acceptedSequence;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId ActorInstanceStableId { get; }
        public StableId ParticipantStableId { get; }
        public long LifecycleGeneration { get; }
        public double CurrentHealth { get; }
        public double MaximumHealth { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public long AcceptedSequence { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "actor", ActorInstanceStableId);
            RunSessionFingerprint.Append(builder, "participant", ParticipantStableId);
            RunSessionFingerprint.Append(builder, "generation", LifecycleGeneration);
            RunSessionFingerprint.Append(builder, "health", CurrentHealth);
            RunSessionFingerprint.Append(builder, "maximum-health", MaximumHealth);
            RunSessionFingerprint.Append(builder, "position-x", PositionX);
            RunSessionFingerprint.Append(builder, "position-y", PositionY);
            RunSessionFingerprint.Append(builder, "accepted-sequence", AcceptedSequence);
            return builder.ToString();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public interface IRunPlayerLivePort : IRunLifecycleLivePort
    {
        RunPlayerSnapshot ExportSnapshot();
    }

    public interface IRunGunLivePort : IRunLifecycleLivePort
    {
        IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds { get; }
    }

    public interface IRunStatusEffectLivePort : IRunLifecycleLivePort
    {
        int ActiveEffectCount { get; }
    }

    public interface IRunConditionalFactLivePort :
        IRunLifecycleLivePort
    {
    }

    public interface IRunActiveAbilityLivePort :
        IRunLifecycleLivePort
    {
    }

    public interface IRunRoomLivePort : IRunLifecycleLivePort
    {
        StableId CurrentRoomStableId { get; }
    }

    public interface IRunMissionResultPort
    {
        long Sequence { get; }
        bool TryGetRun(StableId runStableId, out MissionRunPayload runPayload);
        MissionRunStateResult RecordCollectedStrongbox(
            RunStrongboxCollectionRequest request,
            PlayerRouteProfilePayload routePayload);
        MissionRunStateResult EndRun(
            EndRunSessionCommand command,
            PlayerRouteProfilePayload routePayload);
    }

    public sealed class RunSessionLivePorts
    {
        public RunSessionLivePorts(
            IRunPlayerLivePort player,
            IRunGunLivePort guns,
            IRunStatusEffectLivePort statusEffects,
            IRunConditionalFactLivePort conditionalFacts,
            IRunActiveAbilityLivePort activeAbilities,
            IRunRoomLivePort rooms,
            IRunMissionResultPort missionResults)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Guns = guns ?? throw new ArgumentNullException(nameof(guns));
            StatusEffects = statusEffects
                ?? throw new ArgumentNullException(nameof(statusEffects));
            ConditionalFacts = conditionalFacts
                ?? throw new ArgumentNullException(nameof(conditionalFacts));
            ActiveAbilities = activeAbilities
                ?? throw new ArgumentNullException(nameof(activeAbilities));
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            MissionResults = missionResults
                ?? throw new ArgumentNullException(nameof(missionResults));

            long generation = Player.LifecycleGeneration;
            if (Guns.LifecycleGeneration != generation
                || StatusEffects.LifecycleGeneration != generation
                || ConditionalFacts.LifecycleGeneration != generation
                || ActiveAbilities.LifecycleGeneration != generation
                || Rooms.LifecycleGeneration != generation)
            {
                throw new ArgumentException(
                    "All run-local runtime ports must start at one lifecycle generation.");
            }
        }

        public IRunPlayerLivePort Player { get; }
        public IRunGunLivePort Guns { get; }
        public IRunStatusEffectLivePort StatusEffects { get; }
        public IRunConditionalFactLivePort ConditionalFacts { get; }
        public IRunActiveAbilityLivePort ActiveAbilities { get; }
        public IRunRoomLivePort Rooms { get; }
        public IRunMissionResultPort MissionResults { get; }

        public IReadOnlyList<IRunLifecycleLivePort> LifecyclePorts
        {
            get
            {
                return new ReadOnlyCollection<IRunLifecycleLivePort>(
                    new List<IRunLifecycleLivePort>
                    {
                        Player,
                        Guns,
                        StatusEffects,
                        ConditionalFacts,
                        ActiveAbilities,
                        Rooms,
                    });
            }
        }
    }

    public sealed class FrozenRunEquipment :
        IComparable<FrozenRunEquipment>
    {
        public FrozenRunEquipment(
            StableId slotStableId,
            EquipmentInstance equipmentInstance,
            EquipmentDefinition equipmentDefinition)
        {
            SlotStableId = slotStableId
                ?? throw new ArgumentNullException(nameof(slotStableId));
            EquipmentInstance = equipmentInstance
                ?? throw new ArgumentNullException(nameof(equipmentInstance));
            EquipmentDefinition = equipmentDefinition
                ?? throw new ArgumentNullException(nameof(equipmentDefinition));
            if (EquipmentInstance.DefinitionId
                != EquipmentDefinition.DefinitionId)
            {
                throw new ArgumentException(
                    "Frozen equipment instance and definition identities must match.");
            }
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId SlotStableId { get; }
        public EquipmentInstance EquipmentInstance { get; }
        public EquipmentDefinition EquipmentDefinition { get; }
        public StableId EquipmentInstanceStableId
        {
            get { return EquipmentInstance.InstanceId; }
        }
        public StableId EquipmentDefinitionStableId
        {
            get { return EquipmentDefinition.DefinitionId; }
        }
        public StableId RuntimeGunReferenceStableId
        {
            get { return EquipmentDefinition.RuntimeGunReferenceId; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "slot", SlotStableId);
            RunSessionFingerprint.Append(
                builder,
                "equipment-instance",
                EquipmentInstance.ToCanonicalString());
            RunSessionFingerprint.Append(
                builder,
                "equipment-instance-fingerprint",
                EquipmentInstance.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "equipment-definition",
                EquipmentDefinition.ToCanonicalString());
            return builder.ToString();
        }

        public int CompareTo(FrozenRunEquipment other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }
            return SlotStableId.CompareTo(other.SlotStableId);
        }
    }

    public sealed class FrozenCharacterRunInputs
    {
        private readonly ReadOnlyCollection<FrozenRunEquipment> equipment;

        public FrozenCharacterRunInputs(
            CharacterInstanceSnapshot character,
            PlayerRouteProfilePayload routePayload,
            long loadoutSequence,
            string loadoutFingerprint,
            long holdingsSequence,
            string holdingsFingerprint,
            RankedSkillAllocationSnapshot skillSnapshot,
            DerivedCharacterStatsSnapshot characterStats,
            RunCombatProfile combatProfile,
            IEnumerable<FrozenRunEquipment> frozenEquipment,
            string eventModifierContextFingerprint)
        {
            Character = character
                ?? throw new ArgumentNullException(nameof(character));
            RoutePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (RoutePayload.SelectedCharacterStableId
                != Character.CharacterInstanceStableId)
            {
                throw new ArgumentException(
                    "Run route and selected permanent character identities must match.");
            }
            if (loadoutSequence < 0L || holdingsSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(loadoutSequence));
            }
            if (string.IsNullOrWhiteSpace(loadoutFingerprint)
                || string.IsNullOrWhiteSpace(holdingsFingerprint)
                || string.IsNullOrWhiteSpace(eventModifierContextFingerprint))
            {
                throw new ArgumentException(
                    "Frozen upstream fingerprints are required.");
            }
            SkillSnapshot = skillSnapshot
                ?? throw new ArgumentNullException(nameof(skillSnapshot));
            CharacterStats = characterStats
                ?? throw new ArgumentNullException(nameof(characterStats));
            CombatProfile = combatProfile
                ?? throw new ArgumentNullException(nameof(combatProfile));
            if (!string.Equals(
                CharacterStats.CharacterInstanceId,
                Character.CharacterInstanceStableId.ToString(),
                StringComparison.Ordinal)
                || !string.Equals(
                    CombatProfile.CharacterInstanceId,
                    Character.CharacterInstanceStableId.ToString(),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Derived-stat snapshots must belong to the exact selected character.");
            }

            List<FrozenRunEquipment> ordered =
                (frozenEquipment
                    ?? throw new ArgumentNullException(nameof(frozenEquipment)))
                .ToList();
            if (ordered.Count < 1 || ordered.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one non-null frozen equipment binding is required.",
                    nameof(frozenEquipment));
            }
            ordered.Sort();
            if (ordered.Select(item => item.SlotStableId).Distinct().Count()
                != ordered.Count
                || ordered.Select(item => item.EquipmentInstanceStableId)
                    .Distinct().Count() != ordered.Count)
            {
                throw new ArgumentException(
                    "Frozen slots and exact equipment-instance identities must be unique.",
                    nameof(frozenEquipment));
            }

            LoadoutSequence = loadoutSequence;
            LoadoutFingerprint = loadoutFingerprint.Trim();
            HoldingsSequence = holdingsSequence;
            HoldingsFingerprint = holdingsFingerprint.Trim();
            EventModifierContextFingerprint =
                eventModifierContextFingerprint.Trim();
            equipment = new ReadOnlyCollection<FrozenRunEquipment>(ordered);
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public CharacterInstanceSnapshot Character { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public long LoadoutSequence { get; }
        public string LoadoutFingerprint { get; }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public RankedSkillAllocationSnapshot SkillSnapshot { get; }
        public DerivedCharacterStatsSnapshot CharacterStats { get; }
        public RunCombatProfile CombatProfile { get; }
        public IReadOnlyList<FrozenRunEquipment> Equipment
        {
            get { return equipment; }
        }
        public string EventModifierContextFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(
                builder,
                "character",
                Character.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "route",
                RoutePayload.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "loadout-sequence",
                LoadoutSequence);
            RunSessionFingerprint.Append(
                builder,
                "loadout",
                LoadoutFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "holdings-sequence",
                HoldingsSequence);
            RunSessionFingerprint.Append(
                builder,
                "holdings",
                HoldingsFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "skills",
                SkillSnapshot.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "character-stats",
                CharacterStats.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "combat-profile",
                CombatProfile.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "event-context",
                EventModifierContextFingerprint);
            for (int index = 0; index < equipment.Count; index++)
            {
                RunSessionFingerprint.Append(
                    builder,
                    "equipment-" + index.ToString("D2", CultureInfo.InvariantCulture),
                    equipment[index].Fingerprint);
            }
            return builder.ToString();
        }
    }

    public sealed class RunSessionStartMaterial
    {
        private RunSessionStartMaterial(
            bool succeeded,
            string rejectionCode,
            FrozenCharacterRunInputs frozenInputs,
            RunSessionLivePorts runtimePorts)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            FrozenInputs = frozenInputs;
            RuntimePorts = runtimePorts;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public FrozenCharacterRunInputs FrozenInputs { get; }
        public RunSessionLivePorts RuntimePorts { get; }

        public static RunSessionStartMaterial Accept(
            FrozenCharacterRunInputs frozenInputs,
            RunSessionLivePorts runtimePorts)
        {
            return new RunSessionStartMaterial(
                true,
                string.Empty,
                frozenInputs ?? throw new ArgumentNullException(nameof(frozenInputs)),
                runtimePorts ?? throw new ArgumentNullException(nameof(runtimePorts)));
        }

        public static RunSessionStartMaterial Reject(string rejectionCode)
        {
            return new RunSessionStartMaterial(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-start-source-rejected"
                    : rejectionCode.Trim(),
                null,
                null);
        }
    }

    public interface IRunSessionStartSource
    {
        RunSessionStartMaterial Resolve(
            StartRunSessionCommand command,
            StableId resolvedRunStableId);
    }

    public interface IRunSessionLivePortFactory
    {
        RunSessionLivePorts Create(
            StartRunSessionCommand command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputs frozenInputs);
    }
}

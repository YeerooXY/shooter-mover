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
    public sealed class RunRuntimePortRestartResultV1
    {
        public RunRuntimePortRestartResultV1(
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

    public interface IRunLifecycleRuntimePortV1
    {
        string PortId { get; }
        long LifecycleGeneration { get; }
        string SnapshotFingerprint { get; }
        string ValidateRestart(
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick);
        RunRuntimePortRestartResultV1 Restart(
            StableId operationStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick);
    }

    public sealed class RunPlayerRuntimeSnapshotV1
    {
        public RunPlayerRuntimeSnapshotV1(
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
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
            RunSessionFingerprintV1.Append(builder, "actor", ActorInstanceStableId);
            RunSessionFingerprintV1.Append(builder, "participant", ParticipantStableId);
            RunSessionFingerprintV1.Append(builder, "generation", LifecycleGeneration);
            RunSessionFingerprintV1.Append(builder, "health", CurrentHealth);
            RunSessionFingerprintV1.Append(builder, "maximum-health", MaximumHealth);
            RunSessionFingerprintV1.Append(builder, "position-x", PositionX);
            RunSessionFingerprintV1.Append(builder, "position-y", PositionY);
            RunSessionFingerprintV1.Append(builder, "accepted-sequence", AcceptedSequence);
            return builder.ToString();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public interface IRunPlayerRuntimePortV1 : IRunLifecycleRuntimePortV1
    {
        RunPlayerRuntimeSnapshotV1 ExportSnapshot();
    }

    public interface IRunWeaponRuntimePortV1 : IRunLifecycleRuntimePortV1
    {
        IReadOnlyList<StableId> FrozenEquipmentInstanceStableIds { get; }
    }

    public interface IRunStatusEffectRuntimePortV1 : IRunLifecycleRuntimePortV1
    {
        int ActiveEffectCount { get; }
    }

    public interface IRunConditionalFactRuntimePortV1 :
        IRunLifecycleRuntimePortV1
    {
    }

    public interface IRunActiveAbilityRuntimePortV1 :
        IRunLifecycleRuntimePortV1
    {
    }

    public interface IRunRoomRuntimePortV1 : IRunLifecycleRuntimePortV1
    {
        StableId CurrentRoomStableId { get; }
    }

    public interface IRunMissionResultPortV1
    {
        long Sequence { get; }
        bool TryGetRun(StableId runStableId, out MissionRunPayloadV1 runPayload);
        MissionRunAuthorityResultV1 RecordCollectedStrongbox(
            RunStrongboxCollectionRequestV1 request,
            PlayerRouteProfilePayloadV1 routePayload);
        MissionRunAuthorityResultV1 EndRun(
            EndRunSessionCommandV1 command,
            PlayerRouteProfilePayloadV1 routePayload);
    }

    public sealed class RunSessionRuntimePortsV1
    {
        public RunSessionRuntimePortsV1(
            IRunPlayerRuntimePortV1 player,
            IRunWeaponRuntimePortV1 weapons,
            IRunStatusEffectRuntimePortV1 statusEffects,
            IRunConditionalFactRuntimePortV1 conditionalFacts,
            IRunActiveAbilityRuntimePortV1 activeAbilities,
            IRunRoomRuntimePortV1 rooms,
            IRunMissionResultPortV1 missionResults)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
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
            if (Weapons.LifecycleGeneration != generation
                || StatusEffects.LifecycleGeneration != generation
                || ConditionalFacts.LifecycleGeneration != generation
                || ActiveAbilities.LifecycleGeneration != generation
                || Rooms.LifecycleGeneration != generation)
            {
                throw new ArgumentException(
                    "All run-local runtime ports must start at one lifecycle generation.");
            }
        }

        public IRunPlayerRuntimePortV1 Player { get; }
        public IRunWeaponRuntimePortV1 Weapons { get; }
        public IRunStatusEffectRuntimePortV1 StatusEffects { get; }
        public IRunConditionalFactRuntimePortV1 ConditionalFacts { get; }
        public IRunActiveAbilityRuntimePortV1 ActiveAbilities { get; }
        public IRunRoomRuntimePortV1 Rooms { get; }
        public IRunMissionResultPortV1 MissionResults { get; }

        public IReadOnlyList<IRunLifecycleRuntimePortV1> LifecyclePorts
        {
            get
            {
                return new ReadOnlyCollection<IRunLifecycleRuntimePortV1>(
                    new List<IRunLifecycleRuntimePortV1>
                    {
                        Player,
                        Weapons,
                        StatusEffects,
                        ConditionalFacts,
                        ActiveAbilities,
                        Rooms,
                    });
            }
        }
    }

    public sealed class FrozenRunEquipmentV1 :
        IComparable<FrozenRunEquipmentV1>
    {
        public FrozenRunEquipmentV1(
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
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
        public StableId RuntimeWeaponReferenceStableId
        {
            get { return EquipmentDefinition.RuntimeWeaponReferenceId; }
        }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "slot", SlotStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "equipment-instance",
                EquipmentInstance.ToCanonicalString());
            RunSessionFingerprintV1.Append(
                builder,
                "equipment-instance-fingerprint",
                EquipmentInstance.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "equipment-definition",
                EquipmentDefinition.ToCanonicalString());
            return builder.ToString();
        }

        public int CompareTo(FrozenRunEquipmentV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }
            return SlotStableId.CompareTo(other.SlotStableId);
        }
    }

    public sealed class FrozenCharacterRunInputsV1
    {
        private readonly ReadOnlyCollection<FrozenRunEquipmentV1> equipment;

        public FrozenCharacterRunInputsV1(
            CharacterInstanceSnapshotV1 character,
            PlayerRouteProfilePayloadV1 routePayload,
            long loadoutSequence,
            string loadoutFingerprint,
            long holdingsSequence,
            string holdingsFingerprint,
            RankedSkillAllocationSnapshotV2 skillSnapshot,
            DerivedCharacterStatsSnapshotV1 characterStats,
            RunCombatProfileV1 combatProfile,
            IEnumerable<FrozenRunEquipmentV1> frozenEquipment,
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

            List<FrozenRunEquipmentV1> ordered =
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
            equipment = new ReadOnlyCollection<FrozenRunEquipmentV1>(ordered);
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public CharacterInstanceSnapshotV1 Character { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public long LoadoutSequence { get; }
        public string LoadoutFingerprint { get; }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public RankedSkillAllocationSnapshotV2 SkillSnapshot { get; }
        public DerivedCharacterStatsSnapshotV1 CharacterStats { get; }
        public RunCombatProfileV1 CombatProfile { get; }
        public IReadOnlyList<FrozenRunEquipmentV1> Equipment
        {
            get { return equipment; }
        }
        public string EventModifierContextFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(
                builder,
                "character",
                Character.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "route",
                RoutePayload.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "loadout-sequence",
                LoadoutSequence);
            RunSessionFingerprintV1.Append(
                builder,
                "loadout",
                LoadoutFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "holdings-sequence",
                HoldingsSequence);
            RunSessionFingerprintV1.Append(
                builder,
                "holdings",
                HoldingsFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "skills",
                SkillSnapshot.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "character-stats",
                CharacterStats.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "combat-profile",
                CombatProfile.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "event-context",
                EventModifierContextFingerprint);
            for (int index = 0; index < equipment.Count; index++)
            {
                RunSessionFingerprintV1.Append(
                    builder,
                    "equipment-" + index.ToString("D2", CultureInfo.InvariantCulture),
                    equipment[index].Fingerprint);
            }
            return builder.ToString();
        }
    }

    public sealed class RunSessionStartMaterialV1
    {
        private RunSessionStartMaterialV1(
            bool succeeded,
            string rejectionCode,
            FrozenCharacterRunInputsV1 frozenInputs,
            RunSessionRuntimePortsV1 runtimePorts)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            FrozenInputs = frozenInputs;
            RuntimePorts = runtimePorts;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public FrozenCharacterRunInputsV1 FrozenInputs { get; }
        public RunSessionRuntimePortsV1 RuntimePorts { get; }

        public static RunSessionStartMaterialV1 Accept(
            FrozenCharacterRunInputsV1 frozenInputs,
            RunSessionRuntimePortsV1 runtimePorts)
        {
            return new RunSessionStartMaterialV1(
                true,
                string.Empty,
                frozenInputs ?? throw new ArgumentNullException(nameof(frozenInputs)),
                runtimePorts ?? throw new ArgumentNullException(nameof(runtimePorts)));
        }

        public static RunSessionStartMaterialV1 Reject(string rejectionCode)
        {
            return new RunSessionStartMaterialV1(
                false,
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "run-start-source-rejected"
                    : rejectionCode.Trim(),
                null,
                null);
        }
    }

    public interface IRunSessionStartSourceV1
    {
        RunSessionStartMaterialV1 Resolve(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId);
    }

    public interface IRunSessionRuntimePortFactoryV1
    {
        RunSessionRuntimePortsV1 Create(
            StartRunSessionCommandV1 command,
            StableId resolvedRunStableId,
            FrozenCharacterRunInputsV1 frozenInputs);
    }
}

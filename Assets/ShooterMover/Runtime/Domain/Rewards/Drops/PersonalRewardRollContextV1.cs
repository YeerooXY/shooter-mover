using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    /// <summary>Complete immutable identity and balance context for one participant roll.</summary>
    public sealed class PersonalRewardRollContextV1
    {
        private readonly ReadOnlyCollection<StableId> eventModifierIds;
        private readonly string canonicalText;
        public PersonalRewardRollContextV1(StableId runStableId, int runLifecycleGeneration, StableId terminalSourceStableId, int sourceLifecycleGeneration, StableId roomStableId, int roomLifecycleGeneration, StableId placementStableId, StableId participantStableId, bool participantEligible, int playerLevel, int missionLevel, StableId difficultyStableId, StableId gameModeStableId, IEnumerable<StableId> eventModifierIds, int moneyQuantityMultiplierPermille, int scrapQuantityMultiplierPermille, RewardProfileResolutionV1 profileResolution, RunDropPacingPolicyV1 pacingPolicy, ulong rootSeed, int algorithmVersion)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            TerminalSourceStableId = terminalSourceStableId ?? throw new ArgumentNullException(nameof(terminalSourceStableId));
            RoomStableId = roomStableId ?? throw new ArgumentNullException(nameof(roomStableId));
            PlacementStableId = placementStableId ?? throw new ArgumentNullException(nameof(placementStableId));
            ParticipantStableId = participantStableId ?? throw new ArgumentNullException(nameof(participantStableId));
            DifficultyStableId = difficultyStableId ?? throw new ArgumentNullException(nameof(difficultyStableId));
            GameModeStableId = gameModeStableId ?? throw new ArgumentNullException(nameof(gameModeStableId));
            ProfileResolution = profileResolution ?? throw new ArgumentNullException(nameof(profileResolution));
            PacingPolicy = pacingPolicy ?? throw new ArgumentNullException(nameof(pacingPolicy));
            if (runLifecycleGeneration < 1 || sourceLifecycleGeneration < 1 || roomLifecycleGeneration < 1 || playerLevel < 0 || missionLevel < 0 || moneyQuantityMultiplierPermille < 0 || scrapQuantityMultiplierPermille < 0 || algorithmVersion < 1) throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            RunLifecycleGeneration = runLifecycleGeneration; SourceLifecycleGeneration = sourceLifecycleGeneration; RoomLifecycleGeneration = roomLifecycleGeneration; ParticipantEligible = participantEligible; PlayerLevel = playerLevel; MissionLevel = missionLevel; MoneyQuantityMultiplierPermille = moneyQuantityMultiplierPermille; ScrapQuantityMultiplierPermille = scrapQuantityMultiplierPermille; this.eventModifierIds = CopyIds(eventModifierIds); RootSeed = rootSeed; AlgorithmVersion = algorithmVersion;
            var builder = new StringBuilder("schema=personal-reward-roll-context-v1");
            builder.Append("\nrun_id=").Append(RunStableId).Append("\nrun_lifecycle=").Append(RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture)).Append("\nterminal_source_id=").Append(TerminalSourceStableId).Append("\nsource_lifecycle=").Append(SourceLifecycleGeneration.ToString(CultureInfo.InvariantCulture)).Append("\nroom_id=").Append(RoomStableId).Append("\nroom_lifecycle=").Append(RoomLifecycleGeneration.ToString(CultureInfo.InvariantCulture)).Append("\nplacement_id=").Append(PlacementStableId).Append("\nparticipant_id=").Append(ParticipantStableId).Append("\nparticipant_eligible=").Append(ParticipantEligible ? "1" : "0").Append("\nplayer_level=").Append(PlayerLevel.ToString(CultureInfo.InvariantCulture)).Append("\nmission_level=").Append(MissionLevel.ToString(CultureInfo.InvariantCulture)).Append("\ndifficulty_id=").Append(DifficultyStableId).Append("\ngame_mode_id=").Append(GameModeStableId).Append("\nmoney_quantity_multiplier_permille=").Append(MoneyQuantityMultiplierPermille.ToString(CultureInfo.InvariantCulture)).Append("\nscrap_quantity_multiplier_permille=").Append(ScrapQuantityMultiplierPermille.ToString(CultureInfo.InvariantCulture)).Append("\nprofile_resolution=").Append(ProfileResolution.Fingerprint).Append("\npacing_policy=").Append(PacingPolicy.Fingerprint).Append("\nroot_seed=").Append(RootSeed.ToString(CultureInfo.InvariantCulture)).Append("\nalgorithm_version=").Append(AlgorithmVersion.ToString(CultureInfo.InvariantCulture)).Append("\nevent_modifier_count=").Append(this.eventModifierIds.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < this.eventModifierIds.Count; index++) builder.Append("\nevent_modifier_").Append(index.ToString("D4", CultureInfo.InvariantCulture)).Append("=").Append(this.eventModifierIds[index]);
            canonicalText = builder.ToString(); Fingerprint = RewardGenerationFingerprintV1.Compute(canonicalText);
            OperationStableId = RewardGenerationFingerprintV1.DeriveStableId("personalrewardoperation", Fingerprint, ProfileResolution.EffectiveProfile.Fingerprint, ParticipantStableId.ToString(), AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
        }
        public StableId RunStableId { get; } public int RunLifecycleGeneration { get; } public StableId TerminalSourceStableId { get; } public int SourceLifecycleGeneration { get; } public StableId RoomStableId { get; } public int RoomLifecycleGeneration { get; } public StableId PlacementStableId { get; } public StableId ParticipantStableId { get; } public bool ParticipantEligible { get; } public int PlayerLevel { get; } public int MissionLevel { get; } public StableId DifficultyStableId { get; } public StableId GameModeStableId { get; } public IReadOnlyList<StableId> EventModifierIds { get { return eventModifierIds; } } public int MoneyQuantityMultiplierPermille { get; } public int ScrapQuantityMultiplierPermille { get; } public RewardProfileResolutionV1 ProfileResolution { get; } public RunDropPacingPolicyV1 PacingPolicy { get; } public ulong RootSeed { get; } public int AlgorithmVersion { get; } public StableId OperationStableId { get; } public string Fingerprint { get; }
        public IEnumerable<StableId> EnumerateTierSelectionContexts() { yield return DifficultyStableId; yield return GameModeStableId; for (int index = 0; index < eventModifierIds.Count; index++) yield return eventModifierIds[index]; }
        public string ToCanonicalString() { return canonicalText; }
        private static ReadOnlyCollection<StableId> CopyIds(IEnumerable<StableId> source) { var set = new SortedSet<StableId>(); if (source != null) foreach (StableId value in source) { if (value == null) throw new ArgumentException("Event modifier identities must not contain null entries.", nameof(source)); set.Add(value); } return new ReadOnlyCollection<StableId>(new List<StableId>(set)); }
    }
}

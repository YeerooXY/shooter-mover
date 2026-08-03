using System;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.LootDropBinding;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    public static class CompactEnemyDropProfiles
    {
        public static StableId Resolve(
            string authoredValue,
            StableId definitionStableId)
        {
            string value = (authoredValue ?? string.Empty).Trim().ToLowerInvariant();
            switch (value)
            {
                case "small":
                    return LootSourceCatalog.SmallEnemyId;
                case "normal":
                    return LootSourceCatalog.NormalEnemyId;
                case "large":
                    return LootSourceCatalog.LargeEnemyId;
                case "boss":
                    return LootSourceCatalog.BossEnemyId;
                case "none":
                case "no-drop":
                case "explicit-no-drop":
                    return LootSourceCatalog.ExplicitNoDropId;
                default:
                    throw new InvalidOperationException(
                        "compact-enemy-drop-profile-unsupported:"
                        + definitionStableId + ":" + value);
            }
        }
    }

    public sealed class CompactEnemyTerminalRewardFact :
        ITerminalRewardPlacementFact
    {
        public CompactEnemyTerminalRewardFact(
            StableId deathEventStableId,
            StableId triggeringEventStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            int roomLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId placementStableId,
            long sourceLifecycleGeneration,
            StableId roomStableId,
            StableId definitionStableId,
            int enemyLevel,
            StableId damageSourceStableId,
            StableId attributedParticipantStableId,
            StableId declaredDropProfileStableId,
            EnemyActorDeathCause deathCause)
        {
            DeathEventStableId = deathEventStableId
                ?? throw new ArgumentNullException(nameof(deathEventStableId));
            TriggeringEventStableId = triggeringEventStableId
                ?? throw new ArgumentNullException(nameof(triggeringEventStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            if (roomLifecycleGeneration < 1)
                throw new ArgumentOutOfRangeException(nameof(roomLifecycleGeneration));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            PlacementStableId = placementStableId
                ?? throw new ArgumentNullException(nameof(placementStableId));
            if (sourceLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (enemyLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(enemyLevel));
            if (!Enum.IsDefined(typeof(EnemyActorDeathCause), deathCause))
                throw new ArgumentOutOfRangeException(nameof(deathCause));
            DamageSourceStableId = damageSourceStableId;
            AttributedParticipantStableId = attributedParticipantStableId;
            DeclaredDropProfileStableId = declaredDropProfileStableId
                ?? throw new ArgumentNullException(
                    nameof(declaredDropProfileStableId));
            RunLifecycleGeneration = runLifecycleGeneration;
            RoomLifecycleGeneration = roomLifecycleGeneration;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            EnemyLevel = enemyLevel;
            DeathCause = deathCause;
        }

        public StableId DeathEventStableId { get; }
        public StableId TriggeringEventStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public int RoomLifecycleGeneration { get; }
        public StableId SourceEntityStableId { get; }
        public StableId PlacementStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public StableId RoomStableId { get; }
        public StableId DefinitionStableId { get; }
        public int EnemyLevel { get; }
        public StableId DamageSourceStableId { get; }
        public StableId AttributedParticipantStableId { get; }
        public StableId DeclaredDropProfileStableId { get; }
        public EnemyActorDeathCause DeathCause { get; }

        public StableId RewardTerminalEventStableId
        {
            get { return DeathEventStableId; }
        }

        public StableId RewardRoomStableId
        {
            get { return RoomStableId; }
        }

        public int RewardRoomLifecycleGeneration
        {
            get { return RoomLifecycleGeneration; }
        }

        public StableId RewardPlacementStableId
        {
            get { return PlacementStableId; }
        }

        public string RewardPlacementFingerprint
        {
            get
            {
                return CompactEnemyLootFingerprint.Hash(
                    "compact-enemy-reward-placement-v1|"
                    + RunStableId + "|" + RoomStableId + "|"
                    + RoomLifecycleGeneration + "|" + PlacementStableId + "|"
                    + DeathEventStableId);
            }
        }
    }

    public sealed class CompactEnemyLootDropFactBridge : ILootDropFactBridge
    {
        public StableId FactKindStableId
        {
            get { return LootDropFactKindIds.EnemyDeath; }
        }

        public Type FactType
        {
            get { return typeof(CompactEnemyTerminalRewardFact); }
        }

        public LootDropAdaptationResult Adapt(object terminalFact)
        {
            CompactEnemyTerminalRewardFact fact =
                terminalFact as CompactEnemyTerminalRewardFact;
            if (fact == null
                || fact.DeathEventStableId == null
                || fact.TriggeringEventStableId == null
                || fact.DefinitionStableId == null
                || fact.RunStableId == null
                || fact.SourceEntityStableId == null
                || fact.RoomStableId == null
                || fact.PlacementStableId == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "compact-enemy-terminal-fact-incomplete");
            }

            CompactEnemyDefinition definition;
            if (!CompactEnemyCatalog.TryResolve(
                    fact.DefinitionStableId,
                    out definition)
                || definition == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingDefinition,
                    "compact-enemy-definition-missing:" + fact.DefinitionStableId);
            }

            StableId declared;
            try
            {
                declared = CompactEnemyDropProfiles.Resolve(
                    definition.drops,
                    fact.DefinitionStableId);
            }
            catch (Exception exception)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingDropProfile,
                    exception.Message);
            }
            if (declared != fact.DeclaredDropProfileStableId)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.DropProfileMismatch,
                    "compact-enemy-drop-profile-mismatch:fact="
                    + fact.DeclaredDropProfileStableId + ";definition=" + declared);
            }
            if (fact.AttributedParticipantStableId == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.UnattributedTerminalFact,
                    "compact-enemy-terminal-participant-missing");
            }

            string sourceContextFingerprint = CompactEnemyLootFingerprint.Hash(
                "compact-enemy-source-context-v1|" + fact.RunStableId + "|"
                + fact.RoomStableId + "|" + fact.SourceEntityStableId + "|"
                + fact.PlacementStableId + "|" + fact.SourceLifecycleGeneration);
            string definitionFingerprint = CompactEnemyLootFingerprint.Hash(
                "compact-enemy-definition-v1|" + JsonUtility.ToJson(definition));
            string upstreamFingerprint = CompactEnemyLootFingerprint.Hash(
                "compact-enemy-death-fact-v1|" + fact.DeathEventStableId + "|"
                + fact.TriggeringEventStableId + "|" + fact.RunStableId + "|"
                + fact.RoomStableId + "|" + fact.SourceEntityStableId + "|"
                + fact.PlacementStableId + "|" + fact.DefinitionStableId + "|"
                + fact.EnemyLevel + "|" + fact.SourceLifecycleGeneration + "|"
                + fact.DamageSourceStableId + "|"
                + fact.AttributedParticipantStableId + "|"
                + fact.DeclaredDropProfileStableId + "|" + fact.DeathCause);
            StableId damageChannel =
                fact.DeathCause == EnemyActorDeathCause.DisposableImpact
                    ? StableId.Parse("damage.impact")
                    : StableId.Parse("damage.kinetic");

            return LootDropAdaptationResult.Accepted(
                new LootDropSourceFact(
                    FactKindStableId,
                    fact.DeathEventStableId,
                    fact.TriggeringEventStableId,
                    fact.RunStableId,
                    fact.RunLifecycleGeneration,
                    fact.SourceEntityStableId,
                    fact.PlacementStableId,
                    fact.SourceLifecycleGeneration,
                    fact.DefinitionStableId,
                    fact.AttributedParticipantStableId,
                    fact.DamageSourceStableId,
                    damageChannel,
                    declared,
                    sourceContextFingerprint,
                    definitionFingerprint,
                    upstreamFingerprint));
        }
    }

    internal static class CompactEnemyLootFingerprint
    {
        public static string Hash(string material)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(
                    Encoding.UTF8.GetBytes(material ?? string.Empty));
                return BitConverter.ToString(digest)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunLoot
{
    public interface IRunLootSourcePositionPort
    {
        bool TryResolve(
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            out RunLootWorldSpawnContext worldSpawnContext,
            out string diagnostic);
    }

    /// <summary>
    /// One coherent read of the owning Run Session facts needed by pickup realization and
    /// collection. The next order is derived by the Run Session from its current-lifecycle
    /// collection journal; the pickup authority never maintains a second sequence counter.
    /// </summary>
    public sealed class RunLootRunSessionContext
    {
        public RunLootRunSessionContext(
            StableId runStableId,
            long lifecycleGeneration,
            long authoritativeTick,
            bool isActive,
            StableId playerActorStableId,
            StableId playerParticipantStableId,
            long nextCollectionOrder)
        {
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            if (authoritativeTick < 0L)
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            PlayerActorStableId = playerActorStableId
                ?? throw new ArgumentNullException(nameof(playerActorStableId));
            PlayerParticipantStableId = playerParticipantStableId
                ?? throw new ArgumentNullException(nameof(playerParticipantStableId));
            if (nextCollectionOrder < 1L)
                throw new ArgumentOutOfRangeException(nameof(nextCollectionOrder));

            LifecycleGeneration = lifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            IsActive = isActive;
            NextCollectionOrder = nextCollectionOrder;
        }

        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public bool IsActive { get; }
        public StableId PlayerActorStableId { get; }
        public StableId PlayerParticipantStableId { get; }
        public long NextCollectionOrder { get; }
    }

    public interface IRunLootRunSessionPort
    {
        StableId RunStableId { get; }
        long LifecycleGeneration { get; }
        long AuthoritativeTick { get; }
        bool IsActive { get; }
        StableId PlayerActorStableId { get; }
        StableId PlayerParticipantStableId { get; }

        bool TryReadContext(
            out RunLootRunSessionContext context,
            out string diagnostic);

        RunLootSessionRecordResult RecordCollection(
            RunLootCollectionFact fact);
    }

    public interface IRunLootCollectionState
    {
        RunLootCollectionResult Collect(
            RunLootCollectionCommand command);
        IReadOnlyList<RunLootSnapshot> ExportPickups();
        IReadOnlyList<RunLootSnapshot> ExportAvailablePickups();
        bool TryGetPickup(
            StableId pickupStableId,
            out RunLootSnapshot pickup);
    }

    public static class RunLootIdentity
    {
        public static StableId DerivePickupStableId(
            RunLootGeneratedBatch batch,
            RunLootGeneratedReward reward)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            return RunLoot.DeriveStableId(
                "runpickup",
                batch.RunStableId.ToString(),
                batch.RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture),
                batch.DropOperationStableId.ToString(),
                reward.RewardInstanceStableId.ToString(),
                reward.GeneratedRewardFingerprint);
        }

        public static StableId DeriveCollectionOperationStableId(
            StableId pickupStableId,
            StableId collectorEntityStableId,
            StableId collectorParticipantStableId)
        {
            if (pickupStableId == null)
                throw new ArgumentNullException(nameof(pickupStableId));
            if (collectorEntityStableId == null)
                throw new ArgumentNullException(nameof(collectorEntityStableId));
            if (collectorParticipantStableId == null)
                throw new ArgumentNullException(nameof(collectorParticipantStableId));
            return RunLoot.DeriveStableId(
                "runpickupcollect",
                pickupStableId.ToString(),
                collectorEntityStableId.ToString(),
                collectorParticipantStableId.ToString());
        }
    }

    internal static class RunLoot
    {
        public static void Append(StringBuilder builder, string name, object value)
        {
            string text = value == null
                ? "none"
                : Convert.ToString(value, CultureInfo.InvariantCulture);
            builder.Append('\n')
                .Append(name.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(name)
                .Append('=')
                .Append(text.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(text);
        }

        public static string Hash(string canonicalText)
        {
            byte[] input = Encoding.UTF8.GetBytes(canonicalText ?? string.Empty);
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(input);
            }
            var builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static StableId DeriveStableId(
            string namespaceName,
            params string[] material)
        {
            var builder = new StringBuilder("schema=run-pickup-stable-id-v1");
            Append(builder, "namespace", namespaceName);
            for (int index = 0; index < material.Length; index++)
            {
                Append(
                    builder,
                    "material:" + index.ToString(CultureInfo.InvariantCulture),
                    material[index]);
            }
            string hash = Hash(builder.ToString());
            return StableId.Create(namespaceName, hash.Substring(7, 40));
        }
    }
}

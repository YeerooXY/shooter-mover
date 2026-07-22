using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.RunPickups
{
    public interface IRunPickupSourcePositionPortV1
    {
        bool TryResolve(
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            out RunPickupWorldSpawnContextV1 worldSpawnContext,
            out string diagnostic);
    }

    /// <summary>
    /// One coherent read of the owning Run Session facts needed by pickup realization and
    /// collection. The next order is derived by the Run Session from its current-lifecycle
    /// collection journal; the pickup authority never maintains a second sequence counter.
    /// </summary>
    public sealed class RunPickupRunSessionContextV1
    {
        public RunPickupRunSessionContextV1(
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

    public interface IRunPickupRunSessionPortV1
    {
        StableId RunStableId { get; }
        long LifecycleGeneration { get; }
        long AuthoritativeTick { get; }
        bool IsActive { get; }
        StableId PlayerActorStableId { get; }
        StableId PlayerParticipantStableId { get; }

        bool TryReadContext(
            out RunPickupRunSessionContextV1 context,
            out string diagnostic);

        RunPickupSessionRecordResultV1 RecordCollection(
            RunPickupCollectionFactV1 fact);
    }

    public interface IRunPickupCollectionAuthorityV1
    {
        RunPickupCollectionResultV1 Collect(
            RunPickupCollectionCommandV1 command);
        IReadOnlyList<RunPickupSnapshotV1> ExportPickups();
        IReadOnlyList<RunPickupSnapshotV1> ExportAvailablePickups();
        bool TryGetPickup(
            StableId pickupStableId,
            out RunPickupSnapshotV1 pickup);
    }

    public static class RunPickupIdentityV1
    {
        public static StableId DerivePickupStableId(
            RunPickupGeneratedBatchV1 batch,
            RunPickupGeneratedRewardV1 reward)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            return RunPickupCanonicalV1.DeriveStableId(
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
            return RunPickupCanonicalV1.DeriveStableId(
                "runpickupcollect",
                pickupStableId.ToString(),
                collectorEntityStableId.ToString(),
                collectorParticipantStableId.ToString());
        }
    }

    internal static class RunPickupCanonicalV1
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

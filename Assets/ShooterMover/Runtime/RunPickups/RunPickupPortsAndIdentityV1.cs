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

    public interface IRunPickupRunSessionPortV1
    {
        StableId RunStableId { get; }
        long LifecycleGeneration { get; }
        long AuthoritativeTick { get; }
        bool IsActive { get; }
        StableId PlayerActorStableId { get; }
        StableId PlayerParticipantStableId { get; }

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

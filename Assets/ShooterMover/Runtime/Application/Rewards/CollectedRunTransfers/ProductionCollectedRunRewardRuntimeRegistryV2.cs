using System;
using System.Collections.Generic;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Character-scoped reference registry only. RAP, prepared custody and receipts remain
    /// the owners of their own state and are restored through normal character adapters.
    /// </summary>
    public static class ProductionCollectedRunRewardRuntimeRegistryV2
    {
        private sealed class Entry
        {
            public RewardApplicationServiceV1 RewardApplication;
            public CollectedRunRewardPreparedTransferAuthorityV1 Prepared;
            public CollectedRunRewardTransferReceiptAuthorityV1 Receipts;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<StableId, Entry> Entries =
            new Dictionary<StableId, Entry>();

        public static void BindRewardApplication(
            StableId characterStableId,
            RewardApplicationServiceV1 rewardApplication)
        {
            if (characterStableId == null)
                throw new ArgumentNullException(nameof(characterStableId));
            if (rewardApplication == null)
                throw new ArgumentNullException(nameof(rewardApplication));
            lock (Gate)
            {
                Entry entry = GetOrCreate(characterStableId);
                entry.RewardApplication = rewardApplication;
            }
        }

        public static IReadOnlyList<ISaveComponentAdapterV1> CreateSaveAdapters(
            StableId characterStableId)
        {
            if (characterStableId == null)
                throw new ArgumentNullException(nameof(characterStableId));
            lock (Gate)
            {
                Entry entry = GetOrCreate(characterStableId);
                entry.Prepared =
                    new CollectedRunRewardPreparedTransferAuthorityV1();
                entry.Receipts =
                    new CollectedRunRewardTransferReceiptAuthorityV1();
                return new ISaveComponentAdapterV1[]
                {
                    CollectedRunRewardPreparedTransferSaveComponentV1
                        .CreateAdapter(entry.Prepared),
                    CollectedRunRewardTransferReceiptSaveComponentV1
                        .CreateAdapter(entry.Receipts),
                };
            }
        }

        public static bool TryResolve(
            StableId characterStableId,
            out RewardApplicationServiceV1 rewardApplication,
            out CollectedRunRewardPreparedTransferAuthorityV1 prepared,
            out CollectedRunRewardTransferReceiptAuthorityV1 receipts)
        {
            rewardApplication = null;
            prepared = null;
            receipts = null;
            if (characterStableId == null) return false;
            lock (Gate)
            {
                Entry entry;
                if (!Entries.TryGetValue(characterStableId, out entry)
                    || entry.RewardApplication == null
                    || entry.Prepared == null
                    || entry.Receipts == null)
                {
                    return false;
                }
                rewardApplication = entry.RewardApplication;
                prepared = entry.Prepared;
                receipts = entry.Receipts;
                return true;
            }
        }

        public static void Release(StableId characterStableId)
        {
            if (characterStableId == null) return;
            lock (Gate) Entries.Remove(characterStableId);
        }

        private static Entry GetOrCreate(StableId characterStableId)
        {
            Entry entry;
            if (!Entries.TryGetValue(characterStableId, out entry))
            {
                entry = new Entry();
                Entries.Add(characterStableId, entry);
            }
            return entry;
        }
    }
}

using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Read-only exact-instance projection over the holdings authority. The expensive
    /// immutable snapshot is rebuilt only when the authority sequence changes.
    /// </summary>
    public sealed class PlayerHoldingsEquipmentInstanceLookup : IPlayerEquipmentInstanceLookup
    {
        private readonly object gate = new object();
        private readonly IPlayerHoldingsAuthorityV1 holdings;
        private Dictionary<StableId, EquipmentInstance> equipmentByInstanceId =
            new Dictionary<StableId, EquipmentInstance>();
        private long cachedSequence = -1L;

        public PlayerHoldingsEquipmentInstanceLookup(IPlayerHoldingsAuthorityV1 holdingsAuthority)
        {
            holdings = holdingsAuthority ?? throw new ArgumentNullException(nameof(holdingsAuthority));
        }

        public long CachedSequence
        {
            get
            {
                lock (gate)
                {
                    return cachedSequence;
                }
            }
        }

        public bool TryResolve(
            EquipmentInstanceId equipmentInstanceId,
            out EquipmentInstance equipmentInstance)
        {
            equipmentInstance = null;
            if (equipmentInstanceId == null)
            {
                return false;
            }

            lock (gate)
            {
                if (!RefreshIfNeeded())
                {
                    return false;
                }

                return equipmentByInstanceId.TryGetValue(
                    equipmentInstanceId.Value,
                    out equipmentInstance);
            }
        }

        private bool RefreshIfNeeded()
        {
            long currentSequence;
            try
            {
                currentSequence = holdings.Sequence;
            }
            catch
            {
                return false;
            }

            if (currentSequence == cachedSequence)
            {
                return true;
            }

            PlayerHoldingsSnapshotV1 snapshot;
            try
            {
                snapshot = holdings.ExportSnapshot();
            }
            catch
            {
                return false;
            }

            if (snapshot == null)
            {
                return false;
            }

            var rebuilt = new Dictionary<StableId, EquipmentInstance>();
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                var holding = snapshot.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind != RewardGrantKindV1.EquipmentReference
                    || holding.InstanceStableId == null
                    || holding.EquipmentInstance == null
                    || holding.EquipmentInstance.InstanceId != holding.InstanceStableId)
                {
                    continue;
                }

                rebuilt[holding.InstanceStableId] = holding.EquipmentInstance;
            }

            equipmentByInstanceId = rebuilt;
            cachedSequence = currentSequence;
            return true;
        }
    }
}

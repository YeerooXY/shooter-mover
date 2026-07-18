using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Run
{
    /// <summary>
    /// Production-owned relay for accepted Stage 1 enemy destruction facts.
    ///
    /// Concrete enemy presentation remains responsible for deciding when it has an accepted
    /// destruction notification. This component only binds that fact to one registered room and
    /// forwards it to the production run binding. It keeps no room-clear, reward, XP or duplicate
    /// operation authority; the run session remains authoritative for all mutation and replay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stage1EnemyDestructionRelayV1 : MonoBehaviour
    {
        private IStage1ProductionRunBindingV1 runBinding;
        private StableId roomStableId;

        public bool IsConfigured
        {
            get { return runBinding != null && roomStableId != null; }
        }

        public StableId RoomStableId
        {
            get { return roomStableId; }
        }

        public bool Configure(
            IStage1ProductionRunBindingV1 binding,
            StableId registeredRoomStableId)
        {
            if (binding == null || registeredRoomStableId == null)
            {
                return false;
            }

            if (IsConfigured)
            {
                return ReferenceEquals(runBinding, binding)
                    && roomStableId == registeredRoomStableId;
            }

            runBinding = binding;
            roomStableId = registeredRoomStableId;
            return true;
        }

        public bool TryReportAccepted(
            EnemyDestroyedNotification acceptedDestruction,
            out LevelRunEnemyDestructionResultV1 result)
        {
            result = null;
            if (!IsConfigured || acceptedDestruction == null)
            {
                return false;
            }

            result = runBinding.ReportEnemyDestroyed(
                roomStableId,
                acceptedDestruction);
            return result != null;
        }
    }
}

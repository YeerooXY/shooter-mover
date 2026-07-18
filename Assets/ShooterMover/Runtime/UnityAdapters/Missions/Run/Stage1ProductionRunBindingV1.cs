using System.Collections.Generic;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.UnityAdapters.Missions.Run
{
    /// <summary>
    /// Stable command surface consumed by Stage 1 runtime presentation. This keeps the
    /// accepted enemy, door, extraction and weapon packages independent from the concrete
    /// scene adapter component.
    /// </summary>
    public interface IStage1ProductionRunBindingV1
    {
        bool IsConfigured { get; }
        StableId CurrentRoomStableId { get; }
        int ActiveWeaponSlotIndex { get; }
        StableId ActiveEquipmentInstanceStableId { get; }

        Stage1WeaponSlotSelectionStatusV1 SelectWeaponSlot(int slotIndex);

        Stage1RunRegistrationStatusV1 RegisterRoom(
            StableId roomStableId,
            IEnumerable<Stage1RunEnemyRegistrationV1> enemies);

        LevelRunEnemyDestructionResultV1 ReportEnemyDestroyed(
            StableId roomStableId,
            EnemyDestroyedNotification acceptedDestruction);

        Stage1SceneAdapterStatusV1 TryTraverse(
            StableId exitStableId,
            out RoomGraphOperationResultV1 traversal);

        Stage1SceneAdapterStatusV1 CompleteAndRouteResults(
            out Stage1RunCompletionResultV1 completion);
    }
}

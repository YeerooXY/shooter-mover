using ShooterMover.Domain.Common;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UnityAdapters.Missions.Rooms;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        internal bool IsRunPickupProductionReady
        {
            get
            {
                return initialized
                    && controller != null
                    && controller.PlayerLiveAuthority != null
                    && controller.PlayerLiveAuthority.IsInitialized
                    && controller.PlayerTransform != null
                    && rooms != null
                    && runStableId != null;
            }
        }

        internal Stage1VisibleSliceController RunPickupController
        {
            get { return controller; }
        }

        internal StableId RunPickupRunStableId
        {
            get { return runStableId; }
        }

        internal RoomRuntimeComposition2D RunPickupRooms
        {
            get { return rooms; }
        }

    }
}

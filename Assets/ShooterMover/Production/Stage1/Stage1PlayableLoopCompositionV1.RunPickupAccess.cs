using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;

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
                    && rooms != null
                    && missionResults != null
                    && holdings != null
                    && effectEmitter != null
                    && experience != null
                    && runStableId != null;
            }
        }

        internal Stage1VisibleSliceController RunPickupController
        {
            get { return controller; }
        }

        internal ProductionFlowCoordinatorV1 RunPickupFlow
        {
            get { return flow; }
        }

        internal StableId RunPickupRunStableId
        {
            get { return runStableId; }
        }

        internal RoomRuntimeComposition2D RunPickupRooms
        {
            get { return rooms; }
        }

        internal MissionRunResultAuthorityV1 RunPickupMissionResults
        {
            get { return missionResults; }
        }

        internal PlayerHoldingsService RunPickupHoldings
        {
            get { return holdings; }
        }

        internal InventoryWeaponEffectEmitter2D RunPickupEffectEmitter
        {
            get { return effectEmitter; }
        }

        internal PlayerExperienceAuthorityV1 RunPickupExperience
        {
            get { return experience; }
        }
    }
}

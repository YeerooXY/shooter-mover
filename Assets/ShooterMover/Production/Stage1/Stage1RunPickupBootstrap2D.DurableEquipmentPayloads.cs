using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.UI.ProductionFlow;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1RunPickupBootstrap2D
    {
        private RetainedTerminalDropEquipmentPayloadAuthority equipmentPayloads;

        /// <summary>
        /// Exact run-local equipment payloads consumed by the merged durable collected-run
        /// transfer flow. The personal reward authority remains the sole owner of reward
        /// probabilities; this boundary only retains concrete payloads for equipment grants.
        /// </summary>
        internal ICollectedRunEquipmentPayloadSource EquipmentPayloadSource
        {
            get { return equipmentPayloads; }
        }

        private void ConfigureDurableEquipmentPayloadRetention()
        {
            ProductionCharacterRuntimeGraphV1 graph;
            ProductionFlowProfileRecordV1 selectedProfile;
            CharacterCompositionCoordinatorV1 composition;
            if (!ProductionCharacterAccountCompositionV1.TryResolveCurrent(
                    out graph,
                    out selectedProfile,
                    out composition)
                || graph == null
                || graph.IsDisposed)
            {
                throw new InvalidOperationException(
                    "The selected character graph is unavailable for exact equipment payload retention.");
            }

            equipmentPayloads = new RetainedTerminalDropEquipmentPayloadAuthority(
                new RewardGenerationServiceV1(),
                graph.LoadoutRuntime.EquipmentCatalog);
        }

        private void ReleaseDurableEquipmentPayloadRetention()
        {
            equipmentPayloads = null;
        }
    }
}

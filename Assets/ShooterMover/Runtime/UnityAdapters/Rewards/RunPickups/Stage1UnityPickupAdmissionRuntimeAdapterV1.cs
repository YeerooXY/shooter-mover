using System;
using ShooterMover.Domain.Common;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Public composition adapter used by the Stage 1 predefined assembly. It delegates only
    /// to the typed source registry, exact pending-admission consumer, and generic presenter.
    /// </summary>
    public sealed class Stage1UnityPickupAdmissionRuntimeAdapterV1 :
        IStage1PickupAdmissionRuntimeV1
    {
        private readonly RunPickupSourcePositionRegistry2D sourcePositions;
        private readonly PendingTerminalDropPickupConsumerV1 pickupConsumer;
        private readonly RunPickupPresenter2D presenter;

        public Stage1UnityPickupAdmissionRuntimeAdapterV1(
            RunPickupSourcePositionRegistry2D sourcePositions,
            PendingTerminalDropPickupConsumerV1 pickupConsumer,
            RunPickupPresenter2D presenter)
        {
            this.sourcePositions = sourcePositions
                ?? throw new ArgumentNullException(nameof(sourcePositions));
            this.pickupConsumer = pickupConsumer
                ?? throw new ArgumentNullException(nameof(pickupConsumer));
            this.presenter = presenter
                ?? throw new ArgumentNullException(nameof(presenter));
        }

        public bool TryRegisterPosition(
            TerminalDropSourceFactV1 source,
            Stage1PickupSourcePositionV1 position,
            out string diagnostic)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (position == null)
                throw new ArgumentNullException(nameof(position));
            return sourcePositions.Register(
                source.RunStableId,
                source.RunLifecycleGeneration,
                source.SourceEntityStableId,
                source.SourcePlacementStableId,
                position.RoomStableId,
                position.Position,
                position.Fingerprint,
                out diagnostic);
        }

        public RunPickupRealizationResultV1 Realize(
            PendingTerminalDropAdmissionResultV1 admission)
        {
            return pickupConsumer.Consume(admission);
        }

        public RunPickupPresentationSyncResultV1 Synchronize(
            StableId roomStableId)
        {
            return presenter.Synchronize(roomStableId);
        }
    }
}

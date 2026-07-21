using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    /// <summary>
    /// Typed transaction seam over the existing holdings and BOX authorities.
    /// The port owns no state; it exists so the coordinator can compensate every
    /// mutation and so exception paths can be tested without private-field access.
    /// </summary>
    public interface IStrongboxMissionResultApplicationAuthorityPortV1
    {
        StableId HoldingsAuthorityStableId { get; }
        long HoldingsSequence { get; }

        PlayerHoldingsSnapshotV1 ExportHoldings();
        StrongboxOpeningSnapshotV1 ExportStrongboxes();
        PlayerHoldingsMutationResultV1 AddStrongbox(
            PlayerHoldingsCommandV1 command);
        StrongboxRegistrationResultV1 RegisterStrongbox(
            StrongboxInstanceContextV1 context);
        PlayerHoldingsImportResultV1 ImportHoldings(
            PlayerHoldingsSnapshotV1 snapshot);
        StrongboxOpeningImportResultV1 ImportStrongboxes(
            StrongboxOpeningSnapshotV1 snapshot);
    }

    public sealed class ExistingStrongboxMissionResultApplicationAuthorityPortV1 :
        IStrongboxMissionResultApplicationAuthorityPortV1
    {
        private readonly ProductionCharacterRuntimeGraphV1 graph;

        public ExistingStrongboxMissionResultApplicationAuthorityPortV1(
            ProductionCharacterRuntimeGraphV1 graph)
        {
            this.graph = graph
                ?? throw new ArgumentNullException(nameof(graph));
        }

        public StableId HoldingsAuthorityStableId
        {
            get { return graph.LoadoutRuntime.Holdings.AuthorityStableId; }
        }

        public long HoldingsSequence
        {
            get { return graph.LoadoutRuntime.Holdings.Sequence; }
        }

        public PlayerHoldingsSnapshotV1 ExportHoldings()
        {
            return graph.LoadoutRuntime.Holdings.ExportSnapshot();
        }

        public StrongboxOpeningSnapshotV1 ExportStrongboxes()
        {
            return graph.StrongboxAuthority.ExportSnapshot();
        }

        public PlayerHoldingsMutationResultV1 AddStrongbox(
            PlayerHoldingsCommandV1 command)
        {
            return graph.LoadoutRuntime.Holdings.Apply(command);
        }

        public StrongboxRegistrationResultV1 RegisterStrongbox(
            StrongboxInstanceContextV1 context)
        {
            return graph.StrongboxAuthority.RegisterInstance(context);
        }

        public PlayerHoldingsImportResultV1 ImportHoldings(
            PlayerHoldingsSnapshotV1 snapshot)
        {
            return graph.LoadoutRuntime.Holdings.ImportSnapshot(snapshot);
        }

        public StrongboxOpeningImportResultV1 ImportStrongboxes(
            StrongboxOpeningSnapshotV1 snapshot)
        {
            return graph.StrongboxAuthority.ImportSnapshot(snapshot);
        }
    }
}

using System;
using ShooterMover.Application.Flow.Game;
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
    public interface IStrongboxMissionResultApplicationStatePort
    {
        StableId HoldingsAuthorityStableId { get; }
        long HoldingsSequence { get; }

        PlayerHoldingsSnapshot ExportHoldings();
        StrongboxOpeningSnapshot ExportStrongboxes();
        PlayerHoldingsMutationResult AddStrongbox(
            PlayerHoldingsCommand command);
        StrongboxRegistrationResult RegisterStrongbox(
            StrongboxInstanceContext context);
        PlayerHoldingsImportResult ImportHoldings(
            PlayerHoldingsSnapshot snapshot);
        StrongboxOpeningImportResult ImportStrongboxes(
            StrongboxOpeningSnapshot snapshot);
    }

    public sealed class ExistingStrongboxMissionResultApplicationStatePort :
        IStrongboxMissionResultApplicationStatePort
    {
        private readonly CharacterLiveGraph graph;

        public ExistingStrongboxMissionResultApplicationStatePort(
            CharacterLiveGraph graph)
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

        public PlayerHoldingsSnapshot ExportHoldings()
        {
            return graph.LoadoutRuntime.Holdings.ExportSnapshot();
        }

        public StrongboxOpeningSnapshot ExportStrongboxes()
        {
            return graph.StrongboxAuthority.ExportSnapshot();
        }

        public PlayerHoldingsMutationResult AddStrongbox(
            PlayerHoldingsCommand command)
        {
            return graph.LoadoutRuntime.Holdings.Apply(command);
        }

        public StrongboxRegistrationResult RegisterStrongbox(
            StrongboxInstanceContext context)
        {
            return graph.StrongboxAuthority.RegisterInstance(context);
        }

        public PlayerHoldingsImportResult ImportHoldings(
            PlayerHoldingsSnapshot snapshot)
        {
            return graph.LoadoutRuntime.Holdings.ImportSnapshot(snapshot);
        }

        public StrongboxOpeningImportResult ImportStrongboxes(
            StrongboxOpeningSnapshot snapshot)
        {
            return graph.StrongboxAuthority.ImportSnapshot(snapshot);
        }
    }
}

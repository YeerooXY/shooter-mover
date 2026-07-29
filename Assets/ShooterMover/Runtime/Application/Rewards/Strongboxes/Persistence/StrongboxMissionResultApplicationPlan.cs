using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Holdings;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxMissionResultApplicationFlow
    {
        private static bool TryValidateTransferItem(
            MissionRunStrongboxCollection collection,
            IReadOnlyDictionary<StableId, UniqueHoldingSnapshot> sourceHeld,
            IReadOnlyDictionary<StableId, StrongboxInstanceContext>
                sourceContexts,
            IReadOnlyDictionary<StableId, StrongboxOpeningRecordSnapshot>
                sourceOpenings,
            IReadOnlyDictionary<StableId, UniqueHoldingSnapshot> targetHeld,
            IReadOnlyDictionary<StableId, StrongboxInstanceContext>
                targetContexts,
            IReadOnlyDictionary<StableId, StrongboxOpeningRecordSnapshot>
                targetOpenings,
            ICollection<TransferItem> transfers,
            out string rejection)
        {
            rejection = string.Empty;
            UniqueHoldingSnapshot sourceHolding;
            StrongboxInstanceContext sourceContext;
            if (!sourceHeld.TryGetValue(
                    collection.InstanceStableId,
                    out sourceHolding)
                || !sourceContexts.TryGetValue(
                    collection.InstanceStableId,
                    out sourceContext))
            {
                rejection = "box-transfer-source-fact-missing:"
                    + collection.InstanceStableId;
                return false;
            }
            if (sourceHolding.DefinitionStableId
                    != collection.DefinitionStableId
                || sourceHolding.Provenance == null
                || sourceHolding.Provenance.GrantStableId
                    != collection.GrantStableId
                || sourceHolding.Provenance.SourceStableId
                    != collection.SourceStableId
                || sourceContext.TierStableId
                    != collection.DefinitionStableId
                || sourceContext.CollectionProvenanceStableId
                    != collection.GrantStableId
                || sourceContext.SourceContextStableId
                    != collection.SourceStableId)
            {
                rejection = "box-transfer-source-provenance-conflict:"
                    + collection.InstanceStableId;
                return false;
            }
            if (sourceOpenings.ContainsKey(collection.InstanceStableId))
            {
                rejection = "box-transfer-source-opening-conflict:"
                    + collection.InstanceStableId;
                return false;
            }

            UniqueHoldingSnapshot existingHolding;
            if (targetHeld.TryGetValue(
                    collection.InstanceStableId,
                    out existingHolding)
                && (existingHolding.DefinitionStableId
                        != collection.DefinitionStableId
                    || existingHolding.Provenance == null
                    || existingHolding.Provenance.GrantStableId
                        != collection.GrantStableId
                    || existingHolding.Provenance.SourceStableId
                        != collection.SourceStableId))
            {
                rejection = "box-transfer-holdings-provenance-conflict:"
                    + collection.InstanceStableId;
                return false;
            }
            StrongboxInstanceContext existingContext;
            if (targetContexts.TryGetValue(
                    collection.InstanceStableId,
                    out existingContext)
                && !string.Equals(
                    existingContext.Fingerprint,
                    sourceContext.Fingerprint,
                    StringComparison.Ordinal))
            {
                rejection = "box-transfer-registration-conflict:"
                    + collection.InstanceStableId;
                return false;
            }

            StrongboxOpeningRecordSnapshot targetOpening;
            bool opened = targetOpenings.TryGetValue(
                collection.InstanceStableId,
                out targetOpening);
            if (opened
                && (targetOpening.Stage != StrongboxOpeningStage.Opened
                    || targetOpening.TerminalFact == null
                    || existingHolding != null))
            {
                rejection = "box-transfer-target-opening-conflict:"
                    + collection.InstanceStableId;
                return false;
            }

            transfers.Add(new TransferItem(
                collection,
                sourceContext,
                existingHolding != null,
                existingContext != null,
                opened));
            return true;
        }

        private sealed class TransferPlan
        {
            public TransferPlan(
                CharacterLiveGraph graph,
                IStrongboxMissionResultApplicationStatePort authorityPort,
                PlayerAccountSnapshot beforeAccount,
                CharacterInstanceSnapshot beforeCharacter,
                PlayerHoldingsSnapshot beforeHoldings,
                StrongboxOpeningSnapshot beforeStrongboxes,
                IReadOnlyList<TransferItem> transfers)
            {
                Graph = graph ?? throw new ArgumentNullException(nameof(graph));
                AuthorityPort = authorityPort
                    ?? throw new ArgumentNullException(nameof(authorityPort));
                BeforeAccount = beforeAccount
                    ?? throw new ArgumentNullException(nameof(beforeAccount));
                BeforeCharacter = beforeCharacter
                    ?? throw new ArgumentNullException(nameof(beforeCharacter));
                BeforeHoldings = beforeHoldings
                    ?? throw new ArgumentNullException(nameof(beforeHoldings));
                BeforeStrongboxes = beforeStrongboxes
                    ?? throw new ArgumentNullException(nameof(beforeStrongboxes));
                Transfers = transfers
                    ?? throw new ArgumentNullException(nameof(transfers));
            }

            public CharacterLiveGraph Graph { get; }
            public IStrongboxMissionResultApplicationStatePort
                AuthorityPort { get; }
            public PlayerAccountSnapshot BeforeAccount { get; }
            public CharacterInstanceSnapshot BeforeCharacter { get; }
            public PlayerHoldingsSnapshot BeforeHoldings { get; }
            public StrongboxOpeningSnapshot BeforeStrongboxes { get; }
            public IReadOnlyList<TransferItem> Transfers { get; }
        }
    }
}

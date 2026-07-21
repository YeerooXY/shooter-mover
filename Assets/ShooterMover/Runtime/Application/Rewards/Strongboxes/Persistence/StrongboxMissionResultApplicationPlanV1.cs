using System;
using System.Collections.Generic;
using System.Linq;
using ShooterMover.Application.Flow.Production;
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
    public sealed partial class StrongboxMissionResultApplicationCoordinatorV1
    {
        private static bool TryValidateTransferItem(
            MissionRunStrongboxCollectionV1 collection,
            IReadOnlyDictionary<StableId, UniqueHoldingSnapshotV1> sourceHeld,
            IReadOnlyDictionary<StableId, StrongboxInstanceContextV1>
                sourceContexts,
            IReadOnlyDictionary<StableId, StrongboxOpeningRecordSnapshotV1>
                sourceOpenings,
            IReadOnlyDictionary<StableId, UniqueHoldingSnapshotV1> targetHeld,
            IReadOnlyDictionary<StableId, StrongboxInstanceContextV1>
                targetContexts,
            IReadOnlyDictionary<StableId, StrongboxOpeningRecordSnapshotV1>
                targetOpenings,
            ICollection<TransferItem> transfers,
            out string rejection)
        {
            rejection = string.Empty;
            UniqueHoldingSnapshotV1 sourceHolding;
            StrongboxInstanceContextV1 sourceContext;
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

            UniqueHoldingSnapshotV1 existingHolding;
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
            StrongboxInstanceContextV1 existingContext;
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

            StrongboxOpeningRecordSnapshotV1 targetOpening;
            bool opened = targetOpenings.TryGetValue(
                collection.InstanceStableId,
                out targetOpening);
            if (opened
                && (targetOpening.Stage != StrongboxOpeningStageV1.Opened
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
                ProductionCharacterRuntimeGraphV1 graph,
                PlayerHoldingsService holdings,
                StrongboxOpeningServiceV1 strongboxes,
                PlayerHoldingsSnapshotV1 beforeHoldings,
                StrongboxOpeningSnapshotV1 beforeStrongboxes,
                IReadOnlyList<TransferItem> transfers)
            {
                Graph = graph;
                Holdings = holdings;
                Strongboxes = strongboxes;
                BeforeHoldings = beforeHoldings;
                BeforeStrongboxes = beforeStrongboxes;
                Transfers = transfers;
            }

            public ProductionCharacterRuntimeGraphV1 Graph { get; }
            public PlayerHoldingsService Holdings { get; }
            public StrongboxOpeningServiceV1 Strongboxes { get; }
            public PlayerHoldingsSnapshotV1 BeforeHoldings { get; }
            public StrongboxOpeningSnapshotV1 BeforeStrongboxes { get; }
            public IReadOnlyList<TransferItem> Transfers { get; }
        }
    }
}

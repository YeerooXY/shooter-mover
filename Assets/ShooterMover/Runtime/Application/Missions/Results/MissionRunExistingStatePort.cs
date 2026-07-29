using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Missions.Results
{
    /// <summary>
    /// Read-only adapter over INV-001 and BOX-001. PICK/RAP collection provenance is
    /// verified from the immutable holding created by their normal application path.
    /// </summary>
    public sealed class MissionRunExistingStatePort : IMissionRunExistingStatePort
    {
        private readonly IPlayerHoldingsState holdingsAuthority;
        private readonly Func<StrongboxOpeningSnapshot> strongboxSnapshotExporter;

        public MissionRunExistingStatePort(
            IPlayerHoldingsState holdingsAuthority,
            Func<StrongboxOpeningSnapshot> strongboxSnapshotExporter)
        {
            this.holdingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            this.strongboxSnapshotExporter = strongboxSnapshotExporter
                ?? throw new ArgumentNullException(nameof(strongboxSnapshotExporter));
        }

        public MissionRunCollectionVerification VerifyCollectedStrongbox(
            MissionRunCollectStrongboxCommand command)
        {
            if (command == null)
            {
                return MissionRunCollectionVerification.Reject("run-collection-command-null");
            }

            PlayerHoldingsSnapshot holdings = holdingsAuthority.ExportSnapshot();
            if (holdings == null)
            {
                return MissionRunCollectionVerification.Reject("run-holdings-snapshot-null");
            }
            if (holdingsAuthority.Sequence != command.ExpectedHoldingsSequence)
            {
                return MissionRunCollectionVerification.Reject("run-holdings-sequence-stale");
            }
            if (!string.Equals(holdings.Fingerprint, command.ExpectedHoldingsFingerprint, StringComparison.Ordinal))
            {
                return MissionRunCollectionVerification.Reject("run-holdings-fingerprint-stale");
            }

            UniqueHoldingSnapshot holding = FindStrongbox(holdings, command.InstanceStableId);
            if (holding == null)
            {
                return MissionRunCollectionVerification.Reject("run-strongbox-not-owned");
            }
            if (holding.DefinitionStableId != command.DefinitionStableId)
            {
                return MissionRunCollectionVerification.Reject("run-strongbox-definition-mismatch");
            }
            if (holding.Provenance.GrantStableId != command.GrantStableId
                || holding.Provenance.SourceStableId != command.SourceStableId)
            {
                return MissionRunCollectionVerification.Reject("run-strongbox-provenance-mismatch");
            }

            return MissionRunCollectionVerification.Accept(
                new MissionRunStrongboxCollection(
                    command.DefinitionStableId,
                    command.InstanceStableId,
                    command.GrantStableId,
                    command.SourceStableId,
                    command.OperationStableId,
                    holdingsAuthority.Sequence,
                    holdings.Fingerprint));
        }

        public MissionRunStrongboxView ProjectStrongboxStates(
            EndMissionRunCommand command,
            IReadOnlyList<MissionRunStrongboxCollection> collectedStrongboxes)
        {
            if (command == null || collectedStrongboxes == null)
            {
                return MissionRunStrongboxView.Reject("run-projection-input-null");
            }

            PlayerHoldingsSnapshot holdings = holdingsAuthority.ExportSnapshot();
            StrongboxOpeningSnapshot openings = strongboxSnapshotExporter();
            if (holdings == null || openings == null)
            {
                return MissionRunStrongboxView.Reject("run-external-snapshot-null");
            }
            if (holdingsAuthority.Sequence != command.ExpectedHoldingsSequence)
            {
                return MissionRunStrongboxView.Reject("run-holdings-sequence-stale");
            }
            if (!string.Equals(holdings.Fingerprint, command.ExpectedHoldingsFingerprint, StringComparison.Ordinal))
            {
                return MissionRunStrongboxView.Reject("run-holdings-fingerprint-stale");
            }
            if (openings.Sequence != command.ExpectedStrongboxOpeningSequence)
            {
                return MissionRunStrongboxView.Reject("run-box-opening-sequence-stale");
            }
            if (!string.Equals(openings.Fingerprint, command.ExpectedStrongboxOpeningFingerprint, StringComparison.Ordinal))
            {
                return MissionRunStrongboxView.Reject("run-box-opening-fingerprint-stale");
            }

            List<MissionRunStrongboxResult> results =
                new List<MissionRunStrongboxResult>(collectedStrongboxes.Count);
            for (int index = 0; index < collectedStrongboxes.Count; index++)
            {
                MissionRunStrongboxCollection collection = collectedStrongboxes[index];
                if (collection == null)
                {
                    return MissionRunStrongboxView.Reject("run-collection-null");
                }

                UniqueHoldingSnapshot owned = FindStrongbox(holdings, collection.InstanceStableId);
                StrongboxOpeningRecordSnapshot opened = FindOpenedStrongbox(
                    openings,
                    command.RunStableId,
                    collection.InstanceStableId);
                if (owned != null && opened != null)
                {
                    return MissionRunStrongboxView.Reject("run-strongbox-owned-and-opened-conflict");
                }
                if (owned == null && opened == null)
                {
                    return MissionRunStrongboxView.Reject("run-strongbox-missing-from-authorities");
                }

                if (owned != null)
                {
                    if (owned.DefinitionStableId != collection.DefinitionStableId
                        || owned.Provenance.GrantStableId != collection.GrantStableId
                        || owned.Provenance.SourceStableId != collection.SourceStableId)
                    {
                        return MissionRunStrongboxView.Reject("run-unopened-strongbox-mismatch");
                    }
                    results.Add(new MissionRunStrongboxResult(
                        collection,
                        MissionRunStrongboxState.Unopened,
                        null,
                        null));
                }
                else
                {
                    results.Add(new MissionRunStrongboxResult(
                        collection,
                        MissionRunStrongboxState.Opened,
                        opened.Command.OpeningStableId,
                        opened.TerminalFact.Fingerprint));
                }
            }

            results.Sort();
            return MissionRunStrongboxView.Accept(
                results,
                holdingsAuthority.Sequence,
                holdings.Fingerprint,
                openings.Sequence,
                openings.Fingerprint);
        }

        private static UniqueHoldingSnapshot FindStrongbox(
            PlayerHoldingsSnapshot snapshot,
            StableId instanceStableId)
        {
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshot holding = snapshot.UniqueHoldings[index];
                if (holding.RewardKind == RewardGrantKind.Strongbox
                    && holding.InstanceStableId == instanceStableId)
                {
                    return holding;
                }
            }
            return null;
        }

        private static StrongboxOpeningRecordSnapshot FindOpenedStrongbox(
            StrongboxOpeningSnapshot snapshot,
            StableId runStableId,
            StableId instanceStableId)
        {
            StrongboxOpeningRecordSnapshot match = null;
            for (int index = 0; index < snapshot.Openings.Count; index++)
            {
                StrongboxOpeningRecordSnapshot record = snapshot.Openings[index];
                if (record.Command.RunStableId != runStableId
                    || record.Command.StrongboxInstanceStableId != instanceStableId
                    || record.Stage != StrongboxOpeningStage.Opened
                    || record.TerminalFact == null)
                {
                    continue;
                }
                if (match != null)
                {
                    return null;
                }
                match = record;
            }
            return match;
        }
    }
}

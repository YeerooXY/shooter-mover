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
    public sealed class MissionRunExistingAuthorityPortV1 : IMissionRunExistingAuthorityPortV1
    {
        private readonly IPlayerHoldingsAuthorityV1 holdingsAuthority;
        private readonly Func<StrongboxOpeningSnapshotV1> strongboxSnapshotExporter;

        public MissionRunExistingAuthorityPortV1(
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            Func<StrongboxOpeningSnapshotV1> strongboxSnapshotExporter)
        {
            this.holdingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            this.strongboxSnapshotExporter = strongboxSnapshotExporter
                ?? throw new ArgumentNullException(nameof(strongboxSnapshotExporter));
        }

        public MissionRunCollectionVerificationV1 VerifyCollectedStrongbox(
            MissionRunCollectStrongboxCommandV1 command)
        {
            if (command == null)
            {
                return MissionRunCollectionVerificationV1.Reject("run-collection-command-null");
            }

            PlayerHoldingsSnapshotV1 holdings = holdingsAuthority.ExportSnapshot();
            if (holdings == null)
            {
                return MissionRunCollectionVerificationV1.Reject("run-holdings-snapshot-null");
            }
            if (holdingsAuthority.Sequence != command.ExpectedHoldingsSequence)
            {
                return MissionRunCollectionVerificationV1.Reject("run-holdings-sequence-stale");
            }
            if (!string.Equals(holdings.Fingerprint, command.ExpectedHoldingsFingerprint, StringComparison.Ordinal))
            {
                return MissionRunCollectionVerificationV1.Reject("run-holdings-fingerprint-stale");
            }

            UniqueHoldingSnapshotV1 holding = FindStrongbox(holdings, command.InstanceStableId);
            if (holding == null)
            {
                return MissionRunCollectionVerificationV1.Reject("run-strongbox-not-owned");
            }
            if (holding.DefinitionStableId != command.DefinitionStableId)
            {
                return MissionRunCollectionVerificationV1.Reject("run-strongbox-definition-mismatch");
            }
            if (holding.Provenance.GrantStableId != command.GrantStableId
                || holding.Provenance.SourceStableId != command.SourceStableId)
            {
                return MissionRunCollectionVerificationV1.Reject("run-strongbox-provenance-mismatch");
            }

            return MissionRunCollectionVerificationV1.Accept(
                new MissionRunStrongboxCollectionV1(
                    command.DefinitionStableId,
                    command.InstanceStableId,
                    command.GrantStableId,
                    command.SourceStableId,
                    command.OperationStableId,
                    holdingsAuthority.Sequence,
                    holdings.Fingerprint));
        }

        public MissionRunStrongboxProjectionV1 ProjectStrongboxStates(
            EndMissionRunCommandV1 command,
            IReadOnlyList<MissionRunStrongboxCollectionV1> collectedStrongboxes)
        {
            if (command == null || collectedStrongboxes == null)
            {
                return MissionRunStrongboxProjectionV1.Reject("run-projection-input-null");
            }

            PlayerHoldingsSnapshotV1 holdings = holdingsAuthority.ExportSnapshot();
            StrongboxOpeningSnapshotV1 openings = strongboxSnapshotExporter();
            if (holdings == null || openings == null)
            {
                return MissionRunStrongboxProjectionV1.Reject("run-external-snapshot-null");
            }
            if (holdingsAuthority.Sequence != command.ExpectedHoldingsSequence)
            {
                return MissionRunStrongboxProjectionV1.Reject("run-holdings-sequence-stale");
            }
            if (!string.Equals(holdings.Fingerprint, command.ExpectedHoldingsFingerprint, StringComparison.Ordinal))
            {
                return MissionRunStrongboxProjectionV1.Reject("run-holdings-fingerprint-stale");
            }
            if (openings.Sequence != command.ExpectedStrongboxOpeningSequence)
            {
                return MissionRunStrongboxProjectionV1.Reject("run-box-opening-sequence-stale");
            }
            if (!string.Equals(openings.Fingerprint, command.ExpectedStrongboxOpeningFingerprint, StringComparison.Ordinal))
            {
                return MissionRunStrongboxProjectionV1.Reject("run-box-opening-fingerprint-stale");
            }

            List<MissionRunStrongboxResultV1> results =
                new List<MissionRunStrongboxResultV1>(collectedStrongboxes.Count);
            for (int index = 0; index < collectedStrongboxes.Count; index++)
            {
                MissionRunStrongboxCollectionV1 collection = collectedStrongboxes[index];
                if (collection == null)
                {
                    return MissionRunStrongboxProjectionV1.Reject("run-collection-null");
                }

                UniqueHoldingSnapshotV1 owned = FindStrongbox(holdings, collection.InstanceStableId);
                StrongboxOpeningRecordSnapshotV1 opened = FindOpenedStrongbox(
                    openings,
                    command.RunStableId,
                    collection.InstanceStableId);
                if (owned != null && opened != null)
                {
                    return MissionRunStrongboxProjectionV1.Reject("run-strongbox-owned-and-opened-conflict");
                }
                if (owned == null && opened == null)
                {
                    return MissionRunStrongboxProjectionV1.Reject("run-strongbox-missing-from-authorities");
                }

                if (owned != null)
                {
                    if (owned.DefinitionStableId != collection.DefinitionStableId
                        || owned.Provenance.GrantStableId != collection.GrantStableId
                        || owned.Provenance.SourceStableId != collection.SourceStableId)
                    {
                        return MissionRunStrongboxProjectionV1.Reject("run-unopened-strongbox-mismatch");
                    }
                    results.Add(new MissionRunStrongboxResultV1(
                        collection,
                        MissionRunStrongboxStateV1.Unopened,
                        null,
                        null));
                }
                else
                {
                    results.Add(new MissionRunStrongboxResultV1(
                        collection,
                        MissionRunStrongboxStateV1.Opened,
                        opened.Command.OpeningStableId,
                        opened.TerminalFact.Fingerprint));
                }
            }

            results.Sort();
            return MissionRunStrongboxProjectionV1.Accept(
                results,
                holdingsAuthority.Sequence,
                holdings.Fingerprint,
                openings.Sequence,
                openings.Fingerprint);
        }

        private static UniqueHoldingSnapshotV1 FindStrongbox(
            PlayerHoldingsSnapshotV1 snapshot,
            StableId instanceStableId)
        {
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 holding = snapshot.UniqueHoldings[index];
                if (holding.RewardKind == RewardGrantKindV1.Strongbox
                    && holding.InstanceStableId == instanceStableId)
                {
                    return holding;
                }
            }
            return null;
        }

        private static StrongboxOpeningRecordSnapshotV1 FindOpenedStrongbox(
            StrongboxOpeningSnapshotV1 snapshot,
            StableId runStableId,
            StableId instanceStableId)
        {
            StrongboxOpeningRecordSnapshotV1 match = null;
            for (int index = 0; index < snapshot.Openings.Count; index++)
            {
                StrongboxOpeningRecordSnapshotV1 record = snapshot.Openings[index];
                if (record.Command.RunStableId != runStableId
                    || record.Command.StrongboxInstanceStableId != instanceStableId
                    || record.Stage != StrongboxOpeningStageV1.Opened
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

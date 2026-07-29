using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Missions.Results;

namespace ShooterMover.Application.Missions.Results
{
    public sealed class MissionRunCollectionVerification
    {
        private MissionRunCollectionVerification(
            bool accepted,
            MissionRunStrongboxCollection collection,
            string rejectionCode)
        {
            Accepted = accepted;
            Collection = collection;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Accepted { get; }
        public MissionRunStrongboxCollection Collection { get; }
        public string RejectionCode { get; }

        public static MissionRunCollectionVerification Accept(MissionRunStrongboxCollection collection)
        {
            return new MissionRunCollectionVerification(
                true,
                collection ?? throw new ArgumentNullException(nameof(collection)),
                string.Empty);
        }

        public static MissionRunCollectionVerification Reject(string rejectionCode)
        {
            return new MissionRunCollectionVerification(false, null, rejectionCode ?? "run-collection-rejected");
        }
    }

    public sealed class MissionRunStrongboxView
    {
        private readonly ReadOnlyCollection<MissionRunStrongboxResult> strongboxes;

        private MissionRunStrongboxView(
            bool accepted,
            IEnumerable<MissionRunStrongboxResult> strongboxes,
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint,
            string rejectionCode)
        {
            Accepted = accepted;
            this.strongboxes = new ReadOnlyCollection<MissionRunStrongboxResult>(
                new List<MissionRunStrongboxResult>(
                    strongboxes ?? Array.Empty<MissionRunStrongboxResult>()));
            HoldingsSequence = holdingsSequence;
            HoldingsFingerprint = holdingsFingerprint ?? string.Empty;
            StrongboxOpeningSequence = strongboxOpeningSequence;
            StrongboxOpeningFingerprint = strongboxOpeningFingerprint ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Accepted { get; }
        public IReadOnlyList<MissionRunStrongboxResult> Strongboxes { get { return strongboxes; } }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public long StrongboxOpeningSequence { get; }
        public string StrongboxOpeningFingerprint { get; }
        public string RejectionCode { get; }

        public static MissionRunStrongboxView Accept(
            IEnumerable<MissionRunStrongboxResult> strongboxes,
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint)
        {
            return new MissionRunStrongboxView(
                true,
                strongboxes,
                holdingsSequence,
                holdingsFingerprint,
                strongboxOpeningSequence,
                strongboxOpeningFingerprint,
                string.Empty);
        }

        public static MissionRunStrongboxView Reject(string rejectionCode)
        {
            return new MissionRunStrongboxView(
                false,
                Array.Empty<MissionRunStrongboxResult>(),
                0L,
                string.Empty,
                0L,
                string.Empty,
                rejectionCode ?? "run-projection-rejected");
        }
    }

    /// <summary>
    /// Read-only composition boundary over the existing PICK/RAP/INV and BOX authorities.
    /// RUN never grants, consumes, opens, rerolls, or mutates through this port.
    /// </summary>
    public interface IMissionRunExistingStatePort
    {
        MissionRunCollectionVerification VerifyCollectedStrongbox(
            MissionRunCollectStrongboxCommand command);

        MissionRunStrongboxView ProjectStrongboxStates(
            EndMissionRunCommand command,
            IReadOnlyList<MissionRunStrongboxCollection> collectedStrongboxes);
    }
}

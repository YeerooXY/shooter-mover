using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Missions.Results;

namespace ShooterMover.Application.Missions.Results
{
    public sealed class MissionRunCollectionVerificationV1
    {
        private MissionRunCollectionVerificationV1(
            bool accepted,
            MissionRunStrongboxCollectionV1 collection,
            string rejectionCode)
        {
            Accepted = accepted;
            Collection = collection;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Accepted { get; }
        public MissionRunStrongboxCollectionV1 Collection { get; }
        public string RejectionCode { get; }

        public static MissionRunCollectionVerificationV1 Accept(MissionRunStrongboxCollectionV1 collection)
        {
            return new MissionRunCollectionVerificationV1(
                true,
                collection ?? throw new ArgumentNullException(nameof(collection)),
                string.Empty);
        }

        public static MissionRunCollectionVerificationV1 Reject(string rejectionCode)
        {
            return new MissionRunCollectionVerificationV1(false, null, rejectionCode ?? "run-collection-rejected");
        }
    }

    public sealed class MissionRunStrongboxProjectionV1
    {
        private readonly ReadOnlyCollection<MissionRunStrongboxResultV1> strongboxes;

        private MissionRunStrongboxProjectionV1(
            bool accepted,
            IEnumerable<MissionRunStrongboxResultV1> strongboxes,
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint,
            string rejectionCode)
        {
            Accepted = accepted;
            this.strongboxes = new ReadOnlyCollection<MissionRunStrongboxResultV1>(
                new List<MissionRunStrongboxResultV1>(
                    strongboxes ?? Array.Empty<MissionRunStrongboxResultV1>()));
            HoldingsSequence = holdingsSequence;
            HoldingsFingerprint = holdingsFingerprint ?? string.Empty;
            StrongboxOpeningSequence = strongboxOpeningSequence;
            StrongboxOpeningFingerprint = strongboxOpeningFingerprint ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Accepted { get; }
        public IReadOnlyList<MissionRunStrongboxResultV1> Strongboxes { get { return strongboxes; } }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public long StrongboxOpeningSequence { get; }
        public string StrongboxOpeningFingerprint { get; }
        public string RejectionCode { get; }

        public static MissionRunStrongboxProjectionV1 Accept(
            IEnumerable<MissionRunStrongboxResultV1> strongboxes,
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint)
        {
            return new MissionRunStrongboxProjectionV1(
                true,
                strongboxes,
                holdingsSequence,
                holdingsFingerprint,
                strongboxOpeningSequence,
                strongboxOpeningFingerprint,
                string.Empty);
        }

        public static MissionRunStrongboxProjectionV1 Reject(string rejectionCode)
        {
            return new MissionRunStrongboxProjectionV1(
                false,
                Array.Empty<MissionRunStrongboxResultV1>(),
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
    public interface IMissionRunExistingAuthorityPortV1
    {
        MissionRunCollectionVerificationV1 VerifyCollectedStrongbox(
            MissionRunCollectStrongboxCommandV1 command);

        MissionRunStrongboxProjectionV1 ProjectStrongboxStates(
            EndMissionRunCommandV1 command,
            IReadOnlyList<MissionRunStrongboxCollectionV1> collectedStrongboxes);
    }
}

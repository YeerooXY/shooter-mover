using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Results
{
    public sealed class MissionRunPayloadV1 : IEquatable<MissionRunPayloadV1>
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentContractStableIdText = "mission-run.run-v1";

        private readonly ReadOnlyCollection<MissionRunStrongboxCollectionV1> collectedStrongboxes;
        private readonly string canonicalText;

        private MissionRunPayloadV1(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            IEnumerable<MissionRunStrongboxCollectionV1> collectedStrongboxes,
            long runSequence)
        {
            SchemaVersion = CurrentSchemaVersion;
            ContractStableId = StableId.Parse(CurrentContractStableIdText);
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoutePayload = routePayload ?? throw new ArgumentNullException(nameof(routePayload));
            if (!RoutePayload.HasValidFingerprint())
            {
                throw new ArgumentException("Route payload fingerprint is invalid.", nameof(routePayload));
            }
            if (runSequence < 0L) throw new ArgumentOutOfRangeException(nameof(runSequence));

            List<MissionRunStrongboxCollectionV1> ordered =
                new List<MissionRunStrongboxCollectionV1>(
                    collectedStrongboxes ?? throw new ArgumentNullException(nameof(collectedStrongboxes)));
            ordered.Sort();
            HashSet<StableId> seen = new HashSet<StableId>();
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRunStrongboxCollectionV1 value = ordered[index];
                if (value == null)
                {
                    throw new ArgumentException("Collected strongboxes cannot contain null.", nameof(collectedStrongboxes));
                }
                if (!seen.Add(value.InstanceStableId))
                {
                    throw new ArgumentException("Collected strongbox instance identities must be unique.", nameof(collectedStrongboxes));
                }
            }

            this.collectedStrongboxes =
                new ReadOnlyCollection<MissionRunStrongboxCollectionV1>(ordered);
            RunSequence = runSequence;

            StringBuilder builder = new StringBuilder();
            MissionRunCanonicalV1.AppendToken(builder, "schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "contract_stable_id", ContractStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRunCanonicalV1.AppendToken(builder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRunCanonicalV1.AppendToken(builder, "collected_strongbox_count", ordered.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRunCanonicalV1.AppendToken(
                    builder,
                    "collected_strongbox_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    ordered[index].ToCanonicalString());
            }
            MissionRunCanonicalV1.AppendToken(builder, "run_sequence", RunSequence.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = MissionRunCanonicalV1.Fingerprint(canonicalText);
        }

        public int SchemaVersion { get; }
        public StableId ContractStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public IReadOnlyList<MissionRunStrongboxCollectionV1> CollectedStrongboxes
        {
            get { return collectedStrongboxes; }
        }
        public long RunSequence { get; }
        public string Fingerprint { get; }

        public static MissionRunPayloadV1 Create(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            IEnumerable<MissionRunStrongboxCollectionV1> collectedStrongboxes,
            long runSequence)
        {
            return new MissionRunPayloadV1(
                runStableId,
                routePayload,
                collectedStrongboxes,
                runSequence);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(MissionRunPayloadV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as MissionRunPayloadV1); }
        public override int GetHashCode() { return MissionRunCanonicalV1.DeterministicHash(canonicalText); }
    }

    public sealed class MissionResultPayloadV1 : IEquatable<MissionResultPayloadV1>
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentContractStableIdText = "mission-result.run-v1";

        private readonly ReadOnlyCollection<MissionRunStrongboxResultV1> strongboxes;
        private readonly ReadOnlyCollection<MissionRunStrongboxResultV1> unopenedStrongboxes;
        private readonly ReadOnlyCollection<MissionRunStrongboxResultV1> openedStrongboxes;
        private readonly string canonicalText;

        private MissionResultPayloadV1(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            MissionRunCompletionStateV1 completionState,
            IEnumerable<MissionRunStrongboxResultV1> strongboxes,
            long runSequence,
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint)
        {
            SchemaVersion = CurrentSchemaVersion;
            ContractStableId = StableId.Parse(CurrentContractStableIdText);
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoutePayload = routePayload ?? throw new ArgumentNullException(nameof(routePayload));
            if (!RoutePayload.HasValidFingerprint()) throw new ArgumentException("Route payload fingerprint is invalid.", nameof(routePayload));
            if (!Enum.IsDefined(typeof(MissionRunCompletionStateV1), completionState))
            {
                throw new ArgumentOutOfRangeException(nameof(completionState));
            }
            if (runSequence < 0L) throw new ArgumentOutOfRangeException(nameof(runSequence));
            if (holdingsSequence < 0L) throw new ArgumentOutOfRangeException(nameof(holdingsSequence));
            if (strongboxOpeningSequence < 0L) throw new ArgumentOutOfRangeException(nameof(strongboxOpeningSequence));
            if (!MissionRunCanonicalV1.IsFingerprint(holdingsFingerprint))
            {
                throw new ArgumentException("Holdings fingerprint must be canonical.", nameof(holdingsFingerprint));
            }
            if (!MissionRunCanonicalV1.IsFingerprint(strongboxOpeningFingerprint))
            {
                throw new ArgumentException("Strongbox-opening fingerprint must be canonical.", nameof(strongboxOpeningFingerprint));
            }

            List<MissionRunStrongboxResultV1> ordered = new List<MissionRunStrongboxResultV1>(
                strongboxes ?? throw new ArgumentNullException(nameof(strongboxes)));
            ordered.Sort();
            HashSet<StableId> seen = new HashSet<StableId>();
            List<MissionRunStrongboxResultV1> unopened = new List<MissionRunStrongboxResultV1>();
            List<MissionRunStrongboxResultV1> opened = new List<MissionRunStrongboxResultV1>();
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRunStrongboxResultV1 value = ordered[index];
                if (value == null) throw new ArgumentException("Strongbox results cannot contain null.", nameof(strongboxes));
                if (!seen.Add(value.InstanceStableId))
                {
                    throw new ArgumentException("Strongbox instance identities must be unique.", nameof(strongboxes));
                }
                if (value.IsUnopened) unopened.Add(value); else opened.Add(value);
            }

            CompletionState = completionState;
            this.strongboxes = new ReadOnlyCollection<MissionRunStrongboxResultV1>(ordered);
            unopenedStrongboxes = new ReadOnlyCollection<MissionRunStrongboxResultV1>(unopened);
            openedStrongboxes = new ReadOnlyCollection<MissionRunStrongboxResultV1>(opened);
            RunSequence = runSequence;
            HoldingsSequence = holdingsSequence;
            HoldingsFingerprint = holdingsFingerprint;
            StrongboxOpeningSequence = strongboxOpeningSequence;
            StrongboxOpeningFingerprint = strongboxOpeningFingerprint;

            StringBuilder builder = new StringBuilder();
            MissionRunCanonicalV1.AppendToken(builder, "schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "contract_stable_id", ContractStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRunCanonicalV1.AppendToken(builder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRunCanonicalV1.AppendToken(builder, "completion_state", ((int)CompletionState).ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "strongbox_count", ordered.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRunCanonicalV1.AppendToken(builder, "strongbox_" + index.ToString("D4", CultureInfo.InvariantCulture), ordered[index].ToCanonicalString());
            }
            MissionRunCanonicalV1.AppendToken(builder, "run_sequence", RunSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "holdings_sequence", HoldingsSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "holdings_fingerprint", HoldingsFingerprint);
            MissionRunCanonicalV1.AppendToken(builder, "strongbox_opening_sequence", StrongboxOpeningSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "strongbox_opening_fingerprint", StrongboxOpeningFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = MissionRunCanonicalV1.Fingerprint(canonicalText);
        }

        public int SchemaVersion { get; }
        public StableId ContractStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public MissionRunCompletionStateV1 CompletionState { get; }
        public IReadOnlyList<MissionRunStrongboxResultV1> Strongboxes { get { return strongboxes; } }
        public IReadOnlyList<MissionRunStrongboxResultV1> UnopenedStrongboxes { get { return unopenedStrongboxes; } }
        public IReadOnlyList<MissionRunStrongboxResultV1> OpenedStrongboxes { get { return openedStrongboxes; } }
        public long RunSequence { get; }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public long StrongboxOpeningSequence { get; }
        public string StrongboxOpeningFingerprint { get; }
        public string Fingerprint { get; }

        public static MissionResultPayloadV1 Create(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            MissionRunCompletionStateV1 completionState,
            IEnumerable<MissionRunStrongboxResultV1> strongboxes,
            long runSequence,
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint)
        {
            return new MissionResultPayloadV1(
                runStableId,
                routePayload,
                completionState,
                strongboxes,
                runSequence,
                holdingsSequence,
                holdingsFingerprint,
                strongboxOpeningSequence,
                strongboxOpeningFingerprint);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(MissionResultPayloadV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as MissionResultPayloadV1); }
        public override int GetHashCode() { return MissionRunCanonicalV1.DeterministicHash(canonicalText); }
    }

    public sealed class MissionRunAuthorityResultV1
    {
        public MissionRunAuthorityResultV1(
            MissionRunAuthorityStatusV1 status,
            long previousSequence,
            long currentSequence,
            StableId operationStableId,
            string requestFingerprint,
            MissionRunPayloadV1 runPayload,
            MissionRunStrongboxCollectionV1 collection,
            MissionResultPayloadV1 resultPayload,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(MissionRunAuthorityStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (previousSequence < 0L) throw new ArgumentOutOfRangeException(nameof(previousSequence));
            if (currentSequence < previousSequence) throw new ArgumentOutOfRangeException(nameof(currentSequence));
            Status = status;
            PreviousSequence = previousSequence;
            CurrentSequence = currentSequence;
            OperationStableId = operationStableId;
            RequestFingerprint = requestFingerprint ?? string.Empty;
            RunPayload = runPayload;
            Collection = collection;
            ResultPayload = resultPayload;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public MissionRunAuthorityStatusV1 Status { get; }
        public long PreviousSequence { get; }
        public long CurrentSequence { get; }
        public StableId OperationStableId { get; }
        public string RequestFingerprint { get; }
        public MissionRunPayloadV1 RunPayload { get; }
        public MissionRunStrongboxCollectionV1 Collection { get; }
        public MissionResultPayloadV1 ResultPayload { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == MissionRunAuthorityStatusV1.StrongboxCollected
                    || Status == MissionRunAuthorityStatusV1.RunEnded
                    || Status == MissionRunAuthorityStatusV1.ExactDuplicateNoChange;
            }
        }
    }
}

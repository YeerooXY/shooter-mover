using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Results
{
    public sealed class MissionRunPayload : IEquatable<MissionRunPayload>
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentContractStableIdText = "mission-run.run-v1";

        private readonly ReadOnlyCollection<MissionRunStrongboxCollection> collectedStrongboxes;
        private readonly string canonicalText;

        private MissionRunPayload(
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            IEnumerable<MissionRunStrongboxCollection> collectedStrongboxes,
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

            List<MissionRunStrongboxCollection> ordered =
                new List<MissionRunStrongboxCollection>(
                    collectedStrongboxes ?? throw new ArgumentNullException(nameof(collectedStrongboxes)));
            ordered.Sort();
            HashSet<StableId> seen = new HashSet<StableId>();
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRunStrongboxCollection value = ordered[index];
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
                new ReadOnlyCollection<MissionRunStrongboxCollection>(ordered);
            RunSequence = runSequence;

            StringBuilder builder = new StringBuilder();
            MissionRun.AppendToken(builder, "schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            MissionRun.AppendToken(builder, "contract_stable_id", ContractStableId.ToString());
            MissionRun.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            MissionRun.AppendToken(builder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRun.AppendToken(builder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRun.AppendToken(builder, "collected_strongbox_count", ordered.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRun.AppendToken(
                    builder,
                    "collected_strongbox_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    ordered[index].ToCanonicalString());
            }
            MissionRun.AppendToken(builder, "run_sequence", RunSequence.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = MissionRun.Fingerprint(canonicalText);
        }

        public int SchemaVersion { get; }
        public StableId ContractStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public IReadOnlyList<MissionRunStrongboxCollection> CollectedStrongboxes
        {
            get { return collectedStrongboxes; }
        }
        public long RunSequence { get; }
        public string Fingerprint { get; }

        public static MissionRunPayload Create(
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            IEnumerable<MissionRunStrongboxCollection> collectedStrongboxes,
            long runSequence)
        {
            return new MissionRunPayload(
                runStableId,
                routePayload,
                collectedStrongboxes,
                runSequence);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(MissionRunPayload other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as MissionRunPayload); }
        public override int GetHashCode() { return MissionRun.DeterministicHash(canonicalText); }
    }

    public sealed class MissionResultPayload : IEquatable<MissionResultPayload>
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentContractStableIdText = "mission-result.run-v1";

        private readonly ReadOnlyCollection<MissionRunStrongboxResult> strongboxes;
        private readonly ReadOnlyCollection<MissionRunStrongboxResult> unopenedStrongboxes;
        private readonly ReadOnlyCollection<MissionRunStrongboxResult> openedStrongboxes;
        private readonly string canonicalText;

        private MissionResultPayload(
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            MissionRunCompletionState completionState,
            IEnumerable<MissionRunStrongboxResult> strongboxes,
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
            if (!Enum.IsDefined(typeof(MissionRunCompletionState), completionState))
            {
                throw new ArgumentOutOfRangeException(nameof(completionState));
            }
            if (runSequence < 0L) throw new ArgumentOutOfRangeException(nameof(runSequence));
            if (holdingsSequence < 0L) throw new ArgumentOutOfRangeException(nameof(holdingsSequence));
            if (strongboxOpeningSequence < 0L) throw new ArgumentOutOfRangeException(nameof(strongboxOpeningSequence));
            if (!MissionRun.IsFingerprint(holdingsFingerprint))
            {
                throw new ArgumentException("Holdings fingerprint must be canonical.", nameof(holdingsFingerprint));
            }
            if (!MissionRun.IsFingerprint(strongboxOpeningFingerprint))
            {
                throw new ArgumentException("Strongbox-opening fingerprint must be canonical.", nameof(strongboxOpeningFingerprint));
            }

            List<MissionRunStrongboxResult> ordered = new List<MissionRunStrongboxResult>(
                strongboxes ?? throw new ArgumentNullException(nameof(strongboxes)));
            ordered.Sort();
            HashSet<StableId> seen = new HashSet<StableId>();
            List<MissionRunStrongboxResult> unopened = new List<MissionRunStrongboxResult>();
            List<MissionRunStrongboxResult> opened = new List<MissionRunStrongboxResult>();
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRunStrongboxResult value = ordered[index];
                if (value == null) throw new ArgumentException("Strongbox results cannot contain null.", nameof(strongboxes));
                if (!seen.Add(value.InstanceStableId))
                {
                    throw new ArgumentException("Strongbox instance identities must be unique.", nameof(strongboxes));
                }
                if (value.IsUnopened) unopened.Add(value); else opened.Add(value);
            }

            CompletionState = completionState;
            this.strongboxes = new ReadOnlyCollection<MissionRunStrongboxResult>(ordered);
            unopenedStrongboxes = new ReadOnlyCollection<MissionRunStrongboxResult>(unopened);
            openedStrongboxes = new ReadOnlyCollection<MissionRunStrongboxResult>(opened);
            RunSequence = runSequence;
            HoldingsSequence = holdingsSequence;
            HoldingsFingerprint = holdingsFingerprint;
            StrongboxOpeningSequence = strongboxOpeningSequence;
            StrongboxOpeningFingerprint = strongboxOpeningFingerprint;

            StringBuilder builder = new StringBuilder();
            MissionRun.AppendToken(builder, "schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            MissionRun.AppendToken(builder, "contract_stable_id", ContractStableId.ToString());
            MissionRun.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            MissionRun.AppendToken(builder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRun.AppendToken(builder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRun.AppendToken(builder, "completion_state", ((int)CompletionState).ToString(CultureInfo.InvariantCulture));
            MissionRun.AppendToken(builder, "strongbox_count", ordered.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < ordered.Count; index++)
            {
                MissionRun.AppendToken(builder, "strongbox_" + index.ToString("D4", CultureInfo.InvariantCulture), ordered[index].ToCanonicalString());
            }
            MissionRun.AppendToken(builder, "run_sequence", RunSequence.ToString(CultureInfo.InvariantCulture));
            MissionRun.AppendToken(builder, "holdings_sequence", HoldingsSequence.ToString(CultureInfo.InvariantCulture));
            MissionRun.AppendToken(builder, "holdings_fingerprint", HoldingsFingerprint);
            MissionRun.AppendToken(builder, "strongbox_opening_sequence", StrongboxOpeningSequence.ToString(CultureInfo.InvariantCulture));
            MissionRun.AppendToken(builder, "strongbox_opening_fingerprint", StrongboxOpeningFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = MissionRun.Fingerprint(canonicalText);
        }

        public int SchemaVersion { get; }
        public StableId ContractStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public MissionRunCompletionState CompletionState { get; }
        public IReadOnlyList<MissionRunStrongboxResult> Strongboxes { get { return strongboxes; } }
        public IReadOnlyList<MissionRunStrongboxResult> UnopenedStrongboxes { get { return unopenedStrongboxes; } }
        public IReadOnlyList<MissionRunStrongboxResult> OpenedStrongboxes { get { return openedStrongboxes; } }
        public long RunSequence { get; }
        public long HoldingsSequence { get; }
        public string HoldingsFingerprint { get; }
        public long StrongboxOpeningSequence { get; }
        public string StrongboxOpeningFingerprint { get; }
        public string Fingerprint { get; }

        public static MissionResultPayload Create(
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            MissionRunCompletionState completionState,
            IEnumerable<MissionRunStrongboxResult> strongboxes,
            long runSequence,
            long holdingsSequence,
            string holdingsFingerprint,
            long strongboxOpeningSequence,
            string strongboxOpeningFingerprint)
        {
            return new MissionResultPayload(
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
        public bool Equals(MissionResultPayload other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as MissionResultPayload); }
        public override int GetHashCode() { return MissionRun.DeterministicHash(canonicalText); }
    }

    public sealed class MissionRunStateResult
    {
        public MissionRunStateResult(
            MissionRunStateStatus status,
            long previousSequence,
            long currentSequence,
            StableId operationStableId,
            string requestFingerprint,
            MissionRunPayload runPayload,
            MissionRunStrongboxCollection collection,
            MissionResultPayload resultPayload,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(MissionRunStateStatus), status))
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

        public MissionRunStateStatus Status { get; }
        public long PreviousSequence { get; }
        public long CurrentSequence { get; }
        public StableId OperationStableId { get; }
        public string RequestFingerprint { get; }
        public MissionRunPayload RunPayload { get; }
        public MissionRunStrongboxCollection Collection { get; }
        public MissionResultPayload ResultPayload { get; }
        public string RejectionCode { get; }
        public bool Succeeded
        {
            get
            {
                return Status == MissionRunStateStatus.StrongboxCollected
                    || Status == MissionRunStateStatus.RunEnded
                    || Status == MissionRunStateStatus.ExactDuplicateNoChange;
            }
        }
    }
}

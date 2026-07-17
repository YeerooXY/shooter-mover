using System;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Results
{
    public sealed class MissionRunCollectStrongboxCommandV1 : IEquatable<MissionRunCollectStrongboxCommandV1>
    {
        private readonly string canonicalText;

        private MissionRunCollectStrongboxCommandV1(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            StableId definitionStableId,
            StableId instanceStableId,
            StableId grantStableId,
            StableId sourceStableId,
            long expectedRunSequence,
            long expectedHoldingsSequence,
            string expectedHoldingsFingerprint)
        {
            OperationStableId = operationStableId ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoutePayload = routePayload ?? throw new ArgumentNullException(nameof(routePayload));
            if (!RoutePayload.HasValidFingerprint()) throw new ArgumentException("Route payload fingerprint is invalid.", nameof(routePayload));
            DefinitionStableId = definitionStableId ?? throw new ArgumentNullException(nameof(definitionStableId));
            InstanceStableId = instanceStableId ?? throw new ArgumentNullException(nameof(instanceStableId));
            GrantStableId = grantStableId ?? throw new ArgumentNullException(nameof(grantStableId));
            SourceStableId = sourceStableId ?? throw new ArgumentNullException(nameof(sourceStableId));
            if (expectedRunSequence < 0L) throw new ArgumentOutOfRangeException(nameof(expectedRunSequence));
            if (expectedHoldingsSequence < 0L) throw new ArgumentOutOfRangeException(nameof(expectedHoldingsSequence));
            if (!MissionRunCanonicalV1.IsFingerprint(expectedHoldingsFingerprint))
            {
                throw new ArgumentException("Expected holdings fingerprint must be canonical.", nameof(expectedHoldingsFingerprint));
            }
            ExpectedRunSequence = expectedRunSequence;
            ExpectedHoldingsSequence = expectedHoldingsSequence;
            ExpectedHoldingsFingerprint = expectedHoldingsFingerprint;

            StringBuilder builder = new StringBuilder();
            MissionRunCanonicalV1.AppendToken(builder, "operation_stable_id", OperationStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRunCanonicalV1.AppendToken(builder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRunCanonicalV1.AppendToken(builder, "definition_stable_id", DefinitionStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "instance_stable_id", InstanceStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "grant_stable_id", GrantStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "source_stable_id", SourceStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "expected_run_sequence", ExpectedRunSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "expected_holdings_sequence", ExpectedHoldingsSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "expected_holdings_fingerprint", ExpectedHoldingsFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = MissionRunCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public StableId DefinitionStableId { get; }
        public StableId InstanceStableId { get; }
        public StableId GrantStableId { get; }
        public StableId SourceStableId { get; }
        public long ExpectedRunSequence { get; }
        public long ExpectedHoldingsSequence { get; }
        public string ExpectedHoldingsFingerprint { get; }
        public string Fingerprint { get; }

        public static MissionRunCollectStrongboxCommandV1 Create(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            StableId definitionStableId,
            StableId instanceStableId,
            StableId grantStableId,
            StableId sourceStableId,
            long expectedRunSequence,
            long expectedHoldingsSequence,
            string expectedHoldingsFingerprint)
        {
            return new MissionRunCollectStrongboxCommandV1(
                operationStableId,
                runStableId,
                routePayload,
                definitionStableId,
                instanceStableId,
                grantStableId,
                sourceStableId,
                expectedRunSequence,
                expectedHoldingsSequence,
                expectedHoldingsFingerprint);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(MissionRunCollectStrongboxCommandV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as MissionRunCollectStrongboxCommandV1); }
        public override int GetHashCode() { return MissionRunCanonicalV1.DeterministicHash(canonicalText); }
    }

    public sealed class EndMissionRunCommandV1 : IEquatable<EndMissionRunCommandV1>
    {
        private readonly string canonicalText;

        private EndMissionRunCommandV1(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            MissionRunCompletionStateV1 completionState,
            long expectedRunSequence,
            long expectedHoldingsSequence,
            string expectedHoldingsFingerprint,
            long expectedStrongboxOpeningSequence,
            string expectedStrongboxOpeningFingerprint)
        {
            OperationStableId = operationStableId ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoutePayload = routePayload ?? throw new ArgumentNullException(nameof(routePayload));
            if (!RoutePayload.HasValidFingerprint()) throw new ArgumentException("Route payload fingerprint is invalid.", nameof(routePayload));
            if (!Enum.IsDefined(typeof(MissionRunCompletionStateV1), completionState))
            {
                throw new ArgumentOutOfRangeException(nameof(completionState));
            }
            if (expectedRunSequence < 0L) throw new ArgumentOutOfRangeException(nameof(expectedRunSequence));
            if (expectedHoldingsSequence < 0L) throw new ArgumentOutOfRangeException(nameof(expectedHoldingsSequence));
            if (expectedStrongboxOpeningSequence < 0L) throw new ArgumentOutOfRangeException(nameof(expectedStrongboxOpeningSequence));
            if (!MissionRunCanonicalV1.IsFingerprint(expectedHoldingsFingerprint))
            {
                throw new ArgumentException("Expected holdings fingerprint must be canonical.", nameof(expectedHoldingsFingerprint));
            }
            if (!MissionRunCanonicalV1.IsFingerprint(expectedStrongboxOpeningFingerprint))
            {
                throw new ArgumentException("Expected strongbox-opening fingerprint must be canonical.", nameof(expectedStrongboxOpeningFingerprint));
            }
            CompletionState = completionState;
            ExpectedRunSequence = expectedRunSequence;
            ExpectedHoldingsSequence = expectedHoldingsSequence;
            ExpectedHoldingsFingerprint = expectedHoldingsFingerprint;
            ExpectedStrongboxOpeningSequence = expectedStrongboxOpeningSequence;
            ExpectedStrongboxOpeningFingerprint = expectedStrongboxOpeningFingerprint;

            StringBuilder intentBuilder = new StringBuilder();
            MissionRunCanonicalV1.AppendToken(intentBuilder, "run_stable_id", RunStableId.ToString());
            MissionRunCanonicalV1.AppendToken(intentBuilder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRunCanonicalV1.AppendToken(intentBuilder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRunCanonicalV1.AppendToken(intentBuilder, "completion_state", ((int)CompletionState).ToString(CultureInfo.InvariantCulture));
            IntentFingerprint = MissionRunCanonicalV1.Fingerprint(intentBuilder.ToString());

            StringBuilder builder = new StringBuilder();
            MissionRunCanonicalV1.AppendToken(builder, "operation_stable_id", OperationStableId.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "intent", intentBuilder.ToString());
            MissionRunCanonicalV1.AppendToken(builder, "expected_run_sequence", ExpectedRunSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "expected_holdings_sequence", ExpectedHoldingsSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "expected_holdings_fingerprint", ExpectedHoldingsFingerprint);
            MissionRunCanonicalV1.AppendToken(builder, "expected_strongbox_opening_sequence", ExpectedStrongboxOpeningSequence.ToString(CultureInfo.InvariantCulture));
            MissionRunCanonicalV1.AppendToken(builder, "expected_strongbox_opening_fingerprint", ExpectedStrongboxOpeningFingerprint);
            canonicalText = builder.ToString();
            Fingerprint = MissionRunCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public MissionRunCompletionStateV1 CompletionState { get; }
        public long ExpectedRunSequence { get; }
        public long ExpectedHoldingsSequence { get; }
        public string ExpectedHoldingsFingerprint { get; }
        public long ExpectedStrongboxOpeningSequence { get; }
        public string ExpectedStrongboxOpeningFingerprint { get; }
        public string IntentFingerprint { get; }
        public string Fingerprint { get; }

        public static EndMissionRunCommandV1 Create(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            MissionRunCompletionStateV1 completionState,
            long expectedRunSequence,
            long expectedHoldingsSequence,
            string expectedHoldingsFingerprint,
            long expectedStrongboxOpeningSequence,
            string expectedStrongboxOpeningFingerprint)
        {
            return new EndMissionRunCommandV1(
                operationStableId,
                runStableId,
                routePayload,
                completionState,
                expectedRunSequence,
                expectedHoldingsSequence,
                expectedHoldingsFingerprint,
                expectedStrongboxOpeningSequence,
                expectedStrongboxOpeningFingerprint);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(EndMissionRunCommandV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as EndMissionRunCommandV1); }
        public override int GetHashCode() { return MissionRunCanonicalV1.DeterministicHash(canonicalText); }
    }
}

using System;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Missions.Results
{
    public sealed class MissionRunCollectStrongboxCommand : IEquatable<MissionRunCollectStrongboxCommand>
    {
        private readonly string canonicalText;

        private MissionRunCollectStrongboxCommand(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            StableId definitionStableId,
            StableId instanceStableId,
            StableId grantStableId,
            StableId sourceStableId,
            long expectedRunSequence)
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
            ExpectedRunSequence = expectedRunSequence;

            StringBuilder builder = new StringBuilder();
            MissionRun.AppendToken(builder, "operation_stable_id", OperationStableId.ToString());
            MissionRun.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            MissionRun.AppendToken(builder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRun.AppendToken(builder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRun.AppendToken(builder, "definition_stable_id", DefinitionStableId.ToString());
            MissionRun.AppendToken(builder, "instance_stable_id", InstanceStableId.ToString());
            MissionRun.AppendToken(builder, "grant_stable_id", GrantStableId.ToString());
            MissionRun.AppendToken(builder, "source_stable_id", SourceStableId.ToString());
            MissionRun.AppendToken(builder, "expected_run_sequence", ExpectedRunSequence.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = MissionRun.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public StableId DefinitionStableId { get; }
        public StableId InstanceStableId { get; }
        public StableId GrantStableId { get; }
        public StableId SourceStableId { get; }
        public long ExpectedRunSequence { get; }
        public string Fingerprint { get; }

        public static MissionRunCollectStrongboxCommand Create(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            StableId definitionStableId,
            StableId instanceStableId,
            StableId grantStableId,
            StableId sourceStableId,
            long expectedRunSequence)
        {
            return new MissionRunCollectStrongboxCommand(
                operationStableId,
                runStableId,
                routePayload,
                definitionStableId,
                instanceStableId,
                grantStableId,
                sourceStableId,
                expectedRunSequence);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(MissionRunCollectStrongboxCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as MissionRunCollectStrongboxCommand); }
        public override int GetHashCode() { return MissionRun.DeterministicHash(canonicalText); }
    }

    public sealed class EndMissionRunCommand : IEquatable<EndMissionRunCommand>
    {
        private readonly string canonicalText;

        private EndMissionRunCommand(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            MissionRunCompletionState completionState,
            long expectedRunSequence)
        {
            OperationStableId = operationStableId ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoutePayload = routePayload ?? throw new ArgumentNullException(nameof(routePayload));
            if (!RoutePayload.HasValidFingerprint()) throw new ArgumentException("Route payload fingerprint is invalid.", nameof(routePayload));
            if (!Enum.IsDefined(typeof(MissionRunCompletionState), completionState))
            {
                throw new ArgumentOutOfRangeException(nameof(completionState));
            }
            if (expectedRunSequence < 0L) throw new ArgumentOutOfRangeException(nameof(expectedRunSequence));
            CompletionState = completionState;
            ExpectedRunSequence = expectedRunSequence;

            StringBuilder intentBuilder = new StringBuilder();
            MissionRun.AppendToken(intentBuilder, "run_stable_id", RunStableId.ToString());
            MissionRun.AppendToken(intentBuilder, "route_payload", RoutePayload.ToCanonicalString());
            MissionRun.AppendToken(intentBuilder, "route_fingerprint", RoutePayload.Fingerprint);
            MissionRun.AppendToken(intentBuilder, "completion_state", ((int)CompletionState).ToString(CultureInfo.InvariantCulture));
            IntentFingerprint = MissionRun.Fingerprint(intentBuilder.ToString());

            StringBuilder builder = new StringBuilder();
            MissionRun.AppendToken(builder, "operation_stable_id", OperationStableId.ToString());
            MissionRun.AppendToken(builder, "intent", intentBuilder.ToString());
            MissionRun.AppendToken(builder, "expected_run_sequence", ExpectedRunSequence.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = MissionRun.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public MissionRunCompletionState CompletionState { get; }
        public long ExpectedRunSequence { get; }
        public string IntentFingerprint { get; }
        public string Fingerprint { get; }

        public static EndMissionRunCommand Create(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            MissionRunCompletionState completionState,
            long expectedRunSequence)
        {
            return new EndMissionRunCommand(
                operationStableId,
                runStableId,
                routePayload,
                completionState,
                expectedRunSequence);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(EndMissionRunCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as EndMissionRunCommand); }
        public override int GetHashCode() { return MissionRun.DeterministicHash(canonicalText); }
    }
}

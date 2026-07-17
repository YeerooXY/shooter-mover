using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;

namespace ShooterMover.Application.Development.RunDebug
{
    public static class RunDebugBuildGuardV1
    {
        public static bool IsAvailable
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static bool Evaluate(bool isEditor, bool isDevelopmentBuild)
        {
            return isEditor || isDevelopmentBuild;
        }
    }

    public enum RunDebugSpawnBatchStatusV1
    {
        Spawned = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        Disabled = 5,
        Rejected = 6,
    }

    public sealed class RunDebugSpawnRequestV1 : IEquatable<RunDebugSpawnRequestV1>
    {
        public const int MaximumStrongboxCount = 64;
        private readonly string canonicalText;

        private RunDebugSpawnRequestV1(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            int strongboxCount,
            StableId strongboxTierStableId,
            ulong deterministicSeed)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoutePayload = routePayload ?? throw new ArgumentNullException(nameof(routePayload));
            if (!RoutePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "Route payload fingerprint is invalid.",
                    nameof(routePayload));
            }

            if (strongboxCount < 0 || strongboxCount > MaximumStrongboxCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(strongboxCount),
                    strongboxCount,
                    "Debug strongbox count must be between zero and "
                        + MaximumStrongboxCount.ToString(CultureInfo.InvariantCulture)
                        + ".");
            }

            StrongboxTierStableId = strongboxTierStableId
                ?? throw new ArgumentNullException(nameof(strongboxTierStableId));
            StrongboxCount = strongboxCount;
            DeterministicSeed = deterministicSeed;

            var builder = new StringBuilder();
            Append(builder, "operation", OperationStableId.ToString());
            Append(builder, "run", RunStableId.ToString());
            Append(builder, "route", RoutePayload.ToCanonicalString());
            Append(builder, "route_fingerprint", RoutePayload.Fingerprint);
            Append(builder, "count", StrongboxCount.ToString(CultureInfo.InvariantCulture));
            Append(builder, "tier", StrongboxTierStableId.ToString());
            Append(builder, "seed", DeterministicSeed.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = RewardApplicationCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayloadV1 RoutePayload { get; }
        public int StrongboxCount { get; }
        public StableId StrongboxTierStableId { get; }
        public ulong DeterministicSeed { get; }
        public string Fingerprint { get; }

        public static RunDebugSpawnRequestV1 Create(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            int strongboxCount,
            StableId strongboxTierStableId,
            ulong deterministicSeed)
        {
            if (runStableId == null) throw new ArgumentNullException(nameof(runStableId));
            if (routePayload == null) throw new ArgumentNullException(nameof(routePayload));
            if (strongboxTierStableId == null)
            {
                throw new ArgumentNullException(nameof(strongboxTierStableId));
            }

            StableId operation = RewardApplicationCanonicalV1.DeriveStableId(
                "rundebugrequest",
                runStableId.ToString(),
                routePayload.Fingerprint,
                strongboxTierStableId.ToString(),
                strongboxCount.ToString(CultureInfo.InvariantCulture),
                deterministicSeed.ToString(CultureInfo.InvariantCulture));
            return new RunDebugSpawnRequestV1(
                operation,
                runStableId,
                routePayload,
                strongboxCount,
                strongboxTierStableId,
                deterministicSeed);
        }

        public static RunDebugSpawnRequestV1 CreateWithOperation(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            int strongboxCount,
            StableId strongboxTierStableId,
            ulong deterministicSeed)
        {
            return new RunDebugSpawnRequestV1(
                operationStableId,
                runStableId,
                routePayload,
                strongboxCount,
                strongboxTierStableId,
                deterministicSeed);
        }

        public string ToCanonicalString() { return canonicalText; }

        public bool Equals(RunDebugSpawnRequestV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as RunDebugSpawnRequestV1); }
        public override int GetHashCode()
        {
            return RewardApplicationCanonicalV1.DeterministicHash(canonicalText);
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            RewardApplicationCanonicalV1.AppendToken(builder, name, value);
        }
    }

    public sealed class RunDebugBoxPlanV1 : IComparable<RunDebugBoxPlanV1>
    {
        public RunDebugBoxPlanV1(
            int index,
            StableId sourceInstanceStableId,
            StableId collectionOperationStableId)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
            SourceInstanceStableId = sourceInstanceStableId
                ?? throw new ArgumentNullException(nameof(sourceInstanceStableId));
            CollectionOperationStableId = collectionOperationStableId
                ?? throw new ArgumentNullException(nameof(collectionOperationStableId));
        }

        public int Index { get; }
        public StableId SourceInstanceStableId { get; }
        public StableId CollectionOperationStableId { get; }

        public int CompareTo(RunDebugBoxPlanV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : Index.CompareTo(other.Index);
        }
    }

    public static class RunDebugPlannerV1
    {
        public static IReadOnlyList<RunDebugBoxPlanV1> CreatePlan(
            RunDebugSpawnRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var result = new List<RunDebugBoxPlanV1>(request.StrongboxCount);
            for (int index = 0; index < request.StrongboxCount; index++)
            {
                string ordinal = index.ToString("D4", CultureInfo.InvariantCulture);
                StableId source = RewardApplicationCanonicalV1.DeriveStableId(
                    "rundebugsource",
                    request.RunStableId.ToString(),
                    request.StrongboxTierStableId.ToString(),
                    request.DeterministicSeed.ToString(CultureInfo.InvariantCulture),
                    ordinal);
                StableId collection = RewardApplicationCanonicalV1.DeriveStableId(
                    "rundebugcollect",
                    request.RunStableId.ToString(),
                    source.ToString());
                result.Add(new RunDebugBoxPlanV1(index, source, collection));
            }

            return new ReadOnlyCollection<RunDebugBoxPlanV1>(result);
        }
    }

    public sealed class RunDebugBoxFactV1 : IComparable<RunDebugBoxFactV1>
    {
        public RunDebugBoxFactV1(
            RunDebugBoxPlanV1 plan,
            bool physicalPickupSpawned,
            bool recordedCollected,
            StableId definitionStableId,
            StableId instanceStableId,
            StableId grantStableId,
            StableId sourceOperationStableId,
            StableId pickupStableId,
            string diagnostic)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (recordedCollected && !physicalPickupSpawned)
            {
                throw new ArgumentException(
                    "A collected debug strongbox must have a physical pickup projection.");
            }

            if (physicalPickupSpawned
                && (definitionStableId == null
                    || instanceStableId == null
                    || grantStableId == null
                    || sourceOperationStableId == null
                    || pickupStableId == null))
            {
                throw new ArgumentException(
                    "A spawned debug strongbox requires exact definition, instance, grant, source, and pickup identities.");
            }

            PhysicalPickupSpawned = physicalPickupSpawned;
            RecordedCollected = recordedCollected;
            DefinitionStableId = definitionStableId;
            InstanceStableId = instanceStableId;
            GrantStableId = grantStableId;
            SourceOperationStableId = sourceOperationStableId;
            PickupStableId = pickupStableId;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunDebugBoxPlanV1 Plan { get; }
        public bool PhysicalPickupSpawned { get; }
        public bool RecordedCollected { get; }
        public StableId DefinitionStableId { get; }
        public StableId InstanceStableId { get; }
        public StableId GrantStableId { get; }
        public StableId SourceOperationStableId { get; }
        public StableId PickupStableId { get; }
        public string Diagnostic { get; }

        public RunDebugBoxFactV1 WithCollection(string diagnostic)
        {
            return new RunDebugBoxFactV1(
                Plan,
                true,
                true,
                DefinitionStableId,
                InstanceStableId,
                GrantStableId,
                SourceOperationStableId,
                PickupStableId,
                diagnostic);
        }

        public RunDebugBoxFactV1 WithDiagnostic(string diagnostic)
        {
            return new RunDebugBoxFactV1(
                Plan,
                PhysicalPickupSpawned,
                RecordedCollected,
                DefinitionStableId,
                InstanceStableId,
                GrantStableId,
                SourceOperationStableId,
                PickupStableId,
                diagnostic);
        }

        public int CompareTo(RunDebugBoxFactV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : Plan.CompareTo(other.Plan);
        }
    }

    public sealed class RunDebugSnapshotV1
    {
        private readonly ReadOnlyCollection<RunDebugBoxFactV1> boxes;

        public RunDebugSnapshotV1(
            RunDebugSpawnRequestV1 request,
            IEnumerable<RunDebugBoxFactV1> boxes,
            string diagnostic)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            var ordered = new List<RunDebugBoxFactV1>(
                boxes ?? throw new ArgumentNullException(nameof(boxes)));
            ordered.Sort();
            if (ordered.Count != request.StrongboxCount)
            {
                throw new ArgumentException(
                    "Debug snapshot must contain one fact per requested strongbox.",
                    nameof(boxes));
            }

            var identities = new HashSet<StableId>();
            int spawned = 0;
            int collected = 0;
            for (int index = 0; index < ordered.Count; index++)
            {
                RunDebugBoxFactV1 value = ordered[index]
                    ?? throw new ArgumentException(
                        "Debug snapshot boxes cannot contain null.",
                        nameof(boxes));
                if (value.Plan.Index != index)
                {
                    throw new ArgumentException(
                        "Debug snapshot box ordinals must be contiguous.",
                        nameof(boxes));
                }

                if (value.PhysicalPickupSpawned)
                {
                    spawned++;
                    if (!identities.Add(value.InstanceStableId))
                    {
                        throw new ArgumentException(
                            "Debug snapshot contains duplicate strongbox instance identity.",
                            nameof(boxes));
                    }
                }

                if (value.RecordedCollected) collected++;
            }

            this.boxes = new ReadOnlyCollection<RunDebugBoxFactV1>(ordered);
            RequestedCount = request.StrongboxCount;
            SpawnedCount = spawned;
            CollectedCount = collected;
            PendingCount = spawned - collected;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunDebugSpawnRequestV1 Request { get; }
        public IReadOnlyList<RunDebugBoxFactV1> Boxes { get { return boxes; } }
        public int RequestedCount { get; }
        public int SpawnedCount { get; }
        public int CollectedCount { get; }
        public int PendingCount { get; }
        public string Diagnostic { get; }
    }

    public sealed class RunDebugSpawnBatchResultV1
    {
        public RunDebugSpawnBatchResultV1(
            RunDebugSpawnBatchStatusV1 status,
            RunDebugSnapshotV1 snapshot,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunDebugSpawnBatchStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunDebugSpawnBatchStatusV1 Status { get; }
        public RunDebugSnapshotV1 Snapshot { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunDebugSpawnBatchStatusV1.Spawned
                    || Status == RunDebugSpawnBatchStatusV1.ExactDuplicateNoChange;
            }
        }
    }

    public sealed class RunDebugEndResultV1
    {
        public RunDebugEndResultV1(
            MissionRunAuthorityResultV1 authorityResult,
            MissionResultsSessionV1 resultsSession,
            bool routed,
            string diagnostic)
        {
            AuthorityResult = authorityResult;
            ResultsSession = resultsSession;
            Routed = routed;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public MissionRunAuthorityResultV1 AuthorityResult { get; }
        public MissionResultsSessionV1 ResultsSession { get; }
        public bool Routed { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return AuthorityResult != null
                    && AuthorityResult.Succeeded
                    && AuthorityResult.ResultPayload != null;
            }
        }
    }

    public interface IRunDebugRuntimePortV1
    {
        RunDebugSpawnBatchResultV1 Spawn(RunDebugSpawnRequestV1 request);
        RunDebugSnapshotV1 RefreshSnapshot();
        RunDebugEndResultV1 EndRun(MissionRunCompletionStateV1 completionState);
    }
}

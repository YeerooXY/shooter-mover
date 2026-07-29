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
    public static class RunDebugBuildGuard
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

    public enum RunDebugSpawnBatchStatus
    {
        Spawned = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        Disabled = 5,
        Rejected = 6,
    }

    public sealed class RunDebugSpawnRequest : IEquatable<RunDebugSpawnRequest>
    {
        public const int MaximumStrongboxCount = 64;
        private readonly string canonicalText;

        private RunDebugSpawnRequest(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
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
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public PlayerRouteProfilePayload RoutePayload { get; }
        public int StrongboxCount { get; }
        public StableId StrongboxTierStableId { get; }
        public ulong DeterministicSeed { get; }
        public string Fingerprint { get; }

        public static RunDebugSpawnRequest Create(
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
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

            StableId operation = RewardApplication.DeriveStableId(
                "rundebugrequest",
                runStableId.ToString(),
                routePayload.Fingerprint,
                strongboxTierStableId.ToString(),
                strongboxCount.ToString(CultureInfo.InvariantCulture),
                deterministicSeed.ToString(CultureInfo.InvariantCulture));
            return new RunDebugSpawnRequest(
                operation,
                runStableId,
                routePayload,
                strongboxCount,
                strongboxTierStableId,
                deterministicSeed);
        }

        public static RunDebugSpawnRequest CreateWithOperation(
            StableId operationStableId,
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            int strongboxCount,
            StableId strongboxTierStableId,
            ulong deterministicSeed)
        {
            return new RunDebugSpawnRequest(
                operationStableId,
                runStableId,
                routePayload,
                strongboxCount,
                strongboxTierStableId,
                deterministicSeed);
        }

        public string ToCanonicalString() { return canonicalText; }

        public bool Equals(RunDebugSpawnRequest other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as RunDebugSpawnRequest); }
        public override int GetHashCode()
        {
            return RewardApplication.DeterministicHash(canonicalText);
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            RewardApplication.AppendToken(builder, name, value);
        }
    }

    public sealed class RunDebugBoxPlan : IComparable<RunDebugBoxPlan>
    {
        public RunDebugBoxPlan(
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

        public int CompareTo(RunDebugBoxPlan other)
        {
            return ReferenceEquals(other, null) ? 1 : Index.CompareTo(other.Index);
        }
    }

    public static class RunDebugPlanner
    {
        public static IReadOnlyList<RunDebugBoxPlan> CreatePlan(
            RunDebugSpawnRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var result = new List<RunDebugBoxPlan>(request.StrongboxCount);
            for (int index = 0; index < request.StrongboxCount; index++)
            {
                string ordinal = index.ToString("D4", CultureInfo.InvariantCulture);
                StableId source = RewardApplication.DeriveStableId(
                    "rundebugsource",
                    request.RunStableId.ToString(),
                    request.StrongboxTierStableId.ToString(),
                    request.DeterministicSeed.ToString(CultureInfo.InvariantCulture),
                    ordinal);
                StableId collection = RewardApplication.DeriveStableId(
                    "rundebugcollect",
                    request.RunStableId.ToString(),
                    source.ToString());
                result.Add(new RunDebugBoxPlan(index, source, collection));
            }

            return new ReadOnlyCollection<RunDebugBoxPlan>(result);
        }
    }

    public sealed class RunDebugBoxFact : IComparable<RunDebugBoxFact>
    {
        public RunDebugBoxFact(
            RunDebugBoxPlan plan,
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

        public RunDebugBoxPlan Plan { get; }
        public bool PhysicalPickupSpawned { get; }
        public bool RecordedCollected { get; }
        public StableId DefinitionStableId { get; }
        public StableId InstanceStableId { get; }
        public StableId GrantStableId { get; }
        public StableId SourceOperationStableId { get; }
        public StableId PickupStableId { get; }
        public string Diagnostic { get; }

        public RunDebugBoxFact WithCollection(string diagnostic)
        {
            return new RunDebugBoxFact(
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

        public RunDebugBoxFact WithDiagnostic(string diagnostic)
        {
            return new RunDebugBoxFact(
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

        public int CompareTo(RunDebugBoxFact other)
        {
            return ReferenceEquals(other, null) ? 1 : Plan.CompareTo(other.Plan);
        }
    }

    public sealed class RunDebugSnapshot
    {
        private readonly ReadOnlyCollection<RunDebugBoxFact> boxes;

        public RunDebugSnapshot(
            RunDebugSpawnRequest request,
            IEnumerable<RunDebugBoxFact> boxes,
            string diagnostic)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            var ordered = new List<RunDebugBoxFact>(
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
                RunDebugBoxFact value = ordered[index]
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

            this.boxes = new ReadOnlyCollection<RunDebugBoxFact>(ordered);
            RequestedCount = request.StrongboxCount;
            SpawnedCount = spawned;
            CollectedCount = collected;
            PendingCount = spawned - collected;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunDebugSpawnRequest Request { get; }
        public IReadOnlyList<RunDebugBoxFact> Boxes { get { return boxes; } }
        public int RequestedCount { get; }
        public int SpawnedCount { get; }
        public int CollectedCount { get; }
        public int PendingCount { get; }
        public string Diagnostic { get; }
    }

    public sealed class RunDebugSpawnBatchResult
    {
        public RunDebugSpawnBatchResult(
            RunDebugSpawnBatchStatus status,
            RunDebugSnapshot snapshot,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RunDebugSpawnBatchStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            Snapshot = snapshot;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RunDebugSpawnBatchStatus Status { get; }
        public RunDebugSnapshot Snapshot { get; }
        public string Diagnostic { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunDebugSpawnBatchStatus.Spawned
                    || Status == RunDebugSpawnBatchStatus.ExactDuplicateNoChange;
            }
        }
    }

    public sealed class RunDebugEndResult
    {
        public RunDebugEndResult(
            MissionRunStateResult authorityResult,
            MissionResultsSession resultsSession,
            bool routed,
            string diagnostic)
        {
            AuthorityResult = authorityResult;
            ResultsSession = resultsSession;
            Routed = routed;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public MissionRunStateResult AuthorityResult { get; }
        public MissionResultsSession ResultsSession { get; }
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

    public interface IRunDebugLivePort
    {
        RunDebugSpawnBatchResult Spawn(RunDebugSpawnRequest request);
        RunDebugSnapshot RefreshSnapshot();
        RunDebugEndResult EndRun(MissionRunCompletionState completionState);
    }
}

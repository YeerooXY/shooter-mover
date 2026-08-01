using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Development.RunDebug;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Rewards.LootDrops;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Content.Definitions.Rewards.LootDrops;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Rewards.Pickups;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Development.RunDebug
{
    /// <summary>
    /// Development-only composition adapter. It creates deterministic DROP operations,
    /// delegates generation/commit/projection to the existing PICK factory, observes the
    /// physical pickup, records verified collection facts through RUN-001, and routes only
    /// the frozen terminal result. It never writes holdings or Results directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunDebugRewards : MonoBehaviour, IRunDebugLivePort
    {
        private sealed class LiveBox
        {
            public RunDebugBoxFact Fact;
            public LootPickup Pickup;
        }

        [SerializeField] private LootSpawner dropFactory;
        [SerializeField] private Transform spawnOrigin;
        [SerializeField, Min(0.1f)] private float spawnSpacing = 1.25f;

        private readonly List<LiveBox> runtimeBoxes = new List<LiveBox>();
        private StableId runStableId;
        private PlayerRouteProfilePayload routePayload;
        private IPlayerHoldingsState holdingsAuthority;
        private Func<StrongboxOpeningSnapshot> strongboxSnapshotExporter;
        private MissionRunResultState runAuthority;
        private Action<MissionResultsSession> resultsRouter;
        private RunDebugSpawnRequest acceptedRequest;
        private RunDebugSnapshot snapshot;
        private RunDebugEndResult terminalEndResult;
        private LootDropProfileDefinitionAsset runtimeProfile;
        private int endRunAuthorityCallCount;

        public bool IsConfigured
        {
            get
            {
                return runStableId != null
                    && routePayload != null
                    && holdingsAuthority != null
                    && strongboxSnapshotExporter != null
                    && runAuthority != null
                    && dropFactory != null;
            }
        }

        public int EndRunAuthorityCallCount { get { return endRunAuthorityCallCount; } }
        public RunDebugSnapshot CurrentSnapshot { get { return snapshot; } }
        public MissionResultsSession LastResultsSession
        {
            get { return terminalEndResult == null ? null : terminalEndResult.ResultsSession; }
        }

        public void ConfigureRuntime(
            StableId runStableId,
            PlayerRouteProfilePayload routePayload,
            IPlayerHoldingsState holdingsAuthority,
            Func<StrongboxOpeningSnapshot> strongboxSnapshotExporter,
            MissionRunResultState runAuthority,
            LootSpawner dropFactory,
            Action<MissionResultsSession> resultsRouter = null)
        {
            if (acceptedRequest != null || terminalEndResult != null)
            {
                throw new InvalidOperationException(
                    "A used debug bridge cannot be rebound to another mission run.");
            }

            this.runStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            this.routePayload = routePayload
                ?? throw new ArgumentNullException(nameof(routePayload));
            if (!routePayload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "Route payload fingerprint is invalid.",
                    nameof(routePayload));
            }

            this.holdingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            this.strongboxSnapshotExporter = strongboxSnapshotExporter
                ?? throw new ArgumentNullException(nameof(strongboxSnapshotExporter));
            this.runAuthority = runAuthority
                ?? throw new ArgumentNullException(nameof(runAuthority));
            this.dropFactory = dropFactory
                ?? throw new ArgumentNullException(nameof(dropFactory));
            this.resultsRouter = resultsRouter;
        }

        public RunDebugSpawnRequest CreateRequest(
            int strongboxCount,
            StableId strongboxTierStableId,
            ulong deterministicSeed)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "Run debug bridge must be configured before creating a request.");
            }

            return RunDebugSpawnRequest.Create(
                runStableId,
                routePayload,
                strongboxCount,
                strongboxTierStableId,
                deterministicSeed);
        }

        public RunDebugSpawnBatchResult Spawn(RunDebugSpawnRequest request)
        {
            if (!RunDebugBuildGuard.IsAvailable)
            {
                return new RunDebugSpawnBatchResult(
                    RunDebugSpawnBatchStatus.Disabled,
                    snapshot,
                    "DEV-001 is disabled outside Editor and Development builds.");
            }

            if (!IsConfigured || request == null)
            {
                return new RunDebugSpawnBatchResult(
                    RunDebugSpawnBatchStatus.InvalidRequest,
                    snapshot,
                    "Run debug bridge is not configured or the request is null.");
            }

            if (request.RunStableId != runStableId
                || !string.Equals(
                    request.RoutePayload.Fingerprint,
                    routePayload.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new RunDebugSpawnBatchResult(
                    RunDebugSpawnBatchStatus.InvalidRequest,
                    snapshot,
                    "Debug request run or route payload does not match the configured mission.");
            }

            if (terminalEndResult != null)
            {
                return new RunDebugSpawnBatchResult(
                    RunDebugSpawnBatchStatus.Rejected,
                    snapshot,
                    "The mission run already has a terminal result.");
            }

            if (acceptedRequest != null)
            {
                bool sameOperation = acceptedRequest.OperationStableId == request.OperationStableId;
                bool exact = string.Equals(
                    acceptedRequest.Fingerprint,
                    request.Fingerprint,
                    StringComparison.Ordinal);
                return new RunDebugSpawnBatchResult(
                    exact
                        ? RunDebugSpawnBatchStatus.ExactDuplicateNoChange
                        : sameOperation
                            ? RunDebugSpawnBatchStatus.ConflictingDuplicate
                            : RunDebugSpawnBatchStatus.Rejected,
                    RefreshSnapshot(),
                    exact
                        ? "Exact deterministic spawn request reused the existing physical projections."
                        : sameOperation
                            ? "The debug spawn operation identity was reused with conflicting input."
                            : "Only one debug spawn batch is accepted per mission run.");
            }

            acceptedRequest = request;
            IReadOnlyList<RunDebugBoxPlan> plan = RunDebugPlanner.CreatePlan(request);
            RewardProfile profile = CreateProfile(request);
            runtimeBoxes.Clear();
            Transform origin = spawnOrigin == null ? transform : spawnOrigin;
            Vector3 originalFactoryPosition = dropFactory.transform.position;

            try
            {
                for (int index = 0; index < plan.Count; index++)
                {
                    RunDebugBoxPlan item = plan[index];
                    dropFactory.transform.position =
                        origin.position + Vector3.right * (spawnSpacing * index);
                    runtimeBoxes.Add(SpawnOne(request, item, profile));
                }
            }
            finally
            {
                dropFactory.transform.position = originalFactoryPosition;
            }

            snapshot = BuildSnapshot("Debug batch resolved through DROP, GEN, PICK, and RAP.");
            return new RunDebugSpawnBatchResult(
                RunDebugSpawnBatchStatus.Spawned,
                snapshot,
                snapshot.Diagnostic);
        }

        public RunDebugSnapshot RefreshSnapshot()
        {
            if (acceptedRequest == null)
            {
                return snapshot;
            }

            for (int index = 0; index < runtimeBoxes.Count; index++)
            {
                LiveBox entry = runtimeBoxes[index];
                if (entry.Fact.RecordedCollected
                    || !entry.Fact.PhysicalPickupSpawned
                    || entry.Pickup == null
                    || !entry.Pickup.IsCollected)
                {
                    continue;
                }

                RecordCollection(entry);
            }

            snapshot = BuildSnapshot(
                "Counts are projected from immutable requests, physical PICK state, holdings snapshots, and RUN facts.");
            return snapshot;
        }

        public RunDebugEndResult EndRun(MissionRunCompletionState completionState)
        {
            if (terminalEndResult != null)
            {
                return terminalEndResult;
            }

            if (!RunDebugBuildGuard.IsAvailable)
            {
                terminalEndResult = new RunDebugEndResult(
                    null,
                    null,
                    false,
                    "DEV-001 is disabled outside Editor and Development builds.");
                return terminalEndResult;
            }

            if (!IsConfigured)
            {
                terminalEndResult = new RunDebugEndResult(
                    null,
                    null,
                    false,
                    "Run debug bridge is not configured.");
                return terminalEndResult;
            }

            RefreshSnapshot();
            PlayerHoldingsSnapshot holdings = holdingsAuthority.ExportSnapshot();
            StrongboxOpeningSnapshot openings = strongboxSnapshotExporter();
            if (holdings == null || openings == null)
            {
                terminalEndResult = new RunDebugEndResult(
                    null,
                    null,
                    false,
                    "End Run requires current holdings and strongbox-opening snapshots.");
                return terminalEndResult;
            }

            StableId operationStableId = RewardApplication.DeriveStableId(
                "rundebugend",
                runStableId.ToString(),
                routePayload.Fingerprint,
                ((int)completionState).ToString(CultureInfo.InvariantCulture));
            EndMissionRunCommand command = EndMissionRunCommand.Create(
                operationStableId,
                runStableId,
                routePayload,
                completionState,
                runAuthority.Sequence);

            endRunAuthorityCallCount++;
            MissionRunStateResult result = runAuthority.EndRun(command);
            MissionResultsSession resultsSession = null;
            bool routed = false;
            if (result != null && result.Succeeded && result.ResultPayload != null)
            {
                resultsSession = new MissionResultsSession(result.ResultPayload);
                if (resultsRouter != null)
                {
                    resultsRouter(resultsSession);
                    routed = true;
                }
            }

            terminalEndResult = new RunDebugEndResult(
                result,
                resultsSession,
                routed,
                result == null
                    ? "RUN-001 returned no result."
                    : result.Succeeded
                        ? "RUN-001 froze the exact terminal result."
                        : "RUN-001 rejected End Run: " + result.RejectionCode);
            return terminalEndResult;
        }

        private LiveBox SpawnOne(
            RunDebugSpawnRequest request,
            RunDebugBoxPlan plan,
            RewardProfile profile)
        {
            LootDropOverride manualOverride = LootDropOverride.Default(
                RewardApplication.DeriveStableId(
                    "rundebugoverride",
                    request.RunStableId.ToString(),
                    plan.SourceInstanceStableId.ToString()));
            LootDropOperation operation = LootDropOperationFactory.Create(
                request.RunStableId,
                plan.SourceInstanceStableId,
                profile,
                manualOverride);
            LootSourceResolvedPreview preview = new LootSourceResolvedPreview(
                LootSourceOverrideAuthoringMode.Inherit,
                profile,
                profile,
                operation.OperationRequest,
                operation.RestartParticipantStableId,
                operation.Fingerprint);
            LootSourceSubmissionResult submission = dropFactory.Submit(preview);
            LootPickupSpawnResult spawn = dropFactory.LastSpawnResult;

            RunDebugBoxFact fact;
            LootPickup pickup = null;
            string rejection = null;
            if (submission == null
                || !submission.IsAccepted
                || spawn == null
                || spawn.Pickup == null
                || !TryReadStrongboxFact(plan, spawn.Pickup, out fact, out rejection))
            {
                fact = new RunDebugBoxFact(
                    plan,
                    false,
                    false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    rejection
                        ?? (submission == null
                            ? "DROP/PICK submission returned no result."
                            : submission.Diagnostic));
            }
            else
            {
                pickup = spawn.Pickup;
            }

            return new LiveBox { Fact = fact, Pickup = pickup };
        }

        private static bool TryReadStrongboxFact(
            RunDebugBoxPlan plan,
            LootPickup pickup,
            out RunDebugBoxFact fact,
            out string rejection)
        {
            fact = null;
            rejection = null;
            if (pickup == null
                || pickup.Payload == null
                || pickup.Payload.CommitCommand == null)
            {
                rejection = "Physical pickup payload is missing.";
                return false;
            }

            RewardCommitCommand command = pickup.Payload.CommitCommand;
            RewardGrantApplicationPayload strongboxPayload = null;
            for (int index = 0; index < command.GrantPayloads.Count; index++)
            {
                RewardGrantApplicationPayload candidate = command.GrantPayloads[index];
                if (candidate.Grant.Kind != RewardGrantKind.Strongbox) continue;
                if (strongboxPayload != null)
                {
                    rejection = "Debug DROP produced more than one strongbox grant payload.";
                    return false;
                }

                strongboxPayload = candidate;
            }

            if (strongboxPayload == null
                || strongboxPayload.Grant.Quantity != 1L
                || strongboxPayload.InstanceStableIds.Count != 1)
            {
                rejection = "Debug DROP must produce exactly one strongbox instance.";
                return false;
            }

            fact = new RunDebugBoxFact(
                plan,
                true,
                false,
                strongboxPayload.Grant.ContentStableId,
                strongboxPayload.InstanceStableIds[0],
                strongboxPayload.Grant.GrantStableId,
                command.SourceOperationStableId,
                pickup.Payload.PickupStableId,
                "Physical strongbox pickup projected.");
            return true;
        }

        private void RecordCollection(LiveBox entry)
        {
            PlayerHoldingsSnapshot holdings = holdingsAuthority.ExportSnapshot();
            if (holdings == null)
            {
                entry.Fact = entry.Fact.WithDiagnostic(
                    "Physical pickup is collected, but the holdings snapshot is unavailable.");
                return;
            }

            MissionRunCollectStrongboxCommand command =
                MissionRunCollectStrongboxCommand.Create(
                    entry.Fact.Plan.CollectionOperationStableId,
                    runStableId,
                    routePayload,
                    entry.Fact.DefinitionStableId,
                    entry.Fact.InstanceStableId,
                    entry.Fact.GrantStableId,
                    entry.Fact.SourceOperationStableId,
                    runAuthority.Sequence);
            MissionRunStateResult result = runAuthority.RecordCollectedStrongbox(command);
            if (result != null
                && result.Succeeded
                && result.Collection != null
                && result.Collection.InstanceStableId == entry.Fact.InstanceStableId)
            {
                entry.Fact = entry.Fact.WithCollection(
                    "Physical pickup collection verified and recorded by RUN-001.");
                return;
            }

            entry.Fact = entry.Fact.WithDiagnostic(
                result == null
                    ? "RUN-001 returned no collection result."
                    : "RUN-001 rejected collection: " + result.RejectionCode);
        }

        private RewardProfile CreateProfile(RunDebugSpawnRequest request)
        {
            if (runtimeProfile != null)
            {
                Destroy(runtimeProfile);
            }

            StableId profileId = RewardApplication.DeriveStableId(
                "rundebugprofile",
                request.RunStableId.ToString(),
                request.StrongboxTierStableId.ToString(),
                request.DeterministicSeed.ToString(CultureInfo.InvariantCulture));
            StableId grantId = RewardApplication.DeriveStableId(
                "rundebuggrant",
                request.RunStableId.ToString(),
                request.StrongboxTierStableId.ToString(),
                request.DeterministicSeed.ToString(CultureInfo.InvariantCulture));
            runtimeProfile = LootDropProfileDefinitionAsset.CreateRuntime(
                profileId.ToString(),
                false,
                new[]
                {
                    new RewardGrantAuthoring(
                        grantId.ToString(),
                        RewardGrantKind.Strongbox,
                        request.StrongboxTierStableId.ToString(),
                        1L,
                        1L)
                },
                Array.Empty<IndependentRewardRollAuthoring>(),
                Array.Empty<ExclusiveRewardGroupAuthoring>());
            return runtimeProfile.BuildProfile();
        }

        private RunDebugSnapshot BuildSnapshot(string diagnostic)
        {
            var values = new List<RunDebugBoxFact>(runtimeBoxes.Count);
            for (int index = 0; index < runtimeBoxes.Count; index++)
            {
                values.Add(runtimeBoxes[index].Fact);
            }

            return new RunDebugSnapshot(acceptedRequest, values, diagnostic);
        }

        private void OnDestroy()
        {
            if (runtimeProfile != null)
            {
                Destroy(runtimeProfile);
            }
        }

        private void OnValidate()
        {
            if (spawnSpacing < 0.1f
                || float.IsNaN(spawnSpacing)
                || float.IsInfinity(spawnSpacing))
            {
                spawnSpacing = 1.25f;
            }
        }
    }
}

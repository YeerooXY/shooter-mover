using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Development.RunDebug;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Rewards.GameplayDrops;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Content.Definitions.Rewards.GameplayDrops;
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
    public sealed class RunDebugRewardBridge2D : MonoBehaviour, IRunDebugRuntimePortV1
    {
        private sealed class RuntimeBox
        {
            public RunDebugBoxFactV1 Fact;
            public RewardPickup2D Pickup;
        }

        [SerializeField] private RewardPickupDropFactory2D dropFactory;
        [SerializeField] private Transform spawnOrigin;
        [SerializeField, Min(0.1f)] private float spawnSpacing = 1.25f;

        private readonly List<RuntimeBox> runtimeBoxes = new List<RuntimeBox>();
        private StableId runStableId;
        private PlayerRouteProfilePayloadV1 routePayload;
        private IPlayerHoldingsAuthorityV1 holdingsAuthority;
        private Func<StrongboxOpeningSnapshotV1> strongboxSnapshotExporter;
        private MissionRunResultAuthorityV1 runAuthority;
        private Action<MissionResultsSessionV1> resultsRouter;
        private RunDebugSpawnRequestV1 acceptedRequest;
        private RunDebugSnapshotV1 snapshot;
        private RunDebugEndResultV1 terminalEndResult;
        private GameplayDropProfileDefinitionAsset runtimeProfile;
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
        public RunDebugSnapshotV1 CurrentSnapshot { get { return snapshot; } }
        public MissionResultsSessionV1 LastResultsSession
        {
            get { return terminalEndResult == null ? null : terminalEndResult.ResultsSession; }
        }

        public void ConfigureRuntime(
            StableId runStableId,
            PlayerRouteProfilePayloadV1 routePayload,
            IPlayerHoldingsAuthorityV1 holdingsAuthority,
            Func<StrongboxOpeningSnapshotV1> strongboxSnapshotExporter,
            MissionRunResultAuthorityV1 runAuthority,
            RewardPickupDropFactory2D dropFactory,
            Action<MissionResultsSessionV1> resultsRouter = null)
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

        public RunDebugSpawnRequestV1 CreateRequest(
            int strongboxCount,
            StableId strongboxTierStableId,
            ulong deterministicSeed)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "Run debug bridge must be configured before creating a request.");
            }

            return RunDebugSpawnRequestV1.Create(
                runStableId,
                routePayload,
                strongboxCount,
                strongboxTierStableId,
                deterministicSeed);
        }

        public RunDebugSpawnBatchResultV1 Spawn(RunDebugSpawnRequestV1 request)
        {
            if (!RunDebugBuildGuardV1.IsAvailable)
            {
                return new RunDebugSpawnBatchResultV1(
                    RunDebugSpawnBatchStatusV1.Disabled,
                    snapshot,
                    "DEV-001 is disabled outside Editor and Development builds.");
            }

            if (!IsConfigured || request == null)
            {
                return new RunDebugSpawnBatchResultV1(
                    RunDebugSpawnBatchStatusV1.InvalidRequest,
                    snapshot,
                    "Run debug bridge is not configured or the request is null.");
            }

            if (request.RunStableId != runStableId
                || !string.Equals(
                    request.RoutePayload.Fingerprint,
                    routePayload.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new RunDebugSpawnBatchResultV1(
                    RunDebugSpawnBatchStatusV1.InvalidRequest,
                    snapshot,
                    "Debug request run or route payload does not match the configured mission.");
            }

            if (terminalEndResult != null)
            {
                return new RunDebugSpawnBatchResultV1(
                    RunDebugSpawnBatchStatusV1.Rejected,
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
                return new RunDebugSpawnBatchResultV1(
                    exact
                        ? RunDebugSpawnBatchStatusV1.ExactDuplicateNoChange
                        : sameOperation
                            ? RunDebugSpawnBatchStatusV1.ConflictingDuplicate
                            : RunDebugSpawnBatchStatusV1.Rejected,
                    RefreshSnapshot(),
                    exact
                        ? "Exact deterministic spawn request reused the existing physical projections."
                        : sameOperation
                            ? "The debug spawn operation identity was reused with conflicting input."
                            : "Only one debug spawn batch is accepted per mission run.");
            }

            acceptedRequest = request;
            IReadOnlyList<RunDebugBoxPlanV1> plan = RunDebugPlannerV1.CreatePlan(request);
            RewardProfileV1 profile = CreateProfile(request);
            runtimeBoxes.Clear();
            Transform origin = spawnOrigin == null ? transform : spawnOrigin;
            Vector3 originalFactoryPosition = dropFactory.transform.position;

            try
            {
                for (int index = 0; index < plan.Count; index++)
                {
                    RunDebugBoxPlanV1 item = plan[index];
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
            return new RunDebugSpawnBatchResultV1(
                RunDebugSpawnBatchStatusV1.Spawned,
                snapshot,
                snapshot.Diagnostic);
        }

        public RunDebugSnapshotV1 RefreshSnapshot()
        {
            if (acceptedRequest == null)
            {
                return snapshot;
            }

            for (int index = 0; index < runtimeBoxes.Count; index++)
            {
                RuntimeBox entry = runtimeBoxes[index];
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

        public RunDebugEndResultV1 EndRun(MissionRunCompletionStateV1 completionState)
        {
            if (terminalEndResult != null)
            {
                return terminalEndResult;
            }

            if (!RunDebugBuildGuardV1.IsAvailable)
            {
                terminalEndResult = new RunDebugEndResultV1(
                    null,
                    null,
                    false,
                    "DEV-001 is disabled outside Editor and Development builds.");
                return terminalEndResult;
            }

            if (!IsConfigured)
            {
                terminalEndResult = new RunDebugEndResultV1(
                    null,
                    null,
                    false,
                    "Run debug bridge is not configured.");
                return terminalEndResult;
            }

            RefreshSnapshot();
            PlayerHoldingsSnapshotV1 holdings = holdingsAuthority.ExportSnapshot();
            StrongboxOpeningSnapshotV1 openings = strongboxSnapshotExporter();
            if (holdings == null || openings == null)
            {
                terminalEndResult = new RunDebugEndResultV1(
                    null,
                    null,
                    false,
                    "End Run requires current holdings and strongbox-opening snapshots.");
                return terminalEndResult;
            }

            StableId operationStableId = RewardApplicationCanonicalV1.DeriveStableId(
                "rundebugend",
                runStableId.ToString(),
                routePayload.Fingerprint,
                ((int)completionState).ToString(CultureInfo.InvariantCulture));
            EndMissionRunCommandV1 command = EndMissionRunCommandV1.Create(
                operationStableId,
                runStableId,
                routePayload,
                completionState,
                runAuthority.Sequence,
                holdingsAuthority.Sequence,
                holdings.Fingerprint,
                openings.Sequence,
                openings.Fingerprint);

            endRunAuthorityCallCount++;
            MissionRunAuthorityResultV1 result = runAuthority.EndRun(command);
            MissionResultsSessionV1 resultsSession = null;
            bool routed = false;
            if (result != null && result.Succeeded && result.ResultPayload != null)
            {
                resultsSession = new MissionResultsSessionV1(result.ResultPayload);
                if (resultsRouter != null)
                {
                    resultsRouter(resultsSession);
                    routed = true;
                }
            }

            terminalEndResult = new RunDebugEndResultV1(
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

        private RuntimeBox SpawnOne(
            RunDebugSpawnRequestV1 request,
            RunDebugBoxPlanV1 plan,
            RewardProfileV1 profile)
        {
            GameplayDropOverrideV1 manualOverride = GameplayDropOverrideV1.Default(
                RewardApplicationCanonicalV1.DeriveStableId(
                    "rundebugoverride",
                    request.RunStableId.ToString(),
                    plan.SourceInstanceStableId.ToString()));
            GameplayDropOperationV1 operation = GameplayDropOperationFactoryV1.Create(
                request.RunStableId,
                plan.SourceInstanceStableId,
                profile,
                manualOverride);
            RewardSourceResolvedPreview preview = new RewardSourceResolvedPreview(
                RewardSourceOverrideAuthoringMode.Inherit,
                profile,
                profile,
                operation.OperationRequest,
                operation.RestartParticipantStableId,
                operation.Fingerprint);
            RewardSourceSubmissionResult submission = dropFactory.Submit(preview);
            RewardPickupSpawnResultV1 spawn = dropFactory.LastSpawnResult;

            RunDebugBoxFactV1 fact;
            RewardPickup2D pickup = null;
            string rejection = null;
            if (submission == null
                || !submission.IsAccepted
                || spawn == null
                || spawn.Pickup == null
                || !TryReadStrongboxFact(plan, spawn.Pickup, out fact, out rejection))
            {
                fact = new RunDebugBoxFactV1(
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

            return new RuntimeBox { Fact = fact, Pickup = pickup };
        }

        private static bool TryReadStrongboxFact(
            RunDebugBoxPlanV1 plan,
            RewardPickup2D pickup,
            out RunDebugBoxFactV1 fact,
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

            RewardCommitCommandV1 command = pickup.Payload.CommitCommand;
            RewardGrantApplicationPayloadV1 strongboxPayload = null;
            for (int index = 0; index < command.GrantPayloads.Count; index++)
            {
                RewardGrantApplicationPayloadV1 candidate = command.GrantPayloads[index];
                if (candidate.Grant.Kind != RewardGrantKindV1.Strongbox) continue;
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

            fact = new RunDebugBoxFactV1(
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

        private void RecordCollection(RuntimeBox entry)
        {
            PlayerHoldingsSnapshotV1 holdings = holdingsAuthority.ExportSnapshot();
            if (holdings == null)
            {
                entry.Fact = entry.Fact.WithDiagnostic(
                    "Physical pickup is collected, but the holdings snapshot is unavailable.");
                return;
            }

            MissionRunCollectStrongboxCommandV1 command =
                MissionRunCollectStrongboxCommandV1.Create(
                    entry.Fact.Plan.CollectionOperationStableId,
                    runStableId,
                    routePayload,
                    entry.Fact.DefinitionStableId,
                    entry.Fact.InstanceStableId,
                    entry.Fact.GrantStableId,
                    entry.Fact.SourceOperationStableId,
                    runAuthority.Sequence,
                    holdingsAuthority.Sequence,
                    holdings.Fingerprint);
            MissionRunAuthorityResultV1 result = runAuthority.RecordCollectedStrongbox(command);
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

        private RewardProfileV1 CreateProfile(RunDebugSpawnRequestV1 request)
        {
            if (runtimeProfile != null)
            {
                Destroy(runtimeProfile);
            }

            StableId profileId = RewardApplicationCanonicalV1.DeriveStableId(
                "rundebugprofile",
                request.RunStableId.ToString(),
                request.StrongboxTierStableId.ToString(),
                request.DeterministicSeed.ToString(CultureInfo.InvariantCulture));
            StableId grantId = RewardApplicationCanonicalV1.DeriveStableId(
                "rundebuggrant",
                request.RunStableId.ToString(),
                request.StrongboxTierStableId.ToString(),
                request.DeterministicSeed.ToString(CultureInfo.InvariantCulture));
            runtimeProfile = GameplayDropProfileDefinitionAsset.CreateRuntime(
                profileId.ToString(),
                false,
                new[]
                {
                    new RewardGrantAuthoring(
                        grantId.ToString(),
                        RewardGrantKindV1.Strongbox,
                        request.StrongboxTierStableId.ToString(),
                        1L,
                        1L)
                },
                Array.Empty<IndependentRewardRollAuthoring>(),
                Array.Empty<ExclusiveRewardGroupAuthoring>());
            return runtimeProfile.BuildProfile();
        }

        private RunDebugSnapshotV1 BuildSnapshot(string diagnostic)
        {
            var values = new List<RunDebugBoxFactV1>(runtimeBoxes.Count);
            for (int index = 0; index < runtimeBoxes.Count; index++)
            {
                values.Add(runtimeBoxes[index].Fact);
            }

            return new RunDebugSnapshotV1(acceptedRequest, values, diagnostic);
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

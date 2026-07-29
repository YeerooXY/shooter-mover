using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Pickups
{
    /// <summary>
    /// Converts SRC operations into deterministic GEN/RAP commitments and transient
    /// physical pickup projections. Forced drops accept the same fully prepared RAP
    /// command, so profile and forced paths converge before collection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardPickupDropFactory2D :
        MonoBehaviour,
        IRewardSourceOperationSink
    {
        [SerializeField] private RewardPickup2D pickupPrefab;
        [SerializeField] private Transform pickupParent;
        [SerializeField] private MonoBehaviour lifecycleAuthority;
        [SerializeField] private GameplaySceneScope2D restartScope;
        [SerializeField] private ulong rootSeed = 1UL;
        [SerializeField] private int algorithmVersion = 1;

        private readonly Dictionary<StableId, RewardPickup2D> spawnedPickups =
            new Dictionary<StableId, RewardPickup2D>();
        private RewardGenerationActions generator;
        private ProgressionContext progressionContext;
        private IRewardPickupEquipmentPayloadResolver equipmentResolver;
        private RewardPickupSpawnResult lastSpawnResult;

        public int SpawnedPickupCount { get { return spawnedPickups.Count; } }
        public RewardPickupSpawnResult LastSpawnResult { get { return lastSpawnResult; } }

        public void ConfigureRuntime(
            RewardGenerationActions generator,
            ProgressionContext progressionContext,
            ulong rootSeed,
            int algorithmVersion,
            MonoBehaviour lifecycleAuthority,
            GameplaySceneScope2D restartScope,
            IRewardPickupEquipmentPayloadResolver equipmentResolver = null)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.progressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (algorithmVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            }

            if (!(lifecycleAuthority is IRewardPickupLifecycleState))
            {
                throw new ArgumentException(
                    "Lifecycle authority must implement IRewardPickupLifecycleState.",
                    nameof(lifecycleAuthority));
            }

            this.rootSeed = rootSeed;
            this.algorithmVersion = algorithmVersion;
            this.lifecycleAuthority = lifecycleAuthority;
            this.restartScope = restartScope;
            this.equipmentResolver = equipmentResolver;
        }

        public void ConfigureForTests(
            RewardGenerationActions generator,
            ProgressionContext progressionContext,
            ulong rootSeed,
            int algorithmVersion,
            MonoBehaviour lifecycleAuthority,
            GameplaySceneScope2D restartScope,
            RewardPickup2D pickupPrefab = null,
            IRewardPickupEquipmentPayloadResolver equipmentResolver = null)
        {
            this.pickupPrefab = pickupPrefab;
            ConfigureRuntime(
                generator,
                progressionContext,
                rootSeed,
                algorithmVersion,
                lifecycleAuthority,
                restartScope,
                equipmentResolver);
        }

        public RewardSourceSubmissionResult Submit(RewardSourceResolvedPreview preview)
        {
            if (preview == null)
            {
                return RejectedSubmission("Reward source preview is null.");
            }

            IRewardPickupLifecycleState authority =
                lifecycleAuthority as IRewardPickupLifecycleState;
            if (generator == null || progressionContext == null || authority == null)
            {
                return RejectedSubmission(
                    "Pickup drop factory is missing generator, progression context, or RAP authority.");
            }

            RewardGenerationResultEnvelope generation;
            try
            {
                generation = generator.GenerateReward(
                    RewardGenerationRequest.Create(
                        preview.OperationRequest,
                        preview.ResolvedProfile,
                        progressionContext,
                        rootSeed,
                        algorithmVersion));
            }
            catch (Exception exception)
            {
                return RejectedSubmission("Reward generation threw: " + exception.Message);
            }

            if (generation == null || !generation.IsSuccess || generation.Result == null)
            {
                return RejectedSubmission(
                    generation == null
                        ? "Reward generation returned no result."
                        : "Reward generation failed: " + generation.FailureReason);
            }

            IReadOnlyList<RewardGrantApplicationPayload> payloads;
            string rejectionCode;
            if (!RewardPickupPayloadBuilder.TryBuild(
                preview,
                generation.Result,
                equipmentResolver,
                out payloads,
                out rejectionCode))
            {
                return RejectedSubmission(rejectionCode);
            }

            RewardCommitCommand command;
            try
            {
                command = RewardCommitCommand.Create(
                    preview.OperationRequest,
                    generation.Result,
                    generation.ResultFingerprint,
                    payloads);
            }
            catch (Exception exception)
            {
                return RejectedSubmission("Pickup commit preparation threw: " + exception.Message);
            }

            lastSpawnResult = CommitAndProject(command, null);
            switch (lastSpawnResult.Status)
            {
                case RewardPickupSpawnStatus.Spawned:
                    return new RewardSourceSubmissionResult(
                        RewardSourceSubmissionStatus.Accepted,
                        lastSpawnResult.Diagnostic);
                case RewardPickupSpawnStatus.ExactDuplicateNoChange:
                    return new RewardSourceSubmissionResult(
                        RewardSourceSubmissionStatus.ExactDuplicateNoChange,
                        lastSpawnResult.Diagnostic);
                case RewardPickupSpawnStatus.ExplicitNoDrop:
                    return new RewardSourceSubmissionResult(
                        lastSpawnResult.AuthorityResult != null
                            && lastSpawnResult.AuthorityResult.Status
                                == RewardApplicationResultStatus.ExactDuplicateNoChange
                            ? RewardSourceSubmissionStatus.ExactDuplicateNoChange
                            : RewardSourceSubmissionStatus.Accepted,
                        lastSpawnResult.Diagnostic);
                default:
                    return RejectedSubmission(lastSpawnResult.Diagnostic);
            }
        }

        public RewardPickupSpawnResult SpawnForced(
            RewardCommitCommand command,
            RewardPickupCategory? category = null)
        {
            lastSpawnResult = CommitAndProject(command, category);
            return lastSpawnResult;
        }

        public bool TryGetPickup(StableId pickupStableId, out RewardPickup2D pickup)
        {
            pickup = null;
            return pickupStableId != null
                && spawnedPickups.TryGetValue(pickupStableId, out pickup);
        }

        private RewardPickupSpawnResult CommitAndProject(
            RewardCommitCommand command,
            RewardPickupCategory? category)
        {
            IRewardPickupLifecycleState authority =
                lifecycleAuthority as IRewardPickupLifecycleState;
            if (command == null || authority == null)
            {
                return new RewardPickupSpawnResult(
                    RewardPickupSpawnStatus.Rejected,
                    null,
                    null,
                    "Forced drop requires a prepared commit and RAP lifecycle authority.");
            }

            RewardApplicationResult commitResult = authority.Commit(command);
            if (commitResult == null)
            {
                return new RewardPickupSpawnResult(
                    RewardPickupSpawnStatus.Rejected,
                    null,
                    null,
                    "RAP commit returned no result.");
            }

            bool accepted = commitResult.Status == RewardApplicationResultStatus.Generated
                || commitResult.Status == RewardApplicationResultStatus.ExactDuplicateNoChange;
            if (!accepted)
            {
                return new RewardPickupSpawnResult(
                    RewardPickupSpawnStatus.Rejected,
                    null,
                    commitResult,
                    "RAP rejected pickup commitment: "
                        + (commitResult.RejectionCode ?? commitResult.Status.ToString()));
            }

            if (command.GeneratedReward.Disposition == RewardResultDisposition.ExplicitNoDrop)
            {
                return new RewardPickupSpawnResult(
                    RewardPickupSpawnStatus.ExplicitNoDrop,
                    null,
                    commitResult,
                    "Profile resolved to explicit no-drop; no physical pickup was projected.");
            }

            RewardPickupPayload pickupPayload = RewardPickupPayload.Create(command, category);
            RewardPickup2D existing;
            if (spawnedPickups.TryGetValue(pickupPayload.PickupStableId, out existing))
            {
                if (existing != null)
                {
                    return new RewardPickupSpawnResult(
                        RewardPickupSpawnStatus.ExactDuplicateNoChange,
                        existing,
                        commitResult,
                        "Exact source callback reused the existing physical pickup projection.");
                }

                spawnedPickups.Remove(pickupPayload.PickupStableId);
            }

            RewardPickup2D pickup;
            try
            {
                pickup = CreatePickupInstance();
                pickup.Configure(pickupPayload, lifecycleAuthority, restartScope);
            }
            catch (Exception exception)
            {
                return new RewardPickupSpawnResult(
                    RewardPickupSpawnStatus.Rejected,
                    null,
                    commitResult,
                    "Physical pickup projection failed: " + exception.Message);
            }

            spawnedPickups.Add(pickupPayload.PickupStableId, pickup);
            return new RewardPickupSpawnResult(
                commitResult.Status == RewardApplicationResultStatus.ExactDuplicateNoChange
                    ? RewardPickupSpawnStatus.ExactDuplicateNoChange
                    : RewardPickupSpawnStatus.Spawned,
                pickup,
                commitResult,
                "Physical pickup projected from deterministic SRC/RAP identities.");
        }

        private RewardPickup2D CreatePickupInstance()
        {
            Transform parent = pickupParent == null ? transform : pickupParent;
            if (pickupPrefab != null)
            {
                return Instantiate(pickupPrefab, transform.position, Quaternion.identity, parent);
            }

            GameObject value = new GameObject("RewardPickupProjection");
            value.transform.SetParent(parent, false);
            value.transform.position = transform.position;
            return value.AddComponent<RewardPickup2D>();
        }

        private static RewardSourceSubmissionResult RejectedSubmission(string diagnostic)
        {
            return new RewardSourceSubmissionResult(
                RewardSourceSubmissionStatus.Rejected,
                diagnostic);
        }
    }
}

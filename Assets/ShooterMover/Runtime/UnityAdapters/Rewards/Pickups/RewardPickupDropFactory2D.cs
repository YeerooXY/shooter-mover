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
        private RewardGenerationServiceV1 generator;
        private ProgressionContext progressionContext;
        private IRewardPickupEquipmentPayloadResolverV1 equipmentResolver;
        private RewardPickupSpawnResultV1 lastSpawnResult;

        public int SpawnedPickupCount { get { return spawnedPickups.Count; } }
        public RewardPickupSpawnResultV1 LastSpawnResult { get { return lastSpawnResult; } }

        public void ConfigureRuntime(
            RewardGenerationServiceV1 generator,
            ProgressionContext progressionContext,
            ulong rootSeed,
            int algorithmVersion,
            MonoBehaviour lifecycleAuthority,
            GameplaySceneScope2D restartScope,
            IRewardPickupEquipmentPayloadResolverV1 equipmentResolver = null)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.progressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (algorithmVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            }

            if (!(lifecycleAuthority is IRewardPickupLifecycleAuthorityV1))
            {
                throw new ArgumentException(
                    "Lifecycle authority must implement IRewardPickupLifecycleAuthorityV1.",
                    nameof(lifecycleAuthority));
            }

            this.rootSeed = rootSeed;
            this.algorithmVersion = algorithmVersion;
            this.lifecycleAuthority = lifecycleAuthority;
            this.restartScope = restartScope;
            this.equipmentResolver = equipmentResolver;
        }

        public void ConfigureForTests(
            RewardGenerationServiceV1 generator,
            ProgressionContext progressionContext,
            ulong rootSeed,
            int algorithmVersion,
            MonoBehaviour lifecycleAuthority,
            GameplaySceneScope2D restartScope,
            RewardPickup2D pickupPrefab = null,
            IRewardPickupEquipmentPayloadResolverV1 equipmentResolver = null)
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

            IRewardPickupLifecycleAuthorityV1 authority =
                lifecycleAuthority as IRewardPickupLifecycleAuthorityV1;
            if (generator == null || progressionContext == null || authority == null)
            {
                return RejectedSubmission(
                    "Pickup drop factory is missing generator, progression context, or RAP authority.");
            }

            RewardGenerationResultEnvelopeV1 generation;
            try
            {
                generation = generator.GenerateReward(
                    RewardGenerationRequestV1.Create(
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

            IReadOnlyList<RewardGrantApplicationPayloadV1> payloads;
            string rejectionCode;
            if (!RewardPickupPayloadBuilderV1.TryBuild(
                preview,
                generation.Result,
                equipmentResolver,
                out payloads,
                out rejectionCode))
            {
                return RejectedSubmission(rejectionCode);
            }

            RewardCommitCommandV1 command;
            try
            {
                command = RewardCommitCommandV1.Create(
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
                case RewardPickupSpawnStatusV1.Spawned:
                    return new RewardSourceSubmissionResult(
                        RewardSourceSubmissionStatus.Accepted,
                        lastSpawnResult.Diagnostic);
                case RewardPickupSpawnStatusV1.ExactDuplicateNoChange:
                    return new RewardSourceSubmissionResult(
                        RewardSourceSubmissionStatus.ExactDuplicateNoChange,
                        lastSpawnResult.Diagnostic);
                case RewardPickupSpawnStatusV1.ExplicitNoDrop:
                    return new RewardSourceSubmissionResult(
                        lastSpawnResult.AuthorityResult != null
                            && lastSpawnResult.AuthorityResult.Status
                                == RewardApplicationResultStatusV1.ExactDuplicateNoChange
                            ? RewardSourceSubmissionStatus.ExactDuplicateNoChange
                            : RewardSourceSubmissionStatus.Accepted,
                        lastSpawnResult.Diagnostic);
                default:
                    return RejectedSubmission(lastSpawnResult.Diagnostic);
            }
        }

        public RewardPickupSpawnResultV1 SpawnForced(
            RewardCommitCommandV1 command,
            RewardPickupCategoryV1? category = null)
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

        private RewardPickupSpawnResultV1 CommitAndProject(
            RewardCommitCommandV1 command,
            RewardPickupCategoryV1? category)
        {
            IRewardPickupLifecycleAuthorityV1 authority =
                lifecycleAuthority as IRewardPickupLifecycleAuthorityV1;
            if (command == null || authority == null)
            {
                return new RewardPickupSpawnResultV1(
                    RewardPickupSpawnStatusV1.Rejected,
                    null,
                    null,
                    "Forced drop requires a prepared commit and RAP lifecycle authority.");
            }

            RewardApplicationResultV1 commitResult = authority.Commit(command);
            if (commitResult == null)
            {
                return new RewardPickupSpawnResultV1(
                    RewardPickupSpawnStatusV1.Rejected,
                    null,
                    null,
                    "RAP commit returned no result.");
            }

            bool accepted = commitResult.Status == RewardApplicationResultStatusV1.Generated
                || commitResult.Status == RewardApplicationResultStatusV1.ExactDuplicateNoChange;
            if (!accepted)
            {
                return new RewardPickupSpawnResultV1(
                    RewardPickupSpawnStatusV1.Rejected,
                    null,
                    commitResult,
                    "RAP rejected pickup commitment: "
                        + (commitResult.RejectionCode ?? commitResult.Status.ToString()));
            }

            if (command.GeneratedReward.Disposition == RewardResultDispositionV1.ExplicitNoDrop)
            {
                return new RewardPickupSpawnResultV1(
                    RewardPickupSpawnStatusV1.ExplicitNoDrop,
                    null,
                    commitResult,
                    "Profile resolved to explicit no-drop; no physical pickup was projected.");
            }

            RewardPickupPayloadV1 pickupPayload = RewardPickupPayloadV1.Create(command, category);
            RewardPickup2D existing;
            if (spawnedPickups.TryGetValue(pickupPayload.PickupStableId, out existing))
            {
                if (existing != null)
                {
                    return new RewardPickupSpawnResultV1(
                        RewardPickupSpawnStatusV1.ExactDuplicateNoChange,
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
                return new RewardPickupSpawnResultV1(
                    RewardPickupSpawnStatusV1.Rejected,
                    null,
                    commitResult,
                    "Physical pickup projection failed: " + exception.Message);
            }

            spawnedPickups.Add(pickupPayload.PickupStableId, pickup);
            return new RewardPickupSpawnResultV1(
                commitResult.Status == RewardApplicationResultStatusV1.ExactDuplicateNoChange
                    ? RewardPickupSpawnStatusV1.ExactDuplicateNoChange
                    : RewardPickupSpawnStatusV1.Spawned,
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

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
    public sealed class LootSpawner :
        MonoBehaviour,
        ILootSourceOperationSink
    {
        [SerializeField] private LootPickup pickupPrefab;
        [SerializeField] private Transform pickupParent;
        [SerializeField] private MonoBehaviour lifecycleAuthority;
        [SerializeField] private GameplayScene restartScope;
        [SerializeField] private ulong rootSeed = 1UL;
        [SerializeField] private int algorithmVersion = 1;

        private readonly Dictionary<StableId, LootPickup> spawnedPickups =
            new Dictionary<StableId, LootPickup>();
        private RewardGenerationActions generator;
        private ProgressionContext progressionContext;
        private ILootPickupEquipmentPayloadResolver equipmentResolver;
        private LootPickupSpawnResult lastSpawnResult;

        public int SpawnedPickupCount { get { return spawnedPickups.Count; } }
        public LootPickupSpawnResult LastSpawnResult { get { return lastSpawnResult; } }

        public void ConfigureRuntime(
            RewardGenerationActions generator,
            ProgressionContext progressionContext,
            ulong rootSeed,
            int algorithmVersion,
            MonoBehaviour lifecycleAuthority,
            GameplayScene restartScope,
            ILootPickupEquipmentPayloadResolver equipmentResolver = null)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.progressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (algorithmVersion <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            }

            if (!(lifecycleAuthority is ILootPickupLifecycleState))
            {
                throw new ArgumentException(
                    "Lifecycle authority must implement ILootPickupLifecycleState.",
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
            GameplayScene restartScope,
            LootPickup pickupPrefab = null,
            ILootPickupEquipmentPayloadResolver equipmentResolver = null)
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

        public LootSourceSubmissionResult Submit(LootSourceResolvedPreview preview)
        {
            if (preview == null)
            {
                return RejectedSubmission("Reward source preview is null.");
            }

            ILootPickupLifecycleState authority =
                lifecycleAuthority as ILootPickupLifecycleState;
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
            if (!LootPickupPayloadBuilder.TryBuild(
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
                case LootPickupSpawnStatus.Spawned:
                    return new LootSourceSubmissionResult(
                        LootSourceSubmissionStatus.Accepted,
                        lastSpawnResult.Diagnostic);
                case LootPickupSpawnStatus.ExactDuplicateNoChange:
                    return new LootSourceSubmissionResult(
                        LootSourceSubmissionStatus.ExactDuplicateNoChange,
                        lastSpawnResult.Diagnostic);
                case LootPickupSpawnStatus.ExplicitNoDrop:
                    return new LootSourceSubmissionResult(
                        lastSpawnResult.AuthorityResult != null
                            && lastSpawnResult.AuthorityResult.Status
                                == RewardApplicationResultStatus.ExactDuplicateNoChange
                            ? LootSourceSubmissionStatus.ExactDuplicateNoChange
                            : LootSourceSubmissionStatus.Accepted,
                        lastSpawnResult.Diagnostic);
                default:
                    return RejectedSubmission(lastSpawnResult.Diagnostic);
            }
        }

        public LootPickupSpawnResult SpawnForced(
            RewardCommitCommand command,
            LootPickupCategory? category = null)
        {
            lastSpawnResult = CommitAndProject(command, category);
            return lastSpawnResult;
        }

        public bool TryGetPickup(StableId pickupStableId, out LootPickup pickup)
        {
            pickup = null;
            return pickupStableId != null
                && spawnedPickups.TryGetValue(pickupStableId, out pickup);
        }

        private LootPickupSpawnResult CommitAndProject(
            RewardCommitCommand command,
            LootPickupCategory? category)
        {
            ILootPickupLifecycleState authority =
                lifecycleAuthority as ILootPickupLifecycleState;
            if (command == null || authority == null)
            {
                return new LootPickupSpawnResult(
                    LootPickupSpawnStatus.Rejected,
                    null,
                    null,
                    "Forced drop requires a prepared commit and RAP lifecycle authority.");
            }

            RewardApplicationResult commitResult = authority.Commit(command);
            if (commitResult == null)
            {
                return new LootPickupSpawnResult(
                    LootPickupSpawnStatus.Rejected,
                    null,
                    null,
                    "RAP commit returned no result.");
            }

            bool accepted = commitResult.Status == RewardApplicationResultStatus.Generated
                || commitResult.Status == RewardApplicationResultStatus.ExactDuplicateNoChange;
            if (!accepted)
            {
                return new LootPickupSpawnResult(
                    LootPickupSpawnStatus.Rejected,
                    null,
                    commitResult,
                    "RAP rejected pickup commitment: "
                        + (commitResult.RejectionCode ?? commitResult.Status.ToString()));
            }

            if (command.GeneratedReward.Disposition == RewardResultDisposition.ExplicitNoDrop)
            {
                return new LootPickupSpawnResult(
                    LootPickupSpawnStatus.ExplicitNoDrop,
                    null,
                    commitResult,
                    "Profile resolved to explicit no-drop; no physical pickup was projected.");
            }

            LootPickupPayload pickupPayload = LootPickupPayload.Create(command, category);
            LootPickup existing;
            if (spawnedPickups.TryGetValue(pickupPayload.PickupStableId, out existing))
            {
                if (existing != null)
                {
                    return new LootPickupSpawnResult(
                        LootPickupSpawnStatus.ExactDuplicateNoChange,
                        existing,
                        commitResult,
                        "Exact source callback reused the existing physical pickup projection.");
                }

                spawnedPickups.Remove(pickupPayload.PickupStableId);
            }

            LootPickup pickup;
            try
            {
                pickup = CreatePickupInstance();
                pickup.Configure(pickupPayload, lifecycleAuthority, restartScope);
            }
            catch (Exception exception)
            {
                return new LootPickupSpawnResult(
                    LootPickupSpawnStatus.Rejected,
                    null,
                    commitResult,
                    "Physical pickup projection failed: " + exception.Message);
            }

            spawnedPickups.Add(pickupPayload.PickupStableId, pickup);
            return new LootPickupSpawnResult(
                commitResult.Status == RewardApplicationResultStatus.ExactDuplicateNoChange
                    ? LootPickupSpawnStatus.ExactDuplicateNoChange
                    : LootPickupSpawnStatus.Spawned,
                pickup,
                commitResult,
                "Physical pickup projected from deterministic SRC/RAP identities.");
        }

        private LootPickup CreatePickupInstance()
        {
            Transform parent = pickupParent == null ? transform : pickupParent;
            if (pickupPrefab != null)
            {
                return Instantiate(pickupPrefab, transform.position, Quaternion.identity, parent);
            }

            GameObject value = new GameObject("LootPickupProjection");
            value.transform.SetParent(parent, false);
            value.transform.position = transform.position;
            return value.AddComponent<LootPickup>();
        }

        private static LootSourceSubmissionResult RejectedSubmission(string diagnostic)
        {
            return new LootSourceSubmissionResult(
                LootSourceSubmissionStatus.Rejected,
                diagnostic);
        }
    }
}

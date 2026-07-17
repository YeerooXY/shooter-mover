using System;
using ShooterMover.Application.Rewards.GameplayDrops;
using ShooterMover.Content.Definitions.Rewards.GameplayDrops;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.GameplayDrops
{
    /// <summary>
    /// Reusable source adapter for a terminal gameplay fact. It resolves one immutable
    /// operation and submits it to an existing reward sink. It does not inspect the host
    /// type, generate rewards, create authority state, or mutate player value.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayDropSource2D : MonoBehaviour, IGameplayDropSourceV1
    {
        [SerializeField] private PlacedObjectAuthoring2D placedObject;
        [SerializeField] private GameplayDropProfileDefinitionAsset dropProfile;
        [SerializeField] private GameplayDropOverrideAuthoring manualOverride =
            new GameplayDropOverrideAuthoring();
        [SerializeField] private MonoBehaviour operationSink;

        private GameplayDropOperationV1 resolvedOperation;
        private GameplayDropResolutionResult lastResolution;

        public GameplayDropResolutionResult LastResolution
        {
            get { return lastResolution; }
        }

        public GameplayDropResolutionResult ResolveGameplayDrop()
        {
            PlacedObjectAuthoring2D resolvedPlaced = placedObject;
            if (resolvedPlaced == null)
            {
                resolvedPlaced = GetComponent<PlacedObjectAuthoring2D>();
            }

            if (resolvedPlaced == null)
            {
                return SetFailure(
                    GameplayDropResolutionStatus.MissingPlacedObject,
                    "Gameplay drop source requires an assigned or co-located PlacedObjectAuthoring2D.");
            }

            SceneScopeBindingResult binding = resolvedPlaced.TryBind();
            if (!binding.IsBound || resolvedPlaced.BoundScope == null)
            {
                return SetFailure(
                    GameplayDropResolutionStatus.PlacedObjectBindingFailed,
                    binding.Diagnostic);
            }

            if (dropProfile == null)
            {
                return SetFailure(
                    GameplayDropResolutionStatus.MissingProfile,
                    "Gameplay drop source requires a gameplay drop profile.");
            }

            ShooterMover.Domain.Rewards.Model.RewardProfileV1 inheritedProfile;
            try
            {
                inheritedProfile = dropProfile.BuildProfile();
            }
            catch (Exception exception)
            {
                return SetFailure(
                    GameplayDropResolutionStatus.InvalidProfile,
                    exception.Message);
            }

            GameplayDropOverrideV1 resolvedOverride;
            try
            {
                resolvedOverride = (manualOverride
                    ?? GameplayDropOverrideAuthoring.Default(
                        "gameplay-drop-override.default")).Build();
            }
            catch (Exception exception)
            {
                return SetFailure(
                    GameplayDropResolutionStatus.InvalidOverride,
                    exception.Message);
            }

            GameplayDropOperationV1 operation;
            try
            {
                operation = GameplayDropOperationFactoryV1.Create(
                    resolvedPlaced.BoundScope.RunId,
                    resolvedPlaced.ResolvedIdentity.Value,
                    inheritedProfile,
                    resolvedOverride);
            }
            catch (Exception exception)
            {
                return SetFailure(
                    GameplayDropResolutionStatus.InvalidOverride,
                    exception.Message);
            }

            if (resolvedOperation != null)
            {
                RewardOperationIdentityComparisonV1 comparison =
                    RewardOperationIdentityV1.Classify(
                        resolvedOperation.OperationRequest,
                        operation.OperationRequest);
                if (comparison == RewardOperationIdentityComparisonV1.ConflictingDuplicate)
                {
                    return SetFailure(
                        GameplayDropResolutionStatus.ConflictingResolvedOperation,
                        "The gameplay drop operation was already resolved with a different payload.");
                }

                if (comparison == RewardOperationIdentityComparisonV1.ExactDuplicateNoChange)
                {
                    lastResolution = GameplayDropResolutionResult.Resolved(
                        resolvedOperation,
                        BuildSourcePreview(resolvedOperation));
                    return lastResolution;
                }
            }

            placedObject = resolvedPlaced;
            resolvedOperation = operation;
            lastResolution = GameplayDropResolutionResult.Resolved(
                operation,
                BuildSourcePreview(operation));
            return lastResolution;
        }

        public RewardSourceSubmissionResult SubmitGameplayDrop()
        {
            GameplayDropResolutionResult resolution = ResolveGameplayDrop();
            if (!resolution.IsResolved)
            {
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Rejected,
                    resolution.Diagnostic);
            }

            IRewardSourceOperationSink sink = operationSink as IRewardSourceOperationSink;
            if (sink == null)
            {
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Rejected,
                    "Gameplay drop operation sink is missing or incompatible.");
            }

            return sink.Submit(resolution.SourcePreview)
                ?? new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Rejected,
                    "Gameplay drop operation sink returned no result.");
        }

        public void ConfigureForTests(
            PlacedObjectAuthoring2D placedObject,
            GameplayDropProfileDefinitionAsset dropProfile,
            GameplayDropOverrideAuthoring manualOverride,
            MonoBehaviour operationSink)
        {
            this.placedObject = placedObject;
            this.dropProfile = dropProfile;
            this.manualOverride = manualOverride
                ?? GameplayDropOverrideAuthoring.Default(
                    "gameplay-drop-override.default");
            this.operationSink = operationSink;
            resolvedOperation = null;
            lastResolution = null;
        }

        private static RewardSourceResolvedPreview BuildSourcePreview(
            GameplayDropOperationV1 operation)
        {
            return new RewardSourceResolvedPreview(
                MapMode(operation.AppliedOverride.Mode),
                operation.InheritedProfile,
                operation.ResolvedProfile,
                operation.OperationRequest,
                operation.RestartParticipantStableId,
                operation.Fingerprint);
        }

        private static RewardSourceOverrideAuthoringMode MapMode(
            GameplayDropOverrideModeV1 mode)
        {
            switch (mode)
            {
                case GameplayDropOverrideModeV1.Default:
                    return RewardSourceOverrideAuthoringMode.Inherit;
                case GameplayDropOverrideModeV1.ForcedNone:
                    return RewardSourceOverrideAuthoringMode.None;
                case GameplayDropOverrideModeV1.ForcedSpecificReward:
                    return RewardSourceOverrideAuthoringMode.Replace;
                case GameplayDropOverrideModeV1.AppendGuaranteedReward:
                    return RewardSourceOverrideAuthoringMode.AppendGuaranteed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private GameplayDropResolutionResult SetFailure(
            GameplayDropResolutionStatus status,
            string diagnostic)
        {
            lastResolution = GameplayDropResolutionResult.Failed(status, diagnostic);
            return lastResolution;
        }
    }
}

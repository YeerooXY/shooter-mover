using System;
using ShooterMover.Application.Rewards.LootDrops;
using ShooterMover.Content.Definitions.Rewards.LootDrops;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.LootDrops
{
    /// <summary>
    /// Reusable source adapter for a terminal gameplay fact. It resolves one immutable
    /// operation and submits it to an existing reward sink. It does not inspect the host
    /// type, generate rewards, create authority state, or mutate player value.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootEmitter : MonoBehaviour, ILootDropSource
    {
        [SerializeField] private PlacedObject placedObject;
        [SerializeField] private LootDropProfileDefinitionAsset dropProfile;
        [SerializeField] private LootDropOverrideAuthoring manualOverride =
            new LootDropOverrideAuthoring();
        [SerializeField] private MonoBehaviour operationSink;

        private LootDropOperation resolvedOperation;
        private LootDropResolutionResult lastResolution;

        public LootDropResolutionResult LastResolution
        {
            get { return lastResolution; }
        }

        public LootDropResolutionResult ResolveLootDrop()
        {
            PlacedObject resolvedPlaced = placedObject;
            if (resolvedPlaced == null)
            {
                resolvedPlaced = GetComponent<PlacedObject>();
            }

            if (resolvedPlaced == null)
            {
                return SetFailure(
                    LootDropResolutionStatus.MissingPlacedObject,
                    "Gameplay drop source requires an assigned or co-located PlacedObject.");
            }

            SceneScopeBindingResult binding = resolvedPlaced.TryBind();
            if (!binding.IsBound || resolvedPlaced.BoundScope == null)
            {
                return SetFailure(
                    LootDropResolutionStatus.PlacedObjectBindingFailed,
                    binding.Diagnostic);
            }

            if (dropProfile == null)
            {
                return SetFailure(
                    LootDropResolutionStatus.MissingProfile,
                    "Gameplay drop source requires a gameplay drop profile.");
            }

            ShooterMover.Domain.Rewards.Model.RewardProfile inheritedProfile;
            try
            {
                inheritedProfile = dropProfile.BuildProfile();
            }
            catch (Exception exception)
            {
                return SetFailure(
                    LootDropResolutionStatus.InvalidProfile,
                    exception.Message);
            }

            LootDropOverride resolvedOverride;
            try
            {
                resolvedOverride = (manualOverride
                    ?? LootDropOverrideAuthoring.Default(
                        "gameplay-drop-override.default")).Build();
            }
            catch (Exception exception)
            {
                return SetFailure(
                    LootDropResolutionStatus.InvalidOverride,
                    exception.Message);
            }

            LootDropOperation operation;
            try
            {
                operation = LootDropOperationFactory.Create(
                    resolvedPlaced.BoundScope.RunId,
                    resolvedPlaced.ResolvedIdentity.Value,
                    inheritedProfile,
                    resolvedOverride);
            }
            catch (Exception exception)
            {
                return SetFailure(
                    LootDropResolutionStatus.InvalidOverride,
                    exception.Message);
            }

            if (resolvedOperation != null)
            {
                RewardOperationIdentityComparison comparison =
                    RewardOperationIdentity.Classify(
                        resolvedOperation.OperationRequest,
                        operation.OperationRequest);
                if (comparison == RewardOperationIdentityComparison.ConflictingDuplicate)
                {
                    return SetFailure(
                        LootDropResolutionStatus.ConflictingResolvedOperation,
                        "The gameplay drop operation was already resolved with a different payload.");
                }

                if (comparison == RewardOperationIdentityComparison.ExactDuplicateNoChange)
                {
                    lastResolution = LootDropResolutionResult.Resolved(
                        resolvedOperation,
                        BuildSourcePreview(resolvedOperation));
                    return lastResolution;
                }
            }

            placedObject = resolvedPlaced;
            resolvedOperation = operation;
            lastResolution = LootDropResolutionResult.Resolved(
                operation,
                BuildSourcePreview(operation));
            return lastResolution;
        }

        public LootSourceSubmissionResult SubmitLootDrop()
        {
            LootDropResolutionResult resolution = ResolveLootDrop();
            if (!resolution.IsResolved)
            {
                return new LootSourceSubmissionResult(
                    LootSourceSubmissionStatus.Rejected,
                    resolution.Diagnostic);
            }

            ILootSourceOperationSink sink = operationSink as ILootSourceOperationSink;
            if (sink == null)
            {
                return new LootSourceSubmissionResult(
                    LootSourceSubmissionStatus.Rejected,
                    "Gameplay drop operation sink is missing or incompatible.");
            }

            return sink.Submit(resolution.SourcePreview)
                ?? new LootSourceSubmissionResult(
                    LootSourceSubmissionStatus.Rejected,
                    "Gameplay drop operation sink returned no result.");
        }

        public void ConfigureForTests(
            PlacedObject placedObject,
            LootDropProfileDefinitionAsset dropProfile,
            LootDropOverrideAuthoring manualOverride,
            MonoBehaviour operationSink)
        {
            this.placedObject = placedObject;
            this.dropProfile = dropProfile;
            this.manualOverride = manualOverride
                ?? LootDropOverrideAuthoring.Default(
                    "gameplay-drop-override.default");
            this.operationSink = operationSink;
            resolvedOperation = null;
            lastResolution = null;
        }

        private static LootSourceResolvedPreview BuildSourcePreview(
            LootDropOperation operation)
        {
            return new LootSourceResolvedPreview(
                MapMode(operation.AppliedOverride.Mode),
                operation.InheritedProfile,
                operation.ResolvedProfile,
                operation.OperationRequest,
                operation.RestartParticipantStableId,
                operation.Fingerprint);
        }

        private static LootSourceOverrideAuthoringMode MapMode(
            LootDropOverrideMode mode)
        {
            switch (mode)
            {
                case LootDropOverrideMode.Default:
                    return LootSourceOverrideAuthoringMode.Inherit;
                case LootDropOverrideMode.ForcedNone:
                    return LootSourceOverrideAuthoringMode.None;
                case LootDropOverrideMode.ForcedSpecificReward:
                    return LootSourceOverrideAuthoringMode.Replace;
                case LootDropOverrideMode.AppendGuaranteedReward:
                    return LootSourceOverrideAuthoringMode.AppendGuaranteed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        private LootDropResolutionResult SetFailure(
            LootDropResolutionStatus status,
            string diagnostic)
        {
            lastResolution = LootDropResolutionResult.Failed(status, diagnostic);
            return lastResolution;
        }
    }
}

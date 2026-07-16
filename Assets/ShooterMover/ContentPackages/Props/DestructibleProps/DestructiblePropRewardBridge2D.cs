using System;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Converts the first terminal destruction fact into one SRC-001 submission attempt.
    /// The bridge never owns reward generation, claim, wallet, or holdings truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructiblePropRewardBridge2D : MonoBehaviour
    {
        private DestructibleProp2D prop;
        private RewardSourceAuthoring2D rewardSource;
        private bool submitted;
        private int submissionCount;
        private RewardSourceSubmissionResult lastSubmission;

        public bool IsConfigured => prop != null && rewardSource != null;
        public bool HasSubmitted => submitted;
        public int SubmissionCount => submissionCount;
        public RewardSourceSubmissionResult LastSubmission => lastSubmission;

        public void Configure(
            DestructibleProp2D configuredProp,
            RewardSourceAuthoring2D configuredRewardSource)
        {
            if (IsConfigured)
            {
                if (ReferenceEquals(prop, configuredProp)
                    && ReferenceEquals(rewardSource, configuredRewardSource))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Destructible prop reward bridge is already configured.");
            }

            prop = configuredProp
                ?? throw new ArgumentNullException(nameof(configuredProp));
            rewardSource = configuredRewardSource
                ?? throw new ArgumentNullException(nameof(configuredRewardSource));
            prop.Destroyed += HandleDestroyed;
        }

        private void HandleDestroyed(DestructiblePropDestructionResult ignored)
        {
            if (submitted || rewardSource == null)
            {
                return;
            }

            submitted = true;
            submissionCount++;
            try
            {
                lastSubmission = rewardSource.SubmitResolution();
            }
            catch (Exception exception)
            {
                lastSubmission = new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Rejected,
                    "Reward source submission threw: " + exception.Message);
            }
        }

        private void OnDestroy()
        {
            if (prop != null)
            {
                prop.Destroyed -= HandleDestroyed;
            }

            prop = null;
            rewardSource = null;
        }
    }
}

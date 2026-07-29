using System;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.Breakables
{
    /// <summary>
    /// Converts the first terminal destruction fact into one SRC-001 submission attempt.
    /// The bridge never owns reward generation, claim, wallet, or holdings truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreakableLoot : MonoBehaviour
    {
        private Breakable prop;
        private LootSourceSetup rewardSource;
        private bool submitted;
        private int submissionCount;
        private LootSourceSubmissionResult lastSubmission;

        public bool IsConfigured => prop != null && rewardSource != null;
        public bool HasSubmitted => submitted;
        public int SubmissionCount => submissionCount;
        public LootSourceSubmissionResult LastSubmission => lastSubmission;

        public void Configure(
            Breakable configuredProp,
            LootSourceSetup configuredLootSource)
        {
            if (IsConfigured)
            {
                if (ReferenceEquals(prop, configuredProp)
                    && ReferenceEquals(rewardSource, configuredLootSource))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Destructible prop reward bridge is already configured.");
            }

            prop = configuredProp
                ?? throw new ArgumentNullException(nameof(configuredProp));
            rewardSource = configuredLootSource
                ?? throw new ArgumentNullException(nameof(configuredLootSource));
            prop.Destroyed += HandleDestroyed;
        }

        private void HandleDestroyed(BreakableDestructionResult ignored)
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
                lastSubmission = new LootSourceSubmissionResult(
                    LootSourceSubmissionStatus.Rejected,
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

using System;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Pickups
{
    /// <summary>
    /// Unity-facing RAP adapter. It coordinates projection and claim identities, but
    /// delegates all value mutation and exact-once truth to RewardApplicationActions.
    /// It never calls money, scrap, or holdings services directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardPickupApplicationState2D :
        MonoBehaviour,
        IRewardPickupLifecycleState
    {
        [SerializeField] private string moneyAuthorityId = "authority.money";
        [SerializeField] private string scrapAuthorityId = "authority.scrap";
        [SerializeField] private string holdingsAuthorityId = "authority.holdings";

        private RewardApplicationActions service;
        private StableId parsedMoneyAuthorityId;
        private StableId parsedScrapAuthorityId;
        private StableId parsedHoldingsAuthorityId;
        private string configurationError;

        public bool IsConfigured
        {
            get
            {
                EnsureParsedAuthorityIds();
                return service != null && string.IsNullOrEmpty(configurationError);
            }
        }

        public string ConfigurationError
        {
            get
            {
                EnsureParsedAuthorityIds();
                if (service == null && string.IsNullOrEmpty(configurationError))
                {
                    return "RewardApplicationActions has not been injected.";
                }

                return configurationError ?? string.Empty;
            }
        }

        public void ConfigureRuntime(
            RewardApplicationActions service,
            StableId moneyAuthorityId,
            StableId scrapAuthorityId,
            StableId holdingsAuthorityId)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            parsedMoneyAuthorityId = moneyAuthorityId
                ?? throw new ArgumentNullException(nameof(moneyAuthorityId));
            parsedScrapAuthorityId = scrapAuthorityId
                ?? throw new ArgumentNullException(nameof(scrapAuthorityId));
            parsedHoldingsAuthorityId = holdingsAuthorityId
                ?? throw new ArgumentNullException(nameof(holdingsAuthorityId));
            this.moneyAuthorityId = parsedMoneyAuthorityId.ToString();
            this.scrapAuthorityId = parsedScrapAuthorityId.ToString();
            this.holdingsAuthorityId = parsedHoldingsAuthorityId.ToString();
            configurationError = null;
        }

        public void ConfigureForTests(
            RewardApplicationActions service,
            StableId moneyAuthorityId,
            StableId scrapAuthorityId,
            StableId holdingsAuthorityId)
        {
            ConfigureRuntime(
                service,
                moneyAuthorityId,
                scrapAuthorityId,
                holdingsAuthorityId);
        }

        public RewardApplicationResult Commit(RewardCommitCommand command)
        {
            if (!IsConfigured || command == null)
            {
                return null;
            }

            return service.Commit(command);
        }

        public RewardPickupCollectResult Collect(
            RewardPickupPayload payload,
            StableId claimantStableId)
        {
            if (!IsConfigured)
            {
                return new RewardPickupCollectResult(
                    RewardPickupCollectStatus.Invalid,
                    null,
                    ConfigurationError);
            }

            if (payload == null || claimantStableId == null)
            {
                return new RewardPickupCollectResult(
                    RewardPickupCollectStatus.Invalid,
                    null,
                    "Pickup payload and claimant identity are required.");
            }

            RewardApplicationResult projection = service.Project(
                RewardProjectCommand.Create(
                    payload.ProjectionStableId,
                    payload.CommitCommand.CommitmentStableId,
                    payload.PickupStableId));
            if (IsAppliedSnapshot(projection))
            {
                return AlreadyCollected(projection);
            }

            if (projection.Status != RewardApplicationResultStatus.Projected
                && projection.Status != RewardApplicationResultStatus.ExactDuplicateNoChange)
            {
                return Rejected(projection, "Pickup projection was rejected.");
            }

            StableId claimStableId = payload.DeriveClaimStableId(claimantStableId);
            RewardApplicationResult claim = service.Claim(
                RewardClaimCommand.Create(
                    claimStableId,
                    payload.CommitCommand.CommitmentStableId,
                    claimantStableId,
                    parsedMoneyAuthorityId,
                    parsedScrapAuthorityId,
                    parsedHoldingsAuthorityId));

            if (claim.Status == RewardApplicationResultStatus.ExactDuplicateNoChange
                && claim.CommitmentState == RewardCommitmentState.Claimed)
            {
                claim = service.Retry(
                    RewardRetryClaimCommand.Create(
                        payload.CommitCommand.CommitmentStableId,
                        claimStableId));
            }

            switch (claim.Status)
            {
                case RewardApplicationResultStatus.Applied:
                    return new RewardPickupCollectResult(
                        RewardPickupCollectStatus.Collected,
                        claim,
                        "Reward pickup was atomically applied through RAP.");
                case RewardApplicationResultStatus.AlreadyAppliedNoChange:
                    return AlreadyCollected(claim);
                case RewardApplicationResultStatus.ExactDuplicateNoChange:
                    if (IsAppliedSnapshot(claim))
                    {
                        return AlreadyCollected(claim);
                    }

                    return new RewardPickupCollectResult(
                        RewardPickupCollectStatus.PendingRetry,
                        claim,
                        "The exact claim already exists and remains pending.");
                case RewardApplicationResultStatus.ClaimedPendingApplication:
                    return new RewardPickupCollectResult(
                        RewardPickupCollectStatus.PendingRetry,
                        claim,
                        "RAP retained the claim for deterministic retry.");
                default:
                    return Rejected(claim, "Reward pickup claim was rejected.");
            }
        }

        private static bool IsAppliedSnapshot(RewardApplicationResult result)
        {
            return result != null
                && result.CommitmentState == RewardCommitmentState.Applied;
        }

        private static RewardPickupCollectResult AlreadyCollected(
            RewardApplicationResult result)
        {
            return new RewardPickupCollectResult(
                RewardPickupCollectStatus.AlreadyCollectedNoChange,
                result,
                "The commitment was already applied; no additional value was granted.");
        }

        private static RewardPickupCollectResult Rejected(
            RewardApplicationResult result,
            string diagnostic)
        {
            string suffix = result == null || string.IsNullOrEmpty(result.RejectionCode)
                ? string.Empty
                : " Rejection: " + result.RejectionCode + ".";
            return new RewardPickupCollectResult(
                RewardPickupCollectStatus.Rejected,
                result,
                diagnostic + suffix);
        }

        private void EnsureParsedAuthorityIds()
        {
            if (parsedMoneyAuthorityId != null
                && parsedScrapAuthorityId != null
                && parsedHoldingsAuthorityId != null)
            {
                return;
            }

            configurationError = null;
            if (!StableId.TryParse(moneyAuthorityId, out parsedMoneyAuthorityId))
            {
                configurationError = "Money authority ID is not a canonical StableId.";
                return;
            }

            if (!StableId.TryParse(scrapAuthorityId, out parsedScrapAuthorityId))
            {
                configurationError = "Scrap authority ID is not a canonical StableId.";
                return;
            }

            if (!StableId.TryParse(holdingsAuthorityId, out parsedHoldingsAuthorityId))
            {
                configurationError = "Holdings authority ID is not a canonical StableId.";
                return;
            }

            if (parsedMoneyAuthorityId == parsedScrapAuthorityId
                || parsedMoneyAuthorityId == parsedHoldingsAuthorityId
                || parsedScrapAuthorityId == parsedHoldingsAuthorityId)
            {
                configurationError = "Pickup destination authority identities must be distinct.";
            }
        }

        private void OnValidate()
        {
            parsedMoneyAuthorityId = null;
            parsedScrapAuthorityId = null;
            parsedHoldingsAuthorityId = null;
            configurationError = null;
        }
    }
}

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
    /// delegates all value mutation and exact-once truth to RewardApplicationServiceV1.
    /// It never calls money, scrap, or holdings services directly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardPickupApplicationAuthority2D :
        MonoBehaviour,
        IRewardPickupLifecycleAuthorityV1
    {
        [SerializeField] private string moneyAuthorityId = "authority.money";
        [SerializeField] private string scrapAuthorityId = "authority.scrap";
        [SerializeField] private string holdingsAuthorityId = "authority.holdings";

        private RewardApplicationServiceV1 service;
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
                    return "RewardApplicationServiceV1 has not been injected.";
                }

                return configurationError ?? string.Empty;
            }
        }

        public void ConfigureRuntime(
            RewardApplicationServiceV1 service,
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
            RewardApplicationServiceV1 service,
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

        public RewardApplicationResultV1 Commit(RewardCommitCommandV1 command)
        {
            if (!IsConfigured || command == null)
            {
                return null;
            }

            return service.Commit(command);
        }

        public RewardPickupCollectResultV1 Collect(
            RewardPickupPayloadV1 payload,
            StableId claimantStableId)
        {
            if (!IsConfigured)
            {
                return new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.Invalid,
                    null,
                    ConfigurationError);
            }

            if (payload == null || claimantStableId == null)
            {
                return new RewardPickupCollectResultV1(
                    RewardPickupCollectStatusV1.Invalid,
                    null,
                    "Pickup payload and claimant identity are required.");
            }

            RewardApplicationResultV1 projection = service.Project(
                RewardProjectCommandV1.Create(
                    payload.ProjectionStableId,
                    payload.CommitCommand.CommitmentStableId,
                    payload.PickupStableId));
            if (IsAppliedSnapshot(projection))
            {
                return AlreadyCollected(projection);
            }

            if (projection.Status != RewardApplicationResultStatusV1.Projected
                && projection.Status != RewardApplicationResultStatusV1.ExactDuplicateNoChange)
            {
                return Rejected(projection, "Pickup projection was rejected.");
            }

            StableId claimStableId = payload.DeriveClaimStableId(claimantStableId);
            RewardApplicationResultV1 claim = service.Claim(
                RewardClaimCommandV1.Create(
                    claimStableId,
                    payload.CommitCommand.CommitmentStableId,
                    claimantStableId,
                    parsedMoneyAuthorityId,
                    parsedScrapAuthorityId,
                    parsedHoldingsAuthorityId));

            if (claim.Status == RewardApplicationResultStatusV1.ExactDuplicateNoChange
                && claim.CommitmentState == RewardCommitmentStateV1.Claimed)
            {
                claim = service.Retry(
                    RewardRetryClaimCommandV1.Create(
                        payload.CommitCommand.CommitmentStableId,
                        claimStableId));
            }

            switch (claim.Status)
            {
                case RewardApplicationResultStatusV1.Applied:
                    return new RewardPickupCollectResultV1(
                        RewardPickupCollectStatusV1.Collected,
                        claim,
                        "Reward pickup was atomically applied through RAP.");
                case RewardApplicationResultStatusV1.AlreadyAppliedNoChange:
                    return AlreadyCollected(claim);
                case RewardApplicationResultStatusV1.ExactDuplicateNoChange:
                    if (IsAppliedSnapshot(claim))
                    {
                        return AlreadyCollected(claim);
                    }

                    return new RewardPickupCollectResultV1(
                        RewardPickupCollectStatusV1.PendingRetry,
                        claim,
                        "The exact claim already exists and remains pending.");
                case RewardApplicationResultStatusV1.ClaimedPendingApplication:
                    return new RewardPickupCollectResultV1(
                        RewardPickupCollectStatusV1.PendingRetry,
                        claim,
                        "RAP retained the claim for deterministic retry.");
                default:
                    return Rejected(claim, "Reward pickup claim was rejected.");
            }
        }

        private static bool IsAppliedSnapshot(RewardApplicationResultV1 result)
        {
            return result != null
                && result.CommitmentState == RewardCommitmentStateV1.Applied;
        }

        private static RewardPickupCollectResultV1 AlreadyCollected(
            RewardApplicationResultV1 result)
        {
            return new RewardPickupCollectResultV1(
                RewardPickupCollectStatusV1.AlreadyCollectedNoChange,
                result,
                "The commitment was already applied; no additional value was granted.");
        }

        private static RewardPickupCollectResultV1 Rejected(
            RewardApplicationResultV1 result,
            string diagnostic)
        {
            string suffix = result == null || string.IsNullOrEmpty(result.RejectionCode)
                ? string.Empty
                : " Rejection: " + result.RejectionCode + ".";
            return new RewardPickupCollectResultV1(
                RewardPickupCollectStatusV1.Rejected,
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

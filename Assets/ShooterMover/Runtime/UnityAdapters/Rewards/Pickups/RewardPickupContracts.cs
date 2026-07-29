using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Pickups
{
    public enum RewardPickupCategory
    {
        Money = 1,
        Scrap = 2,
        Strongbox = 3,
        Equipment = 4,
        Miscellaneous = 5,
    }

    public static class RewardPickupCategoryMap
    {
        public static RewardPickupCategory FromGrantKind(RewardGrantKind kind)
        {
            switch (kind)
            {
                case RewardGrantKind.Money:
                    return RewardPickupCategory.Money;
                case RewardGrantKind.Scrap:
                    return RewardPickupCategory.Scrap;
                case RewardGrantKind.Strongbox:
                    return RewardPickupCategory.Strongbox;
                case RewardGrantKind.EquipmentReference:
                    return RewardPickupCategory.Equipment;
                case RewardGrantKind.PremiumAmmo:
                case RewardGrantKind.Miscellaneous:
                    return RewardPickupCategory.Miscellaneous;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported reward grant kind.");
            }
        }

        public static RewardPickupCategory FromCommit(RewardCommitCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.GeneratedReward.Grants.Count == 0)
            {
                return RewardPickupCategory.Miscellaneous;
            }

            RewardPickupCategory category = FromGrantKind(command.GeneratedReward.Grants[0].Kind);
            for (int index = 1; index < command.GeneratedReward.Grants.Count; index++)
            {
                if (FromGrantKind(command.GeneratedReward.Grants[index].Kind) != category)
                {
                    return RewardPickupCategory.Miscellaneous;
                }
            }

            return category;
        }
    }

    [Serializable]
    public sealed class RewardPickupPresentationStyle
    {
        [SerializeField] private RewardPickupCategory category = RewardPickupCategory.Miscellaneous;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public RewardPickupPresentationStyle()
        {
        }

        public RewardPickupPresentationStyle(
            RewardPickupCategory category,
            Sprite sprite,
            Color tint,
            Vector3 localScale)
        {
            this.category = category;
            this.sprite = sprite;
            this.tint = tint;
            this.localScale = localScale;
        }

        public RewardPickupCategory Category { get { return category; } }
        public Sprite Sprite { get { return sprite; } }
        public Color Tint { get { return tint; } }
        public Vector3 LocalScale { get { return localScale; } }
    }

    /// <summary>
    /// Immutable projection of one complete RAP commitment as one physical pickup.
    /// Pickup, projection, claim, and restart identities are derived only from durable
    /// reward identities; names, scene paths, callback counts, and Unity instance IDs
    /// never participate.
    /// </summary>
    public sealed class RewardPickupPayload : IEquatable<RewardPickupPayload>
    {
        private readonly string canonicalText;

        private RewardPickupPayload(
            RewardCommitCommand commitCommand,
            RewardPickupCategory category)
        {
            CommitCommand = commitCommand ?? throw new ArgumentNullException(nameof(commitCommand));
            if (!Enum.IsDefined(typeof(RewardPickupCategory), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }

            Category = category;
            PickupStableId = RewardApplication.DeriveStableId(
                "rewardpickup",
                commitCommand.SourceOperationStableId.ToString(),
                commitCommand.CommitmentStableId.ToString());
            ProjectionStableId = RewardApplication.DeriveStableId(
                "rewardpickupprojection",
                PickupStableId.ToString());
            RestartParticipantStableId = RewardApplication.DeriveStableId(
                "rewardpickuprestart",
                PickupStableId.ToString());

            StringBuilder builder = new StringBuilder();
            RewardApplication.AppendToken(builder, "commit", commitCommand.Fingerprint);
            RewardApplication.AppendToken(
                builder,
                "category",
                ((int)Category).ToString(CultureInfo.InvariantCulture));
            RewardApplication.AppendToken(builder, "pickup", PickupStableId.ToString());
            RewardApplication.AppendToken(builder, "projection", ProjectionStableId.ToString());
            RewardApplication.AppendToken(
                builder,
                "restart_participant",
                RestartParticipantStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = RewardApplication.Fingerprint(canonicalText);
        }

        public RewardCommitCommand CommitCommand { get; }
        public RewardPickupCategory Category { get; }
        public StableId PickupStableId { get; }
        public StableId ProjectionStableId { get; }
        public StableId RestartParticipantStableId { get; }
        public string Fingerprint { get; }

        public static RewardPickupPayload Create(
            RewardCommitCommand commitCommand,
            RewardPickupCategory? category = null)
        {
            return new RewardPickupPayload(
                commitCommand,
                category ?? RewardPickupCategoryMap.FromCommit(commitCommand));
        }

        public StableId DeriveClaimStableId(StableId claimantStableId)
        {
            if (claimantStableId == null)
            {
                throw new ArgumentNullException(nameof(claimantStableId));
            }

            return RewardApplication.DeriveStableId(
                "rewardpickupclaim",
                PickupStableId.ToString(),
                claimantStableId.ToString());
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(RewardPickupPayload other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardPickupPayload);
        }

        public override int GetHashCode()
        {
            return RewardApplication.DeterministicHash(canonicalText);
        }
    }

    public enum RewardPickupCollectStatus
    {
        Collected = 1,
        AlreadyCollectedNoChange = 2,
        PendingRetry = 3,
        Rejected = 4,
        Invalid = 5,
    }

    public sealed class RewardPickupCollectResult
    {
        public RewardPickupCollectResult(
            RewardPickupCollectStatus status,
            RewardApplicationResult authorityResult,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RewardPickupCollectStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            AuthorityResult = authorityResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardPickupCollectStatus Status { get; }
        public RewardApplicationResult AuthorityResult { get; }
        public string Diagnostic { get; }

        public bool IsCollected
        {
            get
            {
                return Status == RewardPickupCollectStatus.Collected
                    || Status == RewardPickupCollectStatus.AlreadyCollectedNoChange;
            }
        }
    }

    public enum RewardPickupSpawnStatus
    {
        Spawned = 1,
        ExactDuplicateNoChange = 2,
        ExplicitNoDrop = 3,
        Rejected = 4,
    }

    public sealed class RewardPickupSpawnResult
    {
        public RewardPickupSpawnResult(
            RewardPickupSpawnStatus status,
            RewardPickup2D pickup,
            RewardApplicationResult authorityResult,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RewardPickupSpawnStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            Pickup = pickup;
            AuthorityResult = authorityResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardPickupSpawnStatus Status { get; }
        public RewardPickup2D Pickup { get; }
        public RewardApplicationResult AuthorityResult { get; }
        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == RewardPickupSpawnStatus.Spawned
                    || Status == RewardPickupSpawnStatus.ExactDuplicateNoChange
                    || Status == RewardPickupSpawnStatus.ExplicitNoDrop;
            }
        }
    }

    public interface IRewardPickupLifecycleState
    {
        RewardApplicationResult Commit(RewardCommitCommand command);

        RewardPickupCollectResult Collect(
            RewardPickupPayload payload,
            StableId claimantStableId);
    }

    /// <summary>
    /// Extension point used only when a profile emits equipment references. The
    /// resolver must return the exact immutable equipment instances retained by RAP.
    /// Forced drops can bypass this port by supplying a fully prepared commit command.
    /// </summary>
    public interface IRewardPickupEquipmentPayloadResolver
    {
        bool TryResolve(
            RewardSourceResolvedPreview source,
            RewardGrant grant,
            out IReadOnlyList<EquipmentInstance> equipmentInstances,
            out string rejectionCode);
    }

    internal static class RewardPickupPayloadBuilder
    {
        public static bool TryBuild(
            RewardSourceResolvedPreview source,
            RewardResult generatedReward,
            IRewardPickupEquipmentPayloadResolver equipmentResolver,
            out IReadOnlyList<RewardGrantApplicationPayload> payloads,
            out string rejectionCode)
        {
            if (source == null || generatedReward == null)
            {
                payloads = Array.Empty<RewardGrantApplicationPayload>();
                rejectionCode = "pickup-payload-input-null";
                return false;
            }

            List<RewardGrantApplicationPayload> values =
                new List<RewardGrantApplicationPayload>(generatedReward.Grants.Count);
            for (int grantIndex = 0; grantIndex < generatedReward.Grants.Count; grantIndex++)
            {
                RewardGrant grant = generatedReward.Grants[grantIndex];
                switch (grant.Kind)
                {
                    case RewardGrantKind.Money:
                    case RewardGrantKind.Scrap:
                    case RewardGrantKind.PremiumAmmo:
                    case RewardGrantKind.Miscellaneous:
                        values.Add(RewardGrantApplicationPayload.ForValue(grant));
                        break;
                    case RewardGrantKind.Strongbox:
                        if (grant.Quantity > int.MaxValue)
                        {
                            payloads = Array.Empty<RewardGrantApplicationPayload>();
                            rejectionCode = "pickup-strongbox-quantity-too-large";
                            return false;
                        }

                        List<StableId> strongboxIds = new List<StableId>((int)grant.Quantity);
                        for (long instanceIndex = 0L; instanceIndex < grant.Quantity; instanceIndex++)
                        {
                            strongboxIds.Add(RewardApplication.DeriveStableId(
                                "rewardpickupstrongbox",
                                source.OperationRequest.SourceOperationStableId.ToString(),
                                grant.GrantStableId.ToString(),
                                instanceIndex.ToString(CultureInfo.InvariantCulture)));
                        }

                        values.Add(RewardGrantApplicationPayload.ForStrongboxes(grant, strongboxIds));
                        break;
                    case RewardGrantKind.EquipmentReference:
                        if (equipmentResolver == null)
                        {
                            payloads = Array.Empty<RewardGrantApplicationPayload>();
                            rejectionCode = "pickup-equipment-resolver-missing";
                            return false;
                        }

                        IReadOnlyList<EquipmentInstance> equipment;
                        if (!equipmentResolver.TryResolve(
                            source,
                            grant,
                            out equipment,
                            out rejectionCode)
                            || equipment == null)
                        {
                            payloads = Array.Empty<RewardGrantApplicationPayload>();
                            rejectionCode = string.IsNullOrEmpty(rejectionCode)
                                ? "pickup-equipment-resolution-rejected"
                                : rejectionCode;
                            return false;
                        }

                        values.Add(RewardGrantApplicationPayload.ForEquipment(grant, equipment));
                        break;
                    default:
                        payloads = Array.Empty<RewardGrantApplicationPayload>();
                        rejectionCode = "pickup-grant-kind-unsupported";
                        return false;
                }
            }

            values.Sort(delegate(
                RewardGrantApplicationPayload left,
                RewardGrantApplicationPayload right)
            {
                return left.Grant.GrantStableId.CompareTo(right.Grant.GrantStableId);
            });
            payloads = new ReadOnlyCollection<RewardGrantApplicationPayload>(values);
            rejectionCode = null;
            return true;
        }
    }
}

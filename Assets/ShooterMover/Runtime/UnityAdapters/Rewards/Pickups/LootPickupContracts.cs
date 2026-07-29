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
    public enum LootPickupCategory
    {
        Money = 1,
        Scrap = 2,
        Strongbox = 3,
        Equipment = 4,
        Miscellaneous = 5,
    }

    public static class LootPickupCategoryMap
    {
        public static LootPickupCategory FromGrantKind(RewardGrantKind kind)
        {
            switch (kind)
            {
                case RewardGrantKind.Money:
                    return LootPickupCategory.Money;
                case RewardGrantKind.Scrap:
                    return LootPickupCategory.Scrap;
                case RewardGrantKind.Strongbox:
                    return LootPickupCategory.Strongbox;
                case RewardGrantKind.EquipmentReference:
                    return LootPickupCategory.Equipment;
                case RewardGrantKind.PremiumAmmo:
                case RewardGrantKind.Miscellaneous:
                    return LootPickupCategory.Miscellaneous;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported reward grant kind.");
            }
        }

        public static LootPickupCategory FromCommit(RewardCommitCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.GeneratedReward.Grants.Count == 0)
            {
                return LootPickupCategory.Miscellaneous;
            }

            LootPickupCategory category = FromGrantKind(command.GeneratedReward.Grants[0].Kind);
            for (int index = 1; index < command.GeneratedReward.Grants.Count; index++)
            {
                if (FromGrantKind(command.GeneratedReward.Grants[index].Kind) != category)
                {
                    return LootPickupCategory.Miscellaneous;
                }
            }

            return category;
        }
    }

    [Serializable]
    public sealed class LootPickupPresentationStyle
    {
        [SerializeField] private LootPickupCategory category = LootPickupCategory.Miscellaneous;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public LootPickupPresentationStyle()
        {
        }

        public LootPickupPresentationStyle(
            LootPickupCategory category,
            Sprite sprite,
            Color tint,
            Vector3 localScale)
        {
            this.category = category;
            this.sprite = sprite;
            this.tint = tint;
            this.localScale = localScale;
        }

        public LootPickupCategory Category { get { return category; } }
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
    public sealed class LootPickupPayload : IEquatable<LootPickupPayload>
    {
        private readonly string canonicalText;

        private LootPickupPayload(
            RewardCommitCommand commitCommand,
            LootPickupCategory category)
        {
            CommitCommand = commitCommand ?? throw new ArgumentNullException(nameof(commitCommand));
            if (!Enum.IsDefined(typeof(LootPickupCategory), category))
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
        public LootPickupCategory Category { get; }
        public StableId PickupStableId { get; }
        public StableId ProjectionStableId { get; }
        public StableId RestartParticipantStableId { get; }
        public string Fingerprint { get; }

        public static LootPickupPayload Create(
            RewardCommitCommand commitCommand,
            LootPickupCategory? category = null)
        {
            return new LootPickupPayload(
                commitCommand,
                category ?? LootPickupCategoryMap.FromCommit(commitCommand));
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

        public bool Equals(LootPickupPayload other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LootPickupPayload);
        }

        public override int GetHashCode()
        {
            return RewardApplication.DeterministicHash(canonicalText);
        }
    }

    public enum LootPickupCollectStatus
    {
        Collected = 1,
        AlreadyCollectedNoChange = 2,
        PendingRetry = 3,
        Rejected = 4,
        Invalid = 5,
    }

    public sealed class LootPickupCollectResult
    {
        public LootPickupCollectResult(
            LootPickupCollectStatus status,
            RewardApplicationResult authorityResult,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(LootPickupCollectStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            AuthorityResult = authorityResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootPickupCollectStatus Status { get; }
        public RewardApplicationResult AuthorityResult { get; }
        public string Diagnostic { get; }

        public bool IsCollected
        {
            get
            {
                return Status == LootPickupCollectStatus.Collected
                    || Status == LootPickupCollectStatus.AlreadyCollectedNoChange;
            }
        }
    }

    public enum LootPickupSpawnStatus
    {
        Spawned = 1,
        ExactDuplicateNoChange = 2,
        ExplicitNoDrop = 3,
        Rejected = 4,
    }

    public sealed class LootPickupSpawnResult
    {
        public LootPickupSpawnResult(
            LootPickupSpawnStatus status,
            LootPickup pickup,
            RewardApplicationResult authorityResult,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(LootPickupSpawnStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            Pickup = pickup;
            AuthorityResult = authorityResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootPickupSpawnStatus Status { get; }
        public LootPickup Pickup { get; }
        public RewardApplicationResult AuthorityResult { get; }
        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == LootPickupSpawnStatus.Spawned
                    || Status == LootPickupSpawnStatus.ExactDuplicateNoChange
                    || Status == LootPickupSpawnStatus.ExplicitNoDrop;
            }
        }
    }

    public interface ILootPickupLifecycleState
    {
        RewardApplicationResult Commit(RewardCommitCommand command);

        LootPickupCollectResult Collect(
            LootPickupPayload payload,
            StableId claimantStableId);
    }

    /// <summary>
    /// Extension point used only when a profile emits equipment references. The
    /// resolver must return the exact immutable equipment instances retained by RAP.
    /// Forced drops can bypass this port by supplying a fully prepared commit command.
    /// </summary>
    public interface ILootPickupEquipmentPayloadResolver
    {
        bool TryResolve(
            LootSourceResolvedPreview source,
            RewardGrant grant,
            out IReadOnlyList<EquipmentInstance> equipmentInstances,
            out string rejectionCode);
    }

    internal static class LootPickupPayloadBuilder
    {
        public static bool TryBuild(
            LootSourceResolvedPreview source,
            RewardResult generatedReward,
            ILootPickupEquipmentPayloadResolver equipmentResolver,
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

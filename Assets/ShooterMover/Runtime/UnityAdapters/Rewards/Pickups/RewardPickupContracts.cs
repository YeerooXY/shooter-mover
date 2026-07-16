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
    public enum RewardPickupCategoryV1
    {
        Money = 1,
        Scrap = 2,
        Strongbox = 3,
        Equipment = 4,
        Miscellaneous = 5,
    }

    public static class RewardPickupCategoryMapV1
    {
        public static RewardPickupCategoryV1 FromGrantKind(RewardGrantKindV1 kind)
        {
            switch (kind)
            {
                case RewardGrantKindV1.Money:
                    return RewardPickupCategoryV1.Money;
                case RewardGrantKindV1.Scrap:
                    return RewardPickupCategoryV1.Scrap;
                case RewardGrantKindV1.Strongbox:
                    return RewardPickupCategoryV1.Strongbox;
                case RewardGrantKindV1.EquipmentReference:
                    return RewardPickupCategoryV1.Equipment;
                case RewardGrantKindV1.PremiumAmmo:
                case RewardGrantKindV1.Miscellaneous:
                    return RewardPickupCategoryV1.Miscellaneous;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported reward grant kind.");
            }
        }

        public static RewardPickupCategoryV1 FromCommit(RewardCommitCommandV1 command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (command.GeneratedReward.Grants.Count == 0)
            {
                return RewardPickupCategoryV1.Miscellaneous;
            }

            RewardPickupCategoryV1 category = FromGrantKind(command.GeneratedReward.Grants[0].Kind);
            for (int index = 1; index < command.GeneratedReward.Grants.Count; index++)
            {
                if (FromGrantKind(command.GeneratedReward.Grants[index].Kind) != category)
                {
                    return RewardPickupCategoryV1.Miscellaneous;
                }
            }

            return category;
        }
    }

    [Serializable]
    public sealed class RewardPickupPresentationStyleV1
    {
        [SerializeField] private RewardPickupCategoryV1 category = RewardPickupCategoryV1.Miscellaneous;
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public RewardPickupPresentationStyleV1()
        {
        }

        public RewardPickupPresentationStyleV1(
            RewardPickupCategoryV1 category,
            Sprite sprite,
            Color tint,
            Vector3 localScale)
        {
            this.category = category;
            this.sprite = sprite;
            this.tint = tint;
            this.localScale = localScale;
        }

        public RewardPickupCategoryV1 Category { get { return category; } }
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
    public sealed class RewardPickupPayloadV1 : IEquatable<RewardPickupPayloadV1>
    {
        private readonly string canonicalText;

        private RewardPickupPayloadV1(
            RewardCommitCommandV1 commitCommand,
            RewardPickupCategoryV1 category)
        {
            CommitCommand = commitCommand ?? throw new ArgumentNullException(nameof(commitCommand));
            if (!Enum.IsDefined(typeof(RewardPickupCategoryV1), category))
            {
                throw new ArgumentOutOfRangeException(nameof(category));
            }

            Category = category;
            PickupStableId = RewardApplicationCanonicalV1.DeriveStableId(
                "rewardpickup",
                commitCommand.SourceOperationStableId.ToString(),
                commitCommand.CommitmentStableId.ToString());
            ProjectionStableId = RewardApplicationCanonicalV1.DeriveStableId(
                "rewardpickupprojection",
                PickupStableId.ToString());
            RestartParticipantStableId = RewardApplicationCanonicalV1.DeriveStableId(
                "rewardpickuprestart",
                PickupStableId.ToString());

            StringBuilder builder = new StringBuilder();
            RewardApplicationCanonicalV1.AppendToken(builder, "commit", commitCommand.Fingerprint);
            RewardApplicationCanonicalV1.AppendToken(
                builder,
                "category",
                ((int)Category).ToString(CultureInfo.InvariantCulture));
            RewardApplicationCanonicalV1.AppendToken(builder, "pickup", PickupStableId.ToString());
            RewardApplicationCanonicalV1.AppendToken(builder, "projection", ProjectionStableId.ToString());
            RewardApplicationCanonicalV1.AppendToken(
                builder,
                "restart_participant",
                RestartParticipantStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = RewardApplicationCanonicalV1.Fingerprint(canonicalText);
        }

        public RewardCommitCommandV1 CommitCommand { get; }
        public RewardPickupCategoryV1 Category { get; }
        public StableId PickupStableId { get; }
        public StableId ProjectionStableId { get; }
        public StableId RestartParticipantStableId { get; }
        public string Fingerprint { get; }

        public static RewardPickupPayloadV1 Create(
            RewardCommitCommandV1 commitCommand,
            RewardPickupCategoryV1? category = null)
        {
            return new RewardPickupPayloadV1(
                commitCommand,
                category ?? RewardPickupCategoryMapV1.FromCommit(commitCommand));
        }

        public StableId DeriveClaimStableId(StableId claimantStableId)
        {
            if (claimantStableId == null)
            {
                throw new ArgumentNullException(nameof(claimantStableId));
            }

            return RewardApplicationCanonicalV1.DeriveStableId(
                "rewardpickupclaim",
                PickupStableId.ToString(),
                claimantStableId.ToString());
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(RewardPickupPayloadV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RewardPickupPayloadV1);
        }

        public override int GetHashCode()
        {
            return RewardApplicationCanonicalV1.DeterministicHash(canonicalText);
        }
    }

    public enum RewardPickupCollectStatusV1
    {
        Collected = 1,
        AlreadyCollectedNoChange = 2,
        PendingRetry = 3,
        Rejected = 4,
        Invalid = 5,
    }

    public sealed class RewardPickupCollectResultV1
    {
        public RewardPickupCollectResultV1(
            RewardPickupCollectStatusV1 status,
            RewardApplicationResultV1 authorityResult,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RewardPickupCollectStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            AuthorityResult = authorityResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardPickupCollectStatusV1 Status { get; }
        public RewardApplicationResultV1 AuthorityResult { get; }
        public string Diagnostic { get; }

        public bool IsCollected
        {
            get
            {
                return Status == RewardPickupCollectStatusV1.Collected
                    || Status == RewardPickupCollectStatusV1.AlreadyCollectedNoChange;
            }
        }
    }

    public enum RewardPickupSpawnStatusV1
    {
        Spawned = 1,
        ExactDuplicateNoChange = 2,
        ExplicitNoDrop = 3,
        Rejected = 4,
    }

    public sealed class RewardPickupSpawnResultV1
    {
        public RewardPickupSpawnResultV1(
            RewardPickupSpawnStatusV1 status,
            RewardPickup2D pickup,
            RewardApplicationResultV1 authorityResult,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(RewardPickupSpawnStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            Pickup = pickup;
            AuthorityResult = authorityResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RewardPickupSpawnStatusV1 Status { get; }
        public RewardPickup2D Pickup { get; }
        public RewardApplicationResultV1 AuthorityResult { get; }
        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == RewardPickupSpawnStatusV1.Spawned
                    || Status == RewardPickupSpawnStatusV1.ExactDuplicateNoChange
                    || Status == RewardPickupSpawnStatusV1.ExplicitNoDrop;
            }
        }
    }

    public interface IRewardPickupLifecycleAuthorityV1
    {
        RewardApplicationResultV1 Commit(RewardCommitCommandV1 command);

        RewardPickupCollectResultV1 Collect(
            RewardPickupPayloadV1 payload,
            StableId claimantStableId);
    }

    /// <summary>
    /// Extension point used only when a profile emits equipment references. The
    /// resolver must return the exact immutable equipment instances retained by RAP.
    /// Forced drops can bypass this port by supplying a fully prepared commit command.
    /// </summary>
    public interface IRewardPickupEquipmentPayloadResolverV1
    {
        bool TryResolve(
            RewardSourceResolvedPreview source,
            RewardGrantV1 grant,
            out IReadOnlyList<EquipmentInstance> equipmentInstances,
            out string rejectionCode);
    }

    internal static class RewardPickupPayloadBuilderV1
    {
        public static bool TryBuild(
            RewardSourceResolvedPreview source,
            RewardResultV1 generatedReward,
            IRewardPickupEquipmentPayloadResolverV1 equipmentResolver,
            out IReadOnlyList<RewardGrantApplicationPayloadV1> payloads,
            out string rejectionCode)
        {
            if (source == null || generatedReward == null)
            {
                payloads = Array.Empty<RewardGrantApplicationPayloadV1>();
                rejectionCode = "pickup-payload-input-null";
                return false;
            }

            List<RewardGrantApplicationPayloadV1> values =
                new List<RewardGrantApplicationPayloadV1>(generatedReward.Grants.Count);
            for (int grantIndex = 0; grantIndex < generatedReward.Grants.Count; grantIndex++)
            {
                RewardGrantV1 grant = generatedReward.Grants[grantIndex];
                switch (grant.Kind)
                {
                    case RewardGrantKindV1.Money:
                    case RewardGrantKindV1.Scrap:
                    case RewardGrantKindV1.PremiumAmmo:
                    case RewardGrantKindV1.Miscellaneous:
                        values.Add(RewardGrantApplicationPayloadV1.ForValue(grant));
                        break;
                    case RewardGrantKindV1.Strongbox:
                        if (grant.Quantity > int.MaxValue)
                        {
                            payloads = Array.Empty<RewardGrantApplicationPayloadV1>();
                            rejectionCode = "pickup-strongbox-quantity-too-large";
                            return false;
                        }

                        List<StableId> strongboxIds = new List<StableId>((int)grant.Quantity);
                        for (long instanceIndex = 0L; instanceIndex < grant.Quantity; instanceIndex++)
                        {
                            strongboxIds.Add(RewardApplicationCanonicalV1.DeriveStableId(
                                "rewardpickupstrongbox",
                                source.OperationRequest.SourceOperationStableId.ToString(),
                                grant.GrantStableId.ToString(),
                                instanceIndex.ToString(CultureInfo.InvariantCulture)));
                        }

                        values.Add(RewardGrantApplicationPayloadV1.ForStrongboxes(grant, strongboxIds));
                        break;
                    case RewardGrantKindV1.EquipmentReference:
                        if (equipmentResolver == null)
                        {
                            payloads = Array.Empty<RewardGrantApplicationPayloadV1>();
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
                            payloads = Array.Empty<RewardGrantApplicationPayloadV1>();
                            rejectionCode = string.IsNullOrEmpty(rejectionCode)
                                ? "pickup-equipment-resolution-rejected"
                                : rejectionCode;
                            return false;
                        }

                        values.Add(RewardGrantApplicationPayloadV1.ForEquipment(grant, equipment));
                        break;
                    default:
                        payloads = Array.Empty<RewardGrantApplicationPayloadV1>();
                        rejectionCode = "pickup-grant-kind-unsupported";
                        return false;
                }
            }

            values.Sort(delegate(
                RewardGrantApplicationPayloadV1 left,
                RewardGrantApplicationPayloadV1 right)
            {
                return left.Grant.GrantStableId.CompareTo(right.Grant.GrantStableId);
            });
            payloads = new ReadOnlyCollection<RewardGrantApplicationPayloadV1>(values);
            rejectionCode = null;
            return true;
        }
    }
}

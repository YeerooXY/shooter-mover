using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.LootDropBinding
{
    /// <summary>
    /// Converts a personal production result into the existing immutable pending-
    /// pickup transport. It does not roll, select, pace or alter rewards.
    /// </summary>
    internal static class TerminalPersonalRewardTransportBridge
    {
        internal static GeneratedLootDropResult Adapt(
            LootDropSourceFact sharedSource,
            PersonalRewardGenerationResult personal)
        {
            if (sharedSource == null)
            {
                throw new ArgumentNullException(nameof(sharedSource));
            }
            if (personal == null)
            {
                throw new ArgumentNullException(nameof(personal));
            }

            if (personal.Status == PersonalRewardGenerationStatus.ConflictingReplay)
            {
                return GeneratedLootDropResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    sharedSource,
                    personal.Diagnostic,
                    true);
            }
            if (!personal.IsSuccess)
            {
                return GeneratedLootDropResult.Rejected(
                    LootDropRejectionCode.GenerationFailed,
                    sharedSource,
                    personal.Diagnostic);
            }

            LootDropSourceFact participantSource =
                CloneForParticipant(
                    sharedSource,
                    personal.Context.ParticipantStableId);
            StableId commitmentId = RewardGenerationFingerprint.DeriveStableId(
                "personalrewardcommitment",
                personal.Context.OperationStableId.ToString(),
                sharedSource.TerminalEventStableId.ToString(),
                personal.Context.ParticipantStableId.ToString());
            RewardOperationRequest operation = RewardOperationRequest.Create(
                sharedSource.RunStableId,
                sharedSource.SourceEntityStableId,
                personal.Context.OperationStableId,
                commitmentId,
                personal.Context.ProfileResolution.EffectiveProfile.ProfileStableId,
                personal.Context.ProfileResolution.Fingerprint);
            List<GeneratedLootDropReward> rewards =
                BuildRewards(operation, personal.Grants);
            LootDropBindingStatus status = rewards.Count == 0
                ? LootDropBindingStatus.ExplicitNoDrop
                : LootDropBindingStatus.Accepted;
            string fingerprint = BuildFingerprint(
                participantSource,
                operation,
                personal,
                rewards);
            return new GeneratedLootDropResult(
                status,
                LootDropRejectionCode.None,
                participantSource,
                personal.Context.ProfileResolution.EffectiveProfile.ProfileStableId,
                operation,
                personal.Context.RootSeed,
                null,
                rewards,
                fingerprint,
                personal.Diagnostic);
        }

        private static LootDropSourceFact CloneForParticipant(
            LootDropSourceFact source,
            StableId participantStableId)
        {
            return new LootDropSourceFact(
                source.FactKindStableId,
                source.TerminalEventStableId,
                source.TriggeringEventStableId,
                source.RunStableId,
                source.RunLifecycleGeneration,
                source.SourceEntityStableId,
                source.SourcePlacementStableId,
                source.SourceLifecycleGeneration,
                source.SourceDefinitionStableId,
                participantStableId,
                source.DamageSourceStableId,
                source.DamageChannelStableId,
                source.DeclaredDropProfileStableId,
                source.SourceContextFingerprint,
                source.DefinitionFingerprint,
                source.UpstreamFactFingerprint);
        }

        private static List<GeneratedLootDropReward> BuildRewards(
            RewardOperationRequest operation,
            IReadOnlyList<RewardGrant> grants)
        {
            var output = new List<GeneratedLootDropReward>();
            int ordinal = 0;
            for (int grantIndex = 0; grantIndex < grants.Count; grantIndex++)
            {
                RewardGrant grant = grants[grantIndex];
                bool unique = grant.Kind == RewardGrantKind.Strongbox
                    || grant.Kind == RewardGrantKind.EquipmentReference;
                if (!unique)
                {
                    output.Add(new GeneratedLootDropReward(
                        grant.GrantStableId,
                        ordinal++,
                        grant.GrantStableId,
                        grant.Kind,
                        grant.ContentStableId,
                        grant.Quantity));
                    continue;
                }

                if (grant.Quantity > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Unique personal reward quantity exceeds child ordinal capacity.");
                }
                for (long unit = 0L; unit < grant.Quantity; unit++)
                {
                    StableId instanceId = grant.Quantity == 1L
                        ? grant.GrantStableId
                        : RewardGenerationFingerprint.DeriveStableId(
                            "personalrewardinstance",
                            operation.SourceOperationStableId.ToString(),
                            grant.GrantStableId.ToString(),
                            unit.ToString(CultureInfo.InvariantCulture));
                    output.Add(new GeneratedLootDropReward(
                        instanceId,
                        ordinal++,
                        grant.GrantStableId,
                        grant.Kind,
                        grant.ContentStableId,
                        1L));
                }
            }
            return output;
        }

        private static string BuildFingerprint(
            LootDropSourceFact participantSource,
            RewardOperationRequest operation,
            PersonalRewardGenerationResult personal,
            IReadOnlyList<GeneratedLootDropReward> rewards)
        {
            var builder = new StringBuilder(
                "schema=generated-personal-terminal-drop-batch-v1");
            LootDrop.Append(
                builder,
                "source",
                participantSource.Fingerprint);
            LootDrop.Append(
                builder,
                "operation",
                operation.Fingerprint);
            LootDrop.Append(
                builder,
                "personal-context",
                personal.Context.Fingerprint);
            LootDrop.Append(
                builder,
                "personal-result",
                personal.Fingerprint);
            LootDrop.Append(
                builder,
                "reward-count",
                rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                LootDrop.Append(
                    builder,
                    "reward-" + index.ToString("D4", CultureInfo.InvariantCulture),
                    rewards[index].Fingerprint);
            }
            return LootDrop.Hash(builder.ToString());
        }
    }
}

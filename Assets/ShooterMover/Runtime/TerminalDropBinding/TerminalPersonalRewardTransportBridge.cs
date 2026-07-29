using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Converts a personal production result into the existing immutable pending-
    /// pickup transport. It does not roll, select, pace or alter rewards.
    /// </summary>
    internal static class TerminalPersonalRewardTransportBridge
    {
        internal static GeneratedTerminalDropResult Adapt(
            TerminalDropSourceFact sharedSource,
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
                return GeneratedTerminalDropResult.Rejected(
                    TerminalDropRejectionCode.InvalidTerminalFact,
                    sharedSource,
                    personal.Diagnostic,
                    true);
            }
            if (!personal.IsSuccess)
            {
                return GeneratedTerminalDropResult.Rejected(
                    TerminalDropRejectionCode.GenerationFailed,
                    sharedSource,
                    personal.Diagnostic);
            }

            TerminalDropSourceFact participantSource =
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
            List<GeneratedTerminalDropReward> rewards =
                BuildRewards(operation, personal.Grants);
            TerminalDropBindingStatus status = rewards.Count == 0
                ? TerminalDropBindingStatus.ExplicitNoDrop
                : TerminalDropBindingStatus.Accepted;
            string fingerprint = BuildFingerprint(
                participantSource,
                operation,
                personal,
                rewards);
            return new GeneratedTerminalDropResult(
                status,
                TerminalDropRejectionCode.None,
                participantSource,
                personal.Context.ProfileResolution.EffectiveProfile.ProfileStableId,
                operation,
                personal.Context.RootSeed,
                null,
                rewards,
                fingerprint,
                personal.Diagnostic);
        }

        private static TerminalDropSourceFact CloneForParticipant(
            TerminalDropSourceFact source,
            StableId participantStableId)
        {
            return new TerminalDropSourceFact(
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

        private static List<GeneratedTerminalDropReward> BuildRewards(
            RewardOperationRequest operation,
            IReadOnlyList<RewardGrant> grants)
        {
            var output = new List<GeneratedTerminalDropReward>();
            int ordinal = 0;
            for (int grantIndex = 0; grantIndex < grants.Count; grantIndex++)
            {
                RewardGrant grant = grants[grantIndex];
                bool unique = grant.Kind == RewardGrantKind.Strongbox
                    || grant.Kind == RewardGrantKind.EquipmentReference;
                if (!unique)
                {
                    output.Add(new GeneratedTerminalDropReward(
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
                    output.Add(new GeneratedTerminalDropReward(
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
            TerminalDropSourceFact participantSource,
            RewardOperationRequest operation,
            PersonalRewardGenerationResult personal,
            IReadOnlyList<GeneratedTerminalDropReward> rewards)
        {
            var builder = new StringBuilder(
                "schema=generated-personal-terminal-drop-batch-v1");
            TerminalDrop.Append(
                builder,
                "source",
                participantSource.Fingerprint);
            TerminalDrop.Append(
                builder,
                "operation",
                operation.Fingerprint);
            TerminalDrop.Append(
                builder,
                "personal-context",
                personal.Context.Fingerprint);
            TerminalDrop.Append(
                builder,
                "personal-result",
                personal.Fingerprint);
            TerminalDrop.Append(
                builder,
                "reward-count",
                rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                TerminalDrop.Append(
                    builder,
                    "reward-" + index.ToString("D4", CultureInfo.InvariantCulture),
                    rewards[index].Fingerprint);
            }
            return TerminalDrop.Hash(builder.ToString());
        }
    }
}

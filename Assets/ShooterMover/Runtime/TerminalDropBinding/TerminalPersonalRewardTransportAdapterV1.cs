using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Converts a personal production result into the existing immutable pending-
    /// pickup transport. It does not roll, select, pace or alter rewards.
    /// </summary>
    internal static class TerminalPersonalRewardTransportAdapterV1
    {
        internal static GeneratedTerminalDropResultV1 Adapt(
            TerminalDropSourceFactV1 sharedSource,
            PersonalRewardGenerationResultV1 personal)
        {
            if (sharedSource == null)
            {
                throw new ArgumentNullException(nameof(sharedSource));
            }
            if (personal == null)
            {
                throw new ArgumentNullException(nameof(personal));
            }

            if (personal.Status == PersonalRewardGenerationStatusV1.ConflictingReplay)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    sharedSource,
                    personal.Diagnostic,
                    true);
            }
            if (!personal.IsSuccess)
            {
                return GeneratedTerminalDropResultV1.Rejected(
                    TerminalDropRejectionCodeV1.GenerationFailed,
                    sharedSource,
                    personal.Diagnostic);
            }

            TerminalDropSourceFactV1 participantSource =
                CloneForParticipant(
                    sharedSource,
                    personal.Context.ParticipantStableId);
            StableId commitmentId = RewardGenerationFingerprintV1.DeriveStableId(
                "personalrewardcommitment",
                personal.Context.OperationStableId.ToString(),
                sharedSource.TerminalEventStableId.ToString(),
                personal.Context.ParticipantStableId.ToString());
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                sharedSource.RunStableId,
                sharedSource.SourceEntityStableId,
                personal.Context.OperationStableId,
                commitmentId,
                personal.Context.ProfileResolution.EffectiveProfile.ProfileStableId,
                personal.Context.ProfileResolution.Fingerprint);
            List<GeneratedTerminalDropRewardV1> rewards =
                BuildRewards(operation, personal.Grants);
            TerminalDropBindingStatusV1 status = rewards.Count == 0
                ? TerminalDropBindingStatusV1.ExplicitNoDrop
                : TerminalDropBindingStatusV1.Accepted;
            string fingerprint = BuildFingerprint(
                participantSource,
                operation,
                personal,
                rewards);
            return new GeneratedTerminalDropResultV1(
                status,
                TerminalDropRejectionCodeV1.None,
                participantSource,
                personal.Context.ProfileResolution.EffectiveProfile.ProfileStableId,
                operation,
                personal.Context.RootSeed,
                null,
                rewards,
                fingerprint,
                personal.Diagnostic);
        }

        private static TerminalDropSourceFactV1 CloneForParticipant(
            TerminalDropSourceFactV1 source,
            StableId participantStableId)
        {
            return new TerminalDropSourceFactV1(
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

        private static List<GeneratedTerminalDropRewardV1> BuildRewards(
            RewardOperationRequestV1 operation,
            IReadOnlyList<RewardGrantV1> grants)
        {
            var output = new List<GeneratedTerminalDropRewardV1>();
            int ordinal = 0;
            for (int grantIndex = 0; grantIndex < grants.Count; grantIndex++)
            {
                RewardGrantV1 grant = grants[grantIndex];
                bool unique = grant.Kind == RewardGrantKindV1.Strongbox
                    || grant.Kind == RewardGrantKindV1.EquipmentReference;
                if (!unique)
                {
                    output.Add(new GeneratedTerminalDropRewardV1(
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
                        : RewardGenerationFingerprintV1.DeriveStableId(
                            "personalrewardinstance",
                            operation.SourceOperationStableId.ToString(),
                            grant.GrantStableId.ToString(),
                            unit.ToString(CultureInfo.InvariantCulture));
                    output.Add(new GeneratedTerminalDropRewardV1(
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
            TerminalDropSourceFactV1 participantSource,
            RewardOperationRequestV1 operation,
            PersonalRewardGenerationResultV1 personal,
            IReadOnlyList<GeneratedTerminalDropRewardV1> rewards)
        {
            var builder = new StringBuilder(
                "schema=generated-personal-terminal-drop-batch-v1");
            TerminalDropCanonicalV1.Append(
                builder,
                "source",
                participantSource.Fingerprint);
            TerminalDropCanonicalV1.Append(
                builder,
                "operation",
                operation.Fingerprint);
            TerminalDropCanonicalV1.Append(
                builder,
                "personal-context",
                personal.Context.Fingerprint);
            TerminalDropCanonicalV1.Append(
                builder,
                "personal-result",
                personal.Fingerprint);
            TerminalDropCanonicalV1.Append(
                builder,
                "reward-count",
                rewards.Count);
            for (int index = 0; index < rewards.Count; index++)
            {
                TerminalDropCanonicalV1.Append(
                    builder,
                    "reward-" + index.ToString("D4", CultureInfo.InvariantCulture),
                    rewards[index].Fingerprint);
            }
            return TerminalDropCanonicalV1.Hash(builder.ToString());
        }
    }
}

using System;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.LootDrops
{
    public enum LootDropOverrideMode
    {
        Default = 1,
        ForcedNone = 2,
        ForcedSpecificReward = 3,
        AppendGuaranteedReward = 4,
    }

    /// <summary>
    /// Immutable manual override applied to one gameplay drop source. Resolution is pure:
    /// generation and application remain owned by GEN-001 and RAP-001.
    /// </summary>
    public sealed class LootDropOverride
    {
        private LootDropOverride(
            StableId overrideStableId,
            LootDropOverrideMode mode,
            StableId resultProfileStableId,
            RewardGrantSpecification reward)
        {
            OverrideStableId = overrideStableId
                ?? throw new ArgumentNullException(nameof(overrideStableId));
            if (!Enum.IsDefined(typeof(LootDropOverrideMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            Mode = mode;
            ResultProfileStableId = resultProfileStableId;
            Reward = reward;
            ValidateShape();
        }

        public StableId OverrideStableId { get; }

        public LootDropOverrideMode Mode { get; }

        public StableId ResultProfileStableId { get; }

        public RewardGrantSpecification Reward { get; }

        public static LootDropOverride Default(StableId overrideStableId)
        {
            return new LootDropOverride(
                overrideStableId,
                LootDropOverrideMode.Default,
                null,
                null);
        }

        public static LootDropOverride ForcedNone(
            StableId overrideStableId,
            StableId resultProfileStableId)
        {
            return new LootDropOverride(
                overrideStableId,
                LootDropOverrideMode.ForcedNone,
                resultProfileStableId,
                null);
        }

        public static LootDropOverride ForcedSpecificReward(
            StableId overrideStableId,
            StableId resultProfileStableId,
            RewardGrantSpecification reward)
        {
            return new LootDropOverride(
                overrideStableId,
                LootDropOverrideMode.ForcedSpecificReward,
                resultProfileStableId,
                reward);
        }

        public static LootDropOverride AppendGuaranteedReward(
            StableId overrideStableId,
            StableId resultProfileStableId,
            RewardGrantSpecification reward)
        {
            return new LootDropOverride(
                overrideStableId,
                LootDropOverrideMode.AppendGuaranteedReward,
                resultProfileStableId,
                reward);
        }

        public RewardProfile Resolve(
            StableId sourceInstanceStableId,
            RewardProfile inheritedProfile)
        {
            if (sourceInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(sourceInstanceStableId));
            }

            if (inheritedProfile == null)
            {
                throw new ArgumentNullException(nameof(inheritedProfile));
            }

            switch (Mode)
            {
                case LootDropOverrideMode.Default:
                    return LootSourceOverride.Inherit(
                        OverrideStableId,
                        sourceInstanceStableId).Resolve(inheritedProfile);
                case LootDropOverrideMode.ForcedNone:
                    return LootSourceOverride.NoReward(
                        OverrideStableId,
                        sourceInstanceStableId,
                        ResultProfileStableId).Resolve(inheritedProfile);
                case LootDropOverrideMode.ForcedSpecificReward:
                    return LootSourceOverride.ReplaceEntirely(
                        OverrideStableId,
                        sourceInstanceStableId,
                        RewardProfile.Create(
                            ResultProfileStableId,
                            new[] { Reward },
                            Array.Empty<IndependentRewardRoll>(),
                            Array.Empty<ExclusiveRewardGroup>()))
                        .Resolve(inheritedProfile);
                case LootDropOverrideMode.AppendGuaranteedReward:
                    return LootSourceOverride.AppendGuaranteedEntries(
                        OverrideStableId,
                        sourceInstanceStableId,
                        ResultProfileStableId,
                        new[] { Reward }).Resolve(inheritedProfile);
                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode));
            }
        }

        public string ToCanonicalString()
        {
            return "override_id=" + OverrideStableId
                + "\nmode=" + ((int)Mode).ToString(CultureInfo.InvariantCulture)
                + "\nresult_profile_id="
                + (ResultProfileStableId == null ? "none" : ResultProfileStableId.ToString())
                + "\nreward="
                + (Reward == null ? "none" : Reward.ToCanonicalString());
        }

        private void ValidateShape()
        {
            switch (Mode)
            {
                case LootDropOverrideMode.Default:
                    if (ResultProfileStableId != null || Reward != null)
                    {
                        throw new ArgumentException(
                            "Default gameplay drop overrides must not carry replacement data.");
                    }

                    return;
                case LootDropOverrideMode.ForcedNone:
                    if (ResultProfileStableId == null || Reward != null)
                    {
                        throw new ArgumentException(
                            "Forced-none overrides require only a result profile identity.");
                    }

                    return;
                case LootDropOverrideMode.ForcedSpecificReward:
                case LootDropOverrideMode.AppendGuaranteedReward:
                    if (ResultProfileStableId == null || Reward == null)
                    {
                        throw new ArgumentException(
                            "Reward overrides require a result profile identity and one reward.");
                    }

                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode));
            }
        }
    }

    /// <summary>
    /// Complete deterministic source operation prepared for the existing reward pipeline.
    /// The operation identity depends only on run and stable source identity.
    /// </summary>
    public sealed class LootDropOperation
    {
        internal LootDropOperation(
            RewardProfile inheritedProfile,
            RewardProfile resolvedProfile,
            LootDropOverride appliedOverride,
            RewardOperationRequest operationRequest,
            StableId restartParticipantStableId,
            string fingerprint)
        {
            InheritedProfile = inheritedProfile
                ?? throw new ArgumentNullException(nameof(inheritedProfile));
            ResolvedProfile = resolvedProfile
                ?? throw new ArgumentNullException(nameof(resolvedProfile));
            AppliedOverride = appliedOverride
                ?? throw new ArgumentNullException(nameof(appliedOverride));
            OperationRequest = operationRequest
                ?? throw new ArgumentNullException(nameof(operationRequest));
            RestartParticipantStableId = restartParticipantStableId
                ?? throw new ArgumentNullException(nameof(restartParticipantStableId));
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        }

        public RewardProfile InheritedProfile { get; }

        public RewardProfile ResolvedProfile { get; }

        public LootDropOverride AppliedOverride { get; }

        public RewardOperationRequest OperationRequest { get; }

        public StableId RestartParticipantStableId { get; }

        public string Fingerprint { get; }
    }

    public static class LootDropOperationFactory
    {
        public static LootDropOperation Create(
            StableId runStableId,
            StableId sourceInstanceStableId,
            RewardProfile inheritedProfile,
            LootDropOverride manualOverride)
        {
            if (runStableId == null)
            {
                throw new ArgumentNullException(nameof(runStableId));
            }

            if (sourceInstanceStableId == null)
            {
                throw new ArgumentNullException(nameof(sourceInstanceStableId));
            }

            if (inheritedProfile == null)
            {
                throw new ArgumentNullException(nameof(inheritedProfile));
            }

            if (manualOverride == null)
            {
                throw new ArgumentNullException(nameof(manualOverride));
            }

            RewardProfile resolvedProfile = manualOverride.Resolve(
                sourceInstanceStableId,
                inheritedProfile);
            StableId sourceOperationStableId =
                RewardApplication.DeriveStableId(
                    "gameplaydropoperation",
                    runStableId.ToString(),
                    sourceInstanceStableId.ToString());
            StableId commitmentStableId =
                RewardApplication.DeriveStableId(
                    "gameplaydropcommitment",
                    runStableId.ToString(),
                    sourceInstanceStableId.ToString());
            StableId restartParticipantStableId =
                RewardApplication.DeriveStableId(
                    "gameplaydroprestart",
                    runStableId.ToString(),
                    sourceInstanceStableId.ToString());

            RewardOperationRequest request = RewardOperationRequest.Create(
                runStableId,
                sourceInstanceStableId,
                sourceOperationStableId,
                commitmentStableId,
                resolvedProfile.ProfileStableId,
                resolvedProfile.Fingerprint);

            var canonical = new StringBuilder();
            RewardApplication.AppendToken(
                canonical,
                "inherited_profile",
                inheritedProfile.Fingerprint);
            RewardApplication.AppendToken(
                canonical,
                "resolved_profile",
                resolvedProfile.Fingerprint);
            RewardApplication.AppendToken(
                canonical,
                "manual_override",
                manualOverride.ToCanonicalString());
            RewardApplication.AppendToken(
                canonical,
                "operation_request",
                request.Fingerprint);
            RewardApplication.AppendToken(
                canonical,
                "restart_participant",
                restartParticipantStableId.ToString());

            return new LootDropOperation(
                inheritedProfile,
                resolvedProfile,
                manualOverride,
                request,
                restartParticipantStableId,
                RewardApplication.Fingerprint(canonical.ToString()));
        }
    }
}

using System;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.GameplayDrops
{
    public enum GameplayDropOverrideModeV1
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
    public sealed class GameplayDropOverrideV1
    {
        private GameplayDropOverrideV1(
            StableId overrideStableId,
            GameplayDropOverrideModeV1 mode,
            StableId resultProfileStableId,
            RewardGrantSpecificationV1 reward)
        {
            OverrideStableId = overrideStableId
                ?? throw new ArgumentNullException(nameof(overrideStableId));
            if (!Enum.IsDefined(typeof(GameplayDropOverrideModeV1), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            Mode = mode;
            ResultProfileStableId = resultProfileStableId;
            Reward = reward;
            ValidateShape();
        }

        public StableId OverrideStableId { get; }

        public GameplayDropOverrideModeV1 Mode { get; }

        public StableId ResultProfileStableId { get; }

        public RewardGrantSpecificationV1 Reward { get; }

        public static GameplayDropOverrideV1 Default(StableId overrideStableId)
        {
            return new GameplayDropOverrideV1(
                overrideStableId,
                GameplayDropOverrideModeV1.Default,
                null,
                null);
        }

        public static GameplayDropOverrideV1 ForcedNone(
            StableId overrideStableId,
            StableId resultProfileStableId)
        {
            return new GameplayDropOverrideV1(
                overrideStableId,
                GameplayDropOverrideModeV1.ForcedNone,
                resultProfileStableId,
                null);
        }

        public static GameplayDropOverrideV1 ForcedSpecificReward(
            StableId overrideStableId,
            StableId resultProfileStableId,
            RewardGrantSpecificationV1 reward)
        {
            return new GameplayDropOverrideV1(
                overrideStableId,
                GameplayDropOverrideModeV1.ForcedSpecificReward,
                resultProfileStableId,
                reward);
        }

        public static GameplayDropOverrideV1 AppendGuaranteedReward(
            StableId overrideStableId,
            StableId resultProfileStableId,
            RewardGrantSpecificationV1 reward)
        {
            return new GameplayDropOverrideV1(
                overrideStableId,
                GameplayDropOverrideModeV1.AppendGuaranteedReward,
                resultProfileStableId,
                reward);
        }

        public RewardProfileV1 Resolve(
            StableId sourceInstanceStableId,
            RewardProfileV1 inheritedProfile)
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
                case GameplayDropOverrideModeV1.Default:
                    return RewardSourceOverrideV1.Inherit(
                        OverrideStableId,
                        sourceInstanceStableId).Resolve(inheritedProfile);
                case GameplayDropOverrideModeV1.ForcedNone:
                    return RewardSourceOverrideV1.NoReward(
                        OverrideStableId,
                        sourceInstanceStableId,
                        ResultProfileStableId).Resolve(inheritedProfile);
                case GameplayDropOverrideModeV1.ForcedSpecificReward:
                    return RewardSourceOverrideV1.ReplaceEntirely(
                        OverrideStableId,
                        sourceInstanceStableId,
                        RewardProfileV1.Create(
                            ResultProfileStableId,
                            new[] { Reward },
                            Array.Empty<IndependentRewardRollV1>(),
                            Array.Empty<ExclusiveRewardGroupV1>()))
                        .Resolve(inheritedProfile);
                case GameplayDropOverrideModeV1.AppendGuaranteedReward:
                    return RewardSourceOverrideV1.AppendGuaranteedEntries(
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
                case GameplayDropOverrideModeV1.Default:
                    if (ResultProfileStableId != null || Reward != null)
                    {
                        throw new ArgumentException(
                            "Default gameplay drop overrides must not carry replacement data.");
                    }

                    return;
                case GameplayDropOverrideModeV1.ForcedNone:
                    if (ResultProfileStableId == null || Reward != null)
                    {
                        throw new ArgumentException(
                            "Forced-none overrides require only a result profile identity.");
                    }

                    return;
                case GameplayDropOverrideModeV1.ForcedSpecificReward:
                case GameplayDropOverrideModeV1.AppendGuaranteedReward:
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
    public sealed class GameplayDropOperationV1
    {
        internal GameplayDropOperationV1(
            RewardProfileV1 inheritedProfile,
            RewardProfileV1 resolvedProfile,
            GameplayDropOverrideV1 appliedOverride,
            RewardOperationRequestV1 operationRequest,
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

        public RewardProfileV1 InheritedProfile { get; }

        public RewardProfileV1 ResolvedProfile { get; }

        public GameplayDropOverrideV1 AppliedOverride { get; }

        public RewardOperationRequestV1 OperationRequest { get; }

        public StableId RestartParticipantStableId { get; }

        public string Fingerprint { get; }
    }

    public static class GameplayDropOperationFactoryV1
    {
        public static GameplayDropOperationV1 Create(
            StableId runStableId,
            StableId sourceInstanceStableId,
            RewardProfileV1 inheritedProfile,
            GameplayDropOverrideV1 manualOverride)
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

            RewardProfileV1 resolvedProfile = manualOverride.Resolve(
                sourceInstanceStableId,
                inheritedProfile);
            StableId sourceOperationStableId =
                RewardApplicationCanonicalV1.DeriveStableId(
                    "gameplaydropoperation",
                    runStableId.ToString(),
                    sourceInstanceStableId.ToString());
            StableId commitmentStableId =
                RewardApplicationCanonicalV1.DeriveStableId(
                    "gameplaydropcommitment",
                    runStableId.ToString(),
                    sourceInstanceStableId.ToString());
            StableId restartParticipantStableId =
                RewardApplicationCanonicalV1.DeriveStableId(
                    "gameplaydroprestart",
                    runStableId.ToString(),
                    sourceInstanceStableId.ToString());

            RewardOperationRequestV1 request = RewardOperationRequestV1.Create(
                runStableId,
                sourceInstanceStableId,
                sourceOperationStableId,
                commitmentStableId,
                resolvedProfile.ProfileStableId,
                resolvedProfile.Fingerprint);

            var canonical = new StringBuilder();
            RewardApplicationCanonicalV1.AppendToken(
                canonical,
                "inherited_profile",
                inheritedProfile.Fingerprint);
            RewardApplicationCanonicalV1.AppendToken(
                canonical,
                "resolved_profile",
                resolvedProfile.Fingerprint);
            RewardApplicationCanonicalV1.AppendToken(
                canonical,
                "manual_override",
                manualOverride.ToCanonicalString());
            RewardApplicationCanonicalV1.AppendToken(
                canonical,
                "operation_request",
                request.Fingerprint);
            RewardApplicationCanonicalV1.AppendToken(
                canonical,
                "restart_participant",
                restartParticipantStableId.ToString());

            return new GameplayDropOperationV1(
                inheritedProfile,
                resolvedProfile,
                manualOverride,
                request,
                restartParticipantStableId,
                RewardApplicationCanonicalV1.Fingerprint(canonical.ToString()));
        }
    }
}

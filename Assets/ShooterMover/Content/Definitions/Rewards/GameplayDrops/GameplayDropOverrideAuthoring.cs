using System;
using ShooterMover.Application.Rewards.GameplayDrops;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Rewards.GameplayDrops
{
    [Serializable]
    public sealed class GameplayDropOverrideAuthoring
    {
        [SerializeField] private GameplayDropOverrideModeV1 mode =
            GameplayDropOverrideModeV1.Default;
        [SerializeField] private string overrideId = "gameplay-drop-override.default";
        [SerializeField] private string resultProfileId =
            "gameplay-drop-profile.override-result";
        [SerializeField] private RewardGrantAuthoring reward = new RewardGrantAuthoring();

        public GameplayDropOverrideModeV1 Mode
        {
            get { return mode; }
        }

        public static GameplayDropOverrideAuthoring Default(string overrideId)
        {
            return Create(
                GameplayDropOverrideModeV1.Default,
                overrideId,
                null,
                null);
        }

        public static GameplayDropOverrideAuthoring ForcedNone(
            string overrideId,
            string resultProfileId)
        {
            return Create(
                GameplayDropOverrideModeV1.ForcedNone,
                overrideId,
                resultProfileId,
                null);
        }

        public static GameplayDropOverrideAuthoring ForcedSpecificReward(
            string overrideId,
            string resultProfileId,
            RewardGrantAuthoring reward)
        {
            return Create(
                GameplayDropOverrideModeV1.ForcedSpecificReward,
                overrideId,
                resultProfileId,
                reward);
        }

        public static GameplayDropOverrideAuthoring AppendGuaranteedReward(
            string overrideId,
            string resultProfileId,
            RewardGrantAuthoring reward)
        {
            return Create(
                GameplayDropOverrideModeV1.AppendGuaranteedReward,
                overrideId,
                resultProfileId,
                reward);
        }

        public GameplayDropOverrideV1 Build()
        {
            StableId parsedOverrideId = StableId.Parse(overrideId);
            switch (mode)
            {
                case GameplayDropOverrideModeV1.Default:
                    return GameplayDropOverrideV1.Default(parsedOverrideId);
                case GameplayDropOverrideModeV1.ForcedNone:
                    return GameplayDropOverrideV1.ForcedNone(
                        parsedOverrideId,
                        StableId.Parse(resultProfileId));
                case GameplayDropOverrideModeV1.ForcedSpecificReward:
                    return GameplayDropOverrideV1.ForcedSpecificReward(
                        parsedOverrideId,
                        StableId.Parse(resultProfileId),
                        RequireReward().Build());
                case GameplayDropOverrideModeV1.AppendGuaranteedReward:
                    return GameplayDropOverrideV1.AppendGuaranteedReward(
                        parsedOverrideId,
                        StableId.Parse(resultProfileId),
                        RequireReward().Build());
                default:
                    throw new InvalidOperationException(
                        $"Unsupported gameplay drop override mode '{mode}'.");
            }
        }

        private static GameplayDropOverrideAuthoring Create(
            GameplayDropOverrideModeV1 mode,
            string overrideId,
            string resultProfileId,
            RewardGrantAuthoring reward)
        {
            return new GameplayDropOverrideAuthoring
            {
                mode = mode,
                overrideId = overrideId
                    ?? throw new ArgumentNullException(nameof(overrideId)),
                resultProfileId = resultProfileId,
                reward = reward,
            };
        }

        private RewardGrantAuthoring RequireReward()
        {
            if (reward == null)
            {
                throw new InvalidOperationException(
                    $"Gameplay drop override '{overrideId}' requires a reward.");
            }

            return reward;
        }
    }
}

using System;
using ShooterMover.Application.Rewards.LootDrops;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Rewards.LootDrops
{
    [Serializable]
    public sealed class LootDropOverrideAuthoring
    {
        [SerializeField] private LootDropOverrideMode mode =
            LootDropOverrideMode.Default;
        [SerializeField] private string overrideId = "gameplay-drop-override.default";
        [SerializeField] private string resultProfileId =
            "gameplay-drop-profile.override-result";
        [SerializeField] private RewardGrantAuthoring reward = new RewardGrantAuthoring();

        public LootDropOverrideMode Mode
        {
            get { return mode; }
        }

        public static LootDropOverrideAuthoring Default(string overrideId)
        {
            return Create(
                LootDropOverrideMode.Default,
                overrideId,
                null,
                null);
        }

        public static LootDropOverrideAuthoring ForcedNone(
            string overrideId,
            string resultProfileId)
        {
            return Create(
                LootDropOverrideMode.ForcedNone,
                overrideId,
                resultProfileId,
                null);
        }

        public static LootDropOverrideAuthoring ForcedSpecificReward(
            string overrideId,
            string resultProfileId,
            RewardGrantAuthoring reward)
        {
            return Create(
                LootDropOverrideMode.ForcedSpecificReward,
                overrideId,
                resultProfileId,
                reward);
        }

        public static LootDropOverrideAuthoring AppendGuaranteedReward(
            string overrideId,
            string resultProfileId,
            RewardGrantAuthoring reward)
        {
            return Create(
                LootDropOverrideMode.AppendGuaranteedReward,
                overrideId,
                resultProfileId,
                reward);
        }

        public LootDropOverride Build()
        {
            StableId parsedOverrideId = StableId.Parse(overrideId);
            switch (mode)
            {
                case LootDropOverrideMode.Default:
                    return LootDropOverride.Default(parsedOverrideId);
                case LootDropOverrideMode.ForcedNone:
                    return LootDropOverride.ForcedNone(
                        parsedOverrideId,
                        StableId.Parse(resultProfileId));
                case LootDropOverrideMode.ForcedSpecificReward:
                    return LootDropOverride.ForcedSpecificReward(
                        parsedOverrideId,
                        StableId.Parse(resultProfileId),
                        RequireReward().Build());
                case LootDropOverrideMode.AppendGuaranteedReward:
                    return LootDropOverride.AppendGuaranteedReward(
                        parsedOverrideId,
                        StableId.Parse(resultProfileId),
                        RequireReward().Build());
                default:
                    throw new InvalidOperationException(
                        $"Unsupported gameplay drop override mode '{mode}'.");
            }
        }

        private static LootDropOverrideAuthoring Create(
            LootDropOverrideMode mode,
            string overrideId,
            string resultProfileId,
            RewardGrantAuthoring reward)
        {
            return new LootDropOverrideAuthoring
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

using System;
using System.Collections.Generic;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Rewards.GameplayDrops
{
    /// <summary>
    /// Authorable gameplay-drop profile. Every reward family uses the shared REW-001
    /// vocabulary, and all random decisions are deferred to GEN-001.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameplayDropProfile",
        menuName = "Shooter Mover/Rewards/Gameplay Drop Profile")]
    public sealed class GameplayDropProfileDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string profileId = "gameplay-drop-profile.unassigned";
        [SerializeField] private bool explicitNoDrop;
        [SerializeField] private RewardGrantAuthoring[] guaranteedEntries =
            Array.Empty<RewardGrantAuthoring>();
        [SerializeField] private IndependentRewardRollAuthoring[] independentRolls =
            Array.Empty<IndependentRewardRollAuthoring>();
        [SerializeField] private ExclusiveRewardGroupAuthoring[] weightedAlternatives =
            Array.Empty<ExclusiveRewardGroupAuthoring>();

        public RewardProfileV1 BuildProfile()
        {
            StableId parsedProfileId = StableId.Parse(profileId);
            if (explicitNoDrop)
            {
                if (Count(guaranteedEntries) != 0
                    || Count(independentRolls) != 0
                    || Count(weightedAlternatives) != 0)
                {
                    throw new InvalidOperationException(
                        "Explicit no-drop gameplay profiles must not contain entries.");
                }

                return RewardProfileV1.CreateExplicitNoDrop(parsedProfileId);
            }

            List<RewardGrantSpecificationV1> guaranteed =
                BuildAll(
                    guaranteedEntries,
                    "guaranteed entry",
                    delegate(RewardGrantAuthoring value) { return value.Build(); });
            List<IndependentRewardRollV1> independent =
                BuildAll(
                    independentRolls,
                    "independent roll",
                    delegate(IndependentRewardRollAuthoring value) { return value.Build(); });
            List<ExclusiveRewardGroupV1> weighted =
                BuildAll(
                    weightedAlternatives,
                    "weighted alternative group",
                    delegate(ExclusiveRewardGroupAuthoring value) { return value.Build(); });

            return RewardProfileV1.Create(
                parsedProfileId,
                guaranteed,
                independent,
                weighted);
        }

        public void ValidateOrThrow()
        {
            BuildProfile();
        }

        public static GameplayDropProfileDefinitionAsset CreateRuntime(
            string profileId,
            bool explicitNoDrop,
            RewardGrantAuthoring[] guaranteedEntries,
            IndependentRewardRollAuthoring[] independentRolls,
            ExclusiveRewardGroupAuthoring[] weightedAlternatives)
        {
            GameplayDropProfileDefinitionAsset asset =
                CreateInstance<GameplayDropProfileDefinitionAsset>();
            asset.profileId = profileId ?? throw new ArgumentNullException(nameof(profileId));
            asset.explicitNoDrop = explicitNoDrop;
            asset.guaranteedEntries = guaranteedEntries ?? Array.Empty<RewardGrantAuthoring>();
            asset.independentRolls =
                independentRolls ?? Array.Empty<IndependentRewardRollAuthoring>();
            asset.weightedAlternatives =
                weightedAlternatives ?? Array.Empty<ExclusiveRewardGroupAuthoring>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.ValidateOrThrow();
            return asset;
        }

        private static int Count<T>(T[] values)
        {
            return values == null ? 0 : values.Length;
        }

        private static List<TResult> BuildAll<TSource, TResult>(
            TSource[] values,
            string label,
            Func<TSource, TResult> build)
            where TSource : class
        {
            var result = new List<TResult>(Count(values));
            if (values == null)
            {
                return result;
            }

            for (int index = 0; index < values.Length; index++)
            {
                TSource value = values[index];
                if (value == null)
                {
                    throw new InvalidOperationException(
                        $"Gameplay drop profile contains a null {label} at index {index}.");
                }

                result.Add(build(value));
            }

            return result;
        }

        private void OnValidate()
        {
            if (guaranteedEntries == null)
            {
                guaranteedEntries = Array.Empty<RewardGrantAuthoring>();
            }

            if (independentRolls == null)
            {
                independentRolls = Array.Empty<IndependentRewardRollAuthoring>();
            }

            if (weightedAlternatives == null)
            {
                weightedAlternatives = Array.Empty<ExclusiveRewardGroupAuthoring>();
            }
        }
    }
}

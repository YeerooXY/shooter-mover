
using System;
using System.IO;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed class WeaponCatalogStrongboxIntegrationV1Tests
    {
        private const string BaselinePath =
            "Assets/ShooterMover/Resources/WeaponCatalog/weapon_baseline_v01.json";

        [Test]
        public void SimulatorAndProductionProjection_HaveExactMatchingCatalogs()
        {
            string json = File.ReadAllText(BaselinePath);
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime = CreateRuntime(json);
            CanonicalWeaponCatalogProjectionV1 production = CreateProjection(json);
            Assert.That(runtime.WeaponCatalog.Definitions.Count, Is.EqualTo(121));
            Assert.That(runtime.WeaponCatalog.Families.Count, Is.EqualTo(44));
            Assert.That(runtime.WeaponCatalog.Fingerprint, Is.EqualTo(production.WeaponCatalog.Fingerprint));
            Assert.That(runtime.EquipmentCatalog.Fingerprint, Is.EqualTo(production.EquipmentCatalog.Fingerprint));
        }

        [Test]
        public void SoftTailPolicy_IsPositiveNearAndFar_AndPeakDominates()
        {
            string json = File.ReadAllText(BaselinePath);
            CanonicalWeaponCatalogProjectionV1 projection = CreateProjection(json);
            WeaponDefinitionData centered = FindDefinitionNear(projection, 16, false);
            StrongboxHybridLootPolicyV1 tierPolicy = PolicyForTier(8);
            var policy = WeaponDefinitionDropWeightPolicyV1.CreateBaselineV1();
            StableId rarity = RarityId(centered.Rarity);
            ulong below = policy.EvaluateWeightUnits(Context(tierPolicy, 8, centered, rarity, Math.Max(1, centered.FirstAppearance - 5)));
            ulong near = policy.EvaluateWeightUnits(Context(tierPolicy, 8, centered, rarity, centered.PeakDropLevel));
            ulong above = policy.EvaluateWeightUnits(Context(tierPolicy, 8, centered, rarity, centered.PeakDropLevel + 20));
            Assert.That(below, Is.GreaterThan(0UL));
            Assert.That(above, Is.GreaterThan(0UL));
            Assert.That(near, Is.GreaterThan(below));
            Assert.That(near, Is.GreaterThan(above));

            WeaponDefinitionData overOneHundred = null;
            for (int index = 0; index < projection.WeaponCatalog.Definitions.Count; index++)
            {
                WeaponDefinitionData candidate = projection.WeaponCatalog.Definitions[index];
                if (candidate.PeakDropLevel > 100
                    && candidate.Availability == WeaponCatalogAvailability.Live
                    && (!candidate.TopBoxOnly || 8 == ProductionStrongboxCatalogV1.Tiers.Count))
                {
                    overOneHundred = candidate;
                    break;
                }
            }
            Assert.That(overOneHundred, Is.Not.Null);
            ulong highTail = policy.EvaluateWeightUnits(Context(
                tierPolicy,
                8,
                overOneHundred,
                RarityId(overOneHundred.Rarity),
                100));
            Assert.That(highTail, Is.GreaterThan(0UL));
        }

        [Test]
        public void StrongboxReplay_PreservesExactQualityAndZeroInstalledAugments()
        {
            string json = File.ReadAllText(BaselinePath);
            const ulong rootSeed = 712345UL;
            int[] tierNumbers = RepeatTier(8, 64);
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime = CreateRuntime(json);
            System.Collections.Generic.IReadOnlyList<AuthoritativeStrongboxPreparedOpenV1> prepared =
                runtime.PrepareBatch(tierNumbers, 70, rootSeed);
            EquipmentInstance first = runtime.EquipmentFrom(runtime.OpenOrRetry(prepared[0]))[0];
            EquipmentDefinition firstDefinition = runtime.EquipmentCatalog.FindEquipmentDefinition(first.DefinitionId);
            Assert.That(first.Augments, Is.Empty);
            Assert.That(firstDefinition.QualityTiers.Count, Is.EqualTo(1));
            Assert.That(first.QualityId, Is.EqualTo(firstDefinition.QualityTiers[0].QualityId));

            AuthoritativeStrongboxSimulatorRuntimeV1 replayRuntime = CreateRuntime(json);
            System.Collections.Generic.IReadOnlyList<AuthoritativeStrongboxPreparedOpenV1> replayPrepared =
                replayRuntime.PrepareBatch(tierNumbers, 70, rootSeed);
            EquipmentInstance replay = replayRuntime.EquipmentFrom(
                replayRuntime.OpenOrRetry(replayPrepared[0]))[0];
            Assert.That(replay.ToCanonicalString(), Is.EqualTo(first.ToCanonicalString()));
            Assert.That(replay.Augments, Is.Empty);
        }

        [Test]
        public void TopBoxOnly_RemainsIneligibleOutsideTopTier()
        {
            string json = File.ReadAllText(BaselinePath);
            CanonicalWeaponCatalogProjectionV1 projection = CreateProjection(json);
            WeaponDefinitionData topOnly = null;
            for (int index = 0; index < projection.WeaponCatalog.Definitions.Count; index++)
            {
                if (projection.WeaponCatalog.Definitions[index].TopBoxOnly)
                {
                    topOnly = projection.WeaponCatalog.Definitions[index];
                    break;
                }
            }
            Assert.That(topOnly, Is.Not.Null);

            int topTier = ProductionStrongboxCatalogV1.Tiers.Count;
            var eligibility =
                WeaponDefinitionDropEligibilityPolicyV1.CreateBaselineV1();
            Assert.That(
                eligibility.IsEligible(
                    new WeaponDefinitionDropEligibilityContextV1(
                        topOnly,
                        topTier - 1,
                        topTier)),
                Is.False);
            Assert.That(
                eligibility.IsEligible(
                    new WeaponDefinitionDropEligibilityContextV1(
                        topOnly,
                        topTier,
                        topTier)),
                Is.True);

            ulong policyWeight = WeaponDefinitionDropWeightPolicyV1
                .CreateBaselineV1()
                .EvaluateWeightUnits(Context(
                    PolicyForTier(topTier - 1),
                    topTier - 1,
                    topOnly,
                    RarityId(topOnly.Rarity),
                    100));
            Assert.That(policyWeight, Is.GreaterThan(0UL));
        }

        private static WeaponDefinitionDropWeightContextV1 Context(
            StrongboxHybridLootPolicyV1 policy,
            int tier,
            WeaponDefinitionData definition,
            StableId rarity,
            int targetLevel)
        {
            return new WeaponDefinitionDropWeightContextV1(
                policy,
                tier,
                targetLevel,
                definition,
                rarity);
        }

        private static StrongboxHybridLootPolicyV1 PolicyForTier(int tier)
        {
            StrongboxHybridLootPolicyV1 policy;
            Assert.That(
                ProductionStrongboxHybridLootCatalogV1.TryGet(
                    ProductionStrongboxCatalogV1.Tiers[tier - 1].TierStableId,
                    out policy),
                Is.True);
            return policy;
        }

        private static WeaponDefinitionData FindDefinitionNear(
            CanonicalWeaponCatalogProjectionV1 projection,
            int level,
            bool topOnly)
        {
            WeaponDefinitionData best = null;
            int bestDistance = int.MaxValue;
            for (int index = 0; index < projection.WeaponCatalog.Definitions.Count; index++)
            {
                WeaponDefinitionData candidate = projection.WeaponCatalog.Definitions[index];
                if (candidate.TopBoxOnly != topOnly) continue;
                int distance = Math.Abs(candidate.PeakDropLevel - level);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            Assert.That(best, Is.Not.Null);
            return best;
        }

        private static StableId RarityId(string rarity)
        {
            switch ((rarity ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "common": return StrongboxDefinitionRarityIdsV1.Common;
                case "rare": return StrongboxDefinitionRarityIdsV1.Rare;
                case "epic": return StrongboxDefinitionRarityIdsV1.Epic;
                case "legendary": return StrongboxDefinitionRarityIdsV1.Legendary;
                default: return StrongboxDefinitionRarityIdsV1.MythicArtifact;
            }
        }

        private static int[] RepeatTier(int tierNumber, int count)
        {
            var values = new int[count];
            for (int index = 0; index < values.Length; index++) values[index] = tierNumber;
            return values;
        }

        private static AuthoritativeStrongboxSimulatorRuntimeV1 CreateRuntime(string json)
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime;
            string diagnostic;
            Assert.That(
                AuthoritativeStrongboxSimulatorRuntimeV1.TryCreate(json, out runtime, out diagnostic),
                Is.True,
                diagnostic);
            return runtime;
        }

        private static CanonicalWeaponCatalogProjectionV1 CreateProjection(string json)
        {
            CanonicalWeaponCatalogProjectionV1 projection;
            string diagnostic;
            Assert.That(
                CanonicalWeaponCatalogProjectionV1.TryCreate(
                    new StringWeaponCatalogSourceV1("production-test", json),
                    WeaponRarityNormalizationPolicyV1.CreateBaselineV1(),
                    out projection,
                    out diagnostic),
                Is.True,
                diagnostic);
            return projection;
        }
    }
}

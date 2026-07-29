using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.Sources
{
    public sealed class LootSourceScopeAndPreviewTests
    {
        private readonly List<Object> _created = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                if (_created[index] != null)
                {
                    Object.Destroy(_created[index]);
                }
            }

            _created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MultipleCompatibleNearestScopesFailClosed()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            RewardProfileDefinitionAsset profile = CreateProfile(false);
            GameObject parent = Track(new GameObject("AmbiguousScopeParent"));
            GameplayScene first = parent.AddComponent<GameplayScene>();
            first.ConfigureForTests(
                "scope.first",
                "scope.gameplay",
                "projection.first",
                "run.test",
                0L);
            GameplayScene second = parent.AddComponent<GameplayScene>();
            second.ConfigureForTests(
                "scope.second",
                "scope.gameplay",
                "projection.second",
                "run.test",
                0L);
            PlacedObject placed = CreatePlaced(
                parent.transform,
                family,
                "placed.reward-source-a");
            LootSourceSetup source =
                placed.gameObject.AddComponent<LootSourceSetup>();
            source.ConfigureForTests(
                placed,
                profile,
                LootSourceOverrideAuthoring.Inherit("reward-override.inherit"),
                null,
                false);

            LootSourceResolutionResult result = source.ResolvePreview();

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(LootSourceResolutionStatus.PlacedObjectBindingFailed));
            Assert.That(first.RegisteredParticipantCount, Is.EqualTo(0));
            Assert.That(second.RegisteredParticipantCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResolvedPreviewIgnoresSerializedProfileListOrder()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            RewardProfileDefinitionAsset firstProfile = CreateProfile(false);
            RewardProfileDefinitionAsset reversedProfile = CreateProfile(true);
            GameObject scopeObject = Track(new GameObject("Scope"));
            GameplayScene scope =
                scopeObject.AddComponent<GameplayScene>();
            scope.ConfigureForTests(
                "scope.primary",
                "scope.gameplay",
                "projection.primary",
                "run.test",
                0L);
            PlacedObject placed = CreatePlaced(
                scope.transform,
                family,
                "placed.reward-source-a");
            LootSourceSetup source =
                placed.gameObject.AddComponent<LootSourceSetup>();
            LootSourceOverrideAuthoring inherit =
                LootSourceOverrideAuthoring.Inherit("reward-override.inherit");
            source.ConfigureForTests(placed, firstProfile, inherit, null, false);
            LootSourceResolutionResult first = source.ResolvePreview();
            Assert.That(first.IsResolved, Is.True, first.Diagnostic);

            source.ConfigureForTests(placed, reversedProfile, inherit, null, false);
            LootSourceResolutionResult second = source.ResolvePreview();

            Assert.That(second.IsResolved, Is.True, second.Diagnostic);
            Assert.That(
                second.Preview.ResolvedProfile,
                Is.EqualTo(first.Preview.ResolvedProfile));
            Assert.That(
                second.Preview.OperationRequest.Fingerprint,
                Is.EqualTo(first.Preview.OperationRequest.Fingerprint));
            Assert.That(second.Preview.Fingerprint, Is.EqualTo(first.Preview.Fingerprint));
            yield return null;
        }

        private ObjectFamilyDefinitionAsset CreateFamily()
        {
            ObjectCapabilityDefinitionAsset presentation = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.presentation",
                    new CapabilityFieldAuthoring(
                        "field.sprite",
                        CapabilityFieldValue.FromStableId(
                            StableId.Parse("sprite.reward-source")))));
            return Track(
                ObjectFamilyDefinitionAsset.CreateRuntime(
                    "family.reward-source",
                    "Reward source",
                    "variant.standard",
                    new[] { presentation },
                    new ObjectVariantAuthoring(
                        "variant.standard",
                        null,
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.presentation"))));
        }

        private RewardProfileDefinitionAsset CreateProfile(bool reversed)
        {
            RewardGrantAuthoring money = new RewardGrantAuthoring(
                "reward-grant.money",
                RewardGrantKind.Money,
                "currency.money",
                1L,
                3L);
            RewardGrantAuthoring scrap = new RewardGrantAuthoring(
                "reward-grant.scrap",
                RewardGrantKind.Scrap,
                "currency.scrap",
                2L,
                4L);
            return Track(
                RewardProfileDefinitionAsset.CreateRuntime(
                    "reward-profile.default",
                    false,
                    reversed ? new[] { scrap, money } : new[] { money, scrap },
                    new IndependentRewardRollAuthoring[0],
                    new ExclusiveRewardGroupAuthoring[0]));
        }

        private PlacedObject CreatePlaced(
            Transform parent,
            ObjectFamilyDefinitionAsset family,
            string placedId)
        {
            GameObject value = Track(new GameObject("LootSource"));
            value.transform.SetParent(parent);
            PlacedObject placed =
                value.AddComponent<PlacedObject>();
            placed.ConfigureForTests(
                placedId,
                family,
                "variant.standard",
                null,
                "scope.gameplay",
                new CapabilityOverrideAuthoring[0]);
            return placed;
        }

        private T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }
    }
}

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
    public sealed class RewardSourceScopeAndPreviewTests
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
            GameplaySceneScope2D first = parent.AddComponent<GameplaySceneScope2D>();
            first.ConfigureForTests(
                "scope.first",
                "scope.gameplay",
                "projection.first",
                "run.test",
                0L);
            GameplaySceneScope2D second = parent.AddComponent<GameplaySceneScope2D>();
            second.ConfigureForTests(
                "scope.second",
                "scope.gameplay",
                "projection.second",
                "run.test",
                0L);
            PlacedObjectAuthoring2D placed = CreatePlaced(
                parent.transform,
                family,
                "placed.reward-source-a");
            RewardSourceAuthoring2D source =
                placed.gameObject.AddComponent<RewardSourceAuthoring2D>();
            source.ConfigureForTests(
                placed,
                profile,
                RewardSourceOverrideAuthoring.Inherit("reward-override.inherit"),
                null,
                false);

            RewardSourceResolutionResult result = source.ResolvePreview();

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(RewardSourceResolutionStatus.PlacedObjectBindingFailed));
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
            GameplaySceneScope2D scope =
                scopeObject.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                "scope.primary",
                "scope.gameplay",
                "projection.primary",
                "run.test",
                0L);
            PlacedObjectAuthoring2D placed = CreatePlaced(
                scope.transform,
                family,
                "placed.reward-source-a");
            RewardSourceAuthoring2D source =
                placed.gameObject.AddComponent<RewardSourceAuthoring2D>();
            RewardSourceOverrideAuthoring inherit =
                RewardSourceOverrideAuthoring.Inherit("reward-override.inherit");
            source.ConfigureForTests(placed, firstProfile, inherit, null, false);
            RewardSourceResolutionResult first = source.ResolvePreview();
            Assert.That(first.IsResolved, Is.True, first.Diagnostic);

            source.ConfigureForTests(placed, reversedProfile, inherit, null, false);
            RewardSourceResolutionResult second = source.ResolvePreview();

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
                RewardGrantKindV1.Money,
                "currency.money",
                1L,
                3L);
            RewardGrantAuthoring scrap = new RewardGrantAuthoring(
                "reward-grant.scrap",
                RewardGrantKindV1.Scrap,
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

        private PlacedObjectAuthoring2D CreatePlaced(
            Transform parent,
            ObjectFamilyDefinitionAsset family,
            string placedId)
        {
            GameObject value = Track(new GameObject("RewardSource"));
            value.transform.SetParent(parent);
            PlacedObjectAuthoring2D placed =
                value.AddComponent<PlacedObjectAuthoring2D>();
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

using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Content.Definitions.Rewards.GameplayDrops;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.GameplayDrops;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.GameplayDrops
{
    public sealed class GameplayDropSource2DTests
    {
        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object value = created[index];
                if (value != null)
                {
                    UnityEngine.Object.Destroy(value);
                }
            }

            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedTerminalCallbacksReuseOneDuplicateSafeOperation()
        {
            GameplaySceneScope2D scope = CreateScope();
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplayDropProfileDefinitionAsset profile = MoneyProfile();
            RecordingGameplayDropSink sink =
                Track(new GameObject("GameplayDropSink"))
                    .AddComponent<RecordingGameplayDropSink>();
            GameplayDropSource2D source = CreateSource(
                "Enemy",
                "placed.enemy-a",
                scope,
                family,
                profile,
                GameplayDropOverrideAuthoring.Default(
                    "gameplay-drop-override.default"),
                sink);

            RewardSourceSubmissionResult first = source.SubmitGameplayDrop();
            RewardSourceSubmissionResult duplicate = source.SubmitGameplayDrop();

            Assert.That(first.Status, Is.EqualTo(RewardSourceSubmissionStatus.Accepted));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(RewardSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(sink.SubmissionCount, Is.EqualTo(2));
            Assert.That(
                sink.FirstPreview.OperationRequest.SourceOperationStableId,
                Is.EqualTo(sink.LastPreview.OperationRequest.SourceOperationStableId));
            Assert.That(
                sink.FirstPreview.OperationRequest.Fingerprint,
                Is.EqualTo(sink.LastPreview.OperationRequest.Fingerprint));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PropsTurretsDroidsAndBossesUseTheSameSourceContract()
        {
            GameplaySceneScope2D scope = CreateScope();
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplayDropProfileDefinitionAsset profile = MoneyProfile();
            RecordingGameplayDropSink sink =
                Track(new GameObject("GameplayDropSink"))
                    .AddComponent<RecordingGameplayDropSink>();
            string[] hostNames = { "Prop", "Turret", "Droid", "Boss" };
            var operationIds = new HashSet<StableId>();

            for (int index = 0; index < hostNames.Length; index++)
            {
                GameplayDropSource2D component = CreateSource(
                    hostNames[index],
                    "placed.drop-host-" + index,
                    scope,
                    family,
                    profile,
                    GameplayDropOverrideAuthoring.Default(
                        "gameplay-drop-override.default-" + index),
                    sink);
                IGameplayDropSourceV1 source = component;
                GameplayDropResolutionResult resolution = source.ResolveGameplayDrop();

                Assert.That(resolution.IsResolved, Is.True, resolution.Diagnostic);
                Assert.That(
                    operationIds.Add(
                        resolution.Operation.OperationRequest.SourceOperationStableId),
                    Is.True);
            }

            Assert.That(operationIds.Count, Is.EqualTo(hostNames.Length));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForcedNoneAndAppendOverridesAreVisibleToTheSharedSink()
        {
            GameplaySceneScope2D scope = CreateScope();
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplayDropProfileDefinitionAsset profile = MoneyProfile();
            RecordingGameplayDropSink sink =
                Track(new GameObject("GameplayDropSink"))
                    .AddComponent<RecordingGameplayDropSink>();
            GameplayDropSource2D none = CreateSource(
                "NoDropEnemy",
                "placed.none-enemy",
                scope,
                family,
                profile,
                GameplayDropOverrideAuthoring.ForcedNone(
                    "gameplay-drop-override.none",
                    "gameplay-drop-profile.none"),
                sink);
            GameplayDropSource2D appended = CreateSource(
                "BonusEnemy",
                "placed.bonus-enemy",
                scope,
                family,
                profile,
                GameplayDropOverrideAuthoring.AppendGuaranteedReward(
                    "gameplay-drop-override.append",
                    "gameplay-drop-profile.appended",
                    new RewardGrantAuthoring(
                        "gameplay-drop-grant.scrap",
                        RewardGrantKindV1.Scrap,
                        "currency.scrap",
                        2L,
                        2L)),
                sink);

            GameplayDropResolutionResult noneResolution = none.ResolveGameplayDrop();
            GameplayDropResolutionResult appendResolution = appended.ResolveGameplayDrop();

            Assert.That(
                noneResolution.Operation.ResolvedProfile.Disposition,
                Is.EqualTo(RewardProfileDispositionV1.ExplicitNoDrop));
            Assert.That(
                appendResolution.Operation.ResolvedProfile.GuaranteedEntries.Count,
                Is.EqualTo(2));
            yield return null;
        }

        private GameplayDropSource2D CreateSource(
            string name,
            string placedId,
            GameplaySceneScope2D scope,
            ObjectFamilyDefinitionAsset family,
            GameplayDropProfileDefinitionAsset profile,
            GameplayDropOverrideAuthoring dropOverride,
            MonoBehaviour sink)
        {
            GameObject value = Track(new GameObject(name));
            value.transform.SetParent(scope.transform);
            PlacedObjectAuthoring2D placed =
                value.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                placedId,
                family,
                "variant.standard",
                scope,
                "scope.gameplay",
                Array.Empty<CapabilityOverrideAuthoring>());

            GameplayDropSource2D source =
                value.AddComponent<GameplayDropSource2D>();
            source.ConfigureForTests(placed, profile, dropOverride, sink);
            return source;
        }

        private GameplaySceneScope2D CreateScope()
        {
            GameObject root = Track(new GameObject("GameplayScope"));
            GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                "scope.gameplay-drop",
                "scope.gameplay",
                "projection.gameplay-drop",
                "run.gameplay-drop",
                0L);
            return scope;
        }

        private ObjectFamilyDefinitionAsset CreateFamily()
        {
            ObjectCapabilityDefinitionAsset presentation = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.presentation",
                    new CapabilityFieldAuthoring(
                        "field.sprite",
                        CapabilityFieldValue.FromStableId(
                            StableId.Parse("sprite.gameplay-drop-host")))));
            return Track(
                ObjectFamilyDefinitionAsset.CreateRuntime(
                    "family.gameplay-drop-host",
                    "Gameplay drop host",
                    "variant.standard",
                    new[] { presentation },
                    new ObjectVariantAuthoring(
                        "variant.standard",
                        null,
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.presentation"))));
        }

        private GameplayDropProfileDefinitionAsset MoneyProfile()
        {
            return Track(
                GameplayDropProfileDefinitionAsset.CreateRuntime(
                    "gameplay-drop-profile.money",
                    false,
                    new[]
                    {
                        new RewardGrantAuthoring(
                            "gameplay-drop-grant.money",
                            RewardGrantKindV1.Money,
                            "currency.money",
                            5L,
                            5L),
                    },
                    Array.Empty<IndependentRewardRollAuthoring>(),
                    Array.Empty<ExclusiveRewardGroupAuthoring>()));
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }
    }

    public sealed class RecordingGameplayDropSink :
        MonoBehaviour,
        IRewardSourceOperationSink
    {
        public int SubmissionCount { get; private set; }

        public RewardSourceResolvedPreview FirstPreview { get; private set; }

        public RewardSourceResolvedPreview LastPreview { get; private set; }

        public RewardSourceSubmissionResult Submit(
            RewardSourceResolvedPreview preview)
        {
            SubmissionCount++;
            LastPreview = preview;
            if (FirstPreview == null)
            {
                FirstPreview = preview;
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Accepted,
                    "Accepted first gameplay drop operation.");
            }

            RewardOperationIdentityComparisonV1 comparison =
                RewardOperationIdentityV1.Classify(
                    FirstPreview.OperationRequest,
                    preview.OperationRequest);
            if (comparison == RewardOperationIdentityComparisonV1.ExactDuplicateNoChange)
            {
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.ExactDuplicateNoChange,
                    "Exact gameplay drop duplicate produced no additional operation.");
            }

            if (comparison == RewardOperationIdentityComparisonV1.DistinctOperation)
            {
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Accepted,
                    "Accepted distinct gameplay drop operation.");
            }

            return new RewardSourceSubmissionResult(
                RewardSourceSubmissionStatus.ConflictingDuplicate,
                "Rejected conflicting gameplay drop operation.");
        }
    }
}

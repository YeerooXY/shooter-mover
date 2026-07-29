using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Content.Definitions.Rewards.LootDrops;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.LootDrops;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.LootDrops
{
    public sealed class LootEmitterTests
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
            GameplayScene scope = CreateScope();
            ObjectFamilyDefinitionAsset family = CreateFamily();
            LootDropProfileDefinitionAsset profile = MoneyProfile();
            RecordingLootDropSink sink =
                Track(new GameObject("LootDropSink"))
                    .AddComponent<RecordingLootDropSink>();
            LootEmitter source = CreateSource(
                "Enemy",
                "placed.enemy-a",
                scope,
                family,
                profile,
                LootDropOverrideAuthoring.Default(
                    "gameplay-drop-override.default"),
                sink);

            LootSourceSubmissionResult first = source.SubmitLootDrop();
            LootSourceSubmissionResult duplicate = source.SubmitLootDrop();

            Assert.That(first.Status, Is.EqualTo(LootSourceSubmissionStatus.Accepted));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(LootSourceSubmissionStatus.ExactDuplicateNoChange));
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
            GameplayScene scope = CreateScope();
            ObjectFamilyDefinitionAsset family = CreateFamily();
            LootDropProfileDefinitionAsset profile = MoneyProfile();
            RecordingLootDropSink sink =
                Track(new GameObject("LootDropSink"))
                    .AddComponent<RecordingLootDropSink>();
            string[] hostNames = { "Prop", "Turret", "Droid", "Boss" };
            var operationIds = new HashSet<StableId>();

            for (int index = 0; index < hostNames.Length; index++)
            {
                LootEmitter component = CreateSource(
                    hostNames[index],
                    "placed.drop-host-" + index,
                    scope,
                    family,
                    profile,
                    LootDropOverrideAuthoring.Default(
                        "gameplay-drop-override.default-" + index),
                    sink);
                ILootDropSource source = component;
                LootDropResolutionResult resolution = source.ResolveLootDrop();

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
            GameplayScene scope = CreateScope();
            ObjectFamilyDefinitionAsset family = CreateFamily();
            LootDropProfileDefinitionAsset profile = MoneyProfile();
            RecordingLootDropSink sink =
                Track(new GameObject("LootDropSink"))
                    .AddComponent<RecordingLootDropSink>();
            LootEmitter none = CreateSource(
                "NoDropEnemy",
                "placed.none-enemy",
                scope,
                family,
                profile,
                LootDropOverrideAuthoring.ForcedNone(
                    "gameplay-drop-override.none",
                    "gameplay-drop-profile.none"),
                sink);
            LootEmitter appended = CreateSource(
                "BonusEnemy",
                "placed.bonus-enemy",
                scope,
                family,
                profile,
                LootDropOverrideAuthoring.AppendGuaranteedReward(
                    "gameplay-drop-override.append",
                    "gameplay-drop-profile.appended",
                    new RewardGrantAuthoring(
                        "gameplay-drop-grant.scrap",
                        RewardGrantKind.Scrap,
                        "currency.scrap",
                        2L,
                        2L)),
                sink);

            LootDropResolutionResult noneResolution = none.ResolveLootDrop();
            LootDropResolutionResult appendResolution = appended.ResolveLootDrop();

            Assert.That(
                noneResolution.Operation.ResolvedProfile.Disposition,
                Is.EqualTo(RewardProfileDisposition.ExplicitNoDrop));
            Assert.That(
                appendResolution.Operation.ResolvedProfile.GuaranteedEntries.Count,
                Is.EqualTo(2));
            yield return null;
        }

        private LootEmitter CreateSource(
            string name,
            string placedId,
            GameplayScene scope,
            ObjectFamilyDefinitionAsset family,
            LootDropProfileDefinitionAsset profile,
            LootDropOverrideAuthoring dropOverride,
            MonoBehaviour sink)
        {
            GameObject value = Track(new GameObject(name));
            value.transform.SetParent(scope.transform);
            PlacedObject placed =
                value.AddComponent<PlacedObject>();
            placed.ConfigureForTests(
                placedId,
                family,
                "variant.standard",
                scope,
                "scope.gameplay",
                Array.Empty<CapabilityOverrideAuthoring>());

            LootEmitter source =
                value.AddComponent<LootEmitter>();
            source.ConfigureForTests(placed, profile, dropOverride, sink);
            return source;
        }

        private GameplayScene CreateScope()
        {
            GameObject root = Track(new GameObject("GameplayScope"));
            GameplayScene scope = root.AddComponent<GameplayScene>();
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

        private LootDropProfileDefinitionAsset MoneyProfile()
        {
            return Track(
                LootDropProfileDefinitionAsset.CreateRuntime(
                    "gameplay-drop-profile.money",
                    false,
                    new[]
                    {
                        new RewardGrantAuthoring(
                            "gameplay-drop-grant.money",
                            RewardGrantKind.Money,
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

    public sealed class RecordingLootDropSink :
        MonoBehaviour,
        ILootSourceOperationSink
    {
        public int SubmissionCount { get; private set; }

        public LootSourceResolvedPreview FirstPreview { get; private set; }

        public LootSourceResolvedPreview LastPreview { get; private set; }

        public LootSourceSubmissionResult Submit(
            LootSourceResolvedPreview preview)
        {
            SubmissionCount++;
            LastPreview = preview;
            if (FirstPreview == null)
            {
                FirstPreview = preview;
                return new LootSourceSubmissionResult(
                    LootSourceSubmissionStatus.Accepted,
                    "Accepted first gameplay drop operation.");
            }

            RewardOperationIdentityComparison comparison =
                RewardOperationIdentity.Classify(
                    FirstPreview.OperationRequest,
                    preview.OperationRequest);
            if (comparison == RewardOperationIdentityComparison.ExactDuplicateNoChange)
            {
                return new LootSourceSubmissionResult(
                    LootSourceSubmissionStatus.ExactDuplicateNoChange,
                    "Exact gameplay drop duplicate produced no additional operation.");
            }

            if (comparison == RewardOperationIdentityComparison.DistinctOperation)
            {
                return new LootSourceSubmissionResult(
                    LootSourceSubmissionStatus.Accepted,
                    "Accepted distinct gameplay drop operation.");
            }

            return new LootSourceSubmissionResult(
                LootSourceSubmissionStatus.ConflictingDuplicate,
                "Rejected conflicting gameplay drop operation.");
        }
    }
}

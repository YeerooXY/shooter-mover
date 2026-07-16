using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.Sources
{
    public sealed class RewardSourceAuthoring2DTests
    {
        private readonly List<Object> _created = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                Object value = _created[index];
                if (value != null)
                {
                    Object.Destroy(value);
                }
            }

            _created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NearestParentBindingKeepsOperationStableAcrossRenameAndReparent()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            RewardProfileDefinitionAsset profile = CreateProfile(
                "reward-profile.default",
                "reward-grant.money");
            GameplaySceneScope2D firstScope = CreateScope(
                "FirstScope",
                "scope.first",
                "run.test");
            GameplaySceneScope2D secondScope = CreateScope(
                "SecondScope",
                "scope.second",
                "run.test");
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "RewardCrate",
                firstScope.transform,
                "placed.reward-crate-a",
                family,
                null);
            RewardSourceAuthoring2D source = CreateSource(
                placed,
                profile,
                RewardSourceOverrideAuthoring.Inherit("reward-override.inherit"),
                null);

            RewardSourceResolutionResult first = source.ResolvePreview();
            Assert.That(first.IsResolved, Is.True, first.Diagnostic);
            StableId operationId = first.Preview.OperationRequest.SourceOperationStableId;
            string requestFingerprint = first.Preview.OperationRequest.Fingerprint;

            source.gameObject.name = "RenamedRewardCrate";
            source.transform.localPosition = new Vector3(8f, -3f, 0f);
            source.transform.SetParent(secondScope.transform);
            placed.Unbind();
            RewardSourceResolutionResult second = source.ResolvePreview();

            Assert.That(second.IsResolved, Is.True, second.Diagnostic);
            Assert.That(placed.BoundScope, Is.SameAs(secondScope));
            Assert.That(
                second.Preview.OperationRequest.SourceOperationStableId,
                Is.EqualTo(operationId));
            Assert.That(
                second.Preview.OperationRequest.Fingerprint,
                Is.EqualTo(requestFingerprint));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitScopeOnPlacedObjectTakesPrecedenceForRewardSource()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            RewardProfileDefinitionAsset profile = CreateProfile(
                "reward-profile.default",
                "reward-grant.money");
            GameplaySceneScope2D parentScope = CreateScope(
                "ParentScope",
                "scope.parent",
                "run.parent");
            GameplaySceneScope2D explicitScope = CreateScope(
                "ExplicitScope",
                "scope.explicit",
                "run.explicit");
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "RewardCrate",
                parentScope.transform,
                "placed.reward-crate-a",
                family,
                explicitScope);
            RewardSourceAuthoring2D source = CreateSource(
                placed,
                profile,
                RewardSourceOverrideAuthoring.Inherit("reward-override.inherit"),
                null);

            RewardSourceResolutionResult result = source.ResolvePreview();

            Assert.That(result.IsResolved, Is.True, result.Diagnostic);
            Assert.That(placed.BoundScope, Is.SameAs(explicitScope));
            Assert.That(
                result.Preview.OperationRequest.RunStableId,
                Is.EqualTo(StableId.Parse("run.explicit")));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingAndDuplicateScopesFailClosedBeforeOperationSubmission()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            RewardProfileDefinitionAsset profile = CreateProfile(
                "reward-profile.default",
                "reward-grant.money");
            GameObject orphan = Track(new GameObject("Orphan"));
            PlacedObjectAuthoring2D orphanPlaced = CreatePlaced(
                "OrphanReward",
                orphan.transform,
                "placed.orphan-reward",
                family,
                null);
            RewardSourceAuthoring2D orphanSource = CreateSource(
                orphanPlaced,
                profile,
                RewardSourceOverrideAuthoring.Inherit("reward-override.orphan"),
                null);

            RewardSourceResolutionResult missing = orphanSource.ResolvePreview();
            Assert.That(missing.IsResolved, Is.False);
            Assert.That(
                missing.Status,
                Is.EqualTo(RewardSourceResolutionStatus.PlacedObjectBindingFailed));

            GameplaySceneScope2D scope = CreateScope(
                "Scope",
                "scope.primary",
                "run.test");
            PlacedObjectAuthoring2D firstPlaced = CreatePlaced(
                "FirstReward",
                scope.transform,
                "placed.duplicate-reward",
                family,
                null);
            Assert.That(firstPlaced.TryBind().IsBound, Is.True);
            PlacedObjectAuthoring2D duplicatePlaced = CreatePlaced(
                "DuplicateReward",
                scope.transform,
                "placed.duplicate-reward",
                family,
                null);
            RewardSourceAuthoring2D duplicateSource = CreateSource(
                duplicatePlaced,
                profile,
                RewardSourceOverrideAuthoring.Inherit("reward-override.duplicate"),
                null);

            RewardSourceResolutionResult duplicate = duplicateSource.ResolvePreview();
            Assert.That(duplicate.IsResolved, Is.False);
            Assert.That(
                duplicate.Status,
                Is.EqualTo(RewardSourceResolutionStatus.PlacedObjectBindingFailed));
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedCallbacksAndRestartRegistrationReuseOneOperationIdentity()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            RewardProfileDefinitionAsset profile = CreateProfile(
                "reward-profile.default",
                "reward-grant.money");
            GameplaySceneScope2D scope = CreateScope(
                "Scope",
                "scope.primary",
                "run.test");
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "RewardCrate",
                scope.transform,
                "placed.reward-crate-a",
                family,
                null);
            RecordingRewardSourceSink sink = Track(
                new GameObject("RewardSink")).AddComponent<RecordingRewardSourceSink>();
            RewardSourceAuthoring2D source = CreateSource(
                placed,
                profile,
                RewardSourceOverrideAuthoring.MoneyOnly(
                    "reward-override.money",
                    "reward-profile.money-only",
                    "reward-grant.money-only",
                    "currency.money",
                    5L,
                    10L),
                sink);

            RestartParticipantRegistrationResult firstRegistration =
                source.RegisterForRestart();
            RestartParticipantRegistrationResult secondRegistration =
                source.RegisterForRestart();
            Assert.That(firstRegistration.IsAccepted, Is.True);
            Assert.That(
                secondRegistration.Status,
                Is.EqualTo(RestartParticipantRegistrationStatus.DuplicateNoChange));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));

            RewardSourceSubmissionResult firstSubmit = source.SubmitResolution();
            RewardSourceSubmissionResult duplicateSubmit = source.SubmitResolution();
            StableId operationId = sink.AcceptedPreview
                .OperationRequest.SourceOperationStableId;
            string requestFingerprint = sink.AcceptedPreview.OperationRequest.Fingerprint;

            Assert.That(firstSubmit.Status, Is.EqualTo(RewardSourceSubmissionStatus.Accepted));
            Assert.That(
                duplicateSubmit.Status,
                Is.EqualTo(RewardSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(sink.SubmissionCount, Is.EqualTo(2));

            scope.RunRestart(1L);
            RewardSourceSubmissionResult afterRestart = source.SubmitResolution();

            Assert.That(
                afterRestart.Status,
                Is.EqualTo(RewardSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(sink.SubmissionCount, Is.EqualTo(3));
            Assert.That(
                sink.LastPreview.OperationRequest.SourceOperationStableId,
                Is.EqualTo(operationId));
            Assert.That(
                sink.LastPreview.OperationRequest.Fingerprint,
                Is.EqualTo(requestFingerprint));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));
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
                            StableId.Parse("sprite.reward-crate")))));
            return Track(
                ObjectFamilyDefinitionAsset.CreateRuntime(
                    "family.reward-crate",
                    "Reward crate",
                    "variant.standard",
                    new[] { presentation },
                    new ObjectVariantAuthoring(
                        "variant.standard",
                        null,
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.presentation"))));
        }

        private RewardProfileDefinitionAsset CreateProfile(
            string profileId,
            string grantId)
        {
            return Track(
                RewardProfileDefinitionAsset.CreateRuntime(
                    profileId,
                    false,
                    new[]
                    {
                        new RewardGrantAuthoring(
                            grantId,
                            RewardGrantKindV1.Money,
                            "currency.money",
                            1L,
                            3L)
                    },
                    new IndependentRewardRollAuthoring[0],
                    new ExclusiveRewardGroupAuthoring[0]));
        }

        private GameplaySceneScope2D CreateScope(
            string name,
            string scopeId,
            string runId)
        {
            GameObject root = Track(new GameObject(name));
            GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
            string suffix = scopeId.Substring(scopeId.IndexOf('.') + 1);
            scope.ConfigureForTests(
                scopeId,
                "scope.gameplay",
                "projection." + suffix,
                runId,
                0L);
            return scope;
        }

        private PlacedObjectAuthoring2D CreatePlaced(
            string name,
            Transform parent,
            string placedId,
            ObjectFamilyDefinitionAsset family,
            GameplaySceneScope2D explicitScope)
        {
            GameObject value = Track(new GameObject(name));
            value.transform.SetParent(parent);
            PlacedObjectAuthoring2D placed =
                value.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                placedId,
                family,
                "variant.standard",
                explicitScope,
                "scope.gameplay",
                new CapabilityOverrideAuthoring[0]);
            return placed;
        }

        private RewardSourceAuthoring2D CreateSource(
            PlacedObjectAuthoring2D placed,
            RewardProfileDefinitionAsset profile,
            RewardSourceOverrideAuthoring sourceOverride,
            MonoBehaviour sink)
        {
            RewardSourceAuthoring2D source =
                placed.gameObject.AddComponent<RewardSourceAuthoring2D>();
            source.ConfigureForTests(
                placed,
                profile,
                sourceOverride,
                sink,
                false);
            return source;
        }

        private T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }
    }

    public sealed class RecordingRewardSourceSink :
        MonoBehaviour,
        IRewardSourceOperationSink
    {
        public int SubmissionCount { get; private set; }

        public RewardSourceResolvedPreview AcceptedPreview { get; private set; }

        public RewardSourceResolvedPreview LastPreview { get; private set; }

        public RewardSourceSubmissionResult Submit(
            RewardSourceResolvedPreview preview)
        {
            SubmissionCount++;
            LastPreview = preview;
            if (AcceptedPreview == null)
            {
                AcceptedPreview = preview;
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.Accepted,
                    "Accepted first source operation.");
            }

            RewardOperationIdentityComparisonV1 comparison =
                RewardOperationIdentityV1.Classify(
                    AcceptedPreview.OperationRequest,
                    preview.OperationRequest);
            if (comparison == RewardOperationIdentityComparisonV1.ExactDuplicateNoChange)
            {
                return new RewardSourceSubmissionResult(
                    RewardSourceSubmissionStatus.ExactDuplicateNoChange,
                    "Exact duplicate source operation produced no change.");
            }

            return new RewardSourceSubmissionResult(
                RewardSourceSubmissionStatus.ConflictingDuplicate,
                "Conflicting source operation payload.");
        }
    }
}

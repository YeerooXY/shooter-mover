using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Authoring
{
    public sealed class PlacedObjectAuthoring2DTests
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
        public IEnumerator IdentitySurvivesRenameReparentSiblingAndTransformChanges()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D firstScope =
                CreateScope("FirstScope", "scope.first");
            GameplaySceneScope2D secondScope =
                CreateScope("SecondScope", "scope.second");
            GameObject firstContainer = Track(new GameObject("FirstContainer"));
            firstContainer.transform.SetParent(firstScope.transform);
            GameObject secondContainer = Track(new GameObject("SecondContainer"));
            secondContainer.transform.SetParent(secondScope.transform);
            GameObject sibling = Track(new GameObject("Sibling"));
            sibling.transform.SetParent(firstContainer.transform);

            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Crate",
                firstContainer.transform,
                "placed.crate-a",
                family,
                null);
            SceneScopeBindingResult first = placed.TryBind();
            Assert.That(first.IsBound, Is.True, first.Diagnostic);
            StableId original = placed.ResolvedIdentity.Value;

            placed.gameObject.name = "RenamedCrate";
            placed.transform.localPosition = new Vector3(19f, -7f, 0f);
            placed.transform.localRotation = Quaternion.Euler(0f, 0f, 123f);
            placed.transform.localScale = new Vector3(2f, 3f, 1f);
            placed.transform.SetSiblingIndex(0);
            placed.transform.SetParent(secondContainer.transform);

            placed.Unbind();
            SceneScopeBindingResult rebound = placed.TryBind();

            Assert.That(rebound.IsBound, Is.True, rebound.Diagnostic);
            Assert.That(placed.ResolvedIdentity.Value, Is.EqualTo(original));
            Assert.That(placed.BoundScope, Is.SameAs(secondScope));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DistinctIdsRegisterIndependently()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope =
                CreateScope("Scope", "scope.primary");
            PlacedObjectAuthoring2D first = CreatePlaced(
                "First",
                scope.transform,
                "placed.crate-a",
                family,
                null);
            PlacedObjectAuthoring2D second = CreatePlaced(
                "Second",
                scope.transform,
                "placed.crate-b",
                family,
                null);

            Assert.That(first.TryBind().IsBound, Is.True);
            Assert.That(second.TryBind().IsBound, Is.True);
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateIdsInOneScopeFailClosedAndReportBothPlacements()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope =
                CreateScope("Scope", "scope.primary");
            PlacedObjectAuthoring2D first = CreatePlaced(
                "FirstCrate",
                scope.transform,
                "placed.shared",
                family,
                null);
            PlacedObjectAuthoring2D second = CreatePlaced(
                "SecondCrate",
                scope.transform,
                "placed.shared",
                family,
                null);

            Assert.That(first.TryBind().IsBound, Is.True);
            SceneScopeBindingResult duplicate = second.TryBind();

            Assert.That(duplicate.IsBound, Is.False);
            Assert.That(
                duplicate.DiagnosticCode,
                Is.EqualTo(
                    SceneScopeBindingDiagnosticCode.DuplicatePlacedIdentity));
            Assert.That(second.BoundScope, Is.Null);
            Assert.That(
                duplicate.RegistrationResult.ExistingLocation,
                Does.Contain("FirstCrate"));
            Assert.That(
                duplicate.RegistrationResult.AttemptedLocation,
                Does.Contain("SecondCrate"));
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator IdenticalIdsInSeparateScopesDoNotCrossBind()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D firstScope =
                CreateScope("FirstScope", "scope.first");
            GameplaySceneScope2D secondScope =
                CreateScope("SecondScope", "scope.second");
            PlacedObjectAuthoring2D first = CreatePlaced(
                "First",
                firstScope.transform,
                "placed.shared",
                family,
                null);
            PlacedObjectAuthoring2D second = CreatePlaced(
                "Second",
                secondScope.transform,
                "placed.shared",
                family,
                null);

            Assert.That(first.TryBind().IsBound, Is.True);
            Assert.That(second.TryBind().IsBound, Is.True);
            Assert.That(first.BoundScope, Is.SameAs(firstScope));
            Assert.That(second.BoundScope, Is.SameAs(secondScope));
            Assert.That(firstScope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That(secondScope.RegisteredParticipantCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitScopeTakesPrecedenceOverNearestParent()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D parentScope =
                CreateScope("ParentScope", "scope.parent");
            GameplaySceneScope2D explicitScope =
                CreateScope("ExplicitScope", "scope.explicit");
            GameObject container = Track(new GameObject("Container"));
            container.transform.SetParent(parentScope.transform);
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Crate",
                container.transform,
                "placed.crate-a",
                family,
                explicitScope);

            SceneScopeBindingResult result = placed.TryBind();

            Assert.That(result.IsBound, Is.True, result.Diagnostic);
            Assert.That(placed.BoundScope, Is.SameAs(explicitScope));
            Assert.That(parentScope.RegisteredParticipantCount, Is.EqualTo(0));
            Assert.That(explicitScope.RegisteredParticipantCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingScopeFailsClosed()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameObject orphanParent = Track(new GameObject("Orphan"));
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Crate",
                orphanParent.transform,
                "placed.crate-a",
                family,
                null);

            SceneScopeBindingResult result = placed.TryBind();

            Assert.That(result.IsBound, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(SceneScopeBindingStatus.MissingScope));
            Assert.That(placed.BoundScope, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IncompatibleExplicitScopeFailsWithoutParentFallback()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D compatibleParent =
                CreateScope("CompatibleParent", "scope.parent");
            GameplaySceneScope2D incompatibleExplicit =
                CreateScope(
                    "IncompatibleExplicit",
                    "scope.explicit",
                    "scope.other");
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Crate",
                compatibleParent.transform,
                "placed.crate-a",
                family,
                incompatibleExplicit);

            SceneScopeBindingResult result = placed.TryBind();

            Assert.That(result.IsBound, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(
                    SceneScopeBindingStatus.IncompatibleExplicitScope));
            Assert.That(compatibleParent.RegisteredParticipantCount, Is.EqualTo(0));
            Assert.That(incompatibleExplicit.RegisteredParticipantCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MultipleCompatibleScopesAtNearestAncestorFailClosed()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameObject parent = Track(new GameObject("AmbiguousParent"));
            GameplaySceneScope2D first =
                parent.AddComponent<GameplaySceneScope2D>();
            first.ConfigureForTests(
                "scope.first",
                "scope.gameplay",
                "projection.first",
                "run.test",
                0);
            GameplaySceneScope2D second =
                parent.AddComponent<GameplaySceneScope2D>();
            second.ConfigureForTests(
                "scope.second",
                "scope.gameplay",
                "projection.second",
                "run.test",
                0);
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Crate",
                parent.transform,
                "placed.crate-a",
                family,
                null);

            SceneScopeBindingResult result = placed.TryBind();

            Assert.That(result.IsBound, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(
                    SceneScopeBindingStatus.ConflictingParentScopes));
            Assert.That(first.RegisteredParticipantCount, Is.EqualTo(0));
            Assert.That(second.RegisteredParticipantCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArbitraryNestedHierarchyBindsToNearestCompatibleAncestor()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope =
                CreateScope("Scope", "scope.primary");
            Transform current = scope.transform;
            for (int index = 0; index < 12; index++)
            {
                GameObject nested =
                    Track(new GameObject("Nested" + index));
                nested.transform.SetParent(current);
                current = nested.transform;
            }

            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Crate",
                current,
                "placed.crate-a",
                family,
                null);

            Assert.That(placed.TryBind().IsBound, Is.True);
            Assert.That(placed.BoundScope, Is.SameAs(scope));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeSpawnUsesOnlyExplicitSpawnIdentityInput()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope =
                CreateScope("Scope", "scope.primary");
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Generated Name 481",
                scope.transform,
                "INVALID AUTHORED ID",
                family,
                null);
            placed.SetRuntimeSpawnIdentity(
                new RuntimeSpawnIdentityInput(
                    StableId.Parse("placed.spawned-a"),
                    StableId.Parse("spawn.operation-a")));

            SceneScopeBindingResult result = placed.TryBind();

            Assert.That(result.IsBound, Is.True, result.Diagnostic);
            Assert.That(
                placed.ResolvedIdentity.Kind,
                Is.EqualTo(PlacedObjectIdentityKind.RuntimeSpawned));
            Assert.That(
                placed.ResolvedIdentity.Value,
                Is.EqualTo(StableId.Parse("placed.spawned-a")));
            yield return null;
        }


        [UnityTest]
        public IEnumerator FiftyRestartRebindsRemainSingleRegistration()
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplaySceneScope2D scope =
                CreateScope("Scope", "scope.primary");
            PlacedObjectAuthoring2D placed = CreatePlaced(
                "Crate",
                scope.transform,
                "placed.crate-a",
                family,
                null);
            Assert.That(placed.TryBind().IsBound, Is.True);

            StubRestartParticipant participant =
                new StubRestartParticipant("restart.crate-a");
            object owner = new object();
            Assert.That(
                placed.RegisterRestartParticipant(
                    participant,
                    owner,
                    "Scope/Crate").IsAccepted,
                Is.True);

            for (int generation = 1; generation <= 50; generation++)
            {
                placed.Unbind();
                scope.RunRestart(generation);
                SceneScopeBindingResult rebound = placed.TryBind();

                Assert.That(rebound.IsBound, Is.True, rebound.Diagnostic);
                Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(1));
                RestartParticipantRegistrationResult restartRetry =
                    placed.RegisterRestartParticipant(
                        participant,
                        owner,
                        "Scope/Crate");
                Assert.That(
                    restartRetry.Status,
                    Is.EqualTo(
                        RestartParticipantRegistrationStatus.DuplicateNoChange));
                Assert.That(
                    scope.RegisteredRestartParticipantCount,
                    Is.EqualTo(1));
            }

            Assert.That(participant.Phases.Count, Is.EqualTo(200));
            for (int index = 0; index < participant.Phases.Count; index += 4)
            {
                Assert.That(
                    participant.Phases[index],
                    Is.EqualTo(RestartLifecyclePhase.RetireAttempt));
                Assert.That(
                    participant.Phases[index + 1],
                    Is.EqualTo(
                        RestartLifecyclePhase.ReleaseTransientResources));
                Assert.That(
                    participant.Phases[index + 2],
                    Is.EqualTo(
                        RestartLifecyclePhase.ApplyResetProjection));
                Assert.That(
                    participant.Phases[index + 3],
                    Is.EqualTo(RestartLifecyclePhase.CompleteRebind));
            }

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
                            StableId.Parse("sprite.crate")))));
            ObjectCapabilityDefinitionAsset collision = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.collision",
                    new CapabilityFieldAuthoring(
                        "field.blocks",
                        CapabilityFieldValue.FromBoolean(true))));
            return Track(
                ObjectFamilyDefinitionAsset.CreateRuntime(
                    "family.crate",
                    "Crate",
                    "variant.standard",
                    new[] { presentation, collision },
                    new ObjectVariantAuthoring(
                        "variant.standard",
                        null,
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.presentation"),
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.collision"))));
        }

        private GameplaySceneScope2D CreateScope(
            string objectName,
            string scopeId,
            string compatibilityId = "scope.gameplay")
        {
            GameObject root = Track(new GameObject(objectName));
            GameplaySceneScope2D scope =
                root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                scopeId,
                compatibilityId,
                "projection." + scopeId.Substring(scopeId.IndexOf('.') + 1),
                "run.test",
                0);
            return scope;
        }

        private PlacedObjectAuthoring2D CreatePlaced(
            string objectName,
            Transform parent,
            string placedId,
            ObjectFamilyDefinitionAsset family,
            GameplaySceneScope2D explicitScope)
        {
            GameObject gameObject = Track(new GameObject(objectName));
            gameObject.transform.SetParent(parent);
            PlacedObjectAuthoring2D placed =
                gameObject.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                placedId,
                family,
                "variant.standard",
                explicitScope,
                "scope.gameplay",
                new CapabilityOverrideAuthoring[0]);
            return placed;
        }


        private sealed class StubRestartParticipant : IRestartParticipant
        {
            public StubRestartParticipant(string id)
            {
                RestartParticipantId = StableId.Parse(id);
                Phases = new List<RestartLifecyclePhase>();
            }

            public StableId RestartParticipantId { get; }

            public List<RestartLifecyclePhase> Phases { get; }

            public void OnRestartPhase(
                RestartContext context,
                RestartLifecyclePhase phase)
            {
                Phases.Add(phase);
            }
        }

        private T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }
    }
}

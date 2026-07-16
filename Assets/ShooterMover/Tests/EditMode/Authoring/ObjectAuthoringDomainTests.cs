using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Authoring
{
    public sealed class ObjectAuthoringDomainTests
    {
        [Test]
        public void FamilySupportsArbitraryVariantCount()
        {
            CapabilityDefinition familyHealth = Capability(
                "capability.health",
                Field("field.maximum", CapabilityFieldValue.FromDecimal(10d)));
            List<ObjectVariantDefinition> variants =
                new List<ObjectVariantDefinition>();

            for (int index = 0; index < 37; index++)
            {
                string variantId = "variant.level-" + index.ToString("00");
                variants.Add(
                    new ObjectVariantDefinition(
                        StableId.Parse(variantId),
                        index,
                        new[]
                        {
                            CapabilitySelection.Override(
                                Capability(
                                    "capability.health",
                                    Field(
                                        "field.maximum",
                                        CapabilityFieldValue.FromDecimal(
                                            10d + index))))
                        }));
            }

            ObjectFamilyDefinition family = new ObjectFamilyDefinition(
                StableId.Parse("family.crate"),
                "Crate",
                StableId.Parse("variant.level-00"),
                new[] { familyHealth },
                variants);

            ObjectDefinitionResolutionResult result =
                ObjectDefinitionResolver.Resolve(
                    family,
                    StableId.Parse("variant.level-36"),
                    null);

            Assert.That(result.IsResolved, Is.True, result.Message);
            Assert.That(family.Variants.Count, Is.EqualTo(37));
            CapabilityDefinition resolved;
            Assert.That(
                result.ResolvedCapabilities.TryGet(
                    StableId.Parse("capability.health"),
                    out resolved),
                Is.True);
            CapabilityField field;
            Assert.That(
                resolved.TryGetField(StableId.Parse("field.maximum"), out field),
                Is.True);
            Assert.That(field.Value.DecimalValue, Is.EqualTo(46d));
        }

        [Test]
        public void VariantCompositionContainsOnlySelectedCapabilities()
        {
            CapabilityDefinition presentation = Capability(
                "capability.presentation",
                Field(
                    "field.sprite",
                    CapabilityFieldValue.FromStableId(
                        StableId.Parse("sprite.crate"))));
            CapabilityDefinition collision = Capability(
                "capability.collision",
                Field("field.blocks", CapabilityFieldValue.FromBoolean(true)));
            CapabilityDefinition combat = Capability(
                "capability.combat",
                Field("field.damage", CapabilityFieldValue.FromDecimal(99d)));

            ObjectFamilyDefinition family = new ObjectFamilyDefinition(
                StableId.Parse("family.crate"),
                "Crate",
                StableId.Parse("variant.standard"),
                new[] { presentation, collision, combat },
                new[]
                {
                    new ObjectVariantDefinition(
                        StableId.Parse("variant.standard"),
                        null,
                        new[]
                        {
                            CapabilitySelection.Inherit(
                                StableId.Parse("capability.presentation")),
                            CapabilitySelection.Inherit(
                                StableId.Parse("capability.collision"))
                        })
                });

            ObjectDefinitionResolutionResult result =
                ObjectDefinitionResolver.Resolve(family, null, null);

            Assert.That(result.IsResolved, Is.True, result.Message);
            Assert.That(result.ResolvedCapabilities.Capabilities.Count, Is.EqualTo(2));
            CapabilityDefinition ignored;
            Assert.That(
                result.ResolvedCapabilities.TryGet(
                    StableId.Parse("capability.combat"),
                    out ignored),
                Is.False);
        }

        [Test]
        public void InheritedOverriddenAndClearedValuesResolveDeterministically()
        {
            CapabilityDefinition inherited = Capability(
                "capability.health",
                Field("field.maximum", CapabilityFieldValue.FromDecimal(12d)));
            ObjectFamilyDefinition family = new ObjectFamilyDefinition(
                StableId.Parse("family.crate"),
                "Crate",
                StableId.Parse("variant.standard"),
                new[] { inherited },
                new[]
                {
                    new ObjectVariantDefinition(
                        StableId.Parse("variant.standard"),
                        null,
                        new[]
                        {
                            CapabilitySelection.Inherit(
                                StableId.Parse("capability.health"))
                        })
                });

            CapabilityDefinition instance = Capability(
                "capability.health",
                Field("field.maximum", CapabilityFieldValue.FromDecimal(42d)));
            ObjectDefinitionResolutionResult overridden =
                ObjectDefinitionResolver.Resolve(
                    family,
                    null,
                    new[] { CapabilityOverride.Override(instance) });
            ObjectDefinitionResolutionResult cleared =
                ObjectDefinitionResolver.Resolve(
                    family,
                    null,
                    new[]
                    {
                        CapabilityOverride.Inherit(
                            StableId.Parse("capability.health"))
                    });
            ObjectDefinitionResolutionResult implicitInheritance =
                ObjectDefinitionResolver.Resolve(family, null, null);

            Assert.That(overridden.IsResolved, Is.True, overridden.Message);
            Assert.That(cleared.IsResolved, Is.True, cleared.Message);
            Assert.That(
                cleared.ResolvedCapabilities,
                Is.EqualTo(implicitInheritance.ResolvedCapabilities));
            Assert.That(
                overridden.ResolvedCapabilities.Fingerprint,
                Is.Not.EqualTo(cleared.ResolvedCapabilities.Fingerprint));

            CapabilityDefinition resolved;
            CapabilityField field;
            Assert.That(
                overridden.ResolvedCapabilities.TryGet(
                    StableId.Parse("capability.health"),
                    out resolved),
                Is.True);
            Assert.That(
                resolved.TryGetField(StableId.Parse("field.maximum"), out field),
                Is.True);
            Assert.That(field.Value.DecimalValue, Is.EqualTo(42d));
        }

        [Test]
        public void CanonicalFingerprintIgnoresInputOrdering()
        {
            CapabilityDefinition left = Capability(
                "capability.presentation",
                Field("field.layer", CapabilityFieldValue.FromInteger(3)),
                Field("field.sprite", CapabilityFieldValue.FromText("crate")));
            CapabilityDefinition right = Capability(
                "capability.presentation",
                Field("field.sprite", CapabilityFieldValue.FromText("crate")),
                Field("field.layer", CapabilityFieldValue.FromInteger(3)));

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left.Fingerprint, Is.EqualTo(right.Fingerprint));
            Assert.That(
                new ResolvedCapabilitySet(new[] { left }).Fingerprint,
                Is.EqualTo(new ResolvedCapabilitySet(new[] { right }).Fingerprint));
        }

        [Test]
        public void OverrideCannotIntroduceUnselectedCapability()
        {
            CapabilityDefinition presentation = Capability(
                "capability.presentation",
                Field("field.sprite", CapabilityFieldValue.FromText("crate")));
            ObjectFamilyDefinition family = new ObjectFamilyDefinition(
                StableId.Parse("family.crate"),
                "Crate",
                StableId.Parse("variant.standard"),
                new[] { presentation },
                new[]
                {
                    new ObjectVariantDefinition(
                        StableId.Parse("variant.standard"),
                        null,
                        new[]
                        {
                            CapabilitySelection.Inherit(
                                StableId.Parse("capability.presentation"))
                        })
                });

            ObjectDefinitionResolutionResult result =
                ObjectDefinitionResolver.Resolve(
                    family,
                    null,
                    new[]
                    {
                        CapabilityOverride.Override(
                            Capability(
                                "capability.combat",
                                Field(
                                    "field.damage",
                                    CapabilityFieldValue.FromDecimal(5d))))
                    });

            Assert.That(
                result.Status,
                Is.EqualTo(
                    ObjectDefinitionResolutionStatus
                        .OverrideTargetsUnselectedCapability));
            Assert.That(result.ResolvedCapabilities, Is.Null);
        }

        [Test]
        public void SceneScopeRegistryRejectsDuplicateAndReportsBothLocations()
        {
            SceneScopeRegistrationRegistry registry =
                new SceneScopeRegistrationRegistry();
            object firstOwner = new object();
            object secondOwner = new object();
            SceneScopeRegistrationRequest first = Request(
                "placed.crate-a",
                firstOwner,
                "Room/CrateA",
                "resolved-a");
            SceneScopeRegistrationRequest second = Request(
                "placed.crate-a",
                secondOwner,
                "Room/CrateB",
                "resolved-a");

            SceneScopeRegistrationResult firstResult = registry.Register(first);
            SceneScopeRegistrationResult duplicateResult =
                registry.Register(second);

            Assert.That(
                firstResult.Status,
                Is.EqualTo(SceneScopeRegistrationStatus.Registered));
            Assert.That(
                duplicateResult.Status,
                Is.EqualTo(
                    SceneScopeRegistrationStatus.RejectedDuplicateIdentity));
            Assert.That(
                duplicateResult.ExistingLocation,
                Is.EqualTo("Room/CrateA"));
            Assert.That(
                duplicateResult.AttemptedLocation,
                Is.EqualTo("Room/CrateB"));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExactRegistrationRetryIsNoChangeButChangedPayloadConflicts()
        {
            SceneScopeRegistrationRegistry registry =
                new SceneScopeRegistrationRegistry();
            object owner = new object();
            SceneScopeRegistrationRequest original = Request(
                "placed.crate-a",
                owner,
                "Room/CrateA",
                "resolved-a");
            SceneScopeRegistrationRequest changed = Request(
                "placed.crate-a",
                owner,
                "Room/CrateA",
                "resolved-b");

            Assert.That(
                registry.Register(original).Status,
                Is.EqualTo(SceneScopeRegistrationStatus.Registered));
            Assert.That(
                registry.Register(original).Status,
                Is.EqualTo(SceneScopeRegistrationStatus.DuplicateNoChange));
            Assert.That(
                registry.Register(changed).Status,
                Is.EqualTo(
                    SceneScopeRegistrationStatus
                        .RejectedConflictingRegistration));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void DistinctIdsRegisterAndSeparateScopesDoNotCrossBind()
        {
            SceneScopeRegistrationRegistry firstScope =
                new SceneScopeRegistrationRegistry();
            SceneScopeRegistrationRegistry secondScope =
                new SceneScopeRegistrationRegistry();

            Assert.That(
                firstScope.Register(
                    Request(
                        "placed.crate-a",
                        new object(),
                        "First/A",
                        "resolved-a")).IsAccepted,
                Is.True);
            Assert.That(
                firstScope.Register(
                    Request(
                        "placed.crate-b",
                        new object(),
                        "First/B",
                        "resolved-a")).IsAccepted,
                Is.True);
            Assert.That(
                secondScope.Register(
                    Request(
                        "placed.crate-a",
                        new object(),
                        "Second/A",
                        "resolved-a")).IsAccepted,
                Is.True);

            Assert.That(firstScope.Count, Is.EqualTo(2));
            Assert.That(secondScope.Count, Is.EqualTo(1));
        }

        [Test]
        public void RestartRegistrationDoesNotDuplicateParticipants()
        {
            RestartParticipantRegistry registry =
                new RestartParticipantRegistry();
            object owner = new object();
            StubRestartParticipant participant =
                new StubRestartParticipant("restart.crate-a");
            RestartParticipantRegistrationRequest request =
                new RestartParticipantRegistrationRequest(
                    participant,
                    owner,
                    "Room/CrateA");

            Assert.That(
                registry.Register(request).Status,
                Is.EqualTo(
                    RestartParticipantRegistrationStatus.Registered));
            Assert.That(
                registry.Register(request).Status,
                Is.EqualTo(
                    RestartParticipantRegistrationStatus.DuplicateNoChange));
            Assert.That(registry.Count, Is.EqualTo(1));

            RestartParticipantRegistrationResult conflict =
                registry.Register(
                    new RestartParticipantRegistrationRequest(
                        new StubRestartParticipant("restart.crate-a"),
                        new object(),
                        "Room/CrateB"));
            Assert.That(
                conflict.Status,
                Is.EqualTo(
                    RestartParticipantRegistrationStatus
                        .RejectedDuplicateParticipantId));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeSpawnIdentityRequiresExplicitStableInputs()
        {
            Assert.Throws<ArgumentNullException>(
                () => new RuntimeSpawnIdentityInput(
                    StableId.Parse("placed.spawned-a"),
                    null));

            RuntimeSpawnIdentityInput input =
                new RuntimeSpawnIdentityInput(
                    StableId.Parse("placed.spawned-a"),
                    StableId.Parse("spawn.operation-a"));
            PlacedObjectIdentity identity = input.CreateIdentity();

            Assert.That(
                identity.Kind,
                Is.EqualTo(PlacedObjectIdentityKind.RuntimeSpawned));
            Assert.That(
                identity.SpawnOperationId,
                Is.EqualTo(StableId.Parse("spawn.operation-a")));
        }

        [Test]
        public void ProductionAuthoringPathsContainNoGlobalSearchApis()
        {
            string root = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "ShooterMover",
                "Runtime",
                "UnityAdapters",
                "Authoring");
            Assert.That(Directory.Exists(root), Is.True, root);

            string[] forbidden =
            {
                "FindFirstObjectByType",
                "FindAnyObjectByType",
                "FindObjectsByType",
                "GameObject.Find(",
                "FindGameObjectWithTag",
                "FindGameObjectsWithTag",
                "SceneManager.GetSceneByName"
            };

            foreach (string file in Directory.GetFiles(
                root,
                "*.cs",
                SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                for (int index = 0; index < forbidden.Length; index++)
                {
                    Assert.That(
                        source.Contains(forbidden[index]),
                        Is.False,
                        $"{file} contains forbidden global search API "
                            + $"'{forbidden[index]}'.");
                }
            }
        }

        [Test]
        public void DomainAndContractsAuthoringRemainEngineIndependent()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string[] roots =
            {
                Path.Combine(
                    projectRoot,
                    "Assets",
                    "ShooterMover",
                    "Runtime",
                    "Domain",
                    "Authoring"),
                Path.Combine(
                    projectRoot,
                    "Assets",
                    "ShooterMover",
                    "Runtime",
                    "Contracts",
                    "Authoring")
            };

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                foreach (string file in Directory.GetFiles(
                    roots[rootIndex],
                    "*.cs",
                    SearchOption.AllDirectories))
                {
                    Assert.That(
                        File.ReadAllText(file).Contains("UnityEngine"),
                        Is.False,
                        file + " must remain engine-independent.");
                }
            }
        }

        private static CapabilityDefinition Capability(
            string id,
            params CapabilityField[] fields)
        {
            return new CapabilityDefinition(StableId.Parse(id), fields);
        }

        private static CapabilityField Field(
            string id,
            CapabilityFieldValue value)
        {
            return new CapabilityField(StableId.Parse(id), value);
        }

        private static SceneScopeRegistrationRequest Request(
            string placedId,
            object owner,
            string location,
            string resolvedFingerprint)
        {
            PlacedObjectIdentity identity =
                PlacedObjectIdentity.CreateAuthored(
                    StableId.Parse(placedId));
            PlacedParticipantRegistration registration =
                new PlacedParticipantRegistration(
                    identity,
                    new ObjectDefinitionReference(
                        StableId.Parse("family.crate"),
                        StableId.Parse("variant.standard")),
                    StableId.Parse("projection.room-a"),
                    StableId.Parse("run.test"),
                    0,
                    new[]
                    {
                        new CapabilityReference(
                            StableId.Parse("capability.presentation"))
                    },
                    resolvedFingerprint);
            return new SceneScopeRegistrationRequest(
                registration,
                owner,
                location);
        }

        private sealed class StubRestartParticipant : IRestartParticipant
        {
            public StubRestartParticipant(string id)
            {
                RestartParticipantId = StableId.Parse(id);
            }

            public StableId RestartParticipantId { get; }

            public void OnRestartPhase(
                RestartContext context,
                RestartLifecyclePhase phase)
            {
            }
        }
    }
}

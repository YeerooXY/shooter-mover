using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class ContentReferenceTests
    {
        private static readonly StableId ProvenanceId =
            StableId.Parse("provenance.accepted-source");

        [Test]
        public void ContentReference_AllAcceptedKindsRoundTripCanonically()
        {
            ContentDefinitionKind[] acceptedKinds =
            {
                ContentDefinitionKind.Weapon,
                ContentDefinitionKind.Enemy,
                ContentDefinitionKind.Room,
                ContentDefinitionKind.Encounter,
                ContentDefinitionKind.Environment,
                ContentDefinitionKind.SharedModule
            };

            for (int index = 0; index < acceptedKinds.Length; index++)
            {
                ContentReference reference = ContentReference.Create(
                    StableId.Parse("content.accepted-" + (index + 1)),
                    acceptedKinds[index],
                    ContentReference.SupportedDefinitionVersion);

                ContentReference parsed = ContentReference.ParseCanonical(
                    reference.ToCanonicalString());

                Assert.That(parsed, Is.EqualTo(reference));
                Assert.That(parsed.GetHashCode(), Is.EqualTo(reference.GetHashCode()));
            }
        }

        [Test]
        public void ContentReference_RejectsUnknownKindsAndNonCanonicalVersions()
        {
            StableId definitionId = StableId.Parse("weapon.blaster-machine-gun");

            Assert.Throws<ArgumentOutOfRangeException>(
                () => ContentReference.Create(
                    definitionId,
                    ContentDefinitionKind.Unknown,
                    1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ContentReference.Create(
                    definitionId,
                    ContentDefinitionKind.Weapon,
                    0));
            Assert.Throws<FormatException>(
                () => ContentReference.ParseCanonical(
                    "definition_id=weapon.blaster-machine-gun\n"
                    + "expected_kind=weapon\n"
                    + "expected_version=01"));
            Assert.Throws<FormatException>(
                () => ContentReference.ParseCanonical(
                    "expected_kind=weapon\n"
                    + "definition_id=weapon.blaster-machine-gun\n"
                    + "expected_version=1"));
            Assert.Throws<FormatException>(
                () => ContentReference.ParseCanonical(
                    "definition_id=weapon.blaster-machine-gun\n"
                    + "expected_kind=projectile\n"
                    + "expected_version=1"));
        }

        [Test]
        public void Descriptor_CopiesAndCanonicallyOrdersReferences()
        {
            List<ContentReference> source = new List<ContentReference>
            {
                Ref("weapon.shotgun", ContentDefinitionKind.Weapon),
                Ref("enemy.pursuer-drone", ContentDefinitionKind.Enemy)
            };

            ContentDefinitionDescriptor descriptor = Descriptor(
                "encounter.test-fixture",
                ContentDefinitionKind.Encounter,
                source);
            source.Clear();

            Assert.That(descriptor.References, Has.Count.EqualTo(2));
            Assert.That(
                descriptor.References[0].DefinitionId,
                Is.EqualTo(StableId.Parse("enemy.pursuer-drone")));
            Assert.That(
                descriptor.References[1].DefinitionId,
                Is.EqualTo(StableId.Parse("weapon.shotgun")));
            Assert.That(descriptor.ToCanonicalString(), Does.Contain("reference_count=2"));
            Assert.That(
                descriptor.ToCanonicalString(),
                Does.Contain("reference_0000=enemy|enemy.pursuer-drone|1"));
        }

        [Test]
        public void Descriptor_EquivalentReferenceSetsHaveIdenticalIdentity()
        {
            ContentReference weapon = Ref(
                "weapon.blaster-machine-gun",
                ContentDefinitionKind.Weapon);
            ContentReference enemy = Ref(
                "enemy.pursuer-drone",
                ContentDefinitionKind.Enemy);

            ContentDefinitionDescriptor first = Descriptor(
                "encounter.baseline",
                ContentDefinitionKind.Encounter,
                new[] { weapon, enemy });
            ContentDefinitionDescriptor second = Descriptor(
                "encounter.baseline",
                ContentDefinitionKind.Encounter,
                new[] { enemy, weapon });

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ToCanonicalString(), Is.EqualTo(second.ToCanonicalString()));
        }

        [Test]
        public void Validation_ResolvesExactUniqueTypedReferences()
        {
            ContentDefinitionDescriptor module = Descriptor(
                "module.automatic-projectile",
                ContentDefinitionKind.SharedModule);
            ContentDefinitionDescriptor weapon = Descriptor(
                "weapon.blaster-machine-gun",
                ContentDefinitionKind.Weapon,
                Ref("module.automatic-projectile", ContentDefinitionKind.SharedModule));

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { weapon, module },
                ContentValidationMode.Release);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);

            ContentDefinitionDescriptor resolved;
            Assert.That(
                result.TryResolve(
                    Ref("weapon.blaster-machine-gun", ContentDefinitionKind.Weapon),
                    out resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(weapon));
            Assert.That(
                result.TryResolve(
                    Ref("weapon.blaster-machine-gun", ContentDefinitionKind.Enemy),
                    out resolved),
                Is.False);
            Assert.That(
                result.TryResolve(
                    ContentReference.Create(
                        StableId.Parse("weapon.blaster-machine-gun"),
                        ContentDefinitionKind.Weapon,
                        2),
                    out resolved),
                Is.False);
        }

        [Test]
        public void Validation_ReportsMissingWrongKindAndUnsupportedVersionSeparately()
        {
            ContentDefinitionDescriptor target = Descriptor(
                "weapon.target",
                ContentDefinitionKind.Weapon);
            ContentDefinitionDescriptor source = Descriptor(
                "encounter.references",
                ContentDefinitionKind.Encounter,
                Ref("enemy.missing", ContentDefinitionKind.Enemy),
                Ref("weapon.target", ContentDefinitionKind.Enemy),
                ContentReference.Create(
                    StableId.Parse("weapon.target"),
                    ContentDefinitionKind.Weapon,
                    2));

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { source, target },
                ContentValidationMode.Release);

            Assert.That(
                result.Errors.Select(error => error.Code),
                Does.Contain(ContentValidationErrorCode.MissingDefinition));
            Assert.That(
                result.Errors.Select(error => error.Code),
                Does.Contain(ContentValidationErrorCode.WrongDefinitionKind));
            Assert.That(
                result.Errors.Select(error => error.Code),
                Does.Contain(ContentValidationErrorCode.UnsupportedDefinitionVersion));
        }

        [Test]
        public void Validation_ReportsDuplicateDefinitionsAndRefusesAmbiguousResolution()
        {
            ContentDefinitionDescriptor first = Descriptor(
                "enemy.duplicate",
                ContentDefinitionKind.Enemy);
            ContentDefinitionDescriptor second = Descriptor(
                "enemy.duplicate",
                ContentDefinitionKind.Enemy);

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { second, first },
                ContentValidationMode.Release);

            Assert.That(
                result.Errors.Count(
                    error => error.Code == ContentValidationErrorCode.DuplicateDefinition),
                Is.EqualTo(1));

            ContentDefinitionDescriptor resolved;
            Assert.That(
                result.TryResolve(
                    Ref("enemy.duplicate", ContentDefinitionKind.Enemy),
                    out resolved),
                Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void Validation_ReportsAStableCycleComponent()
        {
            ContentDefinitionDescriptor alpha = Descriptor(
                "module.alpha",
                ContentDefinitionKind.SharedModule,
                Ref("module.beta", ContentDefinitionKind.SharedModule));
            ContentDefinitionDescriptor beta = Descriptor(
                "module.beta",
                ContentDefinitionKind.SharedModule,
                Ref("module.alpha", ContentDefinitionKind.SharedModule));

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { beta, alpha },
                ContentValidationMode.Release);

            ContentValidationError cycle = result.Errors.Single(
                error => error.Code == ContentValidationErrorCode.CyclicDependency);
            Assert.That(cycle.Cycle, Has.Count.EqualTo(2));
            Assert.That(cycle.Cycle[0], Is.EqualTo(StableId.Parse("module.alpha")));
            Assert.That(cycle.Cycle[1], Is.EqualTo(StableId.Parse("module.beta")));
        }

        [Test]
        public void Validation_SelfReferenceIsCyclic()
        {
            ContentDefinitionDescriptor descriptor = Descriptor(
                "module.self",
                ContentDefinitionKind.SharedModule,
                Ref("module.self", ContentDefinitionKind.SharedModule));

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { descriptor },
                ContentValidationMode.Release);

            Assert.That(
                result.Errors.Select(error => error.Code),
                Does.Contain(ContentValidationErrorCode.CyclicDependency));
        }

        [Test]
        public void Validation_ReportsProvenanceTombstoneAndPrototypeErrorsSeparately()
        {
            StableId definitionId = StableId.Parse("weapon.prototype-only");
            ContentDefinitionDescriptor descriptor = ContentDefinitionDescriptor.Create(
                definitionId,
                ContentDefinitionKind.Weapon,
                1,
                null,
                true,
                Array.Empty<ContentReference>());

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { descriptor },
                new[] { definitionId },
                ContentValidationMode.Release);

            ContentValidationErrorCode[] codes = result.Errors
                .Select(error => error.Code)
                .ToArray();
            Assert.That(codes, Does.Contain(ContentValidationErrorCode.MissingProvenance));
            Assert.That(codes, Does.Contain(ContentValidationErrorCode.TombstonedId));
            Assert.That(codes, Does.Contain(ContentValidationErrorCode.PrototypeOnlyDefinition));
        }

        [Test]
        public void PrototypeMode_AllowsExplicitPrototypeDefinitions()
        {
            ContentDefinitionDescriptor descriptor = ContentDefinitionDescriptor.Create(
                StableId.Parse("weapon.prototype-only"),
                ContentDefinitionKind.Weapon,
                1,
                ProvenanceId,
                true,
                Array.Empty<ContentReference>());

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { descriptor },
                ContentValidationMode.Prototype);

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Validation_UnsupportedDescriptorVersionIsStructured()
        {
            ContentDefinitionDescriptor descriptor = ContentDefinitionDescriptor.Create(
                StableId.Parse("enemy.future-version"),
                ContentDefinitionKind.Enemy,
                2,
                ProvenanceId,
                false,
                Array.Empty<ContentReference>());

            ContentValidationResult result = ContentValidationResult.Validate(
                new[] { descriptor },
                ContentValidationMode.Release);

            ContentValidationError error = result.Errors.Single(
                candidate => candidate.Code
                    == ContentValidationErrorCode.UnsupportedDefinitionVersion);
            Assert.That(error.ExpectedVersion, Is.EqualTo(1));
            Assert.That(error.ActualVersion, Is.EqualTo(2));
        }

        [Test]
        public void Validation_ErrorOrderIsIndependentOfInputOrder()
        {
            StableId tombstoned = StableId.Parse("enemy.tombstoned");
            ContentDefinitionDescriptor wrongTarget = Descriptor(
                "weapon.target",
                ContentDefinitionKind.Weapon);
            ContentDefinitionDescriptor source = ContentDefinitionDescriptor.Create(
                StableId.Parse("encounter.source"),
                ContentDefinitionKind.Encounter,
                1,
                null,
                true,
                new[]
                {
                    Ref("enemy.missing", ContentDefinitionKind.Enemy),
                    Ref("enemy.tombstoned", ContentDefinitionKind.Enemy),
                    Ref("weapon.target", ContentDefinitionKind.Enemy),
                    ContentReference.Create(
                        StableId.Parse("weapon.target"),
                        ContentDefinitionKind.Weapon,
                        2)
                });
            ContentDefinitionDescriptor duplicateOne = Descriptor(
                "room.duplicate",
                ContentDefinitionKind.Room);
            ContentDefinitionDescriptor duplicateTwo = Descriptor(
                "room.duplicate",
                ContentDefinitionKind.Room);

            ContentValidationResult forward = ContentValidationResult.Validate(
                new[]
                {
                    source,
                    duplicateOne,
                    wrongTarget,
                    duplicateTwo
                },
                new[] { tombstoned },
                ContentValidationMode.Release);
            ContentValidationResult reverse = ContentValidationResult.Validate(
                new[]
                {
                    duplicateTwo,
                    wrongTarget,
                    duplicateOne,
                    source
                },
                new[] { tombstoned },
                ContentValidationMode.Release);

            Assert.That(
                reverse.Errors.Select(error => error.ToCanonicalString()),
                Is.EqualTo(forward.Errors.Select(error => error.ToCanonicalString())));
            Assert.That(
                forward.Errors.Select(error => error.Code).ToArray(),
                Is.Ordered);
        }

        [Test]
        public void ContractObjectsExposeNoPublicMutationSurface()
        {
            AssertImmutableClass(typeof(ContentReference));
            AssertImmutableClass(typeof(ContentDefinitionDescriptor));
            AssertImmutableClass(typeof(ContentValidationError));
            AssertImmutableClass(typeof(ContentValidationResult));

            ContentValidationResult result = ContentValidationResult.Validate(
                new[]
                {
                    Descriptor("weapon.immutable", ContentDefinitionKind.Weapon)
                },
                ContentValidationMode.Release);

            ICollection<ContentValidationError> collection =
                result.Errors as ICollection<ContentValidationError>;
            Assert.That(collection, Is.Not.Null);
            Assert.That(collection.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(
                () => collection.Add(result.Errors.FirstOrDefault()));
        }

        private static ContentReference Ref(
            string definitionId,
            ContentDefinitionKind expectedKind)
        {
            return ContentReference.Create(
                StableId.Parse(definitionId),
                expectedKind,
                ContentReference.SupportedDefinitionVersion);
        }

        private static ContentDefinitionDescriptor Descriptor(
            string definitionId,
            ContentDefinitionKind kind,
            params ContentReference[] references)
        {
            return ContentDefinitionDescriptor.Create(
                StableId.Parse(definitionId),
                kind,
                ContentReference.SupportedDefinitionVersion,
                ProvenanceId,
                false,
                references);
        }

        private static ContentDefinitionDescriptor Descriptor(
            string definitionId,
            ContentDefinitionKind kind,
            IEnumerable<ContentReference> references)
        {
            return ContentDefinitionDescriptor.Create(
                StableId.Parse(definitionId),
                kind,
                ContentReference.SupportedDefinitionVersion,
                ProvenanceId,
                false,
                references);
        }

        private static void AssertImmutableClass(Type type)
        {
            Assert.That(type.IsSealed, Is.True, type.FullName + " must be sealed.");
            Assert.That(
                type.GetFields(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty,
                type.FullName + " must expose no public instance fields.");

            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < properties.Length; index++)
            {
                Assert.That(
                    properties[index].CanWrite,
                    Is.False,
                    type.FullName + "." + properties[index].Name + " must be getter-only.");
            }
        }
    }
}

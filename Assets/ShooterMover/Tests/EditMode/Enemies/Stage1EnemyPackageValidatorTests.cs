using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.ContentPackages.Enemies.Stage1;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Contracts.Identity;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class Stage1EnemyPackageValidatorTests
    {
        [Test]
        public void AcceptedRoster_ValidatesExactlyFourOrdinaryAndOneElite()
        {
            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(AcceptedRoster());

            Assert.That(result.IsValid, Is.True, result.ToCanonicalString());
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Packages, Has.Count.EqualTo(5));
            Assert.That(
                result.Packages.Count(
                    package => package.Classification
                        == Stage1EnemyPackageClassification.Ordinary),
                Is.EqualTo(4));
            Assert.That(
                result.Packages.Count(
                    package => package.Classification
                        == Stage1EnemyPackageClassification.Elite),
                Is.EqualTo(1));

            for (int index = 0;
                 index < Stage1EnemyPackageDescriptor.AcceptedEnemyIds.Count;
                 index++)
            {
                Stage1EnemyPackageDescriptor package;
                Assert.That(
                    result.TryGetPackage(
                        Stage1EnemyPackageDescriptor.AcceptedEnemyIds[index],
                        out package),
                    Is.True);
                Assert.That(package, Is.Not.Null);
            }
        }

        [Test]
        public void Validation_ShuffledInputProducesIdenticalCanonicalResult()
        {
            Stage1EnemyPackageDescriptor[] ordered = AcceptedRoster();
            Stage1EnemyPackageDescriptor[] shuffled =
            {
                ordered[4],
                ordered[1],
                ordered[3],
                ordered[0],
                ordered[2],
            };

            Stage1EnemyPackageValidationResult first =
                Stage1EnemyPackageValidator.Validate(ordered);
            Stage1EnemyPackageValidationResult second =
                Stage1EnemyPackageValidator.Validate(shuffled);

            Assert.That(first.ToCanonicalString(), Is.EqualTo(second.ToCanonicalString()));
            Assert.That(
                first.GetRegistryInputs().Select(input => input.DefinitionId),
                Is.EqualTo(second.GetRegistryInputs().Select(input => input.DefinitionId)));
        }

        [Test]
        public void DuplicateIdAndMissingRole_AreReportedDeterministically()
        {
            Stage1EnemyPackageDescriptor[] accepted = AcceptedRoster();
            Stage1EnemyPackageDescriptor[] firstInput =
            {
                accepted[0],
                accepted[0],
                accepted[2],
                accepted[3],
                accepted[4],
            };
            Stage1EnemyPackageDescriptor[] secondInput =
            {
                accepted[4],
                accepted[3],
                accepted[0],
                accepted[2],
                accepted[0],
            };

            Stage1EnemyPackageValidationResult first =
                Stage1EnemyPackageValidator.Validate(firstInput);
            Stage1EnemyPackageValidationResult second =
                Stage1EnemyPackageValidator.Validate(secondInput);

            Assert.That(first.IsValid, Is.False);
            Assert.That(first.ToCanonicalString(), Is.EqualTo(second.ToCanonicalString()));
            Assert.That(
                HasError(
                    first,
                    Stage1EnemyPackageValidationErrorCode.DuplicatePackageId,
                    Stage1EnemyPackageDescriptor.PursuerDroneId),
                Is.True);
            Assert.That(
                HasError(
                    first,
                    Stage1EnemyPackageValidationErrorCode.MissingPackage,
                    Stage1EnemyPackageDescriptor.RamDroidId),
                Is.True);
            Assert.Throws<InvalidOperationException>(() => first.GetRegistryInputs());
        }

        [Test]
        public void UnknownId_DoesNotSatisfyTheAcceptedRoster()
        {
            Stage1EnemyPackageDescriptor[] roster = AcceptedRoster();
            Stage1EnemyPackageDescriptor template = roster[0];
            StableId unknownId = StableId.Parse("enemy.unapproved-sentinel");
            ContentDefinitionDescriptor unknownContent = ContentDefinitionDescriptor.Create(
                unknownId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.unapproved-sentinel"),
                false,
                template.MovementReference,
                template.AttackReference,
                template.TelegraphReference);
            roster[0] = Stage1EnemyPackageDescriptor.Create(
                Stage1EnemyPackageDescriptor.CurrentDescriptorVersion,
                unknownContent,
                Stage1EnemyPackageClassification.Ordinary,
                CombatChannel.Contact,
                CombatWeightClass.Standard,
                template.MovementReference,
                template.AttackReference,
                template.TelegraphReference,
                template.Capabilities);

            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(roster);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                HasError(
                    result,
                    Stage1EnemyPackageValidationErrorCode.UnknownPackageId,
                    unknownId),
                Is.True);
            Assert.That(
                HasError(
                    result,
                    Stage1EnemyPackageValidationErrorCode.MissingPackage,
                    Stage1EnemyPackageDescriptor.PursuerDroneId),
                Is.True);
        }

        [Test]
        public void OrdinaryAndEliteClassificationMismatches_AreRejected()
        {
            Stage1EnemyPackageDescriptor[] roster = AcceptedRoster();
            roster[0] = WithClassification(
                roster[0],
                Stage1EnemyPackageClassification.Elite);
            roster[4] = WithClassification(
                roster[4],
                Stage1EnemyPackageClassification.Ordinary);

            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(roster);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                HasError(
                    result,
                    Stage1EnemyPackageValidationErrorCode.ClassificationMismatch,
                    Stage1EnemyPackageDescriptor.PursuerDroneId),
                Is.True);
            Assert.That(
                HasError(
                    result,
                    Stage1EnemyPackageValidationErrorCode.ClassificationMismatch,
                    Stage1EnemyPackageDescriptor.FourBlasterEliteId),
                Is.True);
        }

        [Test]
        public void MalformedDescriptor_ReportsStableStructuredFailures()
        {
            Stage1EnemyPackageDescriptor[] roster = AcceptedRoster();
            StableId packageId = Stage1EnemyPackageDescriptor.PursuerDroneId;
            ContentReference wrongAttack = ContentReference.Create(
                StableId.Parse("module.enemy-ordinary-contact"),
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion);
            ContentReference unsupportedTelegraph = ContentReference.Create(
                StableId.Parse("module.enemy-contact-telegraph"),
                ContentDefinitionKind.SharedModule,
                2);
            ContentDefinitionDescriptor malformedContent =
                ContentDefinitionDescriptor.Create(
                    packageId,
                    ContentDefinitionKind.Encounter,
                    2,
                    null,
                    true,
                    wrongAttack,
                    unsupportedTelegraph);
            roster[0] = Stage1EnemyPackageDescriptor.Create(
                2,
                malformedContent,
                (Stage1EnemyPackageClassification)99,
                CombatChannel.System,
                (CombatWeightClass)99,
                null,
                wrongAttack,
                unsupportedTelegraph,
                (Stage1EnemyCapability)(1UL << 63));

            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(roster);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.UnsupportedDescriptorVersion),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.WrongDefinitionKind),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.UnsupportedDefinitionVersion),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.MissingProvenance),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.PrototypeOnlyDefinition),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.InvalidClassification),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.InvalidCombatChannel),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.InvalidWeightClass),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.MissingMovementReference),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.WrongReferenceKind),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.UnsupportedReferenceVersion),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.OutOfBoundRegistryReference),
                Is.True);
            Assert.That(
                HasError(result, Stage1EnemyPackageValidationErrorCode.UnknownCapability),
                Is.True);

            Stage1EnemyPackageValidationErrorCode[] orderedCodes =
                result.Errors.Select(error => error.Code).ToArray();
            Stage1EnemyPackageValidationErrorCode[] sortedCodes =
                orderedCodes.OrderBy(code => code).ToArray();
            Assert.That(orderedCodes, Is.EqualTo(sortedCodes));
        }

        [Test]
        public void MissingTelegraphReference_IsRejected()
        {
            Stage1EnemyPackageDescriptor[] roster = AcceptedRoster();
            Stage1EnemyPackageDescriptor template = roster[0];
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                template.DefinitionId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                template.ContentDefinition.ProvenanceId,
                false,
                template.MovementReference,
                template.AttackReference);
            roster[0] = Stage1EnemyPackageDescriptor.Create(
                template.DescriptorVersion,
                content,
                template.Classification,
                template.DamageChannel,
                template.WeightClass,
                template.MovementReference,
                template.AttackReference,
                null,
                template.Capabilities);

            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(roster);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                HasError(
                    result,
                    Stage1EnemyPackageValidationErrorCode.MissingTelegraphReference,
                    template.DefinitionId),
                Is.True);
        }

        [TestCase(Stage1EnemyCapability.PhaseTransition)]
        [TestCase(Stage1EnemyCapability.DenialPulse)]
        [TestCase(Stage1EnemyCapability.MortarAttack)]
        [TestCase(Stage1EnemyCapability.ReinforcementCall)]
        [TestCase(Stage1EnemyCapability.Teleport)]
        [TestCase(Stage1EnemyCapability.ComplexRepositioning)]
        [TestCase(Stage1EnemyCapability.BulletHell)]
        public void FourBlasterElite_RejectsEveryForbiddenCapability(
            Stage1EnemyCapability forbiddenCapability)
        {
            Stage1EnemyPackageDescriptor[] roster = AcceptedRoster();
            Stage1EnemyPackageDescriptor elite = roster[4];
            roster[4] = WithCapabilities(
                elite,
                elite.Capabilities | forbiddenCapability);

            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(roster);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Errors.Any(
                    error => error.Code
                            == Stage1EnemyPackageValidationErrorCode.ForbiddenEliteCapability
                        && error.PackageId.Equals(
                            Stage1EnemyPackageDescriptor.FourBlasterEliteId)
                        && error.Detail != null),
                Is.True,
                result.ToCanonicalString());
        }

        [Test]
        public void OrdinaryRole_RejectsBehaviorOutsideItsAcceptedBoundary()
        {
            Stage1EnemyPackageDescriptor[] roster = AcceptedRoster();
            Stage1EnemyPackageDescriptor pursuer = roster[0];
            roster[0] = WithCapabilities(
                pursuer,
                pursuer.Capabilities | Stage1EnemyCapability.BlasterProjectile);

            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(roster);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                HasError(
                    result,
                    Stage1EnemyPackageValidationErrorCode.OutOfBoundCapability,
                    Stage1EnemyPackageDescriptor.PursuerDroneId),
                Is.True);
        }

        [Test]
        public void AcceptedPackages_ProjectIntoEncounterAndGeneratedRegistryContracts()
        {
            Stage1EnemyPackageValidationResult result =
                Stage1EnemyPackageValidator.Validate(AcceptedRoster());
            Assert.That(result.IsValid, Is.True, result.ToCanonicalString());

            for (int index = 0; index < result.Packages.Count; index++)
            {
                Stage1EnemyPackageDescriptor package = result.Packages[index];
                ContentReference enemyReference = package.CreateEnemyReference();
                EncounterParticipantEntry entry = package.CreateEncounterParticipantEntry(
                    StableId.Parse("entry.fixture-" + index),
                    StableId.Parse("actor.fixture-" + index),
                    index);

                Assert.That(enemyReference.DefinitionId, Is.EqualTo(package.DefinitionId));
                Assert.That(enemyReference.ExpectedKind, Is.EqualTo(ContentDefinitionKind.Enemy));
                Assert.That(entry.RoleId, Is.EqualTo(package.DefinitionId));
                Assert.That(entry.Order, Is.EqualTo(index));
            }

            List<ContentDefinitionDescriptor> catalog =
                new List<ContentDefinitionDescriptor>(result.GetRegistryInputs());
            catalog.AddRange(CreateSupportingDefinitions(result.GetRegistryInputs()));

            GeneratedMachineRegistry registry = GeneratedMachineRegistry.Create(
                1,
                catalog,
                ContentValidationMode.Release);
            GeneratedRegistryReviewSnapshot review =
                GeneratedRegistryReviewSnapshot.Create(registry);
            ContentVersion contentVersion = registry.ContentVersion;

            Assert.That(contentVersion.CatalogVersion, Is.EqualTo(1));
            Assert.That(contentVersion.DefinitionFingerprint, Does.StartWith("sha256:"));
            Assert.That(registry.RegistryFingerprint, Does.StartWith("sha256:"));
            Assert.That(review.SnapshotFingerprint, Does.StartWith("sha256:"));
            Assert.That(
                registry.Entries.Count(entry => entry.Kind == ContentDefinitionKind.Enemy),
                Is.EqualTo(5));
        }

        [Test]
        public void Descriptor_PublicStateIsImmutable()
        {
            PropertyInfo[] properties = typeof(Stage1EnemyPackageDescriptor)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public);
            Assert.That(properties, Is.Not.Empty);
            Assert.That(properties.All(property => property.SetMethod == null), Is.True);

            FieldInfo[] fields = typeof(Stage1EnemyPackageDescriptor)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fields, Is.Not.Empty);
            Assert.That(fields.All(field => field.IsInitOnly), Is.True);
        }

        private static Stage1EnemyPackageDescriptor[] AcceptedRoster()
        {
            return new[]
            {
                CreateAcceptedPackage(Stage1EnemyPackageDescriptor.PursuerDroneId),
                CreateAcceptedPackage(Stage1EnemyPackageDescriptor.RamDroidId),
                CreateAcceptedPackage(Stage1EnemyPackageDescriptor.MobileBlasterDroidId),
                CreateAcceptedPackage(Stage1EnemyPackageDescriptor.BlasterTurretId),
                CreateAcceptedPackage(Stage1EnemyPackageDescriptor.FourBlasterEliteId),
            };
        }

        private static Stage1EnemyPackageDescriptor CreateAcceptedPackage(StableId packageId)
        {
            ContentReference movement;
            ContentReference attack;
            ContentReference telegraph;
            Stage1EnemyPackageClassification classification;
            CombatChannel channel;
            CombatWeightClass weight;
            Stage1EnemyCapability capabilities;

            if (packageId.Equals(Stage1EnemyPackageDescriptor.PursuerDroneId))
            {
                movement = SharedModule("module.enemy-direct-pursuit");
                attack = SharedModule("module.enemy-ordinary-contact");
                telegraph = SharedModule("module.enemy-contact-telegraph");
                classification = Stage1EnemyPackageClassification.Ordinary;
                channel = CombatChannel.Contact;
                weight = CombatWeightClass.Standard;
                capabilities = Stage1EnemyCapability.DirectPursuit
                    | Stage1EnemyCapability.OrdinaryContactDamage;
            }
            else if (packageId.Equals(Stage1EnemyPackageDescriptor.RamDroidId))
            {
                movement = SharedModule("module.enemy-direct-pursuit");
                attack = SharedModule("module.enemy-disposable-impact");
                telegraph = SharedModule("module.enemy-impact-telegraph");
                classification = Stage1EnemyPackageClassification.Ordinary;
                channel = CombatChannel.Contact;
                weight = CombatWeightClass.Light;
                capabilities = Stage1EnemyCapability.DirectPursuit
                    | Stage1EnemyCapability.DisposableImpactAttack;
            }
            else if (packageId.Equals(Stage1EnemyPackageDescriptor.MobileBlasterDroidId))
            {
                movement = SharedModule("module.enemy-mobile-positioning");
                attack = BlasterReference();
                telegraph = SharedModule("module.enemy-blaster-telegraph");
                classification = Stage1EnemyPackageClassification.Ordinary;
                channel = CombatChannel.Kinetic;
                weight = CombatWeightClass.Standard;
                capabilities = Stage1EnemyCapability.MobilePositioning
                    | Stage1EnemyCapability.BlasterProjectile
                    | Stage1EnemyCapability.SafeRecoveryWindow;
            }
            else if (packageId.Equals(Stage1EnemyPackageDescriptor.BlasterTurretId))
            {
                movement = SharedModule("module.enemy-stationary-positioning");
                attack = BlasterReference();
                telegraph = SharedModule("module.enemy-line-of-fire-telegraph");
                classification = Stage1EnemyPackageClassification.Ordinary;
                channel = CombatChannel.Kinetic;
                weight = CombatWeightClass.Immovable;
                capabilities = Stage1EnemyCapability.StationaryPositioning
                    | Stage1EnemyCapability.BlasterProjectile
                    | Stage1EnemyCapability.SafeRecoveryWindow
                    | Stage1EnemyCapability.LineOfFireTelegraph;
            }
            else if (packageId.Equals(Stage1EnemyPackageDescriptor.FourBlasterEliteId))
            {
                movement = SharedModule("module.enemy-simple-elite-positioning");
                attack = BlasterReference();
                telegraph = SharedModule("module.enemy-four-blaster-telegraph");
                classification = Stage1EnemyPackageClassification.Elite;
                channel = CombatChannel.Kinetic;
                weight = CombatWeightClass.Heavy;
                capabilities = Stage1EnemyCapability.BlasterProjectile
                    | Stage1EnemyCapability.FourBlasterOrigins
                    | Stage1EnemyCapability.MildBoundedSpread
                    | Stage1EnemyCapability.SafeRecoveryWindow;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(packageId), packageId, "Unknown fixture ID.");
            }

            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                packageId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Create("provenance", packageId.Value + "-approved"),
                false,
                movement,
                attack,
                telegraph);
            return Stage1EnemyPackageDescriptor.Create(
                Stage1EnemyPackageDescriptor.CurrentDescriptorVersion,
                content,
                classification,
                channel,
                weight,
                movement,
                attack,
                telegraph,
                capabilities);
        }

        private static Stage1EnemyPackageDescriptor WithClassification(
            Stage1EnemyPackageDescriptor source,
            Stage1EnemyPackageClassification classification)
        {
            return Stage1EnemyPackageDescriptor.Create(
                source.DescriptorVersion,
                source.ContentDefinition,
                classification,
                source.DamageChannel,
                source.WeightClass,
                source.MovementReference,
                source.AttackReference,
                source.TelegraphReference,
                source.Capabilities);
        }

        private static Stage1EnemyPackageDescriptor WithCapabilities(
            Stage1EnemyPackageDescriptor source,
            Stage1EnemyCapability capabilities)
        {
            return Stage1EnemyPackageDescriptor.Create(
                source.DescriptorVersion,
                source.ContentDefinition,
                source.Classification,
                source.DamageChannel,
                source.WeightClass,
                source.MovementReference,
                source.AttackReference,
                source.TelegraphReference,
                capabilities);
        }

        private static ContentReference SharedModule(string id)
        {
            return ContentReference.Create(
                StableId.Parse(id),
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
        }

        private static ContentReference BlasterReference()
        {
            return ContentReference.Create(
                Stage1EnemyPackageDescriptor.BlasterMachineGunId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion);
        }

        private static IEnumerable<ContentDefinitionDescriptor> CreateSupportingDefinitions(
            IEnumerable<ContentDefinitionDescriptor> enemyDefinitions)
        {
            Dictionary<StableId, ContentReference> uniqueReferences =
                new Dictionary<StableId, ContentReference>();
            foreach (ContentDefinitionDescriptor enemy in enemyDefinitions)
            {
                for (int index = 0; index < enemy.References.Count; index++)
                {
                    ContentReference reference = enemy.References[index];
                    ContentReference existing;
                    if (uniqueReferences.TryGetValue(reference.DefinitionId, out existing))
                    {
                        Assert.That(existing.ExpectedKind, Is.EqualTo(reference.ExpectedKind));
                        Assert.That(existing.ExpectedVersion, Is.EqualTo(reference.ExpectedVersion));
                    }
                    else
                    {
                        uniqueReferences.Add(reference.DefinitionId, reference);
                    }
                }
            }

            List<ContentDefinitionDescriptor> definitions =
                new List<ContentDefinitionDescriptor>();
            foreach (ContentReference reference in uniqueReferences.Values)
            {
                definitions.Add(
                    ContentDefinitionDescriptor.Create(
                        reference.DefinitionId,
                        reference.ExpectedKind,
                        reference.ExpectedVersion,
                        StableId.Create(
                            "provenance",
                            reference.DefinitionId.Value + "-fixture"),
                        false,
                        Array.Empty<ContentReference>()));
            }

            return definitions;
        }

        private static bool HasError(
            Stage1EnemyPackageValidationResult result,
            Stage1EnemyPackageValidationErrorCode code,
            StableId packageId = null)
        {
            return result.Errors.Any(
                error => error.Code == code
                    && (packageId == null || Equals(error.PackageId, packageId)));
        }
    }
}

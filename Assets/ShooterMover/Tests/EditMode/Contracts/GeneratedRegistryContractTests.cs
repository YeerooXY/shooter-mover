using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class GeneratedRegistryContractTests
    {
        private static readonly StableId ProvenanceId =
            StableId.Parse("provenance.accepted-source");

        [Test]
        public void MachineRegistry_ShuffledInputIsByteStableAndCanonicallyOrdered()
        {
            ContentDefinitionDescriptor module = Descriptor(
                "module.automatic-projectile",
                ContentDefinitionKind.SharedModule);
            ContentDefinitionDescriptor weapon = Descriptor(
                "weapon.blaster-machine-gun",
                ContentDefinitionKind.Weapon,
                Ref("module.automatic-projectile", ContentDefinitionKind.SharedModule));
            ContentDefinitionDescriptor enemyZeta = Descriptor(
                "enemy.zeta",
                ContentDefinitionKind.Enemy);
            ContentDefinitionDescriptor enemyAlpha = Descriptor(
                "enemy.alpha",
                ContentDefinitionKind.Enemy);
            ContentDefinitionDescriptor room = Descriptor(
                "room.factory-alpha",
                ContentDefinitionKind.Room,
                Ref("enemy.alpha", ContentDefinitionKind.Enemy));

            GeneratedMachineRegistry first = GeneratedMachineRegistry.Create(
                7,
                new[] { weapon, enemyZeta, room, module, enemyAlpha },
                ContentValidationMode.Release);
            GeneratedMachineRegistry second = GeneratedMachineRegistry.Create(
                7,
                new[] { enemyAlpha, module, room, enemyZeta, weapon },
                ContentValidationMode.Release);

            CollectionAssert.AreEqual(
                first.GetCanonicalUtf8Bytes(),
                second.GetCanonicalUtf8Bytes());
            Assert.That(first.RegistryFingerprint, Is.EqualTo(second.RegistryFingerprint));
            Assert.That(
                first.ContentVersion.DefinitionFingerprint,
                Is.EqualTo(second.ContentVersion.DefinitionFingerprint));

            Assert.That(
                first.Entries.Select(entry => entry.DefinitionId.ToString()),
                Is.EqualTo(new[]
                {
                    "enemy.alpha",
                    "enemy.zeta",
                    "room.factory-alpha",
                    "module.automatic-projectile",
                    "weapon.blaster-machine-gun"
                }));
        }

        [Test]
        public void CanonicalDocuments_UseUtf8WithoutBomAndLfWithOneTerminalNewline()
        {
            GeneratedMachineRegistry registry = GeneratedMachineRegistry.Create(
                1,
                new[]
                {
                    Descriptor("enemy.pursuer-drone", ContentDefinitionKind.Enemy)
                },
                ContentValidationMode.Release);
            GeneratedRegistryReviewSnapshot review =
                GeneratedRegistryReviewSnapshot.Create(registry);

            AssertCanonicalBytes(registry.ToCanonicalJson(), registry.GetCanonicalUtf8Bytes());
            AssertCanonicalBytes(review.ToCanonicalJson(), review.GetCanonicalUtf8Bytes());
        }

        [Test]
        public void EmptyCatalog_IsDeterministicAndReviewable()
        {
            GeneratedMachineRegistry first = GeneratedMachineRegistry.Create(
                1,
                Array.Empty<ContentDefinitionDescriptor>(),
                ContentValidationMode.Release);
            GeneratedMachineRegistry second = GeneratedMachineRegistry.Create(
                1,
                new List<ContentDefinitionDescriptor>(),
                ContentValidationMode.Release);
            GeneratedRegistryReviewSnapshot review =
                GeneratedRegistryReviewSnapshot.Create(first);

            CollectionAssert.AreEqual(
                first.GetCanonicalUtf8Bytes(),
                second.GetCanonicalUtf8Bytes());
            Assert.That(first.Entries, Is.Empty);
            Assert.That(first.ToCanonicalJson(), Does.Contain("\"entry_count\": 0"));
            Assert.That(first.ToCanonicalJson(), Does.Contain("\"entries\": [  ]").Not);
            Assert.That(review.PrototypeOnlyCount, Is.Zero);
            Assert.That(review.ReferenceCount, Is.Zero);
            Assert.That(review.KindCounts.Select(count => count.Count), Is.All.Zero);
            Assert.That(review.ToCanonicalJson(), Does.Contain("\"is_valid\": true"));
            Assert.That(review.ToCanonicalJson(), Does.Contain("\"error_count\": 0"));
        }

        [Test]
        public void Fingerprints_AreExplicitStableAndSensitiveToDescriptorChanges()
        {
            ContentDefinitionDescriptor firstDescriptor = ContentDefinitionDescriptor.Create(
                StableId.Parse("enemy.pursuer-drone"),
                ContentDefinitionKind.Enemy,
                1,
                StableId.Parse("provenance.source-a"),
                false,
                Array.Empty<ContentReference>());
            ContentDefinitionDescriptor changedDescriptor = ContentDefinitionDescriptor.Create(
                StableId.Parse("enemy.pursuer-drone"),
                ContentDefinitionKind.Enemy,
                1,
                StableId.Parse("provenance.source-b"),
                false,
                Array.Empty<ContentReference>());

            GeneratedMachineRegistry first = GeneratedMachineRegistry.Create(
                3,
                new[] { firstDescriptor },
                ContentValidationMode.Release);
            GeneratedMachineRegistry repeated = GeneratedMachineRegistry.Create(
                3,
                new[] { firstDescriptor },
                ContentValidationMode.Release);
            GeneratedMachineRegistry changed = GeneratedMachineRegistry.Create(
                3,
                new[] { changedDescriptor },
                ContentValidationMode.Release);
            GeneratedRegistryReviewSnapshot review =
                GeneratedRegistryReviewSnapshot.Create(first);

            AssertCanonicalFingerprint(first.ContentVersion.DefinitionFingerprint);
            AssertCanonicalFingerprint(first.RegistryFingerprint);
            AssertCanonicalFingerprint(review.SnapshotFingerprint);
            Assert.That(
                first.ContentVersion.DefinitionFingerprint,
                Is.EqualTo(repeated.ContentVersion.DefinitionFingerprint));
            Assert.That(first.RegistryFingerprint, Is.EqualTo(repeated.RegistryFingerprint));
            Assert.That(
                changed.ContentVersion.DefinitionFingerprint,
                Is.Not.EqualTo(first.ContentVersion.DefinitionFingerprint));
            Assert.That(changed.RegistryFingerprint, Is.Not.EqualTo(first.RegistryFingerprint));
        }

        [Test]
        public void ReviewSnapshot_IsByteStableAndSummarizesValidatedEntries()
        {
            ContentDefinitionDescriptor module = Descriptor(
                "module.arc-conduction",
                ContentDefinitionKind.SharedModule,
                true);
            ContentDefinitionDescriptor weapon = Descriptor(
                "weapon.arc-gun",
                ContentDefinitionKind.Weapon,
                true,
                Ref("module.arc-conduction", ContentDefinitionKind.SharedModule));
            ContentDefinitionDescriptor enemy = Descriptor(
                "enemy.pursuer-drone",
                ContentDefinitionKind.Enemy);

            GeneratedMachineRegistry firstRegistry = GeneratedMachineRegistry.Create(
                2,
                new[] { weapon, enemy, module },
                ContentValidationMode.Prototype);
            GeneratedMachineRegistry secondRegistry = GeneratedMachineRegistry.Create(
                2,
                new[] { module, weapon, enemy },
                ContentValidationMode.Prototype);
            GeneratedRegistryReviewSnapshot first =
                GeneratedRegistryReviewSnapshot.Create(firstRegistry);
            GeneratedRegistryReviewSnapshot second =
                GeneratedRegistryReviewSnapshot.Create(secondRegistry);

            CollectionAssert.AreEqual(
                first.GetCanonicalUtf8Bytes(),
                second.GetCanonicalUtf8Bytes());
            Assert.That(first.PrototypeOnlyCount, Is.EqualTo(2));
            Assert.That(first.ReferenceCount, Is.EqualTo(1));
            Assert.That(first.KindCounts, Has.Count.EqualTo(6));
            Assert.That(
                first.KindCounts.Select(count => count.Kind),
                Is.EqualTo(new[]
                {
                    ContentDefinitionKind.Enemy,
                    ContentDefinitionKind.Encounter,
                    ContentDefinitionKind.Environment,
                    ContentDefinitionKind.Room,
                    ContentDefinitionKind.SharedModule,
                    ContentDefinitionKind.Weapon
                }));
            Assert.That(first.ToCanonicalJson(), Does.Contain("\"mode\": \"prototype\""));
            Assert.That(first.ToCanonicalJson(), Does.Contain("\"prototype_only_count\": 2"));
        }

        [Test]
        public void MachineRegistry_RejectsInvalidContentCatalog()
        {
            ContentDefinitionDescriptor missingProvenance = ContentDefinitionDescriptor.Create(
                StableId.Parse("enemy.invalid"),
                ContentDefinitionKind.Enemy,
                1,
                null,
                false,
                Array.Empty<ContentReference>());

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => GeneratedMachineRegistry.Create(
                    1,
                    new[] { missingProvenance },
                    ContentValidationMode.Release));

            Assert.That(exception.Message, Does.Contain("missing-provenance"));
        }

        [Test]
        public void MachineRegistry_DefensivelyCopiesDescriptorInput()
        {
            List<ContentDefinitionDescriptor> source = new List<ContentDefinitionDescriptor>
            {
                Descriptor("enemy.pursuer-drone", ContentDefinitionKind.Enemy)
            };

            GeneratedMachineRegistry registry = GeneratedMachineRegistry.Create(
                1,
                source,
                ContentValidationMode.Release);
            byte[] before = registry.GetCanonicalUtf8Bytes();
            source.Clear();

            Assert.That(registry.Entries, Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(before, registry.GetCanonicalUtf8Bytes());
        }

        [Test]
        public void Documents_ContainNoMachineLocalPathsOrRuntimeAuthorityFields()
        {
            GeneratedMachineRegistry registry = GeneratedMachineRegistry.Create(
                1,
                new[]
                {
                    Descriptor("enemy.pursuer-drone", ContentDefinitionKind.Enemy)
                },
                ContentValidationMode.Release);
            string combined = registry.ToCanonicalJson()
                + GeneratedRegistryReviewSnapshot.Create(registry).ToCanonicalJson();

            Assert.That(combined, Does.Not.Contain("C:\\"));
            Assert.That(combined, Does.Not.Contain("/Users/"));
            Assert.That(combined, Does.Not.Contain("/home/"));
            Assert.That(combined, Does.Not.Contain("asset_path"));
            Assert.That(combined, Does.Not.Contain("scene_path"));
            Assert.That(combined, Does.Not.Contain("runtime_state"));
            Assert.That(combined, Does.Not.Contain("current_health"));
            Assert.That(combined, Does.Not.Contain("checkpoint_state"));
            Assert.That(combined, Does.Not.Contain("reward_state"));
        }

        [Test]
        public void Schemas_AreLfOnlyStructurallyBalancedAndMatchContractIds()
        {
            string machinePath = RepositoryPath(
                "Assets",
                "ShooterMover",
                "Generated",
                "schemas",
                "generated-registry-v1.schema.json");
            string reviewPath = RepositoryPath(
                "Assets",
                "ShooterMover",
                "Generated",
                "schemas",
                "generated-registry-review-v1.schema.json");

            string machine = File.ReadAllText(machinePath);
            string review = File.ReadAllText(reviewPath);

            AssertSchemaText(
                machine,
                GeneratedMachineRegistry.SchemaId,
                "definition_fingerprint",
                "registry_fingerprint");
            AssertSchemaText(
                review,
                GeneratedRegistryReviewSnapshot.SchemaId,
                "snapshot_fingerprint",
                "kind_counts");
            Assert.That(machine, Does.Not.Contain("weapon.blaster-machine-gun"));
            Assert.That(review, Does.Not.Contain("enemy.pursuer-drone"));
        }

        [Test]
        public void Contracts_AreGetterOnlyAndUnityFree()
        {
            Type[] contractTypes =
            {
                typeof(GeneratedMachineRegistry),
                typeof(GeneratedRegistryReviewSnapshot),
                typeof(GeneratedRegistryKindCount)
            };

            foreach (Type type in contractTypes)
            {
                Assert.That(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .All(property => property.SetMethod == null),
                    Is.True,
                    type.FullName + " must expose getter-only public properties.");
            }

            Assert.That(
                typeof(GeneratedMachineRegistry).Assembly.GetReferencedAssemblies()
                    .Select(reference => reference.Name),
                Does.Not.Contain("UnityEngine"));
            Assert.That(
                typeof(GeneratedMachineRegistry).Assembly.GetReferencedAssemblies()
                    .Select(reference => reference.Name),
                Does.Not.Contain("UnityEngine.CoreModule"));
        }

        private static ContentDefinitionDescriptor Descriptor(
            string id,
            ContentDefinitionKind kind,
            params ContentReference[] references)
        {
            return Descriptor(id, kind, false, references);
        }

        private static ContentDefinitionDescriptor Descriptor(
            string id,
            ContentDefinitionKind kind,
            bool prototypeOnly,
            params ContentReference[] references)
        {
            return ContentDefinitionDescriptor.Create(
                StableId.Parse(id),
                kind,
                ContentReference.SupportedDefinitionVersion,
                ProvenanceId,
                prototypeOnly,
                references);
        }

        private static ContentReference Ref(string id, ContentDefinitionKind kind)
        {
            return ContentReference.Create(
                StableId.Parse(id),
                kind,
                ContentReference.SupportedDefinitionVersion);
        }

        private static void AssertCanonicalBytes(string text, byte[] bytes)
        {
            Assert.That(text, Does.Not.Contain("\r"));
            Assert.That(text, Does.EndWith("\n"));
            Assert.That(text, Does.Not.EndWith("\n\n"));
            CollectionAssert.AreEqual(
                new UTF8Encoding(false, true).GetBytes(text),
                bytes);
            Assert.That(bytes.Take(3).ToArray(), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
        }

        private static void AssertCanonicalFingerprint(string value)
        {
            Assert.That(value, Does.Match("^sha256:[0-9a-f]{64}$"));
        }

        private static string RepositoryPath(params string[] parts)
        {
            string path = Directory.GetCurrentDirectory();
            for (int index = 0; index < parts.Length; index++)
            {
                path = Path.Combine(path, parts[index]);
            }

            Assert.That(File.Exists(path), Is.True, "Expected repository file: " + path);
            return path;
        }

        private static void AssertSchemaText(
            string text,
            string contractId,
            params string[] requiredMarkers)
        {
            Assert.That(text, Does.Not.Contain("\r"));
            Assert.That(text, Does.EndWith("\n"));
            Assert.That(text, Does.Not.EndWith("\n\n"));
            Assert.That(text, Does.Contain("\"$id\": \"" + contractId + "\""));
            Assert.That(text, Does.Contain("\"additionalProperties\": false"));
            Assert.That(text, Does.Contain("^sha256:[0-9a-f]{64}$"));
            for (int index = 0; index < requiredMarkers.Length; index++)
            {
                Assert.That(text, Does.Contain("\"" + requiredMarkers[index] + "\""));
            }

            AssertJsonContainersBalanced(text);
        }

        private static void AssertJsonContainersBalanced(string text)
        {
            Stack<char> containers = new Stack<char>();
            bool inString = false;
            bool escaped = false;
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                }
                else if (current == '{' || current == '[')
                {
                    containers.Push(current);
                }
                else if (current == '}' || current == ']')
                {
                    Assert.That(containers, Is.Not.Empty, "Unexpected closing JSON container.");
                    char opening = containers.Pop();
                    Assert.That(
                        (opening == '{' && current == '}')
                        || (opening == '[' && current == ']'),
                        Is.True,
                        "Mismatched JSON containers.");
                }
            }

            Assert.That(inString, Is.False, "Schema contains an unterminated JSON string.");
            Assert.That(escaped, Is.False, "Schema ends with an incomplete escape.");
            Assert.That(containers, Is.Empty, "Schema contains an unclosed JSON container.");
        }
    }
}

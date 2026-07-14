using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Foundation
{
    public sealed class AssemblyDependencyTests
    {
        private static readonly ExpectedAssembly[] ExpectedAssemblies =
        {
            new ExpectedAssembly(
                "Assets/ShooterMover/Runtime/Domain/ShooterMover.Domain.asmdef",
                "ShooterMover.Domain",
                true),
            new ExpectedAssembly(
                "Assets/ShooterMover/Runtime/Contracts/ShooterMover.Contracts.asmdef",
                "ShooterMover.Contracts",
                true,
                "ShooterMover.Domain"),
            new ExpectedAssembly(
                "Assets/ShooterMover/Runtime/Application/ShooterMover.Application.asmdef",
                "ShooterMover.Application",
                true,
                "ShooterMover.Domain",
                "ShooterMover.Contracts"),
            new ExpectedAssembly(
                "Assets/ShooterMover/Runtime/UnityAdapters/ShooterMover.UnityAdapters.asmdef",
                "ShooterMover.UnityAdapters",
                false,
                "ShooterMover.Domain",
                "ShooterMover.Contracts",
                "ShooterMover.Application",
                "Unity.InputSystem"),
            new ExpectedAssembly(
                "Assets/ShooterMover/Content/Definitions/ShooterMover.Content.Definitions.asmdef",
                "ShooterMover.Content.Definitions",
                false,
                "ShooterMover.Domain",
                "ShooterMover.Contracts",
                "ShooterMover.Application"),
            new ExpectedAssembly(
                "Assets/ShooterMover/Runtime/Presentation/ShooterMover.Presentation.asmdef",
                "ShooterMover.Presentation",
                false,
                "ShooterMover.Domain",
                "ShooterMover.Contracts",
                "ShooterMover.Application",
                "ShooterMover.UnityAdapters"),
            new ExpectedAssembly(
                "Assets/ShooterMover/Runtime/Bootstrap/ShooterMover.Bootstrap.asmdef",
                "ShooterMover.Bootstrap",
                false,
                "ShooterMover.Domain",
                "ShooterMover.Contracts",
                "ShooterMover.Application",
                "ShooterMover.UnityAdapters",
                "ShooterMover.Presentation",
                "ShooterMover.Content.Definitions"),
            new ExpectedAssembly(
                "Assets/ShooterMover/Tests/EditMode/ShooterMover.Tests.EditMode.asmdef",
                "ShooterMover.Tests.EditMode",
                false,
                "ShooterMover.Domain",
                "ShooterMover.Contracts",
                "ShooterMover.Application",
                "ShooterMover.UnityAdapters",
                "ShooterMover.Presentation",
                "ShooterMover.Content.Definitions",
                "ShooterMover.Bootstrap"),
            new ExpectedAssembly(
                "Assets/ShooterMover/Tests/PlayMode/ShooterMover.Tests.PlayMode.asmdef",
                "ShooterMover.Tests.PlayMode",
                false,
                "ShooterMover.Domain",
                "ShooterMover.Contracts",
                "ShooterMover.Application",
                "ShooterMover.UnityAdapters",
                "ShooterMover.Presentation",
                "ShooterMover.Content.Definitions",
                "ShooterMover.Bootstrap",
                "Unity.InputSystem",
                "Unity.InputSystem.TestFramework"),
        };

        private static readonly HashSet<string> AllowedExternalAssemblyReferences =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Unity.InputSystem",
                "Unity.InputSystem.TestFramework",
            };

        [Test]
        public void AssemblyDefinitions_MatchExactInwardOnlyGraph()
        {
            List<string> failures;
            Dictionary<string, LoadedAssembly> loaded = LoadDefinitions(out failures);
            failures.AddRange(ValidateGraph(loaded));

            if (failures.Count > 0)
            {
                Assert.Fail(
                    "Foundation assembly dependency validation failed:\n - "
                    + string.Join("\n - ", failures));
            }
        }

        [Test]
        public void Validator_ReportsForbiddenDependencyWithSourcePath()
        {
            var loaded = new Dictionary<string, LoadedAssembly>(StringComparer.Ordinal);
            for (int index = 0; index < ExpectedAssemblies.Length; index++)
            {
                ExpectedAssembly expected = ExpectedAssemblies[index];
                loaded.Add(
                    expected.Name,
                    new LoadedAssembly(
                        expected.AssetPath,
                        expected.Name,
                        expected.References,
                        expected.NoEngineReferences));
            }

            ExpectedAssembly domain = ExpectedAssemblies[0];
            loaded[domain.Name] = new LoadedAssembly(
                domain.AssetPath,
                domain.Name,
                new[] { "ShooterMover.UnityAdapters" },
                domain.NoEngineReferences);

            List<string> failures = ValidateGraph(loaded);
            string combined = string.Join("\n", failures);

            Assert.That(
                failures.Any(failure =>
                    failure.Contains(domain.AssetPath)
                    && failure.Contains("ShooterMover.UnityAdapters")),
                Is.True,
                "A forbidden edge must identify both its source asset and dependency.\n"
                + combined);
        }

        private static Dictionary<string, LoadedAssembly> LoadDefinitions(
            out List<string> failures)
        {
            failures = new List<string>();
            var loaded = new Dictionary<string, LoadedAssembly>(StringComparer.Ordinal);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            for (int index = 0; index < ExpectedAssemblies.Length; index++)
            {
                ExpectedAssembly expected = ExpectedAssemblies[index];
                string absolutePath = Path.Combine(
                    projectRoot,
                    expected.AssetPath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(absolutePath))
                {
                    failures.Add("Missing assembly definition: " + expected.AssetPath + ".");
                    continue;
                }

                AssemblyDefinitionJson json;
                try
                {
                    json = JsonUtility.FromJson<AssemblyDefinitionJson>(
                        File.ReadAllText(absolutePath));
                }
                catch (Exception exception)
                {
                    failures.Add(
                        "Could not parse " + expected.AssetPath + ": " + exception.Message);
                    continue;
                }

                if (json == null || string.IsNullOrEmpty(json.name))
                {
                    failures.Add(
                        "Assembly definition has no readable name: "
                        + expected.AssetPath + ".");
                    continue;
                }

                if (!string.Equals(json.name, expected.Name, StringComparison.Ordinal))
                {
                    failures.Add(
                        expected.AssetPath + " declares name '" + json.name
                        + "' instead of '" + expected.Name + "'.");
                }

                if (loaded.ContainsKey(json.name))
                {
                    failures.Add(
                        "Duplicate assembly name '" + json.name + "' at "
                        + loaded[json.name].AssetPath + " and " + expected.AssetPath + ".");
                    continue;
                }

                loaded.Add(
                    json.name,
                    new LoadedAssembly(
                        expected.AssetPath,
                        json.name,
                        json.references ?? new string[0],
                        json.noEngineReferences));
            }

            return loaded;
        }

        private static List<string> ValidateGraph(
            IReadOnlyDictionary<string, LoadedAssembly> loaded)
        {
            var failures = new List<string>();
            var knownNames = new HashSet<string>(
                ExpectedAssemblies.Select(expected => expected.Name),
                StringComparer.Ordinal);

            for (int index = 0; index < ExpectedAssemblies.Length; index++)
            {
                ExpectedAssembly expected = ExpectedAssemblies[index];
                LoadedAssembly actual;
                if (!loaded.TryGetValue(expected.Name, out actual))
                {
                    failures.Add(
                        "Expected assembly '" + expected.Name + "' was not loaded from "
                        + expected.AssetPath + ".");
                    continue;
                }

                string[] duplicateReferences = actual.References
                    .GroupBy(reference => reference, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .OrderBy(reference => reference, StringComparer.Ordinal)
                    .ToArray();
                if (duplicateReferences.Length > 0)
                {
                    failures.Add(
                        actual.AssetPath + " repeats references: "
                        + string.Join(", ", duplicateReferences) + ".");
                }

                string[] unexpected = actual.References
                    .Except(expected.References, StringComparer.Ordinal)
                    .OrderBy(reference => reference, StringComparer.Ordinal)
                    .ToArray();
                string[] missing = expected.References
                    .Except(actual.References, StringComparer.Ordinal)
                    .OrderBy(reference => reference, StringComparer.Ordinal)
                    .ToArray();

                if (unexpected.Length > 0 || missing.Length > 0)
                {
                    failures.Add(
                        actual.AssetPath + " has forbidden or missing direct dependencies. "
                        + "Expected [" + string.Join(", ", expected.References) + "]; actual ["
                        + string.Join(", ", actual.References) + "]. Unexpected ["
                        + string.Join(", ", unexpected) + "]; missing ["
                        + string.Join(", ", missing) + "].");
                }

                if (actual.NoEngineReferences != expected.NoEngineReferences)
                {
                    failures.Add(
                        actual.AssetPath + " sets noEngineReferences="
                        + actual.NoEngineReferences + " but expected "
                        + expected.NoEngineReferences + ".");
                }

                for (int referenceIndex = 0;
                    referenceIndex < actual.References.Length;
                    referenceIndex++)
                {
                    string reference = actual.References[referenceIndex];
                    if (!knownNames.Contains(reference)
                        && !AllowedExternalAssemblyReferences.Contains(reference))
                    {
                        failures.Add(
                            actual.AssetPath + " references unknown internal assembly '"
                            + reference + "'.");
                    }
                }
            }

            AddCycleFailures(loaded, failures);
            return failures;
        }

        private static void AddCycleFailures(
            IReadOnlyDictionary<string, LoadedAssembly> loaded,
            List<string> failures)
        {
            var states = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new List<string>();

            foreach (string name in loaded.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!states.ContainsKey(name))
                {
                    Visit(name, loaded, states, stack, failures);
                }
            }
        }

        private static void Visit(
            string name,
            IReadOnlyDictionary<string, LoadedAssembly> loaded,
            IDictionary<string, int> states,
            IList<string> stack,
            ICollection<string> failures)
        {
            states[name] = 1;
            stack.Add(name);

            LoadedAssembly assembly = loaded[name];
            for (int index = 0; index < assembly.References.Length; index++)
            {
                string reference = assembly.References[index];
                if (!loaded.ContainsKey(reference))
                {
                    continue;
                }

                int state;
                if (!states.TryGetValue(reference, out state))
                {
                    Visit(reference, loaded, states, stack, failures);
                    continue;
                }

                if (state == 1)
                {
                    int cycleStart = stack.IndexOf(reference);
                    string[] cycle = stack
                        .Skip(cycleStart)
                        .Concat(new[] { reference })
                        .ToArray();
                    string[] paths = cycle
                        .Distinct(StringComparer.Ordinal)
                        .Select(cycleName => loaded[cycleName].AssetPath)
                        .ToArray();
                    failures.Add(
                        "Assembly dependency cycle " + string.Join(" -> ", cycle)
                        + ". Assets: " + string.Join(", ", paths) + ".");
                }
            }

            stack.RemoveAt(stack.Count - 1);
            states[name] = 2;
        }

        [Serializable]
        private sealed class AssemblyDefinitionJson
        {
            public string name;
            public string[] references;
            public bool noEngineReferences;
        }

        private sealed class ExpectedAssembly
        {
            public ExpectedAssembly(
                string assetPath,
                string name,
                bool noEngineReferences,
                params string[] references)
            {
                AssetPath = assetPath;
                Name = name;
                NoEngineReferences = noEngineReferences;
                References = references ?? new string[0];
            }

            public string AssetPath { get; private set; }

            public string Name { get; private set; }

            public bool NoEngineReferences { get; private set; }

            public string[] References { get; private set; }
        }

        private sealed class LoadedAssembly
        {
            public LoadedAssembly(
                string assetPath,
                string name,
                IEnumerable<string> references,
                bool noEngineReferences)
            {
                AssetPath = assetPath;
                Name = name;
                References = references == null
                    ? new string[0]
                    : references.ToArray();
                NoEngineReferences = noEngineReferences;
            }

            public string AssetPath { get; private set; }

            public string Name { get; private set; }

            public string[] References { get; private set; }

            public bool NoEngineReferences { get; private set; }
        }
    }
}

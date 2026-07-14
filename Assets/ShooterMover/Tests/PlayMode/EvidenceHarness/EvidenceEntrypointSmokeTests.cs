#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using ShooterMover.Bootstrap.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace ShooterMover.Tests.PlayMode.EvidenceHarness
{
    public sealed class EvidenceEntrypointSmokeTests
    {
        private const string BootstrapSceneName = "Bootstrap";
        private const string FoundationSmokeSceneName = "FoundationSmoke";
        private const string FixtureTypeName =
            "ShooterMover.TestSupport.Foundation.FoundationSmokeLoaderFixture";
        private const string ProbeTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceRestartProbe";
        private const float SceneTimeoutSeconds = 30f;

        private const string CanonicalConfiguration =
            "{\n"
            + "  \"schema\": \"shooter-mover.evidence-run-configuration\",\n"
            + "  \"version\": 1,\n"
            + "  \"runSeed\": 104729,\n"
            + "  \"identityReference\": \"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\",\n"
            + "  \"intentFixtureVersion\": 1,\n"
            + "  \"qualityProfile\": \"Medium\",\n"
            + "  \"locale\": \"en-US\",\n"
            + "  \"viewport\": {\n"
            + "    \"width\": 1280,\n"
            + "    \"height\": 720,\n"
            + "    \"fullscreen\": false\n"
            + "  },\n"
            + "  \"diagnostics\": {\n"
            + "    \"maxEventCount\": 4096,\n"
            + "    \"maxEventPayloadBytes\": 4096,\n"
            + "    \"maxLogBytes\": 8388608,\n"
            + "    \"retainedLogCount\": 3\n"
            + "  },\n"
            + "  \"timeouts\": {\n"
            + "    \"setupSeconds\": 30,\n"
            + "    \"smokeRunSeconds\": 120,\n"
            + "    \"shutdownSeconds\": 15\n"
            + "  }\n"
            + "}\n";

        private object activeProbe;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadBootstrapSingle("EH-009 setup");
            yield return null;
            AssertNoProbeResources("before EH-009 setup");
            RequireSingleRunningBootstrap();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (activeProbe != null)
            {
                Invoke(activeProbe, "Dispose");
                activeProbe = null;
            }

            yield return LoadBootstrapSingle("EH-009 teardown");
            yield return null;
            AssertNoProbeResources("after EH-009 teardown");
        }

        [Test]
        public void Scripts_ExposeDistinctFailClosedManifestContracts()
        {
            string repositoryRoot = RepositoryRoot();
            string editPath = Path.Combine(
                repositoryRoot,
                "tools",
                "evidence",
                "run_editmode_smoke.ps1");
            string playPath = Path.Combine(
                repositoryRoot,
                "tools",
                "evidence",
                "run_playmode_smoke.ps1");
            string windowsPath = Path.Combine(
                repositoryRoot,
                "tools",
                "evidence",
                "run_windows_build_smoke.ps1");

            string edit = RequireText(editPath);
            string play = RequireText(playPath);
            string windows = RequireText(windowsPath);

            AssertTokens(
                edit,
                editPath,
                "ParameterSetName = \"ContractTest\"",
                "Resolve-RepositoryFile",
                "Assert-FreshOutputDirectory",
                "Stale output rejected",
                "EH009-CHILD",
                "capture_build_identity.py",
                "build_evidence_manifest.py",
                "\"build\", \"--package-root\"",
                "\"verify\", \"--package-root\"",
                "--require-valid");
            AssertTokens(
                play,
                playPath,
                "run_editmode_smoke.ps1",
                "-InternalTestPlatform",
                "PlayMode",
                "-InternalEntrypointName",
                "playmode",
                "EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap");
            AssertTokens(
                windows,
                windowsPath,
                "Build-WindowsDevelopment.ps1",
                "Assert-Uf010BuildContract",
                "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity",
                "Invoke-PlayerPass",
                "CloseMainWindow",
                "startupPasses = 2",
                "restartVerified = $true",
                "run_editmode_smoke.ps1");

            Assert.That(
                edit,
                Does.Contain("[ValidateSet(\"EditMode\", \"PlayMode\")]")
                    .And.Contain("[string]$InternalTestPlatform = \"EditMode\""),
                "The EditMode entrypoint default is not explicit.");
            Assert.That(
                play,
                Does.Not.Contain("Build-WindowsDevelopment.ps1"),
                "PlayMode must remain distinct from the Windows build entrypoint.");

            foreach (Tuple<string, string> source in new[]
            {
                Tuple.Create(editPath, edit),
                Tuple.Create(playPath, play),
                Tuple.Create(windowsPath, windows)
            })
            {
                AssertForbiddenTokens(source.Item2, source.Item1);
            }
        }

        [Test]
        public void PowerShellContractTests_CoverParsingPathsStaleOutputAndExitPropagation()
        {
            if (UnityEngine.Application.platform != RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("PowerShell contract execution requires Windows Editor.");
            }

            string systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string powerShellPath = Path.Combine(
                systemDirectory,
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            Assert.That(
                File.Exists(powerShellPath),
                Is.True,
                "Windows PowerShell prerequisite is missing at " + powerShellPath + ".");

            string evidenceDirectory = Path.Combine(RepositoryRoot(), "tools", "evidence");
            string[] scripts =
            {
                "run_editmode_smoke.ps1",
                "run_playmode_smoke.ps1",
                "run_windows_build_smoke.ps1"
            };

            for (int index = 0; index < scripts.Length; index++)
            {
                string scriptPath = Path.Combine(evidenceDirectory, scripts[index]);
                ProcessResult result = RunProcess(
                    powerShellPath,
                    new[]
                    {
                        "-NoProfile",
                        "-ExecutionPolicy",
                        "Bypass",
                        "-File",
                        scriptPath,
                        "-ContractTest"
                    },
                    TimeSpan.FromSeconds(60));

                Assert.That(
                    result.ExitCode,
                    Is.Zero,
                    scripts[index] + " contract test failed.\nstdout:\n"
                    + result.StandardOutput + "\nstderr:\n" + result.StandardError);
                Assert.That(
                    result.StandardOutput,
                    Does.Contain("contract tests passed"),
                    scripts[index] + " did not report its contract coverage.");
            }
        }

        [UnityTest]
        public IEnumerator EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap()
        {
            BootstrapSceneAdapter adapterBefore = RequireSingleRunningBootstrap();
            int adapterInstanceId = adapterBefore.GetInstanceID();

            IEnumerator smokeFlow = InvokeStatic(
                FixtureTypeName,
                "LoadUnloadReloadAndReturnToBootstrap") as IEnumerator;
            Assert.That(
                smokeFlow,
                Is.Not.Null,
                FixtureTypeName
                + ".LoadUnloadReloadAndReturnToBootstrap did not return IEnumerator.");
            yield return smokeFlow;
            yield return null;

            Assert.That(
                GetStaticProperty<bool>(FixtureTypeName, "IsLoaded"),
                Is.False,
                "FoundationSmoke remained loaded after entrypoint scene smoke.");
            Assert.That(
                GetStaticProperty<int>(FixtureTypeName, "ActiveInstanceCount"),
                Is.Zero,
                "FoundationSmoke retained a fixture instance.");
            Assert.That(
                GetStaticProperty<bool>(FixtureTypeName, "IsOperationInFlight"),
                Is.False,
                "FoundationSmoke retained an in-flight operation.");

            activeProbe = InvokeStatic(
                ProbeTypeName,
                "Create",
                CanonicalConfiguration,
                "session.eh009-playmode",
                "attempt.eh009-playmode-1");
            Assert.That(activeProbe, Is.Not.Null);

            AssertApplied(
                Invoke(activeProbe, "BeginStart") as string,
                "Configured->Starting");
            AssertApplied(
                Invoke(activeProbe, "CompleteStart") as string,
                "Starting->Running");

            string restart = Invoke(
                activeProbe,
                "QuickRestart",
                "attempt.eh009-playmode-2") as string;
            AssertApplied(restart, "Restarting->Running");
            Assert.That(restart, Does.Contain("diagnostic_kind=RunRestarted"));
            Assert.That(
                GetProperty<string>(activeProbe, "CurrentParentAttemptId"),
                Is.EqualTo("attempt.eh009-playmode-1"));
            Assert.That(
                GetProperty<int>(activeProbe, "CurrentAttemptOrdinal"),
                Is.EqualTo(2));
            Assert.That(
                GetProperty<bool>(activeProbe, "HasStaleIntent"),
                Is.False);

            AssertApplied(
                Invoke(activeProbe, "EndCompleted") as string,
                "Ending->Ended");
            Assert.That(
                GetProperty<string>(activeProbe, "StateName"),
                Is.EqualTo("Ended"));
            Assert.That(
                GetProperty<int>(activeProbe, "CurrentOwnedObjectCount"),
                Is.Zero);
            Assert.That(
                GetProperty<int>(activeProbe, "CurrentSubscriptionCount"),
                Is.Zero);
            Assert.That(
                GetProperty<int>(activeProbe, "RetiredObjectLeakCount"),
                Is.Zero);

            Invoke(activeProbe, "Dispose");
            activeProbe = null;
            yield return null;

            BootstrapSceneAdapter adapterAfter = RequireSingleRunningBootstrap();
            Assert.That(
                adapterAfter.GetInstanceID(),
                Is.EqualTo(adapterInstanceId),
                "Scene/session smoke replaced the canonical Bootstrap owner.");
            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(BootstrapSceneName));
            Assert.That(
                Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(index => SceneManager.GetSceneAt(index).name),
                Does.Not.Contain(FoundationSmokeSceneName));
            AssertNoProbeResources("after EH-009 integrated scene/session smoke");
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo parent = Directory.GetParent(UnityEngine.Application.dataPath);
            Assert.That(parent, Is.Not.Null, "Could not resolve the repository root from Application.dataPath.");
            return parent.FullName;
        }

        private static string RequireText(string path)
        {
            Assert.That(File.Exists(path), Is.True, "Required entrypoint is missing: " + path);
            return File.ReadAllText(path);
        }

        private static void AssertTokens(string source, string path, params string[] tokens)
        {
            for (int index = 0; index < tokens.Length; index++)
            {
                Assert.That(
                    source,
                    Does.Contain(tokens[index]),
                    path + " is missing required contract token: " + tokens[index]);
            }
        }

        private static void AssertForbiddenTokens(string source, string path)
        {
            string[] forbidden =
            {
                "Invoke-WebRequest",
                "Invoke-RestMethod",
                "System.Net.Http",
                "gh auth",
                "git clone",
                "Start-Job",
                "Register-ScheduledJob"
            };
            for (int index = 0; index < forbidden.Length; index++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbidden[index]),
                    path + " contains forbidden remote/background behavior: " + forbidden[index]);
            }
        }

        private static ProcessResult RunProcess(
            string executable,
            IEnumerable<string> arguments,
            TimeSpan timeout)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                WorkingDirectory = RepositoryRoot(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (DiagnosticsProcess process = new DiagnosticsProcess())
            {
                process.StartInfo = startInfo;
                Assert.That(process.Start(), Is.True, "Could not start " + executable + ".");
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // The timeout assertion remains authoritative.
                    }
                    Assert.Fail("Process timed out after " + timeout + ": " + executable);
                }
                return new ProcessResult(process.ExitCode, standardOutput, standardError);
            }
        }

        private static string QuoteArgument(string argument)
        {
            if (argument.IndexOf('"') >= 0)
            {
                Assert.Fail("Test process argument contains an unsupported quote: " + argument);
            }
            return argument.Length == 0 || argument.Any(char.IsWhiteSpace)
                ? "\"" + argument + "\""
                : argument;
        }

        private static IEnumerator LoadBootstrapSingle(string description)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                BootstrapSceneName,
                LoadSceneMode.Single);
            if (operation == null)
            {
                Assert.Fail("Unity did not create the Bootstrap scene operation for " + description + ".");
            }

            float startedAt = Time.realtimeSinceStartup;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - startedAt > SceneTimeoutSeconds)
                {
                    Assert.Fail(
                        description + " did not complete within "
                        + SceneTimeoutSeconds + " seconds.");
                }
                yield return null;
            }
        }

        private static BootstrapSceneAdapter RequireSingleRunningBootstrap()
        {
            BootstrapSceneAdapter[] adapters = Resources
                .FindObjectsOfTypeAll<BootstrapSceneAdapter>()
                .Where(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.scene.isLoaded)
                .ToArray();
            Assert.That(
                adapters,
                Has.Length.EqualTo(1),
                "Expected exactly one loaded BootstrapSceneAdapter; found "
                + adapters.Length + ".");
            Assert.That(
                adapters[0].IsCompositionRootRunning,
                Is.True,
                "The canonical Bootstrap composition root is not running.");
            return adapters[0];
        }

        private static void AssertNoProbeResources(string context)
        {
            Assert.That(
                GetStaticProperty<int>(ProbeTypeName, "ActiveProbeCount"),
                Is.Zero,
                context);
            Assert.That(
                GetStaticProperty<int>(ProbeTypeName, "LiveOwnedObjectCount"),
                Is.Zero,
                context);
            Assert.That(
                GetStaticProperty<int>(ProbeTypeName, "LiveSubscriptionCount"),
                Is.Zero,
                context);
            Assert.That(
                GetStaticProperty<int>(ProbeTypeName, "SceneTestObjectCount"),
                Is.Zero,
                context);
        }

        private static void AssertApplied(string transition, string stateChange)
        {
            Assert.That(transition, Is.Not.Null);
            Assert.That(transition, Does.Contain("disposition=Applied"));
            Assert.That(transition, Does.Contain("state=" + stateChange));
        }

        private static object InvokeStatic(
            string typeName,
            string methodName,
            params object[] arguments)
        {
            return InvokeMethod(null, ResolveType(typeName), methodName, arguments);
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            return InvokeMethod(target, target.GetType(), methodName, arguments);
        }

        private static object InvokeMethod(
            object target,
            Type type,
            string methodName,
            object[] arguments)
        {
            MethodInfo method = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .SingleOrDefault(candidate => candidate.Name == methodName
                    && candidate.IsStatic == (target == null)
                    && ParametersMatch(candidate.GetParameters(), arguments));
            if (method == null)
            {
                Assert.Fail(
                    "Missing method " + type.FullName + "." + methodName
                    + " for " + arguments.Length + " argument(s).");
            }

            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                ExceptionDispatchInfo.Capture(inner).Throw();
                throw;
            }
        }

        private static bool ParametersMatch(
            ParameterInfo[] parameters,
            object[] arguments)
        {
            if (parameters.Length != arguments.Length)
            {
                return false;
            }
            for (int index = 0; index < parameters.Length; index++)
            {
                object argument = arguments[index];
                if (argument == null)
                {
                    if (parameters[index].ParameterType.IsValueType)
                    {
                        return false;
                    }
                }
                else if (!parameters[index].ParameterType.IsAssignableFrom(argument.GetType()))
                {
                    return false;
                }
            }
            return true;
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                Assert.Fail("Missing property " + target.GetType().FullName + "." + propertyName + ".");
            }
            object value = property.GetValue(target, null);
            if (!(value is T))
            {
                Assert.Fail(
                    propertyName + " returned "
                    + (value == null ? "null" : value.GetType().FullName)
                    + " instead of " + typeof(T).FullName + ".");
            }
            return (T)value;
        }

        private static T GetStaticProperty<T>(string typeName, string propertyName)
        {
            Type type = ResolveType(typeName);
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            if (property == null)
            {
                Assert.Fail("Missing static property " + typeName + "." + propertyName + ".");
            }
            object value = property.GetValue(null, null);
            if (!(value is T))
            {
                Assert.Fail(
                    typeName + "." + propertyName + " returned "
                    + (value == null ? "null" : value.GetType().FullName)
                    + " instead of " + typeof(T).FullName + ".");
            }
            return (T)value;
        }

        private static Type ResolveType(string fullName)
        {
            Type found = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type candidate = assemblies[index].GetType(fullName, false);
                if (candidate == null)
                {
                    continue;
                }
                if (found != null)
                {
                    Assert.Fail(
                        "Type " + fullName + " is duplicated in assemblies '"
                        + found.Assembly.GetName().Name + "' and '"
                        + candidate.Assembly.GetName().Name + "'.");
                }
                found = candidate;
            }
            Assert.That(found, Is.Not.Null, "Could not resolve type " + fullName + ".");
            return found;
        }

        private sealed class ProcessResult
        {
            public ProcessResult(
                int exitCode,
                string standardOutput,
                string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public int ExitCode { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }
        }
    }
}
#endif

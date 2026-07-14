#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.EvidenceHarness
{
    public sealed class EvidenceSessionLifecycleTests
    {
        private const string ProbeTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceRestartProbe";
        private const string InitialSessionId = "session.eh006-stage1-0001";
        private const string InitialAttemptId = "attempt.eh006-stage1-0001";

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

        private static readonly string[] ExpectedMarkers =
        {
            "route.start",
            "session.start",
            "socket.player.primary"
        };

        private readonly List<object> probes = new List<object>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return null;
            AssertGlobalCleanup("before EH-006 test setup");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = probes.Count - 1; index >= 0; index--)
            {
                Invoke(probes[index], "Dispose");
            }

            probes.Clear();
            yield return null;
            AssertGlobalCleanup("after EH-006 test teardown");
        }

        [UnityTest]
        public IEnumerator LegalTransitions_VisitEveryOperationalState_AndEndIsIdempotent()
        {
            object probe = CreateProbe();
            var states = new List<string> { Get<string>(probe, "StateName") };

            AssertApplied(Invoke(probe, "BeginStart") as string, "Configured->Starting");
            states.Add(Get<string>(probe, "StateName"));
            AssertApplied(Invoke(probe, "CompleteStart") as string, "Starting->Running");
            states.Add(Get<string>(probe, "StateName"));

            AssertApplied(
                Invoke(probe, "BeginRestart", "attempt.eh006-stage1-0002") as string,
                "Running->Restarting");
            states.Add(Get<string>(probe, "StateName"));
            AssertApplied(Invoke(probe, "CompleteRestart") as string, "Restarting->Running");
            states.Add(Get<string>(probe, "StateName"));

            AssertApplied(Invoke(probe, "BeginEnd") as string, "Running->Ending");
            states.Add(Get<string>(probe, "StateName"));
            AssertApplied(Invoke(probe, "CompleteEndCompleted") as string, "Ending->Ended");
            states.Add(Get<string>(probe, "StateName"));

            string repeatedEnd = Invoke(probe, "CompleteEndCompleted") as string;
            Assert.That(repeatedEnd, Does.Contain("disposition=NoChange"));
            Assert.That(Get<string>(probe, "StateName"), Is.EqualTo("Ended"));
            Assert.That(
                states,
                Is.EqualTo(
                    new[]
                    {
                        "Configured",
                        "Starting",
                        "Running",
                        "Restarting",
                        "Running",
                        "Ending",
                        "Ended"
                    }));
            yield return null;
        }

        [UnityTest]
        public IEnumerator IllegalTransitions_AreRejectedAndFailClosed()
        {
            object premature = CreateProbe();
            string rejected = Invoke(premature, "CompleteStart") as string;

            Assert.That(rejected, Does.Contain("disposition=Rejected"));
            Assert.That(rejected, Does.Contain("state=Configured->Invalid"));
            Assert.That(Get<string>(premature, "StateName"), Is.EqualTo("Invalid"));
            Assert.That(Get<int>(premature, "CurrentOwnedObjectCount"), Is.Zero);
            Assert.That(Get<int>(premature, "CurrentSubscriptionCount"), Is.Zero);

            AssertApplied(Invoke(premature, "BeginEnd") as string, "Invalid->Ending");
            AssertApplied(Invoke(premature, "CompleteEndAborted") as string, "Ending->Ended");

            object duplicate = CreateProbe(
                "session.eh006-stage1-duplicate",
                "attempt.eh006-stage1-duplicate");
            StartProbe(duplicate);
            string duplicateAttempt = Invoke(
                duplicate,
                "BeginRestart",
                "attempt.eh006-stage1-duplicate") as string;

            Assert.That(duplicateAttempt, Does.Contain("disposition=Rejected"));
            Assert.That(duplicateAttempt, Does.Contain("reason=duplicate-attempt-id"));
            Assert.That(Get<string>(duplicate, "StateName"), Is.EqualTo("Invalid"));
            Assert.That(Get<int>(duplicate, "CurrentOwnedObjectCount"), Is.Zero);
            Assert.That(Get<int>(duplicate, "CurrentSubscriptionCount"), Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QuickRestart_PreservesCanonicalIdentityAndAppendsParentLineage()
        {
            object probe = CreateProbe();
            StartProbe(probe);

            string originalSession = Get<string>(probe, "SessionId");
            string originalAttempt = Get<string>(probe, "CurrentAttemptId");
            string originalStart = Get<string>(probe, "CanonicalStartIdentity");
            string originalFingerprint = Get<string>(probe, "ConfigurationFingerprint");

            string restart = Invoke(
                probe,
                "QuickRestart",
                "attempt.eh006-stage1-0002") as string;

            AssertApplied(restart, "Restarting->Running");
            Assert.That(restart, Does.Contain("diagnostic_kind=RunRestarted"));
            Assert.That(restart, Does.Contain("previous_run_id=" + originalAttempt));
            Assert.That(Get<string>(probe, "SessionId"), Is.EqualTo(originalSession));
            Assert.That(
                Get<string>(probe, "CurrentAttemptId"),
                Is.EqualTo("attempt.eh006-stage1-0002"));
            Assert.That(Get<string>(probe, "CurrentParentAttemptId"), Is.EqualTo(originalAttempt));
            Assert.That(Get<int>(probe, "CurrentAttemptOrdinal"), Is.EqualTo(2));
            Assert.That(Get<string>(probe, "CanonicalStartIdentity"), Is.EqualTo(originalStart));
            Assert.That(Get<string>(probe, "ConfigurationFingerprint"), Is.EqualTo(originalFingerprint));

            string audit = Get<string>(probe, "AuditSnapshot");
            Assert.That(audit, Does.Contain("operation=BeginRestart"));
            Assert.That(audit, Does.Contain("operation=CompleteRestart"));
            Assert.That(audit, Does.Contain("parent_attempt_id=" + originalAttempt));
            Assert.That(audit, Does.Contain("previous_run_id=" + originalAttempt));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Restart_ClearsIntentAndReplacesMarkersSubscriptionAndOwnedObjects()
        {
            object probe = CreateProbe();
            StartProbe(probe);
            int[] originalObjectIds = Get<int[]>(probe, "CurrentObjectInstanceIds");

            Invoke(probe, "SetHeldIntentForTest");
            Assert.That(Get<bool>(probe, "HasStaleIntent"), Is.True);
            Invoke(probe, "EmitAttemptSignalForTest", "before-restart");
            Assert.That(Get<int>(probe, "ObservedSignalCount"), Is.EqualTo(1));
            Assert.That(Get<string>(probe, "LastObservedAttemptId"), Is.EqualTo(InitialAttemptId));

            Invoke(probe, "QuickRestart", "attempt.eh006-stage1-0002");
            int[] replacementObjectIds = Get<int[]>(probe, "CurrentObjectInstanceIds");

            Assert.That(Get<bool>(probe, "HasStaleIntent"), Is.False);
            Assert.That(Get<bool>(probe, "LastBoundaryWasFocusLoss"), Is.True);
            Assert.That(Get<bool>(probe, "LastBoundaryReleasedHeldAction"), Is.True);
            Assert.That(Get<string[]>(probe, "CurrentMarkerIds"), Is.EqualTo(ExpectedMarkers));
            Assert.That(Get<int>(probe, "MarkerCount"), Is.EqualTo(ExpectedMarkers.Length));
            Assert.That(Get<int>(probe, "CurrentOwnedObjectCount"), Is.EqualTo(4));
            Assert.That(Get<int>(probe, "CurrentSubscriptionCount"), Is.EqualTo(1));
            Assert.That(Get<int>(probe, "RetiredObjectLeakCount"), Is.Zero);
            Assert.That(GetStatic<int>("LiveOwnedObjectCount"), Is.EqualTo(4));
            Assert.That(GetStatic<int>("LiveSubscriptionCount"), Is.EqualTo(1));
            Assert.That(GetStatic<int>("SceneTestObjectCount"), Is.EqualTo(4));
            Assert.That(
                originalObjectIds.Intersect(replacementObjectIds),
                Is.Empty,
                "Restart retained at least one attempt-owned Unity object.");

            Invoke(probe, "EmitAttemptSignalForTest", "after-restart");
            Assert.That(Get<int>(probe, "ObservedSignalCount"), Is.EqualTo(2));
            Assert.That(
                Get<string>(probe, "LastObservedAttemptId"),
                Is.EqualTo("attempt.eh006-stage1-0002"));
            Assert.That(Get<string>(probe, "LastObservedSignal"), Is.EqualTo("after-restart"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InterruptedRestart_InvalidatesCleansAndEndsDeterministically()
        {
            object probe = CreateProbe();
            StartProbe(probe);
            Invoke(probe, "SetHeldIntentForTest");

            AssertApplied(
                Invoke(probe, "BeginRestart", "attempt.eh006-stage1-0002") as string,
                "Running->Restarting");
            Assert.That(Get<string>(probe, "PendingAttemptId"), Is.EqualTo("attempt.eh006-stage1-0002"));
            AssertAttemptResourcesReleased(probe, "during interrupted restart");

            string invalidated = Invoke(probe, "AbortRestartForTest") as string;
            AssertApplied(invalidated, "Restarting->Invalid");
            Assert.That(invalidated, Does.Contain("diagnostic_kind=Exception"));
            Assert.That(Get<string>(probe, "StateName"), Is.EqualTo("Invalid"));
            Assert.That(Get<string>(probe, "PendingAttemptId"), Is.Empty);
            Assert.That(Get<bool>(probe, "HasStaleIntent"), Is.False);
            AssertAttemptResourcesReleased(probe, "after interrupted restart invalidation");

            AssertApplied(Invoke(probe, "BeginEnd") as string, "Invalid->Ending");
            AssertApplied(Invoke(probe, "CompleteEndAborted") as string, "Ending->Ended");
            Assert.That(
                Invoke(probe, "CompleteEndAborted") as string,
                Does.Contain("disposition=NoChange"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FiftyConsecutiveRestartCycles_LeaveNoDuplicatesLeaksOrStaleState()
        {
            object probe = CreateProbe();
            StartProbe(probe);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int cycle = 1; cycle <= 50; cycle++)
            {
                Invoke(probe, "SetHeldIntentForTest");
                string nextAttemptId =
                    "attempt.eh006-stage1-" + (cycle + 1).ToString("D4");
                string transition = Invoke(probe, "QuickRestart", nextAttemptId) as string;

                AssertApplied(transition, "Restarting->Running");
                Assert.That(Get<string>(probe, "StateName"), Is.EqualTo("Running"));
                Assert.That(Get<string>(probe, "CurrentAttemptId"), Is.EqualTo(nextAttemptId));
                Assert.That(Get<int>(probe, "CurrentAttemptOrdinal"), Is.EqualTo(cycle + 1));
                Assert.That(Get<string[]>(probe, "CurrentMarkerIds"), Is.EqualTo(ExpectedMarkers));
                Assert.That(
                    Get<string[]>(probe, "CurrentMarkerIds")
                        .Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(ExpectedMarkers.Length));
                Assert.That(Get<int>(probe, "MarkerCount"), Is.EqualTo(ExpectedMarkers.Length));
                Assert.That(Get<int>(probe, "CurrentOwnedObjectCount"), Is.EqualTo(4));
                Assert.That(Get<int>(probe, "CurrentSubscriptionCount"), Is.EqualTo(1));
                Assert.That(Get<int>(probe, "RetiredObjectLeakCount"), Is.Zero);
                Assert.That(Get<bool>(probe, "HasStaleIntent"), Is.False);
                Assert.That(GetStatic<int>("LiveOwnedObjectCount"), Is.EqualTo(4));
                Assert.That(GetStatic<int>("LiveSubscriptionCount"), Is.EqualTo(1));
                Assert.That(GetStatic<int>("SceneTestObjectCount"), Is.EqualTo(4));

                Invoke(probe, "EmitAttemptSignalForTest", "cycle-" + cycle.ToString("D2"));
                Assert.That(Get<int>(probe, "ObservedSignalCount"), Is.EqualTo(cycle));
                Assert.That(Get<string>(probe, "LastObservedAttemptId"), Is.EqualTo(nextAttemptId));
            }

            stopwatch.Stop();
            Assert.That(Get<int>(probe, "CurrentAttemptOrdinal"), Is.EqualTo(51));
            Assert.That(Get<int>(probe, "AuditCount"), Is.EqualTo(103));
            Assert.That(
                stopwatch.ElapsedMilliseconds,
                Is.LessThan(5000L),
                "Fifty synchronous quick restarts exceeded the five-second PlayMode sentinel.");

            string audit = Get<string>(probe, "AuditSnapshot");
            Assert.That(CountOccurrences(audit, "operation=BeginRestart"), Is.EqualTo(50));
            Assert.That(CountOccurrences(audit, "operation=CompleteRestart"), Is.EqualTo(50));
            Assert.That(CountOccurrences(audit, "diagnostic_kind=RunRestarted"), Is.EqualTo(50));

            AssertApplied(Invoke(probe, "EndCompleted") as string, "Ending->Ended");
            AssertAttemptResourcesReleased(probe, "after fifty-cycle end");
            Debug.Log(
                "EH-006 fifty-cycle summary: cycles=50; finalAttemptOrdinal=51; "
                + "auditCount=" + Get<int>(probe, "AuditCount")
                + "; elapsedMs=" + stopwatch.ElapsedMilliseconds
                + "; markers=0; subscriptions=0; ownedObjects=0; staleIntent=false");
            yield return null;
        }

        private object CreateProbe()
        {
            return CreateProbe(InitialSessionId, InitialAttemptId);
        }

        private object CreateProbe(string sessionId, string attemptId)
        {
            object created = InvokeStatic(
                "Create",
                CanonicalConfiguration,
                sessionId,
                attemptId);
            Assert.That(created, Is.Not.Null);
            probes.Add(created);
            return created;
        }

        private static void StartProbe(object probe)
        {
            AssertApplied(Invoke(probe, "BeginStart") as string, "Configured->Starting");
            AssertApplied(Invoke(probe, "CompleteStart") as string, "Starting->Running");
            Assert.That(Get<string>(probe, "StateName"), Is.EqualTo("Running"));
            Assert.That(Get<string[]>(probe, "CurrentMarkerIds"), Is.EqualTo(ExpectedMarkers));
            Assert.That(Get<int>(probe, "CurrentOwnedObjectCount"), Is.EqualTo(4));
            Assert.That(Get<int>(probe, "CurrentSubscriptionCount"), Is.EqualTo(1));
            Assert.That(Get<bool>(probe, "HasStaleIntent"), Is.False);
        }

        private static void AssertAttemptResourcesReleased(object probe, string context)
        {
            Assert.That(Get<int>(probe, "MarkerCount"), Is.Zero, context);
            Assert.That(Get<int>(probe, "CurrentOwnedObjectCount"), Is.Zero, context);
            Assert.That(Get<int>(probe, "CurrentSubscriptionCount"), Is.Zero, context);
            Assert.That(Get<int>(probe, "RetiredObjectLeakCount"), Is.Zero, context);
            Assert.That(GetStatic<int>("LiveOwnedObjectCount"), Is.Zero, context);
            Assert.That(GetStatic<int>("LiveSubscriptionCount"), Is.Zero, context);
            Assert.That(GetStatic<int>("SceneTestObjectCount"), Is.Zero, context);
        }

        private static void AssertGlobalCleanup(string context)
        {
            Assert.That(GetStatic<int>("ActiveProbeCount"), Is.Zero, context);
            Assert.That(GetStatic<int>("LiveOwnedObjectCount"), Is.Zero, context);
            Assert.That(GetStatic<int>("LiveSubscriptionCount"), Is.Zero, context);
            Assert.That(GetStatic<int>("SceneTestObjectCount"), Is.Zero, context);
        }

        private static void AssertApplied(string transition, string stateChange)
        {
            Assert.That(transition, Is.Not.Null);
            Assert.That(transition, Does.Contain("disposition=Applied"));
            Assert.That(transition, Does.Contain("state=" + stateChange));
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while (true)
            {
                int found = text.IndexOf(value, offset, StringComparison.Ordinal);
                if (found < 0)
                {
                    return count;
                }

                count++;
                offset = found + value.Length;
            }
        }

        private static object InvokeStatic(string methodName, params object[] arguments)
        {
            return InvokeMethod(null, ResolveProbeType(), methodName, arguments);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
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
                .SingleOrDefault(
                    candidate => candidate.Name == methodName
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

        private static bool ParametersMatch(ParameterInfo[] parameters, object[] arguments)
        {
            if (parameters.Length != arguments.Length)
            {
                return false;
            }

            for (int index = 0; index < parameters.Length; index++)
            {
                if (arguments[index] == null)
                {
                    if (parameters[index].ParameterType.IsValueType)
                    {
                        return false;
                    }
                }
                else if (!parameters[index].ParameterType.IsAssignableFrom(arguments[index].GetType()))
                {
                    return false;
                }
            }

            return true;
        }

        private static T Get<T>(object target, string propertyName)
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

        private static T GetStatic<T>(string propertyName)
        {
            PropertyInfo property = ResolveProbeType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            if (property == null)
            {
                Assert.Fail("Missing static property " + ProbeTypeName + "." + propertyName + ".");
            }

            object value = property.GetValue(null, null);
            if (!(value is T))
            {
                Assert.Fail(
                    propertyName + " returned "
                    + (value == null ? "null" : value.GetType().FullName)
                    + " instead of " + typeof(T).FullName + ".");
            }

            return (T)value;
        }

        private static Type ResolveProbeType()
        {
            Type found = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type candidate = assemblies[index].GetType(ProbeTypeName, false);
                if (candidate == null)
                {
                    continue;
                }

                if (found != null)
                {
                    Assert.Fail(
                        "Type " + ProbeTypeName + " is duplicated in assemblies '"
                        + found.Assembly.GetName().Name + "' and '"
                        + candidate.Assembly.GetName().Name + "'.");
                }

                found = candidate;
            }

            if (found == null)
            {
                Assert.Fail(
                    "Could not resolve " + ProbeTypeName + " from loaded assemblies: "
                    + string.Join(
                        ", ",
                        assemblies.Select(assembly => assembly.GetName().Name)
                            .OrderBy(name => name, StringComparer.Ordinal))
                    + ".");
            }

            return found;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace ShooterMover.Tests.PlayMode.Movement
{
    [Serializable]
    public sealed class MovementThrusterEvidenceProfile
    {
        public string schema;
        public int version;
        public string profileId;
        public EvidenceConfigurationBinding evidenceConfiguration;
        public EvidenceSimulation simulation;
        public EvidenceTuning tuning;
        public EvidenceScenario[] scenarios;
        public EvidenceBindings evidence;

        public MovementThrusterTuningProfile BuildTuning()
        {
            return MovementThrusterTuningProfile.Create(
                1, StableId.Parse(profileId),
                tuning.baseMaximumSpeed, tuning.baseAcceleration, tuning.baseBraking,
                tuning.baseCounterSteerBraking, tuning.baseVelocityResponseExponent,
                tuning.thrusterBaselineChargeCount, tuning.thrusterMaximumAdditionalCharges,
                tuning.thrusterRechargeSeconds, tuning.thrusterSpeedMultiplier,
                tuning.thrusterBurstDurationSeconds, tuning.thrusterDirectionInputThreshold,
                tuning.thrusterMinimumChainIntervalSeconds,
                tuning.thrusterSteeringDegreesPerSecond,
                tuning.thrusterStartupForgivenessSeconds,
                tuning.thrusterExitMomentumSeconds, tuning.thrusterExitSpeedRetention,
                tuning.thrusterExitDecayExponent, tuning.wallReflectionSpeedRetention,
                tuning.wallReflectionInputInfluence, tuning.wallReflectionMinimumSpeed,
                tuning.wallReflectionMaximumContacts,
                tuning.lightContactMomentumRetention,
                tuning.lightContactSteeringRetention,
                tuning.heavyContactMomentumRetention,
                tuning.perEnemyContactGraceSeconds,
                tuning.simultaneousContactWindowSeconds,
                tuning.contactGraceCapacity);
        }
    }

    [Serializable]
    public sealed class EvidenceConfigurationBinding
    {
        public string schema; public int version; public int runSeed;
        public int intentFixtureVersion; public string qualityProfile;
    }

    [Serializable]
    public sealed class EvidenceSimulation
    {
        public double fixedDeltaSeconds; public int tracePrecisionDecimals;
        public int maximumScenarioSteps; public int maximumScenarioWallClockMilliseconds;
    }

    [Serializable]
    public sealed class EvidenceScenario { public string id; public int maximumSteps; }

    [Serializable]
    public sealed class EvidenceBindings
    {
        public string technicalValidity; public string performanceCapture;
        public string manifestSchema; public int manifestVersion;
        public string reviewProtocol; public int reviewProtocolVersion;
        public bool gameplayObservationSeparated;
    }

    [Serializable]
    public sealed class EvidenceTuning
    {
        public double baseMaximumSpeed, baseAcceleration, baseBraking;
        public double baseCounterSteerBraking, baseVelocityResponseExponent;
        public int thrusterBaselineChargeCount, thrusterMaximumAdditionalCharges;
        public double thrusterRechargeSeconds, thrusterSpeedMultiplier;
        public double thrusterBurstDurationSeconds, thrusterDirectionInputThreshold;
        public double thrusterMinimumChainIntervalSeconds;
        public double thrusterSteeringDegreesPerSecond;
        public double thrusterStartupForgivenessSeconds;
        public double thrusterExitMomentumSeconds, thrusterExitSpeedRetention;
        public double thrusterExitDecayExponent;
        public double wallReflectionSpeedRetention, wallReflectionInputInfluence;
        public double wallReflectionMinimumSpeed;
        public int wallReflectionMaximumContacts;
        public double lightContactMomentumRetention, lightContactSteeringRetention;
        public double heavyContactMomentumRetention;
        public double perEnemyContactGraceSeconds, simultaneousContactWindowSeconds;
        public int contactGraceCapacity;
    }

    public sealed class MovementThrusterEvidenceFixture : IDisposable
    {
        public const string ProfilePath =
            "Assets/ShooterMover/Tests/PlayMode/Movement/Fixtures/movement-thruster-evidence-profile-v1.json";
        public const string InputPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Input/ShooterMoverMovement.inputactions";
        public const string ArenaName = "Stage1BenchmarkArena";
        public const string ExpectedProfileSha256 =
            "sha256:531356cb51ad17e108b48a1806e03de31d7035c2e32fad6613ca10db123d7677";
        public const string ExpectedTuningFingerprint =
            "sha256:72e7cba54aaf6829cd8e9806d3dca578f64fbed232089b0d352e6edf0e377d21";

        private const string ArenaType =
            "ShooterMover.TestSupport.EvidenceHarness.Stage1BenchmarkArenaFixture";
        private const string SessionType =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceSessionLifecycle";
        private const string PerformanceBudgetType =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceBudget";
        private const string PerformanceObjectBudgetType =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceObjectBudget";
        private const string PerformanceProbeType =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceProbe";
        private const string PerformanceSampleType =
            "ShooterMover.TestSupport.EvidenceHarness.EvidencePerformanceObjectCounterSample";

        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Keyboard keyboard;
        private readonly StringBuilder trace = new StringBuilder();
        private bool disposed;

        public MovementThrusterEvidenceFixture(
            Scene arena, Keyboard keyboard, MovementThrusterEvidenceProfile profile)
        {
            if (!arena.IsValid() || !arena.isLoaded) throw new ArgumentException(nameof(arena));
            this.keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Tuning = profile.BuildTuning();

            InputActionAsset imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (imported == null) throw new InvalidOperationException("MT-007 input asset missing.");
            InputActionAsset runtimeAsset = InputActionAsset.FromJson(imported.ToJson());
            owned.Add(runtimeAsset);

            GameObject actor = new GameObject("MT-012 evidence actor");
            owned.Add(actor);
            SceneManager.MoveGameObjectToScene(actor, arena);
            actor.transform.position = Find(arena, "Player Spawn").position;

            Body = actor.AddComponent<Rigidbody2D>();
            Body.gravityScale = 0f;
            Body.freezeRotation = true;
            actor.AddComponent<CircleCollider2D>().radius = 0.4f;
            Input = actor.AddComponent<PlayerMovementIntentAdapter>();
            Contact = actor.AddComponent<MovementContact2DAdapter>();
            Lifecycle = actor.AddComponent<MovementActorLifecycle>();
            if (!Lifecycle.Construct(Body, Input, runtimeAsset, Contact, Tuning)
                || !Lifecycle.StartActor())
            {
                throw new InvalidOperationException("Could not start MT-012 movement actor.");
            }

            trace.Append("schema=shooter-mover.movement-thruster-trace\nversion=1\n")
                .Append("profile_id=").Append(profile.profileId).Append('\n')
                .Append("run_seed=").Append(profile.evidenceConfiguration.runSeed).Append('\n')
                .Append("tuning_fingerprint=").Append(Tuning.Fingerprint).Append('\n')
                .Append("arena_snapshot_sha256=").Append(Sha256(CaptureArenaSnapshot())).Append('\n');
            Capture("configured");
        }

        public MovementThrusterEvidenceProfile Profile { get; }
        public MovementThrusterTuningProfile Tuning { get; }
        public Rigidbody2D Body { get; }
        public PlayerMovementIntentAdapter Input { get; }
        public MovementContact2DAdapter Contact { get; }
        public MovementActorLifecycle Lifecycle { get; }
        public int StepCount { get; private set; }
        public string Trace { get { return trace.ToString(); } }

        public static MovementThrusterEvidenceProfile LoadProfile()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ProfilePath);
            if (asset == null || Sha256(asset.bytes) != ExpectedProfileSha256)
                throw new InvalidOperationException("Frozen MT-012 profile drifted.");
            MovementThrusterEvidenceProfile profile =
                JsonUtility.FromJson<MovementThrusterEvidenceProfile>(asset.text);
            ValidateProfile(profile);
            if (profile.BuildTuning().Fingerprint != ExpectedTuningFingerprint)
                throw new InvalidOperationException("Frozen MT-012 tuning drifted.");
            return profile;
        }

        public static IEnumerator LoadArena()
        {
            AsyncOperation bootstrap = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return Wait(bootstrap, "Bootstrap");
            AsyncOperation arena = InvokeStatic(
                Resolve(ArenaType), "LoadFromCanonicalConfiguration", CanonicalConfiguration())
                as AsyncOperation;
            yield return Wait(arena, "Stage1BenchmarkArena");
            yield return null;
            string[] errors = InvokeStatic(Resolve(ArenaType), "ValidateActiveArena") as string[];
            if (errors == null || errors.Length != 0)
                throw new InvalidOperationException("Arena invalid: " + string.Join(",", errors ?? new string[0]));
        }

        public static IEnumerator UnloadArena()
        {
            Type type = Resolve(ArenaType, false);
            if (type == null) yield break;
            PropertyInfo loaded = type.GetProperty("IsLoaded", BindingFlags.Public | BindingFlags.Static);
            if (loaded == null || !(bool)loaded.GetValue(null, null)) yield break;
            yield return Wait(InvokeStatic(type, "Unload") as AsyncOperation, "arena unload");
            yield return null;
        }

        public static string CaptureArenaSnapshot()
        {
            return (string)InvokeStatic(Resolve(ArenaType), "CaptureActiveSnapshot");
        }

        public static string CaptureRestartAudit()
        {
            object session = InvokeStatic(
                Resolve(SessionType), "ConfigureFromCanonicalJson",
                CanonicalConfiguration(), "session.mt-012", "attempt.mt-012-1");
            Invoke(session, "BeginStart"); Invoke(session, "CompleteStart");
            Invoke(session, "BeginRestart", "attempt.mt-012-2");
            Invoke(session, "CompleteRestart"); Invoke(session, "BeginEnd");
            Invoke(session, "CompleteEnd", RunEndKind.Completed);
            return (string)Invoke(session, "CaptureAuditSnapshot");
        }

        public static string CapturePerformanceSummary(int observedSteps, int maximumSteps)
        {
            Type objectBudgetType = Resolve(PerformanceObjectBudgetType);
            Type budgetType = Resolve(PerformanceBudgetType);
            Type probeType = Resolve(PerformanceProbeType);
            Type sampleType = Resolve(PerformanceSampleType);
            StableId counterId = StableId.Parse("objects.movement-evidence-steps");
            Array objectBudgets = Array.CreateInstance(objectBudgetType, 1);
            objectBudgets.SetValue(
                Activator.CreateInstance(objectBudgetType, counterId, (long)maximumSteps), 0);
            object budget = Activator.CreateInstance(
                budgetType, 0d, 0.1d, 0.5d, 16, 4, 1,
                50d, 100d, 1048576L, 262144L, 1000d, 1073741824L,
                false, objectBudgets);
            object probe = Activator.CreateInstance(probeType, new object[] { budget, null });
            Invoke(probe, "Begin", 0d, "medium");
            Invoke(probe, "RecordSceneLoad", 0d, 0.01d);
            double[] times = { 0.02d, 0.04d, 0.06d, 0.08d, 0.1d };
            for (int i = 0; i < times.Length; i++)
            {
                Array samples = Array.CreateInstance(sampleType, 1);
                samples.SetValue(
                    Activator.CreateInstance(sampleType, counterId, (long)observedSteps), 0);
                if (!(bool)Invoke(probe, "RecordFrame", times[i], 16d + i, 0L, 1048576L, samples))
                    throw new InvalidOperationException("EH-007 rejected MT-012 sample.");
            }
            object summary = Invoke(probe, "Complete", 0.1d);
            string canonical = (string)Invoke(summary, "ToCanonicalString");
            if (!canonical.StartsWith("state=completed\n", StringComparison.Ordinal))
                throw new InvalidOperationException("EH-007 MT-012 capture invalid.");
            return canonical;
        }

        public void Queue(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        public ThrusterStatusSnapshot Step(string label, double seconds = -1d)
        {
            if (disposed) throw new ObjectDisposedException(GetType().Name);
            if (StepCount >= Profile.simulation.maximumScenarioSteps)
                throw new InvalidOperationException("MT-012 step bound exceeded.");
            double delta = seconds < 0d ? Profile.simulation.fixedDeltaSeconds : seconds;
            if (!Lifecycle.ExecuteFixedStep(delta))
                throw new InvalidOperationException("Movement step rejected.");
            StepCount++;
            return Capture(label);
        }

        public ThrusterStatusSnapshot Capture(string label)
        {
            ThrusterStatusSnapshot value = ThrusterStatusProjector.Project(Lifecycle.Actor, Tuning);
            trace.Append("sample|label=").Append(label)
                .Append("|step=").Append(StepCount)
                .Append("|generation=").Append(value.RuntimeGeneration)
                .Append("|state=").Append(value.State)
                .Append("|phase=").Append(value.BurstPhase)
                .Append("|charges=").Append(value.AvailableCharges).Append('/').Append(value.MaximumCharges)
                .Append("|velocity=").Append(F(value.VelocityX)).Append(',').Append(F(value.VelocityY))
                .Append("|direction=").Append(F(value.BurstDirectionX)).Append(',').Append(F(value.BurstDirectionY))
                .Append("|steering=").Append(F(value.SteeringIntentX)).Append(',').Append(F(value.SteeringIntentY))
                .Append('\n');
            return value;
        }

        public void Event(string label, object value)
        {
            trace.Append("event|label=").Append(label).Append("|value=").Append(value).Append('\n');
        }

        public void Restart()
        {
            Lifecycle.RestartActor();
            Event("restart", "generation=" + Lifecycle.Actor.Generation);
            Capture("after-restart");
        }

        public void SetFocus(bool focused)
        {
            Input.gameObject.SendMessage(
                "OnApplicationFocus", focused, SendMessageOptions.RequireReceiver);
            Event("focus", focused ? "gained" : "lost");
        }

        public Collider2D Wall(string name)
        {
            Transform wall = Find(Body.gameObject.scene, name);
            EvidenceWallContactContract contract = wall.GetComponent<EvidenceWallContactContract>();
            if (contract == null)
            {
                contract = wall.gameObject.AddComponent<EvidenceWallContactContract>();
                owned.Add(contract);
            }
            return wall.GetComponent<Collider2D>();
        }

        public Collider2D Weighted(
            string id, CombatWeightClass source, CombatWeightClass target)
        {
            GameObject item = new GameObject("MT-012 weight " + id);
            owned.Add(item);
            SceneManager.MoveGameObjectToScene(item, Body.gameObject.scene);
            BoxCollider2D collider = item.AddComponent<BoxCollider2D>();
            item.AddComponent<EvidenceWeightedContactContract>().Configure(id, source, target);
            return collider;
        }

        public MovementContact2DProcessResult Process(
            long step, Collider2D collider, Vector2 normal, double observedAt, string label)
        {
            Contact.BeginFixedStep(step);
            MovementContact2DProcessResult result =
                Contact.TryProcessContact(collider, normal, observedAt);
            Event(label, result);
            Capture(label + "-snapshot");
            return result;
        }

        public string BuildEvidence(
            string scenarioId, string technicalValidity, string reasons,
            string gameplayOutcome, string observationCode, double wallClockMilliseconds)
        {
            string performance = CapturePerformanceSummary(
                StepCount, Profile.simulation.maximumScenarioSteps);
            StringBuilder value = new StringBuilder();
            value.Append("schema=shooter-mover.movement-thruster-evidence\nversion=1\n")
                .Append("scenario_id=").Append(scenarioId).Append('\n')
                .Append("configuration_schema=").Append(Profile.evidenceConfiguration.schema).Append('\n')
                .Append("configuration_version=").Append(Profile.evidenceConfiguration.version).Append('\n')
                .Append("run_seed=").Append(Profile.evidenceConfiguration.runSeed).Append('\n')
                .Append("tuning_fingerprint=").Append(Tuning.Fingerprint).Append('\n')
                .Append("arena_snapshot_sha256=").Append(Sha256(CaptureArenaSnapshot())).Append('\n')
                .Append("session_audit_sha256=").Append(Sha256(CaptureRestartAudit())).Append('\n')
                .Append("manifest_schema=").Append(Profile.evidence.manifestSchema).Append('\n')
                .Append("manifest_version=").Append(Profile.evidence.manifestVersion).Append('\n')
                .Append("review_protocol=").Append(Profile.evidence.reviewProtocol).Append('\n')
                .Append("review_protocol_version=").Append(Profile.evidence.reviewProtocolVersion).Append('\n')
                .Append("technical_validity=").Append(technicalValidity).Append('\n')
                .Append("invalidity_reasons=").Append(reasons).Append('\n')
                .Append("gameplay_outcome=").Append(gameplayOutcome).Append('\n')
                .Append("gameplay_observation_code=").Append(observationCode).Append('\n')
                .Append("step_count=").Append(StepCount).Append('\n')
                .Append("wall_clock_milliseconds=").Append(F(wallClockMilliseconds)).Append('\n')
                .Append("within_bounds=")
                .Append(StepCount <= Profile.simulation.maximumScenarioSteps
                    && wallClockMilliseconds <= Profile.simulation.maximumScenarioWallClockMilliseconds
                    ? "true" : "false")
                .Append("\neh007_summary_begin\n").Append(performance)
                .Append("\neh007_summary_end\ntrace_sha256=").Append(Sha256(Trace))
                .Append("\ntrace_begin\n").Append(Trace).Append("trace_end\n");
            string body = value.ToString();
            return body + "payload_sha256=" + Sha256(body) + "\n";
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Lifecycle != null && !Lifecycle.IsDisposed) Lifecycle.DisposeActor();
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        public static string CanonicalConfiguration()
        {
            return "{\n"
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
        }

        public static string Sha256(string value) { return Sha256(Encoding.UTF8.GetBytes(value)); }
        public static string Sha256(byte[] value)
        {
            using (SHA256 hash = SHA256.Create())
                return "sha256:" + string.Concat(
                    hash.ComputeHash(value).Select(x => x.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void ValidateProfile(MovementThrusterEvidenceProfile profile)
        {
            string[] required =
            {
                "locomotion.acceleration-braking", "thruster.burst-chain-steering",
                "thruster.startup-forgiveness", "thruster.exit-momentum",
                "contact.wall-reflection", "contact.light-shove", "contact.heavy-block",
                "input.focus-loss", "session.restart-identity", "contact.restart-pending"
            };
            if (profile == null
                || profile.schema != "shooter-mover.movement-thruster-evidence-profile"
                || profile.version != 1 || profile.profileId != "movement-evidence.stage1-v1"
                || profile.evidenceConfiguration == null
                || profile.evidenceConfiguration.schema != "shooter-mover.evidence-run-configuration"
                || profile.evidenceConfiguration.version != 1
                || profile.evidenceConfiguration.runSeed != 104729
                || profile.simulation == null || profile.simulation.fixedDeltaSeconds <= 0d
                || profile.scenarios == null
                || !required.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(
                    profile.scenarios.Select(x => x.id).OrderBy(x => x, StringComparer.Ordinal))
                || profile.scenarios.Any(x => x.maximumSteps < 1)
                || profile.evidence == null
                || profile.evidence.technicalValidity != "cs-012-monotonic"
                || profile.evidence.performanceCapture != "eh-007-bounded-observation"
                || profile.evidence.manifestSchema != "shooter-mover.evidence-manifest"
                || profile.evidence.manifestVersion != 1
                || profile.evidence.reviewProtocol != "shooter-mover.stage1-evidence-protocol"
                || profile.evidence.reviewProtocolVersion != 1
                || !profile.evidence.gameplayObservationSeparated)
                throw new InvalidOperationException("Unsupported or incomplete MT-012 profile.");
        }

        private static IEnumerator Wait(AsyncOperation operation, string name)
        {
            if (operation == null) throw new InvalidOperationException(name + " operation missing.");
            float start = Time.realtimeSinceStartup;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - start > 30f)
                    throw new TimeoutException(name + " timed out.");
                yield return null;
            }
        }

        private static Transform Find(Scene scene, string name)
        {
            Transform result = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(x => x.name == name);
            if (result == null) throw new InvalidOperationException("Arena object missing: " + name);
            return result;
        }

        private static Type Resolve(string name, bool required = true)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(x => x.GetType(name, false)).FirstOrDefault(x => x != null);
            if (type == null && required) throw new InvalidOperationException("Type missing: " + name);
            return type;
        }

        private static object InvokeStatic(Type type, string name, params object[] args)
        {
            return InvokeMethod(RequireMethod(type, name, BindingFlags.Public | BindingFlags.Static, args), null, args);
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            return InvokeMethod(RequireMethod(target.GetType(), name, BindingFlags.Public | BindingFlags.Instance, args), target, args);
        }

        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags, object[] args)
        {
            foreach (MethodInfo method in type.GetMethods(flags).Where(x => x.Name == name))
            {
                ParameterInfo[] p = method.GetParameters();
                if (p.Length != args.Length) continue;
                bool match = true;
                for (int i = 0; i < args.Length; i++)
                    if (args[i] != null && !p[i].ParameterType.IsInstanceOfType(args[i])) match = false;
                if (match) return method;
            }
            throw new MissingMethodException(type.FullName, name);
        }

        private static object InvokeMethod(MethodInfo method, object target, object[] args)
        {
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException error)
            {
                ExceptionDispatchInfo.Capture(error.InnerException ?? error).Throw();
                throw;
            }
        }

        private static string F(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed class EvidenceWallContactContract : MonoBehaviour, IMovementContact2DContract
    {
        public bool TryDescribeMovementContact(out MovementContact2DDescriptor descriptor)
        {
            descriptor = MovementContact2DDescriptor.Wall(); return true;
        }
    }

    public sealed class EvidenceWeightedContactContract : MonoBehaviour, IMovementContact2DContract
    {
        private MovementContact2DDescriptor descriptor;
        public void Configure(string id, CombatWeightClass source, CombatWeightClass target)
        {
            StableId targetId = StableId.Parse("contact-probe." + id);
            descriptor = MovementContact2DDescriptor.Enemy(
                targetId,
                new WeightMessage(
                    StableId.Parse("weight-event." + id),
                    StableId.Parse("movement-source.mt-012"),
                    targetId, CombatChannel.Contact, source, target,
                    WeightMessage.DetermineResult(source, target)));
        }
        public bool TryDescribeMovementContact(out MovementContact2DDescriptor value)
        {
            value = descriptor; return value != null;
        }
    }
}
#endif

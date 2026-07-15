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
using ShooterMover.Application.Combat;
using ShooterMover.Contracts.Diagnostics;
using ShooterMover.Contracts.Input;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Input;
using ShooterMover.UnityAdapters.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace ShooterMover.Tests.PlayMode.Combat
{
    [Serializable]
    public sealed class FourMountCombatEvidenceProfile
    {
        public string schema;
        public int version;
        public string profileId;
        public CombatEvidenceConfigurationBinding evidenceConfiguration;
        public CombatEvidenceSimulation simulation;
        public CombatEvidenceTuning combat;
        public CombatEvidenceScenario[] scenarios;
        public CombatEvidenceBindings evidence;

        public WeaponRuntimeProfile[] BuildRuntimeProfiles()
        {
            WeaponRuntimeProfile[] profiles =
                new WeaponRuntimeProfile[FourMountCombatState.MountCount];
            StableId moduleId = FourMountCombatEvidenceFixture.EvidenceModuleId;
            for (int index = 0; index < profiles.Length; index++)
            {
                profiles[index] = WeaponRuntimeProfile.Create(
                    WeaponRuntimeProfile.CurrentProfileVersion,
                    StableId.Parse("weapon-profile.cb011-slot-" + (index + 1)),
                    combat.cadenceSeconds[index],
                    1,
                    0d,
                    0d,
                    WeaponCycleMode.None,
                    0d,
                    0d,
                    0d,
                    0d,
                    true,
                    combat.powerCapacityUnits,
                    combat.empoweredCostUnits,
                    0d,
                    new[] { moduleId },
                    new[] { moduleId },
                    index);
            }

            return profiles;
        }
    }

    [Serializable]
    public sealed class CombatEvidenceConfigurationBinding
    {
        public string schema;
        public int version;
        public int runSeed;
        public int intentFixtureVersion;
        public string qualityProfile;
    }

    [Serializable]
    public sealed class CombatEvidenceSimulation
    {
        public double fixedDeltaSeconds;
        public int tracePrecisionDecimals;
        public int maximumScenarioSteps;
        public int maximumScenarioWallClockMilliseconds;
    }

    [Serializable]
    public sealed class CombatEvidenceTuning
    {
        public double[] cadenceSeconds;
        public double[] initialPowerUnits;
        public double powerCapacityUnits;
        public double empoweredCostUnits;
        public double sharedAimX;
        public double sharedAimY;
    }

    [Serializable]
    public sealed class CombatEvidenceScenario
    {
        public string id;
        public int maximumSteps;
    }

    [Serializable]
    public sealed class CombatEvidenceBindings
    {
        public string technicalValidity;
        public string performanceCapture;
        public string manifestSchema;
        public int manifestVersion;
        public string reviewProtocol;
        public int reviewProtocolVersion;
        public bool gameplayObservationSeparated;
        public string[] incompleteClaims;
    }

    public sealed class FourMountCombatEvidenceFixture : IDisposable
    {
        public const string ProfilePath =
            "Assets/ShooterMover/Tests/PlayMode/Combat/Fixtures/four-mount-combat-foundation-profile-v1.json";
        public const string CombatInputPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Input/ShooterMoverCombat.inputactions";
        public const string MovementInputPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Input/ShooterMoverMovement.inputactions";
        public const string ArenaName = "Stage1BenchmarkArena";
        public const string ExpectedProfileSha256 =
            "sha256:6461028c1bce88dbf6006b845fbdcaf2ed282ef01cf5f472bdb905d77bea331d";

        internal static readonly StableId EvidenceModuleId =
            StableId.Parse("weapon-module.cb011-evidence-only");

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
        private readonly Mouse mouse;
        private readonly StringBuilder trace = new StringBuilder();
        private readonly FourMountCombatStepper stepper;
        private readonly FourMountStatusProjector statusProjector = new FourMountStatusProjector();
        private readonly WeaponMount2DAdapter[] mountAdapters =
            new WeaponMount2DAdapter[FourMountCombatState.MountCount];
        private readonly StableId[] weaponIds = new StableId[FourMountCombatState.MountCount];
        private readonly StableId[] mountIds = new StableId[FourMountCombatState.MountCount];
        private readonly WeaponMountOrigin[] origins =
            new WeaponMountOrigin[FourMountCombatState.MountCount];
        private bool disposed;

        public FourMountCombatEvidenceFixture(
            Scene arena,
            Keyboard keyboard,
            Mouse mouse,
            FourMountCombatEvidenceProfile profile)
        {
            if (!arena.IsValid() || !arena.isLoaded) throw new ArgumentException(nameof(arena));
            this.keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
            this.mouse = mouse ?? throw new ArgumentNullException(nameof(mouse));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Profiles = profile.BuildRuntimeProfiles();

            for (int index = 0; index < FourMountCombatState.MountCount; index++)
            {
                weaponIds[index] = StableId.Parse("weapon.cb011-slot-" + (index + 1));
                mountIds[index] = StableId.Parse("mount.cb011-slot-" + (index + 1));
                origins[index] = new WeaponMountOrigin(
                    index + 1,
                    new AimVector2(index - 1.5d, 0d));
            }

            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[] { new EvidenceOnlyModule() });
            stepper = new FourMountCombatStepper(new FourMountAimResolver(), pipeline);
            ResetCombatState();

            InputActionAsset combatActions = CloneInputAsset(CombatInputPath, "CB-008");
            InputActionAsset movementActions = CloneInputAsset(MovementInputPath, "MT-007");

            GameObject actor = new GameObject("CB-011 four-mount evidence actor");
            owned.Add(actor);
            SceneManager.MoveGameObjectToScene(actor, arena);
            actor.transform.position = Find(arena, "Player Spawn").position;

            Body = actor.AddComponent<Rigidbody2D>();
            Body.gravityScale = 0f;
            Body.freezeRotation = true;
            PlayerCollider = actor.AddComponent<CircleCollider2D>();
            PlayerCollider.radius = 0.4f;

            PlayerMovementIntentAdapter movementInput =
                actor.AddComponent<PlayerMovementIntentAdapter>();
            MovementContact2DAdapter movementContact =
                actor.AddComponent<MovementContact2DAdapter>();
            MovementLifecycle = actor.AddComponent<MovementActorLifecycle>();
            if (!MovementLifecycle.Construct(
                    Body,
                    movementInput,
                    movementActions,
                    movementContact,
                    BuildMovementTuning())
                || !MovementLifecycle.StartActor())
            {
                throw new InvalidOperationException("Could not start MT-010 movement authority.");
            }

            CombatInput = actor.AddComponent<PlayerCombatIntentAdapter>();
            CombatInput.Configure(combatActions);

            for (int index = 0; index < mountAdapters.Length; index++)
            {
                GameObject mount = new GameObject("CB-011 mount adapter " + (index + 1));
                owned.Add(mount);
                SceneManager.MoveGameObjectToScene(mount, arena);
                mount.transform.SetParent(actor.transform, false);
                mountAdapters[index] = mount.AddComponent<WeaponMount2DAdapter>();
                ConfigureMountAdapter(index);
            }

            Queue(Vector2.zero);
            if (!MovementLifecycle.ExecuteFixedStep(profile.simulation.fixedDeltaSeconds))
                throw new InvalidOperationException("Movement authority did not accept initial step.");

            FrozenInitialState = CaptureFrozenState();
            trace.Append("schema=shooter-mover.four-mount-combat-trace\nversion=1\n")
                .Append("profile_id=").Append(profile.profileId).Append('\n')
                .Append("profile_sha256=").Append(ExpectedProfileSha256).Append('\n')
                .Append("run_seed=").Append(profile.evidenceConfiguration.runSeed).Append('\n')
                .Append("arena_snapshot_sha256=").Append(Sha256(CaptureArenaSnapshot())).Append('\n');
            Capture("configured", null);
        }

        public FourMountCombatEvidenceProfile Profile { get; }
        public WeaponRuntimeProfile[] Profiles { get; }
        public FourMountCombatState State { get; private set; }
        public PlayerCombatIntentAdapter CombatInput { get; }
        public MovementActorLifecycle MovementLifecycle { get; }
        public Rigidbody2D Body { get; }
        public CircleCollider2D PlayerCollider { get; }
        public int StepCount { get; private set; }
        public int RestartCount { get; private set; }
        public string FrozenInitialState { get; }
        public string Trace => trace.ToString();

        public static FourMountCombatEvidenceProfile LoadProfile()
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ProfilePath);
            if (asset == null || Sha256(asset.bytes) != ExpectedProfileSha256)
                throw new InvalidOperationException("Frozen CB-011 profile drifted.");
            FourMountCombatEvidenceProfile profile =
                JsonUtility.FromJson<FourMountCombatEvidenceProfile>(asset.text);
            ValidateProfile(profile);
            return profile;
        }

        public static IEnumerator LoadArena()
        {
            AsyncOperation bootstrap = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return Wait(bootstrap, "Bootstrap");
            AsyncOperation arena = InvokeStatic(
                Resolve(ArenaType),
                "LoadFromCanonicalConfiguration",
                CanonicalConfiguration()) as AsyncOperation;
            yield return Wait(arena, ArenaName);
            yield return null;
            string[] errors = InvokeStatic(Resolve(ArenaType), "ValidateActiveArena") as string[];
            if (errors == null || errors.Length != 0)
                throw new InvalidOperationException(
                    "Arena invalid: " + string.Join(",", errors ?? new string[0]));
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
                Resolve(SessionType),
                "ConfigureFromCanonicalJson",
                CanonicalConfiguration(),
                "session.cb-011",
                "attempt.cb-011-1");
            Invoke(session, "BeginStart");
            Invoke(session, "CompleteStart");
            Invoke(session, "BeginRestart", "attempt.cb-011-2");
            Invoke(session, "CompleteRestart");
            Invoke(session, "BeginEnd");
            Invoke(session, "CompleteEnd", RunEndKind.Completed);
            return (string)Invoke(session, "CaptureAuditSnapshot");
        }

        public static string CapturePerformanceSummary(int observedSteps, int maximumSteps)
        {
            Type objectBudgetType = Resolve(PerformanceObjectBudgetType);
            Type budgetType = Resolve(PerformanceBudgetType);
            Type probeType = Resolve(PerformanceProbeType);
            Type sampleType = Resolve(PerformanceSampleType);
            StableId counterId = StableId.Parse("objects.combat-evidence-steps");
            Array objectBudgets = Array.CreateInstance(objectBudgetType, 1);
            objectBudgets.SetValue(
                Activator.CreateInstance(objectBudgetType, counterId, (long)maximumSteps),
                0);
            object budget = Activator.CreateInstance(
                budgetType,
                0d,
                0.1d,
                0.5d,
                16,
                4,
                1,
                50d,
                100d,
                1048576L,
                262144L,
                1000d,
                1073741824L,
                false,
                objectBudgets);
            object probe = Activator.CreateInstance(probeType, new object[] { budget, null });
            Invoke(probe, "Begin", 0d, "medium");
            Invoke(probe, "RecordSceneLoad", 0d, 0.01d);
            double[] times = { 0.02d, 0.04d, 0.06d, 0.08d, 0.1d };
            for (int index = 0; index < times.Length; index++)
            {
                Array samples = Array.CreateInstance(sampleType, 1);
                samples.SetValue(
                    Activator.CreateInstance(sampleType, counterId, (long)observedSteps),
                    0);
                if (!(bool)Invoke(
                        probe,
                        "RecordFrame",
                        times[index],
                        16d + index,
                        0L,
                        1048576L,
                        samples))
                {
                    throw new InvalidOperationException("EH-007 rejected CB-011 sample.");
                }
            }

            object summary = Invoke(probe, "Complete", 0.1d);
            string canonical = (string)Invoke(summary, "ToCanonicalString");
            if (!canonical.StartsWith("state=completed\n", StringComparison.Ordinal))
                throw new InvalidOperationException("EH-007 CB-011 capture invalid.");
            return canonical;
        }

        public void Queue(Vector2 aim, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.QueueDeltaStateEvent(mouse.delta, aim);
            InputSystem.Update();
        }

        public PlayerIntentFrame ReadCombatIntent()
        {
            return CombatInput.ReadIntentFrame();
        }

        public FourMountCombatStepResult Step(
            string label,
            double elapsedSeconds = -1d,
            string[] faults = null)
        {
            if (disposed) throw new ObjectDisposedException(GetType().Name);
            if (StepCount >= Profile.simulation.maximumScenarioSteps)
                throw new InvalidOperationException("CB-011 step bound exceeded.");

            PlayerIntentFrame intent = CombatInput.ReadIntentFrame();
            double elapsed = elapsedSeconds < 0d
                ? Profile.simulation.fixedDeltaSeconds
                : elapsedSeconds;
            bool fire = intent.Fire.IsHeld || intent.Fire.WasPressed;
            bool power = intent.PowerModifier.IsHeld || intent.PowerModifier.WasPressed;
            FourMountCombatStepInput input = new FourMountCombatStepInput(
                StepCount,
                elapsed,
                fire,
                power,
                new AimVector2(intent.Aim.X, intent.Aim.Y),
                new AimVector2(20d, 10d),
                Profiles,
                weaponIds,
                mountIds,
                origins,
                faults);
            FourMountCombatStepResult result = stepper.Step(State, input);
            State = result.State;
            StepCount++;
            Capture(label, result);
            return result;
        }

        public WeaponMount2DExecutionStatus RestartRejectingStalePlan(
            WeaponFireExecutionPlan stalePlan)
        {
            for (int index = 0; index < mountAdapters.Length; index++)
                mountAdapters[index].ClearConfiguration();

            WeaponMount2DExecutionStatus staleStatus = stalePlan == null
                ? WeaponMount2DExecutionStatus.InvalidPlan
                : mountAdapters[0].ExecutePlan(stalePlan).Status;

            CombatInput.enabled = false;
            CombatInput.enabled = true;
            MovementLifecycle.RestartActor();
            ResetCombatState();
            RestartCount++;

            for (int index = 0; index < mountAdapters.Length; index++)
                ConfigureMountAdapter(index);

            Queue(Vector2.zero);
            if (!MovementLifecycle.ExecuteFixedStep(Profile.simulation.fixedDeltaSeconds))
                throw new InvalidOperationException("Movement restart step rejected.");
            Event("restart", "count=" + RestartCount + ";stale_plan=" + staleStatus);
            Capture("after-restart", null);
            return staleStatus;
        }

        public void SetFocus(bool focused)
        {
            CombatInput.gameObject.SendMessage(
                "OnApplicationFocus",
                focused,
                SendMessageOptions.RequireReceiver);
            Event("focus", focused ? "gained" : "lost");
        }

        public string CaptureFrozenState()
        {
            return "combat=" + State.ToTraceString() + "\n" + CaptureMovementAuthority();
        }

        public string CaptureMovementAuthority()
        {
            MovementContactStateSnapshot contactSnapshot;
            bool contactReady = MovementLifecycle.Actor.TryReadContactSnapshot(out contactSnapshot);
            return "movement_velocity=" + F(MovementLifecycle.Actor.CurrentVelocityX)
                + "," + F(MovementLifecycle.Actor.CurrentVelocityY)
                + "\nmovement_phase=" + MovementLifecycle.Actor.CurrentPhase
                + "\nthruster_charges=" + MovementLifecycle.Actor.AvailableThrusterCharges
                + "/" + MovementLifecycle.Actor.MaximumThrusterCharges
                + "\nbody_velocity=" + F(Body.linearVelocity.x) + "," + F(Body.linearVelocity.y)
                + "\ncontact_authority_ready=" + (contactReady ? "true" : "false")
                + "\nplayer_collider_enabled=" + (PlayerCollider.enabled ? "true" : "false")
                + "\n";
        }

        public void Event(string label, object value)
        {
            trace.Append("event|label=").Append(label).Append("|value=").Append(value).Append('\n');
        }

        public string BuildEvidence(
            string scenarioId,
            string technicalValidity,
            string reasons,
            string gameplayOutcome,
            string observationCode,
            double wallClockMilliseconds)
        {
            string performance = CapturePerformanceSummary(
                StepCount,
                Profile.simulation.maximumScenarioSteps);
            string diagnostics = statusProjector.Project(State, Profiles, weaponIds).ToTraceString();
            StringBuilder value = new StringBuilder();
            value.Append("schema=shooter-mover.four-mount-combat-evidence\nversion=1\n")
                .Append("scenario_id=").Append(scenarioId).Append('\n')
                .Append("profile_id=").Append(Profile.profileId).Append('\n')
                .Append("profile_sha256=").Append(ExpectedProfileSha256).Append('\n')
                .Append("configuration_schema=").Append(Profile.evidenceConfiguration.schema).Append('\n')
                .Append("configuration_version=").Append(Profile.evidenceConfiguration.version).Append('\n')
                .Append("run_seed=").Append(Profile.evidenceConfiguration.runSeed).Append('\n')
                .Append("arena_snapshot_sha256=").Append(Sha256(CaptureArenaSnapshot())).Append('\n')
                .Append("session_audit_sha256=").Append(Sha256(CaptureRestartAudit())).Append('\n')
                .Append("manifest_schema=").Append(Profile.evidence.manifestSchema).Append('\n')
                .Append("manifest_version=").Append(Profile.evidence.manifestVersion).Append('\n')
                .Append("eh008_manifest_identity_binding=profile-sha256\n")
                .Append("review_protocol=").Append(Profile.evidence.reviewProtocol).Append('\n')
                .Append("review_protocol_version=").Append(Profile.evidence.reviewProtocolVersion).Append('\n')
                .Append("eh010_review_protocol_binding=required-human-review\n")
                .Append("gameplay_observation_separated=true\n")
                .Append("technical_validity=").Append(technicalValidity).Append('\n')
                .Append("technical_invalid_requires_rerun=")
                .Append(technicalValidity == "invalid" ? "true" : "false").Append('\n')
                .Append("invalidity_reasons=").Append(reasons).Append('\n')
                .Append("gameplay_outcome=").Append(gameplayOutcome).Append('\n')
                .Append("gameplay_observation_code=").Append(observationCode).Append('\n')
                .Append("formal_manifested_package_proof=not-executed\n")
                .Append("windows_player_proof=not-executed\n")
                .Append("human_playable_review=not-executed\n")
                .Append("five_stage1_weapon_packages=not-complete\n")
                .Append("final_audiovisual_identity=not-complete\n")
                .Append("hud_visuals=not-complete\n")
                .Append("encounter_balance=not-complete\n")
                .Append("step_count=").Append(StepCount).Append('\n')
                .Append("restart_count=").Append(RestartCount).Append('\n')
                .Append("wall_clock_milliseconds=").Append(F(wallClockMilliseconds)).Append('\n')
                .Append("within_bounds=")
                .Append(StepCount <= Profile.simulation.maximumScenarioSteps
                    && wallClockMilliseconds <= Profile.simulation.maximumScenarioWallClockMilliseconds
                    ? "true" : "false")
                .Append("\ndiagnostics_sha256=").Append(Sha256(diagnostics))
                .Append("\ndiagnostics_begin\n").Append(diagnostics)
                .Append("\ndiagnostics_end\n")
                .Append("eh007_summary_begin\n").Append(performance)
                .Append("\neh007_summary_end\ntrace_sha256=").Append(Sha256(Trace))
                .Append("\ntrace_begin\n").Append(Trace).Append("trace_end\n");
            string body = value.ToString();
            return body + "payload_sha256=" + Sha256(body) + "\n";
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (MovementLifecycle != null && !MovementLifecycle.IsDisposed)
                MovementLifecycle.DisposeActor();
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null) UnityEngine.Object.DestroyImmediate(owned[index]);
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

        public static string Sha256(string value)
        {
            return Sha256(Encoding.UTF8.GetBytes(value));
        }

        public static string Sha256(byte[] value)
        {
            using (SHA256 hash = SHA256.Create())
                return "sha256:" + string.Concat(
                    hash.ComputeHash(value).Select(x =>
                        x.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private void Capture(string label, FourMountCombatStepResult latest)
        {
            FourMountStatusSnapshot status =
                statusProjector.Project(State, Profiles, weaponIds, latest);
            trace.Append("sample|label=").Append(label)
                .Append("|step=").Append(StepCount)
                .Append("|restart=").Append(RestartCount)
                .Append("|intent_accepting=").Append(CombatInput.IsAcceptingInput ? "true" : "false")
                .Append("|timeline=").Append(latest == null ? "none" : latest.ToTimelineRow())
                .Append('\n')
                .Append(status.ToTraceString()).Append('\n')
                .Append("movement|").Append(CaptureMovementAuthority().Replace('\n', '|')).Append('\n');
        }

        private void ResetCombatState()
        {
            State = FourMountCombatState.Initial(
                Profiles,
                (double[])Profile.combat.initialPowerUnits.Clone());
            StepCount = 0;
        }

        private void ConfigureMountAdapter(int index)
        {
            mountAdapters[index].Configure(
                StableId.Parse("player.cb011"),
                weaponIds[index],
                mountIds[index],
                new IWeaponFireExecutionOperation2DHandler[0]);
        }

        private InputActionAsset CloneInputAsset(string path, string owner)
        {
            InputActionAsset imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (imported == null)
                throw new InvalidOperationException(owner + " input asset missing.");
            InputActionAsset runtime = InputActionAsset.FromJson(imported.ToJson());
            owned.Add(runtime);
            return runtime;
        }

        private static MovementThrusterTuningProfile BuildMovementTuning()
        {
            return MovementThrusterTuningProfile.Create(
                1,
                StableId.Parse("movement-tuning.cb011-no-recoil"),
                12d,
                50d,
                60d,
                90d,
                1.25d,
                2,
                1,
                1.75d,
                2.5d,
                0.3d,
                0.1d,
                0.05d,
                120d,
                0.04d,
                0.2d,
                0.75d,
                2d,
                0.8d,
                0.15d,
                5d,
                4,
                0.8d,
                0.9d,
                0.1d,
                0.5d,
                0.02d,
                128);
        }

        private static void ValidateProfile(FourMountCombatEvidenceProfile profile)
        {
            string[] required =
            {
                "shared-intent.exactly-four-mounts",
                "cadence.mixed-simultaneous-readiness",
                "power.independent-depletion-fallback",
                "fault.single-mount-isolation",
                "recoil.no-player-motion-authority",
                "input.focus-loss-clears-held",
                "session.rapid-restart-frozen-state",
                "validity.valid-invalid-separation",
                "evidence.eh007-eh008-eh010-bindings",
                "determinism.byte-identical-bounded-trace",
                "scope.incomplete-stage1-claims"
            };
            string[] incomplete =
            {
                "encounter-balance-not-complete",
                "final-audiovisual-identity-not-complete",
                "five-stage-1-weapon-packages-not-complete",
                "hud-visuals-not-complete"
            };
            if (profile == null
                || profile.schema != "shooter-mover.four-mount-combat-evidence-profile"
                || profile.version != 1
                || profile.profileId != "combat-foundation.stage1-v1"
                || profile.evidenceConfiguration == null
                || profile.evidenceConfiguration.schema != "shooter-mover.evidence-run-configuration"
                || profile.evidenceConfiguration.version != 1
                || profile.evidenceConfiguration.runSeed != 104729
                || profile.simulation == null
                || profile.simulation.fixedDeltaSeconds <= 0d
                || profile.simulation.maximumScenarioSteps < 1
                || profile.combat == null
                || profile.combat.cadenceSeconds == null
                || profile.combat.cadenceSeconds.Length != FourMountCombatState.MountCount
                || profile.combat.cadenceSeconds.Any(x => x <= 0d)
                || profile.combat.initialPowerUnits == null
                || profile.combat.initialPowerUnits.Length != FourMountCombatState.MountCount
                || profile.combat.initialPowerUnits.Any(x => x < 0d)
                || profile.combat.powerCapacityUnits <= 0d
                || profile.combat.empoweredCostUnits <= 0d
                || profile.scenarios == null
                || !required.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(
                    profile.scenarios.Select(x => x.id)
                        .OrderBy(x => x, StringComparer.Ordinal))
                || profile.scenarios.Any(x => x.maximumSteps < 1)
                || profile.evidence == null
                || profile.evidence.technicalValidity != "cs-012-monotonic"
                || profile.evidence.performanceCapture != "eh-007-bounded-observation"
                || profile.evidence.manifestSchema != "shooter-mover.evidence-manifest"
                || profile.evidence.manifestVersion != 1
                || profile.evidence.reviewProtocol != "shooter-mover.stage1-evidence-protocol"
                || profile.evidence.reviewProtocolVersion != 1
                || !profile.evidence.gameplayObservationSeparated
                || profile.evidence.incompleteClaims == null
                || !incomplete.SequenceEqual(
                    profile.evidence.incompleteClaims.OrderBy(x => x, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException("Unsupported or incomplete CB-011 profile.");
            }
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
            return InvokeMethod(
                RequireMethod(type, name, BindingFlags.Public | BindingFlags.Static, args),
                null,
                args);
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            return InvokeMethod(
                RequireMethod(target.GetType(), name, BindingFlags.Public | BindingFlags.Instance, args),
                target,
                args);
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            BindingFlags flags,
            object[] args)
        {
            foreach (MethodInfo method in type.GetMethods(flags).Where(x => x.Name == name))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length) continue;
                bool match = true;
                for (int index = 0; index < args.Length; index++)
                    if (args[index] != null
                        && !parameters[index].ParameterType.IsInstanceOfType(args[index]))
                        match = false;
                if (match) return method;
            }

            throw new MissingMethodException(type.FullName, name);
        }

        private static object InvokeMethod(MethodInfo method, object target, object[] args)
        {
            try
            {
                return method.Invoke(target, args);
            }
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

        private sealed class EvidenceOnlyModule : IWeaponBehaviorModule
        {
            public StableId ModuleId => EvidenceModuleId;

            public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
            {
                return new WeaponBehaviorModulePlan(ModuleId);
            }
        }
    }
}
#endif

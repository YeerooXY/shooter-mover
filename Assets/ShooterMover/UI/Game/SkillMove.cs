using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.GameplayEntities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    internal static class SkillMoveInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstall(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryInstall(scene);
        }

        private static void TryInstall(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(
                    scene.path,
                    PlayableLevelCatalog.PlayableLevelScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            LevelGame level = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponentInChildren<SkillMoveSetup>(true) != null)
                {
                    return;
                }

                LevelGame candidate = roots[index]
                    .GetComponentInChildren<LevelGame>(true);
                if (candidate == null) continue;
                if (level != null && !ReferenceEquals(level, candidate))
                {
                    Debug.LogError("skill-move-level-duplicated", candidate);
                    return;
                }
                level = candidate;
            }

            if (level != null)
            {
                level.gameObject.AddComponent<SkillMoveSetup>();
            }
        }
    }

    [DefaultExecutionOrder(650)]
    [DisallowMultipleComponent]
    public sealed class SkillMoveSetup : MonoBehaviour
    {
        private const int BindAttemptLimit = 240;

        private int attempts;
        private bool bound;

        private void Start()
        {
            attempts = BindAttemptLimit;
            TryBind();
        }

        private void Update()
        {
            if (bound) return;
            if (TryBind()) return;

            attempts--;
            if (attempts <= 0)
            {
                Debug.LogError("skill-move-player-binding-timeout", this);
                enabled = false;
            }
        }

        private bool TryBind()
        {
            PlayerMarker player = FindPlayer();
            if (player == null) return false;

            CharacterLiveGraph graph;
            FlowProfileRecord profile;
            if (!CharacterSave.TryResolveCurrent(out graph, out profile)
                || graph == null
                || graph.IsDisposed
                || graph.Character == null
                || graph.Character.CharacterInstanceStableId
                    != player.CharacterInstanceStableId)
            {
                return false;
            }

            RankedSkillAllocationSnapshot allocation;
            if (!graph.SkillAuthority.TryGet(
                    graph.SkillProfileId,
                    out allocation)
                || allocation == null)
            {
                Debug.LogError("skill-move-allocation-missing", this);
                enabled = false;
                return false;
            }

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            TopDownMovement movement = player.GetComponent<TopDownMovement>();
            PlayerHUD health = player.GetComponent<PlayerHUD>();
            if (body == null
                || movement == null
                || health == null
                || !health.IsBound)
            {
                return false;
            }

            SkillMove skills = player.GetComponent<SkillMove>()
                ?? player.gameObject.AddComponent<SkillMove>();
            skills.Bind(body, movement, allocation);

            SkillArmor armor = player.GetComponent<SkillArmor>()
                ?? player.gameObject.AddComponent<SkillArmor>();
            armor.Bind(health, skills.ArmorReduction);

            if (MedicHeal.IsMedic(allocation.ClassId))
            {
                MedicHeal medicHeal = player.GetComponent<MedicHeal>()
                    ?? player.gameObject.AddComponent<MedicHeal>();
                medicHeal.Bind(health, allocation);
            }

            bound = true;
            enabled = false;
            return true;
        }

        private PlayerMarker FindPlayer()
        {
            PlayerMarker found = null;
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                PlayerMarker[] players = roots[rootIndex]
                    .GetComponentsInChildren<PlayerMarker>(true);
                for (int index = 0; index < players.Length; index++)
                {
                    PlayerMarker candidate = players[index];
                    if (candidate == null
                        || !candidate.isActiveAndEnabled
                        || candidate.gameObject.scene != gameObject.scene)
                    {
                        continue;
                    }
                    if (found != null && !ReferenceEquals(found, candidate))
                    {
                        Debug.LogError("skill-move-player-duplicated", this);
                        enabled = false;
                        return null;
                    }
                    found = candidate;
                }
            }
            return found;
        }
    }

    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class SkillMove : MonoBehaviour
    {
        private const string ArmorId = "generic.armor";
        private const string SpeedId = "generic.movement_speed";
        private const string RecoveryId = "striker.thruster_recovery";
        private const string EfficiencyId = "striker.movement_efficiency";

        private const float BaseDashSpeed = 18f;
        private const float BaseDashSeconds = 0.18f;
        private const float BaseRechargeSeconds = 2f;

        private readonly List<float> rechargeEnds = new List<float>();

        private Rigidbody2D body;
        private TopDownMovement movement;
        private Vector2 lastDirection = Vector2.up;
        private Vector2 dashDirection;
        private float speedScale = 1f;
        private float dashSpeed = BaseDashSpeed;
        private float dashSeconds = BaseDashSeconds;
        private float rechargeSeconds = BaseRechargeSeconds;
        private float armorReduction;
        private float dashEnds;
        private int armorRank;
        private int speedRank;
        private int recoveryRank;
        private int efficiencyRank;
        private int maximumCharges;
        private bool dashing;
        private bool bound;

        public int MaximumCharges { get { return maximumCharges; } }
        public int AvailableCharges
        {
            get { return Mathf.Max(0, maximumCharges - rechargeEnds.Count); }
        }
        public float RechargeSeconds { get { return rechargeSeconds; } }
        public float ArmorReduction { get { return armorReduction; } }
        public bool IsDashing { get { return dashing; } }

        public void Bind(
            Rigidbody2D configuredBody,
            TopDownMovement configuredMovement,
            RankedSkillAllocationSnapshot allocation)
        {
            if (bound)
            {
                throw new InvalidOperationException("skill-move-duplicate-binding");
            }
            body = configuredBody
                ?? throw new ArgumentNullException(nameof(configuredBody));
            movement = configuredMovement
                ?? throw new ArgumentNullException(nameof(configuredMovement));
            if (allocation == null)
            {
                throw new ArgumentNullException(nameof(allocation));
            }

            armorRank = allocation.RankOf(ArmorId);
            speedRank = allocation.RankOf(SpeedId);
            recoveryRank = allocation.RankOf(RecoveryId);
            efficiencyRank = allocation.RankOf(EfficiencyId);

            armorReduction = Mathf.Clamp(armorRank * 0.01f, 0f, 0.9f);
            speedScale = 1f + speedRank * 0.01f;
            dashSpeed = BaseDashSpeed * speedScale;
            dashSeconds = BaseDashSeconds * (1f + efficiencyRank * 0.01f);

            float recovery = 1f + recoveryRank * 0.01f;
            float milestone = recoveryRank >= 5 ? -0.1f : 0f;
            rechargeSeconds = Mathf.Max(
                0.25f,
                BaseRechargeSeconds / recovery + milestone);

            bool striker = IsClass(allocation.ClassId, "striker")
                || IsClass(allocation.ClassId, "aggressive");
            maximumCharges = BaseChargeCount(allocation.ClassId);
            if (striker && recoveryRank >= 8 && efficiencyRank >= 8)
            {
                maximumCharges++;
            }
            bound = true;
        }

        private void Update()
        {
            if (!bound || body == null || movement == null) return;

            float now = Time.time;
            RefreshCharges(now);
            if (dashing && now >= dashEnds)
            {
                dashing = false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || maximumCharges <= 0) return;

            Vector2 direction = ReadDirection(keyboard);
            if (direction.sqrMagnitude > 0.001f)
            {
                lastDirection = direction.normalized;
            }

            bool pressed = keyboard.spaceKey.wasPressedThisFrame
                || keyboard.leftShiftKey.wasPressedThisFrame
                || keyboard.rightShiftKey.wasPressedThisFrame;
            if (pressed && !dashing && AvailableCharges > 0)
            {
                dashDirection = direction.sqrMagnitude > 0.001f
                    ? direction.normalized
                    : lastDirection;
                rechargeEnds.Add(now + rechargeSeconds);
                dashEnds = now + dashSeconds;
                dashing = true;
            }
        }

        private void FixedUpdate()
        {
            if (!bound || body == null) return;
            if (dashing)
            {
                body.linearVelocity = dashDirection * dashSpeed;
                return;
            }

            body.linearVelocity *= speedScale;
        }

        private void OnGUI()
        {
            if (!bound) return;
            string text;
            if (maximumCharges <= 0)
            {
                text = "THRUSTER DISABLED";
            }
            else
            {
                text = "THRUSTER " + AvailableCharges + "/" + maximumCharges;
                float next = NextRechargeSeconds();
                if (next > 0f)
                {
                    text += "  " + next.ToString("0.0") + "s";
                }
                text += "  [SHIFT / SPACE]";
            }
            GUI.Label(new Rect(16f, Screen.height - 34f, 330f, 24f), text);

            string ranks = "SKILLS  Armor " + armorRank
                + "  Speed " + speedRank
                + "  Recovery " + recoveryRank
                + "  Efficiency " + efficiencyRank;
            GUI.Label(new Rect(16f, Screen.height - 56f, 430f, 24f), ranks);
        }

        private void RefreshCharges(float now)
        {
            for (int index = rechargeEnds.Count - 1; index >= 0; index--)
            {
                if (now >= rechargeEnds[index])
                {
                    rechargeEnds.RemoveAt(index);
                }
            }
        }

        private float NextRechargeSeconds()
        {
            if (rechargeEnds.Count == 0) return 0f;
            float earliest = rechargeEnds[0];
            for (int index = 1; index < rechargeEnds.Count; index++)
            {
                earliest = Mathf.Min(earliest, rechargeEnds[index]);
            }
            return Mathf.Max(0f, earliest - Time.time);
        }

        private static int BaseChargeCount(string classId)
        {
            if (IsClass(classId, "juggernaut")) return 0;
            if (IsClass(classId, "medic")) return 1;
            if (IsClass(classId, "striker")
                || IsClass(classId, "aggressive"))
            {
                return 2;
            }

            Debug.LogError("skill-move-class-unsupported:" + classId);
            return 0;
        }

        private static bool IsClass(string classId, string className)
        {
            return !string.IsNullOrWhiteSpace(classId)
                && classId.IndexOf(
                    className,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Vector2 ReadDirection(Keyboard keyboard)
        {
            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        private void OnDisable()
        {
            dashing = false;
        }
    }

    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class SkillArmor :
        MonoBehaviour,
        IPlayablePlayerDamageReceiver
    {
        private const float HubRetrySeconds = 0.25f;

        private PlayerHUD source;
        private float reduction;
        private float nextHubRetry;
        private bool bound;

        public event Action<PlayablePlayerDefeatedFact> Defeated;

        public GameplayEntityIdentity Identity
        {
            get { return RequireSource().Identity; }
        }
        public StableId CharacterInstanceStableId
        {
            get { return RequireSource().CharacterInstanceStableId; }
        }
        public long LifecycleGeneration
        {
            get { return RequireSource().LifecycleGeneration; }
        }
        public double CurrentHealth
        {
            get { return RequireSource().CurrentHealth; }
        }
        public double MaximumHealth
        {
            get { return RequireSource().MaximumHealth; }
        }
        public bool IsDefeated
        {
            get { return RequireSource().IsDefeated; }
        }

        public void Bind(PlayerHUD configuredSource, float armorReduction)
        {
            if (bound)
            {
                throw new InvalidOperationException("skill-armor-duplicate-binding");
            }
            source = configuredSource
                ?? throw new ArgumentNullException(nameof(configuredSource));
            if (!source.IsBound)
            {
                throw new InvalidOperationException("skill-armor-source-unbound");
            }

            reduction = Mathf.Clamp(armorReduction, 0f, 0.9f);
            source.Defeated += HandleDefeated;
            source.enabled = false;
            bound = true;
        }

        public PlayerActorSnapshot ExportSnapshot()
        {
            return RequireSource().ExportSnapshot();
        }

        public DamageReceiverResult ApplyDamage(DamageReceiverCommand command)
        {
            PlayerHUD health = RequireSource();
            if (command == null || reduction <= 0f)
            {
                return health.ApplyDamage(command);
            }

            var scaled = new DamageReceiverCommand(
                command.EventId,
                command.SourceActorId,
                command.SourceRunParticipantId,
                command.TargetActorId,
                command.Amount * (1d - reduction),
                command.Channel,
                command.LifecycleGeneration);
            return health.ApplyDamage(scaled);
        }

        private void Update()
        {
            if (!bound
                || source == null
                || !source.IsDefeated
                || source.IsHubReturnAccepted
                || Time.unscaledTime < nextHubRetry)
            {
                return;
            }

            nextHubRetry = Time.unscaledTime + HubRetrySeconds;
            source.TryRetryHubReturn();
        }

        private void OnGUI()
        {
            if (!bound || source == null) return;
            GUI.Label(
                new Rect(16f, 16f, 260f, 24f),
                "HP " + source.CurrentHealth.ToString("0")
                + "/" + source.MaximumHealth.ToString("0")
                + "  ARMOR " + (reduction * 100f).ToString("0") + "%");
        }

        private void HandleDefeated(PlayablePlayerDefeatedFact fact)
        {
            SkillMove skills = GetComponent<SkillMove>();
            if (skills != null)
            {
                skills.enabled = false;
            }

            Action<PlayablePlayerDefeatedFact> handlers = Defeated;
            if (handlers != null)
            {
                handlers(fact);
            }
        }

        private PlayerHUD RequireSource()
        {
            if (!bound || source == null)
            {
                throw new InvalidOperationException("skill-armor-source-missing");
            }
            return source;
        }

        private void OnDestroy()
        {
            if (source != null)
            {
                source.Defeated -= HandleDefeated;
            }
        }
    }
}

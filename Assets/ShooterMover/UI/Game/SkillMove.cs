using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Progression.Skills;
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

    [DefaultExecutionOrder(600)]
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
            if (body == null || movement == null) return false;

            SkillEffectSnapshot effects = new SkillEffectProjector().Project(
                graph.SkillAuthority.Catalog,
                allocation);
            SkillMove skills = player.GetComponent<SkillMove>()
                ?? player.gameObject.AddComponent<SkillMove>();
            skills.Bind(body, movement, effects);

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
        private const float BaseDashSpeed = 18f;
        private const float BaseDashSeconds = 0.18f;
        private const float BaseRechargeSeconds = 2f;
        private const int BaseCharges = 2;

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
        private int maximumCharges = BaseCharges;
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
            SkillEffectSnapshot effects)
        {
            if (bound)
            {
                throw new InvalidOperationException("skill-move-duplicate-binding");
            }
            body = configuredBody
                ?? throw new ArgumentNullException(nameof(configuredBody));
            movement = configuredMovement
                ?? throw new ArgumentNullException(nameof(configuredMovement));
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            speedScale = Mathf.Max(
                0.1f,
                (float)effects.Apply("movement.speed", 1m));
            dashSpeed = BaseDashSpeed * speedScale;
            dashSeconds = BaseDashSeconds * Mathf.Max(
                0.1f,
                (float)effects.Apply("movement.energy_efficiency", 1m));

            float recovery = Mathf.Max(
                0.1f,
                (float)effects.Apply("movement.thruster_recovery", 1m));
            float recoveryDelay =
                (float)effects.Apply("movement.recovery_delay", 0m);
            rechargeSeconds = Mathf.Max(
                0.25f,
                BaseRechargeSeconds / recovery + recoveryDelay);

            maximumCharges = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    (float)effects.Apply(
                        "movement.maximum_charges",
                        BaseCharges)));

            float armorFactor = Mathf.Max(
                1f,
                (float)effects.Apply("character.armor", 1m));
            armorReduction = Mathf.Clamp(armorFactor - 1f, 0f, 0.9f);
            bound = true;
        }

        public double ScaleIncomingDamage(double amount)
        {
            return amount * (1d - armorReduction);
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
            if (keyboard == null) return;

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
            string text = "THRUSTER "
                + AvailableCharges + "/" + maximumCharges;
            float next = NextRechargeSeconds();
            if (next > 0f)
            {
                text += "  " + next.ToString("0.0") + "s";
            }
            text += "  [SHIFT / SPACE]";
            GUI.Label(new Rect(16f, Screen.height - 34f, 300f, 24f), text);
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
}

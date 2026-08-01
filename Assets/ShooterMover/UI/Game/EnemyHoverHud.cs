using System;
using System.Globalization;
using System.Text;
using ShooterMover.Content.Definitions.Levels.Selection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.Game
{
    internal static class EnemyHoverHudInstaller
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
                if (roots[index].GetComponentInChildren<EnemyHoverHud>(true)
                    != null)
                {
                    return;
                }

                LevelGame candidate = roots[index]
                    .GetComponentInChildren<LevelGame>(true);
                if (candidate == null) continue;
                if (level != null && !ReferenceEquals(level, candidate))
                {
                    Debug.LogError("enemy-hover-hud-level-duplicated", candidate);
                    return;
                }
                level = candidate;
            }

            if (level != null)
            {
                level.gameObject.AddComponent<EnemyHoverHud>();
            }
        }
    }

    /// <summary>
    /// Read-only presentation of the live enemy currently under the mouse cursor.
    /// EnemyInstance remains the sole health and lifecycle authority.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class EnemyHoverHud : MonoBehaviour
    {
        private const float PanelWidth = 300f;
        private const float PanelHeight = 82f;
        private const float PanelMargin = 24f;

        private Camera gameplayCamera;
        private Enemy hoveredEnemy;
        private GUIStyle headerStyle;
        private GUIStyle healthStyle;

        public Enemy HoveredEnemy { get { return hoveredEnemy; } }

        private void Update()
        {
            hoveredEnemy = ResolveHoveredEnemy();
        }

        private Enemy ResolveHoveredEnemy()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return null;

            if (gameplayCamera == null
                || !gameplayCamera.isActiveAndEnabled
                || gameplayCamera.gameObject.scene != gameObject.scene)
            {
                gameplayCamera = ResolveGameplayCamera(gameObject.scene);
                if (gameplayCamera == null) return null;
            }

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = gameplayCamera.ScreenToWorldPoint(
                new Vector3(
                    screen.x,
                    screen.y,
                    -gameplayCamera.transform.position.z));
            Collider2D[] overlaps = Physics2D.OverlapPointAll(
                new Vector2(world.x, world.y));

            Enemy selected = null;
            float selectedDistance = float.PositiveInfinity;
            for (int index = 0; index < overlaps.Length; index++)
            {
                Collider2D overlap = overlaps[index];
                Enemy candidate = overlap == null
                    ? null
                    : overlap.GetComponentInParent<Enemy>();
                if (!IsLiveSceneEnemy(candidate)) continue;

                float distance = ((Vector2)candidate.transform.position
                    - new Vector2(world.x, world.y)).sqrMagnitude;
                if (selected == null
                    || distance < selectedDistance
                    || (Mathf.Approximately(distance, selectedDistance)
                        && candidate.ActorStableId.CompareTo(
                            selected.ActorStableId) < 0))
                {
                    selected = candidate;
                    selectedDistance = distance;
                }
            }
            return selected;
        }

        private bool IsLiveSceneEnemy(Enemy candidate)
        {
            return candidate != null
                && candidate.gameObject.scene == gameObject.scene
                && candidate.isActiveAndEnabled
                && candidate.IsBound
                && candidate.IsAlive
                && candidate.Runtime != null
                && candidate.Runtime.ActorState != null;
        }

        private void OnGUI()
        {
            if (!UnityEngine.Application.isPlaying
                || !IsLiveSceneEnemy(hoveredEnemy))
            {
                return;
            }

            EnemyActorState state = hoveredEnemy.Runtime.ActorState;
            float normalized = state.MaximumHealth <= 0d
                ? 0f
                : Mathf.Clamp01((float)(state.Health / state.MaximumHealth));
            float x = Mathf.Max(PanelMargin, Screen.width - PanelWidth - PanelMargin);
            const float y = PanelMargin;
            var panel = new Rect(x, y, PanelWidth, PanelHeight);

            EnsureStyles();
            Color previous = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(
                new Rect(x + 12f, y + 7f, PanelWidth - 24f, 22f),
                FormatDroidName(hoveredEnemy.Runtime.Definition.DefinitionId)
                    + "   LEVEL "
                    + hoveredEnemy.Runtime.Tier.ToString(
                        CultureInfo.InvariantCulture),
                headerStyle);

            var bar = new Rect(x + 12f, y + 37f, PanelWidth - 24f, 30f);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = normalized > 0.25f
                ? new Color(0.78f, 0.14f, 0.12f, 1f)
                : new Color(0.95f, 0.38f, 0.08f, 1f);
            GUI.DrawTexture(
                new Rect(
                    bar.x + 3f,
                    bar.y + 3f,
                    (bar.width - 6f) * normalized,
                    bar.height - 6f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(
                bar,
                FormatHealth(state.Health)
                    + " / "
                    + FormatHealth(state.MaximumHealth),
                healthStyle);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                };
            }
            if (healthStyle == null)
            {
                healthStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                };
            }
        }

        public static string FormatDroidName(StableId definitionStableId)
        {
            if (definitionStableId == null) return "Unknown Droid";
            string value = definitionStableId.ToString();
            int separator = value.LastIndexOf('.');
            string leaf = separator >= 0 && separator < value.Length - 1
                ? value.Substring(separator + 1)
                : value;
            string[] words = leaf.Split(
                new[] { '-', '_', ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "Unknown Droid";

            var builder = new StringBuilder();
            for (int index = 0; index < words.Length; index++)
            {
                if (index > 0) builder.Append(' ');
                string word = words[index];
                builder.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    builder.Append(word.Substring(1).ToLowerInvariant());
                }
            }
            return builder.ToString();
        }

        private static string FormatHealth(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static Camera ResolveGameplayCamera(Scene scene)
        {
            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Camera resolved = null;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate == null
                    || !candidate.enabled
                    || candidate.gameObject.scene != scene)
                {
                    continue;
                }
                if (resolved != null) return null;
                resolved = candidate;
            }
            return resolved;
        }

        private void OnDisable()
        {
            hoveredEnemy = null;
            gameplayCamera = null;
        }
    }
}

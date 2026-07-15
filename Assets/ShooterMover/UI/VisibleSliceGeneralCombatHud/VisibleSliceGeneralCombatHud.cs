using System;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Movement;
using UnityEngine;

namespace ShooterMover.UI.VisibleSliceGeneralCombatHud
{
    /// <summary>
    /// Read-only injection boundary for one immutable HUD snapshot.
    /// Implementations may read gameplay-owned state, but the HUD never receives a mutation surface.
    /// </summary>
    public interface IGeneralCombatHudStateSource
    {
        bool TryRead(out GeneralCombatHudSnapshot snapshot);
    }

    /// <summary>
    /// Read-only source of already accepted CS-004 hit facts.
    /// The HUD does not infer hits from projectiles, collisions, or damage deltas.
    /// </summary>
    public interface IGeneralCombatHudHitFactSource
    {
        bool TryReadLatest(out HitMessage hitFact);
    }

    /// <summary>
    /// Immutable session-only input. Player health uses the accepted CS-004 VitalState,
    /// thruster uses MT-011's immutable snapshot, and focused enemy health uses EN-002 state.
    /// </summary>
    public sealed class GeneralCombatHudSnapshot
    {
        public const string DefaultRoomName = "PROTOTYPE ROOM";
        public const string DefaultObjectiveText = "CLEAR THE ROOM";
        public const string DefaultKeyboardRestartHint = "R";
        public const string DefaultControllerRestartHint = "MENU";
        public const string DefaultEnemyLabel = "FOCUSED ENEMY";

        public GeneralCombatHudSnapshot(
            VitalState playerVital,
            ThrusterStatusSnapshot thrusterStatus,
            EnemyActorState focusedEnemy,
            string focusedEnemyLabel,
            string roomName,
            string objectiveText,
            string restartKeyboardHint,
            string restartControllerHint,
            bool reticleVisible,
            double reticleNormalizedX,
            double reticleNormalizedY,
            bool reducedEffects,
            long restartGeneration)
        {
            if (playerVital == null)
            {
                throw new ArgumentNullException(nameof(playerVital));
            }

            if (restartGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(restartGeneration),
                    restartGeneration,
                    "Restart generation cannot be negative.");
            }

            ValidateFinite(reticleNormalizedX, nameof(reticleNormalizedX));
            ValidateFinite(reticleNormalizedY, nameof(reticleNormalizedY));

            PlayerVital = playerVital;
            ThrusterStatus = thrusterStatus;
            FocusedEnemy = focusedEnemy;
            FocusedEnemyLabel = NormalizeRequired(focusedEnemyLabel, DefaultEnemyLabel, 48);
            RoomName = NormalizeRequired(roomName, DefaultRoomName, 48);
            ObjectiveText = NormalizeRequired(objectiveText, DefaultObjectiveText, 120);
            RestartKeyboardHint = NormalizeOptional(restartKeyboardHint, 24);
            RestartControllerHint = NormalizeOptional(restartControllerHint, 24);
            ReticleVisible = reticleVisible;
            ReticleNormalizedX = Clamp01(reticleNormalizedX);
            ReticleNormalizedY = Clamp01(reticleNormalizedY);
            ReducedEffects = reducedEffects;
            RestartGeneration = restartGeneration;
        }

        public VitalState PlayerVital { get; }

        public ThrusterStatusSnapshot ThrusterStatus { get; }

        public EnemyActorState FocusedEnemy { get; }

        public string FocusedEnemyLabel { get; }

        public string RoomName { get; }

        public string ObjectiveText { get; }

        public string RestartKeyboardHint { get; }

        public string RestartControllerHint { get; }

        public bool ReticleVisible { get; }

        public double ReticleNormalizedX { get; }

        public double ReticleNormalizedY { get; }

        public bool ReducedEffects { get; }

        public long RestartGeneration { get; }

        internal static string NormalizeRequired(string value, string fallback, int maximumLength)
        {
            string normalized = NormalizeOptional(value, maximumLength);
            return normalized.Length == 0 ? fallback : normalized;
        }

        internal static string NormalizeOptional(string value, int maximumLength)
        {
            if (maximumLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLength));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(Math.Min(value.Length, maximumLength));
            bool pendingSpace = false;
            foreach (char character in value.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    if (builder.Length >= maximumLength)
                    {
                        break;
                    }

                    builder.Append(' ');
                    pendingSpace = false;
                }

                if (builder.Length >= maximumLength)
                {
                    break;
                }

                builder.Append(character);
            }

            string normalized = builder.ToString();
            if (normalized.Length == maximumLength && value.Trim().Length > maximumLength)
            {
                if (maximumLength >= 3)
                {
                    normalized = normalized.Substring(0, maximumLength - 3) + "...";
                }
            }

            return normalized;
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Reticle coordinates must be finite.");
            }
        }

        private static double Clamp01(double value)
        {
            if (value < 0d)
            {
                return 0d;
            }

            if (value > 1d)
            {
                return 1d;
            }

            return value;
        }
    }

    /// <summary>Immutable text-and-meter frame consumed by the temporary view.</summary>
    public sealed class GeneralCombatHudFrame
    {
        internal GeneralCombatHudFrame(
            string playerHealthText,
            double playerHealthFraction,
            bool playerCritical,
            string playerStateText,
            string thrusterText,
            double thrusterFraction,
            string roomText,
            string objectiveText,
            string restartHint,
            bool hasFocusedEnemy,
            string focusedEnemyTitle,
            string focusedEnemyHealthText,
            double focusedEnemyHealthFraction,
            string focusedEnemyStateText,
            bool reticleVisible,
            double reticleNormalizedX,
            double reticleNormalizedY,
            bool confirmedHitVisible,
            string confirmedHitText,
            bool reducedEffects,
            string reducedEffectsWarning,
            long restartGeneration)
        {
            PlayerHealthText = playerHealthText;
            PlayerHealthFraction = playerHealthFraction;
            PlayerCritical = playerCritical;
            PlayerStateText = playerStateText;
            ThrusterText = thrusterText;
            ThrusterFraction = thrusterFraction;
            RoomText = roomText;
            ObjectiveText = objectiveText;
            RestartHint = restartHint;
            HasFocusedEnemy = hasFocusedEnemy;
            FocusedEnemyTitle = focusedEnemyTitle;
            FocusedEnemyHealthText = focusedEnemyHealthText;
            FocusedEnemyHealthFraction = focusedEnemyHealthFraction;
            FocusedEnemyStateText = focusedEnemyStateText;
            ReticleVisible = reticleVisible;
            ReticleNormalizedX = reticleNormalizedX;
            ReticleNormalizedY = reticleNormalizedY;
            ConfirmedHitVisible = confirmedHitVisible;
            ConfirmedHitText = confirmedHitText;
            ReducedEffects = reducedEffects;
            ReducedEffectsWarning = reducedEffectsWarning;
            RestartGeneration = restartGeneration;
        }

        public string PlayerHealthText { get; }

        public double PlayerHealthFraction { get; }

        public bool PlayerCritical { get; }

        public string PlayerStateText { get; }

        public string ThrusterText { get; }

        public double ThrusterFraction { get; }

        public string RoomText { get; }

        public string ObjectiveText { get; }

        public string RestartHint { get; }

        public bool HasFocusedEnemy { get; }

        public string FocusedEnemyTitle { get; }

        public string FocusedEnemyHealthText { get; }

        public double FocusedEnemyHealthFraction { get; }

        public string FocusedEnemyStateText { get; }

        public bool ReticleVisible { get; }

        public double ReticleNormalizedX { get; }

        public double ReticleNormalizedY { get; }

        public bool ConfirmedHitVisible { get; }

        public string ConfirmedHitText { get; }

        public bool ReducedEffects { get; }

        public string ReducedEffectsWarning { get; }

        public long RestartGeneration { get; }

        public string ToTraceString()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "player=" + PlayerHealthText + "|" + PlayerStateText,
                    "thruster=" + ThrusterText,
                    "room=" + RoomText,
                    "objective=" + ObjectiveText,
                    "restart=" + RestartHint,
                    "enemy=" + (HasFocusedEnemy
                        ? FocusedEnemyTitle + "|" + FocusedEnemyHealthText + "|" + FocusedEnemyStateText
                        : "none"),
                    "reticle=" + (ReticleVisible ? "visible" : "hidden"),
                    "hit=" + (ConfirmedHitVisible ? ConfirmedHitText : "none"),
                    "reduced_effects=" + (ReducedEffects ? "true" : "false"),
                    "warning=" + (string.IsNullOrEmpty(ReducedEffectsWarning)
                        ? "none"
                        : ReducedEffectsWarning),
                    "restart_generation=" + RestartGeneration.ToString(CultureInfo.InvariantCulture),
                });
        }
    }

    /// <summary>
    /// Pure projection from injected immutable state. It does not query scenes, input,
    /// projectiles, missions, saves, rewards, or WP-010.
    /// </summary>
    public sealed class GeneralCombatHudProjector
    {
        public const double CriticalHealthFraction = 0.25d;
        public const string ReducedEffectsWarningText = "REDUCED EFFECTS ENABLED";
        public const string ConfirmedHitText = "HIT CONFIRMED +";

        public GeneralCombatHudFrame Project(
            GeneralCombatHudSnapshot snapshot,
            bool confirmedHitVisible)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            VitalState player = snapshot.PlayerVital;
            double playerFraction = SafeFraction(player.Health, player.MaximumHealth);
            bool playerDestroyed = player.IsDestroyed;
            bool playerCritical = !playerDestroyed && playerFraction <= CriticalHealthFraction;
            string playerState = playerDestroyed
                ? "DESTROYED"
                : playerCritical
                    ? "CRITICAL"
                    : "ACTIVE";

            string thrusterText;
            double thrusterFraction;
            ProjectThruster(snapshot.ThrusterStatus, out thrusterText, out thrusterFraction);

            bool hasEnemy = snapshot.FocusedEnemy != null;
            string enemyTitle = hasEnemy ? snapshot.FocusedEnemyLabel : "NO FOCUSED ENEMY";
            string enemyHealthText = "HEALTH --";
            double enemyFraction = 0d;
            string enemyState = "NO TARGET";
            if (hasEnemy)
            {
                EnemyActorState enemy = snapshot.FocusedEnemy;
                enemyFraction = SafeFraction(enemy.Health, enemy.MaximumHealth);
                enemyHealthText = BuildHealthText(enemy.Health, enemy.MaximumHealth);
                enemyState = enemy.IsDestroyed ? "DESTROYED" : "ACTIVE";
            }

            return new GeneralCombatHudFrame(
                BuildHealthText(player.Health, player.MaximumHealth),
                playerFraction,
                playerCritical,
                playerState,
                thrusterText,
                thrusterFraction,
                "ROOM: " + snapshot.RoomName,
                "OBJECTIVE: " + snapshot.ObjectiveText,
                BuildRestartHint(
                    snapshot.RestartKeyboardHint,
                    snapshot.RestartControllerHint),
                hasEnemy,
                enemyTitle,
                enemyHealthText,
                enemyFraction,
                enemyState,
                snapshot.ReticleVisible,
                snapshot.ReticleNormalizedX,
                snapshot.ReticleNormalizedY,
                confirmedHitVisible,
                confirmedHitVisible ? ConfirmedHitText : string.Empty,
                snapshot.ReducedEffects,
                snapshot.ReducedEffects ? ReducedEffectsWarningText : string.Empty,
                snapshot.RestartGeneration);
        }

        private static void ProjectThruster(
            ThrusterStatusSnapshot status,
            out string text,
            out double fraction)
        {
            if (status == null || !status.IsRuntimeAvailable)
            {
                text = "THRUSTER: UNAVAILABLE | CHARGES --";
                fraction = 0d;
                return;
            }

            fraction = status.MaximumCharges <= 0
                ? 0d
                : SafeFraction(status.AvailableCharges, status.MaximumCharges);

            string detail = status.State.ToString().ToUpperInvariant();
            if (status.IsRegenerating || status.IsEmpty)
            {
                detail += " | RECHARGE "
                    + status.RechargeSeconds.ToString("0.00", CultureInfo.InvariantCulture)
                    + "s";
            }

            text = "THRUSTER: "
                + detail
                + " | CHARGES "
                + status.AvailableCharges.ToString(CultureInfo.InvariantCulture)
                + "/"
                + status.MaximumCharges.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildHealthText(double health, double maximumHealth)
        {
            return "HEALTH "
                + health.ToString("0.#", CultureInfo.InvariantCulture)
                + "/"
                + maximumHealth.ToString("0.#", CultureInfo.InvariantCulture)
                + " ("
                + Percentage(SafeFraction(health, maximumHealth)).ToString(
                    CultureInfo.InvariantCulture)
                + "%)";
        }

        private static string BuildRestartHint(string keyboard, string controller)
        {
            string key = GeneralCombatHudSnapshot.NormalizeOptional(keyboard, 24);
            string pad = GeneralCombatHudSnapshot.NormalizeOptional(controller, 24);
            if (key.Length == 0 && pad.Length == 0)
            {
                key = GeneralCombatHudSnapshot.DefaultKeyboardRestartHint;
                pad = GeneralCombatHudSnapshot.DefaultControllerRestartHint;
            }

            if (key.Length > 0 && pad.Length > 0)
            {
                return "RESTART: " + key + " / " + pad;
            }

            return "RESTART: " + (key.Length > 0 ? key : pad);
        }

        private static int Percentage(double fraction)
        {
            return (int)Math.Round(
                fraction * 100d,
                MidpointRounding.AwayFromZero);
        }

        private static double SafeFraction(double current, double maximum)
        {
            if (maximum <= 0d)
            {
                return 0d;
            }

            double value = current / maximum;
            if (value < 0d)
            {
                return 0d;
            }

            if (value > 1d)
            {
                return 1d;
            }

            return value;
        }
    }

    /// <summary>
    /// Deterministic screen-space layout. The bottom 196 pixels are an exclusive
    /// no-draw reservation around WP-010's existing 170 px strip and 14 px margin.
    /// </summary>
    public sealed class GeneralCombatHudLayout
    {
        internal GeneralCombatHudLayout(
            Rect screen,
            Rect safeGameplay,
            Rect weaponStripReservation,
            Rect playerPanel,
            Rect roomPanel,
            Rect focusedEnemyPanel,
            Rect reducedEffectsPanel,
            Rect restartPanel,
            Rect reticleBounds,
            Rect hitConfirmationPanel)
        {
            Screen = screen;
            SafeGameplay = safeGameplay;
            WeaponStripReservation = weaponStripReservation;
            PlayerPanel = playerPanel;
            RoomPanel = roomPanel;
            FocusedEnemyPanel = focusedEnemyPanel;
            ReducedEffectsPanel = reducedEffectsPanel;
            RestartPanel = restartPanel;
            ReticleBounds = reticleBounds;
            HitConfirmationPanel = hitConfirmationPanel;
        }

        public Rect Screen { get; }

        public Rect SafeGameplay { get; }

        public Rect WeaponStripReservation { get; }

        public Rect PlayerPanel { get; }

        public Rect RoomPanel { get; }

        public Rect FocusedEnemyPanel { get; }

        public Rect ReducedEffectsPanel { get; }

        public Rect RestartPanel { get; }

        public Rect ReticleBounds { get; }

        public Rect HitConfirmationPanel { get; }

        public bool AnyHudPanelOverlapsWeaponStrip()
        {
            return PlayerPanel.Overlaps(WeaponStripReservation)
                || RoomPanel.Overlaps(WeaponStripReservation)
                || FocusedEnemyPanel.Overlaps(WeaponStripReservation)
                || ReducedEffectsPanel.Overlaps(WeaponStripReservation)
                || RestartPanel.Overlaps(WeaponStripReservation)
                || ReticleBounds.Overlaps(WeaponStripReservation)
                || HitConfirmationPanel.Overlaps(WeaponStripReservation);
        }
    }

    public static class GeneralCombatHudLayoutCalculator
    {
        public const float SafeMargin = 24f;
        public const float PanelGap = 12f;
        public const float Wp010ReservedHeight = 196f;
        public const int MinimumSupportedWidth = 960;
        public const int MinimumSupportedHeight = 540;

        public static GeneralCombatHudLayout Compute(
            int screenWidth,
            int screenHeight,
            double reticleNormalizedX,
            double reticleNormalizedY)
        {
            if (screenWidth < MinimumSupportedWidth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(screenWidth),
                    screenWidth,
                    "HUD requires at least 960 horizontal pixels.");
            }

            if (screenHeight < MinimumSupportedHeight)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(screenHeight),
                    screenHeight,
                    "HUD requires at least 540 vertical pixels.");
            }

            float width = screenWidth;
            float height = screenHeight;
            Rect screen = new Rect(0f, 0f, width, height);
            Rect reserved = new Rect(
                0f,
                height - Wp010ReservedHeight,
                width,
                Wp010ReservedHeight);

            float gameplayBottom = reserved.yMin - PanelGap;
            Rect safe = Rect.MinMaxRect(
                SafeMargin,
                SafeMargin,
                width - SafeMargin,
                gameplayBottom);

            float sideWidth = Mathf.Clamp(width * 0.24f, 260f, 360f);
            const float panelHeight = 126f;
            Rect player = new Rect(
                SafeMargin,
                SafeMargin,
                sideWidth,
                panelHeight);
            Rect enemy = new Rect(
                width - SafeMargin - sideWidth,
                SafeMargin,
                sideWidth,
                panelHeight);

            float roomX = player.xMax + PanelGap;
            float roomWidth = enemy.xMin - PanelGap - roomX;
            Rect room = new Rect(
                roomX,
                SafeMargin,
                roomWidth,
                92f);
            Rect warning = new Rect(
                roomX,
                room.yMax + PanelGap,
                roomWidth,
                28f);

            float restartWidth = Mathf.Min(360f, roomWidth);
            Rect restart = new Rect(
                (width - restartWidth) * 0.5f,
                gameplayBottom - 32f,
                restartWidth,
                28f);

            float reticleTop = player.yMax + PanelGap + 22f;
            float reticleBottom = restart.yMin - PanelGap - 22f;
            Rect reticleArea = Rect.MinMaxRect(
                SafeMargin + 22f,
                reticleTop,
                width - SafeMargin - 22f,
                Mathf.Max(reticleTop + 1f, reticleBottom));

            float normalizedX = Mathf.Clamp01((float)reticleNormalizedX);
            float normalizedY = Mathf.Clamp01((float)reticleNormalizedY);
            float centerX = Mathf.Lerp(reticleArea.xMin, reticleArea.xMax, normalizedX);
            float centerY = Mathf.Lerp(reticleArea.yMin, reticleArea.yMax, normalizedY);
            Rect reticle = new Rect(centerX - 22f, centerY - 22f, 44f, 44f);

            float hitWidth = 164f;
            float hitX = Mathf.Clamp(
                centerX - hitWidth * 0.5f,
                SafeMargin,
                width - SafeMargin - hitWidth);
            float hitY = Mathf.Clamp(
                centerY + 28f,
                reticleTop,
                restart.yMin - 26f);
            Rect hit = new Rect(hitX, hitY, hitWidth, 24f);

            return new GeneralCombatHudLayout(
                screen,
                safe,
                reserved,
                player,
                room,
                enemy,
                warning,
                restart,
                reticle,
                hit);
        }
    }

    /// <summary>
    /// Temporary reusable combat HUD. It reads injected immutable state, projects a frame,
    /// and draws only outside the WP-010 reservation. It consumes no gameplay input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisibleSliceGeneralCombatHud : MonoBehaviour
    {
        public const float DefaultHitConfirmationSeconds = 0.20f;
        public const float MinimumHitConfirmationSeconds = 0.05f;
        public const float MaximumHitConfirmationSeconds = 1.00f;

        [SerializeField] private bool visible = true;
        [SerializeField] private float hitConfirmationSeconds = DefaultHitConfirmationSeconds;
        [SerializeField] private bool logProjectionProof;

        private readonly GeneralCombatHudProjector projector = new GeneralCombatHudProjector();
        private IGeneralCombatHudStateSource stateSource;
        private IGeneralCombatHudHitFactSource hitFactSource;
        private GeneralCombatHudSnapshot currentSnapshot;
        private GeneralCombatHudFrame currentFrame;
        private ShooterMover.Domain.Common.StableId lastObservedHitEventId;
        private ShooterMover.Domain.Common.StableId activeHitEventId;
        private double activeHitUntilSeconds;
        private long observedRestartGeneration = -1L;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle criticalStyle;
        private GUIStyle centeredStyle;

        public GeneralCombatHudFrame CurrentFrame
        {
            get { return currentFrame; }
        }

        public bool IsVisible
        {
            get { return visible; }
        }

        public float HitConfirmationSeconds
        {
            get { return hitConfirmationSeconds; }
        }

        public void BindSources(
            IGeneralCombatHudStateSource suppliedStateSource,
            IGeneralCombatHudHitFactSource suppliedHitFactSource)
        {
            if (suppliedStateSource == null)
            {
                throw new ArgumentNullException(nameof(suppliedStateSource));
            }

            stateSource = suppliedStateSource;
            hitFactSource = suppliedHitFactSource;
        }

        public void UnbindSources()
        {
            stateSource = null;
            hitFactSource = null;
        }

        public void SetVisible(bool value)
        {
            visible = value;
        }

        public void SetHitConfirmationSeconds(float seconds)
        {
            if (float.IsNaN(seconds)
                || float.IsInfinity(seconds)
                || seconds < MinimumHitConfirmationSeconds
                || seconds > MaximumHitConfirmationSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seconds),
                    seconds,
                    "Hit confirmation duration is outside the bounded presentation range.");
            }

            hitConfirmationSeconds = seconds;
        }

        public bool RefreshFromSources(double nowSeconds)
        {
            ValidateTime(nowSeconds);
            if (stateSource == null)
            {
                return false;
            }

            GeneralCombatHudSnapshot snapshot;
            if (!stateSource.TryRead(out snapshot) || snapshot == null)
            {
                currentSnapshot = null;
                currentFrame = null;
                return false;
            }

            ObserveRestartGeneration(snapshot.RestartGeneration);
            if (hitFactSource != null)
            {
                HitMessage hitFact;
                if (hitFactSource.TryReadLatest(out hitFact) && hitFact != null)
                {
                    AcceptHitFact(hitFact, nowSeconds);
                }
            }

            Present(snapshot, nowSeconds);
            return true;
        }

        public void Present(GeneralCombatHudSnapshot snapshot, double nowSeconds)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ValidateTime(nowSeconds);
            ObserveRestartGeneration(snapshot.RestartGeneration);
            currentSnapshot = snapshot;
            bool hitVisible = activeHitEventId != null
                && nowSeconds <= activeHitUntilSeconds;
            if (!hitVisible)
            {
                activeHitEventId = null;
            }

            currentFrame = projector.Project(snapshot, hitVisible);
            if (logProjectionProof)
            {
                Debug.Log("VS-004 HUD\n" + currentFrame.ToTraceString(), this);
            }
        }

        public bool AcceptHitFact(HitMessage hitFact, double nowSeconds)
        {
            if (hitFact == null)
            {
                return false;
            }

            ValidateTime(nowSeconds);
            if (hitFact.Result != HitResult.Confirmed)
            {
                return false;
            }

            if (lastObservedHitEventId != null
                && lastObservedHitEventId == hitFact.EventId)
            {
                return false;
            }

            lastObservedHitEventId = hitFact.EventId;
            activeHitEventId = hitFact.EventId;
            activeHitUntilSeconds = nowSeconds + hitConfirmationSeconds;
            if (currentSnapshot != null)
            {
                currentFrame = projector.Project(currentSnapshot, true);
            }

            return true;
        }

        public void ResetPresentationForRestart(long restartGeneration)
        {
            if (restartGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(restartGeneration));
            }

            observedRestartGeneration = restartGeneration;
            activeHitEventId = null;
            activeHitUntilSeconds = 0d;
            if (currentSnapshot != null
                && currentSnapshot.RestartGeneration == restartGeneration)
            {
                currentFrame = projector.Project(currentSnapshot, false);
            }
        }

        public GeneralCombatHudLayout ComputeLayout(int screenWidth, int screenHeight)
        {
            double x = currentFrame == null ? 0.5d : currentFrame.ReticleNormalizedX;
            double y = currentFrame == null ? 0.5d : currentFrame.ReticleNormalizedY;
            return GeneralCombatHudLayoutCalculator.Compute(
                screenWidth,
                screenHeight,
                x,
                y);
        }

        public string BuildDebugTrace()
        {
            return currentFrame == null ? "vs004=no-frame" : currentFrame.ToTraceString();
        }

        private void Update()
        {
            double nowSeconds = Time.unscaledTime;
            if (stateSource != null)
            {
                RefreshFromSources(nowSeconds);
                return;
            }

            if (currentSnapshot != null
                && activeHitEventId != null
                && nowSeconds > activeHitUntilSeconds)
            {
                Present(currentSnapshot, nowSeconds);
            }
        }

        private void ObserveRestartGeneration(long restartGeneration)
        {
            if (observedRestartGeneration < 0L)
            {
                observedRestartGeneration = restartGeneration;
                return;
            }

            if (observedRestartGeneration == restartGeneration)
            {
                return;
            }

            observedRestartGeneration = restartGeneration;
            activeHitEventId = null;
            activeHitUntilSeconds = 0d;
        }

        private void OnGUI()
        {
            if (!visible || currentFrame == null)
            {
                return;
            }

            if (Screen.width < GeneralCombatHudLayoutCalculator.MinimumSupportedWidth
                || Screen.height < GeneralCombatHudLayoutCalculator.MinimumSupportedHeight)
            {
                return;
            }

            EnsureStyles();
            GeneralCombatHudLayout layout = ComputeLayout(Screen.width, Screen.height);
            DrawPlayerPanel(layout.PlayerPanel, currentFrame);
            DrawRoomPanel(layout.RoomPanel, currentFrame);
            DrawEnemyPanel(layout.FocusedEnemyPanel, currentFrame);
            DrawReducedEffectsWarning(layout.ReducedEffectsPanel, currentFrame);
            DrawRestartHint(layout.RestartPanel, currentFrame);

            if (currentFrame.ReticleVisible)
            {
                DrawReticle(layout.ReticleBounds);
            }

            if (currentFrame.ConfirmedHitVisible)
            {
                GUI.Box(layout.HitConfirmationPanel, GUIContent.none);
                GUI.Label(
                    layout.HitConfirmationPanel,
                    currentFrame.ConfirmedHitText,
                    centeredStyle);
            }
        }

        private void DrawPlayerPanel(Rect rect, GeneralCombatHudFrame frame)
        {
            GUI.Box(rect, GUIContent.none);
            Rect line = InsetLine(rect, 8f, 6f, 22f);
            GUI.Label(line, "PLAYER", titleStyle);
            line.y += 23f;
            GUI.Label(line, frame.PlayerHealthText, bodyStyle);
            line.y += 21f;
            DrawMeter(line, frame.PlayerHealthFraction);
            line.y += 18f;
            GUI.Label(line, "STATE: " + frame.PlayerStateText, frame.PlayerCritical
                ? criticalStyle
                : bodyStyle);
            line.y += 21f;
            GUI.Label(line, frame.ThrusterText, bodyStyle);
        }

        private void DrawRoomPanel(Rect rect, GeneralCombatHudFrame frame)
        {
            GUI.Box(rect, GUIContent.none);
            Rect line = InsetLine(rect, 10f, 6f, 22f);
            GUI.Label(line, frame.RoomText, titleStyle);
            line.y += 24f;
            line.height = rect.height - 34f;
            GUI.Label(line, frame.ObjectiveText, bodyStyle);
        }

        private void DrawEnemyPanel(Rect rect, GeneralCombatHudFrame frame)
        {
            GUI.Box(rect, GUIContent.none);
            Rect line = InsetLine(rect, 8f, 6f, 22f);
            GUI.Label(line, frame.FocusedEnemyTitle, titleStyle);
            line.y += 24f;
            GUI.Label(line, frame.FocusedEnemyHealthText, bodyStyle);
            line.y += 21f;
            DrawMeter(line, frame.FocusedEnemyHealthFraction);
            line.y += 18f;
            GUI.Label(line, "STATE: " + frame.FocusedEnemyStateText, bodyStyle);
        }

        private void DrawReducedEffectsWarning(Rect rect, GeneralCombatHudFrame frame)
        {
            if (!frame.ReducedEffects)
            {
                return;
            }

            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, frame.ReducedEffectsWarning, centeredStyle);
        }

        private void DrawRestartHint(Rect rect, GeneralCombatHudFrame frame)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, frame.RestartHint, centeredStyle);
        }

        private static void DrawReticle(Rect rect)
        {
            float centerX = rect.center.x;
            float centerY = rect.center.y;
            GUI.Box(new Rect(centerX - 1f, rect.yMin, 2f, 13f), GUIContent.none);
            GUI.Box(new Rect(centerX - 1f, rect.yMax - 13f, 2f, 13f), GUIContent.none);
            GUI.Box(new Rect(rect.xMin, centerY - 1f, 13f, 2f), GUIContent.none);
            GUI.Box(new Rect(rect.xMax - 13f, centerY - 1f, 13f, 2f), GUIContent.none);
            GUI.Label(new Rect(centerX - 10f, centerY - 12f, 20f, 24f), "+");
        }

        private static void DrawMeter(Rect line, double fraction)
        {
            Rect background = new Rect(line.x, line.y, line.width, 10f);
            GUI.Box(background, GUIContent.none);
            float innerWidth = Mathf.Max(0f, background.width - 4f);
            Rect fill = new Rect(
                background.x + 2f,
                background.y + 2f,
                innerWidth * Mathf.Clamp01((float)fraction),
                6f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
        }

        private static Rect InsetLine(
            Rect rect,
            float horizontal,
            float top,
            float height)
        {
            return new Rect(
                rect.x + horizontal,
                rect.y + top,
                rect.width - horizontal * 2f,
                height);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                clipping = TextClipping.Clip,
                wordWrap = true,
            };
            criticalStyle = new GUIStyle(bodyStyle)
            {
                fontStyle = FontStyle.Bold,
            };
            centeredStyle = new GUIStyle(titleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
            };
        }

        private static void ValidateTime(double nowSeconds)
        {
            if (double.IsNaN(nowSeconds)
                || double.IsInfinity(nowSeconds)
                || nowSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nowSeconds),
                    nowSeconds,
                    "Presentation time must be finite and non-negative.");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (hitConfirmationSeconds < MinimumHitConfirmationSeconds)
            {
                hitConfirmationSeconds = MinimumHitConfirmationSeconds;
            }
            else if (hitConfirmationSeconds > MaximumHitConfirmationSeconds)
            {
                hitConfirmationSeconds = MaximumHitConfirmationSeconds;
            }

            if (UnityEngine.Application.isPlaying
                && isActiveAndEnabled
                && currentSnapshot != null)
            {
                Present(currentSnapshot, Time.unscaledTime);
            }
        }
#endif
    }
}

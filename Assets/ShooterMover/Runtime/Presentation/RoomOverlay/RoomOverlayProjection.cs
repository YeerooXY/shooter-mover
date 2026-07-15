using System;
using System.Text;

namespace ShooterMover.Presentation.RoomOverlay
{
    /// <summary>
    /// Session-only input for the temporary room overlay. This type contains no gameplay or
    /// mission authority and is safe to discard whenever a prototype session ends.
    /// </summary>
    public sealed class RoomOverlayInput
    {
        public RoomOverlayInput(
            string roomName,
            string objectiveText,
            string restartKeyboardHint,
            string restartControllerHint,
            string temporaryStateLabel,
            bool reducedEffects)
        {
            RoomName = roomName;
            ObjectiveText = objectiveText;
            RestartKeyboardHint = restartKeyboardHint;
            RestartControllerHint = restartControllerHint;
            TemporaryStateLabel = temporaryStateLabel;
            ReducedEffects = reducedEffects;
        }

        public string RoomName { get; }
        public string ObjectiveText { get; }
        public string RestartKeyboardHint { get; }
        public string RestartControllerHint { get; }
        public string TemporaryStateLabel { get; }
        public bool ReducedEffects { get; }
    }

    /// <summary>Immutable, deterministic text frame consumed by the temporary view.</summary>
    public sealed class RoomOverlayFrame
    {
        internal RoomOverlayFrame(
            string roomName,
            string objectiveText,
            string restartHint,
            string temporaryStateLabel,
            string reducedEffectsWarning,
            bool reducedEffects)
        {
            RoomName = roomName;
            ObjectiveText = objectiveText;
            RestartHint = restartHint;
            TemporaryStateLabel = temporaryStateLabel;
            ReducedEffectsWarning = reducedEffectsWarning;
            ReducedEffects = reducedEffects;
        }

        public string RoomName { get; }
        public string ObjectiveText { get; }
        public string RestartHint { get; }
        public string TemporaryStateLabel { get; }
        public string ReducedEffectsWarning { get; }
        public bool ReducedEffects { get; }
        public bool HasTemporaryStateLabel => !string.IsNullOrEmpty(TemporaryStateLabel);
        public bool HasReducedEffectsWarning => !string.IsNullOrEmpty(ReducedEffectsWarning);

        public string ToTraceString()
        {
            return string.Join("\n", new[]
            {
                "room=" + RoomName,
                "objective=" + ObjectiveText,
                "restart=" + RestartHint,
                "temporary_state=" + (HasTemporaryStateLabel ? TemporaryStateLabel : "none"),
                "reduced_effects=" + (ReducedEffects ? "true" : "false"),
                "warning=" + (HasReducedEffectsWarning ? ReducedEffectsWarning : "none"),
            });
        }
    }

    /// <summary>
    /// Pure room-overlay projection. Repeated calls with equivalent text produce identical
    /// frames and never read scene, input, mission, combat, enemy, or persistence state.
    /// </summary>
    public sealed class RoomOverlayProjector
    {
        public const string DefaultRoomName = "PROTOTYPE ROOM";
        public const string DefaultObjective = "EXPLORE THE ROOM";
        public const string DefaultKeyboardRestartHint = "R";
        public const string DefaultControllerRestartHint = "MENU";
        public const string ReducedEffectsWarning = "REDUCED EFFECTS ENABLED";

        public RoomOverlayFrame Project(RoomOverlayInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            string roomName = NormalizeRequired(input.RoomName, DefaultRoomName);
            string objective = NormalizeRequired(input.ObjectiveText, DefaultObjective);
            string keyboard = NormalizeOptional(input.RestartKeyboardHint);
            string controller = NormalizeOptional(input.RestartControllerHint);
            string temporaryState = NormalizeOptional(input.TemporaryStateLabel);

            if (keyboard.Length == 0 && controller.Length == 0)
            {
                keyboard = DefaultKeyboardRestartHint;
                controller = DefaultControllerRestartHint;
            }

            string restartHint;
            if (keyboard.Length > 0 && controller.Length > 0)
                restartHint = "RESTART: " + keyboard + " / " + controller;
            else
                restartHint = "RESTART: " + (keyboard.Length > 0 ? keyboard : controller);

            return new RoomOverlayFrame(
                roomName,
                "OBJECTIVE: " + objective,
                restartHint,
                temporaryState,
                input.ReducedEffects ? ReducedEffectsWarning : string.Empty,
                input.ReducedEffects);
        }

        private static string NormalizeRequired(string value, string fallback)
        {
            string normalized = NormalizeOptional(value);
            return normalized.Length == 0 ? fallback : normalized;
        }

        private static string NormalizeOptional(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            StringBuilder result = new StringBuilder(value.Length);
            bool pendingSpace = false;
            foreach (char character in value.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = result.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    result.Append(' ');
                    pendingSpace = false;
                }
                result.Append(character);
            }
            return result.ToString();
        }
    }
}

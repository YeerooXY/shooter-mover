using System;

namespace ShooterMover.Application.Accessibility
{
    /// <summary>
    /// Deterministic fail-closed validation result. Invalid profiles never produce a
    /// partially normalized replacement.
    /// </summary>
    public sealed class Stage1AccessibilityValidationResult
    {
        private Stage1AccessibilityValidationResult(
            bool isValid,
            string errorCode,
            string errorMessage)
        {
            IsValid = isValid;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool IsValid { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }

        internal static Stage1AccessibilityValidationResult Valid()
        {
            return new Stage1AccessibilityValidationResult(true, null, null);
        }

        internal static Stage1AccessibilityValidationResult Invalid(
            string errorCode,
            string errorMessage)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                throw new ArgumentException("A stable validation error code is required.", nameof(errorCode));
            }

            if (string.IsNullOrEmpty(errorMessage))
            {
                throw new ArgumentException("A validation error message is required.", nameof(errorMessage));
            }

            return new Stage1AccessibilityValidationResult(false, errorCode, errorMessage);
        }
    }

    /// <summary>
    /// Strict validator for the AR-001 Stage 1 accessibility profile v1 contract.
    /// Validation order is part of the contract so malformed input fails identically.
    /// </summary>
    public static class Stage1AccessibilityProfileValidator
    {
        public const int MinimumPercent = 0;
        public const int MaximumPercent = 100;
        public const int MinimumAimSensitivityPercent = 25;
        public const int MaximumAimSensitivityPercent = 300;
        public const int MaximumFlashesPerSecond = 3;
        public const int MaximumCameraShakeDurationMilliseconds = 2000;
        public const int MaximumInputBufferMilliseconds = 250;
        public const int MinimumRepeatDelayMilliseconds = 100;
        public const int MaximumRepeatDelayMilliseconds = 1000;

        public static Stage1AccessibilityValidationResult Validate(
            Stage1AccessibilityProfile profile)
        {
            if (profile == null)
            {
                return Invalid("missing-profile", "A Stage 1 accessibility profile is required.");
            }

            if (profile.Version != Stage1AccessibilityProfile.CurrentVersion)
            {
                return Invalid(
                    "unsupported-version",
                    "Only Stage 1 accessibility profile version 1 is supported.");
            }

            if (profile.ReducedEffects == null)
            {
                return Invalid(
                    "missing-reduced-effects",
                    "The reduced-effects settings group is required.");
            }

            if (profile.FlashAndShake == null)
            {
                return Invalid(
                    "missing-flash-and-shake",
                    "The flash-and-shake settings group is required.");
            }

            if (profile.WarningRedundancy == null)
            {
                return Invalid(
                    "missing-warning-redundancy",
                    "The warning-redundancy settings group is required.");
            }

            if (profile.AudioLevels == null)
            {
                return Invalid(
                    "missing-audio-levels",
                    "The audio-level settings group is required.");
            }

            if (profile.InputComfort == null)
            {
                return Invalid(
                    "missing-input-comfort",
                    "The input-comfort settings group is required.");
            }

            Stage1AccessibilityValidationResult result = ValidateReducedEffects(
                profile.ReducedEffects);
            if (!result.IsValid)
            {
                return result;
            }

            result = ValidateFlashAndShake(profile.FlashAndShake);
            if (!result.IsValid)
            {
                return result;
            }

            result = ValidateAudio(profile.AudioLevels);
            if (!result.IsValid)
            {
                return result;
            }

            result = ValidateInputComfort(profile.InputComfort);
            if (!result.IsValid)
            {
                return result;
            }

            result = ValidateWarningRedundancy(
                profile.WarningRedundancy,
                profile.AudioLevels);
            if (!result.IsValid)
            {
                return result;
            }

            return Stage1AccessibilityValidationResult.Valid();
        }

        private static Stage1AccessibilityValidationResult ValidateReducedEffects(
            ReducedEffectsSettings settings)
        {
            if (!IsPercent(settings.NonEssentialEffectIntensityPercent))
            {
                return Invalid(
                    "invalid-reduced-effects-range",
                    "Non-essential effect intensity must be between 0 and 100 percent.");
            }

            return Stage1AccessibilityValidationResult.Valid();
        }

        private static Stage1AccessibilityValidationResult ValidateFlashAndShake(
            FlashAndShakeSettings settings)
        {
            if (settings.MaxFlashesPerSecond < 0
                || settings.MaxFlashesPerSecond > MaximumFlashesPerSecond)
            {
                return Invalid(
                    "invalid-flash-frequency",
                    "Maximum flashes per second must be between 0 and 3.");
            }

            if (!IsPercent(settings.FlashIntensityPercent))
            {
                return Invalid(
                    "invalid-flash-intensity",
                    "Flash intensity must be between 0 and 100 percent.");
            }

            if (!IsPercent(settings.CameraShakeIntensityPercent))
            {
                return Invalid(
                    "invalid-shake-intensity",
                    "Camera shake intensity must be between 0 and 100 percent.");
            }

            if (settings.CameraShakeDurationMilliseconds < 0
                || settings.CameraShakeDurationMilliseconds
                    > MaximumCameraShakeDurationMilliseconds)
            {
                return Invalid(
                    "invalid-shake-duration",
                    "Camera shake duration must be between 0 and 2000 milliseconds.");
            }

            return Stage1AccessibilityValidationResult.Valid();
        }

        private static Stage1AccessibilityValidationResult ValidateAudio(
            AudioLevelSettings settings)
        {
            if (!IsPercent(settings.MasterPercent)
                || !IsPercent(settings.EffectsPercent)
                || !IsPercent(settings.MusicPercent)
                || !IsPercent(settings.WarningPercent))
            {
                return Invalid(
                    "invalid-audio-range",
                    "Every audio level must be between 0 and 100 percent.");
            }

            return Stage1AccessibilityValidationResult.Valid();
        }

        private static Stage1AccessibilityValidationResult ValidateInputComfort(
            InputComfortSettings settings)
        {
            if (!Enum.IsDefined(typeof(HoldActionMode), settings.HoldActionMode))
            {
                return Invalid(
                    "unsupported-hold-action-mode",
                    "Hold action mode must be Hold or Toggle.");
            }

            if (!IsPercent(settings.AimAssistPercent))
            {
                return Invalid(
                    "invalid-aim-assist-range",
                    "Aim assistance must be between 0 and 100 percent.");
            }

            if (settings.AimSensitivityPercent < MinimumAimSensitivityPercent
                || settings.AimSensitivityPercent > MaximumAimSensitivityPercent)
            {
                return Invalid(
                    "invalid-aim-sensitivity-range",
                    "Aim sensitivity must be between 25 and 300 percent.");
            }

            if (settings.InputBufferMilliseconds < 0
                || settings.InputBufferMilliseconds > MaximumInputBufferMilliseconds)
            {
                return Invalid(
                    "invalid-input-buffer-range",
                    "Input buffering must be between 0 and 250 milliseconds.");
            }

            if (settings.RepeatDelayMilliseconds < MinimumRepeatDelayMilliseconds
                || settings.RepeatDelayMilliseconds > MaximumRepeatDelayMilliseconds)
            {
                return Invalid(
                    "invalid-repeat-delay-range",
                    "Repeat delay must be between 100 and 1000 milliseconds.");
            }

            return Stage1AccessibilityValidationResult.Valid();
        }

        private static Stage1AccessibilityValidationResult ValidateWarningRedundancy(
            WarningRedundancySettings warnings,
            AudioLevelSettings audio)
        {
            if (!warnings.ShapeOrIconCueEnabled && !warnings.TextCueEnabled)
            {
                return Invalid(
                    "missing-color-independent-warning",
                    "Every warning requires a shape/icon or text cue that does not depend on color.");
            }

            int effectiveChannelCount = 0;
            if (warnings.ColorCueEnabled)
            {
                effectiveChannelCount++;
            }

            if (warnings.ShapeOrIconCueEnabled)
            {
                effectiveChannelCount++;
            }

            if (warnings.TextCueEnabled)
            {
                effectiveChannelCount++;
            }

            bool audioIsAudible = audio.MasterPercent > 0 && audio.WarningPercent > 0;
            if (warnings.AudioCueEnabled && audioIsAudible)
            {
                effectiveChannelCount++;
            }

            if (effectiveChannelCount < 2)
            {
                return Invalid(
                    "insufficient-warning-redundancy",
                    "Warnings require at least two effective channels, including one color-independent visual channel.");
            }

            return Stage1AccessibilityValidationResult.Valid();
        }

        private static bool IsPercent(int value)
        {
            return value >= MinimumPercent && value <= MaximumPercent;
        }

        private static Stage1AccessibilityValidationResult Invalid(
            string errorCode,
            string errorMessage)
        {
            return Stage1AccessibilityValidationResult.Invalid(errorCode, errorMessage);
        }
    }
}

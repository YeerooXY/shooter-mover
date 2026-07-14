using System;

namespace ShooterMover.Application.Accessibility
{
    /// <summary>
    /// Device-independent preference for actions that can be operated either by holding
    /// an input or by pressing once to latch and pressing again to release.
    /// </summary>
    public enum HoldActionMode
    {
        Hold = 1,
        Toggle = 2,
    }

    public sealed class ReducedEffectsSettings : IEquatable<ReducedEffectsSettings>
    {
        public ReducedEffectsSettings(
            bool isEnabled,
            int nonEssentialEffectIntensityPercent,
            bool screenDistortionEnabled,
            bool motionTrailsEnabled)
        {
            IsEnabled = isEnabled;
            NonEssentialEffectIntensityPercent = nonEssentialEffectIntensityPercent;
            ScreenDistortionEnabled = screenDistortionEnabled;
            MotionTrailsEnabled = motionTrailsEnabled;
        }

        public bool IsEnabled { get; }

        public int NonEssentialEffectIntensityPercent { get; }

        public bool ScreenDistortionEnabled { get; }

        public bool MotionTrailsEnabled { get; }

        public bool Equals(ReducedEffectsSettings other)
        {
            return other != null
                && IsEnabled == other.IsEnabled
                && NonEssentialEffectIntensityPercent == other.NonEssentialEffectIntensityPercent
                && ScreenDistortionEnabled == other.ScreenDistortionEnabled
                && MotionTrailsEnabled == other.MotionTrailsEnabled;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ReducedEffectsSettings);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsEnabled ? 1 : 0;
                hash = (hash * 397) ^ NonEssentialEffectIntensityPercent;
                hash = (hash * 397) ^ (ScreenDistortionEnabled ? 1 : 0);
                hash = (hash * 397) ^ (MotionTrailsEnabled ? 1 : 0);
                return hash;
            }
        }
    }

    public sealed class FlashAndShakeSettings : IEquatable<FlashAndShakeSettings>
    {
        public FlashAndShakeSettings(
            int maxFlashesPerSecond,
            int flashIntensityPercent,
            int cameraShakeIntensityPercent,
            int cameraShakeDurationMilliseconds)
        {
            MaxFlashesPerSecond = maxFlashesPerSecond;
            FlashIntensityPercent = flashIntensityPercent;
            CameraShakeIntensityPercent = cameraShakeIntensityPercent;
            CameraShakeDurationMilliseconds = cameraShakeDurationMilliseconds;
        }

        public int MaxFlashesPerSecond { get; }

        public int FlashIntensityPercent { get; }

        public int CameraShakeIntensityPercent { get; }

        public int CameraShakeDurationMilliseconds { get; }

        public bool Equals(FlashAndShakeSettings other)
        {
            return other != null
                && MaxFlashesPerSecond == other.MaxFlashesPerSecond
                && FlashIntensityPercent == other.FlashIntensityPercent
                && CameraShakeIntensityPercent == other.CameraShakeIntensityPercent
                && CameraShakeDurationMilliseconds == other.CameraShakeDurationMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FlashAndShakeSettings);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MaxFlashesPerSecond;
                hash = (hash * 397) ^ FlashIntensityPercent;
                hash = (hash * 397) ^ CameraShakeIntensityPercent;
                hash = (hash * 397) ^ CameraShakeDurationMilliseconds;
                return hash;
            }
        }
    }

    public sealed class WarningRedundancySettings : IEquatable<WarningRedundancySettings>
    {
        public WarningRedundancySettings(
            bool colorCueEnabled,
            bool shapeOrIconCueEnabled,
            bool textCueEnabled,
            bool audioCueEnabled)
        {
            ColorCueEnabled = colorCueEnabled;
            ShapeOrIconCueEnabled = shapeOrIconCueEnabled;
            TextCueEnabled = textCueEnabled;
            AudioCueEnabled = audioCueEnabled;
        }

        public bool ColorCueEnabled { get; }

        public bool ShapeOrIconCueEnabled { get; }

        public bool TextCueEnabled { get; }

        public bool AudioCueEnabled { get; }

        public bool Equals(WarningRedundancySettings other)
        {
            return other != null
                && ColorCueEnabled == other.ColorCueEnabled
                && ShapeOrIconCueEnabled == other.ShapeOrIconCueEnabled
                && TextCueEnabled == other.TextCueEnabled
                && AudioCueEnabled == other.AudioCueEnabled;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WarningRedundancySettings);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ColorCueEnabled ? 1 : 0;
                hash = (hash * 397) ^ (ShapeOrIconCueEnabled ? 1 : 0);
                hash = (hash * 397) ^ (TextCueEnabled ? 1 : 0);
                hash = (hash * 397) ^ (AudioCueEnabled ? 1 : 0);
                return hash;
            }
        }
    }

    public sealed class AudioLevelSettings : IEquatable<AudioLevelSettings>
    {
        public AudioLevelSettings(
            int masterPercent,
            int effectsPercent,
            int musicPercent,
            int warningPercent)
        {
            MasterPercent = masterPercent;
            EffectsPercent = effectsPercent;
            MusicPercent = musicPercent;
            WarningPercent = warningPercent;
        }

        public int MasterPercent { get; }

        public int EffectsPercent { get; }

        public int MusicPercent { get; }

        public int WarningPercent { get; }

        public bool Equals(AudioLevelSettings other)
        {
            return other != null
                && MasterPercent == other.MasterPercent
                && EffectsPercent == other.EffectsPercent
                && MusicPercent == other.MusicPercent
                && WarningPercent == other.WarningPercent;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AudioLevelSettings);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MasterPercent;
                hash = (hash * 397) ^ EffectsPercent;
                hash = (hash * 397) ^ MusicPercent;
                hash = (hash * 397) ^ WarningPercent;
                return hash;
            }
        }
    }

    public sealed class InputComfortSettings : IEquatable<InputComfortSettings>
    {
        public InputComfortSettings(
            HoldActionMode holdActionMode,
            int aimAssistPercent,
            int aimSensitivityPercent,
            int inputBufferMilliseconds,
            int repeatDelayMilliseconds)
        {
            HoldActionMode = holdActionMode;
            AimAssistPercent = aimAssistPercent;
            AimSensitivityPercent = aimSensitivityPercent;
            InputBufferMilliseconds = inputBufferMilliseconds;
            RepeatDelayMilliseconds = repeatDelayMilliseconds;
        }

        public HoldActionMode HoldActionMode { get; }

        public int AimAssistPercent { get; }

        public int AimSensitivityPercent { get; }

        public int InputBufferMilliseconds { get; }

        public int RepeatDelayMilliseconds { get; }

        public bool Equals(InputComfortSettings other)
        {
            return other != null
                && HoldActionMode == other.HoldActionMode
                && AimAssistPercent == other.AimAssistPercent
                && AimSensitivityPercent == other.AimSensitivityPercent
                && InputBufferMilliseconds == other.InputBufferMilliseconds
                && RepeatDelayMilliseconds == other.RepeatDelayMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as InputComfortSettings);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)HoldActionMode;
                hash = (hash * 397) ^ AimAssistPercent;
                hash = (hash * 397) ^ AimSensitivityPercent;
                hash = (hash * 397) ^ InputBufferMilliseconds;
                hash = (hash * 397) ^ RepeatDelayMilliseconds;
                return hash;
            }
        }
    }

    /// <summary>
    /// Immutable, device-independent Stage 1 accessibility profile. Construction is
    /// intentionally lossless; consumers must validate before applying a profile.
    /// </summary>
    public sealed class Stage1AccessibilityProfile : IEquatable<Stage1AccessibilityProfile>
    {
        public const string ProfileId = "shooter-mover.stage1-accessibility-profile";
        public const int CurrentVersion = 1;

        public Stage1AccessibilityProfile(
            int version,
            ReducedEffectsSettings reducedEffects,
            FlashAndShakeSettings flashAndShake,
            WarningRedundancySettings warningRedundancy,
            AudioLevelSettings audioLevels,
            InputComfortSettings inputComfort)
        {
            Version = version;
            ReducedEffects = reducedEffects;
            FlashAndShake = flashAndShake;
            WarningRedundancy = warningRedundancy;
            AudioLevels = audioLevels;
            InputComfort = inputComfort;
        }

        public int Version { get; }

        public ReducedEffectsSettings ReducedEffects { get; }

        public FlashAndShakeSettings FlashAndShake { get; }

        public WarningRedundancySettings WarningRedundancy { get; }

        public AudioLevelSettings AudioLevels { get; }

        public InputComfortSettings InputComfort { get; }

        public static Stage1AccessibilityProfile CreateDefault()
        {
            return new Stage1AccessibilityProfile(
                CurrentVersion,
                new ReducedEffectsSettings(
                    true,
                    70,
                    false,
                    false),
                new FlashAndShakeSettings(
                    3,
                    50,
                    35,
                    350),
                new WarningRedundancySettings(
                    true,
                    true,
                    false,
                    true),
                new AudioLevelSettings(
                    80,
                    80,
                    60,
                    100),
                new InputComfortSettings(
                    HoldActionMode.Hold,
                    15,
                    100,
                    80,
                    350));
        }

        public bool Equals(Stage1AccessibilityProfile other)
        {
            return other != null
                && Version == other.Version
                && Equals(ReducedEffects, other.ReducedEffects)
                && Equals(FlashAndShake, other.FlashAndShake)
                && Equals(WarningRedundancy, other.WarningRedundancy)
                && Equals(AudioLevels, other.AudioLevels)
                && Equals(InputComfort, other.InputComfort);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1AccessibilityProfile);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Version;
                hash = (hash * 397) ^ (ReducedEffects == null ? 0 : ReducedEffects.GetHashCode());
                hash = (hash * 397) ^ (FlashAndShake == null ? 0 : FlashAndShake.GetHashCode());
                hash = (hash * 397) ^ (WarningRedundancy == null ? 0 : WarningRedundancy.GetHashCode());
                hash = (hash * 397) ^ (AudioLevels == null ? 0 : AudioLevels.GetHashCode());
                hash = (hash * 397) ^ (InputComfort == null ? 0 : InputComfort.GetHashCode());
                return hash;
            }
        }
    }
}

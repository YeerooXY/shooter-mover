using System;
using NUnit.Framework;
using ShooterMover.Application.Accessibility;

namespace ShooterMover.Tests.EditMode.Accessibility
{
    public sealed class Stage1AccessibilityProfileTests
    {
        [Test]
        public void CanonicalDefaults_AreVersionOnePracticalAndValid()
        {
            Stage1AccessibilityProfile profile = Stage1AccessibilityProfile.CreateDefault();
            Stage1AccessibilityValidationResult result =
                Stage1AccessibilityProfileValidator.Validate(profile);

            Assert.That(profile.Version, Is.EqualTo(Stage1AccessibilityProfile.CurrentVersion));
            Assert.That(profile.ReducedEffects.IsEnabled, Is.True);
            Assert.That(profile.ReducedEffects.NonEssentialEffectIntensityPercent, Is.EqualTo(70));
            Assert.That(profile.ReducedEffects.ScreenDistortionEnabled, Is.False);
            Assert.That(profile.ReducedEffects.MotionTrailsEnabled, Is.False);
            Assert.That(profile.FlashAndShake.MaxFlashesPerSecond, Is.EqualTo(3));
            Assert.That(profile.FlashAndShake.FlashIntensityPercent, Is.EqualTo(50));
            Assert.That(profile.FlashAndShake.CameraShakeIntensityPercent, Is.EqualTo(35));
            Assert.That(profile.FlashAndShake.CameraShakeDurationMilliseconds, Is.EqualTo(350));
            Assert.That(profile.WarningRedundancy.ColorCueEnabled, Is.True);
            Assert.That(profile.WarningRedundancy.ShapeOrIconCueEnabled, Is.True);
            Assert.That(profile.WarningRedundancy.TextCueEnabled, Is.False);
            Assert.That(profile.WarningRedundancy.AudioCueEnabled, Is.True);
            Assert.That(profile.AudioLevels.MasterPercent, Is.EqualTo(80));
            Assert.That(profile.AudioLevels.EffectsPercent, Is.EqualTo(80));
            Assert.That(profile.AudioLevels.MusicPercent, Is.EqualTo(60));
            Assert.That(profile.AudioLevels.WarningPercent, Is.EqualTo(100));
            Assert.That(profile.InputComfort.HoldActionMode, Is.EqualTo(HoldActionMode.Hold));
            Assert.That(profile.InputComfort.AimAssistPercent, Is.EqualTo(15));
            Assert.That(profile.InputComfort.AimSensitivityPercent, Is.EqualTo(100));
            Assert.That(profile.InputComfort.InputBufferMilliseconds, Is.EqualTo(80));
            Assert.That(profile.InputComfort.RepeatDelayMilliseconds, Is.EqualTo(350));
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ErrorCode, Is.Null);
            Assert.That(result.ErrorMessage, Is.Null);
        }

        [Test]
        public void ValidationBoundaries_AreInclusive()
        {
            Stage1AccessibilityProfile minimums = new Stage1AccessibilityProfile(
                1,
                new ReducedEffectsSettings(true, 0, false, false),
                new FlashAndShakeSettings(0, 0, 0, 0),
                new WarningRedundancySettings(true, true, false, false),
                new AudioLevelSettings(0, 0, 0, 0),
                new InputComfortSettings(HoldActionMode.Hold, 0, 25, 0, 100));
            Stage1AccessibilityProfile maximums = new Stage1AccessibilityProfile(
                1,
                new ReducedEffectsSettings(false, 100, true, true),
                new FlashAndShakeSettings(3, 100, 100, 2000),
                new WarningRedundancySettings(true, true, true, true),
                new AudioLevelSettings(100, 100, 100, 100),
                new InputComfortSettings(HoldActionMode.Toggle, 100, 300, 250, 1000));

            Assert.That(Stage1AccessibilityProfileValidator.Validate(minimums).IsValid, Is.True);
            Assert.That(Stage1AccessibilityProfileValidator.Validate(maximums).IsValid, Is.True);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void UnknownVersions_FailWithOneStableError(int version)
        {
            Stage1AccessibilityProfile defaults = Stage1AccessibilityProfile.CreateDefault();
            Stage1AccessibilityProfile profile = new Stage1AccessibilityProfile(
                version,
                defaults.ReducedEffects,
                defaults.FlashAndShake,
                defaults.WarningRedundancy,
                defaults.AudioLevels,
                defaults.InputComfort);

            AssertInvalid(profile, "unsupported-version");
        }

        [Test]
        public void NullProfileAndMissingRequiredSettings_FailInCanonicalOrder()
        {
            Stage1AccessibilityProfile defaults = Stage1AccessibilityProfile.CreateDefault();

            AssertInvalid(null, "missing-profile");
            AssertInvalid(
                new Stage1AccessibilityProfile(
                    1,
                    null,
                    defaults.FlashAndShake,
                    defaults.WarningRedundancy,
                    defaults.AudioLevels,
                    defaults.InputComfort),
                "missing-reduced-effects");
            AssertInvalid(
                new Stage1AccessibilityProfile(
                    1,
                    defaults.ReducedEffects,
                    null,
                    defaults.WarningRedundancy,
                    defaults.AudioLevels,
                    defaults.InputComfort),
                "missing-flash-and-shake");
            AssertInvalid(
                new Stage1AccessibilityProfile(
                    1,
                    defaults.ReducedEffects,
                    defaults.FlashAndShake,
                    null,
                    defaults.AudioLevels,
                    defaults.InputComfort),
                "missing-warning-redundancy");
            AssertInvalid(
                new Stage1AccessibilityProfile(
                    1,
                    defaults.ReducedEffects,
                    defaults.FlashAndShake,
                    defaults.WarningRedundancy,
                    null,
                    defaults.InputComfort),
                "missing-audio-levels");
            AssertInvalid(
                new Stage1AccessibilityProfile(
                    1,
                    defaults.ReducedEffects,
                    defaults.FlashAndShake,
                    defaults.WarningRedundancy,
                    defaults.AudioLevels,
                    null),
                "missing-input-comfort");
        }

        [Test]
        public void InvalidEffectFlashShakeAndAudioRanges_FailDeterministically()
        {
            Stage1AccessibilityProfile defaults = Stage1AccessibilityProfile.CreateDefault();

            AssertInvalid(
                ReplaceReducedEffects(defaults, new ReducedEffectsSettings(true, -1, false, false)),
                "invalid-reduced-effects-range");
            AssertInvalid(
                ReplaceReducedEffects(defaults, new ReducedEffectsSettings(true, 101, false, false)),
                "invalid-reduced-effects-range");
            AssertInvalid(
                ReplaceFlashAndShake(defaults, new FlashAndShakeSettings(-1, 50, 35, 350)),
                "invalid-flash-frequency");
            AssertInvalid(
                ReplaceFlashAndShake(defaults, new FlashAndShakeSettings(4, 50, 35, 350)),
                "invalid-flash-frequency");
            AssertInvalid(
                ReplaceFlashAndShake(defaults, new FlashAndShakeSettings(3, 101, 35, 350)),
                "invalid-flash-intensity");
            AssertInvalid(
                ReplaceFlashAndShake(defaults, new FlashAndShakeSettings(3, 50, -1, 350)),
                "invalid-shake-intensity");
            AssertInvalid(
                ReplaceFlashAndShake(defaults, new FlashAndShakeSettings(3, 50, 35, 2001)),
                "invalid-shake-duration");
            AssertInvalid(
                ReplaceAudio(defaults, new AudioLevelSettings(80, 101, 60, 100)),
                "invalid-audio-range");
            AssertInvalid(
                ReplaceAudio(defaults, new AudioLevelSettings(-1, 80, 60, 100)),
                "invalid-audio-range");
        }

        [Test]
        public void InvalidInputComfortValues_FailDeterministically()
        {
            Stage1AccessibilityProfile defaults = Stage1AccessibilityProfile.CreateDefault();

            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings((HoldActionMode)0, 15, 100, 80, 350)),
                "unsupported-hold-action-mode");
            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings(HoldActionMode.Hold, -1, 100, 80, 350)),
                "invalid-aim-assist-range");
            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings(HoldActionMode.Hold, 15, 24, 80, 350)),
                "invalid-aim-sensitivity-range");
            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings(HoldActionMode.Hold, 15, 301, 80, 350)),
                "invalid-aim-sensitivity-range");
            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings(HoldActionMode.Hold, 15, 100, -1, 350)),
                "invalid-input-buffer-range");
            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings(HoldActionMode.Hold, 15, 100, 251, 350)),
                "invalid-input-buffer-range");
            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings(HoldActionMode.Hold, 15, 100, 80, 99)),
                "invalid-repeat-delay-range");
            AssertInvalid(
                ReplaceInput(defaults, new InputComfortSettings(HoldActionMode.Hold, 15, 100, 80, 1001)),
                "invalid-repeat-delay-range");
        }

        [Test]
        public void Warnings_CannotDependOnColorOrOneIneffectiveChannel()
        {
            Stage1AccessibilityProfile defaults = Stage1AccessibilityProfile.CreateDefault();

            AssertInvalid(
                ReplaceWarnings(
                    defaults,
                    new WarningRedundancySettings(true, false, false, true)),
                "missing-color-independent-warning");
            AssertInvalid(
                ReplaceWarnings(
                    defaults,
                    new WarningRedundancySettings(false, true, false, false)),
                "insufficient-warning-redundancy");

            Stage1AccessibilityProfile mutedAudio = ReplaceAudio(
                ReplaceWarnings(
                    defaults,
                    new WarningRedundancySettings(false, true, false, true)),
                new AudioLevelSettings(80, 80, 60, 0));
            AssertInvalid(mutedAudio, "insufficient-warning-redundancy");
        }

        [Test]
        public void Warnings_RemainValidWithAudibleOrFullyVisualRedundancy()
        {
            Stage1AccessibilityProfile defaults = Stage1AccessibilityProfile.CreateDefault();
            Stage1AccessibilityProfile iconAndAudio = ReplaceWarnings(
                defaults,
                new WarningRedundancySettings(false, true, false, true));
            Stage1AccessibilityProfile silentButVisual = ReplaceAudio(
                ReplaceWarnings(
                    defaults,
                    new WarningRedundancySettings(false, true, true, false)),
                new AudioLevelSettings(0, 0, 0, 0));

            Assert.That(Stage1AccessibilityProfileValidator.Validate(iconAndAudio).IsValid, Is.True);
            Assert.That(Stage1AccessibilityProfileValidator.Validate(silentButVisual).IsValid, Is.True);
        }

        [Test]
        public void ProfilesAndNestedSettings_AreImmutableValueObjects()
        {
            Type[] types =
            {
                typeof(Stage1AccessibilityProfile),
                typeof(ReducedEffectsSettings),
                typeof(FlashAndShakeSettings),
                typeof(WarningRedundancySettings),
                typeof(AudioLevelSettings),
                typeof(InputComfortSettings),
                typeof(Stage1AccessibilityValidationResult),
            };

            foreach (Type type in types)
            {
                Assert.That(type.IsSealed, Is.True, type.FullName + " must be sealed.");
                foreach (System.Reflection.PropertyInfo property in type.GetProperties())
                {
                    Assert.That(
                        property.CanWrite,
                        Is.False,
                        type.FullName + "." + property.Name + " must be get-only.");
                }
            }

            Stage1AccessibilityProfile first = Stage1AccessibilityProfile.CreateDefault();
            Stage1AccessibilityProfile second = Stage1AccessibilityProfile.CreateDefault();

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first.ReducedEffects, Is.Not.SameAs(second.ReducedEffects));
        }

        private static Stage1AccessibilityProfile ReplaceReducedEffects(
            Stage1AccessibilityProfile source,
            ReducedEffectsSettings settings)
        {
            return new Stage1AccessibilityProfile(
                source.Version,
                settings,
                source.FlashAndShake,
                source.WarningRedundancy,
                source.AudioLevels,
                source.InputComfort);
        }

        private static Stage1AccessibilityProfile ReplaceFlashAndShake(
            Stage1AccessibilityProfile source,
            FlashAndShakeSettings settings)
        {
            return new Stage1AccessibilityProfile(
                source.Version,
                source.ReducedEffects,
                settings,
                source.WarningRedundancy,
                source.AudioLevels,
                source.InputComfort);
        }

        private static Stage1AccessibilityProfile ReplaceWarnings(
            Stage1AccessibilityProfile source,
            WarningRedundancySettings settings)
        {
            return new Stage1AccessibilityProfile(
                source.Version,
                source.ReducedEffects,
                source.FlashAndShake,
                settings,
                source.AudioLevels,
                source.InputComfort);
        }

        private static Stage1AccessibilityProfile ReplaceAudio(
            Stage1AccessibilityProfile source,
            AudioLevelSettings settings)
        {
            return new Stage1AccessibilityProfile(
                source.Version,
                source.ReducedEffects,
                source.FlashAndShake,
                source.WarningRedundancy,
                settings,
                source.InputComfort);
        }

        private static Stage1AccessibilityProfile ReplaceInput(
            Stage1AccessibilityProfile source,
            InputComfortSettings settings)
        {
            return new Stage1AccessibilityProfile(
                source.Version,
                source.ReducedEffects,
                source.FlashAndShake,
                source.WarningRedundancy,
                source.AudioLevels,
                settings);
        }

        private static void AssertInvalid(
            Stage1AccessibilityProfile profile,
            string expectedErrorCode)
        {
            Stage1AccessibilityValidationResult result =
                Stage1AccessibilityProfileValidator.Validate(profile);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(expectedErrorCode));
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }
    }
}

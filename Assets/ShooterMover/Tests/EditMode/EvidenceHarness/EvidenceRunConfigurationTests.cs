using System;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Input;

namespace ShooterMover.Tests.EditMode.EvidenceHarness
{
    public sealed class EvidenceRunConfigurationTests
    {
        private const string LoaderTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceRunConfigurationLoader";
        private const string FixtureTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceIntentFixture";
        private const string IdentityReference =
            "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private const string CanonicalJson =
            "{\n"
            + "  \"schema\": \"shooter-mover.evidence-run-configuration\",\n"
            + "  \"version\": 1,\n"
            + "  \"runSeed\": 104729,\n"
            + "  \"identityReference\": \"" + IdentityReference + "\",\n"
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

        [Test]
        public void CanonicalFixture_RoundTripsWithoutByteChanges()
        {
            object result = Load(CanonicalJson);
            AssertValid(result);

            object configuration = GetPropertyValue<object>(result, "Configuration");
            string serialized = InvokeString(configuration, "ToCanonicalJson");

            Assert.That(serialized, Is.EqualTo(CanonicalJson));
            Assert.That(GetPropertyValue<string>(configuration, "Schema"),
                Is.EqualTo("shooter-mover.evidence-run-configuration"));
            Assert.That(GetPropertyValue<int>(configuration, "Version"), Is.EqualTo(1));
            Assert.That(GetPropertyValue<int>(configuration, "RunSeed"), Is.EqualTo(104729));
            Assert.That(
                GetPropertyValue<string>(configuration, "IdentityReference"),
                Is.EqualTo(IdentityReference));
        }

        [Test]
        public void Serializer_UsesTheDocumentedCanonicalFieldOrder()
        {
            object configuration = GetConfiguration(Load(CanonicalJson));
            string[] lines = InvokeString(configuration, "ToCanonicalJson").Split('\n');

            Assert.That(lines, Has.Length.EqualTo(26));
            Assert.That(lines[0], Is.EqualTo("{"));
            Assert.That(lines[1], Does.StartWith("  \"schema\":"));
            Assert.That(lines[2], Does.StartWith("  \"version\":"));
            Assert.That(lines[3], Does.StartWith("  \"runSeed\":"));
            Assert.That(lines[4], Does.StartWith("  \"identityReference\":"));
            Assert.That(lines[5], Does.StartWith("  \"intentFixtureVersion\":"));
            Assert.That(lines[6], Does.StartWith("  \"qualityProfile\":"));
            Assert.That(lines[7], Does.StartWith("  \"locale\":"));
            Assert.That(lines[8], Is.EqualTo("  \"viewport\": {"));
            Assert.That(lines[13], Is.EqualTo("  \"diagnostics\": {"));
            Assert.That(lines[19], Is.EqualTo("  \"timeouts\": {"));
            Assert.That(lines[24], Is.EqualTo("}"));
            Assert.That(lines[25], Is.Empty);
        }

        [Test]
        public void SameSeedAndFrozenSetup_ProduceTheSameFingerprint()
        {
            object first = GetConfiguration(Load(CanonicalJson));
            object second = GetConfiguration(Load(CanonicalJson));

            Assert.That(
                GetPropertyValue<string>(first, "Fingerprint"),
                Is.EqualTo(GetPropertyValue<string>(second, "Fingerprint")));
            Assert.That(
                InvokeString(first, "ToCanonicalJson"),
                Is.EqualTo(InvokeString(second, "ToCanonicalJson")));
        }

        [Test]
        public void ChangedSeed_ChangesTheConfigurationFingerprint()
        {
            string changedJson = CanonicalJson.Replace(
                "  \"runSeed\": 104729,",
                "  \"runSeed\": 104730,");

            object baseline = GetConfiguration(Load(CanonicalJson));
            object changed = GetConfiguration(Load(changedJson));

            Assert.That(GetPropertyValue<int>(changed, "RunSeed"), Is.EqualTo(104730));
            Assert.That(
                GetPropertyValue<string>(changed, "Fingerprint"),
                Is.Not.EqualTo(GetPropertyValue<string>(baseline, "Fingerprint")));
        }

        [Test]
        public void UnknownMissingDuplicateAndReorderedFields_AreRejected()
        {
            string unknown = CanonicalJson.Replace(
                "  \"timeouts\": {",
                "  \"unknownField\": 1,\n  \"timeouts\": {");
            string missing = CanonicalJson.Replace(
                "  \"locale\": \"en-US\",\n",
                string.Empty);
            string duplicate = CanonicalJson.Replace(
                "  \"runSeed\": 104729,\n",
                "  \"runSeed\": 104729,\n  \"runSeed\": 104729,\n");
            string reordered = CanonicalJson.Replace(
                "  \"qualityProfile\": \"Medium\",\n  \"locale\": \"en-US\",",
                "  \"locale\": \"en-US\",\n  \"qualityProfile\": \"Medium\",");

            AssertInvalid(Load(unknown));
            AssertInvalid(Load(missing));
            AssertInvalid(Load(duplicate));
            AssertInvalid(Load(reordered), "non-canonical-field-order");
        }

        [Test]
        public void InvalidBoundsUnsupportedVersionsAndMachineLocalValues_AreRejected()
        {
            AssertInvalid(
                Load(CanonicalJson.Replace("  \"runSeed\": 104729,", "  \"runSeed\": 0,")),
                "invalid-run-seed");
            AssertInvalid(
                Load(CanonicalJson.Replace("    \"width\": 1280,", "    \"width\": 319,")),
                "invalid-viewport");
            AssertInvalid(
                Load(CanonicalJson.Replace(
                    "    \"maxEventPayloadBytes\": 4096,",
                    "    \"maxEventPayloadBytes\": 64,")),
                "invalid-diagnostics-bound");
            AssertInvalid(
                Load(CanonicalJson.Replace(
                    "    \"setupSeconds\": 30,",
                    "    \"setupSeconds\": 0,")),
                "invalid-timeout-bound");
            AssertInvalid(
                Load(CanonicalJson.Replace(
                    "  \"intentFixtureVersion\": 1,",
                    "  \"intentFixtureVersion\": 2,")),
                "unsupported-intent-fixture-version");
            AssertInvalid(
                Load(CanonicalJson.Replace(
                    "  \"qualityProfile\": \"Medium\",",
                    "  \"qualityProfile\": \"C:Medium\",")),
                "machine-local-value");
            AssertInvalid(
                Load(CanonicalJson.Replace(
                    "    \"fullscreen\": false",
                    "    \"fullscreen\": true")),
                "machine-local-value");
            AssertInvalid(
                Load(CanonicalJson.Replace(
                    IdentityReference,
                    "sha256:0000000000000000000000000000000000000000000000000000000000000000")),
                "invalid-identity-reference");
        }

        [Test]
        public void NonCanonicalLineEndingsAndTrailingContent_AreRejected()
        {
            AssertInvalid(
                Load(CanonicalJson.Replace("\n", "\r\n")),
                "non-canonical-line-endings");
            AssertInvalid(Load(CanonicalJson + " "), "non-canonical-field-count");
        }

        [Test]
        public void KeyboardMouseAndGamepadFixtures_ResolveToSameCs003Intents()
        {
            Type fixtureType = FindType(FixtureTypeName);
            MethodInfo equivalenceMethod = fixtureType.GetMethod(
                "AreDeviceFixturesEquivalent",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo keyboardMethod = fixtureType.GetMethod(
                "ResolveKeyboardMouse",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo gamepadMethod = fixtureType.GetMethod(
                "ResolveGamepad",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(equivalenceMethod, Is.Not.Null);
            Assert.That(keyboardMethod, Is.Not.Null);
            Assert.That(gamepadMethod, Is.Not.Null);
            Assert.That((bool)equivalenceMethod.Invoke(null, new object[] { 1 }), Is.True);

            PlayerIntentFrame[] keyboard =
                (PlayerIntentFrame[])keyboardMethod.Invoke(null, new object[] { 1 });
            PlayerIntentFrame[] gamepad =
                (PlayerIntentFrame[])gamepadMethod.Invoke(null, new object[] { 1 });

            Assert.That(keyboard, Has.Length.EqualTo(5));
            Assert.That(gamepad, Has.Length.EqualTo(keyboard.Length));
            Assert.That(keyboard[1].Fire, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(keyboard[2].Thruster, Is.EqualTo(ButtonIntent.Pressed));
            Assert.That(keyboard[3].Interact, Is.EqualTo(ButtonIntent.Tap));
            Assert.That(keyboard[4].PauseMenu, Is.EqualTo(ButtonIntent.Tap));
            Assert.That(keyboard[2].Move, Is.EqualTo(gamepad[2].Move));
            Assert.That(keyboard[2].Aim, Is.EqualTo(gamepad[2].Aim));
        }

        [Test]
        public void LoadedConfigurationAndNestedValues_AreImmutable()
        {
            object configuration = GetConfiguration(Load(CanonicalJson));
            AssertReadOnlyPublicProperties(configuration.GetType());
            AssertReadOnlyPublicProperties(
                GetPropertyValue<object>(configuration, "Viewport").GetType());
            AssertReadOnlyPublicProperties(
                GetPropertyValue<object>(configuration, "Diagnostics").GetType());
            AssertReadOnlyPublicProperties(
                GetPropertyValue<object>(configuration, "Timeouts").GetType());
        }

        private static object Load(string json)
        {
            Type loaderType = FindType(LoaderTypeName);
            MethodInfo method = loaderType.GetMethod(
                "Load",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { json });
        }

        private static object GetConfiguration(object result)
        {
            AssertValid(result);
            return GetPropertyValue<object>(result, "Configuration");
        }

        private static void AssertValid(object result)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(GetPropertyValue<bool>(result, "IsValid"), Is.True);
            Assert.That(GetPropertyValue<object>(result, "Configuration"), Is.Not.Null);
            Assert.That(GetPropertyValue<string>(result, "ErrorCode"), Is.Null);
        }

        private static void AssertInvalid(object result, string expectedCode = null)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(GetPropertyValue<bool>(result, "IsValid"), Is.False);
            Assert.That(GetPropertyValue<object>(result, "Configuration"), Is.Null);
            Assert.That(GetPropertyValue<string>(result, "ErrorCode"), Is.Not.Empty);
            Assert.That(GetPropertyValue<string>(result, "ErrorMessage"), Is.Not.Empty);
            if (expectedCode != null)
            {
                Assert.That(GetPropertyValue<string>(result, "ErrorCode"), Is.EqualTo(expectedCode));
            }
        }

        private static void AssertReadOnlyPublicProperties(Type type)
        {
            Assert.That(type.IsSealed, Is.True, type.FullName);
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(property.CanWrite, Is.False, type.FullName + "." + property.Name);
            }
        }

        private static string InvokeString(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(target, null);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target, null);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail("Unable to find loaded type " + fullName + ".");
            return null;
        }
    }
}

using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Input;

namespace ShooterMover.TestSupport.EvidenceHarness
{
    public sealed class EvidenceViewport : IEquatable<EvidenceViewport>
    {
        internal EvidenceViewport(int width, int height, bool fullscreen)
        {
            Width = width;
            Height = height;
            Fullscreen = fullscreen;
        }

        public int Width { get; }

        public int Height { get; }

        public bool Fullscreen { get; }

        public bool Equals(EvidenceViewport other)
        {
            return !ReferenceEquals(other, null)
                && Width == other.Width
                && Height == other.Height
                && Fullscreen == other.Fullscreen;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvidenceViewport);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Width;
                hash = (hash * 397) ^ Height;
                hash = (hash * 397) ^ (Fullscreen ? 1 : 0);
                return hash;
            }
        }
    }

    public sealed class EvidenceDiagnosticsLimits : IEquatable<EvidenceDiagnosticsLimits>
    {
        internal EvidenceDiagnosticsLimits(
            int maxEventCount,
            int maxEventPayloadBytes,
            int maxLogBytes,
            int retainedLogCount)
        {
            MaxEventCount = maxEventCount;
            MaxEventPayloadBytes = maxEventPayloadBytes;
            MaxLogBytes = maxLogBytes;
            RetainedLogCount = retainedLogCount;
        }

        public int MaxEventCount { get; }

        public int MaxEventPayloadBytes { get; }

        public int MaxLogBytes { get; }

        public int RetainedLogCount { get; }

        public bool Equals(EvidenceDiagnosticsLimits other)
        {
            return !ReferenceEquals(other, null)
                && MaxEventCount == other.MaxEventCount
                && MaxEventPayloadBytes == other.MaxEventPayloadBytes
                && MaxLogBytes == other.MaxLogBytes
                && RetainedLogCount == other.RetainedLogCount;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvidenceDiagnosticsLimits);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MaxEventCount;
                hash = (hash * 397) ^ MaxEventPayloadBytes;
                hash = (hash * 397) ^ MaxLogBytes;
                hash = (hash * 397) ^ RetainedLogCount;
                return hash;
            }
        }
    }

    public sealed class EvidenceTimeoutLimits : IEquatable<EvidenceTimeoutLimits>
    {
        internal EvidenceTimeoutLimits(
            int setupSeconds,
            int smokeRunSeconds,
            int shutdownSeconds)
        {
            SetupSeconds = setupSeconds;
            SmokeRunSeconds = smokeRunSeconds;
            ShutdownSeconds = shutdownSeconds;
        }

        public int SetupSeconds { get; }

        public int SmokeRunSeconds { get; }

        public int ShutdownSeconds { get; }

        public bool Equals(EvidenceTimeoutLimits other)
        {
            return !ReferenceEquals(other, null)
                && SetupSeconds == other.SetupSeconds
                && SmokeRunSeconds == other.SmokeRunSeconds
                && ShutdownSeconds == other.ShutdownSeconds;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvidenceTimeoutLimits);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SetupSeconds;
                hash = (hash * 397) ^ SmokeRunSeconds;
                hash = (hash * 397) ^ ShutdownSeconds;
                return hash;
            }
        }
    }

    /// <summary>
    /// Immutable, versioned setup for one deterministic Stage 1 evidence run.
    /// </summary>
    public sealed class EvidenceRunConfiguration : IEquatable<EvidenceRunConfiguration>
    {
        public const string CurrentSchema = "shooter-mover.evidence-run-configuration";
        public const int CurrentVersion = 1;
        public const int CurrentIntentFixtureVersion = 1;
        public const string FingerprintPrefix = "sha256:";

        private readonly string canonicalJson;

        internal EvidenceRunConfiguration(
            int runSeed,
            string identityReference,
            int intentFixtureVersion,
            string qualityProfile,
            string locale,
            EvidenceViewport viewport,
            EvidenceDiagnosticsLimits diagnostics,
            EvidenceTimeoutLimits timeouts)
        {
            Schema = CurrentSchema;
            Version = CurrentVersion;
            RunSeed = runSeed;
            IdentityReference = identityReference;
            IntentFixtureVersion = intentFixtureVersion;
            QualityProfile = qualityProfile;
            Locale = locale;
            Viewport = viewport;
            Diagnostics = diagnostics;
            Timeouts = timeouts;

            canonicalJson = BuildCanonicalJson();
            Fingerprint = ComputeSha256(canonicalJson);
        }

        public string Schema { get; }

        public int Version { get; }

        public int RunSeed { get; }

        public string IdentityReference { get; }

        public int IntentFixtureVersion { get; }

        public string QualityProfile { get; }

        public string Locale { get; }

        public EvidenceViewport Viewport { get; }

        public EvidenceDiagnosticsLimits Diagnostics { get; }

        public EvidenceTimeoutLimits Timeouts { get; }

        public string Fingerprint { get; }

        public string ToCanonicalJson()
        {
            return canonicalJson;
        }

        public override string ToString()
        {
            return canonicalJson;
        }

        public bool Equals(EvidenceRunConfiguration other)
        {
            return !ReferenceEquals(other, null)
                && RunSeed == other.RunSeed
                && string.Equals(IdentityReference, other.IdentityReference, StringComparison.Ordinal)
                && IntentFixtureVersion == other.IntentFixtureVersion
                && string.Equals(QualityProfile, other.QualityProfile, StringComparison.Ordinal)
                && string.Equals(Locale, other.Locale, StringComparison.Ordinal)
                && Equals(Viewport, other.Viewport)
                && Equals(Diagnostics, other.Diagnostics)
                && Equals(Timeouts, other.Timeouts);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EvidenceRunConfiguration);
        }

        public override int GetHashCode()
        {
            return DeterministicHash(canonicalJson);
        }

        private string BuildCanonicalJson()
        {
            StringBuilder builder = new StringBuilder(768);
            builder.Append("{\n");
            AppendStringProperty(builder, 2, "schema", Schema, true);
            AppendIntegerProperty(builder, 2, "version", Version, true);
            AppendIntegerProperty(builder, 2, "runSeed", RunSeed, true);
            AppendStringProperty(builder, 2, "identityReference", IdentityReference, true);
            AppendIntegerProperty(
                builder,
                2,
                "intentFixtureVersion",
                IntentFixtureVersion,
                true);
            AppendStringProperty(builder, 2, "qualityProfile", QualityProfile, true);
            AppendStringProperty(builder, 2, "locale", Locale, true);

            builder.Append("  \"viewport\": {\n");
            AppendIntegerProperty(builder, 4, "width", Viewport.Width, true);
            AppendIntegerProperty(builder, 4, "height", Viewport.Height, true);
            AppendBooleanProperty(builder, 4, "fullscreen", Viewport.Fullscreen, false);
            builder.Append("  },\n");

            builder.Append("  \"diagnostics\": {\n");
            AppendIntegerProperty(
                builder,
                4,
                "maxEventCount",
                Diagnostics.MaxEventCount,
                true);
            AppendIntegerProperty(
                builder,
                4,
                "maxEventPayloadBytes",
                Diagnostics.MaxEventPayloadBytes,
                true);
            AppendIntegerProperty(
                builder,
                4,
                "maxLogBytes",
                Diagnostics.MaxLogBytes,
                true);
            AppendIntegerProperty(
                builder,
                4,
                "retainedLogCount",
                Diagnostics.RetainedLogCount,
                false);
            builder.Append("  },\n");

            builder.Append("  \"timeouts\": {\n");
            AppendIntegerProperty(builder, 4, "setupSeconds", Timeouts.SetupSeconds, true);
            AppendIntegerProperty(
                builder,
                4,
                "smokeRunSeconds",
                Timeouts.SmokeRunSeconds,
                true);
            AppendIntegerProperty(
                builder,
                4,
                "shutdownSeconds",
                Timeouts.ShutdownSeconds,
                false);
            builder.Append("  }\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendStringProperty(
            StringBuilder builder,
            int indentation,
            string name,
            string value,
            bool comma)
        {
            builder.Append(' ', indentation);
            builder.Append('"');
            builder.Append(name);
            builder.Append("\": \"");
            builder.Append(value);
            builder.Append('"');
            if (comma)
            {
                builder.Append(',');
            }

            builder.Append('\n');
        }

        private static void AppendIntegerProperty(
            StringBuilder builder,
            int indentation,
            string name,
            int value,
            bool comma)
        {
            builder.Append(' ', indentation);
            builder.Append('"');
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(',');
            }

            builder.Append('\n');
        }

        private static void AppendBooleanProperty(
            StringBuilder builder,
            int indentation,
            string name,
            bool value,
            bool comma)
        {
            builder.Append(' ', indentation);
            builder.Append('"');
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
            if (comma)
            {
                builder.Append(',');
            }

            builder.Append('\n');
        }

        private static string ComputeSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder(FingerprintPrefix.Length + 64);
            builder.Append(FingerprintPrefix);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static int DeterministicHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }

    /// <summary>
    /// Fail-closed loader result. Invalid text never yields a partial configuration.
    /// </summary>
    public sealed class EvidenceRunConfigurationLoadResult
    {
        private EvidenceRunConfigurationLoadResult(
            bool isValid,
            EvidenceRunConfiguration configuration,
            string errorCode,
            string errorMessage)
        {
            IsValid = isValid;
            Configuration = configuration;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool IsValid { get; }

        public EvidenceRunConfiguration Configuration { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }

        internal static EvidenceRunConfigurationLoadResult Valid(
            EvidenceRunConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return new EvidenceRunConfigurationLoadResult(true, configuration, null, null);
        }

        internal static EvidenceRunConfigurationLoadResult Invalid(
            string errorCode,
            string errorMessage)
        {
            return new EvidenceRunConfigurationLoadResult(
                false,
                null,
                errorCode,
                errorMessage);
        }
    }

    /// <summary>
    /// Strict v1 loader for the canonical UTF-8/LF JSON representation.
    /// Strict layout makes unknown, missing, duplicate, and reordered fields fail closed.
    /// </summary>
    public static class EvidenceRunConfigurationLoader
    {
        private const int CanonicalLineCountIncludingTrailingEmptyLine = 26;

        public static EvidenceRunConfigurationLoadResult Load(string canonicalJson)
        {
            if (string.IsNullOrEmpty(canonicalJson))
            {
                return Invalid("missing-configuration", "Canonical evidence configuration JSON is required.");
            }

            if (canonicalJson[0] == '\ufeff')
            {
                return Invalid("non-canonical-encoding", "A UTF-8 BOM is not allowed.");
            }

            if (canonicalJson.IndexOf('\r') >= 0)
            {
                return Invalid("non-canonical-line-endings", "Canonical configuration uses LF line endings only.");
            }

            string[] lines = canonicalJson.Split('\n');
            if (lines.Length != CanonicalLineCountIncludingTrailingEmptyLine
                || lines[lines.Length - 1].Length != 0)
            {
                return Invalid(
                    "non-canonical-field-count",
                    "Canonical v1 configuration must contain exactly the documented fields and one trailing LF.");
            }

            try
            {
                RequireExact(lines[0], "{");
                string schema = ReadString(lines[1], "  \"schema\": ", true);
                int version = ReadInteger(lines[2], "  \"version\": ", true);
                int runSeed = ReadInteger(lines[3], "  \"runSeed\": ", true);
                string identityReference = ReadString(
                    lines[4],
                    "  \"identityReference\": ",
                    true);
                int intentFixtureVersion = ReadInteger(
                    lines[5],
                    "  \"intentFixtureVersion\": ",
                    true);
                string qualityProfile = ReadString(
                    lines[6],
                    "  \"qualityProfile\": ",
                    true);
                string locale = ReadString(lines[7], "  \"locale\": ", true);

                RequireExact(lines[8], "  \"viewport\": {");
                int width = ReadInteger(lines[9], "    \"width\": ", true);
                int height = ReadInteger(lines[10], "    \"height\": ", true);
                bool fullscreen = ReadBoolean(
                    lines[11],
                    "    \"fullscreen\": ",
                    false);
                RequireExact(lines[12], "  },");

                RequireExact(lines[13], "  \"diagnostics\": {");
                int maxEventCount = ReadInteger(
                    lines[14],
                    "    \"maxEventCount\": ",
                    true);
                int maxEventPayloadBytes = ReadInteger(
                    lines[15],
                    "    \"maxEventPayloadBytes\": ",
                    true);
                int maxLogBytes = ReadInteger(
                    lines[16],
                    "    \"maxLogBytes\": ",
                    true);
                int retainedLogCount = ReadInteger(
                    lines[17],
                    "    \"retainedLogCount\": ",
                    false);
                RequireExact(lines[18], "  },");

                RequireExact(lines[19], "  \"timeouts\": {");
                int setupSeconds = ReadInteger(
                    lines[20],
                    "    \"setupSeconds\": ",
                    true);
                int smokeRunSeconds = ReadInteger(
                    lines[21],
                    "    \"smokeRunSeconds\": ",
                    true);
                int shutdownSeconds = ReadInteger(
                    lines[22],
                    "    \"shutdownSeconds\": ",
                    false);
                RequireExact(lines[23], "  }");
                RequireExact(lines[24], "}");

                ValidateSchema(schema, version);
                ValidateRunSeed(runSeed);
                ValidateIdentityReference(identityReference);
                ValidateIntentFixtureVersion(intentFixtureVersion);
                ValidateQualityProfile(qualityProfile);
                ValidateLocale(locale);
                ValidateViewport(width, height, fullscreen);
                ValidateDiagnostics(
                    maxEventCount,
                    maxEventPayloadBytes,
                    maxLogBytes,
                    retainedLogCount);
                ValidateTimeouts(setupSeconds, smokeRunSeconds, shutdownSeconds);

                EvidenceRunConfiguration configuration = new EvidenceRunConfiguration(
                    runSeed,
                    identityReference,
                    intentFixtureVersion,
                    qualityProfile,
                    locale,
                    new EvidenceViewport(width, height, fullscreen),
                    new EvidenceDiagnosticsLimits(
                        maxEventCount,
                        maxEventPayloadBytes,
                        maxLogBytes,
                        retainedLogCount),
                    new EvidenceTimeoutLimits(
                        setupSeconds,
                        smokeRunSeconds,
                        shutdownSeconds));

                return EvidenceRunConfigurationLoadResult.Valid(configuration);
            }
            catch (EvidenceConfigurationFormatException exception)
            {
                return Invalid(exception.ErrorCode, exception.Message);
            }
        }

        private static void ValidateSchema(string schema, int version)
        {
            if (!string.Equals(
                schema,
                EvidenceRunConfiguration.CurrentSchema,
                StringComparison.Ordinal))
            {
                throw Format("unsupported-schema", "Unsupported evidence configuration schema.");
            }

            if (version != EvidenceRunConfiguration.CurrentVersion)
            {
                throw Format("unsupported-version", "Only evidence configuration version 1 is supported.");
            }
        }

        private static void ValidateRunSeed(int runSeed)
        {
            if (runSeed <= 0)
            {
                throw Format("invalid-run-seed", "runSeed must be between 1 and Int32.MaxValue.");
            }
        }

        private static void ValidateIdentityReference(string identityReference)
        {
            const string prefix = EvidenceRunConfiguration.FingerprintPrefix;
            if (identityReference == null
                || identityReference.Length != prefix.Length + 64
                || !identityReference.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw Format(
                    "invalid-identity-reference",
                    "identityReference must be an EH-001 sha256 fingerprint.");
            }

            bool anyNonZero = false;
            for (int index = prefix.Length; index < identityReference.Length; index++)
            {
                char current = identityReference[index];
                bool isLowerHex = (current >= '0' && current <= '9')
                    || (current >= 'a' && current <= 'f');
                if (!isLowerHex)
                {
                    throw Format(
                        "invalid-identity-reference",
                        "identityReference must use 64 lowercase hexadecimal digits.");
                }

                anyNonZero |= current != '0';
            }

            if (!anyNonZero)
            {
                throw Format(
                    "invalid-identity-reference",
                    "identityReference must not be the all-zero placeholder.");
            }
        }

        private static void ValidateIntentFixtureVersion(int fixtureVersion)
        {
            if (fixtureVersion != EvidenceRunConfiguration.CurrentIntentFixtureVersion)
            {
                throw Format(
                    "unsupported-intent-fixture-version",
                    "Only intent fixture version 1 is supported.");
            }
        }

        private static void ValidateQualityProfile(string qualityProfile)
        {
            if (ContainsMachineLocalMarker(qualityProfile))
            {
                throw Format("machine-local-value", "qualityProfile must not contain a path or expansion marker.");
            }

            bool supported = string.Equals(qualityProfile, "Very Low", StringComparison.Ordinal)
                || string.Equals(qualityProfile, "Low", StringComparison.Ordinal)
                || string.Equals(qualityProfile, "Medium", StringComparison.Ordinal)
                || string.Equals(qualityProfile, "High", StringComparison.Ordinal)
                || string.Equals(qualityProfile, "Very High", StringComparison.Ordinal)
                || string.Equals(qualityProfile, "Ultra", StringComparison.Ordinal);
            if (!supported)
            {
                throw Format(
                    "unsupported-quality-profile",
                    "qualityProfile must name one pinned Unity quality profile.");
            }
        }

        private static void ValidateLocale(string locale)
        {
            if (ContainsMachineLocalMarker(locale))
            {
                throw Format("machine-local-value", "locale must not contain a path or expansion marker.");
            }

            bool valid = locale != null
                && locale.Length == 5
                && locale[0] >= 'a'
                && locale[0] <= 'z'
                && locale[1] >= 'a'
                && locale[1] <= 'z'
                && locale[2] == '-'
                && locale[3] >= 'A'
                && locale[3] <= 'Z'
                && locale[4] >= 'A'
                && locale[4] <= 'Z';
            if (!valid)
            {
                throw Format(
                    "invalid-locale",
                    "locale must use canonical language-region form such as en-US.");
            }
        }

        private static void ValidateViewport(int width, int height, bool fullscreen)
        {
            if (width < 320 || width > 7680)
            {
                throw Format("invalid-viewport", "viewport.width must be between 320 and 7680.");
            }

            if (height < 180 || height > 4320)
            {
                throw Format("invalid-viewport", "viewport.height must be between 180 and 4320.");
            }

            if (fullscreen)
            {
                throw Format(
                    "machine-local-value",
                    "Formal Stage 1 evidence uses a windowed viewport; fullscreen is machine-dependent.");
            }
        }

        private static void ValidateDiagnostics(
            int maxEventCount,
            int maxEventPayloadBytes,
            int maxLogBytes,
            int retainedLogCount)
        {
            if (maxEventCount < 1 || maxEventCount > 100000)
            {
                throw Format(
                    "invalid-diagnostics-bound",
                    "diagnostics.maxEventCount must be between 1 and 100000.");
            }

            if (maxEventPayloadBytes < 128 || maxEventPayloadBytes > 65536)
            {
                throw Format(
                    "invalid-diagnostics-bound",
                    "diagnostics.maxEventPayloadBytes must be between 128 and 65536.");
            }

            if (maxLogBytes < 4096 || maxLogBytes > 67108864)
            {
                throw Format(
                    "invalid-diagnostics-bound",
                    "diagnostics.maxLogBytes must be between 4096 and 67108864.");
            }

            if (maxLogBytes < maxEventPayloadBytes)
            {
                throw Format(
                    "invalid-diagnostics-bound",
                    "diagnostics.maxLogBytes must fit at least one maximum-size event payload.");
            }

            if (retainedLogCount < 1 || retainedLogCount > 16)
            {
                throw Format(
                    "invalid-diagnostics-bound",
                    "diagnostics.retainedLogCount must be between 1 and 16.");
            }
        }

        private static void ValidateTimeouts(
            int setupSeconds,
            int smokeRunSeconds,
            int shutdownSeconds)
        {
            if (setupSeconds < 1 || setupSeconds > 300)
            {
                throw Format(
                    "invalid-timeout-bound",
                    "timeouts.setupSeconds must be between 1 and 300.");
            }

            if (smokeRunSeconds < 1 || smokeRunSeconds > 1800)
            {
                throw Format(
                    "invalid-timeout-bound",
                    "timeouts.smokeRunSeconds must be between 1 and 1800.");
            }

            if (shutdownSeconds < 1 || shutdownSeconds > 120)
            {
                throw Format(
                    "invalid-timeout-bound",
                    "timeouts.shutdownSeconds must be between 1 and 120.");
            }
        }

        private static bool ContainsMachineLocalMarker(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0
                || value.IndexOf(':') >= 0
                || value.IndexOf('~') >= 0
                || value.IndexOf('%') >= 0
                || value.IndexOf('$') >= 0;
        }

        private static void RequireExact(string actual, string expected)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw Format(
                    "non-canonical-field-order",
                    "Configuration fields, punctuation, whitespace, and nesting must use canonical v1 order.");
            }
        }

        private static string ReadString(string line, string prefix, bool comma)
        {
            string token = ReadToken(line, prefix, comma);
            if (token.Length < 2 || token[0] != '"' || token[token.Length - 1] != '"')
            {
                throw Format("invalid-field-type", "Expected a canonical JSON string value.");
            }

            string value = token.Substring(1, token.Length - 2);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current < 0x20 || current == '"' || current == '\\')
                {
                    throw Format(
                        "non-canonical-string",
                        "Canonical v1 string values use unescaped printable characters only.");
                }
            }

            return value;
        }

        private static int ReadInteger(string line, string prefix, bool comma)
        {
            string token = ReadToken(line, prefix, comma);
            int value;
            if (!int.TryParse(
                token,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value)
                || !string.Equals(
                    token,
                    value.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                throw Format("invalid-field-type", "Expected a canonical non-negative JSON integer.");
            }

            return value;
        }

        private static bool ReadBoolean(string line, string prefix, bool comma)
        {
            string token = ReadToken(line, prefix, comma);
            if (string.Equals(token, "true", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(token, "false", StringComparison.Ordinal))
            {
                return false;
            }

            throw Format("invalid-field-type", "Expected a canonical JSON boolean.");
        }

        private static string ReadToken(string line, string prefix, bool comma)
        {
            if (line == null || !line.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw Format(
                    "non-canonical-field-order",
                    "Configuration fields must appear once in canonical v1 order.");
            }

            string token = line.Substring(prefix.Length);
            if (comma)
            {
                if (token.Length == 0 || token[token.Length - 1] != ',')
                {
                    throw Format("non-canonical-punctuation", "A canonical comma is missing.");
                }

                token = token.Substring(0, token.Length - 1);
            }
            else if (token.EndsWith(",", StringComparison.Ordinal))
            {
                throw Format("non-canonical-punctuation", "An unexpected trailing comma is present.");
            }

            return token;
        }

        private static EvidenceRunConfigurationLoadResult Invalid(string code, string message)
        {
            return EvidenceRunConfigurationLoadResult.Invalid(code, message);
        }

        private static EvidenceConfigurationFormatException Format(string code, string message)
        {
            return new EvidenceConfigurationFormatException(code, message);
        }

        private sealed class EvidenceConfigurationFormatException : FormatException
        {
            public EvidenceConfigurationFormatException(string errorCode, string message)
                : base(message)
            {
                ErrorCode = errorCode;
            }

            public string ErrorCode { get; }
        }
    }

    /// <summary>
    /// Device-source fixture adapters. Both v1 source fixtures resolve to the same
    /// immutable CS-003 PlayerIntentFrame sequence.
    /// </summary>
    public static class EvidenceIntentFixture
    {
        public static PlayerIntentFrame[] ResolveKeyboardMouse(int fixtureVersion)
        {
            RequireSupportedVersion(fixtureVersion);
            return CreateKeyboardMouseV1();
        }

        public static PlayerIntentFrame[] ResolveGamepad(int fixtureVersion)
        {
            RequireSupportedVersion(fixtureVersion);
            return CreateGamepadV1();
        }

        public static bool AreDeviceFixturesEquivalent(int fixtureVersion)
        {
            PlayerIntentFrame[] keyboardMouse = ResolveKeyboardMouse(fixtureVersion);
            PlayerIntentFrame[] gamepad = ResolveGamepad(fixtureVersion);
            if (keyboardMouse.Length != gamepad.Length)
            {
                return false;
            }

            for (int index = 0; index < keyboardMouse.Length; index++)
            {
                if (!AreEquivalent(keyboardMouse[index], gamepad[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static PlayerIntentFrame[] CreateKeyboardMouseV1()
        {
            return new[]
            {
                PlayerIntentFrame.Neutral,
                CreateFrame(
                    1f,
                    0f,
                    0f,
                    1f,
                    ButtonIntent.Pressed,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    0f,
                    0f),
                CreateFrame(
                    1f,
                    1f,
                    -1f,
                    1f,
                    ButtonIntent.Held,
                    ButtonIntent.Pressed,
                    ButtonIntent.Pressed,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    0f,
                    0f),
                CreateFrame(
                    0f,
                    1f,
                    -1f,
                    0f,
                    ButtonIntent.Released,
                    ButtonIntent.Held,
                    ButtonIntent.Released,
                    ButtonIntent.Tap,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    0f,
                    0f),
                CreateFrame(
                    0f,
                    0f,
                    0f,
                    0f,
                    ButtonIntent.Inactive,
                    ButtonIntent.Released,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Tap,
                    ButtonIntent.Tap,
                    -1f,
                    0f),
            };
        }

        private static PlayerIntentFrame[] CreateGamepadV1()
        {
            return new[]
            {
                PlayerIntentFrame.Neutral,
                CreateFrame(
                    1f,
                    0f,
                    0f,
                    1f,
                    ButtonIntent.Pressed,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    0f,
                    0f),
                CreateFrame(
                    1f,
                    1f,
                    -1f,
                    1f,
                    ButtonIntent.Held,
                    ButtonIntent.Pressed,
                    ButtonIntent.Pressed,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    0f,
                    0f),
                CreateFrame(
                    0f,
                    1f,
                    -1f,
                    0f,
                    ButtonIntent.Released,
                    ButtonIntent.Held,
                    ButtonIntent.Released,
                    ButtonIntent.Tap,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    0f,
                    0f),
                CreateFrame(
                    0f,
                    0f,
                    0f,
                    0f,
                    ButtonIntent.Inactive,
                    ButtonIntent.Released,
                    ButtonIntent.Inactive,
                    ButtonIntent.Inactive,
                    ButtonIntent.Tap,
                    ButtonIntent.Tap,
                    -1f,
                    0f),
            };
        }

        private static PlayerIntentFrame CreateFrame(
            float moveX,
            float moveY,
            float aimX,
            float aimY,
            ButtonIntent fire,
            ButtonIntent powerModifier,
            ButtonIntent thruster,
            ButtonIntent interact,
            ButtonIntent map,
            ButtonIntent pauseMenu,
            float uiX,
            float uiY)
        {
            return new PlayerIntentFrame(
                NormalizedIntentVector2.Create(moveX, moveY),
                NormalizedIntentVector2.Create(aimX, aimY),
                fire,
                powerModifier,
                thruster,
                interact,
                map,
                pauseMenu,
                NormalizedIntentVector2.Create(uiX, uiY));
        }

        private static bool AreEquivalent(PlayerIntentFrame left, PlayerIntentFrame right)
        {
            return left.Move == right.Move
                && left.Aim == right.Aim
                && left.Fire == right.Fire
                && left.PowerModifier == right.PowerModifier
                && left.Thruster == right.Thruster
                && left.Interact == right.Interact
                && left.Map == right.Map
                && left.PauseMenu == right.PauseMenu
                && left.UiNavigation == right.UiNavigation
                && left.WasFocusLost == right.WasFocusLost;
        }

        private static void RequireSupportedVersion(int fixtureVersion)
        {
            if (fixtureVersion != EvidenceRunConfiguration.CurrentIntentFixtureVersion)
            {
                throw new NotSupportedException("Only evidence intent fixture version 1 is supported.");
            }
        }
    }
}

# Deterministic evidence configuration v1

## Purpose and boundary

EH-002 defines one immutable Stage 1 evidence-run setup that can be resolved the
same way by edit-mode tests, play-mode fixtures, and a Windows smoke invocation.
It binds the harness seed, EH-001 identity fingerprint, CS-003 intent-fixture
version, rendering quality, locale, windowed viewport, bounded local diagnostics,
and bounded lifecycle timeouts.

The configuration is evidence infrastructure only. It does not tune gameplay,
select random item modifiers, inspect the active input device in gameplay code,
own mission state, write a registry, or introduce telemetry. The seed controls
harness setup and the scripted intent sequence only; it is not a promise of mature
gameplay randomness or balance.

Owned implementation:

- `EvidenceRunConfiguration.cs`: immutable values, strict loader, canonical
  serializer, configuration fingerprint, and source-device intent-fixture
  resolvers.
- `EvidenceRunConfigurationTests.cs`: focused EditMode contract proof.
- `stage1-evidence-config-v1.json`: the sole tracked v1 fixture.

## Identity binding

`identityReference` is the exact `record_fingerprint` emitted by EH-001. EH-002
does not parse a second identity format and does not recalculate build identity.
The loader validates the reference as `sha256:` followed by 64 lowercase
hexadecimal characters and rejects the all-zero placeholder.

The tracked JSON uses a deterministic, valid test reference so automated setup
can round-trip without machine or build-artifact access. A formal editor/Windows
evidence comparison must first capture the actual EH-001 record, copy its
`record_fingerprint` into a local v1 configuration, and then pass that exact same
configuration file to both invocations. The local resolved configuration and the
referenced identity record are evidence outputs; they are not additional tracked
configuration owners.

## Canonical JSON

V1 accepts one strict UTF-8/LF representation. It has no BOM, uses two-space
indentation, has one trailing LF, and contains every field exactly once in the
following order:

```json
{
  "schema": "shooter-mover.evidence-run-configuration",
  "version": 1,
  "runSeed": 104729,
  "identityReference": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "intentFixtureVersion": 1,
  "qualityProfile": "Medium",
  "locale": "en-US",
  "viewport": {
    "width": 1280,
    "height": 720,
    "fullscreen": false
  },
  "diagnostics": {
    "maxEventCount": 4096,
    "maxEventPayloadBytes": 4096,
    "maxLogBytes": 8388608,
    "retainedLogCount": 3
  },
  "timeouts": {
    "setupSeconds": 30,
    "smokeRunSeconds": 120,
    "shutdownSeconds": 15
  }
}
```

The strict layout is intentional. Unknown, missing, duplicate, or reordered
fields; alternative punctuation or whitespace; CRLF; a BOM; trailing content;
and non-canonical primitive encodings fail closed. Consumers therefore compare
exact bytes rather than relying on parser-specific normalization.

`EvidenceRunConfiguration.ToCanonicalJson()` reproduces the accepted bytes.
`Fingerprint` is SHA-256 of those complete bytes, including the final LF. The
fingerprint is a setup comparison aid and does not replace the EH-001 identity
reference.

## Field rules

| Field | V1 rule |
|---|---|
| `schema` | Exactly `shooter-mover.evidence-run-configuration`. |
| `version` | Exactly `1`. |
| `runSeed` | Integer from `1` through `Int32.MaxValue`. Same seed plus same setup gives the same canonical bytes and fingerprint. |
| `identityReference` | EH-001 `record_fingerprint`, lowercase SHA-256 form, not all zero. |
| `intentFixtureVersion` | Exactly `1`; unsupported scripted fixtures fail rather than falling back. |
| `qualityProfile` | One pinned Unity profile: `Very Low`, `Low`, `Medium`, `High`, `Very High`, or `Ultra`. The tracked fixture uses `Medium`. |
| `locale` | Canonical five-character language-region form such as `en-US`. |
| `viewport.width` | `320..7680`. |
| `viewport.height` | `180..4320`. |
| `viewport.fullscreen` | Must be `false`; formal Stage 1 evidence is windowed so monitor selection and desktop fullscreen behavior cannot alter setup. |
| `diagnostics.maxEventCount` | `1..100000`. |
| `diagnostics.maxEventPayloadBytes` | `128..65536`. |
| `diagnostics.maxLogBytes` | `4096..67108864` and at least one maximum event payload. |
| `diagnostics.retainedLogCount` | `1..16`. |
| `timeouts.setupSeconds` | `1..300`. |
| `timeouts.smokeRunSeconds` | `1..1800`. |
| `timeouts.shutdownSeconds` | `1..120`. |

Quality and locale strings reject path or expansion markers. The remaining
configuration fields are hashes, integers, booleans, or fixed schema tokens, so
there is no machine name, username, absolute path, drive letter, environment
variable, timestamp, or display enumeration in the canonical setup.

## Fail-closed loader

`EvidenceRunConfigurationLoader.Load` returns an
`EvidenceRunConfigurationLoadResult`:

- valid input: `IsValid == true`, non-null `Configuration`, and no error;
- invalid input: `IsValid == false`, `Configuration == null`, and stable
  `ErrorCode` / `ErrorMessage` values.

Representative error codes are:

| Condition | Error code |
|---|---|
| Missing text | `missing-configuration` |
| BOM or CRLF | `non-canonical-encoding` / `non-canonical-line-endings` |
| Extra, missing, or duplicate line | `non-canonical-field-count` |
| Reordered/renamed field or structural drift | `non-canonical-field-order` |
| Unsupported schema/config version | `unsupported-schema` / `unsupported-version` |
| Unsupported scripted intents | `unsupported-intent-fixture-version` |
| Invalid identity fingerprint | `invalid-identity-reference` |
| Seed outside bounds | `invalid-run-seed` |
| Unsupported quality or locale | `unsupported-quality-profile` / `invalid-locale` |
| Machine-local path/fullscreen value | `machine-local-value` |
| Viewport, diagnostics, or timeout bound | `invalid-viewport`, `invalid-diagnostics-bound`, or `invalid-timeout-bound` |

No invalid load exposes a partially populated setup.

## CS-003 intent fixture v1

`EvidenceIntentFixture.ResolveKeyboardMouse(1)` and
`EvidenceIntentFixture.ResolveGamepad(1)` model two source adapters but return the
same five immutable `PlayerIntentFrame` values. The returned values contain no key,
button, control-path, or Unity Input System identifier.

| Frame | Device-independent CS-003 result |
|---|---|
| 0 | Neutral frame. |
| 1 | Move right, aim up, fire pressed. |
| 2 | Normalized up-right movement, normalized up-left aim, fire held, power modifier pressed, thruster pressed. |
| 3 | Move up, aim left, fire released, power held, thruster released, interact tap. |
| 4 | Neutral movement/aim, power released, map tap, pause/menu tap, UI navigation left. |

`AreDeviceFixturesEquivalent(1)` compares every CS-003 vector, button state, and
focus-loss flag. A later hardware adapter may map physical controls into these
intents, but gameplay code consumes only `PlayerIntentFrame` and never branches on
the source device.

## Editor and Windows smoke comparison

1. Capture a valid EH-001 identity record for the frozen source and artifact.
2. Copy its `record_fingerprint` into a local copy of the canonical v1 JSON.
3. Load that exact file through `EvidenceRunConfigurationLoader` in the editor.
4. Record `Configuration.Fingerprint`, every resolved property, and the intent
   fixture version.
5. Pass the same file bytes to the Windows smoke invocation and record the same
   resolved values.
6. The two configuration fingerprints and resolved setup fields must match. The
   Windows runner must not replace quality, locale, viewport, diagnostics, seed,
   or timeout values with host defaults.

This task supplies the deterministic value/loader boundary. Wiring command-line
arguments, scene setup, diagnostics recording, and smoke-run execution belongs to
later owned EH tasks.

## Automated proof

The focused EditMode fixture covers:

- byte-stable round trip and canonical field order;
- two loads with the same seed and setup;
- changed-seed fingerprint change;
- unknown, missing, duplicate, and reordered fields;
- CRLF and trailing-content rejection;
- invalid seed, viewport, diagnostics, timeout, identity, fixture-version,
  fullscreen, and machine-local quality values;
- keyboard/mouse and gamepad equivalence through CS-003 values;
- get-only configuration and nested-value properties.

Required Unity proof:

```text
ShooterMover.Tests.EditMode.EvidenceHarness.EvidenceRunConfigurationTests
```

Connector-only implementation can inspect source and committed fixture bytes but
cannot substitute for the focused test log from pinned Unity `6000.3.19f1`.

## Limitations and non-goals

- No file I/O is performed by the C# loader; callers supply the already-read exact
  JSON text.
- No identity record lookup is performed; the caller resolves the EH-001
  fingerprint and keeps the record beside the evidence output.
- No quality settings, locale assets, command-line parser, build script, scene,
  gameplay system, diagnostics recorder, or evidence manifest is modified here.
- The v1 intent sequence is a harness fixture, not a player binding profile.
- The v1 seed does not govern mature content generation, loot, rewards, balance,
  or runtime randomness outside later explicitly integrated harness setup.

## Rollback

Remove the EH-002 configuration source, focused test, canonical fixture,
documentation, and their inseparable Unity metadata. Restore later callers to
explicit test defaults. There is no save migration, registry regeneration,
content repair, project-setting restoration, credential cleanup, telemetry
cleanup, or gameplay rollback.

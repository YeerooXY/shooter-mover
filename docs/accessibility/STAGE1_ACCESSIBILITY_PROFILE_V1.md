# Stage 1 Accessibility Profile v1

## Status and ownership

`Stage1AccessibilityProfile` is the AR-001 application-layer authority for the
Stage 1 comfort and warning settings consumed by later adapters and evidence.
The profile is immutable, device-independent, local-first, and valid without a
network connection, account, platform service, or machine-specific identifier.

Profile identifier: `shooter-mover.stage1-accessibility-profile`

Supported version: `1`

AR-005 owns persistence and migration. This contract deliberately contains no
JSON parser, save location, registry, UI, input binding, platform detection, or
remote synchronization behavior.

## V1 shape

A profile contains all five required settings groups. Missing groups fail
closed rather than inheriting hidden machine defaults.

### Reduced effects

| Field | Type | Valid values | Meaning |
| --- | --- | --- | --- |
| `IsEnabled` | Boolean | `true` / `false` | Requests the reduced-effects presentation path. |
| `NonEssentialEffectIntensityPercent` | Integer | `0..100` | Scales presentation-only effects that are not required for gameplay readability. |
| `ScreenDistortionEnabled` | Boolean | `true` / `false` | Allows or suppresses distortion presentation. |
| `MotionTrailsEnabled` | Boolean | `true` / `false` | Allows or suppresses motion-trail presentation. |

### Flash and shake limits

| Field | Type | Valid values | Meaning |
| --- | --- | --- | --- |
| `MaxFlashesPerSecond` | Integer | `0..3` | Hard presentation cap; `0` disables flashing. |
| `FlashIntensityPercent` | Integer | `0..100` | Maximum flash presentation intensity. |
| `CameraShakeIntensityPercent` | Integer | `0..100` | Maximum camera-shake intensity. |
| `CameraShakeDurationMilliseconds` | Integer | `0..2000` | Maximum duration of one shake response. |

These values constrain presentation only. They do not alter damage, timing,
collision, enemy behavior, difficulty, score, rewards, or simulation truth.

### Warning redundancy

| Field | Type | Meaning |
| --- | --- | --- |
| `ColorCueEnabled` | Boolean | Allows color as one warning channel. |
| `ShapeOrIconCueEnabled` | Boolean | Enables a color-independent visual symbol or silhouette. |
| `TextCueEnabled` | Boolean | Enables color-independent warning text. |
| `AudioCueEnabled` | Boolean | Enables the warning audio channel when audible. |

A valid warning configuration must:

1. enable `ShapeOrIconCueEnabled` or `TextCueEnabled`; and
2. provide at least two effective channels.

Color never satisfies the first rule. Audio counts toward the second rule only
when both master and warning audio levels are above zero. Consequently, muting
audio cannot silently reduce a profile to one warning channel. Fully visual
redundancy, such as icon plus text, remains valid with all audio levels at zero.

### Audio levels

`MasterPercent`, `EffectsPercent`, `MusicPercent`, and `WarningPercent` are
independent integers in `0..100`. Zero is valid for every channel. Audio is
never the sole color-independent warning authority.

### Input comfort

| Field | Type | Valid values | Meaning |
| --- | --- | --- | --- |
| `HoldActionMode` | Enum | `Hold`, `Toggle` | Device-independent hold/toggle preference. |
| `AimAssistPercent` | Integer | `0..100` | Requested aim-assistance strength for a later validated adapter. |
| `AimSensitivityPercent` | Integer | `25..300` | Device-independent aim sensitivity multiplier. |
| `InputBufferMilliseconds` | Integer | `0..250` | Requested bounded input buffering window. |
| `RepeatDelayMilliseconds` | Integer | `100..1000` | Delay before a held navigation/action repeat begins. |

No field names a keyboard key, mouse button, controller, binding path, touch
control, operating system, or hardware vendor. Applying these preferences must
continue to publish the accepted CS-003 immutable player-intent contract rather
than introducing a second intent authority.

## Canonical defaults

`Stage1AccessibilityProfile.CreateDefault()` returns exactly:

| Group | Defaults |
| --- | --- |
| Reduced effects | enabled; non-essential intensity `70`; distortion off; motion trails off |
| Flash/shake | max flashes `3/s`; flash intensity `50`; shake intensity `35`; shake duration `350 ms` |
| Warnings | color on; shape/icon on; text off; audio on |
| Audio | master `80`; effects `80`; music `60`; warnings `100` |
| Input comfort | hold mode; aim assist `15`; aim sensitivity `100`; buffer `80 ms`; repeat delay `350 ms` |

The defaults are conservative presentation and comfort values, contain no
machine-local data, require no online lookup, and preserve a color-independent
shape/icon warning even if audio later becomes unavailable.

## Validation contract

Call `Stage1AccessibilityProfileValidator.Validate(profile)` before applying a
profile. Validation is fail-closed and returns one stable error code in the
following order:

1. missing profile;
2. unsupported version;
3. missing required groups in profile order;
4. reduced-effect range;
5. flash and shake ranges;
6. audio ranges;
7. input-comfort enum and ranges;
8. color-independent warning and effective redundancy rules.

The validator does not clamp, normalize, infer, migrate, or replace invalid
values. Unknown versions fail with `unsupported-version`; later version support
must be added explicitly and reviewed with its migration owner.

## Dependency boundaries

- CS-002 identity remains the build/content identity authority; this profile
  does not redefine build identity or content version.
- CS-003 player intents remain device-independent immutable samples; this
  profile carries preferences only.
- CS-012 diagnostics/run validity may record the selected profile/version but
  this model adds no diagnostic events or validity categories.
- EH-002 evidence configuration may select a validated profile fixture without
  adding machine-local state.
- MT-007 and CB-008 remain the physical-device adapters. AR-001 contains no
  Unity Input System types or binding names.

## Non-goals

V1 does not implement a settings screen, rebinding screen, serialization,
migration, platform-specific options, scenes, prefabs, project settings,
gameplay balance changes, or effect/audio/input application code.

## Rollback

Before AR-002 or AR-005 consumers land, rollback is deletion of the two runtime
source files, the focused EditMode test, this document, and their inseparable
Unity metadata. After consumers land, revert consumers first so no code retains
a dependency on the v1 profile types.

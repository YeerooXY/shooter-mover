# WP-010 Stage 1 Weapon Presentation

This folder owns the temporary, bounded Stage 1 presentation for the immutable
CB-010 four-mount status snapshot. It does not read `FourMountCombatState`, does
not project the accepted snapshot itself, and does not mutate weapon, power,
mission, save, reward, enemy, or movement state.

## What is included

- `Stage1WeaponPresentationProjector` converts one immutable
  `FourMountStatusSnapshot` into exactly four canonically ordered presentation
  slots.
- `Stage1WeaponPresentationCatalog` gives each of the five Stage 1 weapons a
  unique text glyph, pattern word, procedural tone, and procedural effect shape.
  Color is redundant and is never the only state or identity cue.
- `Stage1WeaponStatusStrip` draws a temporary immediate-mode four-slot HUD and
  can play at most two low-priority procedural weapon voices.
- `Stage1WeaponCueArbiter` admits at most two weapon audio cues and four weapon
  effects, with every weapon priority below the reserved enemy-warning priority.
- `Stage1WeaponPresentationFixture` supplies detached representative snapshots
  for screenshots and manual inspection only. It is not gameplay authority.

## Human proof procedure

Use Unity `6000.3.19f1`.

1. Open an empty test scene, create an empty GameObject, and add
   `Stage1WeaponStatusStrip`.
2. Enter Play Mode. The component loads the four-slot fixture automatically.
3. Capture the Game view as the **four-slot HUD screenshot**. Verify slot labels
   `S1` through `S4`, glyphs/pattern words, readiness/recovery, numeric power,
   empowered expenditure, empty-power fallback, and the fault text are readable
   without relying on color.
4. Use the component context menu **WP-010/Load Empowered Spend Fixture** and
   confirm `SPENT 1 POWER`, `SPENT 3 POWER`, and
   `NORMAL FALLBACK - NO POWER` are visible.
5. Use **WP-010/Load Reduced-Effects Fixture** and capture the
   **reduced-effects screenshot**. Animated pulses and effect requests must be
   absent while glyph, pattern, state, power, fallback, and fault text remain.
6. Use **WP-010/Load Ricochet Identity Fixture** and confirm the fifth weapon is
   distinct through the `<>` glyph and `BOUNCE` pattern, plus its separate ping.
7. Copy the Console block beginning with `WP-010` as the
   **audio/effect priority capture**. It must report no more than two weapon
   voices, no more than four effects, and `max_weapon_priority` below
   `reserved_enemy_warning_priority=100`.
8. Run `Stage1WeaponPresentationTests` and attach the focused test log to the PR.

## Missing-reference and reduced-effects behavior

An unknown package ID is rendered as `? / MISSING REF / PACKAGE REF MISSING`
and emits no cue. A missing temporary audio or effect cue is reported in text;
the remaining non-color identity and state cues stay visible. Reduced-effects
mode removes animated effect requests rather than hiding critical information.

## Limitations

This is deliberately temporary OnGUI and procedural-tone presentation. It owns
no final art, final audio, localization, input, scene, global UI, enemy warning,
or durable state. The logical priority reservation leaves headroom for a later
enemy-warning implementation but does not implement that warning.

## Rollback

Remove this folder and
`Assets/ShooterMover/Tests/PlayMode/Combat/Stage1WeaponPresentationTests.cs`
with their paired Unity metadata. Combat state and CB-010 projection remain
valid and require no migration or registry repair.

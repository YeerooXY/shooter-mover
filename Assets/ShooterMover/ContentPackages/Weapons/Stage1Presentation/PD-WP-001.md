# PD-WP-001 — Temporary Stage 1 weapon presentation

- **Debt ID:** PD-WP-001
- **Owner:** WP-010 / Movement and Combat Builder
- **Status:** Open prototype debt
- **Introduced for:** Stage 1 four-mount readability and evidence capture
- **Expires:** Before Stage 2 representative-art acceptance

## Temporary choices

- Immediate-mode `OnGUI` strip instead of the final HUD system.
- Text glyphs and pattern words instead of representative weapon icons.
- Runtime-generated short mono tones instead of final authored audio clips.
- Procedural text pulses instead of final VFX assets.
- Package cue availability is represented by local presentation IDs; no global
  content registry or package behavior file is changed.

## Why the debt is bounded

The implementation consumes only the detached immutable CB-010 snapshot and
stores only the previous detached presentation frame to display numeric power
expenditure. It cannot mutate combat state. Weapon cue admission is capped at
two audio voices and four effects, and all weapon priorities remain below the
reserved enemy-warning priority.

## Removal / replacement plan

Replace the temporary strip, tones, and pulses with representative Stage 2
assets behind the same immutable presentation input. Preserve stable slot order,
non-color state cues, explicit numeric power/fallback language, reduced-effects
behavior, and warning-priority headroom. Delete this debt record when the
representative-art gate accepts the replacement.

## Rollback

Delete the WP-010 owned `Stage1Presentation` folder and focused PlayMode test.
No save migration, combat rollback, generated registry update, scene cleanup,
or package-behavior change is required.

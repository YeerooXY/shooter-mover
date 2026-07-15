# VS-UI-001 Temporary Loadout Preview

## Scope

This visible-slice-only presentation lets a player choose one complete authored four-slot comparison loadout before another owner enters the room. It is deliberately removable and non-persistent.

The implementation owns only:

- immutable temporary comparison choices;
- bounded browse, confirm, cancel, and restart state;
- a temporary IMGUI view;
- keyboard and controller command sampling; and
- a narrow read-only reflection bridge to WP-010 weapon labels, glyphs, patterns, and accents.

It does **not** edit individual weapon packages, equip live mounts, load a scene, own room or mission transitions, write inventory, change economy, grant rewards, or read/write saves.

## Deterministic choices

The catalog exposes three fixed comparison sets. Every set has exactly four stable slots and uses only accepted Stage 1 weapon IDs. The player selects a whole set; there is no per-slot editing.

Restart always returns to `loadout.comparison-a`, clears any prior confirmation, and reopens the menu. Confirm and cancel are terminal until restart.

## Integration

A scene or bootstrap owner may add `TemporaryLoadoutMenuView` to an owned GameObject and subscribe to:

- `Confirmed(TemporaryLoadoutChoice)` to copy the four stable IDs into its own room-entry flow; or
- `Cancelled()` to return to its own previous flow.

No scene was edited for this task. `TemporaryLoadoutMenuView.ResetForRestart()` is the explicit restart hook.

WP-010 compiles into Unity's predefined `Assembly-CSharp`, while this task is isolated in an owned asmdef. `TemporaryWeaponIdentityResolver` therefore consumes the public WP-010 catalog through a narrow reflection bridge. An identical five-identity fallback keeps isolated tests readable and fails unknown IDs closed as `UNKNOWN / ? / MISSING`.

## Manual navigation note

1. Add `TemporaryLoadoutMenuView` to a temporary scene-owned GameObject.
2. Enter Play Mode. The menu opens on **Comparison A**.
3. Keyboard: Left/Up selects the previous set; Right/Down selects the next set; Enter or Space confirms; Escape or Backspace cancels.
4. Controller: D-pad or shoulder buttons navigate; South confirms; East cancels.
5. Confirm that selection wraps at both ends and that a confirmed/cancelled menu stops accepting navigation.
6. Call `ResetForRestart()` and confirm the menu reopens on **Comparison A** with no confirmed choice retained.
7. Confirm that no save, inventory, economy, reward, mission, scene, or weapon-package asset changes occur.

## Focused automated verification

Pinned Unity editor: `6000.3.19f1`.

```bat
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Presentation.LoadoutPreview.TemporaryLoadoutMenuTests -testResults Artifacts\TestResults\VS-UI-001-EditMode.xml -logFile Artifacts\Logs\VS-UI-001-EditMode.log -quit
```

```bat
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Presentation.LoadoutPreview.TemporaryLoadoutMenuInputTests -testResults Artifacts\TestResults\VS-UI-001-PlayMode.xml -logFile Artifacts\Logs\VS-UI-001-PlayMode.log -quit
```

## Limitations

- This is temporary IMGUI presentation, not final UI art or layout.
- Confirmation emits a read-only choice; another owner must bridge it into actual room entry or mount configuration.
- No merged scene currently hosts the menu, by design and path ownership.
- The authored comparison sets are not a final balance statement.

## Rollback

Remove the owned `LoadoutPreview` runtime/test folders, this document, and their inseparable Unity metadata. No save migration, registry regeneration, package rollback, scene cleanup, or mission-state repair is required.

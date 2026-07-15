# VS-UI-003 Temporary Room Overlay

## Scope

This package adds removable, session-only room framing for the prototype:

- room title;
- objective hint;
- keyboard and controller restart hint;
- optional temporary state label; and
- a reduced-effects warning area.

The implementation is presentation-only. It does not read or mutate weapon, enemy, mission, reward, save, registry, scene, prefab, or persistence state.

## Runtime shape

`RoomOverlayProjector` converts an explicit `RoomOverlayInput` into an immutable `RoomOverlayFrame`. Projection trims and collapses whitespace, supplies bounded fallback copy, orders trace output deterministically, and never queries Unity scene state.

`RoomOverlayPresenter` owns the current session labels and forwards projected frames to `RoomOverlayView`. The presenter stores no data outside the current component lifetime.

`RoomOverlayView` uses temporary immediate-mode GUI so the package needs no scene, Canvas, prefab, or input-map edits. Its default layout uses a 24-pixel safe margin and reserves a 196-pixel bottom band for the later combat HUD / existing temporary four-slot weapon strip. Wide screens place warning/state copy at the upper right; narrow screens stack it beneath the room/objective panel.

A later scene owner can attach `RoomOverlayView` and `RoomOverlayPresenter` to the same GameObject, then call the presenter setters or `Present(RoomOverlayInput)`. VS-UI-003 intentionally performs no scene integration.

## Focused automated verification

Pinned Unity editor: `6000.3.19f1`.

EditMode deterministic projection:

```bat
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Presentation.RoomOverlay.RoomOverlayProjectorTests -testResults Artifacts\TestResults\VS-UI-003-RoomOverlay-EditMode.xml -logFile Artifacts\Logs\VS-UI-003-RoomOverlay-EditMode.log -quit
```

PlayMode visibility and safe-layout projection:

```bat
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Presentation.RoomOverlay.RoomOverlayPresenterTests -testResults Artifacts\TestResults\VS-UI-003-RoomOverlay-PlayMode.xml -logFile Artifacts\Logs\VS-UI-003-RoomOverlay-PlayMode.log -quit
```

The focused tests verify deterministic text normalization, fallback copy, reduced-effects warning projection, visibility toggles, component-lifetime cleanup, and bottom-HUD safe-area reservation.

## Manual keyboard/controller readability note

In the temporary manual-play scene, verify at 1920x1080 and one smaller 16:9 resolution:

1. the room title and objective are readable while moving against both dark and bright room regions;
2. `RESTART: R / MENU` is understandable without relying on color;
3. the keyboard hint remains legible at normal desk distance;
4. the controller hint remains legible at typical couch distance and accurately names the bound restart control used by the integration owner;
5. the reduced-effects warning and temporary state label do not overlap the room/objective panel; and
6. the overlay does not enter the reserved bottom HUD band.

`MENU` is temporary text, not final platform glyph art. The integration owner must update the explicit controller hint if the accepted restart binding uses another control.

## Limitations

- No scene or prefab includes the overlay yet.
- No restart input is consumed; the overlay only displays caller-supplied hint text.
- No mission objective is inferred or made authoritative.
- Immediate-mode styling is temporary and intentionally replaceable by the later HUD implementation.
- No final art, animation, localization, persistence, or accessibility settings screen is claimed.

## Rollback

Remove `Assets/ShooterMover/Runtime/Presentation/RoomOverlay`, the two focused test folders, their inseparable Unity metadata, and this document. No scene, prefab, gameplay authority, registry, save, or project setting requires rollback.

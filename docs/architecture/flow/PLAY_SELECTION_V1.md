# PLAY-001 — Play Selection V1

## Purpose

PLAY-001 owns the decision screen between the Hub and Level Selection. It
preserves the exact immutable `PlayerRouteProfilePayloadV1` supplied by HUB-001
and emits one route intent only after a valid selection.

The screen does not own gameplay startup, level selection, inventory truth,
networking, lobbies, matchmaking, accounts, transports, or multiplayer state.

## Stable mode identities

V1 defines two content records:

| Identity | Availability | Destination |
|---|---|---|
| `play-mode.solo` | available | Level Selection |
| `play-mode.multiplayer` | prototype unavailable | none |

Mode metadata is supplied by `PlayModeCatalogDefinitionV1`. The UI iterates the
catalog rather than embedding mode behavior only in button callbacks. Catalog
construction rejects null entries, duplicate mode identities, malformed
identities, available modes without a destination, and unavailable modes with a
destination.

## Route behavior

### Solo

Selecting the available Solo record emits:

- route: `LevelSelection`;
- selected mode identity: `play-mode.solo`;
- payload: the exact incoming `PlayerRouteProfilePayloadV1` object;
- payload fingerprint: unchanged.

PLAY-001 does not load a scene or begin gameplay. The LEVELSEL-002 owner consumes
the emitted route through `IPlaySelectionRouteAdapterV1`.

### Multiplayer / co-op placeholder

Selecting Multiplayer returns deterministic status
`ModeUnavailable` with feedback code
`play-selection-mode-prototype-unavailable`.

It emits no route, starts no gameplay, creates no networking state, and does not
lock the screen. The player may subsequently select Solo or go Back.

### Back

Back emits route `Hub` with the exact incoming payload and fingerprint.

## Input locking

The first successful Solo or Back decision becomes the terminal result. All
later selection or Back calls return `InputLocked`, emit no route, and do not
replace the original terminal result. This prevents repeated button, keyboard,
controller, or callback input from producing multiple transitions.

Unavailable, unknown, or invalid inputs do not become terminal routes.

## Payload admission

A route can be emitted only when the incoming payload:

1. is non-null; and
2. reports a valid immutable fingerprint through `HasValidFingerprint()`.

Missing or rejected imported payloads return `InvalidPayload`. PLAY-001 does not
create fallback character, loadout, equipment, or inventory truth.

## Authority boundary

PLAY-001 may:

- project mode metadata;
- retain the incoming immutable route payload;
- report unavailable prototype feedback;
- emit one Hub or Level Selection route intent.

PLAY-001 may not:

- alter character, loadout, equipment-instance, holdings, wallet, XP, reward, or
  skill state;
- load gameplay;
- start a run;
- open sockets, lobbies, matchmaking, accounts, transports, or servers;
- report fake multiplayer success or connection state.

## Focused verification

Required commands in Unity `6000.3.19f1`:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests `
  -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.Flow.PlaySelection" `
  -testResults "artifacts/test-results/PLAY-001-EditMode.xml" `
  -logFile "artifacts/logs/PLAY-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $Unity -batchmode -nographics -projectPath "$PWD" -runTests `
  -testPlatform PlayMode `
  -testFilter "ShooterMover.Tests.PlayMode.Flow.PlaySelection" `
  -testResults "artifacts/test-results/PLAY-001-PlayMode.xml" `
  -logFile "artifacts/logs/PLAY-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

A passing Unity claim is valid only when both named XML files exist and report
zero failed tests.

## Manual proof

1. Enter Play from the Hub with a populated payload.
2. Select Multiplayer and verify explicit unavailable feedback with no scene,
   gameplay, or network activity.
3. Select Solo and verify exactly one Level Selection route with the same payload.
4. Repeat Confirm rapidly and verify no second route.
5. Re-enter the screen, use Back, and verify one Hub route with unchanged payload.

## Known limitation

Multiplayer/co-op is intentionally unavailable in V1. Networking implementation
requires a separately approved architecture and task owner.

## Rollback

Remove the PLAY-001 application, content, UI, scene, test, and documentation
additions. No migration or authority restoration is required because the change
introduces no durable gameplay or profile state.

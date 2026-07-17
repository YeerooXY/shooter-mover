# DEMO-005 — Two-room playable vertical slice

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: DEMO-005
Branch: agent/demo-005-two-room-vertical-slice
Preparation baseline: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

This is a deferred final integration packet. Before branch creation, record an explicit launch override using the then-current `main` SHA after every dependency below is merged. The launch override supersedes the preparation baseline. Create one fresh branch and one non-empty draft PR; do not merge it.

## Objective

Perform the final serialized integration of Level 1: two-room navigation, map, player combat/boosting, standing turret and mobile droid, props, XP, random drops/pickups, debug boxes, idempotent End Run, Results, and exact Strongbox Opening. Keep the HUD compact and consume merged authorities/contracts read-only.

## Dependencies

- MENU-001/MENU-002, HUB-001, CHAR-001, INV-002, PLAY-001, LEVELSEL-002 merged.
- ROOM-001, ROOMTRANS-001, MAP-001, LEVELDES-001 merged.
- XP-002, DROP-001, WEAPON-DATA-001 merged.
- RUN-001, DEV-001, BOXUI-001 merged.
- Existing movement/boosting/combat, turret/mobile-droid, props, PICK/BOX/RAP/INV/MON/SCR authorities merged.
- Mobile droid must be proven damageable and able to damage the player. If its known follow-up is absent, stop rather than patching outside ownership.

## Exclusive owned files and paths

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`
- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity.meta`
- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSliceController.cs`
- `Assets/ShooterMover/Runtime/Bootstrap/VerticalSlice/Demo005/**`
- `Assets/ShooterMover/Tests/PlayMode/VerticalSlice/Demo005/**`
- `docs/proof/DEMO-005/**`

## Forbidden paths and changes

- Do not edit any contributing authority/domain/UI/package prefab/enemy/weapon/prop/drop/room/map/debug/Results implementation or production definition.
- Do not create duplicate truth or bypass public APIs for XP, drops, pickups, run results, holdings, wallets, or boxes.
- Do not add name-based binding or runtime GUID identity.
- Do not redesign upstream screens/assets.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Canonical route reaches Level 1 with exact immutable selected profile/loadout payload.
- [ ] Level 1 uses a valid two-room ROOM graph with configured doors/exits and deterministic transitions.
- [ ] Map shows current/visited/available/locked/completed state from ROOM truth.
- [ ] Player can move, boost, shoot, destroy standing turret/mobile droid, and receive valid enemy damage.
- [ ] Props/enemies produce configured random/forced physical drops through DROP/PICK; collection awards once.
- [ ] Enemy death grants configured XP exactly once; compact HUD shows level/XP.
- [ ] Runtime-supported weapon choices are usable; preview-only weapons never enter live pools.
- [ ] DEV panel spawns deterministic boxes collected through the normal path.
- [ ] End Run is idempotent and routes exact unopened boxes to Results; one selected exact box opens once through the existing scene.
- [ ] Quick restart restores room/enemy/prop/pickup/debug state without duplicate XP/rewards/results.
- [ ] HUD is compact/readable and does not recreate authority state.
- [ ] Focused PlayMode suite proves the full route and duplicate/restart protections.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.VerticalSlice.Demo005" -testResults "artifacts/test-results/DEMO-005-PlayMode.xml" -logFile "artifacts/logs/DEMO-005-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Do not claim passing proof unless the XML exists and reports zero failed tests.

## Manual proof checklist

- [ ] Launch Main Menu → Character Select → Hub → Play → Solo → Level Select → Level 1.
- [ ] Use at least two available weapon choices; shoot and boost in both rooms.
- [ ] Destroy turret, mobile droid, and props; verify XP and physical random/forced drops.
- [ ] Collect pickups/debug boxes and inspect compact HUD.
- [ ] Open/close map before and after transition and verify state.
- [ ] Quick-restart repeatedly and verify no duplicate XP/rewards/results.
- [ ] End Run repeatedly, verify one Results payload, then open one exact box and return.
- [ ] Confirm Level 2 remains a separate valid prototype route.

## Merge order

Final integration only. Start after every dependency is merged and proof-complete. DEMO-005 is the only active owner of Stage1VisibleSlice scene/controller.

## Asset requirements

Consume upstream imported UI/door art. Moving-droid final art may remain placeholder until separately supplied; do not block mechanics on unavailable art.

## Known limitations

- Level 2 remains prototype.
- Moving-droid final art is deferred.
- Multiplayer remains unavailable.
- Preview-only weapons remain simulator/preview-only.

## Parallel dispatch safety

Not safe in parallel with any Stage1VisibleSlice scene/controller editor. This final serialized owner starts last.

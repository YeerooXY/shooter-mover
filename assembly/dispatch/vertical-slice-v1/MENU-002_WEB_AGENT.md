# MENU-002 — Fix and prove MENU-001

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: MENU-002
Branch: agent/menu-002-menu-compile-proof
Exact base commit: f0430794fca20cc911561478767eddbecb476f1e
PR base: agent/menu-001-main-menu-flow
```

Create a fresh branch from the exact base above, verify the branch starts at that commit, and open one draft implementation PR. Do not merge it. Keep changes inside the exclusive ownership below, including inseparable Unity metadata inside those exact subtrees.

## Objective

Repair the Unity `ImageConversion` compilation failure on the existing MENU-001 branch, then prove the existing menu implementation with its focused EditMode and PlayMode suites. Preserve every accepted route, identity, authority, artwork-overlay, and presentation boundary. This is a repair task, not a redesign.

## Read completely before writing

- `AGENTS.md`
- `project_workspace.json`
- `assembly/context/CURRENT_HANDOFF.json`
- `assembly/context/NEW_CHAT_RESUME.md`
- `assembly/context/handoff.md`
- `assembly/dispatch/vertical-slice-v1/README.md`
- `assembly/dispatch/vertical-slice-v1/OWNERSHIP_MATRIX.md`
- `assembly/dispatch/vertical-slice-v1/VALIDATION.md`
- `docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md`
- `Assets/ShooterMover/UI/MainMenu/MainMenuArtworkController.cs`
- `Assets/ShooterMover/Tests/EditMode/Menu/MainMenuFlowStateTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuArtworkControllerTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuControllerTests.cs`

## Dependencies

- Open MENU-001 PR #160 at head `f0430794fca20cc911561478767eddbecb476f1e`.
- MENU-001 remains the integration PR; this repair PR targets `agent/menu-001-main-menu-flow`, not `main`.

## Exclusive owned files and paths

- `Assets/ShooterMover/UI/MainMenu/MainMenuArtworkController.cs`
- `Assets/ShooterMover/Tests/EditMode/Menu/MainMenuFlowStateTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuArtworkControllerTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuControllerTests.cs`

No other production, test, documentation, scene, prefab, asset, generated, context, or dispatch path is owned by this task.

## Forbidden paths and changes

- Do not edit `Assets/ShooterMover/Scenes/Menu/MainMenu.unity`.
- Do not edit any menu artwork or `.png.bytes` payload.
- Do not change route targets, loadout behavior, settings, authority connections, or screen layout.
- Do not edit gameplay scenes, ProjectSettings, Packages, shared asmdefs, economy/inventory/XP/reward authorities, context, generated, or dispatch files.

## Acceptance criteria

- [ ] `MainMenuArtworkController.cs` compiles in Unity 6000.3.19f1 with the correct Unity API qualification/import for image conversion.
- [ ] Existing MENU EditMode tests pass without weakening or deleting assertions.
- [ ] Existing MENU PlayMode tests pass without skipping tests.
- [ ] Level 1 still routes to `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`; Level 2 remains the existing bounded route.
- [ ] Duplicate equipment definitions remain selectable by distinct equipment-instance identity.
- [ ] No direct wallet, inventory, XP, reward, skill, shop, or crafting mutation is introduced.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.Menu" `
  -testResults "artifacts/test-results/MENU-002-EditMode.xml" `
  -logFile "artifacts/logs/MENU-002-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode `
  -testFilter "ShooterMover.Tests.PlayMode.Menu" `
  -testResults "artifacts/test-results/MENU-002-PlayMode.xml" `
  -logFile "artifacts/logs/MENU-002-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

## Proof rule

Do not claim Unity proof from source inspection, logs, GitHub mergeability, or an authored test alone. A passing claim requires the named XML result file to exist and report a passed test run with zero failed tests. Attach or link the XML and log in the pull request. If Unity is unavailable, author the tests, run available static validation, leave the implementation PR draft, and list the exact unexecuted commands.

## Manual proof checklist

- [ ] Cold-open the MENU-001 branch in Unity and confirm there are no compiler errors.
- [ ] Open `Assets/ShooterMover/Scenes/Menu/MainMenu.unity` and enter Play Mode.
- [ ] Exercise Title, Inventory/Armory, Shop, Crafting, Skills/preview, Settings, Level Select, Results shell, Back, and Quit.
- [ ] Confirm Level 1 uses the exact accepted scene route and Level 2 does not load gameplay.
- [ ] Confirm overlay controls—not baked artwork—receive input.

## Merge order

Merge MENU-002 into `agent/menu-001-main-menu-flow`. Re-run both focused suites on the updated MENU-001 head. Merge MENU-001 into `main` only after the XML files report passing tests.

## Asset requirements

Uses only the already committed MENU-001 assets. The real source artwork at asset intake commit is handled by later screen owners; do not import it here.

## Known limitations

- The menu remains visually and behaviorally MENU-001; this task intentionally adds no new hub flow.

## Parallel dispatch safety

Safe to dispatch immediately, but only from MENU-001 head and only against the MENU-001 branch. It must not run in parallel with another agent editing the four exact files above.

## Pull-request evidence

- Exact base and dependency ancestry.
- Exact changed-path list and ownership audit.
- Automated test commands, XML paths, pass/fail counts, and logs.
- Completed manual proof checklist or explicit pending items.
- Known limitations and rollback note.

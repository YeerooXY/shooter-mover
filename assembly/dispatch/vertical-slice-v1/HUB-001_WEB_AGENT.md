# HUB-001 — Main hub navigation

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: HUB-001
Branch: agent/hub-001-main-hub-navigation
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh branch from the exact base above, verify the branch starts at that commit, and open one draft implementation PR. Do not merge it. Keep changes inside the exclusive ownership below, including inseparable Unity metadata inside those exact subtrees.

## Objective

Create the canonical session route/profile payload and the real Main Menu → Character Select → Inventory/Loadout Hub flow. The Inventory/Loadout Hub exposes instant navigation to Skills, Shop, Crafting, and Play while preserving the selected profile and concrete loadout instance identities across every route. The UI is a projection/router only.

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

## Dependencies

- MENU-002 merged into MENU-001 and MENU-001 merged into `main` with passing focused XML proof.
- Merged INV-001 holdings authority is read-only except through its accepted public API.
- StableId and immutable equipment-instance contracts on current `main`.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Contracts/Flow/Session/**`
- `Assets/ShooterMover/Runtime/Application/Flow/Hub/**`
- `Assets/ShooterMover/UI/Hub/**`
- `Assets/ShooterMover/Scenes/Flow/Hub/**`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/Hub/**`
- `Assets/ShooterMover/Art/UI/Hub/**`
- `docs/architecture/flow/HUB_ROUTE_PROFILE_V1.md`

No other production, test, documentation, scene, prefab, asset, generated, context, or dispatch path is owned by this task.

## Forbidden paths and changes

- Do not edit MENU-001-owned files or `Assets/ShooterMover/Scenes/Menu/MainMenu.unity`.
- Do not edit character-select, inventory/loadout, play-selection, level-selection, shop, skills, crafting, Results, or gameplay owners.
- Do not create another inventory, wallet, XP, reward, shop, crafting, or skill authority.
- Do not edit `ProjectSettings/EditorBuildSettings.asset`, gameplay scenes, packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Defines one immutable, versioned, deterministic `PlayerRouteProfilePayloadV1` (or equivalently named V1 contract) containing selected character/loadout-profile identity and ordered concrete equipment-instance identities for all available weapon slots.
- [ ] Payload validation rejects missing/duplicate slot identities, malformed StableIds, unsupported schema/version, and inconsistent fingerprints without mutation.
- [ ] Main Menu routes to Character Select; Character Select continuation routes to Inventory/Loadout Hub.
- [ ] Hub has real buttons/routes for Skills, Shop, Crafting, and Play plus Back/Main Menu.
- [ ] All routes carry the same immutable payload; returning to Hub preserves profile and loadout selections.
- [ ] UI never directly grants/spends currency, mutates holdings, grants XP, opens rewards, purchases skills, shops, or crafts.
- [ ] EditMode tests prove deterministic equality/fingerprint, immutable copy behavior, invalid payload rejection, and route history.
- [ ] PlayMode tests prove navigation and state retention through placeholder destination adapters without requiring destination screens to be implemented.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.Flow.Hub" `
  -testResults "artifacts/test-results/HUB-001-EditMode.xml" `
  -logFile "artifacts/logs/HUB-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode `
  -testFilter "ShooterMover.Tests.PlayMode.Flow.Hub" `
  -testResults "artifacts/test-results/HUB-001-PlayMode.xml" `
  -logFile "artifacts/logs/HUB-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

## Proof rule

Do not claim Unity proof from source inspection, logs, GitHub mergeability, or an authored test alone. A passing claim requires the named XML result file to exist and report a passed test run with zero failed tests. Attach or link the XML and log in the pull request. If Unity is unavailable, author the tests, run available static validation, leave the implementation PR draft, and list the exact unexecuted commands.

## Manual proof checklist

- [ ] Start at Main Menu, navigate to Character Select shell, continue to Hub, and return.
- [ ] Use each Hub navigation button and return with the same profile/loadout payload.
- [ ] Verify keyboard/controller Back behavior is deterministic and does not clear the payload.
- [ ] Inspect connected authority snapshots before/after navigation and confirm no economy/inventory/XP/reward mutation.
- [ ] Verify 16:9 and one non-16:9 window size remain readable.

## Merge order

Merge after MENU-001. HUB-001 is the sole owner of the shared route/profile payload; merge it before CHAR-001, INV-002, PLAY-001, LEVELSEL-002, SHOPUI-001, SKILLUI-001, CRAFTUI-001, DEV-001, or BOXUI-001.

## Asset requirements

May copy `source-assets/user-intake/menu_screens/main_menu_screen.png` from exact asset commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` into `Assets/ShooterMover/Art/UI/Hub/**`. Consume the source asset read-only; do not merge the intake branch wholesale. Interactive controls remain real Unity UI overlays.

## Known limitations

- Destination screens may initially be test doubles/shell adapters; their real implementations are separate tasks.
- Persistence is session/navigation persistence only; save-profile persistence is outside this task.

## Parallel dispatch safety

Not safe to dispatch until MENU-001 is merged. After it merges, HUB-001 runs independently of ROOM/LEVELDES/XP/DROP/WEAPON work. No other parallel task may edit the HUB-owned contract or route files.

## Pull-request evidence

- Exact base and dependency ancestry.
- Exact changed-path list and ownership audit.
- Automated test commands, XML paths, pass/fail counts, and logs.
- Completed manual proof checklist or explicit pending items.
- Known limitations and rollback note.

# CHAR-001 — Character selection

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: CHAR-001
Branch: agent/char-001-character-selection
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh branch from the exact base above, verify the branch starts at that commit, and open one draft implementation PR. Do not merge it. Keep changes inside the exclusive ownership below, including inseparable Unity metadata inside those exact subtrees.

## Objective

Create a reusable character-selection screen and data model that selects one stable character/loadout profile and returns a new immutable HUB-001 route payload. Keep identity stable and leave explicit extension points for future armor, body, and visual variants without inventing character-stat or inventory authority.

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

- HUB-001 merged; consume `PlayerRouteProfilePayloadV1` read-only.
- StableId/current content-definition infrastructure.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Domain/Characters/Selection/**`
- `Assets/ShooterMover/Runtime/Application/Characters/Selection/**`
- `Assets/ShooterMover/Content/Definitions/Characters/Selection/**`
- `Assets/ShooterMover/UI/CharacterSelect/**`
- `Assets/ShooterMover/Scenes/Flow/CharacterSelect/**`
- `Assets/ShooterMover/Tests/EditMode/Characters/Selection/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/CharacterSelect/**`
- `docs/architecture/flow/CHARACTER_SELECTION_V1.md`

No other production, test, documentation, scene, prefab, asset, generated, context, or dispatch path is owned by this task.

## Forbidden paths and changes

- Do not edit HUB-001 route/profile contracts.
- Do not edit inventory, loadout, equipment, wallet, XP, reward, skill, shop, crafting, gameplay, or character-combat authorities.
- Do not implement armor/body rendering variants; expose identifiers/metadata only.
- Do not edit other Flow scenes, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Character definition supports stable character ID, display name/description, portrait/preview metadata, default loadout-profile ID, and optional future visual/body/armor variant metadata.
- [ ] Catalog validation rejects duplicate character/profile IDs and missing/default-invalid references.
- [ ] Selection creates a new immutable HUB payload while preserving existing valid equipment-instance selections.
- [ ] Back returns the incoming payload unchanged; Confirm returns the chosen stable profile identity.
- [ ] Selection and route payload fingerprints are deterministic and independent of Unity object instance IDs.
- [ ] No authority mutation occurs on highlight, select, back, confirm, repeated input, or scene reload.
- [ ] Focused EditMode and PlayMode tests cover multiple profiles, invalid content, repeated confirm, back, and route retention.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.Characters.Selection" `
  -testResults "artifacts/test-results/CHAR-001-EditMode.xml" `
  -logFile "artifacts/logs/CHAR-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode `
  -testFilter "ShooterMover.Tests.PlayMode.Flow.CharacterSelect" `
  -testResults "artifacts/test-results/CHAR-001-PlayMode.xml" `
  -logFile "artifacts/logs/CHAR-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

## Proof rule

Do not claim Unity proof from source inspection, logs, GitHub mergeability, or an authored test alone. A passing claim requires the named XML result file to exist and report a passed test run with zero failed tests. Attach or link the XML and log in the pull request. If Unity is unavailable, author the tests, run available static validation, leave the implementation PR draft, and list the exact unexecuted commands.

## Manual proof checklist

- [ ] Open Character Select from Main Menu using a real incoming HUB payload.
- [ ] Highlight at least two profiles and confirm the selected identity changes only on Confirm.
- [ ] Return Back and verify the prior payload is unchanged.
- [ ] Continue to Hub and verify the selected profile/loadout remains visible.
- [ ] Resize the Game view and verify selection controls remain usable.

## Merge order

Merge after HUB-001. It may merge in parallel with INV-002, PLAY-001, and LEVELSEL-002 once HUB-001 is on `main`.

## Asset requirements

Use placeholder portraits/shapes unless a character-selection asset is separately supplied. Do not reuse the moving-droid asset; it is not available yet.

## Known limitations

- V1 selects profiles; character-specific stats, body assembly, armor visuals, and gameplay spawning are future consumers.

## Parallel dispatch safety

Safe in parallel with INV-002, PLAY-001, LEVELSEL-002, ROOM, XP, DROP, weapons, and simulator after HUB-001 merges. Its exact paths do not overlap them.

## Pull-request evidence

- Exact base and dependency ancestry.
- Exact changed-path list and ownership audit.
- Automated test commands, XML paths, pass/fail counts, and logs.
- Completed manual proof checklist or explicit pending items.
- Known limitations and rollback note.

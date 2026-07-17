# SKILLUI-001 — Functional hub skills screen

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: SKILLUI-001
Branch: agent/skillui-001-functional-skills-screen
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Do not dispatch while PR #171 remains open/unproven. After a skill authority merges with required Unity proof, create a fresh dependency-complete branch and one non-empty draft PR. Do not merge it.

## Objective

Build the Hub Skills screen as a presentation/controller over the merged skill authority and XP-derived point state. Display branches, descriptions, prerequisites, rank/cost, points, and purchased/available/locked state; issue real purchase/reset commands without recreating skill truth.

## Dependencies

- HUB-001 merged.
- SKILL-001 PR #171 or a superseding proof-complete skill authority merged.
- XP-001 merged.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/**`
- `Assets/ShooterMover/UI/Skills/**`
- `Assets/ShooterMover/Scenes/Flow/Skills/**`
- `Assets/ShooterMover/Tests/EditMode/Skills/Presentation/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/Skills/**`
- `Assets/ShooterMover/Art/UI/Skills/**`
- `docs/authoring/SKILLS_SCREEN_V1.md`

## Forbidden paths and changes

- Do not edit skill/XP authorities, definitions, or balance to make UI pass.
- Do not implement skill effects in weapons/enemies/player.
- Do not grant/refund points locally or treat highlight as purchase truth.
- Do not edit HUB/other screens/gameplay, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Projects level, total/available points, branches, descriptions, prerequisites, rank/max, cost, and all states from authority/catalog.
- [ ] Purchase/reset calls only the skill authority and displays deterministic success/rejection facts.
- [ ] Insufficient points, locked prerequisite, max rank, duplicate operation, and stale state reject without local mutation.
- [ ] Back returns to Hub preserving payload and authoritative state.
- [ ] Repeated input cannot spend/refund twice.
- [ ] Artwork is non-authoritative; overlay controls bind stable skill IDs.
- [ ] Tests cover every state, purchase, prerequisite unlock, insufficient, duplicate, max rank, reset policy, revisit, and no XP mutation.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Skills.Presentation" -testResults "artifacts/test-results/SKILLUI-001-EditMode.xml" -logFile "artifacts/logs/SKILLUI-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.Skills" -testResults "artifacts/test-results/SKILLUI-001-PlayMode.xml" -logFile "artifacts/logs/SKILLUI-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Open Skills from Hub with known XP/points.
- [ ] Verify artwork is a backplate and real nodes align/readably.
- [ ] Purchase an available skill and verify one point/rank mutation.
- [ ] Test locked/max/insufficient/duplicate actions with no extra spend.
- [ ] Return/revisit and verify authority state persists.

## Merge order

Second wave after HUB and proof-complete skill authority. May merge with SHOPUI/CRAFTUI.

## Asset requirements

Copy `source-assets/user-intake/menu_screens/skills_demo_screen.png` from exact asset commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` into `Assets/ShooterMover/Art/UI/Skills/**`. Real controls/state remain overlays.

## Known limitations

- Skill-effect gameplay integration is outside UI scope.
- Layout follows merged catalog rather than hardcoded image assumptions.

## Parallel dispatch safety

Blocked on skill-authority merge; safe with other hub screens afterward.

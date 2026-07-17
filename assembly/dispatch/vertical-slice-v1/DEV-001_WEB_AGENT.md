# DEV-001 — Debug reward and run panel

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: DEV-001
Branch: agent/dev-001-debug-reward-run-panel
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Do not dispatch until every dependency below is merged. Then create a fresh dependency-complete branch and one non-empty draft PR. Do not merge it.

## Objective

Build a development/editor-only panel that selects strongbox count, tier, and seed; spawns deterministic physical demo boxes through normal reward/drop/pickup paths; tracks authority-derived spawned/collected state; ends the run idempotently through RUN-001; and routes to Results with exact unopened strongbox instances.

## Dependencies

- RUN-001 separately implemented, proof-complete, and merged. It is absent from baseline 645cf24 and is not created by this batch.
- PICK-001 and existing BOX/REW/GEN/RAP/INV/MON/SCR authorities merged.
- DROP-001 merged.
- HUB-001 route/profile payload merged.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Development/RunDebug/**`
- `Assets/ShooterMover/Runtime/UnityAdapters/Development/RunDebug/**`
- `Assets/ShooterMover/UI/Development/RunDebug/**`
- `Assets/ShooterMover/Tests/EditMode/Development/RunDebug/**`
- `Assets/ShooterMover/Tests/PlayMode/Development/RunDebug/**`
- `docs/authoring/DEBUG_REWARD_RUN_PANEL_V1.md`

## Forbidden paths and changes

- Do not edit Stage1VisibleSlice scene/controller; DEMO-005 integrates the panel.
- Do not directly add boxes/value to holdings, wallets, or Results.
- Do not open/reveal boxes, reroll rewards, grant XP, or create replacement run/result authority.
- Panel must be guarded from production builds by an explicit development/editor boundary.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Validated bounded count, authored tier identity, and deterministic seed inputs.
- [ ] Spawn command derives stable source/operation/box/pickup identities and requests physical boxes through accepted services.
- [ ] Collection occurs only through PICK-001 and normal RAP/holding authorities.
- [ ] Requested/spawned/collected/pending counts are projected from immutable facts/snapshots, not authoritative local counters.
- [ ] End Run calls RUN-001 exactly once; repeats reuse the same immutable result/route payload.
- [ ] Results route contains exact collected still-unopened box instances; no reveal/consume/reroll occurs.
- [ ] Development guard is tested/documented.
- [ ] Tests cover invalid/valid inputs, deterministic repeat, physical collection, duplicate collision, End Run replay, zero/multiple boxes, exact result identity.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Development.RunDebug" -testResults "artifacts/test-results/DEV-001-EditMode.xml" -logFile "artifacts/logs/DEV-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Development.RunDebug" -testResults "artifacts/test-results/DEV-001-PlayMode.xml" -logFile "artifacts/logs/DEV-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require both XML files with zero failures.

## Manual proof checklist

- [ ] Attach panel to a task-owned test scene/fixture, not Stage 1.
- [ ] Spawn multiple boxes by tier/seed and verify deterministic identities.
- [ ] Collect through player collision and verify one authority change.
- [ ] Press End Run repeatedly and verify one immutable result.
- [ ] Match every Results pending box to an exact unopened instance.
- [ ] Verify panel is unavailable without development guard.

## Merge order

Second wave after RUN-001 and DROP-001; merge before BOXUI-001 and DEMO-005.

## Asset requirements

No new art; compact development-only controls.

## Known limitations

- RUN-001 is a hard external prerequisite.
- Stage 1 placement is deferred to DEMO-005.

## Parallel dispatch safety

Not safe while RUN-001 is absent. Safe after dependencies with room/map and hub UI tasks.

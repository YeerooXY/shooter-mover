# SIM-002 — Real-content strongbox simulator

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: SIM-002
Branch: agent/sim-002-real-content-box-simulator
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh branch after WEAPON-DATA-001 merges and one non-empty draft PR. Do not merge it. Read SIM-001, BOX/GEN, real tier definitions, and the weapon catalog first.

## Objective

Extend SIM-001 to consume the authored weapon catalog and real strongbox tier definitions, including the planned 11 tiers. Preserve character level, box level, soft unlock, seed, and single/batch inputs. Add deterministic JSON/CSV or copyable report export. Never create a second reward algorithm.

## Dependencies

- WEAPON-DATA-001 merged.
- Merged SIM-001, GEN-001, BOX-001, and real strongbox definition/catalog contracts.

## Exclusive owned files and paths

- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationServiceV1.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulatorWindow.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/RealContent/**`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/RealContent/**`
- `docs/architecture/rewards/BALANCE_SIMULATOR_V1.md`

## Forbidden paths and changes

- Do not edit weapon catalog, strongbox runtime/tier definitions, GEN/BOX algorithms, or production balance values.
- Do not edit scenes/runtime authorities, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Simulator loads the real authored weapon catalog and real strongbox tiers through explicit adapters/catalog inputs.
- [ ] All planned 11 tiers are selectable/validated without a runtime switch hard-limited to 11.
- [ ] Character level, box level, soft unlock, seed, count, single, and batch modes remain available.
- [ ] Reports include tier/context, definition/family/Mk, live/preview state, item level, quality, augments, duplicate-definition frequency, unique instances, money/scrap/misc, rejected/impossible rolls, and fingerprint.
- [ ] Export offers at least one machine-readable format plus copyable human-readable text with deterministic culture-invariant ordering.
- [ ] Preview-only weapons appear only in explicit preview mode; live simulation excludes them.
- [ ] Same input produces byte-identical report/export; different seed changes deterministic output.
- [ ] Tests cover all tiers, catalog count/filtering, unlocks above 100, 100/1,000 runs, export shape/round trip, and reuse of existing generator services.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.BalanceSimulator.RealContent" -testResults "artifacts/test-results/SIM-002-EditMode.xml" -logFile "artifacts/logs/SIM-002-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require the XML with zero failures.

## Manual proof checklist

- [ ] Open the Balance Simulator and select low/mid/high tiers.
- [ ] Run single and batch at levels near 1, 50, 100, and 125.
- [ ] Export twice with the same seed and compare exact output.
- [ ] Toggle preview inclusion; unsupported weapons must never appear in live mode.
- [ ] Inspect duplicate definitions and unique equipment instance counts.

## Merge order

First-wave follow-on after WEAPON-DATA-001; merge before final content/balance proof but it need not block room/UI code.

## Asset requirements

Consumes authored data; no artwork.

## Known limitations

- Simulator evaluates content; it does not certify final balance.
- Export may use file save and/or clipboard according to Editor constraints.

## Parallel dispatch safety

Blocked until WEAPON-DATA-001 merges; safe afterward with flow and room/UI work.

# BOX-WEAPON-CARD-001 Verification

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/box-weapon-card-001-real-weapon-projection`
- Target: `main`
- Exact launch SHA: `208bc89be4ce34750213139c80399ea7983e70e5`
- Merge and auto-merge are forbidden for this task.

## Focused Unity command

```powershell
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode `
  -testFilter "ShooterMover.Editor.BalanceSimulator.Tests" `
  -testResults "artifacts/test-results/BOX-WEAPON-CARD-001-EditMode.xml" `
  -logFile "artifacts/logs/BOX-WEAPON-CARD-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

The draft PR must not be marked ready until Unity compilation succeeds and this XML reports zero failures.

## Required manual simulator evidence

Open both editor windows after loading the current weapon catalog:

- `Tools > Shooter Mover > Lootbox Opener Simulator`
- `Tools > Shooter Mover > Authoritative Strongbox Wiring`

Capture:

1. the previous result presentation before this branch;
2. the compact card after this branch;
3. one single-projectile weapon;
4. one multi-projectile weapon;
5. one zero-capacity definition;
6. one definition with multiple `◇` capacity symbols;
7. diagnostics showing `Installed augments: 0`;
8. a successful authoritative BOX result after RAP application and exact box consumption.

## Static examples covered by focused tests

- Single projectile: `Damage: <value>` with no projectile row.
- Multi projectile: `Damage: <value> × 7` and `Projectiles: 7`.
- Zero capacity: production starter Blaster, no `◇`.
- Multiple capacity: imported simulator definition, `◇ ◇ ◇`.
- Exact rolled quality: `Common`, never `quality.common` on the primary card.
- Empty authoritative result: `EquipmentInstance.Augments.Count == 0`.

## Known limitations

- This connector-only implementation environment cannot launch Unity, produce XML, or capture editor screenshots. Those items remain explicitly pending.
- The weapon JSON has no canonical per-definition augment-capacity field. The existing simulator equipment projection therefore continues to own a three-slot capacity for imported live definitions; installed augments are still always zero.
- Final production strongbox-opening UI, artwork, persistence, Keep/Sell authority, and augment management remain out of scope.

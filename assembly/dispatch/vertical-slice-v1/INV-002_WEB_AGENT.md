# INV-002 — Inventory and loadout screen

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: INV-002
Branch: agent/inv-002-inventory-loadout-screen
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft implementation PR. Do not merge it. Read `AGENTS.md`, the current handoff, and every file under `assembly/dispatch/vertical-slice-v1/` relevant to this task before editing.

## Objective

Build the real Inventory/Loadout screen as a presentation/application layer over INV-001. Display every owned equipment instance—including multiple instances sharing one definition—and allow any valid owned weapon instance to be assigned to each available weapon slot. Preserve concrete instance identity in the immutable HUB-001 route/profile payload. Do not invent another inventory authority.

## Dependencies

- HUB-001 merged shared route/profile payload.
- Merged INV-001 holdings authority and EQP-001 immutable equipment-instance model.

## Exclusive owned files and paths

- `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/**`
- `Assets/ShooterMover/UI/InventoryLoadout/**`
- `Assets/ShooterMover/Scenes/Flow/InventoryLoadout/**`
- `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/**`
- `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/**`
- `docs/architecture/flow/INVENTORY_LOADOUT_SCREEN_V1.md`

No other production, test, documentation, scene, prefab, asset, generated, context, or dispatch path is owned.

## Forbidden paths and changes

- Do not edit INV-001 domain/application/contracts or create replacement holdings truth.
- Do not edit HUB contracts, equipment generation/definitions, weapon runtime, wallets, rewards, XP, shop, crafting, skills, gameplay scenes, ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.
- UI selection must never mutate holdings.

## Acceptance criteria

- [ ] Owned equipment is projected from INV-001 public snapshots/queries and keyed by equipment-instance StableId.
- [ ] Duplicate definition IDs render as separate entries with distinct immutable instance data.
- [ ] Every available weapon slot can select any policy-valid owned weapon instance.
- [ ] Removed, stale, non-weapon, unknown, or otherwise invalid instances reject without authority mutation.
- [ ] Confirm returns a new immutable HUB payload with ordered concrete instance IDs; Cancel/Back preserves the incoming payload.
- [ ] Refreshing from a newer holdings snapshot retains valid selections by instance ID and clearly invalidates missing ones.
- [ ] No local list becomes authoritative.
- [ ] Focused tests cover duplicate definitions, all slots, stale/removal refresh, invalid types, confirm/cancel, ordering, and no holdings mutation.

## Focused Unity test commands

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Inventory.LoadoutScreen" -testResults "artifacts/test-results/INV-002-EditMode.xml" -logFile "artifacts/logs/INV-002-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ShooterMover.Tests.PlayMode.Flow.InventoryLoadout" -testResults "artifacts/test-results/INV-002-PlayMode.xml" -logFile "artifacts/logs/INV-002-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

A pass may be claimed only when both named XML files exist and report zero failed tests.

## Manual proof checklist

- [ ] Seed INV-001 with two distinct instances sharing one definition and at least one other weapon.
- [ ] Verify all instances are separately visible/selectable.
- [ ] Assign all available slots, confirm, navigate away/back, and verify exact IDs persist.
- [ ] Remove/replace a selected instance via the authority fixture and verify clear invalidation/no phantom item.
- [ ] Compare holdings snapshots before/after UI-only selection; ownership/quantity must not change.

## Merge order

Merge after HUB-001. May merge in parallel with CHAR-001, PLAY-001, and LEVELSEL-002.

## Asset requirements

No inventory artwork is supplied. Use functional code-owned panels/cards that can accept later art.

## Known limitations

- V1 owns available weapon-slot selection only; broader armor-slot editing is outside scope unless already represented by accepted contracts.
- Accessible buttons are sufficient; drag-and-drop is not required.

## Parallel dispatch safety

Safe after HUB-001. No other task may edit the owned subtrees.

# WEAPON-DATA-001 — Reusable 36-definition weapon catalog

Use this packet with one isolated GitHub/Unity coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: WEAPON-DATA-001
Branch: agent/weapon-data-001-weapon-catalog
Exact base commit: 645cf24f30ee6c8762214a84060e59e35df67a05
PR base: main
```

Create a fresh exact-base branch and one non-empty draft PR. Do not merge it. Read EQP/GEN/progression and existing weapon definition conventions first.

## Objective

Author 12 weapon families with Mk1/Mk2/Mk3 variants—36 definitions total—with complete requested combat/progression/content metadata. This is data, validation, and documentation only. Do not implement firing behavior.

## Dependencies

- Merged EQP-001 equipment model, GEN-001 catalog/generator contracts, progression soft activation, and existing weapon conventions.

## Exclusive owned files and paths

- `Assets/ShooterMover/Content/Definitions/Equipment/Weapons/VerticalSliceCatalog/**`
- `Assets/ShooterMover/Tests/EditMode/Equipment/Weapons/VerticalSliceCatalog/**`
- `docs/authoring/WEAPON_CATALOG_VERTICAL_SLICE_V1.md`

## Forbidden paths and changes

- Do not edit existing weapon runtime packages, projectiles, firing/charge/heat/reload behavior, player loadout runtime, GEN/BOX/SHOP/CRA algorithms, scenes, prefabs, or registries outside the owned catalog.
- Preview-only weapons must not enter live pools.
- Do not hardcode family switches into runtime control flow.
- Do not edit ProjectSettings, Packages, shared asmdefs, generated/context/dispatch files.

## Acceptance criteria

- [ ] Exactly 12 families: Blaster, Shotgun, Rocket Launcher, Flamethrower, Sniper, Fast Rocket Launcher, Pulse Shotgun, Pulse Rocket Launcher, Ricochet Weapon, Fast Sniper, Big Gatling Gun, Chain Gun.
- [ ] Each has exactly Mk1/Mk2/Mk3 for exactly 36 unique stable definition IDs.
- [ ] Every definition includes family, Mk, display data, soft unlock, rarity weight, damage, fire rate, projectile count/speed, range/falloff/spread, pierce, splash, DoT, chain fields, reload/heat/ammo, charge, recoil/knockback, role, estimated DPS, and live/preview capability metadata.
- [ ] Mk1 broadly spans levels 1–70; Mk2 45–105; Mk3 75–125; many strong entries cluster 90–110; rare soft unlocks reach about 115–130.
- [ ] Variants use meaningful tradeoffs rather than increasing every stat.
- [ ] DPS calculation is deterministic/documented with explicit splash/DoT/chain assumptions.
- [ ] Preview-only definitions are available to preview/simulator catalogs and excluded from live generation until behavior exists.
- [ ] Validation rejects duplicate IDs/family-Mk pairs, invalid stats/weights/ranges, unsupported live behavior, and inconsistent DPS.
- [ ] Tests prove count, field completeness, level bands, deterministic fingerprint, preview filtering, and no runtime behavior dependency.

## Focused Unity test command

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ShooterMover.Tests.EditMode.Equipment.Weapons.VerticalSliceCatalog" -testResults "artifacts/test-results/WEAPON-DATA-001-EditMode.xml" -logFile "artifacts/logs/WEAPON-DATA-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

Passing claims require the XML with zero failures.

## Manual proof checklist

- [ ] Inspect all 36 entries grouped by family/Mk.
- [ ] Filter live versus preview-only; unsupported behavior must be absent from live pools.
- [ ] Inspect unlock distribution and requested level bands.
- [ ] Spot-check DPS for single-shot, shotgun, DoT, splash, and chain examples.
- [ ] Confirm no runtime/prefab/projectile files changed.

## Merge order

First wave; merge before SIM-002 and DEMO-005 content selection.

## Asset requirements

Existing generic placeholders/display references only; no new weapon sprites required.

## Known limitations

- Initial content baseline, not final balance.
- Unsupported families remain preview-only.
- No firing implementation is authorized.

## Parallel dispatch safety

Safe immediately; the catalog subtree has one owner.

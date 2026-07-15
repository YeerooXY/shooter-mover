# EN-009 Stage 1 Enemy Package Validation

## Scope

This procedure validates the five accepted Stage 1 enemy package descriptors as registry inputs without editing or regenerating the committed CS-011 outputs. It is an authoring-only gate: no scene, encounter, tuning, adapter, lifecycle, persistence, reward, project-setting, or generated-registry ownership moves into EN-009.

Owned artifacts:

- `Stage1EnemyPackageMatrixTests.cs`
- `Fixtures/stage1-enemy-registry-input-v1.json`
- this validation procedure
- inseparable Unity metadata for those artifacts

The EditMode fixture resolves the concrete package factories through reflection because the package sources compile into Unity's predefined `Assembly-CSharp`, while `ShooterMover.Tests.EditMode` is asmdef-backed.

## Accepted roster and ownership matrix

| Stable enemy ID | Classification | Descriptor source | Package-owned serialized assets | Temporary-presentation debt |
|---|---|---|---|---|
| `enemy.pursuer-drone` | ordinary | `PursuerDroneDefinition.CreatePackageDescriptor()` | `PursuerDrone.prefab`; `PursuerDroneDefinition.asset` | generated grayscale body and pulsing detached fins; replace with final package art |
| `enemy.ram-droid` | ordinary | `RamDroidDefinition.CreatePackageDescriptor()` | `RamDroid.prefab`; `RamDroidDefinition.asset` | `RAM!` label and geometric warning pulse; replace while preserving non-color readability |
| `enemy.mobile-blaster-droid` | ordinary | `MobileBlasterDroidDefinition.CreatePackageDescriptor()` | `MobileBlasterDroid.prefab`; `MobileBlasterDroidDefinition.asset` | generated wind-up direction line and compressed recovery outline |
| `enemy.blaster-turret` | ordinary | `BlasterTurretDefinition.CreatePackageDescriptor()` | `BlasterTurret.prefab`; `BlasterTurretDefinition.asset` | generated turret base, line-of-fire rail, and repeated shape ticks |
| `enemy.four-blaster-elite` | elite | `FourBlasterElitePackage.CreateDescriptor()` | none | four-spoke/countdown-bar readability contract only; no final art or serialized presentation asset |

The five package roots are sibling folders beneath `Assets/ShooterMover/ContentPackages/Enemies/`. No root is equal to or a parent of another root. All eight declared serialized assets remain beneath exactly one package root. No package-owned path reaches `Assets/ShooterMover/Generated/`.

## Frozen registry-input fixture

Fixture:

```text
Assets/ShooterMover/Tests/EditMode/Enemies/Fixtures/stage1-enemy-registry-input-v1.json
```

Canonical UTF-8/LF SHA-256:

```text
3dd27a97474939c408d9676c0c81f9b41bf2052b38b037aade35d8a2d0b43d8f
```

The focused test decodes the checked-out file as strict UTF-8, normalizes
Windows CRLF checkout line endings back to canonical LF for checksum
comparison, and still rejects BOM-prefixed or lone-carriage-return content.

The fixture records, for every package:

- stable enemy ID and ordinary/elite classification;
- concrete descriptor factory;
- movement, attack, and telegraph references;
- package root;
- package-owned serialized assets; and
- temporary-presentation debt.

Any mismatch between the fixture and a production descriptor fails with `stale-package-input`. The fixture is not a generated registry and does not authorize writing the central registry pair.

## Automated validation

Focused fixture:

```text
ShooterMover.Tests.EditMode.Enemies.Stage1EnemyPackageMatrixTests
```

Pinned-editor command from the repository root:

```bat
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Enemies.Stage1EnemyPackageMatrixTests -testResults Artifacts\TestResults\EN-009-Stage1EnemyPackageMatrixTests.xml -logFile Artifacts\Logs\EN-009-Stage1EnemyPackageMatrixTests.log -quit
```

Python 3 must be available as `python3`, `python`, or Windows `py -3`. Set `SHOOTER_MOVER_PYTHON` when a non-standard executable is required.

The fixture proves:

1. The real production roster validates as exactly four ordinary IDs and one elite ID.
2. Shuffled package input produces the same canonical result and registry-input ordering.
3. Enemy registry order is stable: Blaster Turret, Four-Blaster Elite, Mobile Blaster Droid, Pursuer Drone, Ram Droid.
4. Duplicate package IDs, a missing accepted role, and an unexpected sixth role fail visibly.
5. Duplicate descriptor inputs and omitted supporting definitions fail through the accepted CS-011 validator.
6. Fixture drift fails before generation.
7. Two shuffled temporary descriptor catalogs generate byte-identical machine and review outputs with matching SHA-256 values.
8. A byte edit to a temporary machine registry makes CS-011 `check` exit with `registry-drift: failed` and visible expected/actual checksums.
9. Every declared serialized asset exists beneath its package root, package roots do not overlap, and all five packages declare temporary-presentation debt.
10. The committed generated registry pair is byte-snapshotted before each test and asserted unchanged during teardown.

All descriptor inputs, locks, machine registries, and review snapshots produced by the test are written below operating-system temporary directories and deleted after each test.

A passing run emits proof lines including:

```text
stage1-enemy-package-matrix: ok
ordinary_role_count=4
elite_role_count=1
stable_enemy_order=enemy.blaster-turret,enemy.four-blaster-elite,enemy.mobile-blaster-droid,enemy.pursuer-drone,enemy.ram-droid
fixture_sha256=3dd27a97474939c408d9676c0c81f9b41bf2052b38b037aade35d8a2d0b43d8f
stage1-enemy-registry-input: ok
first_machine_sha256=<sha256>
second_machine_sha256=<same-sha256>
first_review_sha256=<sha256>
second_review_sha256=<same-sha256>
ownership_audit=pass
serialized_asset_count=8
root_overlap_count=0
temporary_presentation_debt_count=5
no_generated_output_write=true
```

Attach the focused XML and log before the PR is marked ready. Until that pinned-editor run exists, only static/source validation may be claimed.

## Manual ownership and no-generated-diff audit

Review the pull request changed-file list and confirm:

1. only the three EN-009-owned artifacts and inseparable `.meta` files changed;
2. no exact package root or parent folder is claimed by more than one package;
3. each serialized asset is owned by the package root that contains it;
4. the Four-Blaster Elite correctly declares zero serialized assets rather than claiming another package's prefab or definition;
5. no path beneath `Assets/ShooterMover/Generated/` changed;
6. the CS-011 generator, package implementations, scenes, adapters, tuning, persistence, lifecycle bookkeeping, `Packages`, and `ProjectSettings` are unchanged.

This review is the required human ownership proof. The automated path audit supports it but does not replace reviewer confirmation.

## Limitations

- This is EditMode authoring validation, not playable enemy or encounter evidence.
- Temporary supporting module/weapon descriptors exist only in test-owned temporary directories.
- Output SHA-256 values are intentionally produced by the accepted CS-011 implementation during the focused run; this document freezes only the owned registry-input fixture checksum.
- EN-009 reports drift and invalid input. It does not repair, reconcile, or regenerate the central registry outputs.
- Final-art replacement remains owned by the relevant package/presentation work.

## Rollback

Remove the three EN-009-owned artifacts and their inseparable Unity metadata. No content package, generated registry, scene, adapter, runtime state, save schema, package manifest, project setting, or lifecycle bookkeeping requires rollback.

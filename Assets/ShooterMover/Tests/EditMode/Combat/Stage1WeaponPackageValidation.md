# WP-009 Stage 1 Weapon Package Validation

## Scope

This validation is an authoring-only bridge between the five accepted Stage 1 weapon packages and the existing CS-011 registry generator/validator. It does not load content at runtime, alter combat behavior, rewrite the generator, or write to the committed generated registry pair.

Owned inputs:

- `Stage1WeaponPackageRegistryTests.cs`
- `Fixtures/stage1-weapon-registry-input-v1.json`
- this procedure

The test fixture names the production descriptor factory and expected shared-module reference for each package. The test resolves those factories from Unity's predefined `Assembly-CSharp` through reflection because the asmdef-backed EditMode test assembly cannot statically reference package source compiled there.

## Accepted five-package matrix

| Weapon | Production descriptor member | Shared module | Default |
|---|---|---|---|
| `weapon.blaster-machine-gun` | `BlasterMachineGunPackage.CreateDescriptor()` | `module.weapon-automatic-projectile` | yes |
| `weapon.shotgun` | `ShotgunPackageDefinition.CreateDefaultDescriptor()` | `module.weapon-spread-projectile` | no |
| `weapon.rocket-launcher` | `RocketLauncherPackage.Descriptor` | `module.weapon-rocket-area-detonation` | no |
| `weapon.arc-gun` | `ArcGunPackage.CreateDescriptor()` | `module.weapon-arc-chain` | no |
| `weapon.ricochet-gun` | `RicochetGunPackage.CreateDescriptor()` | `module.weapon-ricochet-projectile` | no |

Every normal and empowered profile must retain unlimited consumable-free fire, one declared behavior module, a positive independent power-bank capacity, a positive empowered cost, and the accepted four-mount runtime boundary. The existing Stage 1 package validator remains authoritative for numeric-only empowerment and topology constraints.

## Automated verification

Run the EditMode fixture:

```text
ShooterMover.Tests.EditMode.Combat.Stage1WeaponPackageRegistryTests
```

Python 3 must be available as `python3`, `python`, or Windows `py -3`. A non-standard executable may be supplied through `SHOOTER_MOVER_PYTHON`.

The tests perform the following checks:

1. Resolve all five real package descriptors and validate the exact roster.
2. Compare each descriptor, default flag, shared-module reference, behavior-module list, unlimited-fire rule, independent power bank, and four-mount constant against the committed fixture.
3. Materialize five weapon and five supporting module descriptor inputs beneath two separate temporary directories.
4. Shuffle the second input-to-filename mapping, invoke the existing CS-011 `generate` command twice, and require byte-identical machine and review outputs with matching SHA-256 values.
5. Require deterministic generated weapon ordering: Arc Gun, Blaster Machine Gun, Ricochet Gun, Rocket Launcher, Shotgun.
6. Append drift to a temporary machine registry and require CS-011 `check` to exit with `registry-drift: failed`, including expected and actual SHA-256 values.
7. Inject a duplicate weapon descriptor and require `duplicate-definition`.
8. omit a referenced supporting module and require `missing-definition`.
9. mutate the committed package fixture in memory and require a deterministic `stale-package-input` failure.
10. replace Shotgun's empowered topology with another behavior family and require both `EmpoweredBehaviorTopologyChanged` and `BehaviorKindMismatch`.

Each test snapshots these committed files before execution and asserts that their existence and bytes are unchanged during teardown:

```text
Assets/ShooterMover/Generated/content-registry.json
Assets/ShooterMover/Generated/content-review-snapshot.json
```

All CS-011 descriptor inputs, locks, machine registries, and review snapshots are created below operating-system temporary directories and deleted after each test.

## Required proof lines

A passing run emits lines equivalent to:

```text
stage1-weapon-package-registry: ok
first_machine_sha256=<sha256>
second_machine_sha256=<same-sha256>
first_review_sha256=<sha256>
second_review_sha256=<same-sha256>
four_mount_invariant=4
no_generated_output_write=true
```

The adversarial tests emit the CS-011 failure text for drift, duplicate IDs, and missing module definitions, plus:

```text
drift_failure_exit_code=3
stale-package-input: ...
invalid_empowered_profile_failure=empowered-behavior-topology-changed
```

Attach the focused `Stage1WeaponPackageRegistryTests` log to the implementation PR. The checksum values are intentionally produced by the accepted CS-011 implementation at test time rather than frozen independently in this document.

## Manual no-generated-diff review

Review the implementation PR's changed-file list and confirm that it contains only the three WP-009-owned files and their inseparable Unity metadata. In particular, there must be no path beneath:

```text
Assets/ShooterMover/Generated/
```

Also confirm that the PR does not modify the CS-011 Python tool, package implementation folders, scenes, UI, enemies, mission state, saves, rewards, `Packages`, or `ProjectSettings`.

## Limitations

- This is EditMode authoring validation, not runtime content loading or a playable combat test.
- Supporting shared-module descriptors are temporary validation inputs owned by the fixture. Committing or regenerating the central registry remains a separately reviewed CS-011-owner action.
- The test intentionally invokes the accepted Python generator instead of reproducing its fingerprint algorithm in C#.

## Rollback

Remove the three WP-009-owned files and their Unity metadata. No generated output, package implementation, runtime state, save schema, scene, package manifest, or project setting requires rollback.

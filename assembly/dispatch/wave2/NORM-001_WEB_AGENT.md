# NORM-001 - Blaster Turret identity and registration normalization

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: NORM-001
Branch: agent/norm-001-turret-registration
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- AUD-001 through b1ea74d4efb55916483e02fc007a56b7784bd14a
- OBJ-001 through e967daee4a23ca3372de468e2a4a8d122f99eea0

Create a fresh exact-base branch and open a draft PR.

Objective

Normalize the existing Blaster Turret so a level designer can place any number
under a compatible scope and each works without scene-wide discovery or
name/hierarchy-derived identity. Preserve all accepted gameplay.

Read AGENTS.md, CURRENT_HANDOFF.json, wave2/VALIDATION.md,
ENEMY_AND_SCENE_AUDIT.md, OBJECT_AUTHORING_V1,
PLACED_OBJECT_LIFECYCLE_V1, the Blaster Turret PACKAGE.md, and the NORM-001
roadmap section.

Exclusive owned files/paths

- Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretAuthoring2D.cs
- Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurretSceneContext2D.cs
- Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/BlasterTurret.prefab
- their inseparable metadata
- Assets/ShooterMover/Tests/PlayMode/Enemies/BlasterTurretPackageTests.cs
- its inseparable metadata
- Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/PACKAGE.md

Required behavior

- serialized canonical authored placed ID
- explicit scope override, otherwise nearest compatible parent OBJ-001 scope
- turret self-registration and deterministic duplicate rejection
- remove ordinary-path FindFirstObjectByType/FindObjectsByType discovery
- rename, reparent, sibling reorder, and transform changes do not alter identity
- multiple turrets register and operate independently
- preserve targeting/rotation, angle gate, cadence, physical visible projectiles,
  player damage, destruction, destroyed-collider policy, presentation, and restart
- missing/duplicate scope or identity fails closed with useful diagnostics

Required proof

- all existing focused turret behavior tests
- two or more independently placed turrets
- rename/reparent stability, explicit versus parent scope, duplicate ID rejection
- no global search in production turret path

Forbidden

- Stage1VisibleSlice scene/controller/integration or presentation tests
- other enemy packages, reward profiles, balance changes, new attacks
- shared asmdefs, settings, packages, handoff, dispatch, generated output

If the existing Stage 1 composition requires later serialized migration, document
the exact INT-001 handoff; do not edit Stage 1 here.
```

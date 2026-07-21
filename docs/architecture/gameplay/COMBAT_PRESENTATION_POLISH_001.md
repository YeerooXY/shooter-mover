# COMBAT-PRESENTATION-POLISH-001

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact launch `main` SHA: `7b21fcf66d69a60b25b305b617af24a909054613`
- Branch: `agent/combat-presentation-polish-001-health-bars-death-vfx`
- Target: `main`
- Delivery: non-empty draft PR; no merge or auto-merge

`CURRENT_TASKS.md` was read completely before implementation. No existing reusable generic
world health-bar presenter or accepted-terminal enemy death-VFX consumer was found on launch
`main`.

PR #273 (`ENEMY-ATTACK-PATTERN-001`) was inspected before implementation. This change does not
modify its catalog, attack-pattern runtime, placement attack authority, or focused test files.

## Ownership

### Immutable health projection

`CombatHealthBarSnapshotV1` is a detached presentation snapshot containing only:

- stable entity identity;
- lifecycle generation;
- current and maximum health;
- `clamp(current / maximum, 0, 1)` normalized fill;
- alive/terminal state;
- optional immutable presentation-anchor facts.

It has no damage, healing, reset, authority, room, reward, or persistence method.

Typed read-only adapters project:

- the existing `PlayerHudHealthSnapshot`;
- the canonical `EnemyActorState` read seam used by current Unity enemies;
- the factory-oriented `EnemyRuntimeProjection` used by future generic enemy composition.

### Reusable world bar

`CombatHealthBarPresenter2D` retains only `ICombatHealthBarSnapshotSourceV1`. It:

- binds one exact entity identity;
- rejects another identity and stale lifecycle snapshots;
- accepts a newer lifecycle as a presentation reset;
- changes line-renderer geometry only when its immutable snapshot changes;
- hides on terminal state and restores on a newer alive lifecycle;
- uses world-space horizontal line geometry, so it follows the enemy position without inheriting
  enemy rotation;
- creates no canvas, raycast owner, collider, or rigidbody.

The Mobile Blaster Droid and Blaster Turret are registered through the same private production
binding method and the same public presenter/source contracts. No enemy definition-name or
package-type branch exists in the presenter.

### Player HUD

The existing `VisibleSliceGeneralCombatHud` remains the HUD owner. The production composition
rebinds it to `AuthoritativePlayerCombatHudSourceV1`, which replaces only the retained player vital
projection with the existing `PlayerHudHealthSnapshot` projection. Other HUD facts remain supplied
by the retained read-only HUD source. The bar therefore keeps exact fractional current/maximum
health and lifecycle generation without reading a legacy controller health field or creating a
second HUD authority.

### Enemy terminal VFX

`EnemyDeathVfxPresenter2D` consumes `EnemyTerminalPresentationFactV1`, an immutable
presentation-only fact produced from an accepted `EnemyDestroyedNotification`. It verifies exact
entity identity and lifecycle, ledgers the terminal event identity, hides the bound health bar, and
spawns one shared default explosion from `DefaultCombatExplosionPool2D`.

The production integration observes accepted immutable destruction notifications already retained
for downstream reward processing. It does not infer death from renderer disappearance or missing
GameObjects.

`DefaultCombatExplosionPool2D` reuses the current default orange 24-point ring presentation and
`0.18 s` lifetime behavior in a bounded reusable pool. Pool recycle/deactivation has no gameplay
callback.

## Explosion scale policy

Before terminal presentation is removed, `EnemyPresentationBounds2D` combines renderer bounds and
falls back to collider or transform scale. Generated bar/VFX renderers are explicitly excluded.

Default scale:

```text
scale = clamp(largest presentation dimension / 1.0, 0.75, 2.25)
```

No damage, XP, drop, health, definition-name, or reward value participates in VFX scale.

## Focused automated coverage

EditMode coverage includes:

- full, half, zero, and fractional projection;
- entity mismatch and stale lifecycle rejection;
- terminal hide and restart restore;
- independent generic enemy bars with no bar-owned physics;
- exact terminal replay, wrong identity, stale lifecycle, and new-lifecycle VFX behavior;
- presentation-bound scaling and clamp behavior;
- explosion instances with no collider or rigidbody.

The compact PlayMode smoke uses two differently scaled generic enemy presentations and the existing
`EnemyActorStepper` authority. It damages one, verifies only its bar changes, kills it, projects one
explosion, verifies replay does not duplicate it, confirms the other enemy remains unchanged, and
rebinds the first bar after a new lifecycle.

## Manual acceptance checklist

Unity manual capture remains required in an editor-capable environment:

- [ ] player HUD at full health;
- [ ] player HUD after accepted fractional damage/healing;
- [ ] Mobile Blaster Droid world bar;
- [ ] Blaster Turret world bar;
- [ ] enemy bar shrink after accepted damage;
- [ ] enemy bar hides on accepted terminal state;
- [ ] exactly one default explosion on death/replay;
- [ ] visibly larger clamped explosion for a larger presentation;
- [ ] restart restores alive bars without replaying an old explosion.

No screenshots or recording are fabricated by this change. They should be attached to the draft PR
after the Unity PlayMode/manual pass.

## Explicit non-changes

No combat balance, damage/health authority, enemy definition, attack pattern, reward, XP, drop, room
authority, persistence, run-session truth, run-condition logic, weapon definition/execution, scene,
prefab, or `Stage1VisibleSliceController.cs` file is modified.

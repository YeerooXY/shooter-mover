# COMBAT-PRESENTATION-POLISH-001

## Fresh launch boundary

- Repository: `YeerooXY/shooter-mover`
- Rebuilt from post-PR-273 `main`: `08354d36fc8459632b895e71a925c521dbe91a72`
- Branch: `agent/combat-presentation-polish-001-health-bars-death-vfx`
- Previous draft preserved at: `backup/combat-presentation-polish-001-pre-review-fixes`
- Delivery remains draft-only; no merge or auto-merge

The earlier draft was discarded rather than layered onto divergent history. The branch merge base is now the
post-`ENEMY-ATTACK-PATTERN-001` main commit.

## Immutable health presentation

`CombatHealthBarSnapshotV1` contains only detached presentation facts:

- stable entity identity;
- lifecycle generation;
- current and maximum health;
- `clamp(current / maximum, 0, 1)` fill;
- alive or terminal state;
- optional anchor facts.

The reusable `CombatHealthBarPresenter2D` keeps only a typed read source. It cannot damage, heal, restart,
reward, or mutate an actor. It rejects another entity and stale lifecycle, hides on terminal state, and restores
when a newer alive lifecycle is projected.

## Generic production registration

Combat presentation is attached inside the existing generic `RegisterEnemy(...)` path:

```text
enemy registration
    -> CombatEnemyPresentationRegistration2D.Attach(...)
    -> transparent CombatPresentationEnemyActorAuthority2D decorator
    -> existing EnemyBinding / collider registration
```

The presentation installer contains no Mobile Blaster Droid, Blaster Turret, enemy definition ID, scene object
name, or package-type branch. Current enemies and a third test enemy use the same public registration.

The decorator delegates every authoritative method to the original `IEnemyActor2DAuthority`. It only observes
the immutable accepted `EnemyActorStepResult`, so damage, terminal state, room state and rewards remain owned by
their existing authorities.

Future factory-created enemies may use the canonical registration overload backed by
`EnemyRuntimeProjection`; no presenter changes are needed.

## Canonical terminal fact path

`EnemyDeathVfxPresenter2D` consumes one neutral `EnemyTerminalPresentationFactV1`.

Two projectors are provided:

- canonical `EnemyRuntimeComposition.EnemyDeathFactV1` -> presentation fact;
- transitional `EnemyDestroyedNotification` -> presentation fact for retained EN-002 Unity packages.

The canonical projector preserves `DeathEventStableId`, exact entity identity and lifecycle generation from the
post-PR-273 factory runtime. The transitional adapter is isolated from the presenter and may be removed when the
retained Stage 1 enemies are replaced.

The presenter ledgers exact terminal event identity per lifecycle, hides the associated bar, resolves a bounded
visual scale, and asks one shared presentation pool to play. It emits no damage, hit, room, reward, XP, drop or
persistence fact.

## Retained default explosion ownership

`CombatDeathVfxPool2D` is appearance-agnostic. It accepts an `ICombatDeathVfxFactory2D`; it does not define
sprites, animation timing, sorting, material, radius or line width.

Production loads:

```text
Resources/CombatPresentation/Stage1DefaultEnemyDeathVfx
    -> retained ExplosiveDestructionAnimation.asset
    -> SpriteAnimationCombatDeathVfxDefinitionV1
    -> shared bounded CombatDeathVfxPool2D
```

The retained asset owns frames, seconds per frame, offset, visual scale, sorting order and scaled/unscaled time.
Future edits to that asset do not require changes to death-event code.

### Current retained-asset limitation

On the reviewed main commit, `ExplosiveDestructionAnimation.asset` exists and is the scene-retained default
explosion configuration, but currently contains `frames: []`. Production still resolves and projects that exact
asset. Because it presently has no playable frame, `SpriteAnimationCombatDeathVfxFactory2D` delegates to the
explicit `FallbackRingCombatDeathVfxFactory2D`. The fallback is isolated behind the factory and disappears
automatically as soon as retained frames are authored.

No claim is made that a sprite animation was played while the retained asset has zero frames.

## Explosion size

Before terminal presentation is removed, visual bounds are measured from non-line renderers, then colliders, then
transform scale. Generated health/VFX visuals and transient line renderers are excluded.

```text
scale = clamp(largest presentation dimension / 1.0, 0.75, 2.25)
```

Damage, health, XP, drops and definition names are never used for visual size.

## Focused coverage authored

EditMode coverage includes:

- full, half, zero and fractional health;
- identity mismatch, stale lifecycle, terminal hide and restart restore;
- three independent enemies through one generic registration;
- transparent authority delegation and exact terminal replay;
- canonical `EnemyDeathFactV1` projection and lifecycle replay rules;
- injected VFX-factory ownership and no VFX physics/gameplay components;
- production source audit for zero package-specific presentation installation;
- retained explosion resource GUID proof;
- scale clamps.

The PlayMode smoke:

1. loads the real retained default explosion resource;
2. registers three differently sized generic enemy objects through one path;
3. damages and kills one through its existing authority;
4. observes one explosion and no replay duplicate;
5. verifies the other enemies remain unchanged;
6. verifies larger presentation bounds resolve a larger scale;
7. reconstructs a newer lifecycle and restores the bar;
8. verifies pooled presentation cleanup.

The smoke explicitly accepts the procedural fallback only while the retained resource reports zero frames.

## Validation still required

The available connector environment cannot launch Unity. Before merge, produce:

- zero-failure Unity compilation;
- zero-failure EditMode XML;
- zero-failure PlayMode XML;
- player HUD screenshots at full and damaged fractional health;
- moving-droid and turret bars;
- bar shrink and terminal hide;
- one retained/fallback explosion with replay safety;
- larger presentation producing visibly larger VFX.

No screenshots, XML or Unity pass result are fabricated by this branch.

## Explicit non-changes

No combat balance, player/enemy health authority, damage authority, attack pattern/catalog, Combat Hit Policy,
room authority, rewards, XP, drops, persistence, Run Session/condition truth, weapon definition/execution, scene,
or `Stage1VisibleSliceController.cs` file is modified.

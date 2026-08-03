# Enemy Maker

Launch from the repository root:

```powershell
.\tools\enemy-maker\Start-EnemyMaker.ps1
```

The browser editor writes canonical enemy definitions directly to:

```text
Assets/ShooterMover/Content/Definitions/Enemies/
```

There is no editor-only enemy format and no enemy catalogue.

## First body shape

The first implementation supports circles:

```json
"body": {
  "shape": "circle",
  "radius": 0.45,
  "offset": { "x": 0, "y": 0 }
}
```

The top-level `body.shape` discriminator is intentionally permanent. Later adapters can add shapes without changing ordinary enemy fields:

```json
"body": {
  "shape": "box",
  "size": { "x": 0.8, "y": 1.2 },
  "offset": { "x": 0, "y": 0 },
  "angle": 0
}
```

```json
"body": {
  "shape": "ellipse",
  "size": { "x": 0.8, "y": 1.2 },
  "offset": { "x": 0, "y": 0 },
  "angle": 0
}
```

```json
"body": {
  "shape": "polygon",
  "points": [
    { "x": -0.5, "y": -0.4 },
    { "x": 0.5, "y": -0.4 },
    { "x": 0, "y": 0.5 }
  ],
  "offset": { "x": 0, "y": 0 },
  "angle": 0
}
```

A triangle is a polygon with three points. A square is a box with equal width and height. An oval is an ellipse.

The first validator fails closed for those planned shapes instead of pretending the current runtime supports them. Adding one later requires a shape validator, preview renderer, and Unity collider adapter; enemy identity, combat, movement, scaling, level placement, and art references remain unchanged.

## Leveling

`leveling.json` owns the global exponential strength target, enemy damage power, and level-color stops. Each enemy owns one numeric `scale` that controls how much of the strength curve its health follows.

```text
strength(level) = strengthAtMax ^ normalizedLevel
health(level)   = baseHealth × strength(level) ^ enemy.scale
damage(level)   = baseDamage × strength(level) ^ damagePower
```

Gun-using enemies do not author copied damage. Their canonical gun execution receives the enemy damage multiplier later when the runtime integration is implemented.

## Deliberate boundary

This first slice is an authoring tool and schema validator. It does not add a replacement enemy spawner, `EnemyFactory`, enemy-owned guns/projectiles, Unity collider realization, or gameplay enemies.

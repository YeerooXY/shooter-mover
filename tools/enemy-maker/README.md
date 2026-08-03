# Enemy Maker

Launch from the repository root:

```powershell
.\tools\enemy-maker\Start-EnemyMaker.ps1
```

The browser editor writes canonical enemy definitions directly to:

```text
Content/Enemies/
```

It discovers canonical guns directly from:

```text
Content/Weapons/<category>/<family>/weapon.json
Content/Weapons/<category>/<family>/mk1.json
Content/Weapons/<category>/<family>/mk2.json
Content/Weapons/<category>/<family>/mk3.json
```

There is no editor-only enemy format, enemy catalogue, or Item Package dependency.

## Identity safety

A loaded enemy ID is read-only in the guided editor. The server also rejects changing `previousId` into a different ID, so an accidental edit cannot silently leave the old JSON behind and create a duplicate enemy.

Use **New enemy** to create another identity. Explicit rename support can be added later as an atomic operation.

## Canonical guns

Shooter enemies must select a definition discovered under `Content/Weapons`. The server rejects unknown gun IDs. The enemy JSON stores only the canonical reference:

```json
"gun": "rattler.mk1"
```

It does not copy damage, cadence, spread, projectile, explosion, or status values.

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

Gun-using enemies do not author copied damage. Their canonical gun execution receives the enemy damage multiplier later when runtime integration is implemented.

## Checks

```text
node --check tools/enemy-maker/enemy-schema.js
node --check tools/enemy-maker/server.js
node --check tools/enemy-maker/app.js
node tools/enemy-maker/test-enemy-maker.js
node tools/enemy-maker/test-enemy-maker-server.js
```

The server test creates a temporary repository, discovers canonical guns, saves and reloads an enemy, and verifies that duplicate creation, ID mutation, unknown guns, and reserved future collider shapes fail closed.

## Deliberate boundary

This first slice is an authoring tool and schema validator. It does not add a replacement enemy spawner, `EnemyFactory`, enemy-owned guns/projectiles, Unity collider realization, Level Maker placement integration, or playable enemies.

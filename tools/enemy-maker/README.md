# Enemy Maker

Launch from the repository root:

```powershell
.\tools\enemy-maker\Start-EnemyMaker.ps1
```

The browser editor writes canonical schema-1 enemy definitions directly to:

```text
Content/Enemies/
```

Reusable enemy projectile mechanics live under:

```text
Content/EnemyShots/
```

There is no editor-only enemy format and no enemy catalogue.

## Current authoring boundary

An enemy definition owns:

- identity and tags;
- base health and health scaling power;
- one movement object;
- detection range;
- relative weapon mounts;
- zero, one, or many attacks;
- drops, art, and a circle body.

A shot attack references one reusable Enemy Shot, one or more mount IDs, and an explicit firing pattern. It owns its timing, valid range, and damage.

```json
{
  "id": "dual-burst",
  "kind": "shot",
  "shot": "small-bullet",
  "emitters": ["left-gun", "right-gun"],
  "firePattern": "simultaneous",
  "cooldown": 1.5,
  "sequence": {
    "triggers": 4,
    "interval": 0.2
  },
  "volley": {
    "shotsPerTrigger": 1,
    "spread": 0,
    "distribution": "even"
  },
  "range": {
    "min": 2,
    "max": 12
  },
  "damage": [
    { "type": "kinetic", "amount": 3 }
  ]
}
```

`shotsPerTrigger` applies to every emitter that fires during that trigger. Two simultaneous emitters, four triggers, and one shot per trigger produce eight projectiles. With `alternate` or `round-robin`, one emitter fires per trigger.

## Relative mount coordinates

Mounts use enemy-local coordinates relative to the visual root:

```text
+Y = forward
+X = right
rotation 0° = forward
positive rotation = clockwise
```

The collider's `body.offset` is independent. Moving the collider does not move the guns.

The preview renders mounts as orange handles. Dragging a handle updates its relative X/Y values.

### Mount editing controls

Every completed mount drag creates one undo step. Use **Undo drag** or press **Ctrl/Cmd+Z** while the preview is focused and a text field is not being edited. Manual X/Y edits clear the drag history so an old drag cannot unexpectedly overwrite a newer typed coordinate.

The **Mount zoom** control enlarges the enemy body and local mount geometry from 50% to 500%. The mouse wheel also zooms while the pointer is over the preview. Detection and attack-range rings remain at their overview scale so mount detail can be inspected without losing the combat-range reference.

## Enemy Shots

Enemy Shot definitions contain reusable projectile delivery, impact, and art only. They do not contain enemy damage, cooldown, trigger count, spread, or mount placement.

```json
{
  "schema": 1,
  "id": "small-bullet",
  "delivery": {
    "kind": "projectile",
    "speed": 32,
    "radius": 0.06,
    "range": 18
  },
  "impact": {
    "pierce": 1,
    "ricochet": 0,
    "knockback": 0
  },
  "art": {
    "delivery": "enemy-shot.small-bullet",
    "trail": "enemy-trail.small-bullet",
    "impact": "enemy-impact.small-bullet"
  }
}
```

The Enemy Maker currently discovers and validates shots but does not edit shot files. A focused Shot Maker can be added later if hand-authoring two or three reusable shots becomes painful.

## Guided editor limits

The guided attack card edits one direct-damage component. Advanced JSON can author additional direct components or a complete DoT package:

```json
{
  "type": "thermal",
  "perSecond": 2,
  "duration": 4,
  "stack": "refresh"
}
```

The guided editor preserves additional damage components.

The first iteration supports attack kinds:

```text
shot
contact
suicide
```

It deliberately does not add combat states, traits, bosses, arbitrary condition trees, a runtime spawner, or an encounter generator yet.

## What Save does today

Saving writes or replaces one validated canonical file under `Content/Enemies/`. The file can immediately be reopened in Enemy Maker, reviewed in Git, copied between branches, and consumed by tests or future importers.

Saving does **not** currently create a Unity prefab, register a playable enemy, place it in a level, or execute its attacks. Runtime integration is the next boundary. A playable vertical slice needs:

1. a schema-1 Unity/engine importer;
2. an enemy visual and collider realization;
3. mount transforms created from the saved relative coordinates;
4. an attack scheduler that selects emitters and trigger patterns;
5. Enemy Shot projectile execution;
6. a test-spawn route, followed by Level Maker discovery and placement.

Until that importer exists, the saved JSON is authoritative authored content rather than a live gameplay object.

## Leveling

`Content/Enemies/leveling.json` owns the global exponential strength target, damage power, and level-color stops.

```text
strength(level) = strengthAtMax ^ normalizedLevel
health(level)   = baseHealth × strength(level) ^ healthPower
damage(level)   = baseDamage × strength(level) ^ damagePower
```

Damage scaling is applied exactly once to every authored damage component.

## Checks

```text
node --check tools/enemy-maker/enemy-schema.js
node --check tools/enemy-maker/server.js
node --check tools/enemy-maker/app.js
node tools/enemy-maker/test-enemy-maker.js
node tools/enemy-maker/test-enemy-maker-server.js
```

# Enemy Movement Design Notes

Status: design direction only — do not implement all of this yet.

The enemy format should stay small. Movement is selected with a short `move` field, and the most common behavior should require no field at all.

## Basic rule

If `move` is omitted, the enemy uses `direct` movement:

- Move toward the player's current position.
- Do not predict where the player will be.
- Initial implementation may move in a straight line without navigating around props.
- Obstacle avoidance/pathfinding can be added later without changing enemy files.

```json
{
  "id": "blaster-droid",
  "type": "shooter",
  "hp": 16,
  "gun": "rattler.mk1"
}
```

The example above implicitly uses `move: "direct"`.

An explicit override looks like this:

```json
{
  "id": "strafe-droid",
  "type": "shooter",
  "hp": 18,
  "move": "strafe",
  "gun": "rattler.mk1"
}
```

## Planned movement types

### `direct` — default

Moves straight toward the player's current position. Useful for chasers, self-destruct enemies, melee enemies, and simple shooting enemies.

### `wander`

Moves in random directions, pauses, then chooses another direction. A shooting enemy can fire during its pauses; firing remains part of the attack behavior rather than duplicating gun data inside movement.

### `fly`

Moves without being blocked by ordinary walls or props. It should still respect the playable-area boundary. The first version can otherwise pursue the player like `direct`.

### `strafe`

Moves aggressively around or across the player instead of only closing distance. Intended for mobile ranged enemies and fast aggressive enemies.

### `stationary`

Does not move. Useful for turrets, traps, fixed artillery, environmental enemies, and some boss phases.

## Future movement and coordination ideas

These are intentionally deferred, but should remain possible:

- Better obstacle navigation for `direct`, including moving around props and walls.
- Patrol movement that guards a defined area, follows points, and returns when pulled too far away.
- Guard/leash behavior that protects a location rather than chasing forever.
- Bosses may reuse any normal movement type, switch movement type between phases, or receive a custom movement implementation only when genuinely necessary.
- Flying enemies may later combine flying traversal with patterns such as strafing or wandering if the game needs that distinction.

## Group alert idea

A future enemy group may begin in `wander`. When any member is attacked, that member alerts the others and the whole group switches to targeting the player with the normal default pursuit behavior.

This should not become a special movement type. It is coordination/state logic layered above movement:

1. Group starts idle or wandering.
2. One member is attacked or detects the player.
3. It raises an alert for its group.
4. Group members acquire the player as their target.
5. Their active movement changes to `direct` or another configured combat movement.

A future lightweight field could identify the group, for example:

```json
{
  "id": "guard-drone",
  "type": "shooter",
  "move": "wander",
  "group": "warehouse-guards",
  "gun": "rattler.mk1"
}
```

The exact group format should only be added when group behavior is implemented.

## Design constraints

- Use short author-facing names such as `move`, not long namespaced references.
- Omit default values from enemy JSON.
- Movement controls locomotion; canonical guns still control weapon statistics and firing results.
- Prefer reusable movement types over enemy-specific movement classes.
- Add custom boss movement only when combinations of the standard types are insufficient.
- Do not prebuild every future behavior now. Preserve the direction, then implement each type when an enemy actually needs it.

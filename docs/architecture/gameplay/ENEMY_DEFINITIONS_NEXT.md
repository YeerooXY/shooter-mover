# Enemy definitions: next authoring boundary

Status: approved design direction. This reset does not implement the replacement runtime yet.

## Goal

Enemy files should describe what makes an enemy different, not repeat namespaces, defaults, gun mechanics, or engine wiring.

A normal ranged enemy should be close to this:

```json
{
  "id": "blaster-droid",
  "type": "shooter",
  "hp": 16,
  "gun": "rattler.mk1"
}
```

The author-facing format uses short local IDs and short field names. Runtime namespaces such as `enemy.` or `gun.` may be derived internally where a canonical runtime identity requires them.

## Small ordinary schema

Likely common fields:

- `id` — short enemy identity.
- `type` — broad behavior such as `shooter`, `chaser`, `turret`, `pouncer`, or `popcorn`.
- `hp` — base health.
- `speed` — optional movement-speed override.
- `move` — optional movement behavior; omitted means `direct`.
- `gun` — optional reference to a canonical gun definition.
- `damage` — only for a genuinely non-gun attack such as contact, charge, or self-destruction.
- `range` — optional behavior/engagement override, not a duplicate projectile range.
- `drops` — optional drop-profile shorthand.
- `art` — optional presentation override when convention cannot resolve it.

Fields should be omitted when their default is correct.

## Movement

`move` controls locomotion only. It does not own gun statistics, targeting truth, group coordination, or rewards.

Planned reusable values:

- `direct` — default; move toward the player's current position without prediction. The first version may travel straight toward the target. Obstacle navigation can improve later without changing enemy files.
- `wander` — choose a random direction, move, pause, and repeat. A shooter may fire during pauses through its attack behavior.
- `fly` — ignore ordinary walls and props while respecting the playable-area boundary.
- `strafe` — move aggressively around or across the player.
- `stationary` — do not translate; useful for turrets, traps, artillery, and boss phases.

Future patrol, guard/leash, obstacle navigation, and boss-phase switching should build on these reusable behaviors. Add a custom boss movement only when normal behaviors or combinations cannot express it.

## Group alert is not movement

A future group may begin wandering, then switch to combat when one member is attacked:

1. the group begins idle or wandering;
2. one member is attacked or detects the player;
3. it raises an alert for the group;
4. group members acquire the player;
5. each member changes to its configured combat movement, commonly `direct`.

That is coordination and target-state logic layered above movement. A future lightweight `group` field may identify members, but it should be introduced only when group behavior is implemented.

## Canonical guns are shared

Enemies and players use the same canonical gun definitions and the same actor-neutral shot execution.

A gun definition owns:

- damage and damage type;
- cadence and burst behavior;
- projectile count and spread;
- projectile speed, radius, and range;
- pierce, ricochet, homing, explosion, damage-over-time, and status behavior;
- projectile and impact presentation.

An enemy owns:

- body, health, movement, perception, and target choice;
- a reference to the gun it uses;
- muzzle or mount placement when the default is insufficient;
- attack-state gating and preferred engagement behavior;
- non-gun attacks such as contact, melee, charge, or self-destruction.

Do not introduce `EnemyGun`, `EnemyBullet`, `EnemyRocket`, or enemy-authored copies of canonical gun damage, spread, speed, range, explosion, or status values.

Player and enemy firing components may remain thin actor adapters because input, AI decisions, faction, muzzle selection, and targets differ. Both adapters must invoke the same shared gun execution path.

Enemy-only guns may still live in the canonical gun catalogue. Availability metadata can prevent them from appearing in player inventory, rewards, or Strongboxes without creating another weapon system.

## Advanced content

An ordinary enemy should not need an `attacks` array. Add an advanced section only for enemies with multiple attacks, phases, traits, or special transitions.

Tier scaling should apply generic modifiers to the resolved enemy and canonical gun execution profile. It should not create another copy of gun statistics inside the enemy definition.

## Reset boundary

This repository reset removes all authored enemy catalogues, catalogue assets, built-in enemy room mappings, dependent levels, and old authoring examples.

Some compiled legacy catalogue/runtime types remain temporarily because neutral room, lifecycle, death, reward, and presentation integrations still depend on their contracts. They are compatibility scaffolding, not the next authoring format.

Until the lightweight replacement lands:

- do not add new JSON using the retired catalogue schema;
- do not add enemy-owned projectile or gun statistics;
- do not create concrete enemies through the legacy catalogue;
- preserve generic health, lifecycle, hit, death, room, reward, trait, and presentation seams that the replacement can reuse;
- implement the replacement incrementally, beginning with one compact enemy definition and one shared-gun ranged enemy.

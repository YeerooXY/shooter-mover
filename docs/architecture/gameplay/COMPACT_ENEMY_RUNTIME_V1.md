# Compact Enemy Runtime V1

This vertical slice connects the canonical Enemy Maker schema directly to room gameplay without restoring the deleted enemy catalogue/factory architecture.

## Flow

```text
Content/Enemies/*.json + Content/EnemyShots/*.json
    -> tools/enemy-maker/runtime-export.js
    -> Resources/Enemies/CompactEnemyCatalog.json
    -> CompactEnemyCatalog
    -> enemy.<id> room placement
    -> room-owned code-built GameObject
    -> CompactEnemy
```

Enemy Maker refreshes the generated runtime projection on startup and after saving an enemy or global leveling data.

## Implemented

- schema-1 enemy and Enemy Shot import;
- room object registration for every validated enemy definition;
- code-built circle body and relative gun mounts;
- level health, damage, and color resolution;
- direct, strafe, wander, fly/direct, and stationary movement;
- shot, contact, and suicide attack kinds;
- single, simultaneous, alternate, and round-robin emitter selection;
- trigger sequences, intervals, shots per trigger, even/random spread;
- kinematic enemy projectiles with speed, radius, and range;
- player-projectile hits through the shared `Damageable` boundary;
- enemy damage through the retained player damage authority;
- authoritative room terminal reporting when an enemy dies.

## First-slice limits

- room placements currently spawn at enemy level 1;
- only direct `amount` damage executes; authored damage-over-time packages are retained but not run;
- kinetic and thermal are the supported player-damage channels; other authored damage types fail closed;
- Enemy Shot pierce, ricochet, knockback, trail, and impact art are not executed yet;
- enemy and projectile art IDs currently use code-built placeholder sprites;
- drops, rewards, traits, status effects, and encounter generation are not connected;
- no broad Unity test suite was added in this iteration.

The runtime remains deliberately generic: no Gunner Droid ID switch exists. Gunner Droid is merely the first schema-1 definition available to exercise the path.

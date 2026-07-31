# Enemy Maker

Open `index.html` in a browser.

## Workflow

1. Load a canonical catalog snapshot.
2. Edit one enemy.
3. Preview and fix validation issues.
4. Export `<enemy-id>.enemy.json`.
5. Unity can read the package through `EnemyPkgJson` and publish package sets through `EnemyPkgCompiler`.

## Catalog snapshot

```json
{
  "guns": ["gun.rattler"],
  "views": ["enemy-view.rattler"],
  "moves": ["enemy-move.chase"],
  "ai": ["enemy-ai.basic"],
  "effects": ["effect.melee-hit", "effect.enemy-blast"],
  "perks": ["enemy-perk.armored"],
  "mods": ["enemy-mod.enraged"],
  "xp": ["xp.enemy-basic"],
  "loot": ["loot.enemy-basic"]
}
```

The tool does not keep a second weapon list. Gun choices come from the loaded snapshot.

## Gun ownership

A gun attack exports only:

- gun ID
- mount IDs
- mount order
- alternating or simultaneous mode
- shot count
- shot interval

Damage, projectile count, spread, speed, range, guidance, explosion, and statuses are not editable here.

## Package

The export matches schema `1` used by:

- `EnemyPkg`
- `EnemyPkgJson`
- `EnemyPkgCheck`
- `EnemyPkgCompiler`

The current runtime still uses the legacy enemy catalog until a later migration adapter is added.

No automated tests are included yet. Unity compilation and a browser smoke pass are still required.

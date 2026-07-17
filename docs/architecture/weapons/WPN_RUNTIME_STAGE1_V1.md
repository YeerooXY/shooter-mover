# WPN-RUNTIME-001 — JSON weapon firing in Stage 1

## Dependency

- Base branch: `main`
- Exact launch SHA: `3311b88a3e34d90241bcb222703ec21024808294`
- Catalog dependency: merged WEAPON-DATA-001 / PR #202
- Typed source: `WeaponCatalogJsonImporter`, `WeaponCatalog`, and `WeaponDefinitionData`

## Runtime boundary

`WeaponDefinitionFiringAdapter` is a read-only projection over the typed catalog. It does not parse JSON and does not own a second catalog. It rejects missing, preview-only, or invalid definitions.

The firing profile preserves:

- direct damage
- fire rate and derived cooldown
- projectile and burst counts
- spread
- projectile speed
- range and derived lifetime
- pierce
- explosion radius and area damage
- DoT and pool values
- chain target count and range
- knockback
- deterministic projectile radius

The source schema does not yet expose a dedicated projectile-radius field. Radius is therefore derived from projectile count, explosion radius, and pool radius without weapon-name or archetype switches.

## Stage 1 composition

The existing Stage 1 package fixture remains the selector-facing compatibility input. `Stage1WeaponRuntimeLoadoutAdapter` converts its stable package identities into an immutable four-slot payload containing concrete definition IDs:

| Stage 1 package | Catalog definition |
| --- | --- |
| Blaster Machine Gun | `blaster.mk1` |
| Shotgun | `shotgun.mk1` |
| Rocket Launcher | `rocket_launcher.mk1` |
| Arc Gun | `arc_rifle.mk1` |
| Ricochet Gun | `ricochet_weapon.mk1` |

All tuning comes from the imported JSON projection. The identity bridge contains no firing values.

`WeaponMountFiringSession` owns per-mount cooldown and shot sequence state. Applying a loadout resolves all four definitions before mutation. Restart clears cooldown and sequence state but preserves the selected immutable loadout.

## Determinism

Spread is sampled independently for each projectile from SHA-256 material containing:

- session seed
- mount slot
- concrete definition ID
- mount-local shot sequence
- projectile ordinal

The same inputs produce the same offsets. Changing the shot sequence changes the seed material, so successive shots are not forced into identical patterns.

Physical event IDs also include restart generation, mount slot, mount-local shot sequence, and projectile ordinal.

## Existing authority boundary

`BoundedProjectile2D` remains the physical projectile shell, and `CombatHit2DAdapter` remains the damage translation authority. The runtime profile carries advanced effect values for later package modules; this task does not create parallel explosion, DoT, chain, pierce, or knockback authorities.

## Manual demo

1. Open `Stage1VisibleSlice`.
2. Open the loadout selector and choose the default comparison.
3. Hold fire and observe that the blaster/arc mounts repeat faster than the shotgun and rocket mounts.
4. Observe the shotgun emit seven seeded-random pellets with a new pattern on each shot.
5. Switch to the ricochet comparison.
6. Confirm that mount two now emits one faster, longer-lived projectile instead of seven shotgun pellets.
7. Restart with `R`.
8. Confirm the selected comparison remains active and all mount cooldowns are ready again.

## Test evidence

Do not claim Unity proof unless EditMode and PlayMode XML files show zero failures. Connector-only authoring cannot produce those XML files; they remain required before this draft PR is merge-ready.

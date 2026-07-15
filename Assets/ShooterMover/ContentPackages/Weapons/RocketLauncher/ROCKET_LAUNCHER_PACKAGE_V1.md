# Rocket Launcher Package v1

## WP-005 scope

This package contributes `weapon.rocket-launcher` as one paced Physics2D projectile
with one bounded area detonation. It consumes the accepted WP-001 descriptor boundary,
WP-002 projectile shell, CB-004 execution-plan pipeline, CB-006 power fallback, and
CS-004 hit-message contract without editing any of them.

The package adds no generated registry output, scene authority, enemy state, damage
application, UI, mission, save, reward, final art, or other weapon package.

## Deterministic topology

- One fire plan emits exactly one `RocketLauncherExecutionOperation`.
- The operation carries only existing numeric values: damage intent, projectile speed,
  authored lifetime, projectile radius, and area radius.
- The shared WP-002 projectile shell handles 2D motion and physical completion.
- The package driver owns authored expiry and gates detonation exactly once.
- A valid impact wins when impact and authored expiry are observed in the same frame.
- Cancellation and session reset never detonate.
- The contact probe has no registered combat targets, so direct contact cannot create a
  second hit beside the area detonation.

The area query is bounded to 64 explicitly supplied target bindings and a maximum
10-unit radius. Bindings are sorted by stable target identity before evaluation.
`Collider2D.ClosestPoint` makes the radius boundary inclusive; targets outside the
boundary and owner colliders are excluded. Eligible contacts are translated through
the accepted `CombatHit2DAdapter`. The package records numeric damage intent but never
mutates health, shield, death, or enemy state.

## Authored numeric profiles

| Value | Normal | Empowered |
| --- | ---: | ---: |
| Damage intent | 30 | 42 |
| Projectile speed | 8 | 10 |
| Authored lifetime | 3 s | 3 s |
| Projectile radius | 0.12 | 0.12 |
| Area radius | 2 | 2.5 |
| Cadence | 0.75 s | 0.75 s |
| Recovery | 0.2 s | 0.2 s |

Both profiles preserve the same one-detonation topology, module order, cycle mode,
coefficient kinds, independent power-bank declaration, and unlimited normal fire.
When empowered power is unavailable, CB-003 selects normal fire immediately with no
consumable ammunition.

## Area-of-effect boundary fixture

The focused PlayMode fixture places:

- one target inside radius `2`;
- one box whose nearest point is exactly at radius `2`;
- one box whose nearest point is `2.001` units away;
- the owner collider inside the radius.

Expected canonical result:

```text
inside=included
exact-boundary=included
outside=excluded
owner=excluded
stable-order=target StableId ascending
```

## Manual readability note

Source and prefab inspection confirms that the package-owned impact-warning marker is
bounded to a maximum `0.5` seconds and radius `10`, contains no `SpriteRenderer`,
`Renderer`, `Canvas`, `Camera`, audio source, or screen-space component, and therefore
does not obstruct the screen. It is a presentation-only data envelope for later
readability work and carries no combat authority or final art.

A human in-editor gameplay pass is still required before merge to confirm the eventual
visible warning and impact treatment remain readable amid four-mount fire. The draft PR
must remain draft until that focused Unity log and manual observation are attached.

## Explicit non-goals

No fragmentation, cluster rockets, homing, persistent fire field, status effect,
secondary detonation, pooling framework, final effects, audio, universal weapon switch,
scene lookup, global service, generated registry edit, or 3D physics API is introduced.

## Rollback

Remove the `RocketLauncher` package folder, its paired Unity metadata, and
`RocketLauncherPackageTests.cs` with its metadata. No combat-contract rollback,
generated-registry repair, scene cleanup, save migration, package-lock change, or
ProjectSettings restoration is required.

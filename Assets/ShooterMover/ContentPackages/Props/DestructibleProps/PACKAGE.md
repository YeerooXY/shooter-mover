# Destructible Props Runtime

This package provides the session-local runtime foundation for destructible Stage 1 arena props.

## Authority boundary

- `DestructiblePropAuthority` owns immutable prop identity, maximum/current health,
  active/destroyed state, confirmed-hit replay protection, and deterministic restart.
- Damage is accepted only through `HitMessage` with `HitResult.Confirmed`.
- Repeated event IDs are ignored and conflicting event-ID reuse fails closed.
- A lethal hit creates one `DestructiblePropDestructionResult`.
- No chain reaction, radial damage, particle, audio, loot, persistence, or save authority
  is present.

## Unity bridge

- `DestructibleProp2D` applies authority results to one blocking `Collider2D` and a
  bounded presentation root.
- Destruction disables the blocking collider and cached presentation renderers.
- `Restart()` restores authored collider/renderer states and clears session event history.
- `DestructiblePropProjectileRelay2D` observes the existing WP-002 projectile completion
  result and forwards only the confirmed `HitMessage`; raw contact never mutates health.
- `DestructiblePropAuthoring2D` exposes maximum health, collider size/offset, and one
  optional destruction-animation asset for each prop variant.
- `DestructiblePropDestructionPlayer2D` listens to the existing destruction/restart
  lifecycle and plays an ordered sprite sequence without owning health or damage.

## Configuring destruction animations

The package includes two ready-to-fill assets:

- `CrateDestructionAnimation.asset`
- `ExplosiveDestructionAnimation.asset`

Select an asset and drag ordered sprites into `Frames`. Then set frame duration, visual
scale, local offset, and sorting order. Empty frame lists are valid, so prop destruction
continues to work before final VFX arrives. Restart cancels any animation in progress.

For a new prop variant, add or duplicate `DestructiblePropAuthoring2D`, set HP and
collider dimensions, and assign any destruction-animation asset. No combat-runtime
change is required.

## Stage 1 authoring defaults

- Crate maximum health: `24`
- Explosive maximum health: `12`

The explosive is an ordinary destructible prop. It has no area effect or chain behavior.

## Visible-slice handoff

After `playerHitAdapter` is created in
`Stage1VisibleSliceController.BuildSession()`, the shooting sandbox attaches every
existing grid-aligned crate/explosive collider and binds restart generation with:

```csharp
Stage1DestructiblePropIntegration.Attach(
    gameObject,
    roomPresentation.PropRoot,
    transform,
    playerHitAdapter,
    PlayerShotDamage,
    () => RestartGeneration);
```

The helper scans only the explicitly supplied presentation/collider roots, registers
the matching collider targets with the existing `CombatHit2DAdapter`, attaches the
destructible target and projectile relay, and restores all props when
`RestartGeneration` changes.

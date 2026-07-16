# Destructible Props Runtime

This package provides reusable, definition-driven 2D destructible props.

## Authority boundary

- `DestructiblePropAuthority` owns stable prop identity, health, confirmed-hit replay
  protection, terminal destruction, and restart state.
- Damage is accepted only through a confirmed `HitMessage`.
- Exact duplicate events do nothing; conflicting event-ID reuse fails closed.
- A lethal transition produces one destruction result.
- Reward generation, claims, wallets, holdings, persistence, radial damage, audio, and
  final pickup presentation remain external authorities.

## Authoring boundary

- `DestructiblePropFamilyDefinitionAsset` owns family defaults and arbitrary variants.
- Resolution is instance override, then selected variant, then family default.
- `PlacedObjectAuthoring2D` supplies the stable placed-instance identity and selected
  family/variant identity. Hierarchy names and sibling positions are never identifiers.
- `DestructiblePropAuthoring2D` requires explicit collider and intact-renderer references,
  resolves a preview, registers combat/restart participation, and composes the runtime.
- Collider shape, size, offset, intact sprite, destruction animation, destroyed collision
  policy, and inherited reward profile are definition values.
- `DestructiblePropRewardBridge2D` submits the first destruction fact through SRC-001 once;
  it owns no reward value or application truth.

## Runtime behavior

`DestructibleProp2D` keeps the existing four-argument `Configure` overload for compatible
package consumers. New authoring uses the explicit-renderer overload and one of these
post-destruction collision policies:

- disable the collider;
- keep the collider enabled as a blocker;
- keep the collider enabled as a trigger.

Restart restores health, the authored renderer-enabled states, collider enabled state,
and collider trigger state. Missing or empty destruction animation data safely produces no
animation while destruction and restart continue normally.

## Legacy host seam

`Stage1DestructiblePropIntegration.Attach` remains as a bounded migration entry point for
the existing Stage 1 composition root. It first attempts normal definition-driven
configuration. A marker with no `PlacedObjectAuthoring2D` may then use the temporary legacy
path, which:

- discovers only `DestructiblePropAuthoring2D` markers under the supplied presentation root;
- matches one unique collider by authored world position and rotation under the supplied
  collider root;
- derives a deterministic temporary identity from authored geometry and HP; and
- keeps the existing confirmed-hit, animation, and restart behavior.

The seam never reads `Crate_*`, `Explosive_*`, any other object-name prefix, hierarchy path,
or sibling index. Once a prop has a placed-object component, invalid identity, family,
variant, collider, renderer, reward, or restart configuration fails closed and cannot fall
back to legacy behavior.

See `docs/authoring/DESTRUCTIBLE_PROPS.md` for the designer workflow and validation rules.

# Destructible Prop Authoring

## Goal

A destructible prop prefab is reusable when a designer can duplicate it, select a family
and variant, assign a unique placed identity, and place it anywhere without renaming rules
or hierarchy conventions. Runtime identity and resolved values must remain unchanged when
the object is renamed or reparented.

## Components

Each placed prefab uses:

1. `PlacedObjectAuthoring2D` for the stable placed-instance ID, family definition, selected
   variant, scope binding, and generic capability fingerprint.
2. `DestructiblePropAuthoring2D` for the prop family, optional instance value overrides,
   explicit collider, explicit intact `SpriteRenderer`, optional animation anchor, hit
   damage, and optional reward-source override/sink.
3. A supported explicit `Collider2D`: `BoxCollider2D`, `CircleCollider2D`, or
   `CapsuleCollider2D`.

The authoring component creates or configures the health target, confirmed-projectile
relay, destruction animation player, restart participant, and optional reward bridge. It
does not search for a collider or renderer by name.

## Family and variants

Create a **Shooter Mover → Props → Destructible Prop Family** asset. Author:

- a canonical family ID and default variant ID;
- family defaults for HP, collider shape/size/offset, intact presentation ID and sprite,
  animation ID and asset, destroyed collision policy, and inherited reward profile ID and
  asset;
- any number of variants, each with its own canonical variant ID, optional object level,
  and field-level overrides.

Typical variants can be level 1, level 2, and reinforced, but runtime code imposes no fixed
names or count.

Resolved value order is:

```text
placed instance override
  → selected variant override
    → family default
```

The resolved preview exposes family, variant, and placed IDs; optional object level; every
resolved gameplay/presentation value; and deterministic family, variant, and resolved
fingerprints. Asset references participate through their authored stable IDs, not Unity
object names.

## Placed identity

Use a unique canonical ID such as `placed.warehouse-barrel-017`. Duplicate IDs in one
`GameplaySceneScope2D` are rejected. An invalid family, missing variant, malformed ID,
collider-shape mismatch, missing explicit reference, or conflicting registration fails
closed and returns an understandable diagnostic.

Never use an object name, hierarchy path, or sibling number as an identity. Renaming and
reparenting are safe after binding because the authored ID remains the authority.

## Collider and presentation

The selected collider type must match the resolved shape. The authoring component applies
resolved size and offset directly. On destruction, all explicitly supplied intact
renderers are hidden and collision follows the selected policy:

- **Disable**: collider disabled;
- **Keep Blocking**: collider enabled as a non-trigger;
- **Keep As Trigger**: collider enabled and converted to a trigger.

Restart restores the authored collider enabled/trigger state, renderer enabled state, and
full health.

The destruction animation asset is optional. A missing asset or empty frame list is a safe
no-op. An authored non-none animation ID without an asset is invalid, preventing a silent
broken reference.

## Rewards

A family may inherit a reward-profile asset. A placed instance may supply a
`RewardSourceOverrideAuthoring` and operation sink. On the first destruction, the prop
bridge submits one stable SRC-001 source operation. Duplicate hit callbacks, repeated
destruction callbacks, and later restart/destruction cycles do not submit a second source
notification. GEN-001, RAP-001, wallet, holdings, and claim authorities remain unchanged.

Use `reward-profile.none` with no asset for a prop that grants no reward. A non-none reward
profile ID requires an assigned profile asset.

## Prefab duplication checklist

- Assign a new unique placed-instance ID.
- Select the intended family and variant in `PlacedObjectAuthoring2D`.
- Assign the same family asset in `DestructiblePropAuthoring2D`.
- Assign the exact collider and intact sprite renderer.
- Confirm collider type matches the resolved shape.
- Add only the instance overrides that intentionally differ from the variant.
- Assign the reward operation sink when the resolved family profile is not none.
- Inspect `ResolvedPreview` during validation or future editor tooling.

No scene-specific object name or path is required.

## Validation expectations

Before merging authored content, run repository static validation, Unity GUID audit, a cold
Unity import/compile, full EditMode tests, focused prop PlayMode tests, and existing
prop-authority/runtime regressions. The focused coverage verifies arbitrary names, ten
independent instances, duplicate identity rejection, all three resolution layers, distinct
variant HP/sprites, collider values, once-only destruction and reward notification,
collision policies, optional animation, restart, rename/reparent stability, compatibility,
and deterministic fingerprints.

# COMBAT-HIT-POLICY-001 — reusable faction-aware hit eligibility

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact launch SHA: `208bc89be4ce34750213139c80399ea7983e70e5`
- Branch: `agent/combat-hit-policy-001-faction-targeting`
- Target: `main`

## Ownership

`CombatHitPolicyV1` is a stateless engine-neutral decision boundary. Unity physics may
prefilter contacts, but layers, collider names, prefabs, enemy types, and scene
controllers never authorize damage.

The effect owner supplies immutable source, effect, contact, and already-hit facts. The
policy returns an immutable disposition and replacement hit-history snapshot. It does
not own projectile or field lifetime, cooldowns, health, death, rewards, or room state.

Health remains authoritative in existing damage receivers. An accepted result may be
adapted to the existing `DamageReceiverCommand`; only the receiving authority mutates
health.

## Reused contracts

- `StableId` for actor, faction, policy, effect, blocker, and capability IDs.
- `GameplayEntityIdentity` / `GameplayEntityOwnership` for actor, faction, character,
  and run-participant attribution.
- `WeaponEffectIdentity` / `IWeaponEffectDescription` from WPN-CORE-002.
- `DamageReceiverCommand` / `IDamageReceiver` for downstream health mutation.
- `PropDamageCommandV1` for existing prop health/destruction routing.

No second player, enemy, prop, projectile, or damage authority is introduced.

## Policy inputs

Every evaluation receives immutable facts for:

- current source actor identity, faction, lifecycle generation, active state, and
  capabilities;
- effect identity, policy ID, source generation, geometry kind, self-hit and
  friendly-fire flags, world-blocker behavior, pierce, and per-target hit limit;
- actor or world-blocker contact identity, observed target generation, and deterministic
  squared distance;
- accepted total and per-target hit counts for that exact effect.

Unknown, inactive, mismatched, or stale actors; unknown policies; malformed history; and
invalid contacts fail closed.

## Stable policy IDs

- `combat-hit-policy.player-normal-v1`
- `combat-hit-policy.enemy-normal-v1`
- `combat-hit-policy.chaotic-all-factions-v1`

Player-normal and enemy-normal are effect-controlled for self-hit and friendly fire, so
ordinary effects deny both while a future authored friendly-fire effect can opt in
without an enemy-type branch. Chaotic/all-factions always allows same-faction targets but
still denies the source actor.

All actor policies require `combat-capability.damage-receiver`; a neutral prop becomes
eligible by projecting that capability, not through a prop-specific policy branch.

## Geometry and world blockers

The same result vocabulary is consumed by projectiles, explosions, melee swings,
contact attacks, persistent fields, and chain effects. A world-blocker contact returns
`Ignore`, `Terminate`, or `Reflect` without consuming pierce or actor-hit state.

For actor contacts, `pierce = 0` permits one accepted actor hit; each additional pierce
permits one more. Per-target hit limits independently control repeated overlap or tick
contact.

## Deterministic multi-target order

Contacts are ordered by:

1. squared distance;
2. world blocker before actor at an exact distance tie;
3. canonical stable contact ID;
4. observed target lifecycle generation.

The result is independent from Unity callback order and dictionary iteration.

## Integration seams

- WPN-CORE-002 projectile and chain descriptions use
  `WeaponEffectHitPolicyAdapterV1`.
- Explosion and persistent-field owners construct a distinct effect snapshot for the
  area or field lifetime while retaining source actor and generation facts.
- Enemy projectile, melee, and contact adapters supply the same actor/contact/history
  input.
- Accepted actor results convert to `DamageReceiverCommand` through
  `CombatHitDamageCommandAdapterV1`; rejected, blocker, and reflective results cannot
  produce a command.
- PROP-RUNTIME-001 consumers use `CombatHitPropDamageCommandAdapterV1` without moving
  prop health or destruction ownership into the policy.

## Focused validation

EditMode fixture:

`ShooterMover.Tests.EditMode.CombatHitPolicy.CombatHitPolicyV1Tests`

Coverage includes player/enemy/neutral-prop relations, chaotic policy, blocker
termination and reflection, per-target limits, pierce exhaustion, stale generations,
unknown and mismatched actors, missing capabilities, deterministic ordering, every
supported geometry, WPN-CORE adaptation, and downstream command projection.

# CRIT-LIVE-001 — Deterministic Critical-Hit Resolution V1

Launch base: `7b2dfb1dadb13a6d8c0631a56d10fc44f3080472`

## Ownership boundary

`CriticalHitResolutionAuthorityV1` is the single engine-neutral boundary between:

1. an accepted `CombatHitPolicyResultV1`; and
2. the existing `DamageReceiverCommand` consumed by player/enemy health authorities.

It owns only deterministic critical-hit resolution and operation replay state. It does **not** mutate health, select targets, evaluate factions, author weapon stats, own status effects, or generate rewards.

The existing health authority remains the sole mutation owner.

## Immutable inputs

Each `CriticalHitResolutionCommandV1` carries:

- stable operation identity;
- deterministic seed;
- hit sequence;
- base damage and combat channel;
- immutable `RunCombatProfileV1`;
- the accepted hit-policy result.

The run profile is produced by `DefaultDerivedCharacterStatComposerV1`, so permanent skill/equipment modifiers and run/event/status modifiers continue to flow through the shared `RuntimeModifierSnapshotV1` language. CRIT-LIVE reads only:

- `OutgoingDamageMultiplier`;
- `CriticalChance`;
- `CriticalMultiplier`;
- the run-profile fingerprint.

It does not recompute or reinterpret modifiers.

## Deterministic roll domain

The SHA-256 roll domain includes:

- operation ID;
- deterministic seed;
- hit sequence;
- source actor identity and lifecycle generation;
- source run-participant identity, when present;
- target actor identity and lifecycle generation;
- effect identity;
- hit-policy identity;
- effect geometry;
- run-combat-profile fingerprint;
- base damage;
- combat channel.

The first 48 digest bits produce a decimal sample in `[0, 1)`. The same immutable facts therefore produce the same roll sample, critical decision, final damage, and resolved-damage fingerprint on every machine.

A changed source, target, effect, sequence, seed, profile, base amount, or channel changes the domain fingerprint.

## Resolution order

1. Validate the command and accepted hit facts.
2. Resolve exact replay/conflict by operation ID.
3. Hash the immutable roll domain once.
4. Calculate ordinary damage: `base damage × outgoing damage multiplier`.
5. Compare the roll sample with critical chance.
6. For a critical hit, calculate: `ordinary damage × critical multiplier`.
7. Store one immutable resolved result in the operation ledger.
8. Project only the resolved final amount through `CriticalHitDamageCommandAdapterV1`.

The critical operation ID becomes the downstream damage event ID. Exact command re-dispatch is therefore idempotent in the receiving health authority as well.

## Replay behavior

- First valid operation: `Applied`.
- Identical operation replay: `Duplicate`, returning the original immutable resolved result.
- Same operation ID with different facts: `ConflictingDuplicate`, without rolling or producing damage.
- Invalid/non-eligible hit: `Rejected`, without consuming an applied operation.

`AppliedResolutionCount` advances only for first valid operations.

## Edge behavior

- `CriticalChance == 0`: guaranteed ordinary hit.
- `CriticalChance == 1`: guaranteed critical hit.
- Zero outgoing damage resolves to an immutable zero-damage outcome; no damage command is emitted.
- Decimal arithmetic overflow rejects before ledger insertion.
- Projectile, explosion, melee swing, contact attack, persistent field, and chain geometries use the same boundary.

## Validation coverage

Focused EditMode tests cover:

- deterministic replay across separate authorities;
- source/target/effect/sequence/seed domain separation;
- exact duplicate and conflicting duplicate behavior;
- guaranteed non-critical and critical edges;
- every supported effect geometry;
- permanent, event, and conditional status modifiers through shared run-profile composition;
- multiplayer source participant attribution;
- replay-safe downstream damage command identity;
- rejection of non-damage-eligible policy results.

No scene, Unity collision callback, weapon-specific executor, or health authority is modified.

# CRIT-LIVE-001 — Deterministic Critical-Hit Resolution V1

Launch base: `7b2dfb1dadb13a6d8c0631a56d10fc44f3080472`

## Ownership boundary

`CriticalHitResolutionAuthorityV1` sits between an accepted `CombatHitPolicyResultV1` and the existing `DamageReceiverCommand`.

It owns deterministic critical resolution and run-local operation replay state only. It does not mutate health, select targets, infer factions, author weapon stats, own status effects, or grant rewards. The existing player/enemy damage receiver remains the sole health authority.

## Explicit critical policy

Critical behavior is not inferred from effect geometry. The weapon, attack, or effect execution supplies immutable `CriticalHitEffectFactsV1` containing:

- effect-definition identity;
- explicit critical-policy identity;
- concrete equipment-instance identity when the effect is equipment-backed.

`CriticalHitPolicyRegistryV1` resolves the policy definition. The default registry provides:

- `critical-hit-policy.normal-v1` — profile chance and multiplier;
- `critical-hit-policy.cannot-crit-v1` — effective chance `0`, effective multiplier `1`;
- `critical-hit-policy.guaranteed-v1` — chance override `1`;
- `critical-hit-policy.modified-chance-v1` — data-defined chance modification;
- `critical-hit-policy.modified-multiplier-v1` — data-defined multiplier modification.

Definitions support chance and multiplier overrides, flat modifiers, and multiplicative modifiers. Unknown policy identities fail closed without consuming the operation ID.

A projectile, explosion, melee swing, contact attack, persistent field, or chain may select any policy. The same geometry can therefore be normal, non-crittable, guaranteed, or modified according to its authored execution facts.

## Shared stat composition

`RunCombatProfileV1` remains the source of:

- outgoing damage multiplier;
- character/run critical chance;
- character/run critical multiplier.

Permanent skills/equipment and temporary event/status contributions continue to flow through the existing `RuntimeModifierSnapshotV1` and `DefaultDerivedCharacterStatComposerV1`. The critical policy is applied after that shared profile is built; no second modifier system is introduced.

For `cannot-crit`, profile critical modifiers are deliberately ignored for the critical decision and multiplier. Outgoing damage still applies normally.

## Shot and hit identity

Every critical-resolution command carries two separate sequence facts:

- `ShotSequence` identifies the fire/attack execution;
- `HitOrdinal` identifies one deterministic contact, pellet, chain hop, field tick target, or other hit evaluation within that shot.

A shotgun shot may therefore use one shot sequence with ordinals `0..N-1`. Two contacts against the same target remain independent rolls when their ordinals differ. Negative shot sequences or hit ordinals reject before ledger insertion.

The compatibility constructor maps the former single `HitSequence` value to `ShotSequence` with ordinal `0`; new execution adapters should always supply both explicit facts.

## Deterministic roll domain

The SHA-256 domain includes:

- the complete command fingerprint, including accepted-hit facts and history counters;
- operation ID and deterministic seed;
- explicit shot sequence and hit ordinal;
- explicit run ID, run-context fingerprint, and run-profile fingerprint;
- equipment-instance identity when present;
- effect-definition identity and critical-policy identity;
- resolved critical-policy-definition/application fingerprint;
- source/target actor, generation, participant, character, and faction facts;
- effect-instance identity, hit-admission policy, and geometry;
- base damage and damage channel.

The first 64 digest bits produce a decimal sample in `[0, 1)`. Identical immutable facts produce the same roll, outcome, damage, and fingerprints. Changing run, equipment instance, effect definition, policy, source, target, shot sequence, hit ordinal, seed, profile, base damage, or channel changes the domain.

## Resolution order

1. Resolve exact operation replay/conflict.
2. Validate shot sequence, hit ordinal, accepted hit, run profile, and effect execution facts.
3. Resolve the explicit critical policy from the immutable registry.
4. Calculate ordinary damage: `base damage × outgoing damage multiplier`.
5. Apply policy rules to profile chance and multiplier.
6. Decide critical outcome from policy eligibility and deterministic roll.
7. For a critical hit: `ordinary damage × effective critical multiplier`.
8. Store one immutable result in the operation ledger.
9. Project only final damage through `CriticalHitDamageCommandAdapterV1`.

The critical operation ID is reused as the downstream damage event ID, preserving health-authority idempotency.

## Replay behavior

- First valid operation: `Applied`.
- Exact replay: `Duplicate`, returning the original immutable resolved object.
- Same operation ID with changed facts, including changed shot sequence, hit ordinal, policy, equipment, effect, or target identity: `ConflictingDuplicate`.
- Invalid or non-damage-eligible input: `Rejected` without consuming an applied operation.

## Focused coverage

EditMode tests cover:

- deterministic equality for identical immutable facts;
- explicit run, equipment-instance, and effect-definition domain separation;
- same shot and target with ordinal `0` versus `1` producing independent roll domains;
- shot `10` versus `11` with the same ordinal and target producing independent roll domains;
- the same shot and ordinal with a changed target producing independent roll domains;
- exact replay and conflicting operation reuse when shot sequence or hit ordinal changes;
- outgoing damage before critical multiplication;
- `100%` character crit chance plus `cannot-crit` producing ordinary damage;
- guaranteed, modified-chance, and modified-multiplier policies;
- same geometry selecting different critical policies;
- every supported geometry respecting an explicit non-crittable policy;
- permanent, event, and conditional status modifiers through shared profile composition;
- multiplayer attribution and replay-safe downstream command identity;
- unknown policy, negative ordinal, and non-eligible hit rejection.

No scenes, collision callbacks, weapon-specific controllers, health authorities, reward systems, inventory systems, or status authorities are modified.

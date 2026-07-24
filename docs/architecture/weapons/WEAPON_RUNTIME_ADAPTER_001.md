# WEAPON-RUNTIME-ADAPTER-001 — Effective weapon to current runtime

## Scope

This change adds one typed, engine-independent migration seam from an immutable `EffectiveWeapon` plus one already accepted firing-schedule entry into the retained:

- `WeaponRuntimeFiringProfile` execution-era profile;
- `WeaponBehaviorRegistry` behavior boundary;
- immutable `WeaponEffectBatch` output boundary.

It does not replace the current scheduler, mutate `EffectiveWeapon`, change `WeaponExecutionCore`, introduce production composition, or spawn Unity objects.

## Typed seam

`IEffectiveWeaponRuntimeAdapter.Adapt(EffectiveWeapon, WeaponFiringScheduleEntry)` is the only new adapter entry point.

`WeaponFiringScheduleEntry` is an immutable description of one shot already accepted by the scheduling authority. It freezes:

- the existing `WeaponFireCommand`;
- the resolved `RunParticipantId`;
- the deterministic shot sequence;
- the scheduler-owned cooldown in simulation ticks.

It owns no trigger state, cooldown state, cadence calculation, replay ledger, registry, sink, or scene lifetime. A future firing scheduler can create this value directly and replace the old catalog-profile path without changing the adapter contract.

## Identity preservation

The adapter requires the effective equipment-instance ID to equal the command equipment-instance ID. It then uses the existing `WeaponBehaviorContext` and validates every emitted effect identity against the supplied inputs.

The following identities are preserved exactly:

- actor instance ID;
- run participant ID;
- equipment instance ID;
- weapon definition ID;
- fire operation ID;
- lifecycle generation;
- shot sequence;
- deterministic projectile ordinal.

An identity mismatch rejects the entire adaptation. No corrected, guessed, or substituted identity is emitted.

## Existing authorities reused

The adapter constructs the existing `WeaponRuntimeFiringProfile`, resolves an exact behavior from the injected existing `WeaponBehaviorRegistry`, and asks that behavior to produce the existing `WeaponEffectBatch`.

It introduces no second:

- runtime profile type;
- behavior registry;
- behavior-selection service;
- effect-batch type;
- effect sink;
- damage authority;
- projectile runtime;
- firing scheduler.

Behavior selection is based only on representable modular structure. There is no weapon-definition-ID switch and no fallback to another behavior.

## Lossless compatibility set

The temporary legacy runtime can represent only a narrow subset of the modular model without losing behavior.

### Regular projectiles

Supported when all of the following are true:

- fire mode is semi-automatic, automatic, or burst;
- the firing scheduler has already produced the concrete schedule entry;
- shot pattern is `Single` or `Spread`;
- one pulse, no pulse interval, and no additional pattern randomness;
- projectile kind is `RegularProjectile`;
- guidance is unguided;
- no ricochet or modular effects;
- fixed-point pierce converts exactly to the legacy integer value;
- impact and termination settings match the current direct-projectile runtime behavior.

### Rocket explosions

Supported only for the exact current explosive-projectile behavior:

- rocket projectile;
- zero pierce;
- positive area damage and explosion radius;
- full retained explosion damage (`MinimumDamageMultiplier == 1`);
- explosion on enemy impact, wall impact, range expiry, and termination;
- stop on the first blocking impact;
- no homing, ricochet, or additional effect kind.

### Chain arcs

Supported only for the exact current chain behavior:

- beam delivery with no projectile;
- positive direct damage;
- no area damage;
- full retained damage per jump (`RetainedDamagePerJump == 1`);
- no projectile-impact policy;
- no additional effect kind.

## Explicit failures

The adapter returns a typed status and stable rejection code instead of downgrading or substituting behavior.

Current explicit failures include:

- continuous fire;
- pulse and pulse-spread scheduling;
- authored pattern randomness not represented by the current behavior;
- fractional pierce;
- orb projectiles;
- homing and reacquisition;
- ricochet and post-bounce guidance pause;
- damage over time and stacking/refresh semantics;
- mixed modular effect kinds;
- explosion falloff not representable by the current batch;
- chain damage retention below one;
- unsupported impact/termination combinations;
- missing exact behavior registration;
- behavior rejection or invalid output batch.

These features remain available in the modular domain/application models. Their rejection here means only that the current legacy runtime batch cannot preserve them exactly yet.

## Batch validation

After the existing registered behavior builds a batch, the adapter validates:

- exact effect count;
- exact fire identity and projectile ordinals;
- exact effect kind for the selected behavior;
- exact damage, range, speed, pierce, explosion, chain, knockback, and damage-category payloads representable by that effect type.

Any mismatch rejects the whole adapter result. The adapter never submits a partial batch and owns no sink transaction.

## Excluded changes

This task intentionally makes no changes to:

- Stage 1 or `Stage1VisibleSlice` ownership;
- scenes, prefabs, resources, or Unity adapters;
- reflection, scene discovery, static service locators, or runtime initialization;
- `WeaponExecutionCore` state/replay/cooldown ownership;
- catalog mapping or effective-profile evaluation;
- projectile, guidance, impact, or effect-resolution authorities;
- production loadout, strongbox, simulator, or persistence composition;
- automated tests during the current prototype phase.

## Migration path

A later production integration can inject `IEffectiveWeaponRuntimeAdapter` beside the accepted modular firing scheduler, pass its returned `WeaponEffectBatch` to the existing sink, and retire the old catalog-derived `WeaponRuntimeFiringProfile` resolver only after all required modular features have runtime support.

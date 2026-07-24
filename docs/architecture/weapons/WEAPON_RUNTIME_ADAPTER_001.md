# WEAPON-RUNTIME-ADAPTER-001 — Effective weapon to current runtime

## Scope

This change adds one typed, engine-independent migration seam from an immutable `EffectiveWeapon` plus one bound firing-schedule handoff into the retained:

- `WeaponRuntimeFiringProfile` execution-era profile;
- `WeaponBehaviorRegistry` behavior boundary;
- immutable `WeaponEffectBatch` output boundary.

It does not replace the current scheduler, mutate `EffectiveWeapon`, change `WeaponExecutionCore`, introduce production composition, or spawn Unity objects.

## Typed seam

`IEffectiveWeaponRuntimeAdapter.Adapt(EffectiveWeapon, WeaponFiringScheduleEntry)` is the only adapter entry point.

`WeaponFiringScheduleEntry` freezes:

- the existing `WeaponFireCommand`;
- the resolved `RunParticipantId`;
- the exact weapon definition ID;
- the exact equipment instance ID;
- a deterministic fingerprint of the effective combat snapshot;
- the deterministic shot sequence;
- the scheduler-owned cooldown in simulation ticks;
- a deterministic schedule-entry fingerprint.

Its constructor is internal. External callers cannot construct an entry and claim that it was accepted by the firing scheduler.

The repository does not yet contain a merged `WEAPON-FIRING-001` implementation or canonical accepted-schedule output. Therefore this handoff is intentionally not described as the final scheduler authority. The future scheduler in the same application assembly must become its sole producer or replace it with its canonical output type before live composition adopts this adapter.

## Schedule-to-weapon binding

The adapter does not recalculate cadence. It proves that the supplied schedule handoff belongs to the supplied effective weapon by requiring:

- command equipment instance equals schedule equipment instance;
- schedule equipment instance equals effective equipment instance;
- schedule weapon definition equals effective weapon definition;
- schedule effective-profile fingerprint equals a fresh fingerprint of the supplied `EffectiveWeapon`.

The fingerprint covers every effective combat field consumed or rejected by this adapter:

- fire mode and cadence values;
- burst and trigger counts;
- shot-pattern count, spread, randomness, pulses, and pulse interval;
- projectile kind, speed, range, fixed-point pierce, and termination;
- guidance and reacquisition;
- impact, ricochet, and explosion-trigger policy;
- direct, area, DoT, knockback, and typed damage;
- explosion, DoT, and chain effect configuration;
- equipment and definition identity metadata.

A stale schedule created before an augment or effective-profile change therefore fails even when the equipment instance ID is unchanged.

## Identity preservation

The adapter uses the existing `WeaponBehaviorContext` and validates every emitted effect identity against the bound inputs.

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
- a bound schedule handoff already exists;
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

Chain support is not claimed by this adapter.

The modular domain requires beam delivery to use continuous fire, while the retained runtime profile and batch path do not preserve continuous tick cadence. Chain weapons therefore return the explicit rejection code:

`weapon-runtime-adapter-chain-unsupported`

There is no unreachable chain-profile builder and no dead supported path.

## Explicit failures

The adapter returns a typed status and stable rejection code instead of downgrading or substituting behavior.

Current explicit failures include:

- continuous fire;
- chain arcs;
- pulse and pulse-spread scheduling;
- `TwinBarrel`, `Volley`, `Beam`, and `Spray` shot-pattern semantics;
- authored pattern randomness not represented by the current behavior;
- fractional pierce;
- orb projectiles;
- homing and reacquisition;
- ricochet and post-bounce guidance pause;
- damage over time and stacking/refresh semantics;
- mixed modular effect kinds;
- explosion falloff not representable by the current batch;
- unsupported impact/termination combinations;
- missing exact behavior registration;
- behavior rejection or invalid output batch.

These features remain available in the modular domain/application models. Their rejection here means only that the current legacy runtime batch cannot preserve them exactly yet.

## Task 9 readiness

With the current compatibility set, the representative weapons are expected to remain approximately:

| Representative weapon | Adapter readiness |
|---|---|
| Physical shotgun | Supported when authored as ordinary spread projectiles |
| Plasma machine gun | Supported when authored as ordinary automatic projectiles |
| Pulse shotgun | Pending pulse schedule/runtime projection |
| Dual-barrel rocket launcher | Pending `TwinBarrel` runtime projection |
| Sniper rifle | Supported when authored as an ordinary direct projectile |
| Homing thermal DoT orb | Pending orb, guidance, and DoT runtime integration |
| Five-shot burst gun | Supported only after the canonical scheduler proves burst timing |

Task 9 should not be treated as fully proven by this PR. Before the seven-weapon representative set is made playable, follow-up runtime integration should add reusable support for:

- `TwinBarrel`;
- `PulseSpread`;
- orb projectile execution;
- guidance/homing;
- DoT emission and resolution.

Those integrations must consume the already merged projectile, guidance, impact, and effect components. They must not add weapon-specific runtime classes or silently flatten modular behavior into the legacy effect descriptions.

## Batch validation

After the existing registered behavior builds a batch, the adapter validates:

- exact effect count;
- exact fire identity and projectile ordinals;
- exact effect kind for the selected behavior;
- exact damage, range, speed, pierce, explosion, knockback, and damage-category payloads representable by that effect type.

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

1. Land the canonical `WEAPON-FIRING-001` scheduler and accepted output contract.
2. Make that scheduler the sole producer of the bound schedule handoff, or replace the provisional handoff with the canonical type.
3. Add reusable runtime projection for the Task 9 gaps through the merged projectile, guidance, impact, and effect authorities.
4. Inject `IEffectiveWeaponRuntimeAdapter` beside the accepted modular scheduler.
5. Pass its returned `WeaponEffectBatch` to the existing sink.
6. Retire the old catalog-derived `WeaponRuntimeFiringProfile` resolver only after every required modular feature has runtime support.

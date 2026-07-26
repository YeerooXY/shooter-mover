# WEAPON-DATA-002A — Canonical authored weapon contracts

## Status and scope

`WeaponBlueprint` is the canonical immutable authored weapon definition introduced by
WEAPON-DATA-002A. This implementation also repairs the existing execution and effective-weapon
compatibility seams required to represent canonical Ricochet, Rocket damage, Rocket Pierce,
non-projectile Range/Pierce modifiers, named strongbox restrictions, and the final effective-value
handoff into projectile execution honestly.

The implementation does **not** migrate the full catalogue, change the production strongbox
resolver, implement the strongbox raffle/opening flow, modify shops or opening animation, add
scenes or prefabs, recreate Stage 1, or add a new projectile/effect/random authority.

Canonical authored content remains grouped as:

```text
WeaponBlueprint
├── Identity
├── FireSettings
├── ShotPattern
├── BaseStats
├── Delivery
├── Presentation
└── DropMetadata
```

The current flat catalogue and `WeaponCatalogBlueprintMapper` remain the explicit transitional
migration boundary.

## Reused authorities

The implementation reuses the existing:

- `WeaponDefinitionId`, `StableId`, actor identity, and lifecycle-generation contracts;
- `WeaponFireSettings`, `WeaponShotPattern`, and `WeaponFiringScheduler`;
- `PierceValue` and `RicochetValue` fixed-point values;
- generic projectile lifecycle, guidance, impact, and effect-emission contracts;
- `DeterministicRandom` and its named-substream derivation;
- deterministic target snapshots and target ordering;
- `WeaponExplosionResolver`, DoT, chain, and `WeaponEffectBatch` boundaries;
- `EffectiveWeapon`, equipment, augment, and generated-signature authorities.

No weapon-specific Rocket, Orb, Laser, or homing runtime class hierarchy was added.

## EffectiveWeapon to projectile execution handoff

### Production composition route

The production post-scheduler composition route is now:

```text
WeaponBlueprint
+ EquipmentInstance
+ installed AugmentInstances
+ permitted modifiers
    ↓ EffectiveWeaponFactory / EffectiveWeaponStatEvaluator
EffectiveWeapon
    ↓ AcceptedEmissionRuntimeAdapter.Adapt(...)
    ↓ canonical branch: ProjectileExecutionProfile.From(EffectiveWeapon)
ProjectileExecutionProfile
    ↓ one ordered ProjectileLaunchRequest per accepted projectile ordinal
ProjectileLaunchRequest
    ↓ ProjectileLifecycleState.Launch(...)
ProjectileLifecycleState
    ↓ retained as CanonicalProjectileLaunchEffect in WeaponEffectBatch
existing inventory pending-delivery / sink boundary
```

The live call into this route remains
`InventoryBackedWeaponExecutionAdapter.ProjectAcceptedSchedule(...)` calling
`emissionAdapter.Adapt(effectiveWeapon, emission)`. The canonical branch inside
`AcceptedEmissionRuntimeAdapter` now calls `ProjectileExecutionProfile.From(effectiveWeapon)` and
constructs the immutable launch request and initial lifecycle state immediately. It does not call the
legacy behaviour registry for canonical travelling definitions.

`CanonicalProjectileLaunchEffect` is not another projectile runtime. It is the immutable payload
carried through the already-existing `WeaponEffectBatch` and inventory pending-delivery envelope. It
contains the canonical `ProjectileLaunchRequest` and its matching initial
`ProjectileLifecycleState`, so a downstream adapter cannot replace final values with catalogue or
blueprint values.

The retained `WeaponRuntimeFiringProfile` inside the inventory envelope is compatibility metadata
only for the existing delivery contract. Canonical execution authority is the embedded
`ProjectileExecutionProfile` and lifecycle state, including fixed-point Pierce/Ricochet values that
the integer compatibility profile cannot represent losslessly.

### Values baked into the profile

`ProjectileExecutionProfile.From(EffectiveWeapon)` copies the final immutable execution values:

- stable weapon-definition identity;
- equipment-instance identity through the existing effect identity;
- canonical execution mode and canonical delivery type;
- final travelling `WeaponProjectileSpec`;
- final limited maximum attack distance;
- final fixed-point `PierceValue`;
- final fixed-point `RicochetValue` and matching impact specification;
- final guidance specification;
- final impact and explosion-trigger configuration;
- final direct/universal damage, DoT data, knockback, and damage category;
- final reusable effects, including explosion, DoT policy, and chain policy;
- final movement penalty;
- the Normal, Orb, or Rocket delivery identity needed by impact and effect execution.

Projectile launch then initialises:

```text
state.Speed             = profile.Projectile.Speed
state.RemainingRange    = profile.Projectile.Range - state.DistanceTravelled
state.Pierce             = ProjectilePierceState.Create(profile.Projectile.Pierce)
state.Guidance           = final profile.Guidance
impact decisions         = final profile.Impact
emitted damage/effects   = final profile.Damage / profile.Effects
```

A Range modifier therefore changes live expiry distance, a Projectile Speed modifier changes live
movement speed, Normal and Orb use final Pierce for enemy-hit continuation, and Rocket retains final
Pierce for explosion-victim capacity while still terminating on first blocking contact.

### Canonical versus transitional construction

`ProjectileExecutionProfile.From(WeaponBlueprint)` is transitional-only. It rejects every canonical
authored definition with `projectile-profile-canonical-blueprint-rejected` and accepts only an
explicit `IsTransitionalCatalogProjection` blueprint.

The retained behaviour-registry and flat scalar effect-batch projection are therefore used only for
transitional catalogue content. Canonical travelling definitions fail closed if they cannot produce
an effective profile, finite range, matching delivery/projectile kind, matching final Pierce,
matching canonical fixed-point Ricochet, or a valid launch identity.

After `EffectiveWeapon` has been resolved, canonical projectile movement, impact, emission, and
explosion resolution do not reconstruct combat values from `WeaponBlueprint`.

## Canonical fixed-point Ricochet execution

### Authored meaning

`RicochetValue` stores tenths:

```text
0  -> no bounce
2  -> one 20% chance for one bounce
10 -> one guaranteed bounce
12 -> one guaranteed bounce, then one 20% chance for one final bounce
20 -> two guaranteed bounces
```

The integer part is guaranteed. The fractional part is one chance for exactly one additional
bounce. It is not a per-collision chance.

### Runtime state

`WeaponRicochetRuntimeState` remains immutable and caller-owned. Canonical projectiles carry the
remaining fixed-point budget as an exact `RicochetValue`; runtime never repeatedly subtracts a
floating-point value.

The state therefore preserves:

- remaining guaranteed bounces;
- whether a fractional final-bounce chance remains;
- exhaustion after that chance is consumed;
- successful bounce count for diagnostics/compatibility;
- duplicate wall-contact suppression for the current simulation step.

Canonical state is initialised lazily from the exact `WeaponRicochetSpec.FixedPointBudget` on the
first eligible wall contact. A state that already entered the legacy maximum/chance path cannot be
reinterpreted as canonical state, and canonical state cannot enter the legacy path.

### Collision rules

For one eligible, non-duplicate wall contact:

```text
remaining guaranteed bounces > 0
    -> bounce
    -> subtract exactly 10 tenths
    -> no chance roll

no guaranteed bounce remains and a fraction is pending
    -> obtain exactly one deterministic chance result
    -> success: bounce once and set remaining budget to zero
    -> failure: do not bounce and set remaining budget to zero

remaining budget is zero
    -> do not bounce
```

Examples:

```text
Ricochet 1.2
collision 1 -> guaranteed bounce, remaining 0.2
collision 2 -> one 20% decision, remaining 0
collision 3 -> impossible

Ricochet 0.2
collision 1 -> one 20% decision, remaining 0
collision 2 -> impossible

Ricochet 2.0
collision 1 -> guaranteed, remaining 1.0
collision 2 -> guaranteed, remaining 0
collision 3 -> impossible
```

Guaranteed bounces do not advance a random stream merely to confirm a guaranteed result.

### Deterministic decision identity

The one fractional decision uses the existing `DeterministicRandom` authority and an isolated
named substream derived from existing execution identity, including:

- actor identity and lifecycle generation;
- participant identity;
- equipment-instance identity;
- fire-operation identity;
- shot sequence;
- projectile ordinal;
- impact ordinal;
- stable wall-contact identity and simulation step;
- the stable Ricochet final-bounce decision purpose.

Random reflection angle remains a separate existing roll after a successful bounce. Duplicate
wall-contact suppression, retained speed, post-bounce homing pause, explosion-trigger reasons, and
immutable caller-owned state remain intact.

### Legacy Ricochet

The prior independent contract remains explicit and transitional:

```text
maximum successful bounces
+ per-eligible-collision bounce chance
```

Its chance is still evaluated per eligible collision. It is never reinterpreted as the canonical
integer-plus-one-fraction model.

## Canonical Rocket explosion damage

Canonical definitions author one universal damage value. They do not author separate direct-contact
and area-damage magnitudes.

For canonical Rocket delivery, the final effective universal damage is the explosion's base damage.
The executable handoff is:

```text
ProjectileEffectEmission.Profile.Damage.DirectDamage
    -> WeaponExplosionResolutionRequest.ResolvedExplosionDamage

ProjectileEffectEmission.Profile.Pierce
    -> WeaponExplosionResolutionRequest.TargetPierce

ProjectileEffectEmission.Profile.Effects.Explosion
    -> explosion radius and falloff
```

`ProjectileExplosionResolutionAdapter.Resolve(...)` is the downstream application call site. For a
canonical Rocket emission it calls:

```text
WeaponExplosionResolutionRequest.ForCanonicalRocket(
    emission.Profile,
    source,
    emission.Position,
    emission.Lifecycle.Random,
    ...)
```

The builder reads only final values already retained by the profile. It does not query inventory,
equipment, augments, skills, character state, the weapon catalogue, or a runtime weapon registry.
The additive `ForCanonicalRocket(EffectiveWeapon, ...)` overload delegates through
`ProjectileExecutionProfile.From(effectiveWeapon)` and then through the same profile-based builder;
it is not a second semantic path.

The projection fails closed unless the profile is a canonical Rocket with:

- a matching Rocket delivery and projectile kind;
- a reusable explosion effect;
- positive final effective universal damage;
- positive final fixed-point Pierce;
- enemy-contact and wall-contact explosion triggers;
- no independently authored `AreaDamage` payload;
- first-blocking-impact projectile termination;
- the existing deterministic-random authority.

### Atomic contact suppression and explosion resolution

The projectile effect emitter checks `profile.IsCanonicalRocket`; it no longer inspects the authored
blueprint. On canonical Rocket enemy contact it requires an enemy-impact explosion reason, suppresses
the separate direct enemy-impact emission, and requires the resulting emission list to contain one
canonical Rocket explosion emission.

The semantic route is therefore atomic:

```text
canonical Rocket enemy contact
    -> no direct-contact damage emission
    -> one explosion emission retaining the exact profile and random stream
    -> one profile-based canonical Rocket explosion request
    -> WeaponExplosionResolver applies final universal damage
```

If any part of that route is invalid, profile construction, effect emission, or request construction
fails closed with a stable diagnostic. Direct damage is not silently suppressed while leaving an
unexecutable authored-value fallback.

Transitional explosion content retains the old constructor and continues to use authored positive
`AreaDamage` with unlimited targets. Normal, Orb, Laser, and Special deliveries do not silently
receive the Rocket damage projection.

## Rocket Pierce as explosion-target capacity

Canonical Rocket Pierce limits explosion victims; it never allows the travelling Rocket body to
continue through enemies. Rocket projectile projection retains `StopOnFirstBlockingImpact`.

The explosion request receives the final resolved `PierceValue` explicitly. The resolver does not
query the catalogue, inventory, equipment instance, augments, skills, or blueprint.

Candidates retain the existing deterministic ordering:

1. distance from explosion centre;
2. stable target actor identity;
3. lifecycle generation;
4. the existing final stable target tie-breaker.

After sorting, capacity is resolved once:

```text
capacity = guaranteed hits
         + one additional hit when the single fractional decision succeeds

damaged targets = ordered candidates.Take(capacity)
```

Examples:

```text
Pierce 1.0 -> first ordered eligible target
Pierce 1.2 -> first target, then one 20% decision for one additional target
Pierce 2.0 -> first two targets
Pierce 2.7 -> first two targets, then one 70% decision for one additional target
```

There is at most one fractional Pierce decision per explosion, not one decision per candidate. Its
isolated existing-random substream is derived from actor/lifecycle, participant, equipment,
fire-operation, shot sequence, projectile ordinal, impact ordinal, and a stable Rocket-Pierce
purpose identity.

The unlimited policy remains the explicit transitional behaviour for existing area-damage
explosions. Rocket victim budgeting is not imposed automatically on future explosive Orb or Special
behaviour.

## Normal, Orb, and Rocket execution boundaries

### Normal

Canonical Normal projectiles retain direct enemy-impact emission, use final speed and range, consume
final fixed-point Pierce through `ProjectilePierceState`, and use the final fixed-point Ricochet
impact specification when present.

### Orb

Canonical Orb delivery remains a travelling projectile. Its final guidance, speed, range, Pierce,
impact, damage, and effects are baked into the same profile. Orb does not become a Rocket merely
because an explosion effect exists: it retains normal enemy-impact payload behaviour and does not
receive automatic Rocket wall-contact detonation or Rocket victim budgeting.

A future canonical non-Rocket explosion damage policy still needs an explicit semantic contract;
`ProjectileExplosionResolutionAdapter` therefore rejects a canonical non-Rocket emission rather
than silently applying transitional area-damage or Rocket rules.

### Rocket

Canonical Rocket projectiles terminate on first blocking contact. Their direct enemy-contact damage
is suppressed only by the validated explosion route, and final Pierce remains available exclusively
as deterministic explosion-victim capacity.

## Effective semantic Range, Pierce, Ricochet, and movement penalty

`EffectiveWeapon` exposes final immutable semantic values independently of projectile structure:

- `EffectiveMaximumAttackDistance`;
- `EffectivePierce`;
- `EffectiveRicochet`;
- `EffectiveMovementPenaltyPercent`.

The effective evaluator reconstructs those values after applying compatible modifiers. Travelling
projectile projection consumes the same resolved Range/Pierce values, while Laser and approved
non-projectile Special delivery retain them without inventing `WeaponProjectileSpec`.

Modifier compatibility is separated by meaning:

| Stat | Canonical support |
| --- | --- |
| Projectile speed | Normal, Orb, Rocket only |
| Maximum range | Normal, Orb, Rocket, Laser, and Special schemas explicitly opting into canonical range |
| Pierce | Normal enemy-hit budget, Orb enemy-hit budget, Rocket explosion-victim budget, Laser ordered-ray budget, and Special schemas explicitly opting in |

Therefore a canonical Laser:

- rejects Projectile Speed modifiers;
- accepts compatible Range modifiers;
- accepts compatible Pierce modifiers;
- preserves final Range and Pierce despite having no travelling projectile.

Special delivery defaults to no reusable Range/Pierce modifier compatibility. An approved schema
must opt in explicitly; those flags expose data compatibility only and do not implement a Unity
Special execution route.

Structural modifiers still fail closed when they would create absent structure. Examples include:

- Projectile Speed on Laser;
- Explosion Radius without an explosion effect;
- deterministic Spread on a weapon without an existing Spread structure;
- homing modifiers on an unguided definition;
- legacy maximum-Ricochet modification on canonical fixed-point Ricochet.

## Stable identities for named strongbox restrictions

Numeric minimum-tier eligibility remains a progression rule:

```text
minimum tier = 11
-> eligible at numeric tier 11 and every later progression tier
```

Explicit named restrictions use stable tier identities:

```text
AllowedTierIds = [ strongbox-tier.antimatter ]
-> Antimatter only

AllowedTierIds = [ strongbox-tier.secret ]
-> secret box only
```

`WeaponStrongboxEligibility.FromAllowedTierIds(...)` requires explicit identities that are:

- non-null;
- non-empty;
- individually non-null;
- unique;
- sorted deterministically by ordinal `StableId` ordering.

An explicit list cannot be combined with a numeric minimum tier. Its meaning does not depend on the
current number or ordering of production tiers.

`WeaponCatalogAuthoredMappingDetails` carries `AllowedStrongboxTierIds`. Transitional mapper aliases
exist only to keep the in-PR mapping implementation source-compatible; their values are still
`StableId`, not numeric tiers.

The old flat `TopBoxOnly` field remains transitional until STRONGBOX-DATA-002. `MapAuthored(...)`
requires an explicit replacement eligibility rule and never infers "top" from the current maximum
tier. The current production strongbox resolver is unchanged by this PR.

## Representative development-only samples

The four samples remain unregistered and do not affect production content:

1. **Pulse Shotgun** — semi-automatic, eight simultaneous Normal deliveries, no burst, Energy damage, and no owned augment capacity in the canonical definition.
2. **Seeking Chemical DoT Orb Launcher** — Orb delivery, gradual guidance, Chemical direct damage plus DoT, no wall-contact explosion semantics, with installed augments only on its owned sample instance.
3. **Contact Rocket Launcher** — enemy/wall contact detonation, final canonical damage projected as explosion base damage, Pierce used as explosion-target capacity, first-blocking-impact projectile termination, and no Ricochet.
4. **Automatic Energy Laser** — automatic rate, width, no projectile speed, finite Range and Pierce, with semantic Range/Pierce modifier compatibility and Projectile Speed rejection.

## Burst cadence compatibility

Canonical `RateOfFire` remains firing cycles per second. Burst authored count/timing is projected
into the existing scheduler without introducing a second cadence authority. The exact projection and
fail-closed rules remain documented in
`WEAPON_DATA_002A_BURST_CADENCE_COMPATIBILITY.md`.

## Remaining transitional and Unity boundaries

This PR now carries canonical Normal, Orb, and Rocket launch data through the production inventory
pending-delivery envelope, but it does not claim implementation of:

- the Unity adapter that consumes `CanonicalProjectileLaunchEffect.InitialState` and instantiates or updates a Unity projectile presentation;
- Unity target-snapshot, collision-contact, line-of-sight, and damage-application adapters needed to exercise the pure projectile and explosion decisions in-game;
- Unity Laser delivery execution;
- Unity Special delivery execution;
- a canonical non-Rocket explosion damage policy for Orb or future Special delivery;
- the full weapon catalogue migration or JSON schema replacement;
- production strongbox selection using the new stable named-tier restrictions;
- strongbox raffle/opening, shop, inventory/results UI, or opening animation changes;
- scenes, prefabs, Stage 1 content, simulator execution, or account persistence;
- in-game exercise of the repaired behaviours.

The current transitional flat catalogue continues through the retained behaviour registry and legacy
scalar effect descriptions. That route retains unlimited-target area-damage semantics. It cannot
silently accept a canonical authored blueprint.

## Validation policy

No automated tests are added under the prototype policy. Validation for this PR is repository-level
static inspection of:

- the production scheduler-emission call into `AcceptedEmissionRuntimeAdapter`;
- the exact `EffectiveWeapon -> ProjectileExecutionProfile -> ProjectileLaunchRequest -> ProjectileLifecycleState` projection;
- final speed, range, Pierce, guidance, impact, damage, effects, delivery identity, and movement penalty retention;
- exact fixed-point Ricochet state transitions and explicit legacy branching;
- deterministic decision-purpose and execution identities;
- immutable state replacement;
- profile-retaining projectile effect emission;
- canonical Rocket direct-contact suppression and profile-based explosion-request construction;
- deterministic target ordering and one-roll Rocket Pierce capacity;
- semantic modifier compatibility and effective reconstruction;
- stable named-tier identity validation and mapper boundaries;
- representative sample construction;
- changed-file scope excluding scenes, prefabs, shops, strongbox runtime, and tests.

Unity compilation and in-game validation must be reported only when genuinely performed; they are
not implied by this document.

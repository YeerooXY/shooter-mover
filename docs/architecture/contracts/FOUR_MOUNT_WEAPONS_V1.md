# Four-Mount Weapons and HUD Contracts v1

## Status and scope

This document defines the engine-independent contract boundary for four concurrently
operating player weapon mounts. It consumes Player Intents v1 and Combat Messages
v1, but it does not implement weapon simulation, balance, targeting, projectiles,
effects, audio, or UI prefabs.

The contract files are:

- `Assets/ShooterMover/Runtime/Contracts/Combat/WeaponMountContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Presentation/WeaponHudState.cs`

All contract values are immutable and live in the Unity-free
`ShooterMover.Contracts` assembly.

## Fixed slot model and HUD order

V1 has exactly four stable slots:

| HUD index | Stable slot |
|---:|---|
| 0 | `MountOne` |
| 1 | `MountTwo` |
| 2 | `MountThree` |
| 3 | `MountFour` |

`FourMountWeaponState` requires all four slots exactly once. Constructor input may
arrive in any order; lookup and HUD projection always use the canonical order above.
A slot remains present when unequipped. Weapon identities may repeat across slots
because identical base copies are valid, but slot identities may not repeat.

Slot order is the deterministic V1 presentation priority. Content, runtime behavior,
or a UI prefab must not reorder the four rows by weapon identity, readiness, power,
damage, or arrival order.

## Shared player intent

`WeaponArrayIntent.FromPlayerIntent` projects exactly three fields from one
`PlayerIntentFrame`:

- shared aim;
- shared fire action;
- shared power modifier.

There is no per-slot fire selector in this contract. When fire is requested, every
ready mount is addressed by the same attempt. Each mount still resolves readiness,
cadence, heat or charge, power, recoil, and faults independently.

A held or newly pressed fire action is an active fire request. A held or newly
pressed power modifier is an active empowerment request. Adapters remain responsible
for producing Player Intents v1; this contract contains no keyboard, mouse, gamepad,
touch, or Unity Input System identifiers.

## Per-mount snapshot

`WeaponMountState` contains one stable slot and, when equipped, one `StableId`
weapon identity plus the following independent state:

- `WeaponMountReadiness` — unequipped, ready, cadence-blocked, recovering,
  overheated, charging, or faulted;
- `WeaponCadenceState` — finite non-negative time until the next shot and remaining
  authored burst shots;
- `WeaponCycleResourceState` — either heat, charge, or no cycle resource, with
  finite bounded current and maximum values;
- `WeaponRecoilState` — finite non-negative current recoil impulse and movement
  influence;
- `WeaponPowerBankState` — optional independent capacity, available units, and
  authored empowered cost.

An unequipped slot has no weapon identity and must use neutral cadence, cycle,
recoil, and power state. A ready slot cannot have cadence time remaining or maximum
heat. An overheated slot requires maximum heat. A charging slot requires a charge
resource below maximum.

V1 represents heat or charge, never both in one mount snapshot. Weapon-specific
simulation decides how these values change; the contract only validates and carries
the authoritative result supplied by that simulation.

## Unlimited normal fire

Normal fire has no ammunition, magazine, reload, or finite normal-fire resource in
this contract. `WeaponMountContractRules.NormalFireConsumesConsumable` is `false`.
Cadence, heat, charge, recovery, and faults may make a mount temporarily not ready,
but ordinary shots do not deplete an ammunition stock.

The optional power bank is exclusively an empowered-fire resource. A mount without
a configured bank can still fire normally.

## Per-mount fire-result rules

One `FourMountFireResult` contains exactly one result for each stable slot. Results
are canonicalized by slot and checked against the shared intent and the corresponding
mount snapshot.

| Mount state during an active shared fire request | Power modifier inactive | Power modifier active |
|---|---|---|
| Ready, bank can pay authored cost | `NormalFired` | `EmpoweredFired` |
| Ready, bank absent or cannot pay cost | `NormalFired` | `NormalFallbackPowerUnavailable` |
| Equipped but not ready | `NotReady` | `NotReady` |
| Unequipped | `Unequipped` | `Unequipped` |
| Faulted | `Faulted` | `Faulted` |

This is the required mixed-fallback invariant: power shortage changes only that
mount's result. Other ready mounts with sufficient independent banks remain
empowered, and a fault on one mount does not suppress fire from the other eligible
mounts.

A fired result carries a stable combat-event identity and one known non-`System`
`CombatChannel` from Combat Messages v1. A non-fired result carries neither. The
contract does not create a projectile, apply damage, deduct power, or emit a hit;
those are later behavior and application responsibilities.

## HUD read model

`WeaponHudState` projects four immutable `WeaponHudSlotState` rows in canonical
slot order. Each row exposes the mount's:

- equipped state and weapon identity;
- readiness and cadence;
- heat or charge state;
- recoil and movement influence;
- independent power-bank state;
- optional latest per-mount fire result.

Presentation may render, animate, localize, or suppress cosmetic details, but it
must not mutate this state, decide readiness, spend power, or reinterpret fallback.
Critical information must remain readable without relying on color alone.

## Representative Stage 1 mappings

The contract is deliberately behavior-agnostic. The five amended Stage 1 weapon
packages fit without package switches in the shared contract:

| Package | Representative contract use |
|---|---|
| Blaster Machine Gun | ready/cadence snapshots, unlimited normal fire, optional empowered numeric profile |
| Shotgun | independent cadence or burst count; spread and pellet behavior remain package-owned |
| Rocket Launcher | paced cadence and recoil; projectile and bounded detonation remain package-owned |
| Arc Gun | readiness/power result plus Combat Messages v1 channel; chain targeting remains package-owned |
| Ricochet Gun | readiness/power result; bounce policy and projectile lifetime remain package-owned |

Any future weapon may use the same snapshots and result kinds. A genuinely new
shared semantic requires an explicit versioned contract extension rather than a
weapon-ID branch or mutable universal DTO.

## Validation and rejection rules

Construction fails deterministically for:

- a mount or result count other than four;
- duplicate, missing, null, or unknown slots;
- unknown readiness, resource, result, or combat-channel enum values;
- non-finite or negative cadence, resource, recoil, or power values;
- current resource or available power above its declared maximum/capacity;
- non-neutral state on an unequipped slot;
- contradictory readiness and cadence/heat/charge state;
- a fired result without an event identity and non-system combat channel;
- a non-fired result that publishes a combat event;
- a result whose slot or weapon identity disagrees with its mount;
- empowered or fallback results inconsistent with that mount's independent bank;
- a four-mount fire result without an active shared fire request.

## Ownership and non-goals

This contract does not define:

- weapon behavior modules or execution order;
- numeric balance or content-package tuning;
- projectile, hit, damage, target-selection, recoil application, or power deduction;
- loadout inventory, rewards, persistence, mission state, or randomized modifiers;
- HUD prefabs, layout assets, effects, audio, animation, or scene references;
- mutable universal weapon DTOs.

Consumers must preserve the four stable slots and extend behavior behind isolated,
tested modules rather than modifying the contract for each weapon package.

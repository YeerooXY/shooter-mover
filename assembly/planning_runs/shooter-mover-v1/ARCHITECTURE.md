# Shooter Mover Architecture

## 1. Architectural stance

Use one Unity product repository with a strict hybrid architecture:

```text
device input / Unity scene events
        ↓
Unity adapters and presentation
        ↓
application use cases and ports
        ↓
engine-independent domain core
        ↓
validated commands, events, snapshots, and journals
```

Unity owns engine integration and presentation. Plain C# owns durable rules and authoritative state wherever practical. The same fact must never be authoritative in both a scene component and the domain model.

The internal MVP has no remote backend. Services are local in-process ports for persistence, content lookup, diagnostics, artifact identity, time, randomness, and file access.

## 2. Unity project layout

The Unity project lives at repository root:

```text
Assets/
  ShooterMover/
    Runtime/
      Domain/{Common,Movement,Combat,Enemies,Mission,Rewards,Progression,Persistence}/
      Contracts/
      Application/
      UnityAdapters/{Input,Physics,Combat,Enemies,Scenes,Rendering,Audio,Platform}/
      Bootstrap/
      Presentation/
    Content/{Definitions,SharedModules}/
    ContentPackages/{Weapons,Enemies,Rooms,Encounters,Environment}/
    Generated/
    Scenes/{Bootstrap,MenuHub,Prototypes,Factory,Tests}/
    UI/
    Localization/
    Tests/{EditMode,PlayMode,Performance}/
    TestSupport/
Packages/
ProjectSettings/
tools/
docs/
source-assets/
assembly/
```

Assembly references point inward:

```text
Domain
  ↑
Contracts / Application
  ↑
UnityAdapters / Content / Presentation
  ↑
Bootstrap / Scenes / Tests
```

The domain core must not reference `UnityEngine`. Content definitions may use ScriptableObjects, but mutable runtime state never lives in shared content assets.

## 3. Authority and state flow

`MissionRunState` is the sole durable truth for explored and cleared rooms, objectives, routes, checkpoints, fast travel, provisional and banked rewards, shop state, run-only refreshes, temporary resources, suspension, and completion.

Unity rooms are projections. A room loads, reads its state slice, presents it, and submits commands. It does not decide that an enemy is permanently defeated, a reward is banked, or a route is durably open.

Command path:

1. an adapter converts input or a scene interaction into an application request;
2. the application layer creates a typed `MissionCommand`;
3. domain validation checks identity, sequence, state, and invariants;
4. accepted commands emit `MissionEvent` values;
5. events mutate domain state, update projections, feed diagnostics, and enter the journal only when durability requires it;
6. risky transitions are acknowledged only after the required durable write succeeds.

Journal checkpoint activation, banking, unique rewards, route/objective changes, completion, and suspend/resume boundaries. Do not journal shots, ordinary movement, damage ticks, every enemy action, or presentation state.

## 4. Shared contracts before parallel implementation

The Contract Steward owns the first accepted versions of:

- `StableId` syntax and namespacing;
- content identity and version fingerprints;
- damage, hit, health, shield, contact, and status-effect messages;
- movement, aim, fire, power, thruster, and interaction intents;
- weapon readiness and four-mount HUD state;
- mission commands, events, rejection reasons, and sequence semantics;
- room lifecycle and projection interfaces;
- encounter start, reinforcement, completion, and withdrawal interfaces;
- profile, loadout, inventory, reward, shop, checkpoint, and difficulty DTOs;
- save snapshot, journal, migration, tombstone, and recovery envelopes;
- diagnostics event and run-validity formats;
- build/content identity manifest;
- generated registry and deterministic review snapshot formats.

A consuming lane may propose a versioned extension but may not fork an unofficial duplicate contract.

## 5. Movement and input

The domain movement model represents responsive base acceleration and braking, bounded inertia, a small independently regenerating thruster bank, movement direction independent from aim, immediate velocity replacement at activation, bounded steering, short startup forgiveness, deterministic wall reflection, controlled exit momentum, light-enemy shove, heavy blocking, and discrete per-enemy contact grace.

Exact charge count, recharge time, distance, curves, forgiveness, and ricochet influence are versioned tuning data. Formal test rounds freeze a tuning profile and build identity.

Unity Input System actions are device-independent from the first playable: move, aim, fire, power modifier, thruster, interact, map, pause/menu, and UI navigation. Keyboard and mouse are tuned first. Gamepad and later touch consume the same intents rather than adding device checks to gameplay code.

## 6. Four-weapon combat

One shared aim point drives four independently simulated mounts. Each mount owns cadence/burst state, heat or charge, recovery, optional recoil/presentation state, projectile or hit behavior, power bank, empowered cost, and presentation priority. Weapon fire must never alter player position, velocity, thruster state, wall/contact resolution, or any other player-movement authority.

Normal fire has no consumable ammunition. Holding the power modifier attempts to empower every ready mount. An empty bank falls back to normal fire while other mounts remain empowered.

Use compact reusable modules for automatic, projectile, spread/burst, beam/heat, charge/pierce, homing, chain, and lobbed-area behavior. New mechanics may add isolated tested modules; ordinary content must not modify a universal god object.

## 7. Enemies and encounters

Enemy definitions compose reusable movement, targeting, attack, telegraph, weight, contact, and reward modules.

Baseline pressure is readable and aimed. Bounded barrages are authored peaks. Specialized attacks may lead current velocity, but a visible lock preserves last-second boost counterplay. Ordinary direct attacks respect cover; explicit visual language marks arcing, piercing, ricocheting, destructible, or area attacks.

Encounters define initial formation, deterministic reinforcement triggers and entry points, retreat or lockdown rules, pursuit boundaries, difficulty overrides, completion/reward events, and enemy/projectile/light/particle/audio budgets.

Enemy AI and exact combat state live in the loaded encounter. Durable cleared/completion truth lives in `MissionRunState`.

## 8. Rooms and level graph

Meaningful rooms are independently testable additive scenes or prefabs with a `RoomDefinition` and package manifest. Small connectors may remain prefabs.

A room contract declares stable ID and zone, connection sockets, encounter/objective IDs, projection state keys, collision/navigation/sorting/lighting/audio/camera constraints, services, performance budget, owned serialized assets, and validation preview.

The authored `LevelGraph` stores stable nodes and edges. Cross-room communication uses IDs and events, never direct scene-object references.

## 9. Persistence and rewards

Profiles and active missions use validated versioned snapshots, atomic replacement, rolling backups, compact idempotent journal, ordered migrations, stable-ID tombstones/compensation, manual import/export, newer-schema refusal, and cloned saves for destructive diagnostics.

A collected strongbox creates an immutable `RewardCommitment`. Opening atomically consumes the box and persists the exact reward before or with presentation. The same reward ID can grant once.

Shop inventory rolls from a run seed, stays stable for that run, and changes only through an allowed mission-bound refresh token. Revisits, death, reload, and teleport travel never refresh stock or tokens.

## 10. Presentation

Presentation consumes read models and use cases; it never mutates durable state directly.

Information priority:

1. lethal enemy telegraphs and hazards;
2. player damage, health, shield, and movement state;
3. weapon readiness and power depletion;
4. objective, banking, checkpoint, and at-risk reward state;
5. damage numbers and cosmetic effects.

Dynamic lighting reinforces but never carries essential readability. Reduced-effects profiles preserve timing and information while lowering flashes, particles, shadows, debris, shake, and post-processing.

All player-facing text uses localization keys and layouts tolerate longer strings and scalable text.

## 11. Diagnostics and evidence

Structured local events include build/content/save identity, run start/end/restart, loadout/settings, room entry/completion, deaths, checkpoints, travel, banking, shop, refresh, rewards, completion, exceptions, missing assets, save recovery, migrations, performance warnings, diagnostic commands, and evidence validity.

Logs rotate locally. Export is explicit and redacted.

## 12. Architecture rejection rules

Reject a proposal that:

- makes a scene or ScriptableObject the durable state owner;
- introduces a second authoritative mission, inventory, or reward state;
- requires scene searches or implicit execution order;
- adds a remote service to solve an offline MVP problem;
- adds Android, co-op, storefront, analytics, accounts, or cloud packages early;
- creates a universal schema that every novel mechanic must bend into;
- lets one content package edit unrelated foundations;
- hand-edits generated registries;
- hides critical rules in presentation code;
- bypasses atomic persistence;
- sacrifices evidence, accessibility, diagnostics, reliability, or performance to preserve breadth.

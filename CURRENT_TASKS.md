# CURRENT TASKS — Shooter Mover extensibility roadmap

This is the canonical dispatch board for the next architecture and integration tasks.

## How to use this file

In a fresh work window, use a command such as:

> Check `CURRENT_TASKS.md` in `YeerooXY/shooter-mover` and do `ROOM-ACCESS-001`.

The agent must then:

1. Read this entire file before changing code.
2. Verify the requested task is not already implemented by a merged PR.
3. Verify every listed dependency is merged into `main`.
4. Fetch the latest `main` and record its exact launch SHA.
5. Work only on the requested task.
6. Use a fresh `agent/<task-id>-<description>` branch from that exact SHA.
7. Preserve existing authority boundaries and deterministic replay behavior.
8. Open one non-empty **draft PR** targeting `main`.
9. Do not merge or enable auto-merge.
10. Include changed-file audit, Unity test commands/results, and known limitations in the PR.

Do not treat the status table below as more authoritative than GitHub. The repository may have advanced since this file was edited.

## Global architecture rules

These rules apply to every task below.

- Definitions describe content. Authorities own mutable truth. Policies make decisions. Unity adapters project facts and presentation.
- Do not add scene-, room-, enemy-, weapon-, skill-, event-, class-, or prop-specific branches to shared controllers.
- Adding content that uses existing mechanics should normally add definitions/assets/tests and modify zero existing gameplay classes.
- Use stable IDs and immutable snapshots/facts at subsystem boundaries.
- Authoritative randomness must be deterministic and derived from explicit immutable context and seeds.
- Repeated operation IDs must be idempotent. Conflicting reuse must reject without mutation.
- Do not create duplicate XP, inventory, money, scrap, skills, loadout, reward, strongbox, player-health, enemy-health, room, or run authorities.
- Do not put transient health, cooldowns, projectiles, active effects, or room position into the permanent character snapshot.
- Do not persist derived statistics as primary truth. Persist their inputs and rebuild them.
- Prefer open stable target/behavior/capability IDs over ever-growing enums and switch statements.
- Keep Unity collision callbacks and UI presentation from directly mutating authoritative state.
- Unless a task explicitly says otherwise, do not edit `Stage1VisibleSliceController.cs`.
- Every new Unity source/asset must include valid `.meta` files with unique GUIDs.

## Merged foundations

At creation of this board, the following foundations are merged:

- `ROOM-DATA-001` / PR #252 — split JSON room authoring foundation.
- `SAVE-CHARACTER-001` / PR #253 — six-character account persistence aggregate.
- `MODIFIER-RUNTIME-001` / PR #254 — generic numerical modifiers and fact-window conditions.

Planning baseline after those merges: `4a702411730cd9681abf6cd1e22764d47f535f46`.

Every task must still branch from the latest eligible `main`, not automatically from this historical SHA.

---

# Dispatch order

## Parallel Wave A — ready after verifying current `main`

These tasks are designed to avoid file overlap when their ownership boundaries are respected:

1. `SAVE-ADAPTERS-001`
2. `DERIVED-STATS-001`
3. `ENEMY-DATA-001`
4. `PROP-RUNTIME-001`
5. `ROOM-ACCESS-001`
6. `EVENT-MODIFIER-001`

`STATUS-EFFECT-RUNTIME-001` may also run in this wave, but only when no other agent is editing the same modifier files. It should own new status-effect subpaths rather than rewriting the merged modifier core.

## Parallel Wave B

After the relevant Wave A dependencies merge:

1. `CHARACTER-COMPOSITION-001`
2. `ENEMY-FACTORY-001`
3. `COMBAT-HIT-POLICY-001`
4. `STATUS-EFFECT-RUNTIME-001` if not already complete

## Parallel Wave C

1. `CRIT-LIVE-001`
2. `CONDITION-LIVE-001`
3. `RUN-SESSION-001`
4. `EXTENSIBILITY-GUARDRAILS-001`

## Completed/merged integration work

- `BOX-PERSIST-001` / PR #276 — durable unopened strongboxes and crash-safe opening. Its full prompt remains below as historical architecture context.

## Integration Wave — coordinate sequentially

These tasks touch central production composition and should not be assigned concurrently against the same files:

1. `STAGE1-FREEZE-001`
2. `ROOM-JSON-LIVE-001`
3. `STAGE1-RUNTIME-DECOMPOSE-A-001`
4. `STAGE1-RUNTIME-DECOMPOSE-B-001`
5. `ABILITY-RUNTIME-001`
6. `LEVEL1-CONTROLLER-RETIRE-001`

---

# Full task prompts

## SAVE-ADAPTERS-001 — Persist existing authority snapshots as account components

**Status:** READY after verifying PR #253 is merged.

**Branch:** `agent/save-adapters-001-authority-components`

**Dependencies:** `SAVE-CHARACTER-001`; existing XP, holdings, money, scrap, skills, loadout, and strongbox authorities.

### Task

Connect the existing immutable subsystem snapshots to `PlayerAccountSnapshotV1` and `CharacterInstanceSnapshotV1` through typed, versioned save-component adapters.

### Requirements

- Define stable component IDs and schema versions for:
  - player experience;
  - player holdings/inventory;
  - money wallet;
  - scrap wallet;
  - ranked skill allocation;
  - exact-instance loadout;
  - unopened strongboxes and strongbox opening state;
  - character statistics where a canonical snapshot already exists.
- Each adapter must export an existing authority snapshot into one canonical save-component payload.
- Each adapter must validate and import without creating a second authority or mutating the source payload.
- Implement an aggregate restore coordinator that validates **all** required components before replacing any live authority state.
- Corrupt, missing-required, unsupported-version, fingerprint-mismatched, or semantically inconsistent components must reject atomically.
- Preserve replay/idempotency histories required by the underlying authorities.
- Permit unknown optional future component IDs to be retained without being interpreted, where safe.
- Add a filesystem-facing port and an engine-neutral atomic save protocol: write temporary data, validate/read back, replace the active file, retain one last-known-good backup.
- Do not use PlayerPrefs as the new authoritative account store.
- Document canonical serialization, schema migration boundary, backup behavior, and recovery behavior.

### Tests

- Round-trip one character containing every supported component.
- Six slots remain isolated.
- Duplicate equipment definitions remain distinct equipment instances.
- XP and wallet replay protection survives restore.
- Strongbox opening replay protection survives restore.
- Corrupt one component and prove no authority changes.
- Missing optional component is accepted; missing required component rejects.
- Unsupported schema rejects without overwriting the last valid save.
- Temporary-write interruption preserves the previous valid save.

### Non-goals

- No cloud synchronization, encryption, multiplayer backend, achievements implementation, or UI.
- Do not reconstruct the selected character into the Hub; that belongs to `CHARACTER-COMPOSITION-001`.

### PR proof

Run focused EditMode tests for the adapter and atomic-store suites. Include fixture payloads or canonical text fingerprints in the PR description.

---

## DERIVED-STATS-001 — Deterministic character stat composition

**Status:** READY after verifying PR #254 is merged.

**Branch:** `agent/derived-stats-001-character-composition`

**Dependencies:** merged modifier runtime; equipment, augments, skills, progression, and class/loadout foundations.

### Task

Implement the engine-neutral service that deterministically derives character and run-start statistics from class, level, equipped gear, augments, skills, account modifiers, and event modifiers.

### Requirements

- Define stable target IDs for at least:
  - maximum health;
  - movement speed;
  - armor and damage resistance channels;
  - outgoing damage multiplier;
  - critical chance and critical multiplier;
  - healing output and received healing;
  - contact damage and knockback;
  - weapon/ability capacity;
  - reward/drop modifiers.
- Compose sources in a documented deterministic order.
- Reuse `RuntimeModifierSnapshotV1`; do not create a second modifier language.
- Distinguish permanent character inputs from run-only active conditions.
- Produce an immutable `DerivedCharacterStatsSnapshotV1` and immutable `RunCombatProfileV1` with complete input fingerprints.
- Clamp impossible values through explicit policies, not hidden ad hoc conditionals.
- Class identity must be data-defined. Do not introduce healer/aggressive/juggernaut subclasses.
- Derived values must be rebuildable after equipment, skill, level, event, or achievement changes.
- Do not persist the derived output as primary character truth.

### Tests

- Same inputs produce byte-equivalent fingerprints.
- Source ordering does not depend on dictionary iteration.
- Crit chance, health, movement, and reward modifiers combine correctly.
- Class-specific skill caps/curves remain respected by the existing skill authority.
- Invalid negative or over-cap results clamp/reject according to policy.
- One changed equipment instance changes the derived fingerprint.
- Removing a skill or respec rebuilds all derived values with no stale effects.

### Non-goals

- No actual critical-hit roll, damage application, status-effect lifetime, or Unity HUD changes.

---

## ENEMY-DATA-001 — Versioned data-driven enemy catalog

**Status:** READY.

**Branch:** `agent/enemy-data-001-definition-catalog`

**Dependencies:** reusable enemy actor/decision foundations, weapon execution core, XP/drop profile foundations.

### Task

Create a typed, versioned, deterministic enemy-definition catalog suitable for arbitrary room placements and future enemy additions.

### Requirements

- Definitions must support:
  - stable enemy definition ID;
  - display/presentation reference;
  - base health and level-scaling profile;
  - faction;
  - detection radius and vision arc;
  - attack arc and attack range;
  - movement policy ID;
  - decision policy ID;
  - one or more attack capability descriptors;
  - cooldown, damage, damage channel, projectile/area/melee parameters;
  - XP profile;
  - drop profile;
  - room-clear role;
  - optional reusable special capabilities.
- Support ranged, contact/pounce, stationary turret, and pursuing enemy fixtures without enemy-type-specific runtime branches.
- Behavior/capability IDs must resolve through registries and fail closed when unregistered.
- Validate IDs, ranges, arcs, levels, duplicate definitions, unsupported capabilities, missing presentation references, and incompatible attack combinations.
- Provide canonical fingerprints and content/schema versions.
- Prefer JSON or an equivalent typed imported catalog consistent with the weapon and room data foundations.
- Adding another enemy that uses registered mechanics must require only a definition, presentation asset, placement, and focused content test.

### Tests

- Import multiple enemies using shared and different capabilities.
- Duplicate IDs and malformed ranges reject.
- Unknown movement/attack capability rejects.
- Attack arc remains independent from vision arc.
- Data order does not affect catalog fingerprint.
- A new fixture enemy is added without editing any existing production enemy class.

### Non-goals

- Do not instantiate live enemies or edit Stage 1 composition.
- Do not award XP or roll drops inside enemy definitions.

---

## PROP-RUNTIME-001 — Generic prop definitions and runtime capabilities

**Status:** READY.

**Branch:** `agent/prop-runtime-001-data-driven-props`

**Dependencies:** room placement data, equipment/reward facts where needed, shared combat capabilities.

### Task

Implement a reusable prop catalog and engine-neutral prop runtime so adding crates, cover, explosive barrels, terminals, switches, hazards, or decorative objects does not require room-specific controller logic.

### Requirements

- Define stable prop definition and presentation IDs.
- Support composable capabilities for:
  - solid/non-solid collision;
  - indestructible or health-based destructibility;
  - damage channels/resistances;
  - explode-on-destroy;
  - drop-on-destroy;
  - interactable/switch/objective facts;
  - faction-neutral or hostile damage behavior;
  - room-clear participation;
  - decorative-only presentation.
- Separate immutable definition, placement identity, and runtime state.
- Runtime death/destruction emits immutable attributed facts; downstream systems handle rewards/objectives.
- Provide a generic placement-to-runtime factory interface, but do not yet cut over Stage 1 room composition.
- Adding a prop using existing capabilities must be definition + presentation + placement only.

### Tests

- Decorative prop creates no combat authority.
- Destructible cover retains independent health per placement.
- Explosive barrel emits one terminal/explosion fact and cannot duplicate rewards.
- Two placements of one definition remain distinct.
- Unknown capability and invalid combinations reject.
- Friendly-fire policy is not hardcoded inside the prop runtime.

### Non-goals

- No full room JSON live cutover; that belongs to `ROOM-JSON-LIVE-001`.

---

## ROOM-ACCESS-001 — Keys, locks, switches, and composable door conditions

**Status:** READY. This task is the exclusive owner of room importer/access-condition changes while active.

**Branch:** `agent/room-access-001-keys-locks-conditions`

**Dependencies:** merged room graph, room-live authority, and split JSON room authoring.

### Task

Extend the room model with a generic, data-driven access-condition language so additional keys, locked doors, switches, objectives, and compound gates require no new C# branches.

### Requirements

- Define immutable condition descriptors supporting at least:
  - always;
  - room entered;
  - room complete;
  - exact enemy/prop terminal;
  - exact holding/key present;
  - exact holding/key consumed;
  - exact collected drop;
  - objective complete;
  - switch active;
  - difficulty threshold;
  - `all`, `any`, and `not` composition.
- Use stable condition and item IDs.
- Model mission keys as exact run holdings or exact stable stack facts through a narrow port; do not create a general inventory authority.
- Door evaluation must be authority-owned and deterministic.
- Optional key consumption must be exactly-once and conflict-safe.
- Extend room JSON authoring with readable lock/access definitions and precise diagnostics.
- Preserve existing room-entered, room-complete, and collected-drop semantics.
- A new key and locked door using existing condition types must be data-only.

### Tests

- Key-present door remains closed before pickup and opens after accepted pickup.
- Consuming key unlocks once; replay does not consume twice.
- `all`, `any`, and `not` trees evaluate deterministically.
- Different doors in one room have independent conditions.
- Return doors and progression doors retain authored meanings.
- Unknown references and circular/invalid condition graphs reject.
- JSON round-trip and fingerprint remain deterministic.

### Non-goals

- No inventory UI, key artwork, or Stage 1 live room cutover.

---

## EVENT-MODIFIER-001 — Versioned special-event modifier context

**Status:** READY after verifying PR #254 is merged.

**Branch:** `agent/event-modifier-001-active-event-context`

**Dependencies:** runtime modifier foundation; reward/drop generation contexts.

### Task

Implement versioned special-event definitions and deterministic active-event projection so events such as double-drop weekends modify generation context rather than rewriting catalogs.

### Requirements

- Define stable event IDs, schema/content versions, activation windows, priority, compatibility/exclusion rules, and modifier descriptors.
- Active event selection must consume an injected authoritative clock/time-window port; domain code must not call local system time directly.
- Project active events into one immutable `ActiveEventModifierSnapshotV1`.
- Support at least reward strongbox weight, money quantity, XP quantity, and future open target IDs through the merged modifier language.
- Reward/drop/opening commands must be able to record the exact event snapshot fingerprint applied.
- Once an opening or mission result is frozen, later event changes must not alter that result.
- Overlapping events must combine or reject according to explicit policy.
- Provide offline/server-authoritative boundary documentation for future multiplayer.

### Tests

- Event inactive before start, active inside window, inactive after end.
- Same clock/context produces identical fingerprint.
- Double-drop event multiplies only the intended target.
- Frozen reward context remains unchanged after event expiry.
- Conflicting/exclusive overlapping events fail deterministically.
- Unknown modifier targets remain representable and are consumed only by supporting systems.

### Non-goals

- No remote event service, monetization backend, calendar UI, or catalog rewriting.

---

## STATUS-EFFECT-RUNTIME-001 — Generic temporary effects, stacks, and expiry

**Status:** READY after PR #254, but coordinate modifier-path ownership.

**Branch:** `agent/status-effect-runtime-001-temporary-modifiers`

**Dependencies:** runtime modifier and fact-window condition foundations.

### Task

Implement an engine-neutral status-effect authority for temporary buffs, debuffs, stacks, refresh rules, and deterministic expiry.

### Requirements

- Define versioned status-effect definitions with stable IDs.
- Support duration ticks, maximum stacks, add/refresh/replace/ignore stacking policies, source identity, dispel category, and modifier contributions.
- Apply and expire through explicit commands and simulation ticks.
- Exact replay returns the original result; conflicting operation reuse rejects.
- Export immutable active-effect snapshots and modifier projections.
- Lifecycle restart must clear run-local effects without modifying permanent skills/equipment.
- A killing-spree activation must be able to apply a generic temporary status effect rather than custom skill code.
- Do not embed damage-over-time tick execution in the first version unless implemented as a generic reusable capability with deterministic commands.

### Tests

- Apply, refresh, stack, replace, ignore, expire, and dispel policies.
- Exact duplicate and conflicting duplicate behavior.
- Multiple sources of the same effect follow authored stacking policy.
- Expired effects no longer contribute modifiers.
- Snapshot round-trip preserves active effects and replay history when used for a run checkpoint.

---

## CHARACTER-COMPOSITION-001 — Load complete characters from account saves

**Status:** BLOCKED until `SAVE-ADAPTERS-001` is merged.

**Branch:** `agent/character-composition-001-account-to-hub`

**Dependencies:** `SAVE-CHARACTER-001`, `SAVE-ADAPTERS-001`, current Hub/character-select/loadout flow.

### Task

Replace fresh starter-runtime reconstruction with authoritative composition from the selected character slot.

### Requirements

- Selecting a slot loads the exact `CharacterInstanceSnapshotV1` and reconstructs its existing authorities through save adapters.
- Keep all six character slots isolated.
- Persist confirmed mutations from inventory/loadout, skills, crafting, shops, strongbox opening, XP, and wallets through an explicit save coordinator.
- Switching characters disposes/unbinds the previous runtime graph before activating the next.
- Preserve concrete equipment-instance identity and loadout bindings.
- Add one-time migration from the existing PlayerPrefs route-profile records into valid account characters with starter components.
- Migration must be idempotent and must not duplicate starter equipment.
- Failed restore/migration must retain the last valid save and show an explicit diagnostic rather than silently resetting progress.

### Tests

- Create two characters, mutate each differently, restart composition, and verify isolation.
- Close/reload restores XP, inventory, skills, loadout, money, scrap, and boxes.
- Switching characters does not leak cached authority state.
- PlayerPrefs migration runs once and preserves selected class/loadout identities.
- Corrupt selected character fails safely without damaging other slots.

### Non-goals

- No mission/run actor composition; that belongs to `RUN-SESSION-001`.

---

## COMBAT-HIT-POLICY-001 — Faction-aware reusable target eligibility

**Status:** READY, but assign only one combat-core owner at a time.

**Branch:** `agent/combat-hit-policy-001-faction-targeting`

**Dependencies:** shared combat identities, factions, player/enemy/prop damage ports, weapon effect facts.

### Task

Implement one reusable hit-eligibility policy shared by projectiles, explosions, melee swings, contact attacks, persistent fields, and future friendly-fire mechanics.

### Requirements

- Define immutable policy inputs for source actor, source faction, target actor/capabilities/faction, effect identity, geometry/effect kind, world-blocking behavior, self-hit, friendly-fire, pierce, and already-hit state.
- Provide content-addressable policy IDs such as player-normal, enemy-normal, and chaotic/all-factions without hardcoding enemy types.
- Unity physics layers may prefilter contacts, but the authoritative policy must make the final eligibility decision.
- Invalid/unknown actors and stale lifecycle generations fail closed.
- Damage authorities remain the only owners of health mutation.
- Existing weapon, enemy projectile, area, DoT pool, and melee paths must be able to consume the same policy result.
- Preserve deterministic hit ordering for multi-target effects.

### Tests

- Player effect hits enemy and damageable prop, ignores player ally.
- Enemy effect hits player and eligible prop, ignores normal enemy ally.
- Chaotic effect can damage all eligible factions except source.
- Wall/blocker behavior terminates or reflects according to effect policy.
- Same effect cannot hit one target more times than authored.
- Stale actor generation rejects.

### Non-goals

- Do not rebalance damage or rewrite weapon behaviors.

---

## ENEMY-FACTORY-001 — Room placement to independent live enemy actor

**Status:** BLOCKED until `ENEMY-DATA-001` is merged.

**Branch:** `agent/enemy-factory-001-placement-runtime`

**Dependencies:** enemy catalog, room placement identities, enemy decision/actor foundations, player authority, weapon execution core, room-live query/command ports.

### Task

Implement the generic factory that turns every imported enemy placement into an independent live enemy runtime without enemy-type-specific controller branches.

### Requirements

- Resolve placement object ID to enemy definition and presentation.
- Derive one stable actor/participant/lifecycle identity from run, room, and placement facts.
- Resolve level and difficulty scaling through the enemy definition policies.
- Compose movement, perception, targeting, ranged/melee attack capability, cooldown, and terminal collision adapters.
- Register player-damage routing, room occupancy, room-clear role, death facts, XP consumer, drop consumer, and kill-stat consumer through narrow ports.
- Enemy attacks require valid target, detection, vision, LOS, range, and attack arc.
- Attacks must use locked direction/intent facts so evasion is meaningful.
- Death must emit once with killer/source participant identity.
- Restart reconstructs authored state with a new lifecycle generation.
- Multiple placements of the same definition must remain independent.
- No switch on enemy type, room number, prefab name, or hierarchy name.

### Tests

- Instantiate at least ten placements with repeated definitions and distinct identities.
- Ranged, turret, pursuit, and melee fixtures execute through registered capabilities.
- Vision and attack arcs remain separate.
- Death awards downstream XP/drop once, never from enemy runtime directly.
- Room clear waits only for blocking occupants.
- Restart restores all actors and clears stale projectiles/intents.

### Non-goals

- Do not cut over the complete Level 1 room renderer; that belongs to `ROOM-JSON-LIVE-001`.

---

## CRIT-LIVE-001 — Deterministic critical-hit resolution

**Status:** BLOCKED until `DERIVED-STATS-001` and preferably `COMBAT-HIT-POLICY-001` are merged.

**Branch:** `agent/crit-live-001-deterministic-resolution`

**Dependencies:** derived run combat profile, modifier runtime, weapon/effect damage facts.

### Task

Connect `combat.critical-chance` and `combat.critical-multiplier` to live damage through deterministic, replay-safe critical resolution.

### Requirements

- Resolve crit chance/multiplier from the immutable run combat profile plus valid active effects.
- Derive one deterministic roll from immutable hit facts: run ID, source actor, equipment/effect identity, shot sequence, target actor, hit ordinal, and lifecycle generations.
- Record roll, threshold, critical result, multiplier, and final damage in an immutable resolution fact.
- Exact replay must return the identical result.
- Conflicting reuse of the same operation/hit identity must reject.
- Support policies for effects that cannot crit or use modified crit rules.
- Damage authority receives only the resolved final damage command.
- Adding another ordinary crit-chance skill must require only skill data.

### Tests

- Zero chance never crits; guaranteed chance always crits.
- Same hit facts reproduce the same result.
- Changed target/hit ordinal creates an independent roll.
- Exact duplicate cannot deal damage twice.
- Non-crittable effects ignore crit modifiers.
- Temporary killing-spree or event modifiers combine correctly where authored.

---

## CONDITION-LIVE-001 — Runtime facts to killing-spree and conditional effects

**Status:** BLOCKED until `STATUS-EFFECT-RUNTIME-001`; preferably after `ENEMY-FACTORY-001`.

**Branch:** `agent/condition-live-001-kill-fact-effects`

**Dependencies:** fact-window conditions, status effects, participant-attributed enemy death facts, run tick source.

### Task

Wire accepted gameplay facts into generic condition activation and temporary modifiers, proving a killing-spree effect without skill-specific runtime code.

### Requirements

- Convert accepted enemy death facts into stable `enemy-killed` observed facts for the correct participant.
- Feed facts exactly once into `FactWindowConditionAuthorityV1`.
- Resolve active condition IDs into status-effect applications and modifier projections.
- Include at least one data-defined killing-spree fixture: configurable kill count, window, active duration, and damage modifier.
- Do not create `KillingSpreeController`, enemy-type branches, or polling over kill counters.
- Support future fact types through registration/adapters, not central switches.
- Run restart clears run-local windows/effects while persistent skill allocation remains unchanged.

### Tests

- Required kills inside the window activate once.
- Kills outside the window do not activate.
- Duplicate death delivery does not increment the window.
- Different participants maintain independent windows.
- Effect expires and no longer modifies damage.
- A second unrelated fact-window fixture works without modifying the authority.

---

## RUN-SESSION-001 — Permanent character to transient mission runtime

**Status:** BLOCKED until `CHARACTER-COMPOSITION-001` and `DERIVED-STATS-001` are merged.

**Branch:** `agent/run-session-001-character-to-mission`

**Dependencies:** account-backed character composition, derived stats, player authority, weapon execution, mission result authority.

### Task

Create the authoritative transient run aggregate that freezes one selected character into one mission session without moving run-local state into the permanent save.

### Requirements

- Define immutable run identity, selected character identity, mission/layout ID, difficulty, deterministic seed, and frozen run combat profile.
- Compose player actor, weapon runtime states, status effects, abilities port, room query/command ports, collected strongboxes, run-only cash, and mission statistics.
- Keep health, room/position, cooldowns, active effects, bullets, and temporary pickups run-local.
- Provide immutable run snapshots for HUD/debug and an optional checkpoint contract distinct from permanent character saves.
- End Run remains exactly-once and returns immutable mission results.
- Applying results to permanent character authorities must be downstream and atomic.
- Restart/lifecycle generation must reject stale damage/effect facts.

### Tests

- Same selected character creates a new distinct run identity per new run.
- Frozen stats do not change when Hub equipment changes elsewhere.
- Restart increments lifecycle generation but preserves run identity where intended.
- End Run replay returns the same result.
- Permanent character is unchanged until accepted result application.

---

## BOX-PERSIST-001 — Durable unopened strongboxes and crash-safe opening

**Status:** BLOCKED until `SAVE-ADAPTERS-001` and `RUN-SESSION-001` are merged.

**Branch:** `agent/box-persist-001-unopened-lifecycle`

**Dependencies:** holdings, strongbox opening, reward application, mission result, account save adapters, run session.

### Task

Complete the exact strongbox lifecycle from physical run pickup through durable unopened storage and crash-safe deterministic opening.

### Requirements

- Physical collection records exact strongbox instance identity and provenance in the run.
- Accepted mission completion transfers exact unopened box instances into the selected character through existing reward/holdings authorities.
- Save immediately after accepted transfer.
- Results display and re-entry must not open, reroll, consume, or duplicate boxes.
- Opening one exact box freezes its deterministic result before applying rewards.
- Persist sufficient opening state so closing before, during, or after presentation resumes the same terminal result.
- Reward application and box consumption must be exactly-once and atomically persisted.
- Different boxes may produce the same equipment definition; every equipment result remains a distinct instance.

### Tests

- Close after win before Results; box remains unopened after reload.
- Close during opening; reload resumes identical reward.
- Repeated confirm/back/retry/re-entry cannot award twice.
- Two boxes with same resulting weapon definition produce distinct instances.
- Corrupt save recovery never rerolls an already frozen accepted opening.

---

## STAGE1-FREEZE-001 — Freeze retained Stage 1 migration surfaces

**Status:** READY after verifying PR #280 and `DROP-STRONGBOX-LIVE-001` / PR #284 are merged and no active PR owns the Stage 1 production composition files.

**Branch:** `agent/stage1-freeze-001-migration-guardrails`

**Dependencies:** PR #280; `DROP-STRONGBOX-LIVE-001` / PR #284; current Stage 1 production composition, pickup, terminal-drop, reward-transfer, and legacy scene surfaces.

### Task

Freeze the retained Stage 1 migration surfaces before authored-room cutover and runtime decomposition. Stop architectural growth, inventory current responsibilities, and establish source-level guardrails without performing the extraction itself.

### Frozen migration targets

At minimum:

- `Stage1VisibleSliceController`;
- `Stage1PlayableLoopCompositionV1` and every partial;
- `Stage1RunPickupBootstrap2D` and every partial;
- `Stage1RunPickupPropBootstrap2D` and every partial;
- `Stage1TerminalDropContentV1`;
- current Stage 1 terminal fact/resolver/support and durable-transfer integration;
- Stage 1 legacy prop migration integration;
- Stage 1 self-installing runtime components.

### Requirements

- Add a machine-readable responsibility manifest recording each target's fully qualified type, source files, approximate line count, interfaces, Unity lifecycle methods, mutable state, discovery, reflection, gameplay responsibilities, intended owner, and deletion/presentation destination.
- Add a human-readable report covering health/player authority, movement/input, weapon execution, damage/effects, enemy runtime, prop runtime, room truth, traversal/access, pickups, reward generation, durable transfer, Results, HUD, restart, and scene installation.
- Declare `Stage1VisibleSliceController` and `Stage1PlayableLoopCompositionV1` as separate retirement targets. Moving behavior from the former into the latter is prohibited.
- Mark retained types clearly as migration-only without renaming the main types.
- Declare the intended replacement boundaries without creating empty placeholders:
  - `Stage1SceneInstaller2D`;
  - narrow `Stage1RunLoopDriver2D`;
  - `InventoryWeaponEffectDamageRouter2D`;
  - `Stage1RoomFlowController2D`;
  - `Stage1EnemyTerminalPickupConsumerV1`;
  - `Stage1PropTerminalPickupConsumerV1`;
  - `RunPickupLifecycleProjection2D`;
  - `Stage1LegacyScenePresentation2D` or `Stage1SceneView2D`.
- `Stage1RunLoopDriver2D` may only observe Run Session lifecycle, forward typed commands, coordinate lifecycle projections, and request restart/end through ports. It must not inherit player, movement, weapon, enemy, room, reward, transfer, Results, or persistence authority/state.
- Add source-level audits that distinguish exact inventoried debt from new violations and reject:
  - unlisted Stage 1 production source files;
  - additional `SceneManager.sceneLoaded` subscribers or self-installers;
  - additional private reflection into either retirement target;
  - new name/hierarchy/room-number gameplay decisions;
  - new authority or persistence interfaces on retained targets, including interfaces added through partial declarations;
  - direct creation of another `RunSessionAggregateV1` or duplicate money, scrap, inventory, holdings, reward, strongbox, room, player-health, or enemy-health authorities;
  - new reward probability/selection logic;
  - new weapon-name or weapon-definition switches;
  - new content registration requiring edits to a retained Stage 1 controller.
- Permit intentional deletion/extraction by updating the source baseline, debt entry, and migration plan together.
- Preserve all runtime behavior.

### Tests

- Manifest matches the current source tree and every known debt entry appears exactly once.
- Fixture additions for a new scene hook, private reflection, name-based decision, authority interface, partial-class interface, unlisted Stage 1 source, Stage 1 source outside legacy folders, multiline weapon-definition switch, and previously unknown authority construction all fail.
- Ordinary data-only content does not require a Stage 1 edit.
- Genuine legacy-source deletion plus matching manifest/debt update passes.
- End-to-end temporary-repository fixtures exercise both passing and failing `run_audit()` paths.

### Non-goals

- No room JSON production cutover.
- No large controller extraction or controller deletion.
- No weapon-effect rewrite, scene/prefab redesign, pickup decomposition, Results/reward behavior change, balancing, or content change.

### PR proof

Include the exact launch `main` SHA, current line/interface/lifecycle inventories, scene-hook and reflection inventories, responsibility-to-owner map, architecture test output, changed-file audit, and confirmation that runtime behavior was not intentionally changed.

---

## ROOM-JSON-LIVE-001 — Cut production Level 1 over to authored room packages

**Status:** BLOCKED until `ROOM-ACCESS-001`, `ENEMY-FACTORY-001`, and `PROP-RUNTIME-001` are merged.

**Branch:** `agent/room-json-live-001-production-cutover`

**Dependencies:** room JSON importer, room-live authority, generic enemy factory, prop runtime/factory, access conditions, door/traversal presentation.

### Task

Make the production Level 1 route instantiate and run entirely from the authored JSON room package rather than retained manual room/enemy bindings.

### Requirements

- Load and validate the manifest and all referenced room documents.
- Construct room roots, bounds, floor/decor layers, spawns, props, doors, enemies, encounters, and final exits from imported content.
- Instantiate every enemy through `ENEMY-FACTORY-001` and every gameplay prop through `PROP-RUNTIME-001`.
- Bind door/access conditions through `ROOM-ACCESS-001`.
- Traverse using authored links and exact spawn identities.
- Retain defeated enemies, destroyed props, consumed keys, collected drops, and visited/clear/completed state correctly on return.
- Restart reconstructs authored state with new lifecycle generations.
- Remove production dependence on room-number coordinate checks, marker proxies, and manually named enemy package fields.
- Do not add a second room runtime or duplicate scene-specific encounter logic.

### Tests

- Production Bootstrap → Hub → Level1 route loads the two-room JSON fixture.
- Add a third fixture room without production C# changes and traverse it.
- Add repeated enemy and prop definitions with distinct placement identities.
- Locked/keyed door, return door, and final exit behave independently.
- Return does not respawn retained terminal occupants.
- Restart restores authored initial state.

### Manual acceptance

Capture screenshots/video of the authored rooms, door states, enemy placement, return traversal, and final exit.

---

## STAGE1-RUNTIME-DECOMPOSE-A-001 — Extract installation, lifecycle coordination, and room projection

**Status:** BLOCKED until `STAGE1-FREEZE-001` and `ROOM-JSON-LIVE-001` are merged.

**Branch:** `agent/stage1-runtime-decompose-a-001-install-run-room`

**Dependencies:** the Stage 1 migration manifest/guardrails; authored-room production cutover; existing Run Session, player scene adapter, generic room authority, and production flow ports.

### Task

Remove self-installation, global discovery, reflection, and room/lifecycle orchestration from the retained composition. Establish explicit installation plus narrow lifecycle and room-projection boundaries without moving gameplay ownership into another Stage 1 aggregate.

### Requirements

- Create an explicit `Stage1SceneInstaller2D` boundary owned by the production route/scene composition. Remove the retained global `sceneLoaded` hook, fallback scene scans, reflection, and component self-attachment.
- Create a narrow `Stage1RunLoopDriver2D` that may only:
  - observe `RunSessionAggregateV1` lifecycle snapshots/facts;
  - forward typed start/restart/end commands through existing ports;
  - coordinate lifecycle generation changes across projections;
  - expose diagnostics needed by production flow tests.
- The driver must not own or implement player input/movement, weapon selection/execution, weapon effects/damage, enemy runtime/scheduling, room truth, reward generation, durable transfer, Results navigation, save/persistence, HUD, or presentation.
- Keep player input and movement in the canonical player scene/input adapter, including `Level1PlayerRuntimeSceneAdapterV1` and its existing typed input/lifecycle ports.
- Keep weapon selection/execution in `InventoryWeaponRuntimeComposition` and `InventoryBackedWeaponExecutionAdapter`; the driver may forward commands but may not recreate their state.
- Keep authoritative room truth in the generic room authority / `RoomRuntimeComposition2D`. Extract only Unity room/traversal projection into `Stage1RoomFlowController2D`.
- `Stage1RoomFlowController2D` must consume authored room/link/spawn identities and typed room snapshots. It must not infer rooms from coordinates, names, hierarchy, or room numbers.
- Preserve the existing Run Session aggregate and existing authority instances; do not create replacements or duplicate ports.
- Delete or shrink the migrated composition partials as responsibilities move. Do not leave forwarding methods that retain hidden mutable state.
- Preserve current gameplay and visible presentation.

### Tests

- Production Bootstrap → Hub → Level1 installs Stage 1 exactly once without `SceneManager.sceneLoaded`, global object search, or private reflection.
- Scene reload and route re-entry do not duplicate installers, drivers, room projections, authorities, or subscriptions.
- Run start/restart/end commands pass through typed ports and stale lifecycle facts reject.
- Player input/movement and weapon firing remain owned by their existing adapters/runtime.
- Authored room traversal, return traversal, door states, and restart behave identically.
- Architecture audit proves the driver has no concrete authority/persistence/runtime-owner fields or constructions.

### Non-goals

- No weapon damage/effect extraction.
- No enemy scheduler extraction.
- No pickup, reward, durable-transfer, Results, or presentation extraction; those belong to `STAGE1-RUNTIME-DECOMPOSE-B-001`.
- No controller deletion; final serialized-reference cleanup belongs to `LEVEL1-CONTROLLER-RETIRE-001`.

### PR proof

Include before/after responsibility and subscription inventories, deleted reflection/discovery proof, driver ownership audit, changed-file audit, focused EditMode/PlayMode XML, and manual room/restart acceptance evidence.

---

## STAGE1-RUNTIME-DECOMPOSE-B-001 — Extract combat routing, pickups, transfer, Results, and presentation

**Status:** BLOCKED until `STAGE1-RUNTIME-DECOMPOSE-A-001` is merged.

**Branch:** `agent/stage1-runtime-decompose-b-001-combat-pickups-transfer`

**Dependencies:** decomposition A; authored-room production route; inventory weapon runtime; generic enemy factory/runtime and attack scheduler; terminal-drop/pickup authorities; collected-run transfer/persistence; production Results flow.

### Task

Remove the remaining combat, enemy, pickup, reward-delivery, durable-transfer, Results, and presentation responsibilities from `Stage1PlayableLoopCompositionV1` and the retained pickup bootstraps. Connect existing canonical owners through focused adapters without turning `Stage1RunLoopDriver2D` into another god object.

### Requirements

- Extract projectile/effect realization and hit routing into `InventoryWeaponEffectDamageRouter2D`. It consumes immutable weapon-effect descriptions and typed target/damage ports; it does not select weapons, own cooldowns, or mutate reward/room state.
- Keep weapon selection and execution in `InventoryWeaponRuntimeComposition` / `InventoryBackedWeaponExecutionAdapter`.
- Keep enemy construction/runtime in the generic enemy factory/runtime and attack execution in the existing scheduler, including `EnemyAttackPatternLiveSchedulerV1`. Stage 1 adapters may bind authored placements and presentation only.
- Extract enemy terminal reward/pickup translation into `Stage1EnemyTerminalPickupConsumerV1` and prop terminal translation into `Stage1PropTerminalPickupConsumerV1`. They consume immutable attributed terminal facts and call existing reward/pickup authorities; they do not own probability tables, reward selection, wallets, holdings, or enemy/prop health.
- Extract pickup realization, collection projection, restart cleanup, and presentation into `RunPickupLifecycleProjection2D`. It must not own reward generation or durable character state.
- Keep durable transfer and exactly-once persistence in the existing collected-run transfer preparation/receipt/persistence services, including `CollectedRunRewardTransferPreparationFactoryV2` and `ProductionCollectedRunRewardPersistenceV2`.
- Keep Results publication/navigation in the production Results/flow bridge, including `ProductionCollectedRunRewardResultsBridge`; do not implement navigation in the run-loop driver.
- Reduce `Stage1WeaponPresentationRepairV1` to presentation-only compatibility or move it into `Stage1LegacyScenePresentation2D` / `Stage1SceneView2D`. Definition-specific gameplay decisions are forbidden.
- The narrow `Stage1RunLoopDriver2D` may connect typed ports and coordinate lifecycle order, but it must not store or implement player, weapon, enemy, room, reward, transfer, Results, or persistence state.
- Delete migrated composition/pickup partials or reduce them to temporary delegation scheduled for final deletion. Do not retain a second hidden composition root.
- Preserve all gameplay, reward, Results, persistence, and restart behavior.

### Tests

- All inventory weapon effect kinds route damage through the new router with deterministic hit ordering and no duplicate application.
- Enemy attacks continue through the generic scheduler; adding an enemy using existing capabilities requires no Stage 1 controller edit.
- Enemy and prop terminal facts produce exactly one accepted pickup batch and preserve participant attribution/provenance.
- Pickup restart/re-entry does not duplicate, lose, or recollect rewards.
- Final exit uses the existing durable transfer/persistence path exactly once; crash/retry behavior and unopened strongbox identity remain unchanged.
- Results publication/re-entry does not reroll, reopen, duplicate, or navigate through the run-loop driver.
- Architecture audit proves no new authority, persistence, probability, weapon-definition switch, global discovery, or unlisted Stage 1 source was introduced.

### Non-goals

- No new weapon, enemy, prop, room, reward, or balancing content.
- No replacement reward, pickup, persistence, Results, player-health, enemy-health, room, or run authority.
- No final serialized controller deletion; that belongs to `LEVEL1-CONTROLLER-RETIRE-001`.

### PR proof

Include before/after responsibility and line-count inventory, canonical-owner map, deleted partial/bootstrap audit, architecture audit output, focused and full Unity EditMode/PlayMode XML, changed-file audit, and manual combat/pickup/final-exit/Results acceptance evidence.

---

## ABILITY-RUNTIME-001 — Data-driven active abilities and cooldowns

**Status:** BLOCKED until `RUN-SESSION-001`, `DERIVED-STATS-001`, `STATUS-EFFECT-RUNTIME-001`, and `STAGE1-RUNTIME-DECOMPOSE-B-001` are merged.

**Branch:** `agent/ability-runtime-001-data-driven-active-abilities`

**Dependencies:** skills, derived stats, run session, status effects, player/weapon/effect ports, and the decomposed Stage 1 runtime boundaries.

### Task

Implement the generic active-ability definition and runtime system so abilities unlocked by class or skills use reusable execution capabilities, charges, cooldowns, and temporary effects.

### Requirements

- Stable ability definition IDs and behavior/capability registry.
- Support cooldown ticks, charges, recharge policy, target mode, cast/commit timing, effect descriptors, and status-effect application.
- Ability runtime state is run-local and keyed by actor/lifecycle/ability identity.
- Allocation/class data determines which abilities are available; it does not own cooldown state.
- Exact cast operation replay and conflicting duplicate handling.
- Locked target/direction/position facts for committed attacks.
- Adding an ability using registered capabilities is definition + presentation + tests only.

### Tests

- Cooldown, charges, recharge, replay, conflict, restart, and stale lifecycle behavior.
- One self-buff, one targeted effect, and one area ability fixture.
- Skill respec outside a run rebuilds availability for the next run.

---

## EXTENSIBILITY-GUARDRAILS-001 — Prove ordinary content additions require no production edits

**Status:** BLOCKED until enemy data, prop runtime, room access, derived stats, and event modifier foundations are merged.

**Branch:** `agent/extensibility-guardrails-001-content-proof`

**Dependencies:** the data-driven foundations above.

### Task

Add automated architectural and content-validation proof that ordinary content additions do not require editing existing gameplay classes.

### Requirements

- Add fixture content for:
  - one new numerical skill such as crit chance;
  - one fact-window/temporary-effect skill;
  - one enemy using existing capabilities;
  - one prop using existing capabilities;
  - one room;
  - one key and locked door;
  - one special drop-rate event.
- Tests must import/register/use each fixture through public extension points.
- Add source/path audits or architecture tests that fail when known central switch/controller files are modified merely to register fixture content.
- Add JSON schema or equivalent validation documentation for room, enemy, prop, event, and access definitions.
- Diagnostics must identify exact file/object/field for unknown IDs or unsupported capabilities.
- Document the extension checklist for designers and developers.

### Acceptance standard

For every fixture above, the production implementation change should consist only of new definitions/assets/registration data and focused tests. No new scene-specific or type-specific branch is allowed.

---

## LEVEL1-CONTROLLER-RETIRE-001 — Retire the retained giant scene controller

**Status:** FINAL CUTOVER. BLOCKED until `CHARACTER-COMPOSITION-001`, `RUN-SESSION-001`, `ROOM-JSON-LIVE-001`, `STAGE1-RUNTIME-DECOMPOSE-A-001`, `STAGE1-RUNTIME-DECOMPOSE-B-001`, `ABILITY-RUNTIME-001`, `BOX-PERSIST-001`, and the combat routing cutovers are merged.

**Branch:** `agent/level1-controller-retire-001`

**Dependencies:** all production cutovers listed above, explicitly including both Stage 1 decomposition tasks.

### Task

Remove the remaining gameplay ownership from `Stage1VisibleSliceController` and either delete it or reduce it to a small typed scene-binding compatibility facade.

### Requirements

- Freeze the controller first; no new behavior may be added during migration.
- Move serialized scene references into typed scene-binding components without unsafe bulk YAML edits.
- Move player presentation/input projection to the canonical player scene adapter.
- Move room/door/traversal presentation to the canonical room presentation adapter.
- Delete retained weapon firing, cooldown, projectile, area/DoT, local damage, fixture loadout, room-state, HUD-state, and manual enemy-binding logic after production-route tests replace it.
- Remove coordinate room traversal, marker proxy creation, manual enemy package properties, and direct-scene legacy test hooks.
- Migrate tests to Bootstrap → Hub → Level1 and public commands/immutable snapshots.
- Final controller target: approximately 100–250 lines of references/delegation and zero gameplay authority, or complete deletion when no serialized dependency remains.
- Preserve current visible gameplay and scene assets.

### Tests

- Full production acceptance route passes.
- No direct health, reward, XP, loadout, room, weapon, or enemy authority remains in the controller.
- No production test requires reflection into controller internals.
- Serialized scene opens with no missing scripts/references.
- Quick restart, weapon execution, enemy combat, room traversal, Results, and unopened boxes still function.

### PR requirements

Include before/after responsibility inventory, final line count, deleted-code audit, scene/prefab reference audit, full Unity EditMode and PlayMode XML, and manual acceptance screenshots/video.

---

# Recommended next commands

Safe examples after verifying GitHub:

- `Check CURRENT_TASKS and do SAVE-ADAPTERS-001.`
- `Check CURRENT_TASKS and do DERIVED-STATS-001.`
- `Check CURRENT_TASKS and do ENEMY-DATA-001.`
- `Check CURRENT_TASKS and do PROP-RUNTIME-001.`
- `Check CURRENT_TASKS and do ROOM-ACCESS-001.`
- `Check CURRENT_TASKS and do EVENT-MODIFIER-001.`

Do not dispatch two tasks that claim ownership of the same production composition or shared core files at the same time.

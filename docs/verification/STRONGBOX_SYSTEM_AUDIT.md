# Shooter Mover Strongbox-System Audit

## Status

Static repository audit and future-plan document.

This audit summarizes the current strongbox architecture, how far it is connected
to playable game systems, the main maintainability risks, and the recommended path
toward a complete player-visible feature.

The connector environment used for this audit could inspect repository source and
history, but could not launch Unity. Compilation, PlayMode execution, save/reload
acceptance and hands-on gameplay remain unverified.

## Audit questions

The review focused on these questions:

1. How connected are strongboxes to the actual game?
2. How configurable and maintainable is the loot model?
3. Can future skills alter box drop chances or upgrade/skip tiers cleanly?
4. How close is the project to visually opening a chosen number of boxes at a
   chosen character level?
5. How close is a generated item to entering inventory, being equipped and being
   usable in combat?
6. What next step produces the most visible progress without creating a second
   loot authority or disposable prototype code?

## Executive conclusion

The strongbox system is substantially further along in backend architecture than
it is in player-visible integration.

The repository already has a credible production transaction spine:

```text
owned exact strongbox instance
    -> validate ownership and operation identity
    -> resolve production tier and opening policy
    -> generate a deterministic reward
    -> construct exact equipment payloads
    -> commit and apply the reward through RAP
    -> consume the exact source strongbox
    -> preserve retry and replay state
    -> project the immutable result for presentation
```

The difficult anti-duplication and partial-failure behavior is therefore not just
an idea. `StrongboxOpeningServiceV1` binds an opening to an exact instance,
rejects conflicting retries, replays exact completed operations without minting
new rewards, handles pending reward application, and consumes the box only after
the reward is applied.

The weaker area is runtime composition. Static source proves that the authorities
and presentation pieces exist, but does not prove that normal gameplay currently
completes this uninterrupted route:

```text
enemy or mission reward
    -> persistent owned box
    -> Hub strongbox collection
    -> select exact box
    -> opening scene
    -> persistent equipment inventory
    -> equip exact instance
    -> runtime weapon construction
    -> combat use
```

The best next step is therefore not another loot-algorithm redesign. It is one
production-backed vertical slice that demonstrates a real box becoming a real,
persistent and usable weapon.

## Current readiness summary

| Area | Current assessment | Main evidence or limitation |
|---|---|---|
| Exact strongbox ownership | Strong | Holdings use stable exact instance IDs rather than UI slots or display names. |
| Opening transaction | Strong | Opening, reward application, retry, replay and source-box consumption share one service path. |
| Anti-reroll and duplicate protection | Strong | Completed operations replay the original outcome; conflicting operations are rejected. |
| Static tier and loot configuration | Moderately strong | Production catalogs and hybrid policies exist and are shared with the simulator. |
| Statistical simulation | Moderately strong | A production-backed simulator gateway exists, but recent simulator work was not fully backed by executed automated tests in the inspected evidence. |
| Standalone reveal presentation | Moderate | A dedicated opening UI flow and standalone scene exist, including preview and production-result projection. |
| Normal Hub/gameplay integration | Incomplete or unverified | Static source does not prove normal navigation and bootstrap reach the opening service with real saved holdings. |
| Inventory insertion | Backend path is strong; runtime proof missing | RAP payloads contain exact equipment instances, but the real save envelope and normal inventory UI were not executed. |
| Equip and combat use | Partially connected | Strongbox weapon candidates resolve through live equipment and weapon definitions, but exact-instance equip-to-fire acceptance was not observed. |
| Skill-modified acquisition | Early | No clear canonical modifier pipeline was verified for many interacting player-specific box-drop effects. |
| Armor and general gear loot | Less mature than weapons | The production simulator projection inspected is explicitly weapon-focused. |

## What is already architecturally sound

### Exact identities are authoritative

Strongboxes are represented as exact owned instances. Opening and removal commands
operate on stable instance identities, not a mutable tier count, inventory index,
display name or screen position.

A grouped UI such as `Steel x 12` should remain a projection over twelve exact
instances. Selection and opening must resolve back to the exact identities.

### The opening service is the canonical transaction route

`StrongboxOpeningServiceV1` coordinates the production definition catalog,
reward generator, holdings authority, reward application service and payload
resolver. It validates the requested box and its content fingerprint before
opening.

The service also stores opening records and exports/imports its own snapshot. This
is a good foundation for retry and restart recovery, although the combined game
save composition still needs runtime verification.

### Reward application precedes source-box consumption

The opening flow prepares and commits the generated reward, claims or retries the
reward through RAP, and only then removes the owned strongbox. A failure to remove
the box leaves a consume-pending operation rather than pretending that the whole
opening never happened.

This is the correct direction for avoiding both lost rewards and duplicate rewards.

### The simulator uses production authorities

The authoritative simulator gateway projects its eligible live weapon metadata
from the production equipment and weapon catalogs and fingerprints the production
hybrid strongbox policies. It does not need a second simulator-only rarity table.

The simulator and reveal UI must continue to consume the production result. They
must never become alternative loot-generation authorities.

### The presentation layer understands immutable results

The strongbox opening presentation contains a staged flow for closed box,
opening animation, reward reveal and continue/back behavior. It can project
money, scrap, equipment and other reward payloads, including exact equipment
instance identity and item metadata.

Preview-only operation is useful for authoring, but production navigation must be
guarded so a preview result cannot masquerade as a durable inventory grant.

## Main gaps and risks

### 1. The complete gameplay caller chain is not proven

The opening authority exists, but an authority with no normal production caller
is not a finished feature. Mission rewards, Hub collection, scene navigation,
opening composition, inventory refresh and loadout use must be validated as one
workflow.

### 2. Player-specific loot modifiers need a canonical boundary

Static tier balance is reasonably organized. A larger collection of skills,
difficulty modifiers, pity rules and event rules would become difficult to
maintain if each feature edits distributions or calls randomness independently.

Acquisition and opening are two different decisions and should remain separate:

```text
Which box is awarded?
    reward source
    -> base tier distribution
    -> acquisition modifiers
    -> exact tier roll
    -> exact StrongboxInstance grant

What is inside an owned box?
    exact StrongboxInstance
    -> tier/opening profile
    -> opening modifiers
    -> exact EquipmentInstance grant
```

A skill that upgrades a Copper drop to Iron should change the awarded tier before
the instance becomes owned. An owned Copper box should not silently behave like
Iron unless an explicit, visible opening modifier defines that behavior.

### 3. Configuration remains relatively code-centric

The repository has a clear design direction for small, versioned tier definitions,
opening profiles and equipment-drop metadata. The current production route still
relies heavily on C# production catalogs and policy classes.

That is safe and deterministic, but frequent balance work will become easier once
one validated authored-data route loads immutable runtime policies. JSON, YAML,
ScriptableObjects and C# catalogs must not all become competing write authorities.

### 4. Save/reload composition is unverified

The opening service has snapshots, but the real game must restore all relevant
authorities together:

- player holdings;
- reward application state;
- strongbox opening state;
- generated equipment augment signatures;
- character/loadout state where applicable.

The restore order and content-version behavior need one real restart acceptance
path.

### 5. Generated weapon usability is not yet an observed invariant

The production simulator restricts its weapon projection to equipment that maps to
live weapon definitions, which is encouraging. The remaining proof is behavioral:
an exact generated instance must appear in normal inventory, be equipped by its
instance ID, resolve its runtime weapon definition, enter the playable loadout and
fire correctly.

## Five-step future plan

### Step 1 — STRONGBOX-VERTICAL-001: real box to real inventory

Build a production-backed Hub or development flow that can:

- create an isolated profile at a chosen level;
- grant exact production strongbox instances;
- display owned boxes grouped by tier while preserving exact identities;
- open one selected instance through `StrongboxOpeningServiceV1`;
- show the production result through the existing reveal presentation;
- confirm the source box disappears and the exact equipment instance appears in
  the real holdings/inventory projection.

This step should not add new balance formulas or skill modifiers.

### Step 2 — Persist, equip and fire the exact reward

Extend the same vertical slice through the normal save/load and loadout routes:

- save after opening;
- restart and restore the same remaining boxes and generated items;
- equip one exact generated live weapon instance;
- enter a small test arena;
- fire the generated weapon successfully;
- retry the completed opening operation and confirm that no second reward is
  created.

This is the decisive milestone that proves strongboxes are a game feature rather
than a collection of disconnected subsystems.

### Step 3 — Add a production-backed Strongbox Lab

After the single-box path is authentic, add development controls for:

- profile level;
- production tier;
- quantity;
- deterministic seed;
- Open 1, Open 5 and Open All;
- animation skip or fast-forward;
- aggregate summaries by definition, rarity, item level and augment signature.

Visual batch opening should execute individual authoritative opening transactions
against disposable exact instances. Statistical reports may use the batch
simulator, but the visual inventory demonstration must not bypass production
ownership and reward application.

### Step 4 — Introduce acquisition and opening modifier contracts

Add two explicit immutable extension boundaries:

1. a strongbox acquisition-distribution modifier pipeline for selecting the tier
   that becomes owned;
2. a strongbox opening modifier pipeline for changing defined parts of the content
   calculation without taking ownership of the roll.

Implement only one proof modifier in each boundary first, for example:

- a deterministic skill chance to upgrade an awarded box by one tier;
- a small skill modifier to augment-capacity weighting during opening.

Modifier ordering, validation, diagnostics, fingerprints and replay inputs must be
explicit. Skills must not call random functions or replace the final result on
their own.

### Step 5 — Data-drive balance and expand content

Once the runtime route and modifier contracts are stable:

- move tier/opening balance toward one validated versioned authored-data route;
- add authoring validation for duplicate IDs, unknown definitions, invalid weights,
  empty pools and unsupported tier transitions;
- expand production equipment categories only when inventory, loadout and runtime
  consumers support them;
- add pity, discovery and targeted-box features through the canonical modifiers;
- add final box art, animations, sound and high-tier presentation without changing
  the committed loot result.

## Parallel-work guidance

### Recommended active implementation load

At the current integration stage, the safest target is **two major implementation
tracks at once**:

1. one authority/integration track;
2. one presentation, content or isolated tooling track.

Three simultaneous tracks are possible only when they have explicit file and
contract ownership and do not all modify holdings, progression, loadout or reward
application.

### Work that can run in parallel with Step 1

The following can proceed with relatively low conflict:

- final strongbox sprites, tier presentation and reveal effects;
- UI layout and reward-card presentation using immutable view models;
- statistical simulator report improvements that continue to call the production
  gateway;
- additional live weapon definitions that preserve existing equipment/runtime
  reference contracts;
- unrelated level-editor or environment work.

### Work that should not independently rewrite shared authorities

Avoid running these as disconnected architecture changes at the same time as the
strongbox vertical slice:

- a player-holdings or inventory authority replacement;
- a reward-application rewrite;
- a new equipment-instance persistence model;
- a competing loadout/equip authority;
- a separate loot RNG or rarity policy;
- skill-specific box mutations before modifier contracts exist.

If inventory, weapon and progression audits all recommend changes to these shared
boundaries, choose one integration owner and sequence the shared-contract changes.
The feature-specific UI and content work can remain parallel.

### Dependency relationship with other likely audit tracks

- **Inventory:** Steps 1 and 2 depend directly on the canonical inventory projection,
  persistence and equip routes. Strongbox and inventory implementation should be
  coordinated, not independently redesigned.
- **Weapons:** Step 2 depends on exact generated weapon instances resolving through
  the normal runtime weapon factory. New weapon content can run in parallel if the
  reference contract remains stable.
- **XP and skills:** Step 4 should wait until the canonical skill snapshot and
  progression context are clear. The vertical slice does not need to wait for the
  full skill system.
- **Enemy rewards:** Enemy work may expose a stable reward-source event or mission
  reward request in parallel. Final tier acquisition modifiers should remain in the
  canonical acquisition resolver rather than enemy scripts.

## Acceptance target

The strongbox feature should be considered vertically connected when this flow is
observed in Unity:

1. Start a development profile at level 10.
2. Grant ten exact Steel strongboxes.
3. See `Steel x 10` in the Hub projection.
4. Open five through production transactions.
5. Watch five reveals derived from committed results.
6. See `Steel x 5` and five exact generated equipment instances.
7. Restart the game and recover the same state.
8. Equip one generated live weapon.
9. Enter the test arena and fire it.
10. Retry one completed opening operation and receive no additional item.

## Validation status

- static source review: completed for the inspected strongbox authorities,
  simulator and reveal implementation;
- structural production-path analysis: completed;
- compilation: not executed;
- automated tests: not executed in this audit;
- Unity EditMode/PlayMode tests: not executed in this audit;
- save/reload acceptance: not executed;
- manual opening/inventory/equip/combat acceptance: not executed;
- performance testing: not executed.

This PR should remain draft because it records planning and static findings rather
than executed Unity acceptance.

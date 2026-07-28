# XP progression audit and future plan

**Audit date:** 2026-07-27  
**Scope:** Current production path from enemy death through XP, level-up, skill allocation, persistence, and gameplay use.  
**Purpose:** Summarize the present state and provide a bounded implementation plan that can be coordinated with the repository's other feature audits.

## Executive summary

The XP and skill foundations are substantially stronger than the current playable feature suggests. Canonical enemy death, exactly-once XP grants, level-up facts, account-backed character persistence, ranked skill allocation, skill migration, respec foundations, and a Skills presentation all exist.

They do not yet form one production path.

The current playable sequence is:

```text
Player weapon damages enemy
-> canonical enemy death
-> room records the terminal occupant
-> room-clear exit can unlock
-> XP/drop/kill-stat downstream consumers receive the death fact
-> NoRewardPort discards it
```

The normal production Skills route is also deliberately disconnected. The visible screen is based on the older `SkillProgressionAuthorityV1`, while the persisted character graph owns `RankedSkillAllocationAuthorityV2`.

Therefore, the player currently cannot:

1. earn XP by killing a production enemy;
2. level through the production combat path;
3. spend a persisted skill point through the normal Skills screen;
4. make a purchased skill alter real gameplay;
5. equip or activate an active skill.

This is primarily an integration problem, not a reason to rewrite the XP or enemy-health systems.

## Current state by stage

| Stage | Current state | Assessment |
|---|---|---|
| Player weapon damages enemy | Connected | Production route exists |
| Enemy dies canonically | Connected | One authoritative enemy runtime |
| Death updates room-clear state | Connected | Exact room/placement/lifecycle validation |
| Death awards XP | Disconnected | `RoomEnemySpawner2D` injects `NoRewardPort` |
| XP calculates level-ups | Implemented | Deterministic and exactly-once |
| Level-up awards skill points | Implemented | Current policy is one point per level, including level 1 |
| Level-up awards another reward | Not implemented | No milestone reward authority exists |
| Skill allocation persists | Implemented in V2 foundation | Owned by the production character graph |
| Production Skills screen allocates V2 ranks | Disconnected | Screen is opened in disconnected mode |
| Passive skill changes gameplay | Not connected | Effect projection exists without a production consumer |
| Active skill can be equipped and used | Not implemented | No active-ability loadout/runtime yet |

## Important findings

### 1. Enemy death is already a suitable reward boundary

The canonical enemy runtime already produces a validated death fact containing stable run, room, placement, actor, participant, definition, level, and lifecycle information. `RoomEnemySpawner2D` verifies these identities before accepting the terminal transition.

This is the correct seam for XP and rewards. XP logic should consume the accepted canonical death fact rather than being inserted into projectiles, damage handlers, enemy health, or room-clear code.

Relevant files:

- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeInstanceV1.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemyActor2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemySpawner2D.cs`

### 2. XP-001 is ready to be used

`PlayerExperienceAuthorityV1` already provides:

- deterministic level thresholds;
- exactly-once source operation handling;
- duplicate replay without additional XP;
- conflicting duplicate rejection;
- ordered multi-level facts;
- level cap and overflow XP;
- snapshot import/export;
- replay protection after XP snapshot restoration.

The current production default uses a flat placeholder curve of 100 XP per level because the configured minimum and maximum costs are both 100. This is suitable for a vertical slice, not final balancing.

Relevant files:

- `Assets/ShooterMover/Runtime/Application/Progression/Experience/PlayerExperienceAuthorityV1.cs`
- `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs`
- `docs/architecture/progression/PLAYER_EXPERIENCE_V1.md`

### 3. Current skill-point policy is explicit but still a product decision

The current rule is:

```text
total awarded skill points = player level
```

A fresh level-1 character therefore begins with one point, and each crossed level boundary grants one more point.

This is internally consistent across XP and ranked-skill tests. It should be confirmed before persistent player progression is exposed, because changing later to `level - 1` or milestone-only points requires migration and changes the allocation contract.

### 4. Production owns ranked skills V2, but the visible screen uses V1

The account-backed `ProductionCharacterRuntimeGraphV1` owns and persists `RankedSkillAllocationAuthorityV2`. V2 supports class eligibility, variable caps, prerequisites, category gates, milestones, synergies, migration, and respec foundations.

The current `SkillsScreenSessionV1` instead uses `SkillProgressionAuthorityV1`. `ProductionFlowCoordinatorV1` opens the Skills scene with `ShowDisconnected(...)`, so the normal route deliberately cannot mutate either authority.

This split should be resolved by making V2 the only production skill mutation route. V1 may remain temporarily as a compatibility/test fixture, but new production features should not extend both models.

Relevant files:

- `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs`
- `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs`
- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs`
- `Assets/ShooterMover/UI/Skills/SkillsSceneController.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionFlowCoordinatorV1.cs`

### 5. V2 allocation should not trust caller-supplied level

`AllocateSkillRankCommandV2` currently accepts `playerLevel` from the caller and uses it as the point budget. A production view should not be able to provide progression truth.

The production application boundary should read the authoritative XP state itself and pass either:

- `TotalSkillPointsAwarded`; or
- a trusted progression-budget object derived from the active character graph.

Using awarded points instead of level also preserves flexibility if point cadence changes later.

### 6. Skill effects need one canonical runtime composition path

The V2 sample catalog emits effect identifiers such as `character.armor` and `movement.speed`, while the shared derived-stat system uses identifiers such as `combat.armor` and `combat.movement-speed`.

The ranked-skill projector also has its own `Apply(...)` stacking implementation. Production systems should not each apply skill effects independently.

The sustainable route is:

```text
RankedSkillAllocationSnapshotV2
-> skill-to-runtime-modifier projection
-> DerivedStatModifierSourceV1 with Skills priority
-> canonical derived-stat composition
-> player movement/health/weapon/reward consumers
```

This preserves one stacking authority and allows equipment, augments, skills, account bonuses, events, and run conditions to combine deterministically.

### 7. Active abilities remain a separate missing system

The product-vision document defines active, passive, and keystone skills and proposes a limited number of equipped active slots. The repository does not yet contain a complete production authority for:

- active ability ownership/unlock projection;
- equipped ability slots;
- input binding;
- cooldowns or charges;
- activation validation;
- timed state and cancellation;
- active-skill loadout persistence.

Active abilities should not be implemented as one-off branches inside the Skills screen or player controller.

## Sustainability assessment

### Strong foundations

- canonical enemy death and room lifecycle;
- exactly-once XP authority;
- account-backed per-character persistence;
- ranked skill definitions, allocation, migration, and respec seams;
- derived-stat composition foundation;
- stable identities and deterministic fingerprints.

### Current risks

- production uses disconnected authorities;
- V1 and V2 skill paths can drift;
- caller-supplied point budget can bypass progression truth;
- V2 replay receipts do not appear in the inspected persisted snapshot contract;
- skill effect IDs and stacking do not yet align with derived stats;
- no end-to-end test proves kill -> XP -> level -> allocation -> gameplay -> restart.

### Verdict

The repository is **architecture-sustainable but not yet feature-sustainable** for progression content.

It is safe to connect the existing islands. It is not yet safe to author a large final skill catalog or several active abilities, because every new skill would otherwise require ad hoc runtime wiring.

## Five-step future plan

### Step 1 — Connect canonical enemy death to persisted XP

Create a production `IEnemyExperienceFactConsumerV1` adapter that:

1. consumes the canonical accepted enemy death fact;
2. validates the run participant/killer policy against the active selected character;
3. resolves XP through a stable XP reward profile and enemy level;
4. derives a deterministic operation identity from stable run/death/enemy facts;
5. grants through the active graph's existing `PlayerExperienceAuthorityV1`;
6. exposes the returned XP and ordered level-up facts to presentation;
7. persists the active character after an applied mutation.

Do not place XP logic inside projectile, enemy-health, or room-clear code.

**Acceptance:** killing a production enemy visibly advances XP exactly once and survives restart.

### Step 2 — Replace the disconnected production Skills route with V2

Build a V2 Skills application/presentation session over the active character graph.

It should:

- read total awarded points from XP;
- read the current V2 allocation snapshot;
- project ranks, caps, prerequisites, gates, spent points, and available points;
- construct allocation commands internally;
- persist accepted allocation;
- reject missing active graph rather than inventing preview state.

V2 becomes the only production mutation route.

**Acceptance:** the normal Hub -> Skills route spends a real persisted point and restores the rank after restart.

### Step 3 — Make one passive skill affect real gameplay

Use one simple, measurable passive as the first proof. Recommended: movement speed.

Add a skill-to-derived-stat adapter, align stable target IDs, and make the real player movement runtime consume the resulting derived movement speed.

**Acceptance:** before/after allocation movement is measurably different, remains correct through scene changes, and restores after restart.

### Step 4 — Add level-up presentation and define milestone rewards

Consume the authoritative ordered `PlayerLevelUpFactV1` batch for:

- `+XP` feedback;
- XP bar update;
- level-up notification;
- newly available skill-point notification.

Define secondary milestone rewards separately from XP. A future level-reward authority may grant money, scrap, strongboxes, slots, or class unlocks through their canonical authorities with its own exactly-once receipts.

Do not make XP directly mutate wallets and inventory.

**Acceptance:** multi-level grants show ordered results and duplicate/replayed XP grants do not replay rewards or presentation side effects.

### Step 5 — Add the active-ability runtime, then expand content

After V2 allocation and passive effect consumption are proven, add a reusable active-ability system with:

- stable ability definitions;
- unlock projection from ranked skills;
- equipped active slots;
- cooldown/charge authority;
- activation commands and immutable results;
- transient run state;
- permanent loadout persistence;
- captured activation parameters for already-running timed effects.

Only then expand toward the approximately twenty-node class boards and class keystones described in `docs/product-vision/future-systems/SKILLS_AND_CLASSES.md`.

**Acceptance:** one equipped active ability can be unlocked, equipped, activated, cooled down, persisted, and reconciled after respec.

## Parallel work recommendation

### Safe immediately

Two implementation lanes can proceed in parallel with clear path ownership:

| Lane | Work | Main ownership |
|---|---|---|
| A | Step 1: death -> XP -> persistence | enemy reward adapter, reward composition, XP presentation handoff |
| B | Step 2: V2 Skills production route | Skills application/presentation, Skills UI, production navigation binding |

A third lane can work on **design/content only** without mutating production runtime:

- confirm level-1 point semantics;
- define the first small V2 catalog;
- finalize skill names/descriptions/icons;
- design XP/level-up feedback;
- decide milestone reward policy.

### After Steps 1 and 2 merge

Up to three implementation lanes are reasonable:

1. Step 3 passive effect/derived-stat integration;
2. Step 4 XP and level-up presentation;
3. active-ability contracts and content schema preparation for Step 5.

The full active runtime should still wait until the V2 production allocation route and canonical effect vocabulary are stable.

### Recommended concurrency limit

For the progression area:

- **Now:** 2 code PRs + 1 design/content PR.
- **After the two integration seams merge:** up to 3 code PRs.
- **Avoid more than 3 simultaneous code lanes** unless the shared composition work is centralized in one integration branch.

Other audits may reduce this number when they touch the same shared files.

## Cross-audit integration hotspots

These files/modules should have one active owner at a time across all feature audits:

- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomEnemySpawner2D.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterRuntimeGraphV1.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionFlowCoordinatorV1.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterAccountCompositionV1.cs`
- save component definitions/codecs/adapters under `Application/Persistence/Components`
- shared derived-stat targets and composition under `Domain/Characters`

Leaf work can remain parallel when it owns disjoint paths, but each shared composition hotspot should be changed by one designated integration PR rather than merged independently by several feature branches.

## Coordination rule for the other audits

Classify every proposed task as one of:

1. **Leaf foundation/content:** safe to parallelize when paths are disjoint.
2. **Feature adapter/UI:** usually parallelizable with explicit contracts and file ownership.
3. **Production composition/persistence:** serialize through one integration owner.

The practical project-wide capacity is therefore not simply the number of features. It is:

```text
several leaf feature PRs
+ one controlled production-composition lane
+ one optional UI/content lane
```

When two audited features both need the same production composition or save files, their foundations may proceed in parallel, but their final integrations should be sequenced or combined under one owner.

## Required end-to-end proof

The progression feature should not be called complete until one testable route proves:

```text
enter authored combat room
-> kill real enemy
-> receive visible XP
-> cross level threshold
-> receive one available point
-> return to Hub
-> open connected Skills screen
-> spend persisted V2 point
-> return to gameplay
-> observe real passive effect
-> restart application
-> retain XP, level, allocation, and effect
```

Required failure cases include duplicate death delivery, terminal-transition retry, missing XP profile, wrong killer participant, multi-level grant, failed durable save, stale skill version, removed skill migration, and unknown derived-stat target.

## Recommended next ticket

`PROGRESSION-LIVE-001: Connect canonical enemy death to persisted XP, connect the production Skills screen to ranked skills V2, and prove one movement-speed passive through restart.`

This ticket should be split into the two initial parallel lanes described above, with a small integration follow-up after both are merged.
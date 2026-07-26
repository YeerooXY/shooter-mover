# Next visible delivery workflow

## Purpose

This document converts the near-term product roadmap into small implementation tasks that always end in a visible, launchable improvement.

The planning rule is:

> One large product outcome becomes up to four small parallel branches, followed by one integration branch that proves the complete visible result.

This is a workflow plan, not authorisation to implement every milestone at once. Each branch still requires its own bounded prompt.

## Core delivery rules

### Every merged implementation PR must be visible

Every task must end with a sentence in this form:

> After this change, I can launch the game and visibly do X.

A supporting change that is not independently visible should normally target an integration branch rather than `main`.

### Keep each branch narrow

A small task should normally own one player-facing behaviour and one primary subsystem.

A task must not quietly expand into:

- unrelated refactoring;
- additional content families;
- balancing the full catalogue;
- replacing existing authorities;
- adding future systems because a convenient hook exists;
- redesigning UI outside the visible path;
- building a generic framework without an immediate consumer.

### Parallel branches target an integration branch

For a four-way split:

```text
latest main
-> create one milestone integration branch
-> create A/B/C/D task branches from the exact same integration SHA
-> each small PR targets the integration branch
-> merge and validate the four small PRs there
-> open one final integration PR to main
```

Do not open four overlapping PRs directly against `main` and hope the conflicts sort themselves out.

### Shared-contract rule

Before parallel work starts, the milestone owner identifies any small shared boundary the branches must agree on.

Examples:

- player combat target identity;
- damage command/result shape;
- strongbox drop presentation request;
- run-local reward manifest entry;
- mission completion fact.

If the repository already has the needed contract, reuse it. If a missing boundary is unavoidable, add the smallest version in the integration branch before creating A/B/C/D.

Parallel branches must not independently invent competing versions of the same shared contract.

### Suggested commit shape inside each small PR

A small PR should normally contain no more than three logical commits:

1. production behaviour;
2. focused test or validation proof;
3. verification note or necessary scene/prefab wiring.

Fewer commits are fine. Do not split by file merely to increase commit count.

### Validation honesty

Every PR description must distinguish:

- validation performed;
- validation not performed;
- visible manual result;
- known limitations.

Never claim a Unity compile, test run or manual play-through that was not actually completed.

---

# Milestone 1 — Playable authored level

## Big outcome

```text
Character Select
-> Hub
-> Level Select
-> authored JSON level
-> selected character spawns
-> movement and following camera
-> authored exit
-> Hub with the same exact loadout
```

## Task

Use the already prepared task:

```text
PLAYABLE-LEVEL-BOOT-001
branch: agent/playable-level-boot-001
```

## Scope

This remains one focused PR because level selection, character handoff, authored spawn, movement, camera and exit form one thin traversal spine. Splitting these before a gameplay scene exists would create several non-visible branches and excessive scene conflicts.

## Explicitly out of scope

- weapon input;
- enemy AI;
- damage and health;
- player death;
- strongbox drops;
- mission rewards;
- inventory changes.

## Visible completion statement

> After this change, I can select a character, enter the first authored JSON level, move with the camera following, reach the authored exit and return to the Hub with the same exact loadout.

## What comes immediately after

Create the first-combat integration branch from the exact merged `main` SHA.

---

# Milestone 2 — First complete combat room

## Big outcome

```text
enter authored level
-> exact equipped weapon fires
-> one authored Mobile Blaster Droid moves and attacks
-> player and enemy can take damage
-> enemy dies
-> room becomes clear
-> authored exit unlocks
-> return to Hub
```

## Integration branch

```text
agent/first-combat-room-001-integration
```

Create it from `main` only after `PLAYABLE-LEVEL-BOOT-001` is merged.

## Parallel task A — Exact player weapon firing

```text
Task: PLAYER-WEAPON-LIVE-001
Branch: agent/player-weapon-live-001
Target: agent/first-combat-room-001-integration
```

### Visible result

The selected character's first equipped exact weapon visibly aims and fires in the authored level, and its projectile can damage a bound room enemy.

### Exact requirements

- Resolve the selected character's existing exact equipment-instance binding.
- Resolve that instance through the canonical equipment and weapon catalogues.
- Connect one existing input action to aiming/firing.
- Use the existing inventory-backed live weapon scheduler/execution path.
- Show a visible projectile or effect for the first simple weapon family.
- Apply canonical damage to `RoomEnemyActor2D` or the retained generic enemy-damage boundary.
- Prevent duplicate firing caused by duplicate scene bindings.
- Re-entering the level must not create a second holdings/loadout authority.

### Scope limit

Prove one straightforward projectile weapon, preferably the equipped Rattler MK1 path. Do not make all eighteen weapons visually complete in this task.

### Out of scope

- enemy movement or attacks;
- player health;
- weapon switching UI;
- augments;
- ammunition economy redesign;
- complete weapon VFX/audio catalogue.

### Acceptance

> After this change, I can enter the level and visibly fire my selected character's exact equipped weapon into a room enemy.

## Parallel task B — Mobile Blaster Droid behaviour

```text
Task: ENEMY-BLASTER-LIVE-001
Branch: agent/enemy-blaster-live-001
Target: agent/first-combat-room-001-integration
```

### Visible result

One authored Mobile Blaster Droid detects the player target, repositions and visibly fires a readable projectile attack.

### Exact requirements

- Use the existing authored enemy definition and room placement.
- Bind behaviour to the already factory-created enemy runtime.
- Use the authored movement, decision and attack capability identities.
- Acquire the current player through an explicit gameplay target registration.
- Maintain a preferred ranged distance or use the simplest authored positioning policy.
- Telegraph the shot before the projectile becomes dangerous.
- Use authored cadence/projectile/damage identities rather than enemy-name switches.
- Stop movement and attacks when the bound enemy becomes terminal.
- A room presentation rebuild must not duplicate behaviour or attacks.

### Scope limit

Implement only the Mobile Blaster Droid's first ranged pattern. Do not add the Pouncer, Turret, Pursuer Drone or Sentinel here.

### Out of scope

- player death handling;
- elite modifiers;
- pathfinding framework replacement;
- status effects;
- drops or XP.

### Acceptance

> After this change, I can enter the room and visibly see the authored Mobile Blaster Droid reposition and fire at the registered player target.

## Parallel task C — Player vitals and one safe failure path

```text
Task: PLAYER-VITALS-LIVE-001
Branch: agent/player-vitals-live-001
Target: agent/first-combat-room-001-integration
```

### Visible result

The player has visible health, reacts to accepted damage and follows one safe defeat path.

### Exact requirements

- Create or reuse one authoritative player-health boundary.
- Register the gameplay player as a valid combat target with stable runtime identity.
- Accept canonical damage commands exactly once.
- Show a minimal readable health indicator.
- Show visible hit feedback without obscuring gameplay.
- At zero health, disable gameplay input and weapon firing.
- Use one deliberately simple failure behaviour:
  - return to Hub with a clear failure message; or
  - restart at the authored spawn with a fresh run-local health state.
- Preserve character holdings, exact loadout, money and progression through failure.
- Repeated lethal collision facts must not trigger multiple transitions.

### Scope limit

Choose one failure path only. Lives, checkpoints and multiplayer revive come later.

### Out of scope

- armour;
- healing skills;
- lives/revive economy;
- death penalties;
- detailed results statistics.

### Acceptance

> After this change, I can take visible damage, reach zero health and enter one safe, non-duplicated failure path without losing my character or loadout.

## Parallel task D — Room clear and exit gate

```text
Task: ROOM-CLEAR-EXIT-LIVE-001
Branch: agent/room-clear-exit-live-001
Target: agent/first-combat-room-001-integration
```

### Visible result

The authored exit starts locked while a live room enemy remains and becomes usable exactly once after the enemy is terminal.

### Exact requirements

- Derive room-clear truth from bound enemy lifecycle/terminal facts.
- Do not poll GameObjects by enemy name.
- Lock or visibly disable the authored exit while required occupants remain alive.
- Unlock the exit immediately after the last required enemy becomes terminal.
- Show a minimal visible locked/unlocked state.
- Prevent duplicate completion/scene-transition requests.
- Preserve the ordinary traversal exit behaviour when a room has no required enemies.
- Rebuilding the room presentation must not lose or duplicate clear-state subscriptions.

### Scope limit

This task gates the existing authored exit only. It does not add rewards, objectives or a full mission-state machine.

### Acceptance

> After this change, I can see that the exit is blocked during combat and becomes available once the room's required enemy is defeated.

## Combat integration PR

```text
Task: FIRST-COMBAT-ROOM-001
Branch: agent/first-combat-room-001-integration
Target: main
```

### Integration responsibilities

- Merge A/B/C/D in the order that minimises shared-contract conflicts.
- Connect enemy projectiles to player damage.
- Connect player projectiles to canonical enemy damage.
- Confirm player defeat and enemy terminal state cannot race into duplicate transitions.
- Validate the exact selected weapon remains unchanged after success and failure.
- Remove temporary test targets or direct debug triggers used by the small branches.
- Perform the complete visible play-through.

### Full acceptance

> After this change, I can enter the authored level, fight one Mobile Blaster Droid with my exact equipped weapon, take damage, defeat it, see the exit unlock and return safely to the Hub.

### Not included

- XP;
- loot;
- strongboxes;
- multiple enemy archetypes;
- weapon switching;
- class skills.

---

# Milestone 3 — Earn and collect one strongbox

## Big outcome

```text
kill authored enemy
-> guaranteed Tier 1 strongbox appears
-> player collects it
-> exit becomes available
-> mission completes
-> exact unopened box belongs to selected character
-> restart preserves it
```

## Integration branch

```text
agent/mission-strongbox-loop-001-integration
```

Create it only after `FIRST-COMBAT-ROOM-001` is merged.

Before creating parallel branches, agree on one shared representation for:

```text
accepted enemy death
-> exact strongbox drop request
-> physical pickup identity
-> run-local collected reward entry
-> durable completion commit
```

## Parallel task A — Deterministic enemy drop

```text
Task: ENEMY-STRONGBOX-DROP-001
Branch: agent/enemy-strongbox-drop-001
Target: agent/mission-strongbox-loop-001-integration
```

### Visible result

The first accepted death of the authored test enemy produces one guaranteed Tier 1 strongbox drop request and visible drop presentation.

### Exact requirements

- Trigger from accepted canonical enemy death, not GameObject destruction alone.
- Resolve a registered drop source/profile.
- Create one exact `StrongboxInstance` with stable identity.
- Use a guaranteed Tier 1 result for this integration milestone.
- Replayed or duplicate death facts must not create another box.
- A no-drop profile must remain representable for future enemies.
- The drop appears at the enemy's terminal position or an authored safe offset.

### Scope limit

One guaranteed box from one enemy. Random probabilities and multiple drops come later.

### Acceptance

> After this change, defeating the test enemy visibly produces exactly one Tier 1 strongbox pickup.

## Parallel task B — Physical strongbox pickup

```text
Task: STRONGBOX-PICKUP-LIVE-001
Branch: agent/strongbox-pickup-live-001
Target: agent/mission-strongbox-loop-001-integration
```

### Visible result

The player can collect an exact strongbox pickup once and see immediate pickup feedback.

### Exact requirements

- Bind the physical presentation to one exact strongbox-instance identity.
- Collect through a generic pickup boundary.
- Disable/remove the pickup only after accepted collection.
- Duplicate collision or interaction cannot collect twice.
- Show a minimal visible/audio pickup acknowledgement.
- Collection adds to run-local rewards, not directly to durable character holdings.
- A room rebuild must not respawn an already collected run-local pickup.

### Scope limit

Strongboxes only. Do not add money, scrap, healing or generic item-pickup catalogues unless an existing generic path already supports them.

### Acceptance

> After this change, I can walk over or interact with the dropped strongbox and visibly collect that exact pickup once.

## Parallel task C — Run-local reward manifest and durable commit

```text
Task: RUN-REWARD-COMMIT-001
Branch: agent/run-reward-commit-001
Target: agent/mission-strongbox-loop-001-integration
```

### Visible result

A collected box remains run-local until successful completion, then becomes an unopened box owned by the selected character.

### Exact requirements

- Maintain a run-local collected-reward manifest.
- Record exact box identity and provenance.
- Commit collected boxes only on accepted mission success.
- Use the existing character holdings authority.
- Persist character/account changes atomically through the existing composition boundary.
- Repeated success requests return no change rather than duplicating the box.
- Failure before success follows one explicit rule, initially discarding uncommitted run rewards.
- Save failure cannot leave an ambiguous half-committed state.

### Scope limit

Strongboxes only. No XP, money, scrap or equipment grant in this task.

### Acceptance

> After this change, a collected box becomes durable only after successful mission completion and remains present after restart.

## Parallel task D — Minimal mission results

```text
Task: MISSION-RESULTS-BOX-001
Branch: agent/mission-results-box-001
Target: agent/mission-strongbox-loop-001-integration
```

### Visible result

Successful exit shows a small results view containing the collected strongbox before returning to the Hub.

### Exact requirements

- Show mission success/failure state.
- Show zero or one collected strongbox for this milestone.
- Display the box tier and exact reward identity/fingerprint where appropriate for diagnostics.
- Confirm durable reward commit before presenting success as final.
- Provide one Continue/Return to Hub action.
- Prevent duplicate button input from repeating reward commit or scene transition.
- Reuse existing results UI concepts where retained; do not rebuild the full raid statistics screen.

### Scope limit

No kill count, XP summary, money animation, multiplayer participant table or item reveal.

### Acceptance

> After this change, successful completion visibly shows the box I collected and then returns me to the Hub.

## Strongbox integration PR

```text
Task: MISSION-STRONGBOX-LOOP-001
Branch: agent/mission-strongbox-loop-001-integration
Target: main
```

### Full acceptance

> After this change, I can defeat the test enemy, collect its guaranteed Tier 1 strongbox, complete the mission, see it in Results, return to the Hub and still own the same unopened exact box after restart.

### Not included

- opening the box;
- Keep/Sell;
- equipment reveal;
- random drop probability;
- XP or money rewards.

---

# Milestone 4 — Open the box and make the first loot decision

## Big outcome

```text
Hub strongbox screen
-> open exact unopened box
-> reveal exact weapon
-> Keep or Sell
-> exactly one decision persists
-> inventory or money reflects the decision
```

## Integration branch

```text
agent/loot-disposition-001-integration
```

Create it only after `MISSION-STRONGBOX-LOOP-001` is merged.

## Parallel task A — Exact strongbox opening

```text
Task: STRONGBOX-OPEN-EXACT-001
Branch: agent/strongbox-open-exact-001
Target: agent/loot-disposition-001-integration
```

Requirements:

- list selected character's unopened exact boxes;
- open one exact box through the existing opening authority;
- commit one deterministic equipment result;
- closing/reopening cannot reroll;
- duplicate open input cannot grant twice;
- remove or mark only the exact opened box;
- persist the resulting exact equipment instance.

Visible acceptance:

> After this change, I can choose one unopened box and reveal the same exact weapon result every time I revisit its committed opening.

## Parallel task B — Reveal presentation

```text
Task: LOOT-REVEAL-LIVE-001
Branch: agent/loot-reveal-live-001
Target: agent/loot-disposition-001-integration
```

Requirements:

- present weapon name, family/mark, rarity/quality and key authored behaviour;
- show exact item identity in diagnostics, not as noisy normal UI;
- offer Keep and Sell actions;
- disable actions while a transaction is in progress;
- do not invent a reroll button;
- reuse weapon art/presentation registration where available.

Visible acceptance:

> After this change, opening a box visibly reveals a concrete weapon and presents clear Keep and Sell choices.

## Parallel task C — Exactly-once Keep/Sell

```text
Task: KEEP-SELL-TRANSACTION-001
Branch: agent/keep-sell-transaction-001
Target: agent/loot-disposition-001-integration
```

Requirements:

- Keep records a terminal no-op disposition receipt while leaving the exact item owned;
- Sell removes only the revealed exact equipment instance;
- Sell credits one exact, previewed money value;
- holdings and wallet changes succeed atomically;
- repeated same decision returns no change;
- conflicting second decision rejects;
- save failure cannot remove the item without payment;
- closing/reopening cannot sell twice.

Visible acceptance:

> After this change, I can Keep the revealed exact item or Sell it once for a visible money increase, with no duplicate outcome.

## Parallel task D — Owned weapon list

```text
Task: INVENTORY-OWNED-WEAPONS-001
Branch: agent/inventory-owned-weapons-001
Target: agent/loot-disposition-001-integration
```

Requirements:

- list selected character's owned exact weapons;
- show definition name, mark, quality and equipped/not-equipped state;
- refresh after Keep or Sell;
- never show another character's holdings;
- handle empty inventory and deleted/sold exact instances safely;
- no equip interaction yet beyond existing retained behaviour.

Visible acceptance:

> After this change, I can open Inventory and visibly confirm that a kept weapon exists or a sold weapon is absent.

## Disposition integration PR

```text
Task: LOOT-DISPOSITION-001
Branch: agent/loot-disposition-001-integration
Target: main
```

### Full acceptance

> After this change, I can open the earned box, reveal one exact weapon, choose Keep or Sell, then confirm the durable result in Inventory or my money balance after restart.

### Not included

- dismantling;
- crafting;
- augments;
- shop rotations;
- equipping the newly kept item;
- opening multiple boxes in one batch.

---

# Milestone 5 — Equip the kept weapon and replay

## Big outcome

```text
owned exact weapon
-> select available mount
-> equip/replace/swap
-> save
-> re-enter level
-> fire that exact weapon
```

This milestone should again be split around an integration branch:

```text
agent/inventory-loadout-loop-001-integration
```

Recommended small tasks:

| Task | Visible responsibility |
|---|---|
| `INVENTORY-WEAPON-DETAILS-001` | inspect exact weapon details and compatibility |
| `LOADOUT-EQUIP-REPLACE-001` | equip, unequip where allowed and replace one occupied slot |
| `LOADOUT-SWAP-PERSIST-001` | swap available physical mounts and preserve exact bindings after restart |
| `GAMEPLAY-EXACT-WEAPON-REENTRY-001` | re-enter and visibly fire the newly equipped exact instance |

Do not add armour, presets or class skills to this milestone.

## Full acceptance

> After this change, I can keep a box weapon, equip that exact instance, restart, re-enter the level and visibly fire it.

---

# Milestone 6 — First repeatable end-to-end game loop

## Final integration script

1. Select a Striker character.
2. Enter the authored JSON level.
3. Fire the exact equipped starter weapon.
4. Defeat the Mobile Blaster Droid.
5. Collect its guaranteed Tier 1 strongbox.
6. Complete the mission.
7. See the box in Results.
8. Return to the Hub.
9. Open the exact box.
10. Keep the weapon.
11. Equip it in the second available mount.
12. Re-enter the level.
13. Fire the newly acquired exact weapon.
14. Complete another run.
15. Open another box and choose Sell.
16. Confirm money increases and the sold item is absent.
17. Restart the application.
18. Confirm character, holdings, unopened/opened box state, loadout and money all remain correct.

## Completion statement

> After this change, the game has one complete, visible and repeatable combat-to-loot-to-loadout loop.

This integration should primarily test and repair seams. It must not expand into a new feature milestone.

---

# What comes after the repeatable loop

Once Milestone 6 is proven, choose the next visible feature from the design-memory documents. The recommended order is:

1. second authored level, proving level-content extensibility;
2. second enemy archetype, proving enemy extensibility;
3. player lives/respawn and richer failure handling;
4. XP display and level-up;
5. first small class-skill slice;
6. dismantling and basic crafting;
7. first augment installation;
8. four-slot armour;
9. deterministic rotating shop;
10. first endless survival mode;
11. multiplayer/co-op foundation only after the single-player authorities and results loop are reliable.

Each of these must receive a new bounded workflow before implementation. Do not implement the full corresponding design-memory document in one task.

---

# Parallel-execution safety checklist

Before launching four branches, confirm:

- [ ] the prerequisite visible milestone is already merged;
- [ ] all branches start from the same integration SHA;
- [ ] each branch has one subsystem owner;
- [ ] shared contracts are frozen or clearly owned by the integration branch;
- [ ] scene/prefab ownership is assigned to avoid four branches editing the same YAML;
- [ ] each task has a visible acceptance sentence;
- [ ] each task has explicit exclusions;
- [ ] no task creates a duplicate authority or content catalogue;
- [ ] small PRs target the integration branch, not `main`;
- [ ] the integration PR performs the full launch-to-visible-result play-through;
- [ ] temporary fixtures and debug shortcuts are removed before merging to `main`;
- [ ] validation evidence names the exact tested commit SHA.

## Scene and prefab conflict rule

When several branches need gameplay presentation changes, assign one branch as the scene/prefab wiring owner. Other branches should provide reusable components, prefabs or registration assets without editing the shared scene when possible.

For the first combat milestone, `ROOM-CLEAR-EXIT-LIVE-001` is the preferred gameplay-scene wiring owner. The other branches should minimise direct edits to the production gameplay scene.

For the strongbox milestone, `MISSION-RESULTS-BOX-001` owns the Results scene, while `STRONGBOX-PICKUP-LIVE-001` owns the pickup prefab/presentation. Neither should rewrite the gameplay scene unless integration requires a small explicit hook.

## Stop rule

If one small task grows until it needs several unrelated authorities, multiple scenes and a broad content migration, stop and split it again before implementation continues.

The purpose of this workflow is not maximum parallelism. It is **safe parallelism that keeps every merge understandable and every final milestone immediately visible in the game**.

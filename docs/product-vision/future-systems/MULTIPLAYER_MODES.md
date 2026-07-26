# Multiplayer and co-op mode ideas

## Status

Product-planning document only. This file describes desired player-facing modes and fairness rules, not networking architecture.

## Confirmed direction

- The game should support meaningful multiplayer possibilities rather than treating multiplayer as a simple extra player object.
- Character identity, holdings and exact loadouts remain player-specific.
- Raid/results presentation should show participants, kills, XP and space for earned strongboxes or loot.
- Cooperative play is the primary multiplayer direction; competitive modes are later possibilities.
- Rewards must be exactly-once and must not duplicate because a player disconnects, reconnects or changes scenes.

## Core multiplayer principles

- One player cannot equip or sell another player's exact item.
- Personal rewards are resolved per player unless a reward is explicitly shared.
- Shared encounter state and personal account state remain separate.
- Party-size scaling should change encounter composition intelligently, not only multiply health.
- A player joining or leaving must not corrupt room-clear, boss, reward or mission-result state.
- The game should remain enjoyable solo; co-op must not be mandatory for ordinary progression.

## Mode 1 — Campaign co-op

### Working identity

One to four players enter ordinary authored levels together with their persistent characters.

### Player experience

```text
create/join lobby
-> select character and exact loadout
-> choose level and difficulty
-> load shared mission
-> fight and complete objectives
-> resolve personal XP, money and loot
-> show group results
-> return to party Hub/lobby
```

### Key rules

- Each player brings a separate character-local loadout.
- Class combinations should create synergy without requiring a strict tank/healer/damage composition.
- Enemy count and role combinations scale with party size.
- Boss health may scale, but mechanics and telegraphs remain readable.
- Personal strongboxes prevent arguments over who receives a rare drop.
- Shared pickups, where used, must clearly state whether they affect the team or collector.

## Mode 2 — Raid missions

### Working identity

Longer, more difficult cooperative missions with several encounters, stronger bosses and a substantial completion reward.

### Presentation memory

The raid statistics screen should support:

- participant list;
- kills or combat contribution;
- XP earned;
- mission outcome;
- room for multiple earned loot boxes;
- return to the main menu/Hub.

### Working structure

- 3–4 players preferred, with solo/duo scaling considered later.
- Multiple encounter stages or rooms.
- Limited recovery/revive resources.
- At least one mechanic requiring coordination, not merely combined damage.
- Personal exact loot package on completion.
- First-clear and challenge rewards granted once.

## Mode 3 — Co-op survival

Players defend or survive escalating waves in one or several authored arenas.

- Short sessions can cash out at milestones.
- Longer survival improves reward quality.
- Team composition matters through movement, healing, barriers and area control.
- Disconnect/rejoin rules preserve the player's earned milestone state without duplicating rewards.

This mode overlaps with the endless-mode document but belongs here as the multiplayer-facing version.

## Mode 4 — Boss hunt

A short cooperative mode focused on one boss or a sequence of boss phases.

- Minimal filler enemies.
- Clear difficulty selection.
- Fixed or rotating boss challenge modifiers.
- Good source for capstone blueprints, cosmetics or targeted materials.
- Personal rewards based on completion, not damage ranking alone.

## Mode 5 — Challenge operations

A rotating set of authored levels with fixed modifiers, seeds or loadout constraints.

Examples:

- only selected weapon categories receive a bonus;
- environmental hazards are active;
- elite enemies use a named modifier set;
- limited healing;
- fixed difficulty and leaderboard seed.

Constraints should encourage experimentation without deleting the value of a player's collection.

## Later competitive possibilities

Competitive modes are explicitly secondary to co-op and may require power normalisation.

### Arena PvP

Small teams fight in an authored arena with normalised or mode-specific equipment values.

Potential formats:

- 2v2 or 3v3 elimination;
- score-based respawn arena;
- objective capture;
- payload or control-point mode.

Persistent loot should not create an unwinnable gear gap. Options include normalised base stats, curated PvP loadouts or separate competitive modifiers.

### PvPvE extraction race

Teams enter a hostile level, fight enemies and compete to extract objectives or resources.

This is high risk for scope and fairness. It should not be attempted before co-op, rewards, reconnection and authoritative combat are mature.

## Downed, revive and failure proposal

### Working rules

- A player reaching zero health enters a bounded downed state rather than immediately disappearing.
- Teammates can revive within a visible interaction window.
- Combat Medic skills improve revive/support but are not required.
- Repeated downs create increasing pressure or consume a limited team resource.
- A fully defeated player may spectate until checkpoint, room completion or mission end depending on mode.
- Solo uses a separate life/revive rule and does not pretend another player is present.

## Matchmaking ideas

Players may filter or be matched by:

- region/latency;
- mode;
- difficulty;
- level band;
- public/private/friends-only;
- voice preference;
- in-progress joining allowed or disabled.

Class composition should be displayed but should not hard-lock ordinary matches to one of each class.

## Loot ownership

### Preferred direction

- Personal enemy/mission rewards are generated per eligible participant.
- Physical pickups may be visible only to the owning player or collected into a player-specific run inventory.
- Shared objective rewards create separate exact reward receipts for each eligible player.
- One player's Keep/Sell/Dismantle decision never changes another player's reward.
- Raid statistics may display earned boxes without exposing private item details unless the player chooses to reveal them.

## Communication and cooperation

Initial useful tools:

- ping location;
- ping enemy/priority target;
- ping pickup or exit;
- ready status;
- simple emotes;
- revive/help request.

These provide practical coordination before full voice or text systems are considered.

## Host and authority product requirements

Technical design is deferred, but the player-facing rules require:

- no host-only reward advantage;
- deterministic handling of host migration or a clear safe failure policy;
- no duplicated mission rewards after reconnect;
- consistent enemy lifecycle and room-clear truth;
- explicit handling of players joining during a mission;
- results that survive temporary connection loss.

## Open decisions

- Maximum party size; one to four is the current working target.
- Whether campaign progress advances for all eligible players or only the lobby owner.
- Whether players may join missions already in progress.
- Exact revive/life rules per mode.
- Whether physical loot is personal, shared or hybrid.
- Whether raids require fixed roles or only recommended composition.
- Whether PvP belongs in the same executable/progression ecosystem.
- How much equipment power is normalised in competitive modes.
- Whether cross-play and dedicated servers are realistic project goals.
- How leaderboards handle disconnects, substitutions and party-size differences.

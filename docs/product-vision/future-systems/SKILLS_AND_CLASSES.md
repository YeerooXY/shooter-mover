# Skills and class identity

## Status

Product-planning document only. No implementation is authorised by this file.

## Confirmed direction

The game currently revolves around three distinct playstyle identities:

| Class direction | Combat identity | Baseline weapon mounts |
|---|---|---:|
| Striker / Assault | aggressive movement and weapon pressure | 2 |
| Combat Medic / Healer | support, recovery and flexible positioning | 3 |
| Juggernaut / Defensive | durability, control and sustained heavy fire | 4 |

The skill screen should feel like a substantial progression board rather than a tiny list. The current UI concept reserves roughly twenty skill rectangles/nodes per character direction.

Skills must deepen a class identity without turning weapons, armour and augments into irrelevant decoration.

## Working system proposal

Each class has three kinds of progression nodes:

1. **Active skills** — player-triggered abilities with cooldowns or charges.
2. **Passive skills** — permanent statistical or mechanical improvements.
3. **Keystone skills** — build-defining nodes that change how the class plays.

A character may unlock many passive nodes but equips only a small number of active abilities at once. A working target is two active ability slots plus one class utility slot, but this is not locked.

The board can contain about twenty nodes while still launching with a much smaller implemented subset. Empty future branches should not be shown as purchasable placeholders.

## Striker / Assault ideas

### Role

Fast pressure, target deletion, repositioning and temporary offensive spikes. The Striker starts with fewer weapon mounts so that mobility and skill execution remain meaningful.

### Concrete skill ideas

| Skill | Type | Intended behaviour |
|---|---|---|
| Combat Dash | Active | A short directional burst that breaks ordinary movement limits without granting long invulnerability. |
| Overdrive | Active | Temporarily improves weapon cadence and handling; should not bypass weapon-specific safety limits. |
| Suppression Field | Active | Marks or slows enemies inside a forward area while the Striker keeps firing. |
| Adrenaline Loop | Passive | Successful kills or critical actions briefly improve movement speed, with a refresh cap. |
| Hot Reload | Passive | Rewards sustained firing or clean reload timing where a weapon uses reload mechanics. |
| Execution Window | Passive | Improves damage against already weakened enemies rather than adding unconditional damage. |
| Evasive Plating | Passive | Grants a short defensive benefit after a successful dash or near-miss. |
| Auxiliary Hardpoint | Keystone | Unlocks a third configurable weapon mount for the Assault class. This is a major progression reward, not a default starting benefit. |
| Full Burn | Keystone | Converts a defensive resource into a short, dangerous offensive state with a clear downside. |

### Guardrails

- The third mount must use the same physical mount and exact-instance loadout system as every other weapon position.
- The skill must not create a hidden duplicate weapon or temporary fake equipment instance.
- Mobility must not allow escaping authored collision or bypassing level boundaries.

## Combat Medic / Healer ideas

### Role

Team sustain, emergency recovery, controlled support and reliable solo survivability without becoming passive or helpless.

### Concrete skill ideas

| Skill | Type | Intended behaviour |
|---|---|---|
| Healing Pulse | Active | Emits a short-range heal centred on the Medic, affecting self and allies with reduced repeated effectiveness. |
| Repair Drone | Active | Deploys a temporary support drone that follows or guards a target and provides periodic repair. |
| Triage Beam | Active | Channels focused healing or cleansing to one ally; movement or damage may interrupt it. |
| Nanite Reserve | Passive | Stores a limited amount of overhealing or converts excess healing into a temporary shield. |
| Field Pharmacology | Passive | Improves healing consumables and support pickups without multiplying all rewards. |
| Emergency Protocol | Passive | Automatically triggers a small recovery effect at low health with a long internal cooldown. |
| Shared Recovery | Passive | A portion of healing given to allies also restores the Medic. |
| Combat Revival | Keystone | Allows a downed ally to be revived faster or from limited range in multiplayer modes. |
| Mobile Clinic | Keystone | Changes the Repair Drone into a stronger stationary support zone with reduced mobility. |

### Guardrails

- Solo play must remain viable; support skills should have self-use cases.
- Healing cannot permanently erase difficulty through infinite safe loops.
- Revive behaviour belongs to multiplayer/downed-state rules and should not be implemented before those systems exist.

## Juggernaut / Defensive ideas

### Role

Durability, space control, threat management and the ability to keep several weapons active under pressure.

### Concrete skill ideas

| Skill | Type | Intended behaviour |
|---|---|---|
| Kinetic Barrier | Active | Projects a directional barrier that absorbs a limited amount of incoming damage. |
| Ground Slam | Active | Creates a close-range shockwave that interrupts or pushes lighter enemies. |
| Threat Beacon | Active | Draws nearby enemy attention in co-op and creates a defensive hold point. |
| Reinforced Frame | Passive | Increases effective durability with diminishing returns rather than flat immortality. |
| Stabilised Mounts | Passive | Reduces movement or recoil penalties while several weapons fire together. |
| Reactive Armour | Passive | Grants temporary resistance after receiving a large hit, with an internal cooldown. |
| Heavy Momentum | Passive | Rewards continuing to move toward danger rather than permanently camping. |
| Fortress Mode | Keystone | Trades movement speed for strong frontal defence and mount stability until cancelled. |
| Last Reactor | Keystone | Once per mission, prevents immediate defeat and grants a brief recovery window. |

### Guardrails

- Four mounts are already the class baseline; skills should not simply grant even more permanent weapon slots.
- Defence must involve positioning and timing, not only larger health numbers.
- Threat manipulation should degrade gracefully in solo play.

## Skill progression proposal

A possible board structure for each class:

- 4 core identity nodes;
- 6 passive branches;
- 4 active-skill unlocks or upgrades;
- 3 utility nodes;
- 2 keystones;
- 1 mastery capstone.

This totals twenty visible nodes while still allowing a staged release. Node costs, respec costs and level gates remain open.

## Respec philosophy

### Working proposal

- Respeccing should be available and understandable.
- Early experimentation should be cheap or free.
- Late repeated respecs may consume money, but should not require rare loot-box resources.
- A respec never changes owned weapons, equipment instances or character identity.

## Open decisions

- Exact number of equipped active abilities.
- Whether skill points are awarded every level or at milestone levels.
- Whether the level-65 launch cap exposes the full board or only its first major branches.
- Whether keystones are mutually exclusive.
- Whether skill loadouts can be saved as presets.
- Whether the Assault third mount is a permanent unlock, a loadout choice or a timed active state.
- How class skills scale in multiplayer without making solo and co-op balance separate games.

# Enemy archetypes and encounter ideas

## Status

Product-planning document only. Existing names are separated from future proposals so that content ideas are not mistaken for implemented runtime behaviour.

## Confirmed direction

- Enemies are authored content with stable identities, movement/decision/attack capabilities and presentation references.
- Adding a new enemy should primarily mean adding a definition, presentation registration and room placement—not adding enemy-name branches to room or combat controllers.
- Visual readability matters: dangerous actions need clear telegraphs, silhouettes and effects.
- Enemy variety should come from roles and combinations, not only from multiplying health and damage.

## Existing concrete baseline identities

The current catalogue/runtime work already establishes these useful archetype directions:

| Enemy direction | Intended role |
|---|---|
| Mobile Blaster Droid | basic mobile ranged pressure |
| Ram Pouncer | committed melee leap or charge |
| Blaster Turret | stationary ranged area denial |
| Pursuer Drone | fast pursuit and contact pressure |
| Hybrid Sentinel | mixed or multi-attack enemy |

These should form the first reusable encounter vocabulary before introducing a large catalogue.

## Core archetype proposals

### Mobile Blaster Droid

- Maintains a preferred firing distance.
- Repositions rather than running directly at the player.
- Fires a clearly visible projectile burst.
- Vulnerable while changing position or recovering from a burst.
- Good first enemy for proving target selection, cadence, projectiles, damage and death.

### Ram Pouncer

- Tracks briefly, locks its direction, telegraphs, then commits to a high-speed pounce.
- Cannot perfectly steer after commitment.
- Collision or a missed charge creates a recovery window.
- Strong against stationary players, weak against deliberate lateral movement.

### Blaster Turret

- Stationary and easy to understand spatially.
- Rotates aim, telegraphs a firing line or cone and launches a heavy projectile.
- May be armoured from one direction or require movement around cover.
- Useful for rooms where geometry is part of the challenge.

### Pursuer Drone

- Light, fast and fragile.
- Continuously pressures the player and punishes tunnel vision.
- May deal contact damage or make short lunges.
- Should be dangerous in groups but readable individually.

### Hybrid Sentinel

- Uses two clearly separated attack patterns, such as ranged burst plus close shockwave.
- Switches behaviour based on distance, health threshold or room state.
- Serves as an elite or late-level enemy, not an early baseline unit.

## Additional future enemy ideas

### Shield Carrier

- Projects a directional barrier protecting itself or nearby enemies.
- Encourages flanking, piercing, area damage or target priority.
- Barrier direction and break state must be visually obvious.

### Repair Drone

- Repairs a bounded amount of health or armour on nearby enemies.
- Weak alone but high priority in mixed groups.
- Healing cadence should be interruptible or limited to avoid endless stalemates.

### Mine Layer

- Moves between safe positions and leaves visible mines.
- Mines arm after a delay and can be destroyed.
- Creates changing room hazards without requiring procedural geometry.

### Artillery Walker

- Fires slow, clearly marked area attacks at predicted player positions.
- Weak at close range.
- Encourages movement and interrupts static defensive play.

### Arc Conductor

- Chains energy between nearby allied enemies or environmental nodes.
- Becomes more dangerous in tightly packed formations.
- Rewards separating or quickly eliminating link targets.

### Corrosion Sprayer

- Uses a short-range cone that applies armour reduction or damage-over-time.
- Forces players out of comfortable close-range positions.
- The persistent effect must have a strict duration and stack cap.

### Cloaked Stalker

- Becomes partially hidden while repositioning, but reveals before attacking.
- Never attacks from perfect invisibility without a telegraph.
- Sound, distortion or floor effects preserve fairness.

### Splitter Unit

- On death or at a threshold, divides into smaller weaker units.
- Child identities and rewards must be bounded to avoid duplicate-drop exploits.
- Useful for testing lifecycle generation and room-clear correctness.

### Suppression Platform

- Creates a sustained danger lane or rotating beam.
- Controls space rather than chasing the player.
- Works well with mobile enemies that push the player into the lane.

### Elite Commander

- Buffs nearby enemies through a visible aura or command pulse.
- Does not silently multiply every statistic.
- Killing it changes the encounter immediately and visibly.

## Boss-scale ideas

### Siege Walker

A large multi-part machine with directional armour, exposed components and alternating artillery/close-defence phases. Damageable parts may disable attacks, but the first implementation should avoid requiring a fully general detachable-part system.

### Core Reactor Guardian

A stationary or semi-mobile boss built around room hazards, rotating shields and timed vulnerability. It demonstrates that boss challenge can come from mechanics rather than an enormous health bar.

### Swarm Fabricator

Creates bounded waves of small drones while defending its production core. Spawn limits and reward ownership must prevent infinite farming.

## Encounter composition ideas

Enemy roles should combine intentionally:

- **Pouncer + Turret** — dodge the committed charge without entering the turret lane.
- **Shield Carrier + Blaster Droids** — flank or break the protector before cleaning up ranged enemies.
- **Repair Drone + Sentinel** — prioritise support while surviving mixed attacks.
- **Mine Layer + Pursuer Drones** — keep moving, but choose movement paths carefully.
- **Artillery Walker + Suppression Platform** — alternating forced movement and safe windows.

## Telegraph rules

Every dangerous attack should communicate:

1. who is attacking;
2. where the danger will occur;
3. when it becomes active;
4. how long recovery lasts;
5. whether the attack can be interrupted, blocked or avoided.

Higher difficulty may shorten telegraphs or combine roles, but must not remove essential information.

## Reward and size concepts

A working classification:

| Enemy class | Encounter role | Reward direction |
|---|---|---|
| Small | swarm or nuisance | low XP, small/no direct drop chance |
| Normal | standard combat unit | normal XP and registered drop profile |
| Large | durable specialist | higher XP and stronger drop profile |
| Elite | modified named threat | guaranteed progress currency or improved box chance |
| Boss | encounter climax | guaranteed authored reward package and milestone credit |

Exact rewards must be resolved by reward/drop authorities, never directly spawned by an enemy-name switch.

## Open decisions

- Which five archetypes form the first complete enemy roster.
- Whether elites are separate definitions or deterministic modifiers applied to eligible enemies.
- Whether directional armour is a generic mechanic or reserved for bosses.
- How crowd-control resistance scales without making skills useless.
- Whether enemies can damage each other or environmental objects.
- How much enemy behaviour changes between solo and co-op.
- Which enemy families visually belong to the same faction or biome.

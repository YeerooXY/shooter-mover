# Build-Relative Difficulty and Apocalypse Scaling

## Purpose

Define a difficulty model that:

- supports progression from roughly 4 DPS to millions;
- preserves the feeling of becoming stronger;
- keeps current endgame content challenging for optimized players;
- prevents low-level characters with account-wide or premium gear from entering under-scaled lobbies;
- gives every build an endless mode that eventually overwhelms it.

## Core Terms

- `P_max`: maximum practical sustained combat power available at the current content tier. It must account for realistic reloads, heat/ammunition downtime, average accuracy, enemy resistance, average critical output, and realistic ability uptime. It is not a five-second theoretical burst.
- `P_build`: evaluated practical power of the player's equipped loadout.
- Percentages below describe power relative to `P_max`.

## Fixed Difficulty Ladder

| Mode | Suitable build range | Balance reference | Purpose |
|---|---:|---:|---|
| Normal | 10-25% | 20% | Progression and accessibility |
| Hard | 25-50% | 40% | Developed casual builds |
| Nightmare | 50-75% | 65% | Strong, coherent builds |
| Impossible | 75-105% | 90% | Endgame mastery |

For each mode:

```text
P_mode = R_mode x P_max
Enemy EHP = P_mode x target active TTK x enemy-role factor
```

Normal through Impossible are **tier-relative**, not exactly player-relative. If every upgrade immediately raised enemy health by the same amount, progression would feel cancelled.

## Example Boss TTK

For a boss tuned to last 120 seconds against the mode's reference build:

| Player build | Normal | Hard | Nightmare | Impossible |
|---|---:|---:|---:|---:|
| Reference build for that mode | 120 s | 120 s | 120 s | 120 s |
| Maximum 100% build | 24 s | 48 s | 78 s | 108 s |

This preserves visible progression:

- a maximum build destroys Normal;
- Hard becomes fast but not completely meaningless;
- Nightmare remains an encounter;
- Impossible stays close to the intended full fight.

For a current-tier major boss on Impossible, useful targets are:

- 75% entrant: roughly 130-180 seconds;
- 90% intended build: roughly 90-130 seconds;
- 100% optimized build: roughly 60-110 seconds;
- exceptional burst may shorten phases, but should not erase the encounter.

Avoid universal hard damage caps. Prefer attack patterns, armor transitions, positioning pressure, target switching, resource constraints, and carefully used phase gates.

## Impossible Mode Detail

Impossible should use the full 75-105% range selectively:

- ordinary encounters: 80-90%;
- dangerous elites and combinations: 90-100%;
- major bosses: 90-105%;
- optional enrages and challenge objectives: 110-125%.

A 75% build may enter but should struggle. Around 90% is the intended preparation level. A perfect build remains advantaged without trivializing current-tier content.

## Apocalypse Mode

Apocalypse is exact build-relative endless survival.

At run start:

1. snapshot the player's equipped loadout;
2. evaluate practical offense and defense;
3. lock that rating for the run.

Do not continuously infer power from observed damage. Players could otherwise sandbag, miss deliberately, or equip weak gear during calibration.

### Apocalypse Pressure

Start at 10% of the player's actual build and increase until the build fails.

A simple initial curve:

```text
A(w) = 0.10 + 0.05 x (w - 1), for waves 1-19
```

This reaches 100% at wave 19.

After wave 19:

```text
A(w) = 1.08^(w - 19)
```

| Wave | Apocalypse pressure |
|---:|---:|
| 1 | 10% |
| 5 | 30% |
| 10 | 55% |
| 15 | 80% |
| 19 | 100% |
| 25 | ~159% |
| 30 | ~233% |
| 40 | ~503% |

Apocalypse has no conventional final clear. The result is maximum wave, maximum pressure, and survival time.

### Wave Health Budget

```text
EHP_wave = P_offense x active combat seconds x A(w)
```

This is the wave's total effective-health budget. The encounter generator distributes it across fodder, heavies, elites, bosses, shielding, healing, and other archetypes.

### Damage and Pressure Growth

Enemy damage should increase more slowly than enemy health load:

```text
Enemy damage pressure ~= sqrt(A(w))
```

Use caps where necessary. High-wave pressure should increasingly come from:

- enemy density;
- elite frequency;
- mutations;
- movement and attack speed;
- shielding and healing;
- area denial;
- dangerous enemy combinations;
- mixed boss waves.

The failure state should normally be battlefield collapse, not a random basic-enemy one-shot.

## Build Evaluation

Do not use one tooltip DPS value.

### Boss Power

Evaluate sustained single-target output including:

- reload, heat, and ammunition downtime;
- average critical output;
- realistic ability uptime;
- damage-over-time stacking and caps;
- resistance and penetration;
- practical range and accuracy.

### Crowd Power

Evaluate a standard-density multi-target scenario including:

- explosions and area damage;
- piercing and chains;
- damage-over-time spread;
- target switching;
- overkill efficiency;
- acquisition and travel time.

A starting aggregate can be:

```text
P_offense = 0.55 x P_boss + 0.45 x P_crowd
```

Apocalypse wave types must still test the two components separately. A sniper build should face swarm checks, and a crowd-clearing build should face boss checks.

### Defense

Evaluate:

- effective health;
- mitigation;
- healing and sustain;
- mobility and avoidance;
- crowd control;
- revives and escape tools.

### Four-Weapon Loadouts

Do not sum the tooltip DPS of all four weapons unless they truly deal damage simultaneously.

Count:

- active-weapon sustained DPS;
- persistent damage already applied;
- realistic switching;
- utility and status contribution.

## Matchmaking and Premium-Weapon Guardrails

Avoid the failure mode where character level determines lobby difficulty while account-wide or premium equipment determines actual combat power.

Rules:

- Matchmake from effective combat rating and equipped loadout, not character level alone.
- A level-1 character carrying endgame equipment must be treated as endgame combat power.
- Use the highest meaningful signal among progression tier, equipment, and build; do not average a powerful item down into a beginner lobby.
- Account-wide or premium gear in public progression must either synchronize to the tier the character has unlocked or queue at its full effective power.
- Premium weapons should be same-tier sidegrades. Aim for no more than roughly 5-10% raw advantage, with explicit tradeoffs.
- Private unsynchronized runs may permit annihilating old content, but should provide reduced progression rewards.
- Standard modes may become easy when revisited. Current-tier public matchmaking must still see the player's real power.
- Apocalypse naturally absorbs legitimate power because it evaluates the actual build.

## Multiplayer

Use aggregate evaluated combat contribution, not total player level.

A carry must never lower lobby difficulty by joining on a low-level character.

For roughly equal players, a possible boss-health baseline is:

```text
HP_N = HP_1 x [1 + 0.8 x (N - 1)]
```

| Players | Boss-health multiplier |
|---:|---:|
| 1 | 1.0x |
| 2 | 1.8x |
| 3 | 2.6x |
| 4 | 3.4x |

The production implementation should use actual combat ratings and role utility rather than assuming equal players.

## Telemetry

Track at least:

- boss TTK percentiles by mode and build percentile;
- completion, death, and quit rates;
- damage-contribution skew within parties;
- character-level-to-combat-power mismatch;
- premium versus earnable weapon performance;
- weapon and build pick rates;
- Apocalypse maximum wave and pressure distributions;
- low-level characters dominating high-tier lobbies.

Balance around practical player percentiles, not the largest theoretical screenshot number.

## Decisions

- Normal supports 10-25% builds and uses roughly 20% as its reference.
- Hard supports 25-50% and uses roughly 40%.
- Nightmare supports 50-75% and uses roughly 65%.
- Impossible supports 75-105% and uses roughly 90%, with selective spikes.
- Apocalypse begins at 10% of the player's snapshotted build and scales indefinitely.
- Fixed modes preserve progression; Apocalypse equalizes builds and measures survival.
- Matchmaking uses combat power, never character level alone.
- Premium equipment cannot bypass the intended progression curve.

## Open Questions

- Is `P_max` global per content tier, or separate by class and build archetype?
- What Apocalypse pressure and reward curve best supports long-term play?
- How strongly should defense rating affect enemy damage versus spawn and elite pressure?
- Should public low-level power control use a hard cap, stat normalization, or automatic queue promotion?

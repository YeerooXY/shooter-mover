# Augments and overclock cores

## Status

Product-planning document only. The examples below are design memory and working proposals, not final balance or implementation instructions.

## Confirmed direction

- Weapons are exact equipment instances. Augments belong to the exact instance, not globally to the definition.
- Available augment capacity is not the same thing as installed augments.
- A newly generated weapon may have augment capacity while containing no installed augment.
- Players should not need to exploit rerolls to obtain a satisfying result.
- Overclock cores are intended as a later progression material/system after the basic loot, inventory and crafting loops work.

## Player-facing goal

Augments should let two copies of the same weapon family feel meaningfully different without erasing the weapon's identity.

A Rattler should remain recognisably a Rattler. An augment may improve cadence, piercing, accuracy or utility, but it should not silently transform it into a rocket launcher.

## Working augment structure

Each augment has:

- a stable augment identity;
- a category;
- a supported weapon tag or mechanic requirement;
- a tier or strength band;
- one primary effect;
- an optional trade-off;
- a slot cost;
- a deterministic fingerprint on the exact weapon instance.

A weapon may expose a small number of augment-capacity points. Powerful augments can cost more than one point.

## Augment categories and examples

### Core output

| Augment | Intended effect |
|---|---|
| Calibrated Accelerator | Increases projectile speed and effective range. |
| Rapid Cycling Assembly | Improves cadence with a heat, spread or stability trade-off. |
| High-Density Capacitor | Improves energy damage or charge behaviour but increases recovery time. |
| Reinforced Chamber | Improves per-shot power while reducing cadence. |

### Projectile behaviour

| Augment | Intended effect |
|---|---|
| Piercing Core | Adds or improves pierce count where the weapon supports travelling projectiles. |
| Ricochet Matrix | Adds a bounded wall bounce or improves retained damage after a bounce. |
| Seeker Module | Adds limited target correction to a supported projectile, not perfect homing. |
| Splitter Lens | Divides one projectile into weaker fragments after impact or distance. |
| Volatile Payload | Adds a small area effect with reduced direct-hit efficiency. |

### Spread and multi-shot

| Augment | Intended effect |
|---|---|
| Tight-Bore Choke | Reduces spread and improves range for shotgun-like weapons. |
| Scatter Amplifier | Adds projectiles while increasing spread or reducing damage per pellet. |
| Sequential Loader | Converts a supported simultaneous shot into a short sequential burst. |
| Burst Governor | Improves burst control, pause timing or final-shot power. |

### Elemental and status

| Augment | Intended effect |
|---|---|
| Cryogenic Inducer | Adds slowing or chill accumulation to supported hits. |
| Corrosive Injector | Adds armour degradation or a bounded corrosion effect. |
| Thermal Jacket | Adds burn or improves thermal area persistence. |
| Bioactive Reservoir | Improves poison/biological stack behaviour with a maximum stack cap. |
| Arc Coupler | Adds a limited secondary chain to an energy-compatible weapon. |

### Handling and utility

| Augment | Intended effect |
|---|---|
| Gyro Stabiliser | Reduces spread growth or movement penalty. |
| Smart Feed | Improves reload, charge recovery or ammunition efficiency where applicable. |
| Targeting Relay | Improves critical or weak-point consistency without unconditional damage. |
| Salvage Beacon | Small non-combat utility related to pickup visibility or collection radius. |
| Emergency Vent | Prevents one heat lockout and then enters a long cooldown. |

## Compatibility rules

### Working proposal

- Augments declare mechanic requirements instead of listing every weapon by name.
- A ricochet augment requires a compatible travelling projectile and collision policy.
- A shotgun choke requires a spread-capable shot pattern.
- A burn augment requires a damage/effect pipeline that supports thermal status.
- Invalid combinations are rejected before installation.
- Removing an augment restores the exact previous instance state; it does not regenerate the weapon.

## Acquisition

Augments may come from:

- strongbox rewards;
- crafting specific known augment definitions;
- dismantling augmented equipment;
- high-difficulty or endless-mode milestone rewards;
- limited shop offers after the augment system unlocks.

The system should prefer visible, targetable progress over blind reroll loops.

## Installation and removal

### Working proposal

- Installing an augment costs scrap and possibly money.
- Removing a common augment is cheap and returns the augment item.
- High-tier augments may require a removal fee, but should not be destroyed by default.
- Replacing an augment is an explicit transaction with previewed before/after effects.
- A failed save or rejected transaction leaves both weapon and resources unchanged.

## Overclock cores

### Intended identity

Overclock cores are rare, endgame-oriented materials used to push a favourite exact weapon beyond ordinary augmentation. They should not be required for basic viability.

### Working proposal

A core can be spent on one of two controlled actions:

1. **Capacity overclock** — add one augment-capacity point up to a strict weapon-specific cap.
2. **Signature overclock** — unlock one authored, weapon-family-specific transformation with a clear benefit and drawback.

Examples:

- Rattler overclock: sustained fire ramps cadence but also spread/heat.
- Ironwake overclock: tighter initial blast followed by a wider secondary fragment wave.
- Crownfall overclock: larger explosion with slower projectile speed and longer recovery.
- Nullstar overclock: stronger damage-over-time stacks but lower direct-hit damage.

Overclocks should be authored choices, not unrestricted stat multiplication.

## Anti-reroll philosophy

- Opening a box is deterministic once committed.
- Closing and reopening cannot change the reward.
- Augment installation does not reroll unrelated weapon properties.
- A player who receives an unwanted augment should have useful sell, dismantle or crafting value.
- Duplicate augments should feed progression rather than become pure dead drops.
- The strongest builds should require choices and trade-offs, not only luck.

## Open decisions

- Exact augment capacity by rarity, mark and item level.
- Whether augments are reusable inventory items or consumed on installation.
- Whether capacity overclocking can fail; the current preference is deterministic success rather than destructive gambling.
- Whether signature overclocks are mutually exclusive.
- Whether overclock cores are character-bound, account-wide or freely stored.
- How much augment power is allowed in competitive multiplayer modes.
- Whether armour receives the same augment system or a separate module system.

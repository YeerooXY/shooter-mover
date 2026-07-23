# DROP-STRONGBOX-LIVE-001

## Authority flow

```text
immutable terminal source event
→ resolve frozen effective reward profile
→ run one personal roll for each eligible participant
→ apply that participant's pacing state
→ freeze an exact canonical strongbox tier on each generated box grant
→ admit an exact participant-owned pickup
→ collect the exact box instance
→ open through the production hybrid strongbox policy
→ receive one exact fixed-stat equipment instance
```

Enemy combat, prop destruction, Unity presentation and Stage 1 controllers do not
select rewards. They publish immutable source facts and stable profile references.

## Profile resolution precedence

The resolver applies these layers in exactly this order:

1. source default profile;
2. game-mode override;
3. mission override;
4. difficulty override;
5. event overrides sorted by stable override ID;
6. exact placement override.

Each layer is one of:

- **Replace** — discard the inherited profile and use another immutable profile;
- **AddGroups** — append ordered groups to the inherited profile;
- **Modify** — apply authored probability/quantity multipliers and/or replace the
  strongbox-tier-selection profile;
- **Disable** — resolve to an explicit no-drop profile.

The complete resolution, including the source reference, applied override IDs,
effective profile and fingerprints, is frozen into the personal roll context.
Changing authored data later therefore changes new rolls but cannot change an exact
retry whose operation identity has already been recorded.

## Random-box pacing formula

All probabilities use integer millionths. For an eligible random strongbox attempt:

```text
scaled = floor(base_probability × room_multiplier / 1,000,000)
scaled = floor(scaled × run_multiplier / 1,000,000)
effective = clamp(scaled + pity_bonus, 0, 1,000,000)
```

The default policy authors:

- completed-run minimum: 1 box;
- pity begins after 10 consecutive eligible random-box failures;
- each later failure adds 4,000 millionths (0.4 percentage points);
- pity is capped at 80,000 millionths (8 percentage points);
- room random-box multipliers: 100%, 100%, 75%, 50%, 30%, then 15% for five or
  more boxes;
- run multiplier: 100% by default, with an authored extension point for modes;
- guaranteed boxes do not consume random probability and do not reset pity by
  default.

Guaranteed boss, treasure, level-designer, debug and run-minimum grants bypass
random saturation. The minimum fallback creates only the missing count as a
mission-completion participant reward; it never fabricates a late enemy drop.

## Deterministic identity

A personal reward operation derives its stable logical identity from:

- run ID and run lifecycle generation;
- exact terminal source ID and source lifecycle generation;
- room and placement ID;
- participant ID;
- declared profile reference ID;
- algorithm version.

The separately compared full context fingerprint additionally freezes:

- immutable terminal-fact fingerprint;
- effective profile and override fingerprint;
- pacing-policy fingerprint;
- player/mission level;
- difficulty, game mode and event modifiers;
- economy multipliers;
- deterministic root seed.

An exact retry returns the recorded result. Reusing the logical operation ID with a
different full fingerprint is a conflicting duplicate and grants nothing.

## Canonical source profiles

| Reference ID | Production profile |
|---|---|
| `drop-source.small-enemy` | 85% none, 13.5% money, 1.4% scrap, 0.1% random box |
| `drop-source.normal-enemy` | 90% money, 8% scrap, 2% random box |
| `drop-source.large-enemy` | 60% money, 8% scrap, 32% random box |
| `drop-source.boss-enemy` | one guaranteed box plus money |
| `drop-source.extra-boss-enemy` | money plus 3/4/5 boxes at 70/25/5 |
| `drop-source.normal-prop` | exact alias of normal enemy |
| `drop-source.rare-prop` | exact alias of large enemy |
| `drop-source.extra-rare-prop` | 2/3/4 boxes at 70/25/5 |
| `drop-source.normal-hidden-treasure` | exact alias of normal enemy |
| `drop-source.large-hidden-loot` | one guaranteed box |
| `drop-source.large-treasure-loot` | exact alias of extra-rare prop |
| `drop-source.explicit-no-drop` | explicit no-drop |

Aliases resolve to the same immutable profile object and fingerprint; values are not
copied.

## Stage 1 migration

| Content | New profile reference |
|---|---|
| mobile blaster droid | `drop-source.normal-enemy` |
| ram pouncer | `drop-source.small-enemy` |
| blaster turret | `drop-source.large-enemy` |
| pursuer drone | `drop-source.explicit-no-drop` |
| hybrid sentinel | `drop-source.large-enemy` |
| ordinary crate | `drop-source.normal-prop` |
| explosive prop | `drop-source.rare-prop` |

The bounded persisted-ID migration map is:

| Legacy ID | New reference |
|---|---|
| `drop.enemy-common` | `drop-source.normal-enemy` |
| `drop.enemy-turret` | `drop-source.large-enemy` |
| `drop.enemy-none` | `drop-source.explicit-no-drop` |
| `drop.prop-stage1-ordinary` | `drop-source.normal-prop` |
| `drop.prop-stage1-explosive` | `drop-source.rare-prop` |
| `drop.prop-legacy-none` | `drop-source.explicit-no-drop` |

Legacy IDs are accepted only through this explicit migration map. They are not
executed as a second reward authority. `strongbox.tier-common` has no migration:
new drops freeze one of the eleven canonical production tiers immediately.

## Designer copy/paste extensibility proof

A designer can add these examples without changing generation code:

### `drop-source.elite-medic-drone`

1. Copy the authored `drop-source.large-enemy` catalog entry.
2. Change the stable reference/profile ID to
   `drop-source.elite-medic-drone` and edit its ordered groups or quantities.
3. Assign that stable profile ID to the enemy definition.

### `drop-source.locked-vault`

1. Copy `drop-source.large-hidden-loot` or
   `drop-source.extra-rare-prop`.
2. Change the stable ID to `drop-source.locked-vault` and choose an authored tier
   selection profile.
3. Assign it to the prop/treasure/placement definition.

### `game-mode.survival-boss-override`

1. Author a replacement reward profile containing money and crafting-material
   groups.
2. Author a **Replace** override with stable ID
   `game-mode.survival-boss-override`.
3. Assign the override to Survival mode.

None of these additions requires editing:

- enemy combat or movement code;
- prop health/destruction code;
- a Stage 1 controller;
- personal or multiplayer reward generation;
- tier-selection or pacing formulas;
- simulator formulas;
- existing profiles.

Only a genuinely new reward kind requires one new handler registered through
`RewardGrantHandlerRegistryV1`.

## Hybrid strongbox rarity mapping

The hybrid policy exposes all seven canonical weapon-definition rarities:
Common, Uncommon, Rare, Epic, Legendary, Mythic and Artifact. The five original
policy anchors remain unchanged. Uncommon uses the rounded midpoint between the
Common and Rare selection multipliers; Mythic uses the rounded midpoint between
Legendary and Artifact. These are distinct stable rarity IDs, not aliases or silent
merges.

Weapon-definition rarity is independent of equipment quality. Equipment quality
continues to use its own Common/Rare/Exceptional policy.

## Generated augment representation

A generated box item records an immutable versioned signature keyed by its exact
equipment-instance ID:

```text
capacity = 3
shared augment level = 8
installed augment instances = []
```

`GeneratedEquipmentAugmentSignatureV1` freezes the source box, hybrid policy ID and
fingerprint, capacity, shared level and algorithm version. The exact-instance
authority is idempotent, rejects conflicting duplicates and exports/restores a
sorted snapshot. Installed `AugmentInstance` objects remain owned exclusively by
the augment installation authority.

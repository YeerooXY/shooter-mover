# Strongbox Hybrid Loot V1

## Purpose

This policy captures the intended SAS4-style strongbox feeling without creating a second reward generator or installing fake augment identities.

The complete decision order is:

1. roll one box-adjusted target level from the player's level;
2. weight all weapon definitions near that target through a bounded bell curve;
3. apply the definition's authored base weight and rarity multiplier;
4. after selecting one definition, roll its concrete item level near an 80/20 blend of the target and definition peak;
5. roll one shared augment signature such as `10/3`;
6. let the equipment/augment authority later resolve concrete augment identities for those available slots.

`10/3` means three augment slots whose shared generated augment level is ten. V1 does not create three installed `AugmentInstance` values. It creates the immutable capacity/level decision needed by a later equipment-instance schema cutover.

## Deterministic inputs

Every roll consumes only immutable facts:

- tier policy fingerprint;
- player level;
- root seed;
- RNG algorithm version;
- equipment slot ordinal;
- selected definition peak level;
- selected definition rarity;
- normal and absolute augment slot capacity.

No ambient time, Unity object identity, dictionary iteration order, retry loop, or `System.Random` participates.

## Stage A — triangular target level

Each box authors:

```text
minimum delta / most-likely delta / maximum delta
```

The policy rolls a discrete triangular distribution and resolves:

```text
target level = max(1, player level + rolled delta)
```

Early boxes trail the player on average. Premium boxes lead the player on average.

| Tier | Min | Mode | Max |
|---:|---:|---:|---:|
| 1 | -8 | -4 | 0 |
| 2 | -7 | -3 | +1 |
| 3 | -6 | -2 | +2 |
| 4 | -5 | -1 | +3 |
| 5 | -4 | -1 | +4 |
| 6 | -3 | 0 | +3 |
| 7 | -2 | +1 | +4 |
| 8 | -1 | +2 | +5 |
| 9 | 0 | +3 | +6 |
| 10 | +1 | +4 | +7 |
| 11 | +3 | +5 | +7 |

## Stage B — definition affinity

A weapon definition is compared through its authored peak drop level, not through its runtime damage.

```text
distance = abs(definition peak - target level)
final weight = base definition weight
             * bell affinity(distance)
             * tier rarity multiplier
```

The authored affinity table is a fixed-point Gaussian-shaped kernel with sigma approximately four levels.

| Distance | Relative affinity |
|---:|---:|
| 0 | 100% |
| 4 | 41.1112% |
| 8 | 2.8566% |
| 12 | 0.0335% |
| 13+ | 0% |

This provides the requested bounded twelve-level early and late tail. A level-19-peak weapon can therefore still appear around target level 7 with `0.0335%` of its centered level affinity before definition and rarity weights are applied.

## Stage C — hybrid concrete item level

Selecting a definition does not force its peak level onto the equipment instance.

```text
hybrid center = round(target level * 0.80 + definition peak * 0.20)
item level = max(1, hybrid center + nearby bell offset)
```

The nearby offset is bounded to `-4..+4` and strongly concentrated at `-1..+1`.

This preserves both meanings:

- the box determines the broad power expectation;
- the definition's natural level still pulls the item slightly toward its authored home.

## Stage D — rarity

V1 registers five open stable rarity IDs:

- `rarity.common`
- `rarity.rare`
- `rarity.epic`
- `rarity.legendary`
- `rarity.artifact`

Each tier can independently gate or multiply a rarity. Artifact has zero selection multiplier in tiers 1–4, a tiny authored path beginning at tier 5, and becomes materially represented only in the top boxes.

Rarity also adjusts augment bias:

| Rarity | Augment bias |
|---|---:|
| Common | +2 levels |
| Rare | +1 |
| Epic | 0 |
| Legendary | -1 |
| Artifact | -2 |

This makes naturally rare definitions consume a little more of the overall reward budget while still allowing exceptional high-augment jackpots.

## Stage E — shared augment signature

The augment bias is:

```text
bias = clamp(player level - item level + rarity bias, -12, +12)
```

A positive bias tilts slot and shared-level weights upward. A negative bias tilts them downward. It never removes an authored outcome, so an item above the player's level can still very rarely roll a powerful signature.

The authored baseline curve rises quickly:

| Tier | Slot outcomes | Shared augment levels |
|---:|---|---|
| 1 | 0–3, strongly zero-biased | 1–5 |
| 2 | 0–3 | 1–6 |
| 3 | 0–3 | 2–10, level 10 jackpot present |
| 4 | 0–3 | 3–10 |
| 5 | 1–3 guaranteed | 4–10 |
| 6 | 1–3 guaranteed | 5–10 |
| 7 | 2–3 guaranteed | 6–10 |
| 8 | normal maximum guaranteed | 6–10 |
| 9 | normal maximum guaranteed | 8–10 |
| 10 | normal maximum guaranteed; 3% authored overcap | 9–11 |
| 11 | normal maximum guaranteed; 30% authored overcap | 10–11 |

For normal weapons, normal maximum is three and absolute maximum may be four. For normal gear, normal maximum is two and a three-slot absolute cap can be supplied later. A tier-8 full-slot outcome maps to three slots for a weapon and two slots for ordinary gear.

## Ownership boundary

This PR deliberately does not change `EquipmentInstance`, holdings persistence, or the durable reward-transfer branch while DROP-PERSIST-PROOF-001 / PR #280 owns those paths.

The next integration should:

1. add an immutable generated augment-capacity/shared-level field to the equipment instance schema;
2. version holdings encoding and restore compatibility;
3. bind `ProductionStrongboxHybridLootCatalogV1` into `StrongboxEquipmentGenerationResolverV1`;
4. project the signature into the strongbox card and simulator reports;
5. keep `EquipmentInstance.Augments` empty until concrete augments are installed by their owning authority.

This preserves PR #266's correction: strongbox generation must not pretend that capacity symbols are installed augment identities.

## Validation included

Focused EditMode tests cover:

- byte-equivalent deterministic replay;
- early boxes trailing and premium boxes leading the player;
- exact twelve-level `0.0335%` tail affinity;
- hybrid 80/20 instance-level centering;
- rejection outside the twelve-level definition radius;
- tier 7 two-slot guarantee;
- tier 8 full normal slot guarantee with levels 6–10;
- two-slot gear mapping;
- under-level augment compensation and retained over-level jackpots;
- artifact gating;
- tier 11 normal-max guarantee plus four-slot/level-11 overcaps;
- zero-augment early-box outcomes.

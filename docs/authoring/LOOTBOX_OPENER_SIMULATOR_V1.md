# Lootbox Opener Simulator V1

Status: editor-only real-content simulator, dependent on WEAPON-DATA-001.

## Entry point

Open:

`Tools > Shooter Mover > Lootbox Opener Simulator`

Load the current `weapon_baseline_v01.json`. The simulator imports and validates the JSON through `WeaponCatalogJsonImporter`, filters to live definitions, projects those definitions into the accepted EQP catalog shape, and delegates equipment generation to the existing BOX strongbox resolver and GEN service.

Preview-only weapon definitions are not eligible for normal box rolls. `TopBoxOnly` definitions are eligible only for Antimatter.

## Ordered Results / Opening flow

1. Select a player level and deterministic seed.
2. Add any number of the eleven normal strongbox tiers to **Currently Looted**.
3. The list preserves exact insertion order and supports per-entry removal.
4. Select **Open Selected Boxes In Order**.
5. The queue, player level, and seed are copied into an immutable editor-session projection and fingerprinted. Editing the next **Currently Looted** queue cannot alter the active opening order.
6. Each box freezes one generated immutable equipment instance before presenting it on **Results / Opening**.
7. Choose:
   - **Keep / Accept** — submits the exact `EquipmentInstance` to `PlayerHoldingsService`;
   - **Sell** — does not add equipment and increments simulator cash by exactly `1000`.

Keep and Sell are keyed by concrete equipment-instance identity. Repeated decisions cannot add the same item or cash twice.

The sale value is intentionally marked:

`TODO(ECONOMY): replace the temporary fixed sale value with the real item valuation service.`

The editor simulator does not mutate the real mission Results flow, consume gameplay strongboxes, or create a second reward-generation algorithm.

## Eleven-tier balance

| Tier | Name | Mean level offset | Item SD | Slots | Slot SD | Scrap | Common / Rare / Exceptional |
|---:|---|---:|---:|---:|---:|---:|---:|
| 1 | Steel | -6 | 5.2 | 1..2 | 0.70 | 5..10 | 82 / 17 / 1 |
| 2 | Copper | -4 | 5.0 | 1..2 | 0.65 | 8..16 | 78 / 20 / 2 |
| 3 | Silver | -2 | 4.7 | 1..2 | 0.60 | 12..24 | 72 / 25 / 3 |
| 4 | Amethyst | 0 | 4.4 | 1..3 | 0.55 | 18..36 | 64 / 31 / 5 |
| 5 | Gold | +2 | 4.1 | 1..3 | 0.50 | 25..50 | 55 / 37 / 8 |
| 6 | Black Opal | +4 | 3.8 | 1..3 | 0.475 | 35..70 | 46 / 42 / 12 |
| 7 | Blue Sapphire | +6 | 3.5 | 1..3 | 0.45 | 50..100 | 37 / 46 / 17 |
| 8 | Emerald | +8 | 3.2 | 2..3 | 0.425 | 70..140 | 29 / 49 / 22 |
| 9 | Alexandrite | +10 | 2.9 | 2..3 | 0.40 | 100..200 | 21 / 50 / 29 |
| 10 | Red Diamond | +12 | 2.6 | 2..3 | 0.35 | 150..300 | 14 / 48 / 38 |
| 11 | Antimatter | +14 | 2.2 | 3 | 0.30 | 250..500 | 7 / 40 / 53 |

Steel, Copper, and Silver intentionally produce a below-player mean. Their broad distribution still permits occasional exciting outcomes, but their expected result should feel materially weaker than late-tier boxes. Antimatter has a three-slot floor and a +14 mean, so even its low tail remains visibly stronger than the early tiers.

Negative offsets are applied to an immutable effective player context because BOX V1's power-budget policy accepts only non-negative tier bonuses. Positive offsets use the normal tier bonus. The real player progression state is never mutated.

## Odds inspector

The Odds page performs deterministic repeated opens through the same BOX/GEN composition and reports:

- exact source weapon-definition frequency;
- quality frequency;
- augment-slot frequency, including 1, 2, and 3 slots;
- augment-tier frequency from 1 through 3;
- augment-level frequency from 1 through 10;
- item-level delta relative to the selected player level;
- rejected or impossible rolls.

The current Results / Opening item also has a **Calculate This Tier's Odds** action. It shows the observed chance of that exact definition plus the tier's slot, augment-tier, and augment-level distributions without leaving the item view.

The report is empirical for the selected catalog, player level, tier, seed, and sample count. It is not a hand-calculated table and therefore reflects catalog eligibility, top-box filtering, soft activation, obsolescence, quality selection, item-level rolls, and augment generation together.

## Deterministic copy/export

The window can copy:

- the frozen opening queue canonical text;
- the current generated item's canonical text;
- the complete odds report canonical text.

Each projection includes a SHA-256 fingerprint. Identical catalog content and identical inputs produce byte-identical canonical output.

## Current temporary boundaries

- Generic simulator augment definitions are used until AUG-002 supplies production augment content. They use tiers `1..3` and levels `1..10`.
- Each normal box currently presents exactly one equipment item. The authored strongbox definition also carries its mandatory scrap policy for later full reward-payload display.
- The opening simulator tracks its own editor-session holdings and cash. It does not persist to a player save.
- Artwork is intentionally not bound in this task.
- Unity compile and EditMode XML proof are required before readiness.

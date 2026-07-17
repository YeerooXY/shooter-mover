# Lootbox Opener Simulator V1

Status: editor-only real-content simulator. `WEAPON-DATA-001` is merged into `main`.

## Entry points

### Decision preview and odds

Open:

`Tools > Shooter Mover > Lootbox Opener Simulator`

This surface is intended for balance iteration, odds inspection, deterministic exports, and the temporary Keep/Sell interaction.

### Real authority wiring

Open:

`Tools > Shooter Mover > Authoritative Strongbox Wiring`

This surface verifies the production-shaped transaction chain:

`exact owned box -> BOX -> GEN -> RAP -> MON/SCR/INV -> exact box consumption`

Load the current `weapon_baseline_v01.json`. Both surfaces import and validate it through `WeaponCatalogJsonImporter`, filter to live definitions, project those definitions into the accepted EQP catalog shape, and delegate equipment generation to the existing BOX resolver and GEN service.

Preview-only weapon definitions are not eligible for normal box rolls. `TopBoxOnly` definitions are eligible only for Antimatter.

## Click-to-queue behavior

Both windows display all eleven box tiers as clickable controls.

1. Click any box to append one entry to **Currently Looted**.
2. Click a tier repeatedly to add multiple boxes of that tier.
3. Mix tiers in any order.
4. Remove individual entries before opening.
5. Start opening to freeze the selected order, player level, and deterministic seed.

For example:

`Steel -> Steel -> Emerald -> Copper -> Antimatter`

is retained and opened in exactly that order.

## Authoritative opening behavior

When an authoritative batch is prepared:

1. The existing BOX/GEN composition deterministically commits the exact source weapon definition for each queue entry.
2. The complete frozen batch is bound to one `StrongboxOpeningServiceV1` instance.
3. Every queue entry receives a distinct strongbox-instance ID, opening ID, context fingerprint, and immutable command.
4. Each box is added through `PlayerHoldingsService` and registered with BOX before it can be opened.
5. Clicking **OPEN THROUGH REAL BOX AUTHORITIES** submits that exact immutable opening command.
6. BOX generates the actual equipment instance, quality, item level, augments, and mandatory scrap; RAP applies the payload to MON/SCR/INV; BOX then consumes only the exact opened box.
7. Pending operations reuse the same opening identity. Repeated successful input returns an exact duplicate and cannot grant or consume twice.

The authoritative result view displays:

- production tier and editor authority-binding tier;
- exact box and opening identities;
- committed weapon-definition identity;
- generated equipment definition and concrete instance identity;
- item level, quality, augment count, tier, and level;
- money, scrap, and holdings balances/sequences;
- BOX status and sequence transition;
- generated-outcome fingerprint;
- every applied reward payload.

The editor-only binding tier is unique per queued box so the strict RAP contract can retain an exact generated equipment-definition identity while the visual surface still presents the original production tier. No alternate reward sampler is introduced: definition commitment and final equipment generation both use the existing BOX/GEN composition.

## Decision-preview flow

The decision-preview opener freezes one generated item and presents:

- **Keep / Accept** — submits the exact equipment instance to `PlayerHoldingsService`;
- **Sell** — does not add that preview equipment and increments simulator cash by exactly `1000`.

Keep and Sell are keyed by concrete equipment-instance identity. Repeated decisions cannot add the same item or cash twice.

The sale value is intentionally marked:

`TODO(ECONOMY): replace the temporary fixed sale value with the real item valuation service.`

This Keep/Sell interaction is not presented as the production transaction. The current real BOX authority applies rewards during `Open`. A production post-open Sell action needs its own exactly-once equipment disposition/valuation transaction rather than UI-local cash mutation.

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

Steel, Copper, and Silver intentionally produce a below-player mean. Their broad distribution still permits occasional exciting outcomes, while Antimatter has a three-slot floor and a +14 mean.

Negative offsets are applied to an immutable effective player context because BOX V1's power-budget policy accepts only non-negative tier bonuses. The real player progression state is never mutated.

## Odds inspector

The Odds page performs deterministic repeated opens through BOX/GEN and reports:

- exact source weapon-definition frequency;
- quality frequency;
- augment-slot frequency, including 1, 2, and 3 slots;
- augment-tier frequency from 1 through 3;
- augment-level frequency from 1 through 10;
- item-level delta relative to the selected player level;
- rejected or impossible rolls.

The current preview item also has **Calculate This Tier's Odds**, showing that exact definition's observed frequency together with the tier distributions.

## Deterministic copy/export

The preview window can copy:

- the frozen queue canonical text;
- the current generated item's canonical text;
- the complete odds report canonical text.

The authoritative prepared-box model also has canonical text and a SHA-256 fingerprint. Identical catalog content and identical inputs produce byte-identical commitments.

## Current boundaries

- Generic simulator augment definitions remain in use until AUG-002 supplies production augment content. They use tiers `1..3` and levels `1..10`.
- The authoritative window uses editor-session MON/SCR/INV authorities; it proves the real service chain but does not persist to a player save.
- Mission Results-to-scene routing of RUN-001 exact collected boxes remains the responsibility of BOXUI-001.
- Box artwork is not yet bound; the current visual cards are labeled controls ready to receive the art assets.
- Unity compilation and focused EditMode XML with zero failures are still required before the draft PR can be marked ready.

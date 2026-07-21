# Lootbox Opener Simulator V1

Status: editor-only real-content simulator. `WEAPON-DATA-001` is merged into `main`.

## Entry points

### Decision preview and odds

Open:

`Tools > Shooter Mover > Lootbox Opener Simulator`

This surface is intended for deterministic reward inspection, compact weapon-card review, odds inspection, and the temporary Keep/Sell interaction.

### Real authority wiring

Open:

`Tools > Shooter Mover > Authoritative Strongbox Wiring`

This surface verifies the production-shaped transaction chain:

`exact owned box -> BOX -> GEN -> RAP -> MON/SCR/INV -> exact box consumption`

Load the current `weapon_baseline_v01.json`. Both surfaces import and validate it through `WeaponCatalogJsonImporter`, filter to live definitions, project those definitions into the accepted EQP catalog shape, and delegate equipment generation to the existing BOX resolver and GEN service.

Preview-only weapon definitions are not eligible for normal box rolls. `TopBoxOnly` definitions are eligible only for Antimatter.

## Weapon-card resolution

Every generated result must resolve through the existing identity chain:

`EquipmentInstance.DefinitionId`
→ `EquipmentDefinition`
→ `RuntimeWeaponReferenceId`
→ `WeaponDefinitionData`

The projection fails closed when any link is missing or ambiguous. It does not fabricate fallback statistics.

The player-facing card contains:

- weapon display name with the Mark composed exactly once;
- the exact rolled equipment-quality label;
- archetype and damage type;
- damage;
- shots per second;
- DPS;
- pierce only above zero;
- projectile count only above one;
- one empty `◇` symbol for each definition-owned augment slot.

The card uses `WeaponDefinitionData.DamagePerProjectile`, `FireRate`, `TargetDps`, `Pierce`, and `ProjectilesPerTrigger`. It does not recalculate or rebalance combat values.

For example:

```text
FAMILY 000 MK I
COMMON

Hybrid · Kinetic

Damage: 0.12
Shots/sec: 2
DPS: 1.2
Pierce: 1

◇ ◇ ◇
```

A seven-projectile definition formats damage as:

```text
Damage: 0.12 × 7
Projectiles: 7
```

Internal equipment IDs, weapon IDs, raw quality IDs, fingerprints, and generation identities are available only in diagnostics foldouts or deterministic copy output.

## Quality versus definition rarity

The primary badge is the exact generated `EquipmentInstance.QualityId` resolved to the matching `EquipmentQualityTier.Label` owned by its `EquipmentDefinition`.

The weapon catalog's fixed definition rarity and drop-weight classification are not shown on the primary card. Definition rarity describes catalog/drop classification; equipment quality is the concrete rolled item result.

## Empty augment generation

Fresh strongbox-created equipment always has:

`EquipmentInstance.Augments.Count == 0`

All eleven production-shaped strongbox tiers now author a zero installed-augment roll. BOX still selects the exact weapon definition, item level, quality, and concrete equipment-instance identity through the existing deterministic path.

This is not a visual reinterpretation. No augment instances are created and then hidden.

Augment capacity remains definition-owned:

`EquipmentDefinition.MaximumAugmentSlots`

A definition with zero capacity shows no symbols. A definition with three slots shows:

`◇ ◇ ◇`

No per-instance capacity field is added.

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
6. BOX generates the exact empty equipment instance, quality, item level, and mandatory scrap; RAP applies the payload to MON/SCR/INV; BOX then consumes only the exact opened box.
7. Pending operations reuse the same opening identity. Repeated successful input returns an exact duplicate and cannot grant or consume twice.

The editor-only binding tier is unique per queued box so the strict RAP contract can retain an exact generated equipment-definition identity while the visual surface still presents the original production tier. No alternate reward sampler is introduced.

## Decision-preview flow

The decision-preview opener freezes one generated item and presents:

- **Keep / Accept** — submits the exact equipment instance to `PlayerHoldingsService`;
- **Sell** — does not add that preview equipment and increments simulator cash by exactly `1000`.

Keep and Sell are keyed by concrete equipment-instance identity. Repeated decisions cannot add the same item or cash twice.

The sale value remains explicitly temporary:

`TODO(ECONOMY): replace the temporary fixed sale value with the real item valuation service.`

This Keep/Sell interaction is not presented as the production transaction. The current real BOX authority applies rewards during `Open`. A production post-open Sell action needs its own exactly-once equipment disposition/valuation transaction.

## Eleven-tier balance

| Tier | Name | Mean level offset | Item SD | Installed augments | Scrap | Common / Rare / Exceptional |
|---:|---|---:|---:|---:|---:|---:|
| 1 | Steel | -6 | 5.2 | 0 | 5..10 | 82 / 17 / 1 |
| 2 | Copper | -4 | 5.0 | 0 | 8..16 | 78 / 20 / 2 |
| 3 | Silver | -2 | 4.7 | 0 | 12..24 | 72 / 25 / 3 |
| 4 | Amethyst | 0 | 4.4 | 0 | 18..36 | 64 / 31 / 5 |
| 5 | Gold | +2 | 4.1 | 0 | 25..50 | 55 / 37 / 8 |
| 6 | Black Opal | +4 | 3.8 | 0 | 35..70 | 46 / 42 / 12 |
| 7 | Blue Sapphire | +6 | 3.5 | 0 | 50..100 | 37 / 46 / 17 |
| 8 | Emerald | +8 | 3.2 | 0 | 70..140 | 29 / 49 / 22 |
| 9 | Alexandrite | +10 | 2.9 | 0 | 100..200 | 21 / 50 / 29 |
| 10 | Red Diamond | +12 | 2.6 | 0 | 150..300 | 14 / 48 / 38 |
| 11 | Antimatter | +14 | 2.2 | 0 | 250..500 | 7 / 40 / 53 |

Negative offsets are applied to an immutable effective player context because BOX V1's power-budget policy accepts only non-negative tier bonuses. The real player progression state is never mutated.

## Odds inspector

The Odds page performs deterministic repeated opens through BOX/GEN and reports:

- exact source weapon-definition frequency;
- canonical player-facing quality-label frequency;
- item-level delta relative to the selected player level;
- rejected or impossible rolls.

Reports implying boxes roll installed augment count, augment identity, augment tier, or augment level are retired from the visible simulator. The retained canonical diagnostics are expected to contain only the zero-installed-augment observation under this schema.

## Deterministic copy/export

The preview window can copy:

- the frozen queue canonical diagnostics;
- the compact primary card;
- the complete deterministic card projection;
- the complete odds diagnostics.

The authoritative prepared-box model also has canonical text and a SHA-256 fingerprint. Identical catalog content and identical inputs produce byte-identical generation results and card projections. Different concrete equipment instances of the same weapon definition retain different instance identities and card fingerprints.

## Current boundaries

- The imported simulator equipment projection currently assigns a definition-owned capacity of three slots to live weapon definitions because the weapon JSON does not yet contain a canonical per-definition augment-capacity field.
- The production starter catalog demonstrates canonical zero-capacity equipment.
- Augment selection, installation, replacement, and leveling remain outside strongbox generation and require a separate augment-management authority.
- The authoritative window uses editor-session MON/SCR/INV authorities; it proves the real service chain but does not persist to a player save.
- Mission Results-to-scene routing of exact collected boxes remains the responsibility of BOXUI-001.
- Box artwork and final production strongbox-opening presentation remain out of scope.
- Unity compilation, focused EditMode XML, and human simulator screenshots are required before the draft PR is ready.

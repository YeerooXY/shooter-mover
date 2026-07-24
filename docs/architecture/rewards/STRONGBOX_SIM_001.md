# STRONGBOX-SIM-001 — authoritative equipment strongbox simulator

## Status

This change establishes the engine-independent, equipment-generic batch-analysis boundary. It deliberately does not duplicate any production loot formula. Every generated observation must come from an `IStrongboxSimulationProductionGateway` backed by the real production opening resolver.

The current production hybrid resolver (`StrongboxHybridEquipmentGenerationResolverV1`) is the live authority used by real strongbox openings. At the time this document was written it selects only live weapon equipment and passes the production weapon augment capacities into `StrongboxHybridLootPolicyV1`. Wearable equipment is therefore a production-catalog/resolver limitation, not something the simulator may invent around. The simulator contracts already carry category, family, explicit slot, canonical tags and authored/absolute augment limits, so future production-supported equipment categories flow through without an equipment-ID switch or display-name parsing.

## Production authorities reused

The production gateway must delegate to the same authorities used by a real opening:

- `ProductionStrongboxCatalogV1` and `ProductionStrongboxHybridLootCatalogV1` for tier and hybrid policy identity;
- `StrongboxHybridLootPolicyV1.RollTargetLevel` for target-level and level-offset behavior;
- `StrongboxHybridLootPolicyV1.EvaluateDefinitionWeight` for anchored/peak affinity, rarity weighting and definition weighting;
- `StrongboxHybridLootPolicyV1.RollInstanceLevel` for item level;
- the production equipment catalog and weapon catalog projection for eligibility and stable identity;
- `StrongboxHybridEquipmentGenerationResolverV1` for final candidate selection, quality selection and equipment-instance construction;
- `StrongboxHybridLootPolicyV1.RollAugmentSignature` for ordinary and exceptional slot/level outcomes;
- `DeterministicRandom.CreateSubstream` and the resolver's production purpose IDs for deterministic random derivation;
- `GeneratedEquipmentAugmentSignatureAuthorityV1` for generated augment diagnostics;
- canonical production fingerprints wherever exposed.

The simulator owns counters, deterministic ordering, serialization and report fingerprinting only.

## Architecture

`StrongboxSimulationRequest` identifies the mode and one or two immutable scenarios. A scenario contains player level, exact strongbox tier ID, sample count, root seed, optional exact equipment definition ID and an explicitly named diagnostic override flag.

`IStrongboxSimulationProductionGateway` is the only generation seam. It exposes immutable production metadata and produces one immutable `StrongboxGeneratedEquipmentObservation` for an ordinal. The gateway is responsible for invoking production; the simulator cannot calculate target levels, weights, item levels, qualities, slot counts or augment levels itself.

`StrongboxBatchSimulator` streams observations and aggregates incrementally. It does not retain equipment instances. All maps are sorted explicitly before report construction and serialization. Report fingerprints exclude elapsed time, timestamps, machine identity and local paths.

## Equipment identity

Reports preserve:

- stable equipment definition ID;
- category ID;
- family ID;
- explicit slot ID when present;
- canonical tags;
- rarity ID;
- display name for presentation only.

Display names are never lookup or classification keys. When production lacks a reliable helmet/torso/leggings/boots discriminator, the gateway reports the missing identity as a catalog limitation instead of synthesizing a simulator-only classification.

## Ordinary and exceptional outcomes

Every metadata row carries the authored ordinary maximum and production-declared absolute maximum for both slots and shared augment level. Each observation carries the actual roll and explicit exceptional flags/diagnostic text when production exposes them.

The aggregator never clamps slot counts or augment levels. A production result beyond its own declared absolute maximum throws an invalid-production-result exception with the opening ordinal. Zero-slot items are excluded from conditional augment-level averages.

## Simulation modes

The contracts define:

1. full opening;
2. definition-conditioned generation;
3. comparison;
4. player-level sweep;
5. strongbox-tier sweep.

The current batch executor implements the two single-scenario modes. Comparison and sweep orchestration must call the same single-scenario executor with paired scenario ordinals and then compare immutable reports; they must not introduce a probability table or alternate RNG.

Definition-conditioned mode may bypass only final weighted definition selection. Its production gateway must still run target-level, item-level, quality, augment-slot, exceptional-slot, augment-level, exceptional-level, equipment-instance and diagnostic generation. Normal production eligibility remains enforced unless the explicitly named diagnostic override is enabled and recorded.

## Aggregation and report fields

The immutable report currently contains scenario identity, production fingerprints, generated/rejected counts, target-level distribution, item-level distribution, augment-slot distribution, augment-level distribution conditional on nonzero slots, combined slot/level signatures, per-equipment averages, exceptional counts, diagnostics and a deterministic report fingerprint.

The report model is intentionally dynamic: every observed slot count and augment level becomes a row. Levels 11 and 12 and exceptional third/fourth slots are preserved rather than clamped.

Markdown and CSV serializers use invariant culture, explicit ordering and RFC-style CSV escaping. Callers choose output paths; serializers never write into production `Assets` automatically.

## Zero-drop and rare-outcome interpretation

Zero observations do not prove impossibility. A gateway/catalog projection should distinguish unavailable, box-policy-excluded, hard-ineligible, eligible-but-unobserved and invalid/unmapped definitions. Rare-outcome diagnostics may add descriptive zero-event bounds and suggested sample sizes, but must never fabricate probability.

## Deterministic seed behavior

The same production snapshots, player level, tier, count, root seed, mode, exact definition filter and diagnostic override settings must produce the same report. Opening or generated-copy ordinal is passed unchanged to the production gateway. Independent runs may choose different root seeds.

## Balancing workflow

1. Build a production gateway from the same catalog/projection and resolver used by live opening.
2. Run at least 100,000 samples for baseline distributions.
3. Export Markdown plus focused CSV distributions.
4. Repeat with paired adjacent levels or tiers.
5. Investigate flagged cliffs or inversions in production policy; never patch simulator formulas.
6. Use one-million or multi-million runs for outcomes expected around 1 in 100,000 or rarer.

## Illustrative examples

All numbers below are fictional and are not production results.

### 100,000 full openings

A fictional report could show 100,000 generated items, target-level mean 18.4, average 1.62 slots and 0.006% exceptional slots.

### Legendary weapon, level 16 versus 17

A fictional paired run could observe 300/100,000 at level 16 and 4,000/100,000 at level 17: +3,700 drops, +3.70 percentage points, 13.33x observed rate, about 1 in 333 versus 1 in 25.

### Exact helmet, 100,000 copies

A fictional definition-conditioned run could report 100,000 copies, average item level 22.7, 41% zero slots, 0.03% exceptional third slots and 0.002% level 12. This becomes valid only after the production resolver supports that helmet.

### Weapon ordinary maximum 3, exceptional fourth

A fictional weapon may declare ordinary maximum 3 and absolute maximum 4. A four-slot observation is classified as exceptional and retained as four.

### Gear ordinary maximum 2, exceptional third

A fictional boot definition may declare ordinary maximum 2 and absolute maximum 3. A three-slot observation is exceptional and is not reinterpreted as two.

### Ordinary augment maximum 10, rare levels 11 and 12

A fictional policy may declare ordinary maximum 10 and absolute maximum 12. Levels 11 and 12 remain separate report rows and over-cap outcomes.

### One-million-opening rare analysis

A fictional one-million run observing 7 events reports 7/1,000,000. A zero count reports zero observed events and sample size; it does not claim impossibility.

### Player-level sweep

A fictional level 1–40 sweep may flag a +2.5 percentage-point adjacent-level acquisition cliff using a caller-configured threshold.

### Strongbox-tier sweep

A fictional tier sweep may show Tier 5 with higher rarity but lower average slots than Tier 4. The report flags the inversion but does not change policy or declare it invalid automatically.

## Scope exclusions

No Unity references, MonoBehaviours, ScriptableObjects, scene discovery, Stage 1 changes, inventory/account mutation, save writes, achievements, analytics, firing/runtime behavior, alternate random service, alternate catalog, alternate rarity authority or alternate augment authority belong in the simulator core.

## Static validation notes

The simulator core contains no loot formula, rarity/weight formula, item-level formula, slot/level formula, `System.Random`, `UnityEngine`, inventory mutation, account mutation, display-name classification, equipment-ID behavior switch, fixed slot-count branch or fixed augment-level branch. No automated tests were added under the current prototype policy.

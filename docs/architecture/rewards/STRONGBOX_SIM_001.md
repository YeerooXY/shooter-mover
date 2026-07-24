# STRONGBOX-SIM-001 — authoritative equipment strongbox simulator

## Status

This change establishes the engine-independent, equipment-generic batch-analysis boundary. It deliberately does not duplicate any production loot formula. Every generated observation comes from an `IStrongboxSimulationProductionGateway` backed by the real production opening resolver.

The current production hybrid resolver (`StrongboxHybridEquipmentGenerationResolverV1`) is the live authority used by real strongbox openings. It currently selects only live weapon equipment and passes the production weapon augment capacities into `StrongboxHybridLootPolicyV1`. Wearable equipment is therefore a production-catalog/resolver limitation, not something the simulator may invent around. The simulator contracts already carry category, family, explicit slot, canonical tags and authored/absolute augment limits, so future production-supported equipment categories flow through without an equipment-ID switch or display-name parsing.

## Production authorities reused

The production gateway delegates to the same authorities used by a real opening:

- `ProductionStrongboxCatalogV1` and `ProductionStrongboxHybridLootCatalogV1` for tier and hybrid policy identity;
- `StrongboxHybridLootPolicyV1.RollTargetLevel` for target-level and level-offset behavior;
- `StrongboxHybridLootPolicyV1.EvaluateDefinitionWeight` for anchored/peak affinity, rarity weighting and definition weighting;
- `StrongboxHybridLootPolicyV1.RollInstanceLevel` for item level;
- the production equipment catalog and weapon catalog projection for eligibility and stable identity;
- `StrongboxHybridEquipmentGenerationResolverV1` for final candidate selection, quality selection and equipment-instance construction;
- `StrongboxHybridLootPolicyV1.RollAugmentSignature` for ordinary and exceptional slot/level outcomes;
- `DeterministicRandom.CreateSubstream` and the resolver's production purpose IDs for deterministic random derivation;
- `EquipmentInstance.Create` for generated equipment identity;
- `GeneratedEquipmentAugmentSignatureAuthorityV1` for generated augment metadata.

## Canonical editor composition

`AuthoritativeStrongboxSimulationGatewayFactoryV1` imports the exact production weapon catalog, reuses the resulting production equipment projection and builds simulator metadata from the definitions consumed by the hybrid resolver. Callers do not hand-author eligibility, family, rarity, first appearance, peak level, base weight, TopBoxOnly state or augment limits.

The factory also freezes deterministic fingerprints for the catalog projection and all tier hybrid-policy fingerprints. Unsupported live rarity values, duplicate identities and empty projections fail explicitly.

`AuthoritativeStrongboxSimulationRunnerV1` is the small end-to-end invocation surface for ordinary full-opening requests. It performs catalog loading, canonical projection, gateway construction, streaming simulation and structural report validation as one explicit operation.

## Production gateway

`AuthoritativeStrongboxSimulationProductionGatewayV1` is an editor-side adapter over `AuthoritativeStrongboxSimulatorRuntimeV1`. It executes:

`StrongboxOpeningServiceV1` → production reward generation → transactional payload resolution → `StrongboxHybridEquipmentGenerationResolverV1` → RAP → isolated holdings.

The gateway prepares a bounded chunk of 256 real openings inside one disposable composition. Each observation is streamed from that composition, and the complete holdings, wallet, RAP and generated-signature state is discarded before the next chunk. This avoids rebuilding the production composition for every opening while preventing unbounded simulator inventory retention.

No player-owned inventory, account, save, progression, achievement or analytics authority is supplied to the gateway.

After an opening applies, the gateway reads the exact generated `EquipmentInstance` and committed `GeneratedEquipmentAugmentSignatureV1`. It replays `RollTargetLevel` and `RollAugmentSignature` through the same production policy using the exact opening seed and ordinal. The replay is observational: its slot, shared level, policy ID and policy fingerprint must match the committed production signature or the observation is rejected. Policy lookup or replay exceptions become deterministic rejection diagnostics rather than escaping from the batch.

The current production resolver has no supported definition-conditioning seam. A scenario containing `EquipmentDefinitionId` is therefore rejected explicitly. The gateway does not imitate the resolver's private weighted selection or use rejection sampling to counterfeit conditioned probabilities.

## Architecture

`StrongboxBatchSimulator` is a streaming analysis consumer. It receives one immutable `StrongboxGeneratedEquipmentObservation` per opening/copy ordinal and updates deterministic counters. It does not retain generated equipment instances.

`StrongboxSimulationCoordinator` owns paired comparison, player-level sweep and tier-sweep orchestration. It runs ordinary single-scenario reports through the same gateway and compares immutable results. Diagnostic thresholds are configurable and never alter generation policy.

## Deterministic identity

A scenario includes player level, exact tier ID, sample count, root seed, optional exact definition ID and diagnostic override state. The gateway deterministically derives a bounded-session seed from the scenario seed and chunk start; the production runtime then derives each exact opening seed from its stable local ordinal. Chunk size is fixed by the gateway implementation and therefore participates in the production observation identity.

The report fingerprint includes request identity, production fingerprints, generated/rejected counts, ordered distributions, per-definition counts, exact per-definition augment-signature distributions and deterministic diagnostics. Elapsed time, timestamps, machine identity, paths and processor count are excluded.

## Ordinary and exceptional outcomes

Each observation carries ordinary authored and absolute attainable slot/augment limits. Actual slot counts and shared augment levels are aggregated dynamically.

Results above ordinary maxima are preserved and classified as exceptional. They are never clamped. Results beyond production-declared absolute maxima are reported as invalid production results with the opening ordinal.

Zero-slot items are excluded from conditional augment-level averages. Levels 11 and 12 remain separate values.

## Zero-drop interpretation

`StrongboxSimulationDiagnostics.BuildCatalogCoverage` compares a completed report with the gateway's deterministic equipment projection.

A zero count may be labelled unavailable when production metadata explicitly says so. Otherwise it remains eligible-but-not-observed or a catalog limitation. Zero observations never prove mathematical impossibility.

TopBoxOnly compatibility is not inferred from a tier stable ID. Until the production gateway exposes explicit tier-role metadata, an unobserved TopBoxOnly definition is reported as a catalog/gateway limitation rather than falsely classified as excluded.

## Rare-outcome diagnostics

`StrongboxRareOutcomeQuery` supports exact definition filters, minimum slots, above-ordinary slots, minimum augment level and above-ordinary augment level.

When zero events are observed, the result may expose the descriptive rule-of-three 95% upper bound (`3 / sample count`). This is a bound, not an invented production probability. When an expected probability is supplied, the suggested sample size is `ceil(3 / expected probability)`.

Exact combined slot-and-level queries use each definition's measured combined-signature distribution. Independent slot and level marginals are never multiplied.

## Report validation

`StrongboxSimulationReportValidator` performs deterministic structural validation without rerunning production generation. It checks requested/generated/rejected totals, distribution totals and percentages, ordering and duplicate keys, equipment totals, exceptional counters and combined-signature formatting. Consumers may inspect sorted diagnostics or use `ThrowIfInvalid` at an export boundary.

## Simulation modes

- Full opening: every ordinal calls the complete production-backed gateway path.
- Definition conditioned: represented by the core contracts, but explicitly rejected by the current gateway until production exposes a supported conditioned-selection boundary.
- Comparison: reports absolute, percentage-point, relative-rate, boxes-per-result and quality differences.
- Player-level sweep: evaluates an inclusive level range with configurable cliff and regression diagnostics.
- Tier sweep: evaluates caller-supplied production tier IDs and flags suspicious inversions without declaring them invalid.

## Report formats

The immutable in-memory report is the source for deterministic Markdown and CSV serializers. Serializers do not automatically write into production `Assets`.

The serializer surface includes the main report, catalog-coverage diagnostics, rare-outcome diagnostics and exact per-equipment augment-signature tables. Missing optional values are emitted as empty deterministic fields rather than locale-specific placeholders.

## Illustrative examples

All numbers below are fictional and are not production results.

- 100,000 full openings may observe one rare definition 37 times, approximately one observed copy per 2,703 boxes.
- A fictional legendary weapon may appear 300/100,000 times at level 16 and 4,000/100,000 at level 17, a +3.70 percentage-point and 13.33x observed-rate change.
- A weapon with ordinary maximum three slots and production absolute maximum four preserves a four-slot result as exceptional.
- Gear with ordinary maximum two and production absolute maximum three preserves a three-slot result as exceptional once production supports that category.
- With ordinary augment maximum 10 and production absolute maximum 12, levels 11 and 12 remain separately reported.
- A combined query can measure the exact count of four-slot, level-12 outcomes for one definition without assuming slot and level independence.
- A one-million-opening run with zero rare events reports zero observations and an optional descriptive upper bound, not impossibility.
- Player-level and tier sweeps may flag cliffs or inversions while leaving production policy unchanged.

## Scope exclusions

The simulator does not mutate any player-owned inventory/account/progression authority, write saves, unlock achievements, emit live analytics, invoke presentation, modify scenes, execute weapons, or introduce another random service. The production gateway does exercise disposable simulation-owned holdings and RAP authorities because those are part of the real opening transaction boundary.

No automated tests are added under the current prototype policy. Unity compilation has not been run in the connector-only environment.

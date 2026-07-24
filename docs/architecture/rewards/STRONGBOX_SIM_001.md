# STRONGBOX-SIM-001 — authoritative equipment strongbox simulator

## Status

This change establishes the engine-independent, equipment-generic batch-analysis boundary. It deliberately does not duplicate any production loot formula. Every generated observation comes from an `IStrongboxSimulationProductionGateway` backed by the real production opening resolver.

The current production hybrid resolver (`StrongboxHybridEquipmentGenerationResolverV1`) is the live authority used by real strongbox openings. It currently selects only live weapon equipment and passes the production weapon augment capacities into `StrongboxHybridLootPolicyV1`. Wearable equipment is therefore a production-catalog/resolver limitation, not something the simulator may invent around.

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

`AuthoritativeStrongboxSimulationProductionGatewayV1` executes:

`StrongboxOpeningServiceV1` → production reward generation → transactional payload resolution → `StrongboxHybridEquipmentGenerationResolverV1` → RAP → isolated holdings.

The gateway prepares a bounded chunk of 256 real openings inside one disposable composition. Each observation is streamed from that composition, and the complete holdings, wallet, RAP and generated-signature state is discarded before the next chunk.

No player-owned inventory, account, save, progression, achievement or analytics authority is supplied to the gateway.

The current production resolver has no supported definition-conditioning seam. A scenario containing `EquipmentDefinitionId` is therefore rejected explicitly before sampling. The gateway does not imitate the resolver's private weighted selection or use rejection sampling to counterfeit conditioned probabilities.

## Deterministic aggregation

`StrongboxBatchSimulator` is a streaming analysis consumer. It receives immutable production observations and updates deterministic counters without retaining generated equipment instances.

Exact augment-bias observations are represented by their IEEE-754 bit keys and counted in ordinally sorted distributions. `AverageAugmentBias` is never accumulated in opening order. After sampling completes, `StrongboxSimulationBiasMath` decodes the sorted exact distribution and performs one canonical grouped sum in that order. The same helper is used by report construction, report validation, paired comparisons and sweep bias diagnostics. Zero-count averages are exactly `0d`; malformed, duplicate, unsorted, NaN and infinite bias keys fail explicitly.

This rule makes the published average independent of observation arrival order whenever the counted bias distribution is identical.

## Canonical metadata tags

`StrongboxEquipmentMetadata` copies and sorts canonical tags by stable identity. Null and duplicate tags are rejected at construction. Report fingerprinting appends every canonical tag in that normalized order, so caller iteration order cannot change report identity while different tag content does.

## Deterministic identity

A scenario includes player level, exact tier ID, sample count, root seed, optional exact definition ID and diagnostic override state.

The report fingerprint includes complete request identity, all production fingerprint fields, generated/rejected counts, counted diagnostics, all global distributions and exceptional values, all per-equipment metadata and distributions, exact average bit patterns, and canonical tags. Elapsed time, timestamps, machine identity, paths and processor count are excluded.

## Ordinary and exceptional outcomes

Each observation carries ordinary authored and absolute attainable slot/augment limits. Actual slot counts and shared augment levels are aggregated dynamically.

Results above ordinary maxima are preserved and classified as exceptional. They are never clamped. Results beyond production-declared absolute maxima are reported as invalid production results.

Zero-slot items are excluded from conditional augment-level averages. Levels 11 and 12 remain separate values.

## Zero-drop interpretation

`StrongboxSimulationDiagnostics.BuildCatalogCoverage` compares a completed report with the gateway's deterministic equipment projection.

Coverage uses exact production tier identities and concrete TopBoxOnly gating. Stable IDs are not treated as an ordered tier scale. Where production exposes no inspectable eligibility boundary, diagnostics state that limitation rather than inventing a formula.

## Report validation

`StrongboxSimulationReportValidator` recomputes the complete report fingerprint and verifies requested/generated/rejected totals, counted diagnostics, distribution totals and ordering, quality and bias representations, per-equipment totals, exact signature reconciliation, ordinary/absolute exceptional thresholds, canonical tag ordering, deterministic averages and all exceptional counters.

Bias-average validation calls the same canonical helper used during construction and compares the exact double bit pattern.

## Simulation modes

- Full opening: every ordinal calls the complete production-backed gateway path.
- Definition conditioned: represented by the contracts, but explicitly rejected before sampling until production exposes a supported conditioned-selection boundary.
- Comparison: reports deterministic differences over complete immutable reports.
- Player-level sweep: evaluates an inclusive level range with configurable diagnostics.
- Tier sweep: evaluates caller-supplied production tier IDs without inferring ID ordering.

## Scope exclusions

The simulator does not mutate player-owned inventory, account, progression, currency, saves, achievements, analytics, scenes or gameplay state. It does not introduce another reward, catalog, rarity, generation, augment or random authority.

## Validation status

Unity compilation and the deterministic in-editor production smoke simulation could not be run from the connector-only environment because no Unity-capable runner or connector-visible CI check is available.

A manual IEEE-754 exercise used multiple fractional values and values with strongly different magnitudes. Different observation orders produced different opening-order averages, while the canonical grouped/sorted distribution produced the same exact bit pattern for every equivalent ordering. This validates the repaired construction rule independently of Unity integration.

Static review confirms that bias construction, validation, comparisons and sweep diagnostics share one canonical sorted-distribution helper, canonical tags are normalized before publication, DefinitionConditioned remains rejected before sampling, and report fingerprint recomputation covers both average bias bits and tag content.

No automated tests are added under the current prototype policy.

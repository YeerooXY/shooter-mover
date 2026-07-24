# STRONGBOX-SIM-001 — authoritative equipment strongbox simulator

## Status

This change establishes an analysis-only, equipment-generic batch simulator. It does not duplicate any production loot formula. Every generated observation comes from an `IStrongboxSimulationProductionGateway` backed by the real production opening resolver.

The current production hybrid resolver (`StrongboxHybridEquipmentGenerationResolverV1`) selects live weapon equipment. Wearable equipment is therefore a production resolver limitation, not something the simulator may invent around. The simulator already carries category, family, slot, tags, rarity, availability, first appearance, peak level, authored weight, TopBoxOnly and augment-limit metadata for future production-supported categories.

## Authoritative production route

Every simulated opening preserves the real route:

`StrongboxOpeningServiceV1`
→ production reward generation
→ `TransactionalStrongboxGrantPayloadResolverV1`
→ `DeterministicStrongboxGrantPayloadResolverV1`
→ `StrongboxHybridEquipmentGenerationResolverV1`
→ RAP
→ isolated simulator holdings

The gateway prepares bounded chunks of 256 openings inside disposable compositions. Holdings, wallets, RAP state and generated augment signatures are discarded between chunks. No player-owned inventory, account, progression, currency, save, achievement, analytics, scene or gameplay authority is supplied.

After each opening, the gateway reads the committed `EquipmentInstance` and `GeneratedEquipmentAugmentSignatureV1`. It observationally replays the production target/signature policy with the exact opening context. Capacity, shared-level, policy-ID or policy-fingerprint mismatches reject the observation explicitly.

## Canonical production projection

`AuthoritativeStrongboxSimulationGatewayFactoryV1` derives simulator metadata from the exact catalogs consumed by production. Callers do not hand-author eligibility, rarity, family, first appearance, peak level, authored weight, TopBoxOnly state or augment limits.

Rarity weighting, item-level rolls, augment-slot rolls and augment-level rolls are currently all owned by `StrongboxHybridLootPolicyV1`. The corresponding named fields in `StrongboxProductionFingerprints` therefore expose the same hybrid-policy authority fingerprint. They are named projections, not claims that four independent production authorities exist.

## Simulation modes

### Full opening

Full-opening mode executes real equipment selection and measures both exact drop frequency and the resulting copy quality.

### Definition conditioned

A genuine definition-conditioned mode would bypass only equipment selection and then measure quality after the exact requested definition had been selected.

Production currently exposes no legitimate conditioned-selection seam. Until it does:

- `AuthoritativeStrongboxSimulationRunnerV1` rejects the mode before gateway creation and before sampling;
- `StrongboxBatchSimulator` rejects direct conditioned calls before its sample loop;
- comparisons and sweeps reject conditioned scenarios before running;
- no normal zero-generated report is created or validated;
- no rejection sampling, private weight copying or full-opening fallback is used.

The stable unsupported diagnostic is `strongbox-simulation-definition-conditioned-unsupported`.

## Published report

Global reporting includes:

- generated and rejected counts;
- counted rejection diagnostics;
- target-level, item-level and quality distributions;
- augment-slot and conditional augment-level distributions;
- exact slot-and-level signatures;
- exact augment-bias distributions using IEEE-754 bit keys;
- exceptional slot, exceptional augment-level and combined exceptional counts and percentages.

Per-equipment reporting includes counts, item level, quality, slot, augment-level, exact signature and exact bias distributions, deterministic averages, and all three exceptional count/percentage classes.

Zero-slot copies are excluded from conditional augment-level totals and averages. Exceptional outcomes are preserved rather than clamped. Output beyond production-declared absolute limits fails with a stable integrity diagnostic instead of becoming balancing data.

## Deterministic identity

The canonical report fingerprint includes:

- complete primary and optional comparison request identity;
- mode, level, exact tier, sample count, seed, optional definition and diagnostic override;
- every production fingerprint field;
- generated/rejected counts and counted diagnostics;
- every global distribution and exceptional aggregate;
- every published per-equipment metadata value, count, average, distribution, exact signature and exceptional aggregate.

Floating-point identity uses invariant IEEE-754 bit representations rather than formatted decimal text. Ordering is canonical and deterministic.

Comparison and sweep outputs have separate fingerprints containing ordered report fingerprints, scenario identity, metrics and warnings.

## Validation

`StrongboxSimulationReportValidator` recomputes the complete report fingerprint rather than trusting the stored value. It also validates:

- generated plus rejected equals requested;
- all global and per-equipment distribution totals and ordering;
- nonzero-slot augment-level totals;
- exact signature reconciliation with slot and augment-level marginals;
- exceptional counts against exact signatures and production-projected ordinary/absolute limits;
- quality and exact bias totals;
- per-equipment averages;
- counted diagnostic totals against rejected count;
- global exceptional totals against per-equipment totals.

Invalid production output or report corruption fails explicitly through `StrongboxSimulationIntegrityException`.

## Catalog coverage

Coverage diagnostics use inspectable production facts only. They resolve the exact requested tier through `ProductionStrongboxCatalogV1`; tier ordering is never inferred from `StableId` values.

Definitions may be classified as observed, eligible-but-not-observed, unavailable, excluded by concrete TopBoxOnly gating, unsupported because the production eligibility boundary is not inspectable, or requiring diagnostic override. The simulator does not evaluate or copy selection weights to produce these classifications.

## Comparisons and sweeps

Comparisons include generation/rejection rates, item/slot/level/bias quality, all exceptional rates, dynamic quality percentages and counted diagnostic differences.

Player-level and tier sweeps preserve the full report at every point, fingerprint ordered entries, and surface generation cliffs, rejection cliffs, quality regressions, combined-exception regressions and suspicious tier inversions. Diagnostic thresholds never change production behavior.

## Report formats

Deterministic Markdown and CSV serializers publish the complete report summary, quality and exact bias distributions, exceptional outcomes and counted diagnostics. Separate serializers retain exact per-equipment slot-and-level signatures. Serializers never automatically write into production `Assets`.

## Scope exclusions

The simulator does not mutate real gameplay state, write saves, unlock achievements, emit live analytics, modify scenes, execute weapons, introduce another random service, counterfeit wearable generation or become a competing opening/reward authority.

## Validation status

Unity compilation and an in-editor deterministic smoke simulation could not be executed from the connector-only environment. No GitHub status checks are currently attached to the branch.

The required smoke run remains:

- generated plus rejected equals requested;
- identical request and seed produce the same report fingerprint;
- a different seed changes the report when outcomes differ;
- diagnostic counts reconcile with rejected count;
- global and per-equipment distributions reconcile;
- report fingerprint validation succeeds;
- conditioned mode fails before sampling;
- no real gameplay state is mutated.

No automated tests were added under the prototype policy.

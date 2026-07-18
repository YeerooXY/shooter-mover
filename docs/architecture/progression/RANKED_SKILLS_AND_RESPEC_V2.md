# Ranked Skills and Respec V2

## Scope

This foundation extends the existing skill progression contracts without replacing the V1 tree and category-gate API. It supplies compact fixtures only; the final balanced catalog remains a separate content task.

## Stable contracts

`RankedSkillCatalogV2` is schema- and content-versioned. Skill, category, class, synergy, stat, condition, currency, profile, and operation identities are stable strings and are never inferred from display names.

A shared generic skill is defined once. `SkillClassOverrideV2` changes its effective rank cap and optional rank-value curve for a class. The sample catalog proves:

- `generic.armor`: Striker/Combat Medic 6, Juggernaut 18;
- `generic.movement_speed`: Striker 18, Combat Medic 6, Juggernaut 9;
- a 15-rank Striker skill;
- rank milestones;
- prerequisites;
- the derived 8/8 third-movement-charge synergy.

## Point and allocation authority

One point is earned per player level. The authority derives availability as `playerLevel - allocatedPoints`; no spendable-point balance is persisted. Allocation always adds exactly one rank and increments the immutable allocation version.

Operation IDs are replay keys. Repeating the byte-equivalent command returns the original accepted or rejected result. Reusing an operation ID with a different command fingerprint is rejected as a conflict. Stale expected versions are rejected before mutation.

Persisted allocation state contains profile identity, class identity, allocation version, schema/content versions, rank mapping, and replay receipts required by the persistence adapter. Derived armor, movement speed, charge capacity, milestones, and synergy flags are never persisted.

## Effect projection and stacking

`SkillEffectProjectorV2` rebuilds contributions from current ranks on every accepted allocation, migration, or respec. It does not mutate character, equipment, augment, movement, ability, or weapon definitions.

Unconditional stacking order is deterministic:

1. sum flat and integer-capacity contributions;
2. add the flat result to the base value;
3. sum percentage contributions and multiply by `1 + percentageSum`;
4. multiply multiplicative contributions in canonical source order.

Conditional descriptors remain explicit contributions for the consuming runtime to evaluate against its own stable condition facts. Milestones and synergies are source-labelled contributions.

The Striker synergy is active only while both ranks are at least eight:

- `striker.thruster_recovery >= 8`;
- `striker.movement_efficiency >= 8`;
- contribution: `movement.maximum_charges +1`.

There is no permanent unlock flag. Removing either prerequisite removes the contribution on the next projection.

## Respec boundary

`SkillRespecOrchestratorV2` owns orchestration, not currency. It receives:

- `ISkillRespecCostPolicyV2` for an exact cost;
- `ISkillRespecPaymentAuthorityV2` as an adapter over the existing credit/money authority.

A quote binds profile identity, allocation version, allocated-point count, exact cost, currency identity, and payment-state fingerprint. Execution rejects stale allocation, cost, or payment state before charging.

Successful execution charges once through the payment authority, atomically replaces the allocation with an empty snapshot at the next version, refunds all points by making them immediately unallocated, rebuilds effects, and returns an immutable receipt. Failed payment leaves allocation unchanged. The payment adapter must itself provide exactly-once charge semantics using the supplied operation ID.

## Runtime reconciliation

After a respec, the next effective snapshot is authoritative:

- losing the Striker synergy returns maximum movement charges from three to the base two;
- current charges are clamped to the new maximum;
- removed passive contributions disappear immediately;
- future ability activations capture the new snapshot;
- already-active timed abilities retain parameters captured at activation until they end.

The captured-activation policy avoids retroactively changing active durations, damage ticks, or costs while still making all future activations deterministic.

## Migration

`SkillAllocationMigratorV2` deterministically handles removed/unknown skills, reduced caps, and class eligibility changes. Invalid or excess ranks are returned as `RefundedPoints` with explicit diagnostics; they are never silently destroyed. Migration increments allocation version and updates schema/content versions. A caller may reject migration instead of accepting refunds when product policy requires stricter handling.

## Validation

Catalog construction validates duplicate IDs, positive ranks, unique class overrides, value-curve lengths, prerequisite references, prerequisite cycles, milestone bounds, synergy IDs, and satisfiable synergy rank requirements. Effects validate stable stat IDs and multiplicative numeric ranges.

## Ownership and integration

Responsibilities remain separate:

- domain definitions and validation: `RankedSkillFoundationV2.cs`;
- allocation authority, respec orchestration, payment contracts, migration, reconciliation: `RankedSkillAuthorityV2.cs`;
- engine-independent NUnit coverage: `RankedSkillFoundationV2Tests.cs`.

No UI or final production catalog is included.

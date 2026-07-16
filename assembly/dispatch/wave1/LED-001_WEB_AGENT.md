# LED-001 — Typed idempotent ledger primitive

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: LED-001
Branch: agent/led-001-idempotent-ledger
Exact base commit: 0e678a9333956aa29ba2e3598265c8e1a4122e72
PR base: main
Dependencies already merged: ADR-001 through PR #132

Create a fresh branch from the exact base and open a draft PR.

Objective

Implement one engine-independent typed ledger primitive supplying exact-once
transaction semantics, conflict detection, optional sequence admission,
immutable snapshots, deterministic fingerprints, and validated snapshot import.
Money, scrap, and holdings will compose this primitive later while retaining
separate public authorities.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/contracts/STABLE_ID_V1.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- docs/architecture/FILE_OWNERSHIP.md
- the LED-001 section of
  docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Economy/Ledger/**
- Assets/ShooterMover/Tests/EditMode/Economy/Ledger/**
- docs/architecture/contracts/IDEMPOTENT_LEDGER_V1.md
- inseparable leaf metadata inside those exact subtrees

Forbidden

- money, scrap, holdings, rewards, shops, boxes, crafting, upgrades, UI, scenes
- Unity adapters, persistence files/backend, serialization package additions
- shared asmdefs, assembly/context/**, assembly/generated/**, assembly/dispatch/**
- ProjectSettings/** and Packages/**

Required behavior

1. Generic/typed entry vocabulary prevents one product authority from accepting
   another authority's entry type accidentally.
2. Every mutation carries a stable transaction ID and deterministic payload
   fingerprint.
3. Exact repeat returns DuplicateNoChange with the original accepted result.
4. Reusing a transaction ID with changed payload, entry type, target, or
   expected sequence returns a conflicting-duplicate rejection.
5. Optional expected-sequence admission rejects stale commands without mutation.
6. Validation happens before mutation.
7. Debit/bounded-removal policy is supplied explicitly by the composing
   authority; the primitive does not invent money, scrap, capacity, or item
   semantics.
8. Snapshots are immutable, canonically ordered, sequence-bearing, and
   fingerprinted deterministically.
9. Snapshot import validates schema/version, sequence, duplicate transaction
   records, canonical identities, quantities/entries, and fingerprint before
   replacing state.
10. Import failure leaves existing state unchanged.
11. The primitive has no Unity, scene, UI, file-system, save-backend, random,
    clock, locale, or ambient static-state dependency.
12. Public results distinguish applied, duplicate no-change, conflicting
    duplicate, sequence conflict, validation rejection, and policy rejection.

Design constraint

The ledger owns exact-once mutation mechanics, not cross-authority atomic reward
application. RAP-001 remains the later aggregate coordinator. Do not implement a
wallet, inventory, or universal untyped currency bag.

Required proof

- first credit/add applies
- exact duplicate credit/add is no-change
- conflicting duplicate rejects
- accepted debit/removal applies
- rejected debit/removal does not mutate
- duplicate rejected command behavior is deterministic and documented
- expected sequence succeeds/fails correctly
- immutable snapshots cannot mutate ledger state
- canonical ordering/fingerprint is independent of insertion order
- snapshot round trip preserves state, sequence, and accepted transaction facts
- corrupt/unsupported/conflicting snapshot import rejects atomically
- large representative ledger remains deterministic
- two distinct typed ledgers cannot accidentally exchange entries
- no UnityEngine reference

Validation

- Add comprehensive EditMode tests.
- Run repository layout and assembly graph validation.
- Run focused Unity EditMode tests and cold compile when available.
- If Unity is unavailable, leave the PR draft and state the exact pending proof.

Acceptance

MON-001, SCR-001, and INV-001 can compose the same exact-once primitive without
sharing their public authority or product validation.

PR body

Record task ID, exact base/dependency, changed paths, public semantics, snapshot
format/version, tests, pending proof, limitations, and rollback.

Non-goals

No money/scrap meaning, holdings model, reward lifecycle, persistence backend,
Unity adapter, UI, shop, crafting, scene, or cross-authority transaction.
```

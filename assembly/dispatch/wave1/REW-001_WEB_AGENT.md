# REW-001 — Reward and economy contracts v1

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: REW-001
Branch: agent/rew-001-reward-economy-contracts
Exact base commit: 0e678a9333956aa29ba2e3598265c8e1a4122e72
PR base: main
Dependencies already merged: ADR-001 through PR #132

Create a fresh branch from the exact base and open a draft PR.

Objective

Define the immutable reward/economy vocabulary used later by drops,
strongboxes, shops, crafting, pickups, reward application, money, scrap, and
holdings. The model must represent money-only, strongbox-only, miscellaneous,
equipment-reference, mixed, and no-drop outcomes without product-specific
switch statements.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/authoring/PLACED_OBJECT_LIFECYCLE_V1.md
- docs/architecture/contracts/STABLE_ID_V1.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- docs/architecture/FILE_OWNERSHIP.md
- the REW-001 section of
  docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Rewards/Model/**
- Assets/ShooterMover/Runtime/Contracts/Rewards/**
- Assets/ShooterMover/Runtime/Contracts/Economy/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Contracts/**
- docs/architecture/contracts/REWARDS_V1.md
- docs/architecture/contracts/ECONOMY_TRANSACTIONS_V1.md
- inseparable leaf metadata inside those exact subtrees

Forbidden

- wallets, ledgers, holdings implementations, generation algorithms, random
  sampling, reward application, pickups, shops, boxes, crafting, or upgrades
- equipment-domain implementation owned by EQP-001
- Unity adapters, ScriptableObjects, scenes, prefabs, Stage 1, shared asmdefs
- assembly/context/**, assembly/generated/**, assembly/dispatch/**
- ProjectSettings/** and Packages/**

Required model

1. Use StableId for source, operation, commitment, grant, transaction, content,
   currency, item, box, and equipment-definition references where applicable.
2. Keep commands/results immutable and engine-independent.
3. Represent:
   - guaranteed entries;
   - independent chance rolls;
   - exclusive weighted groups;
   - explicit no-drop;
   - quantities/ranges and scaling input descriptors;
   - money, scrap, strongbox, equipment-reference, premium-ammo, and generic
     miscellaneous grants;
   - mixed reward sets;
   - explainable trace entries;
   - strongbox opening request/result envelopes without implementing opening.
4. Define explicit source override modes at least for:
   - inherit/default;
   - no reward;
   - replace entirely;
   - append guaranteed entries.
5. Do not depend on EQP-001 concrete types. Equipment grants reference stable
   definition/instance vocabulary so both parallel tasks remain independent.
6. Define duplicate-safe transaction command/result vocabulary:
   - applied;
   - exact duplicate/no change;
   - conflicting duplicate;
   - invalid request;
   - insufficient value/capacity where later authorities need it;
   - expected-sequence conflict.
7. Reusing an operation/transaction ID with a changed payload must be
   representable as a conflict, not silently treated as an exact duplicate.
8. Canonical ordering and deterministic fingerprints must not depend on culture,
   dictionary order, Unity, ambient time, or random state.
9. Contracts must not select production probabilities, quantities, prices,
   tiers, item lists, or balance values.

Required proof

- money-only profile is representable
- strongbox-only profile is representable
- misc/premium-ammo-only profile is representable
- mixed profile is representable
- guaranteed plus independent plus exclusive groups coexist
- explicit no-drop is distinct from an empty accidental configuration
- inherit, no-reward, append-guaranteed, and replace-entirely resolve
  deterministically
- malformed weights, probabilities, quantities, duplicate grant IDs, and
  conflicting identities are rejected
- canonical ordering/fingerprint is stable across input order
- exact duplicate and conflicting duplicate vocabulary is unambiguous
- no dependency on EQP-001 implementation or UnityEngine

Validation

- Add comprehensive EditMode tests.
- Run repository layout and assembly graph validation.
- Run focused Unity EditMode tests and cold compile when available.
- If Unity is unavailable, leave the PR draft and state the exact missing proof.

Acceptance

Every Wave 2/3 reward product can depend on one vocabulary without inventing a
private DTO hierarchy. No authority or random behavior is implemented.

PR body

Record task ID, exact base/dependency, changed paths, model examples, validation,
pending proof, limitations, and rollback.

Non-goals

No random implementation, ledger, wallet, holdings, reward lifecycle, generator,
Unity assets, pickup, shop, crafting, strongbox runtime, or balance.
```

# MON-001 - Money wallet

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: MON-001
Branch: agent/mon-001-money-wallet
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- REW-001 through 06012ea116c1b8bd1f087a5f9275079d5fd882bd
- LED-001 through 95a19fbf60fe81c443ad1a366422bf67d17d953e

Create a fresh branch from the exact base, verify zero commits behind, and open
a draft PR.

Objective

Implement the sole engine-independent money authority by composing LED-001.
Support exact-once grants and bounded spends, deterministic snapshots, immutable
change facts, sequence admission, and validated import/export.

Read completely before writing

- AGENTS.md
- assembly/context/CURRENT_HANDOFF.json
- assembly/dispatch/wave2/VALIDATION.md
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/contracts/REWARDS_V1.md
- docs/architecture/contracts/ECONOMY_TRANSACTIONS_V1.md
- docs/architecture/contracts/IDEMPOTENT_LEDGER_V1.md
- the MON-001 roadmap section

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Economy/Money/**
- Assets/ShooterMover/Runtime/Application/Economy/Money/**
- Assets/ShooterMover/Tests/EditMode/Economy/Money/**
- inseparable metadata inside those exact subtrees

Required behavior

- separate typed money API; never expose the raw ledger as public authority
- accept only the canonical money currency identity and reject scrap/unknown IDs
- positive grants and positive bounded spends
- insufficient funds rejects without mutation
- exact duplicate returns deterministic no-change; conflicting duplicate rejects
- optional expected-sequence admission
- immutable UI-ready change facts and deterministic snapshots/fingerprints
- validated snapshot import is atomic and leaves state unchanged on failure
- no fixed maximum balance beyond checked numeric representation

Required proof

- grant, spend, insufficient funds, wrong currency, invalid/overflow amount
- duplicate and conflicting duplicate
- expected-sequence success/conflict
- snapshot round trip, corrupt import rejection, deterministic ordering
- no Unity, scene, UI, shop, pickup, scrap, or persistence-file dependency

Forbidden

- SCR/INV/RAP/product paths, scenes, shared asmdefs, ProjectSettings, Packages,
  assembly context/generated/dispatch files

Run layout, assembly, cold compile, and focused EditMode proof when available.
Leave draft and state exact pending proof if Unity is unavailable.
```

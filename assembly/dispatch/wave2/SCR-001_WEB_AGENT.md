# SCR-001 - Scrap wallet

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: SCR-001
Branch: agent/scr-001-scrap-wallet
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- REW-001 through 06012ea116c1b8bd1f087a5f9275079d5fd882bd
- LED-001 through 95a19fbf60fe81c443ad1a366422bf67d17d953e

Create a fresh exact-base branch and open a draft PR.

Objective

Implement the sole scrap authority by composing LED-001, with exact-once grants
and spends plus salvage/strongbox-ready reason and provenance fields.

Read AGENTS.md, CURRENT_HANDOFF.json, wave2/VALIDATION.md, the reward architecture,
REWARDS_V1, ECONOMY_TRANSACTIONS_V1, IDEMPOTENT_LEDGER_V1, and the SCR-001
roadmap section completely.

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Economy/Scrap/**
- Assets/ShooterMover/Runtime/Application/Economy/Scrap/**
- Assets/ShooterMover/Tests/EditMode/Economy/Scrap/**
- inseparable metadata inside those exact subtrees

Required behavior

- public scrap types remain distinct from money types
- reject money/unknown currency identities
- exact-once positive grants and bounded positive spends
- deterministic reason/provenance values including strongbox-opening and future
  salvage-compatible identities without implementing either product
- insufficient balance, sequence conflict, invalid amount, overflow, duplicate,
  and conflicting duplicate semantics
- immutable change facts, deterministic snapshots/fingerprints, atomic validated
  import/export

Required proof

- grant/spend, insufficient scrap, wrong currency, malformed provenance
- exact and conflicting duplicates, sequence conflict, overflow
- strongbox and future-salvage reason round trip
- snapshot round trip and corrupt import rejection
- no money authority, crafting, salvage calculation, box generation, UI, scene,
  or persistence backend

Forbidden

- MON/INV/RAP/product paths, scenes, shared asmdefs, ProjectSettings, Packages,
  assembly context/generated/dispatch files

Run layout, assembly, cold compile, and focused EditMode proof when available.
```

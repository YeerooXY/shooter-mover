# INV-001 - Player holdings and equipment inventory

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: INV-001
Branch: agent/inv-001-player-holdings
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- REW-001 through 06012ea116c1b8bd1f087a5f9275079d5fd882bd
- EQP-001 through 0bac603dc5921ab1da1b89895f725a0b97261fae
- LED-001 through 95a19fbf60fe81c443ad1a366422bf67d17d953e

Create a fresh exact-base branch and open a draft PR.

Objective

Implement the sole durable holdings authority for unique equipment/armor,
owned strongboxes, premium ammunition, and stackable miscellaneous items.

Read AGENTS.md, CURRENT_HANDOFF.json, wave2/VALIDATION.md, reward architecture,
REWARDS_V1, ECONOMY_TRANSACTIONS_V1, EQUIPMENT_AUGMENTS_V1,
IDEMPOTENT_LEDGER_V1, and the INV-001 roadmap section.

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Holdings/**
- Assets/ShooterMover/Runtime/Contracts/Holdings/**
- Assets/ShooterMover/Runtime/Application/Holdings/**
- Assets/ShooterMover/Tests/EditMode/Holdings/**
- docs/architecture/contracts/PLAYER_HOLDINGS_V1.md
- inseparable metadata inside those exact subtrees

Required behavior

- typed exact-once add/remove commands for unique and stackable holdings
- unique equipment instance and strongbox instance collision rejection
- equipment instances validate through EQP-001 contracts
- stack quantities use checked arithmetic and explicit bounds
- immutable provenance from grant/source/transaction identities
- missing item, type mismatch, expected-sequence, duplicate, and conflicting
  duplicate rejection without partial mutation
- canonical immutable snapshots/fingerprints and atomic validated import/export
- arbitrary future misc identities without a closed enum

Required proof

- unique equipment/armor and strongbox add/remove
- premium-ammo and misc stack add/remove
- collision, missing item, underflow/overflow, wrong reward type
- duplicate/conflict, sequence conflict, deterministic snapshot round trip
- corrupt snapshot leaves previous state unchanged

Non-goals

No loadout/equipping behavior, reward rolling, wallet balance, UI, scene,
strongbox opening, save backend, crafting, shop, or pickup logic.

Do not edit EQP/REW/LED implementations, scenes, shared asmdefs, settings,
packages, handoff, dispatch, or generated outputs.
```

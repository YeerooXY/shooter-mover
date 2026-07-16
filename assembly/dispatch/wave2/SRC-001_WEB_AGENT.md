# SRC-001 - Reward definitions and source authoring

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: SRC-001
Branch: agent/src-001-drop-source-authoring
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- REW-001 through 06012ea116c1b8bd1f087a5f9275079d5fd882bd
- OBJ-001 through e967daee4a23ca3372de468e2a4a8d122f99eea0

Create a fresh exact-base branch and open a draft PR.

Objective

Create reusable data-driven reward/drop definitions and a placed-source Unity
adapter with inherited defaults, explicit instance overrides, validation, and
resolved preview data suitable for level designers.

Read AGENTS.md, CURRENT_HANDOFF.json, wave2/VALIDATION.md, reward architecture,
PLACED_OBJECT_LIFECYCLE_V1, OBJECT_AUTHORING_V1, REWARDS_V1, and the SRC-001
roadmap section.

Exclusive owned paths

- Assets/ShooterMover/Content/Definitions/Rewards/**
- Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Sources/**
- Assets/ShooterMover/Tests/EditMode/Rewards/Authoring/**
- Assets/ShooterMover/Tests/PlayMode/Rewards/Sources/**
- docs/authoring/REWARD_SOURCE_WORKFLOW.md
- inseparable metadata inside those exact subtrees

Required behavior

- profile definitions use REW-001 immutable vocabulary
- source consumes OBJ-001 authored placed identity and explicit/nearest scope
- explicit modes: Inherit, None, Replace, AppendGuaranteed, money-only,
  exact box tier, box tier range, and miscellaneous override
- allow arbitrary composable grant profiles without one giant universal object
- deterministic resolved preview, validation diagnostics, and fingerprint
- one stable source-operation identity per logical resolution
- repeated callback/restart cannot create a second claimed operation identity
- definition assets contain configuration only, never mutable claim truth

Required proof

- every override mode, inherited defaults, clearing override, invalid ranges
- rename/reparent stability and missing/duplicate scope failure
- repeated callbacks and restart registration do not duplicate source operation
- deterministic preview independent of serialized list order where applicable

Non-goals

No existing enemy/prop edits, wallet/holdings mutation, generation, pickups,
scene edits, production balance, or prefabs. ScriptableObjects are allowed only
inside the owned definition subtree.
```

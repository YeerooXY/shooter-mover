# ADR-001 — Reward/object architecture lock

Use this prompt with a GitHub web agent.

```text
Repository: YeerooXY/shooter-mover
Task: ADR-001
Branch: agent/adr-001-reward-object-architecture
Exact base commit: 56a84838558fdfe67fb97254d832b2dd7cd5c018
PR base: main

You are a GitHub web agent. Use only the authenticated GitHub connector.
Do not attempt local Git, gh, cloning, shell access, filesystem paths, or browser
login. Create a fresh branch from the exact base commit and open a draft PR.

Objective

Document and freeze the shared contracts and ownership required by the reward,
equipment, economy, object-authoring, door, hazard, shop, crafting, simulator,
and Stage 1 integration work.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- assembly/context/handoff.md
- docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md
- docs/architecture/FILE_OWNERSHIP.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- accepted combat, enemy, mission, encounter, restart, and StableId contracts
  referenced by the roadmap

Owned paths

- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/architecture/authoring/PLACED_OBJECT_LIFECYCLE_V1.md
- docs/architecture/authoring/STAGE1_INTEGRATION_OWNERSHIP.md

Forbidden paths

- Assets/**
- assembly/context/**
- assembly/generated/**
- assembly/dispatch/**
- ProjectSettings/**
- Packages/**
- any scene, prefab, ScriptableObject, source asset, or .meta file

Required decisions

1. Stage 1 has one serialized owner at a time:
   - DEMO-001 first;
   - ownership is released after merge;
   - INT-001 becomes the sole final owner.
2. Placed objects bind through an explicit or nearest-parent scene scope.
   Global Find* and scene/object names are not primary integration paths.
3. Placed identity is a serialized authored StableId with duplicate validation.
4. Object variants use composable capability definitions. Do not define a giant
   universal object asset containing irrelevant combat, movement, door, and
   reward fields.
5. Money, scrap, and holdings use one shared typed idempotent-ledger primitive
   while retaining separate public authorities.
6. Strongboxes, equipment/armor, premium ammunition, and miscellaneous items
   have an explicit holdings authority.
7. Reward lifecycle is explicit and monotonic:
   Generated -> Projected -> Claimed -> Applied, with Cancelled only through a
   documented policy.
8. Mixed reward application is all-or-none and retry-safe.
9. Quick restart cannot duplicate durable reward operations.
10. Character/region/difficulty values come from an explicit progression
    context provider shared by gameplay and simulation.
11. Randomness uses one versioned deterministic algorithm with named substreams.
12. The simulator invokes exact application services rather than reimplementing
    reward logic.
13. Doors and void hazards consume typed lifecycle, checkpoint, combat, and
    condition ports.
14. Record unresolved balance/persistence choices without silently selecting
    tuning values.

Required lifecycle matrix

Document what quick restart, room reload, mission restart, and new run do to:

- enemies and props
- doors and hazards
- projectiles and transient effects
- generated/projected/claimed/applied rewards
- money and scrap
- strongbox/equipment/misc holdings
- shop inventory identity
- source claim ledgers

Tests and review

- Confirm every shared concept has one named owner.
- Confirm no two roadmap tasks own the same serialized path concurrently.
- Confirm Wave 1 packets can depend on these decisions without inventing a
  second wallet, inventory, reward lifecycle, progression context, or scene
  scope.
- Run documentation/link/layout validation available through the repository.

Acceptance criteria

- Three owned documents are complete and internally consistent.
- All required decisions above are explicit.
- Remaining human decisions are clearly marked and do not block the technical
  boundaries needed by Wave 1.
- No implementation or balance data is changed.
- Draft PR body lists exact base, owned paths, validation, unresolved decisions,
  and confirms zero Unity/scene changes.

Non-goals

- No implementation.
- No production balance values.
- No task-backlog regeneration.
- No handoff edits.
- No Stage 1 scene or controller edits.
```

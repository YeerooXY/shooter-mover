# VOID-001 - Void and fall hazards

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: VOID-001
Branch: agent/void-001-fall-hazards
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- ADR-001 through d243819bf0bf4a9e1ab9a880b50aceec0d23b99d
- OBJ-001 through e967daee4a23ca3372de468e2a4a8d122f99eea0

Create a fresh exact-base branch and open a draft PR.

Objective

Create configurable, editor-visible, drag-anywhere void regions with typed
per-category responses, accepted damage/death/respawn ports, checkpoint
references, presentation hooks, and restart safety.

Read AGENTS.md, CURRENT_HANDOFF.json, wave2/VALIDATION.md,
PLACED_OBJECT_LIFECYCLE_V1, OBJECT_AUTHORING_V1,
STAGE1_INTEGRATION_OWNERSHIP.md, COMBAT_MESSAGES_V1, restart documentation, and
the VOID-001 roadmap section.

Exclusive owned paths

- Assets/ShooterMover/ContentPackages/Environment/VoidHazards/**
- Assets/ShooterMover/Tests/EditMode/Environment/VoidHazards/**
- Assets/ShooterMover/Tests/PlayMode/Environment/VoidHazards/**
- docs/authoring/VOID_HAZARDS.md
- inseparable metadata inside those exact subtrees

Required behavior

- authored placed ID and OBJ-001 scope/restart registration
- explicit classification and separate policy for player, enemy, projectile,
  and supported prop
- player damage or instant-death request only through accepted combat authority
- typed checkpoint/respawn port; missing required checkpoint fails closed
- explicit projectile removal and optional enemy-fall handling
- presentation hooks without presentation-owned truth
- restart clears transient contacts/state and restores authored policy
- no scene/map/name assumptions or global discovery
- package-local generic prefab allowed

Required proof

- category filters and ignored-category behavior
- damage versus instant-death request
- respawn/checkpoint request and missing checkpoint validation
- projectile removal, enemy policy, supported prop policy, duplicate contacts
- restart and arbitrary hierarchy placement

Non-goals

No new health authority, direct damage shortcut, reward semantics for fallen
enemies, final art, scene edit, or Stage 1 placement.
```

# DOOR-001 - Reusable door package

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: DOOR-001
Branch: agent/door-001-reusable-doors
Exact base commit: 6d04451883127dcf597c4f6fec199aeaec2a7f9e
PR base: main
Dependencies merged:
- ADR-001 through d243819bf0bf4a9e1ab9a880b50aceec0d23b99d
- OBJ-001 through e967daee4a23ca3372de468e2a4a8d122f99eea0

Create a fresh exact-base branch and open a draft PR.

Objective

Create a drag-anywhere, scene-independent door package with typed visible
conditions, collision/presentation states, animation hooks, one-way and room
transition support, restart restoration, and authoring validation.

Read AGENTS.md, CURRENT_HANDOFF.json, wave2/VALIDATION.md,
PLACED_OBJECT_LIFECYCLE_V1, OBJECT_AUTHORING_V1,
STAGE1_INTEGRATION_OWNERSHIP.md, ROOM_PROJECTION_V1,
ENCOUNTER_LIFECYCLE_V1, and the DOOR-001 roadmap section.

Exclusive owned paths

- Assets/ShooterMover/ContentPackages/Environment/Doors/**
- Assets/ShooterMover/Tests/EditMode/Environment/Doors/**
- Assets/ShooterMover/Tests/PlayMode/Environment/Doors/**
- docs/authoring/DOORS.md
- inseparable metadata inside those exact subtrees

Required behavior

- authored placed ID and OBJ-001 scope/restart registration
- condition types: always, trigger, interact, encounter resolved, target
  destroyed, and future wallet/key read ports
- all/any condition composition with deterministic diagnostics
- open/closed collider and presentation state, animation/event hooks
- one-way behavior and typed transition/socket authorization
- restart restores package-defined transient initial state and reevaluates
  authoritative conditions
- arbitrary hierarchy placement; no global/name/scene discovery
- package-local generic prefab allowed

Required proof

- condition evaluation and all/any composition
- impossible/missing condition configuration
- collider/presentation transitions, one-way behavior, restart
- rename/reparent placement and missing transition/socket validation
- no Stage 1 controller, wallet implementation, key inventory, or mission mutation

Do not place the door in Stage 1 or edit scenes, project settings, shared asmdefs,
other packages, handoff, dispatch, or generated files.
```

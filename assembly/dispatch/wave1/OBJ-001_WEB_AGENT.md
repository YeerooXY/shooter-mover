# OBJ-001 — Placed identity, capabilities, variants, and overrides

Use this prompt with one isolated GitHub web/coding agent.

```text
Repository: YeerooXY/shooter-mover
Task: OBJ-001
Branch: agent/obj-001-placed-object-variants
Exact base commit: 0e678a9333956aa29ba2e3598265c8e1a4122e72
PR base: main
Dependencies already merged: ADR-001 through PR #132

Create a fresh branch from the exact base. Verify the branch is zero commits
behind that commit before editing. Open a draft PR.

Objective

Implement the reusable foundation that lets a level designer place arbitrary
objects with stable authored identity, explicit scene scope, unbounded
family/variant definitions, composable capabilities, and explicit per-instance
overrides. This task defines generic authoring infrastructure only. It does not
migrate existing turrets, enemies, props, or Stage 1.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- docs/architecture/authoring/PLACED_OBJECT_LIFECYCLE_V1.md
- docs/architecture/authoring/STAGE1_INTEGRATION_OWNERSHIP.md
- docs/architecture/rewards/REWARD_EQUIPMENT_ARCHITECTURE_V1.md
- docs/audits/reward-object-readiness/ENEMY_AND_SCENE_AUDIT.md
- docs/architecture/contracts/STABLE_ID_V1.md
- docs/architecture/ASSEMBLY_DEPENDENCIES.md
- docs/architecture/FILE_OWNERSHIP.md

Exclusive owned paths

- Assets/ShooterMover/Runtime/Domain/Authoring/**
- Assets/ShooterMover/Runtime/Contracts/Authoring/**
- Assets/ShooterMover/Runtime/UnityAdapters/Authoring/**
- Assets/ShooterMover/Content/Definitions/Objects/**
- Assets/ShooterMover/Tests/EditMode/Authoring/**
- Assets/ShooterMover/Tests/PlayMode/Authoring/**
- docs/architecture/contracts/OBJECT_AUTHORING_V1.md
- inseparable leaf metadata for files created inside those exact subtrees

Forbidden

- existing enemy, turret, weapon, room, prop, or movement packages
- Assets/ShooterMover/Scenes/**
- Assets/ShooterMover/TestSupport/**
- existing Stage 1 integration tests
- Runtime/Bootstrap/** and all shared .asmdef files
- assembly/context/**, assembly/generated/**, and assembly/dispatch/**
- ProjectSettings/** and Packages/**
- shared ancestor .meta files outside the exact owned subtrees

Required architecture

1. Domain and Contracts remain engine-independent.
2. A persistent placed object has one serialized canonical StableId string at
   the Unity boundary and one validated StableId value after resolution.
3. Runtime-spawned identity is accepted only through explicit spawn input. It
   must never derive from Unity name, hierarchy, sibling index, transform,
   frame, or instance ID.
4. One explicit/nearest-compatible-parent scene-scope contract owns
   registration and duplicate detection. No global Find*, tag, scene-name,
   singleton, or static discovered registry is a normal path.
5. Missing scope and duplicate identity fail closed with deterministic,
   diagnosable results.
6. Family definitions support an arbitrary variant count. No enum or fixed
   array limits the number of crate, turret, door, hazard, or future variants.
7. Variants compose only relevant capability definitions. Do not create a
   giant universal definition exposing health, door, weapon, movement, reward,
   and hazard fields on every object.
8. Instance overrides are grouped by capability and use explicit modes such as
   Inherit and Override. A bool plus an unrelated default value must not create
   ambiguous resolution.
9. Resolution produces immutable, deterministic values and a canonical
   fingerprint/order suitable for tests and future validation.
10. Restart participation is a typed registration/lifecycle port. OBJ-001 does
    not own health reset, rewards, persistence, combat, or mission truth.

Minimum public concepts

- placed instance identity
- object family and variant identity/reference
- capability identity/reference
- immutable resolved capability set
- explicit override mode/result
- scene-scope registration request/result
- duplicate/missing/conflicting-scope diagnostics
- restart participant registration port
- Unity authoring component/definition assets that translate serialized values
  into the engine-independent contracts

Required proof

- identity remains stable after rename, reparent, sibling reorder, and transform
  changes
- distinct IDs register independently
- duplicate IDs in one scope are rejected deterministically and report both
  placements where available
- identical IDs in intentionally separate compatible scopes do not cross-bind
- explicit scope takes precedence over nearest-parent scope
- missing and conflicting scope fail closed
- arbitrary hierarchy placement works
- arbitrary variant counts resolve
- capability composition excludes irrelevant fields
- inherited and overridden values resolve deterministically
- clearing an override restores inherited behavior
- restart registration does not duplicate participants
- no global-search API appears in the production authoring path

Validation

- Add focused EditMode tests for domain/contracts/definition resolution.
- Add focused PlayMode tests for Unity scope binding and hierarchy behavior.
- Run repository layout and assembly graph validation.
- Run the focused Unity tests and a cold Unity 6000.3.19f1 compile when the
  environment supports Unity.
- If Unity cannot be executed, do not claim it ran. Leave the PR draft and list
  the exact pending commands/proof for the coordinator.

Acceptance

- Future NORM-001, SRC-001, PROP-001, DOOR-001, VOID-001, and enemy-authoring
  work can consume one public placed-object boundary.
- No existing gameplay package or scene changes.
- No second combat, reward, persistence, checkpoint, or mission authority.
- Worktree/diff contains only owned paths.

PR body

Record task ID, exact base/dependency, changed paths, public API summary,
automated results, pending manual/Unity proof, limitations, and rollback.

Non-goals

No existing package migration, custom inspectors, production balance assets,
reward behavior, health implementation, scene integration, or universal object
controller.
```

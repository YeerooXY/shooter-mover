# AUD-001 — Existing enemy and scene-readiness audit

Use this prompt with a separate GitHub web agent.

```text
Repository: YeerooXY/shooter-mover
Task: AUD-001
Branch: agent/aud-001-enemy-scene-reward-readiness
Exact base commit: 56a84838558fdfe67fb97254d832b2dd7cd5c018
PR base: main

You are a GitHub web agent. Use only the authenticated GitHub connector.
Do not attempt local Git, gh, cloning, shell access, filesystem paths, or browser
login. Create a fresh branch from the exact base commit and open a draft PR.

Objective

Perform a read-only, evidence-backed audit of every existing enemy package,
destructible prop, identity source, scene context, restart path, health/damage
boundary, physical projectile path, and relevant test.

Read completely before writing

- AGENTS.md
- project_workspace.json
- assembly/context/CURRENT_HANDOFF.json
- assembly/context/handoff.md
- docs/architecture/REWARD_PROGRESSION_AND_LEVEL_AUTHORING_PLAN.md
- docs/architecture/FILE_OWNERSHIP.md
- existing enemy, combat, mission, encounter, restart, prop, and Stage 1 docs

Only owned output

- docs/audits/reward-object-readiness/ENEMY_AND_SCENE_AUDIT.md

All implementation paths are read-only.

Required audit targets

- Assets/ShooterMover/ContentPackages/Enemies/**
- Assets/ShooterMover/ContentPackages/Props/**
- Assets/ShooterMover/Runtime/Domain/Enemies/**
- Assets/ShooterMover/Runtime/UnityAdapters/Enemies/**
- Assets/ShooterMover/Runtime/UnityAdapters/Combat/**
- Assets/ShooterMover/Runtime/UnityAdapters/Physics/**
- Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity
- Assets/ShooterMover/TestSupport/VisibleSlice/**
- focused EditMode and PlayMode tests

For every enemy package, report

- package identity and role
- target acquisition and whether lookup is injected, scoped, or global
- health/damage authority
- physical projectile/contact semantics
- avoidability/fairness hooks
- tracking/firing configuration
- placed identity source
- duplicate placement behavior
- arbitrary hierarchy/map placement behavior
- collider and destroyed-wreck handling
- destruction and restart behavior
- reward-source readiness
- reusable definition/authoring separation
- test coverage and concrete gaps
- exact evidence paths and line context
- verdict: Retain, Normalize, or Replace, with justification

For destructible props and Stage 1 integration, explicitly inspect

- name-prefix behavior such as Crate_* and Explosive_*
- collider lookup by object name
- scene/hierarchy-derived IDs
- direct Stage 1 controller dependencies
- destruction event quality
- restart restoration
- destruction-animation configuration
- readiness for inherited variants and per-instance reward overrides

Required report sections

1. Executive findings
2. Evidence table by package
3. Confirmed strengths to retain
4. Confirmed normalization requirements
5. Suspected issues requiring tests before changes
6. Recommended follow-up task splits and exact ownership
7. Risks to DEMO-001, OBJ-001, PROP-001, NORM-001, SRC-001, and INT-001

Acceptance criteria

- Every normalization recommendation cites concrete repository evidence.
- Working systems are explicitly protected from broad rewrites.
- No implementation, asset, scene, prefab, test, generated, or handoff file is
  modified.
- Draft PR body lists exact base, single owned file, inspected areas, and main
  conclusions.

Non-goals

- No fixes.
- No new tests.
- No balance decisions.
- No generated-backlog changes.
- No scene or prefab edits.
```

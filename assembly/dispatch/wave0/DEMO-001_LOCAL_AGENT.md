# DEMO-001 — Immediate complete playable Stage 1 baseline

Use this prompt with a local/path-capable agent. Do not dispatch it to a
GitHub-only web agent.

```text
Repository: C:\Users\Yeeroo\Desktop\Work\Projects\shooter-mover-main
Remote: YeerooXY/shooter-mover
Task: DEMO-001
Branch: agent/demo-001-complete-playable-baseline
Exact remote base commit: 56a84838558fdfe67fb97254d832b2dd7cd5c018
PR base: main
Required local robot commit:
96d6ce9791f4eee860a385e6c7613f972491a4f6

You are a local/path-capable implementation agent. Create a fresh clean worktree
from the exact remote base. Do not edit an existing dirty Unity checkout. Never
reuse a branch whose PR merged.

Objective

Publish one immediately playable Stage 1 baseline containing the systems that
already exist:

- transparent robot player visual
- movement
- mouse/controller aiming as currently supported
- visible physical projectile shooting
- projectile rotation for up-facing source sprites
- boosting/dashing
- boost trail presentation
- stable camera following
- turret tracking, firing, damage, destruction, and configurable wreck collision
- destructible crates/explosives and configured destruction-animation hooks
- player/world collision
- clean restart behavior

This task consolidates and verifies existing behavior. It is not a new reward,
inventory, shop, crafting, door, or hazard implementation.

Required startup

1. Read AGENTS.md, project_workspace.json, CURRENT_HANDOFF.json, handoff.md, and
   the roadmap completely.
2. Verify origin/main is exactly
   56a84838558fdfe67fb97254d832b2dd7cd5c018 before branching.
3. Inspect:
   git show --stat 96d6ce9791f4eee860a385e6c7613f972491a4f6
   git show --name-status 96d6ce9791f4eee860a385e6c7613f972491a4f6
   git merge-base 96d6ce9791f4eee860a385e6c7613f972491a4f6 origin/main
4. Cherry-pick the robot commit if clean. If it conflicts, reapply only its exact
   intended paths and document why.

Exclusive owned paths while active

- Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity
- Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity.meta
- Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs
- paired metadata
- Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/**
- Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/player_robot.png
- Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/player_robot.png.meta

Read-only package inputs

- movement/thruster runtime
- camera rig
- projectile and weapon packages
- Blaster Turret package
- destructible-prop package
- room presentation

Required behavior

- The robot sprite is transparent, centered, up-facing, above floor/tiles, and
  below projectiles/HUD.
- The authored robot removes the old white placeholder mount blocks.
- Existing four-mount/loadout runtime behavior is not rewritten.
- Shooting creates visible physical projectiles; no magical direct-hit shortcut.
- Projectile sprites rotate consistently with up-facing source art.
- Turret tracks the player and fires only through its accepted facing/pattern
  rules.
- Turret shots remain physically avoidable.
- Destroyed-turret collision follows the merged configurable policy.
- Destructible props retain confirmed-hit, once-only destruction, collider,
  animation-hook, and restart behavior.
- Boosting does not regress movement or camera behavior.
- Quick restart clears transient projectiles/effects and restores gameplay
  objects without duplicate authorities.
- Missing optional art fails closed to an understandable fallback rather than a
  blue/blank scene.

Required tests

- Cold Unity 6000.3.19f1 compile.
- Full focused Stage1VisibleSliceIntegrationTests.
- Existing movement/boost focused tests relevant to the scene.
- Existing turret package tests relevant to tracking/projectiles/restart.
- Existing destructible-prop focused tests.
- Fifty quick restarts with stable owner/object counts.
- Verify projectile cleanup and no duplicate reward/economy work is introduced.
- Manual keyboard/mouse playtest.
- Manual visual check: robot scale, sorting, aim rotation, boost trail, camera,
  turret tracking, turret shots, prop destruction, and restart.

Known proof

The local robot commit previously passed all eight focused Stage 1 integration
tests on Unity 6000.3.19f1. Re-run proof from the final branch; do not rely only
on that historical result.

Acceptance criteria

- One playable Stage 1 scene demonstrates all listed existing systems together.
- No new gameplay authority or broad architecture is invented.
- No unrelated Unity-generated files are committed.
- Worktree is clean after the intentional commit.
- Push the branch and open a draft PR against main.
- PR body records exact base, robot commit handling, paths, automated tests,
  manual proof, limitations, and rollback.

Non-goals

- No money, scrap, inventory, rewards, strongboxes, shop, crafting, augments,
  doors, void hazards, or balance work.
- No enemy-package rewrite.
- No ProjectSettings or Packages changes.
- No generated backlog or handoff edits.
```

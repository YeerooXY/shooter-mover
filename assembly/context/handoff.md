# Shooter Mover UF-003 Handoff

## Outcome

PR #17 merged and accepted UF-002, unlocking UF-003 and UF-004. UF-003 now pins the runtime baseline to:

- linear color;
- the new Input System native backend;
- zero 2D gravity for the top-down plane;
- a Unity-authored URP asset whose default renderer is Renderer2D;
- the same URP 2D pipeline across all six rendering-only quality profiles;
- owned URP global settings and default volume profile under `Assets/ShooterMover/Settings/Rendering/`.

Unity `6000.3.19f1` created the assets. Both the setup run and a clean follow-up batchmode import exited 0. Static checks confirmed all serialized GUID relationships, the input/color/physics settings, six shared-pipeline quality profiles, and zero forbidden 3D runtime references.

## Review boundary

PR #18 contains only UF-003-owned settings/rendering assets plus workflow proof. Collaboration state is `review`; no completion is claimed before the PR is manually inspected and merged.

The other first-import Unity files remain untracked and must not be bulk-staged. UF-004 is isolated in `../shooter-mover-uf004` on branch `nemo/uf-004-assembly-skeleton`.

## Exact next action

Review PR #18 in Unity `6000.3.19f1`. Open an empty scene, confirm URP 2D is active, switch quality profiles and confirm only visual-cost settings differ, then merge if accepted. UF-004 may proceed independently in its prepared worktree.

Stage 2 remains blocked behind `GATE-010`.

# Resume Shooter Mover Requirements Review and Planning Handoff

Continue from committed repository state in `YeerooXY/shooter-mover`.

## Required startup sequence

1. Read `AGENTS.md` completely.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Verify the active repository, branch, pull-request, and merge state.
5. Read every path listed in `authoritative_artifacts`.
6. Read the complete prompt for the active role under `assembly/prompts/` or from `YeerooXY/ai-assembly-line` if the local copy is absent.
7. Follow the exact `next_action` in `CURRENT_HANDOFF.json`.

## Requirements checkpoint

- Product Discovery is closed with verified decisions through D-233.
- D-231 through D-233 were persisted as an early partial batch because the user requested handoff.
- D-234 was presented but not selected and is not an accepted requirement.
- D-001 through D-039 remain excluded unverified recovery material.
- The verified one-level internal factory slice supersedes the recovered three-level MVP.
- `assembly/intake/project_intake.json` and `assembly/requirements/REQUIREMENTS.md` contain the requirements package.
- Planning must begin only after the requirements/bootstrap pull request is reviewed and merged.

## Stable product direction

- Windows-first, offline-capable, fully 2D Unity/C# shooter with a shallow angled top-down view.
- Signature regenerating directional thruster.
- Four mounted weapons fire concurrently toward one aim point with independent cadence and power banks.
- One complete automated-weapons-factory level is the internal MVP.
- Roughly six to eight base weapons, four or five ordinary machine roles, and an upgraded-droid climax.
- Minimal real strongbox, shop, banking, save, completion, and replay loop.
- Practical accessibility, structured local diagnostics, stable 1080p/60 primary target, versioned atomic saves, and repeatable content pipelines.
- Android and online co-op are post-MVP.
- One human lead with four or more concurrent AI agents supported by isolated owned tasks and protected integration.

## Exact next action

If the requirements pull request is not merged, review the package and report any blocking inconsistency; otherwise merge only with explicit human approval.

If the requirements package is already merged into `main`, switch to the Planning Agent and create the planning and architecture package in a new branch and pull request. Do not generate the canonical task backlog in the planning stage.

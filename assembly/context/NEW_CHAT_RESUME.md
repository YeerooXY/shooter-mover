# Resume Shooter Mover Stage 1 Visible-Slice Planning

Continue from committed state in `YeerooXY/shooter-mover`. Never write to a
branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and
   `assembly/context/CURRENT_HANDOFF.json`.
2. Verify current `main`, PR #104 status, and exact path ownership before
   writing.
3. Read `AMENDMENT_STAGE1_VISIBLE_SLICE.md` only from merged current `main`
   before acting as Task Splitter.
4. Use a fresh branch and separate worktree for every planning, task-split, or
   implementation change.

## Durable state

- Stage 1 Dispatch currently retains 103 canonical tasks and a
  50-focused-lead-day / 12-calendar-week cap.
- Draft PR #104 proposes a bounded visible-slice-first amendment. Until that PR
  merges, no `VS-*` task is authoritative or dispatchable.
- The planning proposal contains VS-001 through VS-007: local art intake first,
  five disjoint presentation tasks that may run concurrently, and one final
  serial integration-scene owner.
- VS-007 alone may own
  `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
- WP-010 is merged through PR #102 and exclusively owns its Stage1Presentation
  folder and focused test; no VS task may duplicate or edit the four-slot
  weapon-status strip.
- The local files `tile_concept_1.jfif`, `level_idea_1.png`,
  `standing_turret_weak.png`, and candidate props under
  `C:\Users\Yeeroo\Desktop\sprites` have not been inspected or checksummed by
  the planning agent. VS-001 must perform that local intake.
- The proposal uses 3.15 focused lead days from existing reserve: 0.60 S1.2 and
  2.55 S1.3. It does not raise the 50-day aggregate cap.
- Accepted movement, combat, enemy, encounter, mission, collision, registry, and
  persistence authorities remain unchanged. Visible-slice state is session-only.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Review and merge draft PR #104.

After it merges, start from fresh current `main` and open one separate Task
Splitter PR that materializes a dedicated `stage1-visible-slice` batch and
VS-001 through VS-007, updates generated backlog/index/collaboration artifacts,
validates the acyclic ownership graph, and stops. Do not dispatch VS agents or
implement Unity code/assets in the task-split PR.

Do not proceed to Task Splitter while PR #104 is unmerged. Do not add networking,
mobile, analytics, storefront, or Stage 2 work.

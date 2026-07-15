# Resume Shooter Mover Stage 1 Visible-Slice Materialization

Continue from committed state in `YeerooXY/shooter-mover`. Never write to a
branch whose pull request has merged.

## Required startup

1. Read `AGENTS.md`, `project_workspace.json`, and
   `assembly/context/CURRENT_HANDOFF.json`.
2. Verify current `main`, PR #110 status, and exact path ownership before
   writing.
3. Read merged `AMENDMENT_STAGE1_VISIBLE_SLICE.md` and the generated
   `stage1-visible-slice.json` batch before acting as Task Splitter or Dispatch.
4. Use a fresh branch and separate worktree for every planning, task-split, or
   implementation change.

## Durable state

- Merged PR #104 authorizes the bounded visible-slice-first amendment.
- Draft generated-only PR #110 materializes the tenth Stage 1 batch and expands
  the canonical backlog from 103 to 110 tasks. It contains no Unity implementation.
- The accepted visible-slice batch contains VS-001 through VS-007: local art intake,
  art-dependent room/turret presentation, three immediately parallel UI/camera
  tasks, and one final serial integration-scene owner.
- VS-007 alone may own
  `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
- WP-010 is merged through PR #102 and exclusively owns its Stage1Presentation
  folder and focused test; no VS task may duplicate or edit the four-slot
  weapon-status strip.
- The local files `tile_concept_1.jfif`, `level_idea_1.png`,
  `standing_turret_weak.png`, and candidate props under
  `C:\Users\Yeeroo\Desktop\sprites` have not been accepted into Git. VS-001
  must inspect, inventory, checksum, and import selected inputs locally.
- The proposal authorizes 3.15 focused lead days from existing reserve: 2.55
  days of task estimates and 0.60 day of explicit VS-007 integration
  contingency. It does not raise the 50-day aggregate cap.
- PRs #106, #107, and #108 are superseded prototype branches. Their useful
  floor replacement, overlay projection, and loadout state-machine ideas are
  recorded in the amendment, but their submitted branches must not merge.
- VS branches may add only exact leaf-folder `.meta` files, never shared
  ancestor metadata.
- Accepted movement, combat, enemy, encounter, mission, collision, registry, and
  persistence authorities remain unchanged. Visible-slice state is session-only.
- Stage 2 remains locked behind `GATE-010`.

## Exact next action

Review and merge draft PR #110. After it merges, dispatch EN-010, EN-011,
VS-001, VS-004, VS-005, and VS-006 from the combined prepared context file.
VS-001 requires local filesystem access; the other five prompts include
GitHub-connector-only web-agent instructions. WP-011 is dependency-ready but
deliberately deferred until after VS-007 by the merged amendment.

Do not dispatch VS tasks from an unmerged generated backlog. Do not add
networking, mobile, analytics, storefront, or Stage 2 work.

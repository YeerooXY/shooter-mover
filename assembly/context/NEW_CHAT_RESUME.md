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
- The planning proposal contains VS-001 through VS-007: local art intake,
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

Review and merge draft PR #104.

After it merges, run the repository prompt preparer once from fresh current
`main`. It must materialize a dedicated `stage1-visible-slice` batch and VS-001
through VS-007, update generated backlog/index/collaboration artifacts, validate
the acyclic ownership graph, and emit copy-ready contexts. A generated-only PR
is mechanical bookkeeping, not another planning approval gate.

Do not materialize or dispatch VS tasks while PR #104 is unmerged. Do not add
networking, mobile, analytics, storefront, or Stage 2 work.

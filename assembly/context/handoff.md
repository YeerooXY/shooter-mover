# Shooter Mover Stage 1 Visible-Slice Materialization Handoff

## Current boundary

Merged PR #104 authorizes the bounded screen-visible Stage 1 prototype and
stable task identities `VS-001` through `VS-007`.

Draft generated-only PR #110 materializes those identities as the tenth Stage 1
batch, expands the canonical backlog from 103 to 110 tasks, and updates bounded
collaboration/slot state without adding Unity implementation.

## Verified planning facts

- The planning branch started from then-current `main` at EN-007 merge commit
  `103e6fdc3ba8024662137f660507ce6102e0a76c`.
- Current `main` subsequently advanced through non-overlapping implementation
  work and is verified at `d867861454c97674b3f57e360c7427df7f4ec37d`,
  merged EN-009 / PR #105.
- No earlier visible-slice amendment, `VS-*` task card, or visible-slice batch
  exists on current main.
- WP-010 is merged through PR #102. It exclusively owns its weapon-presentation
  folder and focused test and remains a required read-only VS-007 dependency.
- The named local art files are available to a local intake worktree under
  `C:\Users\Yeeroo\Desktop\sprites`; they remain unaccepted until VS-001
  inventories, checksums, and imports selected inputs.
- PRs #106, #107, and #108 are superseded prototype branches. Their reusable
  ideas are recorded in the amendment, but their submitted diffs are not merge
  candidates.

## Accepted execution shape

1. PR #110 contains one validated `stage1-visible-slice` batch with VS-001
   through VS-007 and the rebuilt canonical backlog.
2. VS-001 performs local temporary-art intake.
3. VS-004, VS-005, and VS-006 may begin from accepted non-VS dependencies;
   VS-002 and VS-003 begin after VS-001 merges.
4. None of VS-002 through VS-006 may edit a `.unity` scene.
5. VS-007 runs last and alone owns
   `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
6. Generated bookkeeping contains no Unity implementation. VS branches add
   only exact leaf-folder metadata and never shared ancestor `.meta` files.

## Capacity

The proposal authorizes 3.15 focused lead days, consuming 0.60 of S1.2 reserve
and 2.55 of S1.3 reserve. Seven task estimates total 2.55 days; the remaining
0.60 day is held explicitly for VS-007 integration defects. S1.2 becomes
9.70/10.00 and S1.3 becomes 10.00/10.00. The Stage 1 aggregate cap remains 50
days under that reserve consumption and visible-slice-first resequencing.

Any scope or estimate growth requires the written cap review described in the
amendment. The exact unapproved alternative is 53.15 aggregate focused lead
days and a 12.55-day S1.3 cap plus a revised calendar cap.

## Next action

Review and merge PR #110. Then dispatch EN-010, EN-011, VS-001, VS-004,
VS-005, and VS-006 from the combined prepared context file. Keep WP-011
deliberately deferred until after VS-007 and do not dispatch VS-002/VS-003 until
VS-001 merges.

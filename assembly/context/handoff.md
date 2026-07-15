# Shooter Mover Stage 1 Visible-Slice Planning Handoff

## Current boundary

Draft PR #104 is the sole visible-slice planning-amendment PR. It proposes a
bounded screen-visible Stage 1 prototype and stable future task identities
`VS-001` through `VS-007`.

The amendment is not authoritative until PR #104 merges. Current generated task
batches, the 103-task canonical backlog, collaboration state, and slots remain
unchanged.

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

## Proposed execution shape after merge

1. After PR #104 merges, the repository prompt preparer creates one
   `stage1-visible-slice` batch containing VS-001 through VS-007, validates it,
   and emits copy-ready contexts without another planning review.
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

Review and merge PR #104. After the merge, run the repository prompt preparer
from fresh current `main`, validate the generated graph, and emit the VS
contexts. Do not generate, dispatch, or implement VS work before that approval
boundary.

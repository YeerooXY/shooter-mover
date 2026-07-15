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
  work and is verified at `bf5f35fa439a1121e8f44e3ea523170a9e4aecdd`.
- No earlier visible-slice amendment, `VS-*` task card, or visible-slice batch
  exists on current main.
- WP-010 is merged through PR #102. It exclusively owns its weapon-presentation
  folder and focused test and remains a required read-only VS-007 dependency.
- The named local art files and `C:\Users\Yeeroo\Desktop\sprites` directory were
  not accessible through GitHub and were not inspected or checksummed.

## Proposed execution shape after merge

1. A fresh Task Splitter PR creates one `stage1-visible-slice` batch containing
   VS-001 through VS-007 and updates generated planning/collaboration artifacts.
2. VS-001 performs local temporary-art intake first.
3. VS-002 through VS-006 use disjoint task-local paths and may run concurrently
   after VS-001 and their accepted non-VS dependencies.
4. None of VS-002 through VS-006 may edit a `.unity` scene.
5. VS-007 runs last and alone owns
   `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
6. The Task Splitter PR stops after graph generation and validation; no task is
   dispatched or implemented in that PR.

## Capacity

The proposal totals 3.15 focused lead days, consuming 0.60 of S1.2 reserve and
2.55 of S1.3 reserve. S1.2 becomes 9.70/10.00 and S1.3 becomes 10.00/10.00.
The Stage 1 aggregate cap remains 50 days only under that explicit reserve
consumption and visible-slice-first resequencing.

Any scope or estimate growth requires the written cap review described in the
amendment. The exact unapproved alternative is 53.15 aggregate focused lead
days and a 12.55-day S1.3 cap plus a revised calendar cap.

## Next action

Review and merge PR #104. Only after the merge, verify the amendment from fresh
current `main` and open the separate Task Splitter PR. Do not generate,
dispatch, or implement VS work before that approval boundary.

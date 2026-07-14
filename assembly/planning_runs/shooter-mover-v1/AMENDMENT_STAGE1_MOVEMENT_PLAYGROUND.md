# Stage 1 Movement Playground Amendment

**Status:** selected by the human lead; authoritative when this amendment pull
request merges
**Scope:** an optional test-only S1.1 manual movement/thruster check
**Architecture impact:** none; accepted movement authority remains unchanged

## Decision

Add `MT-013`, **Create the test-only movement playground**, to the canonical
Stage 1 movement-thruster batch. It creates the first manual playable check:
an explicit test scene with one player using the accepted movement runtime and
one orthographic camera that remains centered on that player.

The playground is not a production level, vertical slice, benchmark replacement
or a claim that movement has passed Stage 1 evidence. It is deliberately a
small visual/manual inspection surface while the authoritative evidence tasks
continue separately.

## Composition boundary

`MT-013` consumes `MT-007` and `MT-010` read-only. The lifecycle from `MT-010`
remains the only authority that combines input, domain movement, contacts and
the `MT-008` Rigidbody2D projection. The task must not add a second driver,
velocity writer, scene search, singleton, implicit ordering rule, Bootstrap
change or shared adapter edit.

The scene owns a bounded placeholder room sized for roughly two to three
camera-view extents around the player. Its camera follows the player without a
camera clamp. Placeholder geometry is test-only prototype debt; it expires
when a separately owned presentation task replaces it.

## Task-splitting effect

- Add one 0.40-focused-lead-day `MT-013` task to `movement-thruster`.
- Give it only `MT-007` and `MT-010` as dependencies; those inputs have
  already been accepted.
- Make it a leaf: no existing task may gain `MT-013` as a dependency or block
  on its manual proof.
- Its exact owned surface is its Unity test scene, test-support harness,
  PlayMode tests and movement-playground operator document, with inseparable
  Unity metadata permitted by the ownership map.
- It adds no user artwork import, production content package, registry entry,
  save schema, HUD, combat, enemy, encounter, build-settings or Bootstrap work.

## Capacity and order proof

The accepted S1.1 cap remains eight focused lead days. Its direct planned work
changes from 6.15 to 6.55 days, leaving 1.45 days of reserve. Stage 1 remains
within the accepted 50-focused-lead-day cap.

`MT-013` has no reverse dependency. Therefore the active `EH-009`, `EN-004`
and `EN-005` wave, all other ready work, later evidence tasks and every later
milestone retain their existing dependencies and merge order.

## Verification and rollback

The task-batch validator must confirm a 103-task acyclic canonical backlog,
matching batch/index/backlog artifacts, and no new dependency on `MT-013`.
The implementation task requires focused PlayMode proof plus a manual
movement/thruster/camera check in the Unity editor. The planning amendment
itself adds no Unity runtime implementation.

Rollback removes `MT-013` from the batch, backlog and indexes, restores the
102-task cardinality and deletes this amendment. No other task needs a graph
rewrite because the task is a leaf.

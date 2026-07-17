# Shooter Mover playable vertical-slice handoff

## Current boundary

Live inspection verified `main` at `645cf24f30ee6c8762214a84060e59e35df67a05`. Reward/economy/equipment foundations plus SIM-001, STAT-001, PICK-001, BOXSCENE-001, and XP-001 are merged.

Open work is not merged truth:

- MENU-001 PR #160 is draft/unmerged at `f0430794fca20cc911561478767eddbecb476f1e` and requires MENU-002 plus passing Unity XML proof.
- SKILL-001 PR #171 is open/unmerged and lacks Unity execution proof.
- RUN-001 is absent from the launch base and has no active implementation PR.

## Dispatch source

`assembly/dispatch/vertical-slice-v1/README.md` contains 20 independent packets, a zero-overlap ownership matrix, validation record, and deferred launch-base policy.

## Canonical flow

```text
Main Menu → Character Select → Inventory/Loadout Hub
→ Skills / Shop / Crafting / Play → Solo / Multiplayer
→ Level Select → Level 1 / Level 2 prototype
→ two-room combat, XP, drops, pickups, map
→ End Run → Results → Strongbox Opening
```

## Critical rules

- HUB-001 alone owns the immutable route/profile payload.
- Concrete equipment-instance identity is preserved; duplicate definitions remain valid separate instances.
- Results lists pending boxes without opening/consuming them.
- Opening references one exact box and cannot reroll/award twice.
- Debug spawning/collection use normal reward/pickup authorities.
- End Run is idempotent through RUN-001.
- UI never becomes inventory, wallet, XP, reward, shop, crafting, or skill truth.
- DEMO-005 alone edits Stage1VisibleSlice scene/controller.

## Asset intake

Use exact commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` read-only and copy only the named image into each unique task-owned art subtree. Moving-droid art is not available yet.

## Dispatch now

After the coordinator PR merges, dispatch MENU-002, ROOM-001, LEVELDES-001, XP-002, DROP-001, and WEAPON-DATA-001. Follow the dependency graph for all others. Do not dispatch blocked DEV-001, BOXUI-001, or SKILLUI-001 early.

## Proof boundary

Dispatch preparation did not run Unity. No compile/test pass is claimed. Implementation pass claims require the named XML files to exist and report zero failed tests.

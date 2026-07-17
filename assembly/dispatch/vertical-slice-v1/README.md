# Playable vertical-slice dispatch — v1

Prepared from live repository state on 2026-07-17.

## Verified launch boundary

- Repository: `YeerooXY/shooter-mover`
- Verified preparation `main`: `645cf24f30ee6c8762214a84060e59e35df67a05`
- Asset intake: exact commit `0b1b654c1fb8cf8208904eb55041fde954cfb560`
- Open PRs at preparation:
  - `#160 MENU-001`, draft/unmerged and still requiring Unity compile/test proof.
  - `#171 SKILL-001`, draft/unmerged and explicitly lacking Unity execution proof.
- Merged foundations present on the preparation base include XP-001, PICK-001, BOXSCENE-001, SIM-001, STAT-001, and the existing reward/economy/equipment/shop/crafting/strongbox authorities.
- `RUN-001` is **not** merged on this base. `DEV-001` and `BOXUI-001` remain blocked until a separate proof-complete RUN-001 implementation merges.

The attached `DEMO_FIRST_FLOW_HANDOFF.md` was read during preparation. Its immutable result/box identity rules are preserved: Results may list pending boxes without consuming them; opening references one exact box instance; different boxes may produce duplicate weapon definitions as distinct equipment instances; End Run and reward application are idempotent.

## Canonical player flow

```text
Main Menu
→ Character Select
→ Inventory / Loadout Hub
→ Skills / Shop / Crafting / Play
→ Solo / Multiplayer selection
→ Level Selection
→ Level 1 or Level 2 prototype
→ two-room Level 1 gameplay
→ combat, enemy deaths, XP, random drops, physical pickups
→ map and room navigation
→ End Run
→ Results
→ Strongbox Opening
```

## Dispatch rules

1. Give one packet to one isolated agent/worktree.
2. The immediate foundation tasks `ROOM-001`, `LEVELDES-001`, `XP-002`, `DROP-001`, and `WEAPON-DATA-001` branch from exact commit `645cf24f30ee6c8762214a84060e59e35df67a05` and target `main`.
3. `MENU-002` branches from MENU-001 head `f0430794fca20cc911561478767eddbecb476f1e` and targets `agent/menu-001-main-menu-flow`.
4. Every deferred task must receive a dispatch-time exact current-main launch override after all named dependencies merge. That override supersedes the preparation-baseline line in the packet. Follow `LAUNCH_BASE_POLICY.md`; never branch a deferred task from stale `645cf24`.
5. Open a draft implementation PR only after a non-empty owned-path change exists.
6. Never merge an implementation PR from the task agent.
7. A dependency is satisfied only when its implementation PR is merged after required proof. Open/draft/unproven PRs are not dependencies.
8. Do not merge the asset-intake branch wholesale. Copy only the named source file from exact commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` into the task-owned art path.
9. No task may edit another task's owned paths. See `OWNERSHIP_MATRIX.md`.
10. No Unity pass claim is valid unless the named XML result exists and reports a passed run with zero failures.
11. `DEMO-005` is the only final owner of `Stage1VisibleSlice.unity` and `Stage1VisibleSliceController.cs`.

## Wave shape and honest parallelization

The user-facing “first wave” is split into dependency-safe lanes rather than pretending every task can start together.

### Wave 1A — immediate parallel dispatch

- `MENU-002` on the MENU-001 branch
- `ROOM-001`
- `LEVELDES-001`
- `XP-002`
- `DROP-001`
- `WEAPON-DATA-001`

### Wave 1B — first-wave fan-out after shared prerequisites

- After MENU-002 proves and MENU-001 merges: `HUB-001`
- After HUB-001 merges, in parallel: `CHAR-001`, `INV-002`, `PLAY-001`, `LEVELSEL-002`
- After WEAPON-DATA-001 merges: `SIM-002`

This preserves one route/profile payload owner and avoids four parallel agents inventing incompatible session truth.

### Wave 2 — parallel after named dependencies

- `ROOMTRANS-001` after ROOM-001 + HUB-001
- `MAP-001` after ROOM-001; final proof consumes ROOMTRANS-001
- `SHOPUI-001` after HUB-001 and existing shop authorities
- `SKILLUI-001` after HUB-001 and proof-complete merged SKILL-001
- `CRAFTUI-001` after HUB-001 and existing crafting authorities
- `DEV-001` after separately merged RUN-001 + DROP-001
- `BOXUI-001` after RUN-001 + DEV-001

### Final serialized integration

- `DEMO-005` starts last, after every listed dependency is merged and proven.

## Dependency graph

```text
MENU-002 -> MENU-001 merge -> HUB-001
                               ├─> CHAR-001
                               ├─> INV-002
                               ├─> PLAY-001 -> LEVELSEL-002 (integration proof)
                               ├─> SHOPUI-001
                               ├─> SKILLUI-001  [also needs merged SKILL-001]
                               ├─> CRAFTUI-001
                               └─> ROOMTRANS-001 / route consumers

ROOM-001 ─> ROOMTRANS-001 ─┐
    └─────> MAP-001 ───────┤
LEVELDES-001 ──────────────┤
XP-002 ────────────────────┤
DROP-001 ─> DEV-001 ─> BOXUI-001 ─┤
WEAPON-DATA-001 ─> SIM-002 ───────┤
RUN-001 (external missing prerequisite) ─> DEV-001 / BOXUI-001
all required merged contracts ───────────> DEMO-005
```

## Packets

| Task | Packet | Earliest dispatch | Parallel-safe condition |
|---|---|---|---|
| MENU-002 | `MENU-002_WEB_AGENT.md` | immediate on PR #160 head | exclusive repair files |
| HUB-001 | `HUB-001_WEB_AGENT.md` | after MENU-001 merge | exclusive shared payload owner |
| CHAR-001 | `CHAR-001_WEB_AGENT.md` | after HUB-001 | parallel with INV/PLAY/LEVELSEL |
| INV-002 | `INV-002_WEB_AGENT.md` | after HUB-001 | parallel with CHAR/PLAY/LEVELSEL |
| PLAY-001 | `PLAY-001_WEB_AGENT.md` | after HUB-001 | parallel with CHAR/INV/LEVELSEL |
| LEVELSEL-002 | `LEVELSEL-002_WEB_AGENT.md` | after HUB-001 | coordinate route API only |
| ROOM-001 | `ROOM-001_WEB_AGENT.md` | immediate | model-only |
| ROOMTRANS-001 | `ROOMTRANS-001_WEB_AGENT.md` | after ROOM/HUB | separate application/adapter |
| MAP-001 | `MAP-001_WEB_AGENT.md` | after ROOM | separate projection/UI |
| LEVELDES-001 | `LEVELDES-001_WEB_AGENT.md` | immediate | separate authoring/prefab/editor |
| XP-002 | `XP-002_WEB_AGENT.md` | immediate | exact enemy definition owner |
| DROP-001 | `DROP-001_WEB_AGENT.md` | immediate | no enemy/prop file edits |
| WEAPON-DATA-001 | `WEAPON-DATA-001_WEB_AGENT.md` | immediate | catalog-only |
| SIM-002 | `SIM-002_WEB_AGENT.md` | after weapon data | simulator-only |
| DEV-001 | `DEV-001_WEB_AGENT.md` | after RUN + DROP | blocked now |
| BOXUI-001 | `BOXUI-001_WEB_AGENT.md` | after RUN + DEV | blocked now |
| SHOPUI-001 | `SHOPUI-001_WEB_AGENT.md` | after HUB | isolated shop presentation |
| SKILLUI-001 | `SKILLUI-001_WEB_AGENT.md` | after HUB + merged SKILL authority | blocked on PR #171 proof |
| CRAFTUI-001 | `CRAFTUI-001_WEB_AGENT.md` | after HUB | isolated crafting presentation |
| DEMO-005 | `DEMO-005_WEB_AGENT.md` | final | sole Stage1 serialized owner |

## Asset intake

Read-only source files at `0b1b654c1fb8cf8208904eb55041fde954cfb560`:

- `source-assets/user-intake/menu_screens/main_menu_screen.png`
- `source-assets/user-intake/menu_screens/level_selection.png`
- `source-assets/user-intake/menu_screens/results_screen.png`
- `source-assets/user-intake/menu_screens/skills_demo_screen.png`
- `source-assets/user-intake/menu_screens/shop_template.png`
- `source-assets/user-intake/map_items/door_open.png`

The moving droid asset is not present. Existing placeholder presentation remains valid until a separately owned asset-swap task is created.

## Merge order

1. MENU-002 into MENU-001 branch; then proof-complete MENU-001 into main.
2. Immediate independent foundations: ROOM-001, LEVELDES-001, XP-002, DROP-001, WEAPON-DATA-001.
3. HUB-001.
4. CHAR-001, INV-002, PLAY-001, LEVELSEL-002; SIM-002 after weapon data.
5. ROOMTRANS-001 and MAP-001.
6. SHOPUI-001, SKILLUI-001, CRAFTUI-001 when each authority dependency is merged.
7. External RUN-001.
8. DEV-001.
9. BOXUI-001.
10. DEMO-005.

Merge independent items within a numbered group in any order after their focused proof passes. For every deferred packet, record the dispatch-time exact launch SHA and never absorb unmerged sibling work.

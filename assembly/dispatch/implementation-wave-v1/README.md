# Implementation Wave V1

This batch follows the planning-only vertical-slice coordinator in PR #172 and the completed weapon-system planning session. It turns the agreed foundations into interactive, implementation-ready work.

## Locked goals

- Skill trees may have different sizes; the default may contain 15 skills while a specialized or mixed tree may contain 5.
- Skills can require a minimum number of invested points in one or more categories.
- Skills must eventually produce real gameplay effects, not only rank changes or menu text.
- Augments must have authored effects that reach live weapon, armor, movement, and ability runtime.
- Activated abilities support multiple equipped slots with independent state.
- Crafting spends scrap, creates one new equipment instance, and places it into inventory.
- Different strongboxes may produce the same weapon definition; each result remains a separate equipment instance.
- Results retains unopened strongbox instances; opening is exact-instance and exactly-once.
- New menu, class, crafting, map, door, and moving-droid assets are source inputs for the appropriate tasks.

## Packets

| Task | Purpose | Dependency |
|---|---|---|
| ASSET-INTAKE-002 | Add the new source images to the repository | None |
| SKILL-002 | Variable skill trees and category gates | SKILL-001 |
| SKILL-003 | Interactive session implementing real skill effects | SKILL-002 |
| AUG-002 | Interactive session implementing real augment effects/content | AUG-001, weapon plan |
| ACT-001 | Multiple activated-ability runtime foundation | Input/combat contracts |
| CRAFT-002 | Scrap → craft → inventory → equip flow | CRA-001, INV-001, RAP-001 |
| WEAPON-IMPL-001 | Implement the approved weapon-family behaviors | Weapon planning session |
| MENU-003 | Bind the expanded screen/art route | MENU-002, asset intake |
| DEMO-006 | Integrate one real skill, augment, ability, crafted weapon, droid, and door route | All applicable packets |

## Interactive-session rule

SKILL-003, AUG-002, ACT-001, and WEAPON-IMPL-001 are explicitly two-phase tasks. The agent must inspect the repository and present a concrete implementation proposal first. It must wait for approval before changing code. The approved proposal, implementation commit, tests, changed-file list, blockers, and manual proof must all be included in its draft PR.

## Recommended order

1. Merge PR #172, then merge ASSET-INTAKE-002 independently.
2. Merge or otherwise establish the tested SKILL-001 foundation.
3. Run SKILL-002, ACT-001, and the first phase of WEAPON-IMPL-001 in parallel.
4. Run SKILL-003 and AUG-002 after their planning approvals.
5. Run CRAFT-002 and MENU-003 after their authority/UI dependencies are accepted.
6. Run DEMO-006 last as the sole owner of final demo scene/controller composition.

Every implementation PR must target `main`, remain draft until its focused Unity tests and manual proof are complete, and must not merge automatically.

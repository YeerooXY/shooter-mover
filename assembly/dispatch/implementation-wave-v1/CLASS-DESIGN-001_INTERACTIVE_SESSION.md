# CLASS-DESIGN-001 — Interactive class, mounts, and skill identity session

## Status

Planning-only session. This packet defines the decisions required before class-capability and class-skill implementation. It must not add gameplay authority, mutate the existing skill system, or change the current four-mount combat runtime by itself.

## Locked design baseline

The game has three classes and six persistent character/loadout slots. At character creation, each slot receives one class identity. That class identity remains fixed until the later character-deletion feature is implemented.

| Class | Weapon mounts | Base dash charges | Class unlock |
| --- | ---: | ---: | --- |
| Assault / Aggressive | 2 | 2 | A class skill can unlock a third dash charge |
| Medic / Healer | 3 | 1 | To be defined during the session |
| Heavy | 4 | 0 | Boosting is unavailable; movement remains usable |

Weapons are mounted to class-specific body locations. A class does not merely reduce a generic inventory list: it determines which body mount points are available, how the HUD presents them, and which weapons can be active in combat.

## Session objectives

The interactive session must produce a decision record for:

1. The role and gameplay identity of each class.
2. The exact named body mount points for each class.
3. Class-exclusive skills and their prerequisites.
4. Shared skills available to all classes.
5. Skill-category thresholds and mixed-skill requirements.
6. Dash, cooldown, movement, and weapon-capacity balancing.
7. Which class capabilities are permanent character properties versus skill-derived upgrades.
8. The UI presentation for class creation, loadout mounting, locked mounts, and class skills.

## Required decisions

### Class identity

For each class, define:

- official ID, display name, and short description;
- intended combat role and preferred range;
- strengths, weaknesses, and mobility expectations;
- whether its weapon count is balanced by damage, cooldown, heat, range, survivability, or utility;
- whether class-specific weapons are restricted or merely recommended.

### Mount layout

Define stable mount IDs rather than relying on array positions. Each mount should have:

- stable ID;
- body region and visual anchor;
- aim/orientation rule;
- weapon category restrictions, if any;
- HUD label and display order;
- whether it is available at creation or unlocked later.

The session must explicitly distinguish a weapon’s ownership in inventory from its ability to be mounted by a particular character.

### Skill model

Separate skills into:

- class-only skills, such as Assault’s third dash;
- shared skills, such as health, movement, reload, pickup, or weapon handling;
- mixed skills that require investment in more than one category.

For each class-only skill, define its category, prerequisite threshold, level requirement, maximum rank, and whether it changes a capability snapshot or applies a runtime stat modifier.

The session must preserve the existing rule that skill effects are derived from class profile plus owned skill ranks. Skills must not rewrite the base class definition.

### Persistence

Define the future character-slot envelope:

```text
CharacterSlot
  characterId
  classId                         immutable until deletion
  mountedEquipmentByMountId
  inventoryReference
  skillProgression
  progressionLevel
```

The session must decide whether the effective weapon capacity and dash capacity are recomputed from `classId + skillProgression` or stored as a verified projection. The preferred approach is deterministic recomputation with a versioned projection for UI/runtime use.

## Technical constraints for the later implementation

- Preserve the existing four-mount combat maximum internally for compatibility.
- Add an explicit active-mount capability layer; inactive lanes must not fire or appear as usable HUD slots.
- Do not create fake equipment identities for unavailable class mounts.
- Keep the player’s inventory capacity separate from class active-mount capacity.
- Heavy’s zero-dash state requires an explicit disabled-thruster policy; setting a charge integer to zero must not silently violate the current thruster authority.
- Class selection must be deterministic and included in the character/loadout route payload.
- Character deletion, respec, class switching, and migration are out of scope for this session.
- The session must not introduce multiplayer, networking, or account persistence.

## Session output contract

The web agent must return:

1. A completed decision table for all three classes.
2. A mount-ID map and body-anchor diagram or textual equivalent.
3. A class-only/shared/mixed skill matrix.
4. The proposed capability and persistence contracts.
5. A list of implementation tasks split into data, runtime, UI, save/load, and tests.
6. Open questions that require human approval, with a recommended default for each.
7. A balance test matrix covering:
   - Assault before and after the third-dash unlock;
   - Medic with one dash and three weapon mounts;
   - Heavy with four weapon mounts and boosting disabled;
   - duplicate equipment definitions mounted as separate equipment instances;
   - restart/load preservation of class identity.

## Explicit non-goals

Do not implement actual class abilities, class-specific weapons, character deletion, skill respec, visual body swapping, or final balance values during this planning session. Those should be separate implementation or interactive tuning sessions after this decision record is accepted.

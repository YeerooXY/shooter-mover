# Low-Level Strongbox Synthetic Test Pool

This pool adds deterministic early-game catalogue depth. It is synthetic test content, not recovered PR #288 data and not approved balance.

- Families: **11**
- Marks: **33**
- Every MK1-MK3 peak: **level 1 through 10**
- Rarity mix: **3 common, 3 rare, 2 epic, 2 legendary, 1 artifact**
- Base selection weight: **1 for every Mark**
- Runtime profiles: existing Rattler automatic-rifle and Sweeper shotgun profiles only

The current production catalogue requires exactly three Marks per family. MK1-MK2-only families are intentionally not introduced here because the canonical family builder rejects non-three-Mark anchors and the flat Strongbox projection reads MK1, MK2, and MK3 directly.

| Creator | Family | Profile | Rarity | MK1 / MK2 / MK3 peaks |
|---|---|---|---|---|
| Helix Vanguard | HV Finch | Rattler rifle | common | 1 / 4 / 8 |
| Helix Vanguard | HV Buckler | Sweeper shotgun | rare | 2 / 6 / 10 |
| Teknova | Teknova Flicker | Rattler rifle | common | 1 / 5 / 9 |
| Teknova | Teknova Vector | Rattler rifle | epic | 3 / 7 / 10 |
| Ronsen Dynamics | Ronsen Ember | Rattler rifle | common | 2 / 5 / 8 |
| Ronsen Dynamics | Ronsen Ashmaker | Sweeper shotgun | rare | 3 / 6 / 9 |
| Virex | Virex Thorn | Rattler rifle | rare | 1 / 4 / 7 |
| Virex | Virex Crown | Sweeper shotgun | epic | 2 / 7 / 10 |
| Helix Vanguard | HV Paragon | Rattler rifle | legendary | 2 / 6 / 10 |
| Ronsen Dynamics | Ronsen Warden | Sweeper shotgun | legendary | 4 / 8 / 10 |
| Teknova | Teknova Singularity | Rattler rifle | artifact | 3 / 7 / 10 |

## Intended use

Run low-player-level Strongbox simulations and inspect whether rare, epic, legendary, and artifact suppression behaves correctly when all candidate families are level-compatible. Fixed anchors make repeated simulation results comparable.

Do not interpret the combat numbers, names, rarity allocation, or creator lines as final game balance.

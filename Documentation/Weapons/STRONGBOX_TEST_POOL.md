# Strongbox Synthetic Test Pool

This pool adds deterministic catalogue depth for Strongbox distribution testing. It is synthetic test content, not recovered PR #288 data and not approved balance.

- Families: **12**
- Marks: **36**
- Rarity mix: **3 common, 3 rare, 3 epic, 2 legendary, 1 artifact**
- Base selection weight: **1 for every Mark**
- Level anchors: fixed pseudo-random values so repeated simulator runs remain comparable
- Runtime profiles: existing Rattler automatic-rifle and Sweeper shotgun profiles only

Creator identity is currently represented in the display name and description. This intentionally does not add a permanent `creator` or `manufacturer` schema field yet.

| Creator | Family | Profile | Rarity | MK1 / MK2 / MK3 peaks |
|---|---|---|---|---|
| Helix Vanguard | HV Kestrel | Rattler rifle | common | 4 / 29 / 57 |
| Helix Vanguard | HV Breacher | Sweeper shotgun | rare | 18 / 47 / 73 |
| Helix Vanguard | HV Vanguard | Rattler rifle | legendary | 52 / 79 / 104 |
| Teknova | Teknova Spark | Rattler rifle | rare | 11 / 36 / 64 |
| Teknova | Teknova Pulse | Sweeper shotgun | epic | 27 / 58 / 83 |
| Teknova | Teknova Sovereign | Rattler rifle | legendary | 60 / 87 / 109 |
| Ronsen Dynamics | Ronsen Cinder | Rattler rifle | common | 7 / 32 / 55 |
| Ronsen Dynamics | Ronsen Furnace | Sweeper shotgun | rare | 24 / 45 / 76 |
| Ronsen Dynamics | Ronsen Sunspike | Rattler rifle | epic | 41 / 69 / 96 |
| Virex | Virex Needle | Rattler rifle | common | 14 / 38 / 62 |
| Virex | Virex Corroder | Sweeper shotgun | epic | 35 / 65 / 93 |
| Virex | Virex Apex | Rattler rifle | artifact | 72 / 94 / 110 |

## Intended use

Use this pool to inspect how current level affinity and family rarity affect Strongbox output. The repeated profiles deliberately model the future situation where multiple creators sell mechanically related weapon lines with different names, rarity positions, level bands, and eventually distinct art.

Do not interpret the current combat numbers, names, or rarity allocation as final game balance.

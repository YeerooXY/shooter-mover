# Weapon names, families and mechanic ideas

## Status

Product-planning document only. Current authored families are recorded separately from future weapon concepts. Names and mechanics may change before implementation.

## Confirmed direction

- A character can equip no more than four weapons in the current physical mount model.
- Every owned weapon is an exact equipment instance.
- Weapon definitions provide authored combat identity; character or item level should not silently multiply every firing statistic.
- Different exact instances may share one weapon definition and still be equipped together where the class mount layout allows it.
- Strongboxes, shops, crafting, inventory and live firing should resolve the same canonical weapon identity.
- Weapon concepts should be recognisable from side-profile art and readable during top-down gameplay.

## Current provisional authored families

The existing eighteen-definition baseline contains six three-mark families:

| Family | Current identity |
|---|---|
| Rattler | physical automatic / semi-auto / burst projectile family |
| Ironwake | physical shotgun and spread family |
| Voltspike | guided energy projectile family |
| Prismata | chemical orb-delivery family |
| Crownfall | thermal rocket and area-damage family |
| Nullstar | chemical direct-hit plus stacking damage-over-time family |

These families provide a technical and gameplay baseline. Non-Rattler names and values remain open to later content revision.

## Previously discussed named weapon concepts

### Energy, plasma and photon

| Concept | Working identity |
|---|---|
| Plasma Launcher | launches a slow, high-energy plasma mass with splash or lingering heat |
| Photon Repeater | fast, accurate energy repeater designed around sustained fire |
| Photon Burst Rifle | controlled multi-shot energy burst with strong medium-range identity |
| Prism Shotgun | refracted energy spread that may split, reflect or change pattern after impact |
| Pulse Rocket Launcher | energy-pulse rocket with a clean shockwave rather than conventional explosive fragments |
| Energy Rocket Launcher | extremely powerful endgame launcher, inspired by the dramatic role of SAS4's Zerfallen without copying it directly |

### Thermal and fire

| Concept | Working identity |
|---|---|
| Flamethrower | continuous short-range thermal stream and area denial |
| Inferno Scattergun | close-range blast that combines pellet impact with burn zones or ignition |
| Fast Rocket Launcher | lower-damage, higher-cadence rocket weapon with strong movement pressure |
| Thermite Cannon | heavy projectile that creates a persistent high-temperature damage area |
| Thermal Carbine | sleeker, mobile medium-range thermal rifle rather than another bulky launcher |

### Chemical, biological and corrosion

| Concept | Working identity |
|---|---|
| Bio Needler | rapid biological needles that build a bounded poison or rupture effect |
| Corrosive Sprayer | short-range chemical stream that degrades armour or applies corrosion |
| Chem-Burst Carbine | controlled chemical burst rifle with stacking status or delayed reaction |

### Cryogenic

| Concept | Working identity |
|---|---|
| Cryo Cannon | carbine-like cryogenic weapon that builds slow/freeze pressure instead of behaving like a giant launcher |

### Physical, kinetic and unusual delivery

| Concept | Working identity |
|---|---|
| Energized Autocannon | accessible/common automatic cannon; intentionally less flashy than high-tier energy weapons |
| Three-Load Burst Shotgun | sequentially fires three loaded shotgun blasts as one committed attack cycle |
| Ricochet Blade Shooter | launches physical or energy blades that bounce from walls with a strict bounce cap |

## Proposed weapon-family structure

A weapon family should define a durable fantasy and mechanic, while marks or models change cadence, delivery and specialisation.

Example:

```text
family
-> MK1: understandable baseline
-> MK2: side-grade or stronger specialised version
-> MK3: advanced signature version
```

Marks should not always be simple numerical upgrades. A later mark may trade damage for control, change semi-auto to burst, add guidance or introduce a carefully bounded secondary effect.

## Concrete family proposals

### Rattler — kinetic automatic family

- MK1: simple automatic starter.
- MK2: accurate semi-automatic marksman configuration.
- MK3: three-shot burst with improved control or final-shot emphasis.

### Ironwake — impact spread family

- MK1: straightforward close-range shotgun.
- MK2: tighter spread and stronger knockback.
- MK3: three-load sequential burst shotgun or secondary fragment wave.

### Voltspike — guided energy family

- MK1: slow seeking projectile.
- MK2: faster automatic guided bolts with weaker correction.
- MK3: short burst that assigns or prioritises targets.

### Prismata — refracted/orb family

- MK1: chemical or energy orb with simple impact.
- MK2: orb splits after impact or distance.
- MK3: prism-shotgun pattern or controlled reflection mechanic.

### Crownfall — thermal launcher family

- MK1: contact rocket.
- MK2: faster pulse rocket or controlled area burst.
- MK3: thermite payload with persistent area danger.

### Nullstar — biological/chemical status family

- MK1: direct hit plus a simple damage-over-time stack.
- MK2: rapid needler that builds stacks quickly.
- MK3: Chem-Burst Carbine with delayed detonation or stack consumption.

## Rarity and accessibility ideas

- Common weapons must still be enjoyable and visually credible. The Energized Autocannon is specifically intended to be a useful, less-flashy common weapon rather than disposable junk.
- Rare and Epic weapons can introduce stronger mechanic combinations.
- Legendary, Mythic and Artifact weapons should have unmistakable signature behaviour, not only larger numbers.
- Permanent family rarity is preferable to a system where every definition appears in every rarity with recoloured stats.

## Weapon-role matrix

A healthy catalogue should eventually cover:

| Role | Example concepts |
|---|---|
| reliable automatic | Rattler, Energized Autocannon, Photon Repeater |
| precision / burst | Photon Burst Rifle, Chem-Burst Carbine |
| spread | Ironwake, Inferno Scattergun, Prism Shotgun |
| launcher | Plasma Launcher, Pulse Rocket Launcher, Energy Rocket Launcher |
| area denial | Flamethrower, Thermite Cannon, Corrosive Sprayer |
| status buildup | Bio Needler, Cryo Cannon, Nullstar family |
| unusual trajectory | Ricochet Blade Shooter, guided Voltspike family |

## Art and presentation notes

- Side-profile inventory/shop art should clearly communicate barrel, stock, launcher chamber and power source.
- Common weapons should use restrained effects; high-tier weapons earn stronger glow, containment and energy presentation.
- Top-down projectiles must remain readable without making collision silhouettes misleading.
- Powerful endgame launchers should feel visually exceptional while still preserving the shared weapon-family footprint and UI readability.

## Open decisions

- Which names remain final and which are temporary concept labels.
- Whether the six current families are replaced, expanded or retained as the launch set.
- Exact damage channels for Plasma, Prism and Bio concepts.
- Whether the Energy Rocket Launcher is a family capstone, an Artifact definition or a boss/endless reward.
- How ammunition, reload and heat differ between families.
- Whether marks are always ordered progression or sometimes parallel side-grades.
- Which weapons are available at the initial level-65 cap.
- Whether weapon blueprints are account-wide unlocks or character-local progression.

# Blaster Machine Gun package

## Package baseline

- Stable weapon ID: `weapon.blaster-machine-gun`
- Default starting weapon: yes
- Behavior module: `module.weapon-automatic-projectile`
- Execution operation: `operation-kind.bounded-projectile-2d`
- Normal cadence: one accepted cycle every 0.1 seconds while fire is held
- Normal consumable ammunition: none
- Power bank: independent per mounted runtime, four units capacity and one unit per empowered shot

The package is intentionally the uncomplicated Stage 1 reference weapon. Every accepted
cycle emits one forward bounded projectile using the shared aim resolved by the combat
foundation. It adds no alternate fire, spread, burst topology, random modifier, target
search, scene query, final art, audio, or package-specific hit authority.

## Empowerment and fallback

Normal and empowered fire use the same automatic-projectile behavior module, the same
one-projectile topology, the same operation kind, and the same kinetic combat channel.
Empowerment changes numeric projectile and descriptor coefficient values only. When a
mount's independent power bank cannot afford an empowered shot, CB-003/CB-006 immediately
select normal fire. The fallback preserves the same one-projectile topology without
consulting or affecting another mount's bank.

## Manual baseline/readability note

In a four-mount gameplay check, this should read as the plain automatic baseline beside
specialized weapons: steady repeated single projectiles, no secondary pattern, and no
surprise targeting behavior. The temporary shared projectile/hit presentation from WP-002
is sufficient for this package-level check; final sprites, effects, sound, and balancing
remain later presentation/integration work.

## Authoritative manifest inputs

- `blaster-machine-gun.content-descriptor.json`
- `automatic-projectile.content-descriptor.json`

These files are package-owned inputs to the CS-011 registry workflow. This task does not
edit or regenerate the central registry outputs.

## Rollback

Remove this package folder and
`Assets/ShooterMover/Tests/PlayMode/Combat/BlasterMachineGunPackageTests.cs` together with
their paired Unity metadata. No scene, save, generated registry, package-lock, project
setting, shared projectile primitive, combat foundation, or migration rollback is needed.

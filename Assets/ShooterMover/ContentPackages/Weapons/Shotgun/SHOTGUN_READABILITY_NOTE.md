# WP-004 Shotgun readability note

## Readability intent

The default spread is seven short-lived pellets ordered from left to right across
24 degrees. Empowerment keeps the same seven-pellet topology, narrows the spread
to 18 degrees, and changes only existing numeric damage, cadence, speed, and
recovery values. The shared WP-002 projectile and temporary hit hook remain
small and finite, so the weapon should read as a close-range fan without hiding
enemy silhouettes or telegraphs near enemies.

The package also caps authored density at 48 pellets per second and 48 estimated
or active pellets per handler. A whole spread is reserved before pellet zero, so
a density rejection cannot emit a partial visual fan.

## Human Unity verification

**Status: pending human Unity verification.** The connector-only implementation
cannot launch the Unity editor or inspect the weapon beside live enemies. Before
merge, run the focused PlayMode fixture and manually confirm:

1. At minimum aim distance, the seven-pellet fan remains visibly centered on the
   shared aim direction.
2. Enemy body silhouettes and attack telegraphs remain readable near enemies
   during normal and empowered fire.
3. Repeated close-range fire does not create a persistent wall of projectiles or
   hit markers.
4. Normal fallback after an empty independent power bank is visually the same
   topology as ordinary normal fire.

Record the observed scene, enemy fixture, and result in the draft PR before
marking it ready for review.

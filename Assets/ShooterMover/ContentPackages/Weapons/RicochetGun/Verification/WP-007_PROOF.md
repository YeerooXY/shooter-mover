# WP-007 Ricochet Gun proof fixture

## Bounce trace fixture

The deterministic policy uses normalized incoming directions and canonicalized opposing
2D contact normals. The focused fixtures emit these trace lines:

```text
first-bounce count=1 direction=(0.70710678,0.70710678)
second-bounce count=2 direction=(-0.70710678,0.70710678)
third-collision terminated=true reflected=false count=2
bounce-trace fixture=corner incoming=(1,1) normals=[(-1,0),(0,-1)] outgoing=(-0.70710678,-0.70710678) count=1 order-independent=true
grazing-contact reflected=false count=0 direction=(1,0)
```

Canonical sequential trace:

1. Incoming `(1,-1)` against valid-wall normal `(0,1)` reflects to `(1,1)` and records bounce 1.
2. Incoming `(1,1)` against valid-wall normal `(-1,0)` reflects to `(-1,1)` and records bounce 2.
3. The next valid-wall collision terminates with `ThirdWallCollision`; it does not produce a third reflection.
4. A simultaneous corner supplies both normals to one policy call. Normal input order cannot change the result.
5. A grazing normal with zero opposing projection does not reflect and does not consume a bounce.

## Manual predictability note

Use an empty Physics2D scene with one Ricochet Gun mount, the package projectile prefab,
and walls whose non-trigger `Collider2D` is explicitly paired with `RicochetWall2D`.

Review checklist:

- Fire the same shot repeatedly at a flat wall and confirm the first bank angle is stable.
- Arrange two walls and confirm the projectile reflects twice, then disappears on the next wall contact.
- Fire into a marked inside corner and confirm the outgoing diagonal is stable across repeated shots.
- Fire nearly parallel to a marked wall and confirm grazing contact does not add a bounce.
- Replace a marked wall with an unmarked 2D collider and confirm the projectile terminates instead of reflecting.
- Leave projectiles unobstructed and confirm every projectile expires within its finite configured lifetime.
- Restart the session repeatedly and confirm no projectile or callback survives reset.

Execution status: this connector-authored fixture records the deterministic expected trace
and manual procedure. A human Unity run is still required before the draft PR is proof-complete.

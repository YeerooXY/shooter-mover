# Four-Blaster Elite readability capture

This is the package-owned static layout specification used by the automated readability assertions. It is **not** a substituted gameplay screenshot; the playable screenshot remains a manual acceptance gate.

## Full warning

```text
                  [ countdown bar 0 ]
                         ╲  -6°

        origin 0  ◇ =====╲
                          ╲
                           ███████
                           █ BOSS █ ===== ◇  origin 1
                           ███████       -2°
                          ╱
        origin 2  ◇ =====╱
                         ╱  +2°

                  [ countdown bar 3 ]  →  origin 3 / +6°
```

The production presentation may arrange the four spokes more cleanly around the boss footprint, but these invariants must remain:

- exactly four origin markers;
- four distinct line/spoke shapes;
- a visible countdown-bar pattern before the first shot;
- the outermost directions remain inside the 8° cap;
- the warning communicates by **shape and count**, never by color alone.

## Reduced-effects form

```text
       ◇ ─ ─ ─ ┐
               │
       ◇ ─ ─ [ 4 ][ 3 ][ 2 ][ 1 ] ─ ─ ◇
               │
       ◇ ─ ─ ─ ┘
```

No flicker, particles, bloom, screen shake or animated hue is required. The four static spokes and four countdown bars preserve the same tactical information.

## Pending playable evidence

A pinned-editor review must still attach an in-game capture proving that the authored layout is visible at gameplay scale, that the recovery window is obvious, and that the fight reads as the easy Four-Blaster Elite rather than the Prototype Overseer.

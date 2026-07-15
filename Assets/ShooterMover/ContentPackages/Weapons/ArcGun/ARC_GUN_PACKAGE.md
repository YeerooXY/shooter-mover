# Arc Gun Stage 1 Package

## Originality and readability note

The Arc Gun uses an original, minimal gameplay identity: one confirmed primary hit emits a short ordered pulse path through nearby targets. The implementation contains no imported third-party/SAS identity, names, code, art, audio, or effects. No final art is included.

Readability is preserved by exposing one immutable ordered chain result and one immutable execution operation. A presentation layer can draw a brief segment for each `source -> target` step in order, while damage and target validity remain outside presentation. The three-additional-target cap keeps the effect visually bounded amid four independently firing mounts.

## Deterministic chain-order fixture

For a primary target at `(0, 0)` and eligible targets:

- `enemy.arc-a` at `(2, 0)`
- `enemy.arc-b` at `(3, 0)`
- `enemy.arc-c` at `(4, 0)`
- `enemy.arc-d` at `(5, 0)`

with hop range `3`, the canonical additional-hit order is:

```text
enemy.arc-a
enemy.arc-b
enemy.arc-c
```

`enemy.arc-d` is rejected because the topology permits at most three additional targets. Equal-distance candidates are ordered by canonical `StableId`.

## Boundaries

- Primary plus zero to three additional targets only.
- Per-hop distance ranking, then stable target identity.
- Dead, invalid, out-of-range, visited, duplicate, or failed-final-confirmation targets are skipped.
- Empowerment changes authored numbers only: damage, range, cadence, recovery, and power-bank values.
- Empowerment does not change module order, operation topology, or the three-target cap.
- No status effects, infinite chain, scene discovery, enemy authority, UI, saves, rewards, registry output, or final art.

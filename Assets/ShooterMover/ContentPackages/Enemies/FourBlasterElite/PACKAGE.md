# Four-Blaster Elite package

EN-008 supplies the approachable **easy first boss** for Stage 1. The package deliberately stays much simpler than the Prototype Overseer: one EN-002 health model, one deterministic cadence, four ordered WP-003 Blaster origins, and one generous recovery window.

## Package identity

- Definition: `enemy.four-blaster-elite`
- Classification: elite
- Weight: heavy
- Attack reference: `weapon.blaster-machine-gun`
- Damage channel: kinetic
- Registry behavior: contributes one package-owned enemy definition input; it never edits generated registry output

## Cadence and spread trace

The target vector is normalized once at the boss center. Four package-owned origin offsets then fire in stable order with a mild symmetric angular adjustment.

| Absolute time | State / action | Origin | Spread |
|---:|---|---:|---:|
| 0.00–0.75 s | four-spoke warning | — | bounded preview |
| 0.75 s | Blaster shot | 0 | -6° |
| 0.90 s | Blaster shot | 1 | -2° |
| 1.05 s | Blaster shot | 2 | +2° |
| 1.20 s | Blaster shot | 3 | +6° |
| 1.20–2.70 s | safe recovery | — | no fire |
| 2.70–3.45 s | next warning | — | bounded preview |
| 3.45 s | next cycle begins | 0 | -6° |

The authored hard spread cap is 8°. The actual four-shot pattern remains inside ±6°. Every shot is built through the accepted WP-003 Blaster behavior module and produces one `operation-kind.bounded-projectile-2d` operation for CB-009 execution.

## Readability contract

The warning communicates **shape and count**, not hue:

- four dashed spokes identify the four origins;
- four countdown bars communicate time-to-fire;
- the static reduced-effects form retains the same spokes and bars;
- color, bloom, particles, animation and screen shake are optional presentation only.

See `BOSS_READABILITY_CAPTURE.md` for the package-owned static layout specification.

## Explicit simplicity boundary

This boss has no health-based behavior change, denial attack, indirect explosive attack, reinforcement call, teleport, complex repositioning, or dense barrage mode. Death immediately stops the cadence. EN-002 emits the encounter-resolution-ready terminal notification once; the package records that single completion and does not own rewards, persistence, scene progression, or encounter authority.

## Focused automated verification

```powershell
& <PINNED_UNITY_EDITOR> -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Enemies.FourBlasterElitePackageTests -testResults artifacts/en-008-playmode-results.xml -logFile artifacts/en-008-playmode.log -quit
```

The fixture covers:

- four-origin ordering and uniqueness;
- exact Blaster operation topology;
- spread cap and deterministic directions;
- telegraph, volley timing and generous recovery;
- completion-once and no post-death fire;
- death/restart replay across 25 cycles;
- color-independent and reduced-effects-readable warning facts.

## Manual playable gate

The formal playable review remains required in a pinned Unity editor:

1. Confirm the warning is readable in grayscale and reduced-effects mode.
2. Confirm the four shots feel mild and avoidable at Stage 1 movement speed.
3. Confirm the 1.50-second recovery provides a clear punish window.
4. Confirm death ends fire immediately and completion is observed once.
5. Confirm the fight is obviously an easy first boss and not the Prototype Overseer.

This connector-only change does not claim an executed Unity test log or a playable screenshot. Those artifacts must be attached before formal acceptance.

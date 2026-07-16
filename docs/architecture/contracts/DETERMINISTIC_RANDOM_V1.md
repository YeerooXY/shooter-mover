# Deterministic Random v1

## Status and scope

This contract freezes the engine-independent random algorithm owned by `RNG-001`.
Reward generation, equipment generation, strongboxes, shops, crafting, tests, and
the future simulator must use this algorithm rather than `UnityEngine.Random`,
ambient `System.Random`, time, frame counts, unordered iteration, or scene state.

Version 1 owns algorithm mechanics only. It does not choose seeds, production
probabilities, balance values, candidate ordering, or product-specific purpose IDs.

## Public state model

`DeterministicRandom` is an immutable value. Every valid stream records:

- `AlgorithmVersion`;
- `RootSeed`;
- `StreamSeed`;
- current 64-bit `State`; and
- `SamplesConsumed`, including raw values rejected by unbiased bounded sampling.

Sampling returns a new stream state and writes the sample through an `out` value.
The original value is never mutated. The default struct has algorithm version zero
and fails closed when sampled.

Only algorithm version `1` is accepted. Any other version throws
`NotSupportedException` before a sample or substream is produced.

## SplitMix64 version 1

All arithmetic below uses unsigned 64-bit values with explicit wraparound modulo
`2^64` (`unchecked` C# semantics).

For current state `s`:

```text
s' = s + 0x9E3779B97F4A7C15
z  = s'
z  = (z xor (z >> 30)) * 0xBF58476D1CE4E5B9
z  = (z xor (z >> 27)) * 0x94D049BB133111EB
sample = z xor (z >> 31)
```

The returned stream stores `s'` and increments `SamplesConsumed`. The root stream
starts with `State = StreamSeed = RootSeed`; therefore the first sample applies the
add step before mixing.

Changing any constant, state transition, output bit selection, bounded-sampling
rule, substream derivation, or trace fingerprint requires a new algorithm version.

## Named substream derivation

A substream is derived only from:

```text
root seed + algorithm version + canonical StableId purpose + unsigned ordinal
```

Forking never reads or advances the parent state. Forking from an already-advanced
parent therefore returns the same named stream as forking from the original parent.
An unrelated cosmetic or trace stream cannot shift eligibility, candidate,
quantity, quality, augment, scrap, shop, or crafting streams.

Version 1 derivation is:

1. Start with FNV-1a 64 offset basis `14695981039346656037` and prime
   `1099511628211`.
2. Append ASCII `sm-rng-substream-v1`.
3. Append the root seed as eight little-endian bytes.
4. Append the algorithm version as four little-endian bytes.
5. Append the canonical ASCII `StableId` text.
6. Append separator byte `0xFF`.
7. Append the ordinal as eight little-endian bytes.
8. Apply the SplitMix64 finalizer (the three xor/multiply steps above, without the
   state-add step) to the FNV result.
9. Use that final value as both `StreamSeed` and initial `State`.

Purpose IDs are full canonical `StableId` values such as `rng.eligibility` or
`rng.quality`; callers must not use culture-sensitive strings or unstable object
names.

## Sampling semantics

### Unsigned 64-bit and 32-bit

`NextUInt64` returns one complete SplitMix64 sample. `NextUInt32` consumes one
64-bit sample and returns its high 32 bits.

### Bounded unsigned integer

For positive exclusive bound `b`, version 1 computes:

```text
threshold = (-b modulo 2^64) modulo b
```

It consumes raw 64-bit samples until `candidate >= threshold`, then returns
`candidate modulo b`. This rejection step removes modulo bias. A zero bound is
invalid.

`NextInt32(minInclusive, maxExclusive)` uses the same bounded sampler over the
positive 64-bit width of the requested signed range. Empty or reversed ranges are
invalid.

### Unit interval

`NextUnitInterval` consumes one sample and returns:

```text
(sample >> 11) / 2^53
```

The exact result domain is `[0, 1)`, with 53 random bits aligned to an IEEE-754
double mantissa.

### Exact rational probability

`NextChance(numerator, denominator)` requires:

```text
0 <= numerator <= denominator
and denominator > 0
```

It takes an unbiased bounded sample from `[0, denominator)` and succeeds exactly
when the sample is less than the numerator. Valid zero and one probabilities use
the same bounded-sampling path as other probabilities, so their consumption rule
is explicit rather than optimized away.

## Trace and fingerprint

`GetTrace()` observes the current stream without consuming any value. Its immutable
snapshot includes version, root seed, stream seed, state, sample count, and a
16-character lowercase hexadecimal fingerprint.

The version 1 fingerprint is FNV-1a 64 over:

1. ASCII `sm-rng-trace-v1`;
2. algorithm version as four little-endian bytes;
3. root seed, stream seed, state, and sample count as four ordered unsigned
   little-endian 64-bit values.

The fingerprint is an observation and comparison aid, not a collision-free durable
identity and not a source of additional random values.

## Frozen vectors

### Root seed zero

The first five version 1 outputs are:

| Index | Output |
|---:|---|
| 0 | `E220A8397B1DCDAF` |
| 1 | `6E789E6AA1B965F4` |
| 2 | `06C45D188009454F` |
| 3 | `F88BB8A8724C81EC` |
| 4 | `1B39896A51A8749B` |

After three samples, the trace fingerprint is `a06f983ab418b31f`.

### Named stream fixture

For root seed `0123456789ABCDEF`, version `1`, purpose `rng.eligibility`,
and ordinal `0`:

- derived stream seed: `590F362AC2071C8B`;
- first outputs: `E4A794A9D2AC195D`, `32A6EFF2A46A2C71`,
  `812979C0565FC18E`;
- fingerprint after those three samples: `7ae44635008fa1ae`.

These vectors are compatibility locks. A deliberate algorithm change must add a
new version and new vectors rather than replacing version 1 silently.

## Failure, rollback, and compatibility

Invalid ranges, null purpose IDs, exhausted sample counters, and unsupported
versions fail before returning a result. There is no fallback to another random
source.

Rollback is achieved by restoring the prior immutable stream value. Persisted or
committed generated results must retain their algorithm version, root seed, named
purpose/ordinal information or trace, and content fingerprint. Replaying version 1
must continue to use this exact implementation even after a later version exists.

## Non-goals

This contract does not implement reward generation, candidate ordering, balances,
strongbox rules, shop stock, crafting admission, Unity assets, scenes, progression
context, or production tuning.

# WEAPON-DATA-002A — Burst cadence compatibility projection

## Canonical authored meaning

`WeaponFireSettings.RateOfFire` means the number of complete firing cycles that may begin per second.

For burst weapons, the canonical authored values are only:

- rate of fire;
- shots per burst;
- time between burst shots.

There is no independently authored post-burst delay.

## Existing scheduler compatibility

The current `WeaponFiringScheduler` is not redesigned by WEAPON-DATA-002A. Its burst path waits for each in-burst interval, then applies a separate recovery interval derived from `ShotsPerSecond` and `IntervalAfterBurstSeconds`.

Canonical burst construction therefore projects the authored cycle into those existing scheduler fields:

```text
cycle interval
    = 1 / authored rate of fire

burst emission span
    = (shots per burst - 1) * time between burst shots

scheduler recovery
    = cycle interval - burst emission span

scheduler ShotsPerSecond projection
    = 1 / scheduler recovery

scheduler IntervalAfterBurstSeconds projection
    = scheduler recovery
```

The resulting scheduler cycle remains:

```text
burst emission span + scheduler recovery = authored cycle interval
```

Construction fails closed when the burst emission span is greater than or equal to the authored cycle interval. Such content cannot satisfy its requested firing-cycle rate.

## Effective-weapon modifiers

`RateOfFire` modifiers apply to the canonical cycle-start frequency. `EffectiveWeaponStatEvaluator` then reconstructs canonical fire settings and recalculates the scheduler compatibility projection.

The derived `ShotsPerSecond` and `IntervalAfterBurstSeconds` values are not independent authored or modifier-facing weapon stats.

## Transitional catalogue path

The existing flat `WeaponCatalogBlueprintMapper.Map(...)` remains unchanged and retains its legacy explicit post-burst timing semantics.

The canonical migration path `MapAuthored(...)`:

- requires exactly one shot group per firing cycle;
- accepts semi-automatic, automatic, or burst;
- rejects the transitional `Continuous` mode;
- requires burst count and in-burst interval for burst fire;
- requires the old independent post-burst interval to be zero;
- builds canonical fire settings from catalogue rate of fire and explicit burst data.

This keeps the live route compatible while preventing transitional scheduler fields from becoming a second authored cadence authority.

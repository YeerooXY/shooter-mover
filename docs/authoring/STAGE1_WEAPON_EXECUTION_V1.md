# Stage 1 weapon execution V1

## Runtime flow

The production-owned boundary is intentionally independent from the retained player controller:

1. Read the production run's authoritative active weapon slot.
2. Resolve the exact equipment instance already held by the production holdings authority.
3. Resolve the equipment definition and its `RuntimeWeaponReferenceId`.
4. Build one `Stage1WeaponExecutionRequestV1` containing the exact equipment-instance object, operation ID, origin, aim and timestamp.
5. Pass the request to `Stage1WeaponExecutionDispatcherV1`.
6. Resolve the matching `IStage1WeaponExecutorV1` through `Stage1WeaponExecutionRegistryV1`.
7. The executor validates duplicate/cooldown state and submits projectile or arc requests to `IStage1WeaponEffectSinkV1`.
8. The sink adapts those requests to the existing projectile, hit, explosion, chain and enemy-damage services.

The dispatcher never switches on weapon names or IDs. Unknown runtime IDs fail closed and produce no effect request.

## Authority boundaries

Weapon execution does not own or duplicate inventory, holdings, loadout, active-slot selection, equipment identity, enemy health, XP, rewards, room state, mission completion or Results routing.

It may own only transient executor state:

- accepted operation IDs;
- next accepted fire timestamp;
- future burst/charge state;
- executor-local audiovisual state.

`ResetTransientState()` clears this state on restart or disposal.

## Registry and composition

`Stage1WeaponCompositionV1.CreateDefault` is the single explicit registration location for the starter set:

- `weapon.blaster-machine-gun` → `BlasterMachineGunExecutorV1`
- `weapon.shotgun` → `ShotgunWeaponExecutorV1`
- `weapon.rocket-launcher` → `RocketLauncherWeaponExecutorV1`
- `weapon.arc-gun` → `ArcGunWeaponExecutorV1`

Duplicate runtime-ID registration throws immediately. Lookup is deterministic by `StableId`.

Weapon-specific values are owned by each executor's `Stage1WeaponTuningV1`, not by the player controller. The current tuning object covers fire interval, projectile count, spread, speed, lifetime, direct damage, area damage, explosion radius, chain count and chain range. It is deliberately shaped so an upcoming JSON catalogue can construct the same typed values.

## Adding a new weapon

```csharp
public sealed class ExampleWeaponExecutorV1 : Stage1ConfiguredWeaponExecutorV1
{
    public static readonly StableId WeaponStableId =
        StableId.Parse("weapon.example");

    public ExampleWeaponExecutorV1(IStage1WeaponEffectSinkV1 sink)
        : base(
            WeaponStableId,
            new Stage1WeaponTuningV1(
                0.25d, 1, 0f, 20f, 1.5f,
                2f, 0f, 0f, 0, 0f),
            sink)
    {
    }
}
```

Checklist:

1. Add or import the equipment definition with runtime ID `weapon.example`.
2. Implement one executor or firing strategy.
3. Register it in Stage 1 weapon composition.
4. Reuse the existing projectile/damage/effect sink.
5. Add registry, rejection and execution tests.
6. Do not modify the player controller or dispatcher.

`Stage1WeaponExecutionV1Tests.FifthWeapon_RegistersAndExecutesWithoutDispatcherChanges` demonstrates this public extension point with `weapon.test-burst`.

## Unknown IDs and rejection safety

Unknown runtime IDs return `UnknownRuntimeWeapon`. Missing equipment, invalid aim, duplicate operations, cooldown and sink rejection have separate deterministic statuses/codes.

A rejected request does not commit duplicate-operation state or cooldown state. It must not be translated by the sink into a projectile, resource debit or damage event.

## Restart and disposal

Call `Stage1WeaponExecutionDispatcherV1.ResetTransientState()` whenever Stage 1 restarts or tears down. This clears executor-local cooldown and operation history without mutating production equipment or run state.

## Required integration seam

The retained `Stage1VisibleSliceController` still contains its historical `FireSelectedLoadout` / `FireWeapon` ID branches on the PR #211 base. The new boundary is the production replacement for that branch. Final scene wiring must provide an `IStage1WeaponEffectSinkV1` adapter over the existing retained projectile/hit services and make the controller delegate one request resolved from `Stage1ProductionRunBindingV1`'s active equipment. The controller must then delete the old chooser and blaster fallback.

This connector-only change does not claim that final 2,194-line controller replacement, Unity compilation, PlayMode execution, or XML proof. Those remain explicit runtime limitations until a source patch-capable Unity checkout performs the mechanical call-site handoff and validates it.

## Tests for every new weapon

At minimum add tests for:

- unique registry registration and deterministic lookup;
- unknown-ID fail-closed behavior;
- exact equipment identity forwarding;
- accepted effect request shape;
- rejected execution producing no effect;
- duplicate-operation behavior where applicable;
- restart clearing transient state;
- focused PlayMode behavior for projectiles, explosions, spread or chaining.

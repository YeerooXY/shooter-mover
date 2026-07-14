# Bootstrap lifecycle

UF-006 introduces the explicit composition-root shell only. It does not create a
Unity scene, a `MonoBehaviour` adapter, or any gameplay, persistence, content,
diagnostics, or UI service.

## Ownership boundary

`BootstrapCompositionRoot` is a plain C# object in `ShooterMover.Bootstrap`.
UF-007 owns the Unity adapter and Bootstrap scene that create one root and call
its lifecycle methods. The root does not use scene-name lookup,
`FindObjectOfType`, static singleton state, a global service locator, or Script
Execution Order.

Future service owners add direct construction and `Register(...)` calls inside
`RegisterServices()`. Registration order is the source order in that method.
There is no type scanning, reflection-based resolution, container, or dependency
injection framework.

## Lifecycle phases

A new root begins in `Created`.

1. **Registering** — construct concrete services and register their lifecycle
   callbacks in dependency order.
2. **Starting** — invoke start callbacks in registration order.
3. **Running** — the composition is available to its owning Unity adapter.
4. **Stopping** — invoke stop callbacks in reverse registration order for every
   service whose start completed.
5. **Disposing** — invoke dispose callbacks in reverse registration order for
   every registered service.
6. **Stopped** — all registrations are cleared. The same root may be started
   again for a new play-session boundary.
7. **Disposed** — terminal state. Starting a disposed root is an error.

If registration or startup fails, the root rolls back every completed start and
every completed registration using the same reverse-order shutdown path. Cleanup
continues after individual stop/dispose failures and reports the collected
failures after all entries have been attempted.

## Idempotence and ordering rules

- `Start()` while already `Running` is a no-op.
- `Stop()` before startup or after a completed stop is a no-op.
- `Dispose()` is idempotent. A valid call from `Created`, `Running`, or
  `Stopped` always leaves the root in terminal `Disposed`, even when shutdown
  callbacks fail; the original cleanup exception is rethrown after the phase is
  finalized.
- `Dispose()` during `Registering`, `Starting`, `Stopping`, or `Disposing` is
  rejected and does not override the active transition.
- Re-entrant start/stop calls during a transition throw instead of depending on
  incidental callback order.
- Lifecycle calls are expected on the Unity main thread. The shell does not add
  background synchronization or a scheduler.

The root intentionally contains zero registered services in UF-006. A registered
service count of zero is therefore the expected baseline until later owned tasks
add concrete services.

## Domain reload disabled and rapid Play Mode

The shell stores no static instance or static mutable lifecycle state, so a
suppressed domain reload cannot retain a hidden global root. The UF-007 adapter
must own its root instance explicitly, prevent duplicate scene adapters, and
pair every successful `Start()` with `Stop()`/`Dispose()` at the Unity play-mode
boundary. It must not use `DontDestroyOnLoad` as a substitute for ownership.

Repeated or rapid enter/exit sequences are handled by the root's idempotent
start, stop, and dispose guards. These guards do not replace the Unity-side
adapter: with scene reload or domain reload disabled, UF-007 must still ensure
that Unity invokes the lifecycle boundary exactly once per active adapter.
Duplicate prevention and interactive enter/exit proof are therefore deferred to
UF-007; construction/disposal tests remain owned by UF-009.

## What can be inspected now

Before UF-007 exists, review can verify:

- the registration/start and stop/dispose ordering directly in source;
- rollback behavior for partial startup;
- absence of scene searches, global lookup state, and implicit execution-order
  dependencies;
- the empty registered-service baseline and explicit lifecycle phase model.

Play Mode hierarchy inspection, duplicate-instance prevention, and two rapid
enter/exit cycles require the UF-007 adapter and Bootstrap scene and are not
claimed by UF-006.

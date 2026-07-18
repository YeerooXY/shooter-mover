# Player actor foundation v1

## Scope

ENTITY-001 adds an engine-neutral identity/capability boundary and one player actor authority. It does **not** wire Stage 1, replace enemy/prop authorities, or move presentation, movement, weapons, inventory, rewards, room flow, or navigation.

## Identity boundaries

The foundation keeps five concepts deliberately separate:

- **Character identity** selects durable authored character content. Existing character-selection definitions remain the source of that `StableId`.
- **Run-participant identity** identifies the participant controlling or contributing through an entity in one run and is preserved in damage, death, and healing attribution for future multiplayer coordination.
- **Entity-instance identity** identifies one stable gameplay entity instance. For a player, `PlayerActorSnapshot.ActorInstanceId` is a convenience alias over this generic identity. Equivalent authored characters may create independent actor instances.
- **Combat operation identity** identifies one damage or healing request within a lifecycle generation.
- **Lifecycle generation** is a monotonic `long`, matching the repository's restart/movement vocabulary. It identifies the current incarnation/state version and is not part of stable entity identity.

`GameplayEntityOwnership` models participant and character ownership explicitly. Neutral props are allowed to have neither; they do not invent fake player identities. `GameplayEntityIdentity` contains only stable `EntityInstanceId`, optional ownership, and faction. Lifecycle generation is projected separately by `PlayerActorSnapshot` and combat/restart commands, so restart never changes entity equality or hashing.

## Authority ownership

| State or behaviour | Authority |
| --- | --- |
| Stable entity-instance, participant, character, faction identity | `PlayerActorAuthority` construction inputs; immutable for the authority lifetime |
| Current lifecycle generation | `PlayerActorAuthority`, projected separately from stable identity |
| Maximum/current health, alive/dead state | `PlayerActorAuthority` |
| Generation-scoped damage/healing deduplication | `PlayerActorAuthority` |
| Monotonic accepted state-transition sequence | `PlayerActorAuthority` |
| Damage envelope/channel and health projection vocabulary | Existing `CombatChannel`, `DamageMessage`, and `VitalState` |
| Character catalog/profile metadata | Existing character-selection domain |
| Movement generation/velocity/thruster state | Existing movement actor; unchanged |
| Restart orchestration phases | Existing restart contracts/registries; unchanged |
| Inventory, loadout, money, XP, drops, kill totals | Existing/future dedicated authorities; never the player actor |
| Unity object, input, physics, camera, HUD, scene navigation | Future adapters/presentation; never the player actor |

## Reuse and reconciliation

- `StableId` is reused for all stable identities; no parallel identifier primitive is introduced.
- `CombatChannel`, `DamageMessage`, `VitalState`, `ICombatEventMessage`, and `CombatEventIdentity` are reused. The damage command implements the existing envelope and the authority uses the existing classifier before applying its stricter amount/participant/generation exact-replay check. The player authority creates a normal zero-shield `DamageMessage` after an accepted transition.
- The existing combat envelope does not carry run-participant attribution or lifecycle generation. `DamageReceiverCommand` therefore composes those missing fields around, rather than modifying or duplicating, Combat Messages v1.
- `PlayerActorHealingCommand` likewise carries optional source run-participant attribution. This field is evidence supplied by the authoritative combat pipeline. A network/client adapter must not trust a client-provided participant ID directly; it must resolve or validate attribution against the authenticated run participant before constructing the command.
- `EnemyActorState` / `EnemyActorStepper` remain unchanged. They demonstrate immutable state and deterministic event deduplication, but their role/contact/encounter semantics and lack of restart generation do not fit a player actor directly.
- `DestructiblePropAuthority` remains unchanged. Its current restart clears event history without accepting a generation, so a later prop adapter should adopt the shared identity/damage capability and explicit generation vocabulary rather than silently treating the current prop restart method as generation-safe.
- Existing character/profile and route/run identities remain `StableId` inputs. This task does not create a second character catalog, profile, or run coordinator.
- The movement actor's generation remains movement-owned. Live integration must deliberately coordinate its restart with the player actor generation; ENTITY-001 does not modify movement.
- `IRestartParticipant` and `RestartContext` remain orchestration contracts. The player authority exposes an explicit restart command/result and does not implement the presentation/registration callback itself.

## Damage capability and later adoption

`IDamageReceiver` exposes only a stable immutable `GameplayEntityIdentity` projection and `ApplyDamage(DamageReceiverCommand)`. It mentions no player, enemy, prop, Unity, scene, inventory, or reward type. Lifecycle generation travels with state/commands rather than changing the capability identity.

Later migrations can use adapters:

- a player adapter can forward validated hit/network input to `PlayerActorAuthority`;
- an enemy adapter can translate the shared command into the existing `EnemyActorCommand`/stepper flow while preserving enemy-specific contact and encounter facts;
- a prop adapter can translate the command into `DestructiblePropAuthority` while adding explicit lifecycle-generation rejection.

These adapters should preserve their existing authorities instead of forcing all gameplay objects into one inheritance tree.

## Healing policy

An accepted healing operation with positive effective healing returns `Applied`, records the operation, and advances the accepted state-transition sequence. Healing at full health returns `AcceptedNoEffect`, records the operation for exact replay protection, reports zero applied healing, and does **not** advance the state-transition sequence. This gives later Medic statistics an unambiguous rule: only positive `AppliedAmount` from an `Applied` result is effective healing.

Healing a dead actor is rejected by lifecycle. Revive mechanics, if added later, require a separate explicit capability and must not be smuggled through ordinary healing.

## Why composition

Players, enemies, destructible props, attacks, and rooms share only selected capabilities. A deep `EntityBase`/`ActorBase` hierarchy would couple unrelated lifecycle, movement, reward, and presentation concerns and would make neutral objects invent player-only state. Small immutable projections and ports let each authority opt into identity or damage reception without inheriting responsibilities it does not own.

## Planned runtime flow

```text
input / network / hit adapter
    -> authoritative attribution and command validation
    -> PlayerActorAuthority transition
    -> explicit result + DamageMessage / death or healing attribution
    -> Unity presentation and run coordinator observers
```

The death fact contains source actor, optional source run participant, target actor, triggering operation, channel, amount, generation, and accepted sequence. Healing results retain source actor and optional source run participant through the accepted command. A later run coordinator may assign a kill, reward, or support statistic from these facts; the player actor does not do so.

## Stage 1 migration guidance

The retained Stage 1 controller currently owns `playerHealth`. A later migration should:

1. construct one player actor from the selected character/run participant and authored maximum health;
2. replace direct health writes with damage/healing commands;
3. render HUD/death presentation from immutable snapshots/results;
4. route the death fact and effective-healing attribution to run coordination rather than awarding or navigating inside the actor;
5. coordinate restart generations with the existing movement and restart orchestration seams;
6. remove the controller's local health/deduplication fields only after parity tests pass.

**Stage 1 migration is not complete in ENTITY-001.** No scene, prefab, controller, input, movement, weapon, or UI file is changed here.

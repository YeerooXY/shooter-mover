# Extensibility content checklist V1

This document is the validation contract for ordinary Shooter Mover content additions. It accompanies `EXTENSIBILITY-GUARDRAILS-001` and intentionally adds no new gameplay authority, controller branch, enemy runtime composition, or status-effect runtime path.

## Core rule

When a mechanic already exists, adding content should normally add only definitions/assets/registration data and focused tests. Definitions describe content; existing authorities own mutable truth; policies decide; Unity adapters present.

## Public extension points proven by the fixture suite

| Content | Public boundary | Fixture proof |
|---|---|---|
| Numerical skill | `RankedSkillDefinitionV2` → `RankedSkillCatalogV2` → `SkillEffectProjectorV2` → `SkillEffectModifierAdapterV1` | `skill.guardrail-critical-focus` adds critical chance without a new stat switch. |
| Fact-window skill | `FactWindowConditionDefinitionV1` / `FactWindowConditionAuthorityV1` plus the same skill-to-modifier adapter | `skill.guardrail-pressure-cycle` activates only after two kill facts inside the authored window. |
| Enemy | `EnemyCatalogJsonImporterV1` with `EnemyCatalogRegistryV1` | `enemy.guardrail-scout` uses registered pursuit, ranged decision, projectile, damage, XP, drop, and presentation IDs. No enemy factory/runtime-composition edit is required. |
| Prop | `PropDefinitionV1` + `PropCapabilityRegistryV1.CreateBuiltIns()` + `PropRuntimeFactoryV1` | `prop.guardrail-switch` composes collision, interaction, switch, and objective capabilities. |
| Room | `RoomContentJsonPackageV1` → `RoomContentJsonImporterV1` with `BuiltInRoomContentObjectCatalogV1` | `room.guardrail-entry` and its support terminal compile into the room graph using existing door, floor, and cover objects. |
| Key / locked door | `RoomAccessReferenceCatalogV1` → `RoomAccessJsonImporterV1` | `holding.guardrail-key` is registered as a run holding and consumed by the authored progression door. |
| Special event | `SpecialEventDefinitionV1` → `SpecialEventCatalogV1` → `ActiveEventModifierProjectionServiceV1` | `event.guardrail-drop-boost` multiplies only strongbox drop weight during its activation window. |

## Equivalent schema and validation contract

The repository uses typed immutable definitions for skill, prop, and event fixtures, and deterministic JSON importers for enemy, room, and access fixtures. The tables below are the equivalent validation documentation required when a standalone JSON Schema is not the subsystem's canonical boundary.

### Enemy definition

Required fields:

- `schema_version`, `content_version`, and non-empty `definitions`;
- stable `id` and `presentation`;
- positive base health and valid level-scaling range;
- stable faction, movement-policy, and decision-policy IDs;
- finite detection radius and vision arc;
- at least one attack with stable ID, unique priority, arc, ordered ranges, cooldown, damage, and damage channel;
- capability-compatible projectile/area/melee parameter blocks;
- registered XP and drop profiles plus room-clear role.

Unknown references fail closed through `EnemyCatalogRegistryV1`. Diagnostics retain the authored JSON path, for example `$.definitions[0].attacks[0].capability`.

### Room package

A package contains a versioned manifest and one layout, enemy, prop, decor, and encounter document per room. Required validation includes:

- manifest layout/start/terminal room IDs;
- all referenced documents present;
- matching room ID in every document;
- valid bounds, spawns, doors, links, positions, and rotations;
- object IDs registered by `IRoomContentObjectCatalogV1`;
- deterministic generated identities for anonymous placements;
- valid encounter completion and door-rule selectors.

Diagnostics include both the document key and exact field, such as `$documents["guardrail.entry.props"].props[0].object`.

### Prop definition

A prop requires a stable definition ID, presentation ID, and one or more capabilities registered by `PropCapabilityRegistryV1`. Capability combinations must remain semantically valid:

- health, indestructible, and decorative modes may not conflict;
- explosion/drop-on-destroy require health-based destructibility;
- damage behavior is policy-driven rather than hardcoded;
- switch/interactable/objective descriptors use stable IDs;
- placement identity remains separate from definition identity.

Typed constructor/registry failures identify the definition and unsupported capability; fixture tests should include the source asset or object ID in assertion messages.

### Special event definition

Required fields are schema/content version, stable event ID, start-inclusive/end-exclusive activation window, priority, overlap mode, modifier descriptors, and optional exclusions. Modifier targets are open stable IDs. Overlap conflicts reject deterministically; active snapshots and frozen contexts retain fingerprints.

### Room access definition

Required fields are version/layout, stable condition IDs, valid condition kinds, child/subject references, and door selectors. External holdings, objectives, switches, and drops must exist in `IRoomAccessReferenceRegistryV1`. Unknown IDs fail closed at exact authored paths such as `$.conditions[0].subject` or `$.doors[0].consume_holding`.

### Ranked skill definition

A skill requires stable skill/category IDs, a positive maximum rank, optional class eligibility/overrides, rank curve, prerequisites/gates, per-rank effects, and milestones. Effects use open target IDs and the existing modifier kinds. Temporary/fact-window effects specify a condition ID instead of introducing a new runtime branch.

## Designer/developer extension checklist

1. Pick existing mechanics and stable IDs; do not begin in a controller.
2. Add the definition or asset under the subsystem's content/test-fixture path.
3. Register only presentation, capability, policy, profile, or external-reference IDs through the subsystem's public registry.
4. Import or construct through the public catalog boundary and fail closed on unknown IDs.
5. Add one positive use test and one exact diagnostic-path rejection test.
6. Confirm repeated authored order produces the same fingerprint where ordering is non-semantic.
7. Confirm definition identity and placement/instance identity remain separate.
8. Keep rewards, XP, inventory, health, room state, and event time in their existing authorities.
9. Run the focused EditMode suite and `tools/architecture/verify_extensibility_guardrails.py`.
10. Inspect the changed-file list: ordinary content must not edit central controllers, `Runtime/EnemyRuntimeComposition/**`, or status-effect runtime paths.

## When production code changes are justified

Production code is appropriate only for a genuinely new reusable mechanic or public capability/policy implementation. That work must be a separately scoped architecture task with its own authority and replay review. It must not be smuggled into an ordinary content addition.

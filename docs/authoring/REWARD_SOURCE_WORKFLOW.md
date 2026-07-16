# Reward source authoring workflow

## Scope

`SRC-001` supplies reusable reward profile definitions and a placed reward-source
adapter. It composes the accepted REW-001 immutable vocabulary and the OBJ-001
placed identity/scope lifecycle. It does not generate rewards, apply grants,
mutate wallets or holdings, own claim truth, spawn pickups, or edit scenes.

## Author a reusable profile

Create **Shooter Mover > Rewards > Reward Profile Definition** under
`Assets/ShooterMover/Content/Definitions/Rewards/`.

1. Assign a canonical `StableId` profile ID.
2. Choose either explicit no-drop or a configured profile. Explicit no-drop must
   contain no entries.
3. Configure any combination of:
   - guaranteed grants;
   - independent rolls using integer probability millionths; and
   - exclusive weighted groups, including explicit no-drop outcomes.
4. Use REW-001 grant kinds and positive inclusive quantity ranges. Scaling inputs
   are descriptors only; generation/progression services interpret them later.
5. Run validation by opening/importing the asset. `BuildProfile()` and
   `BuildDefinition()` reject malformed IDs, invalid ranges, duplicate identities,
   null entries, empty configured profiles, and inconsistent no-drop shapes.

The asset first builds `RewardProfileV1`, then publishes a flattened generic
capability snapshot. Entries are canonicalized by REW-001 identity before the
snapshot is written, so changing serialized list order does not change the
profile or preview fingerprint.

## Add a placed source

Add `RewardSourceAuthoring2D` beside, or explicitly reference, a bound
`PlacedObjectAuthoring2D`.

1. Assign the inherited `RewardProfileDefinitionAsset`.
2. Let `PlacedObjectAuthoring2D` resolve its authored placed identity and bind by
   OBJ-001 precedence: explicit compatible scope first, otherwise the nearest
   compatible ancestor scope.
3. Select one explicit override mode:
   - **Inherit** — use the profile unchanged;
   - **None** — resolve an explicit no-drop profile;
   - **Replace** — use another reusable profile asset;
   - **Append Guaranteed** — append arbitrary guaranteed REW-001 grants;
   - **Money Only** — replace with one money grant and an authored quantity range;
   - **Strongbox Exact Tier** — replace with one exact authored tier grant;
   - **Strongbox Tier Range** — select one weighted tier from a contiguous authored
     order range; there is no fixed tier-count cap;
   - **Miscellaneous** — replace with one or more miscellaneous or premium-ammo
     grants.
4. Inspect `ResolvePreview()` output for the inherited profile, resolved profile,
   applied mode, operation request, and deterministic fingerprints.
5. Connect a component implementing `IRewardSourceOperationSink` when a later
   application package is ready to accept the immutable request.

The component performs no global scene search. A missing placed object, malformed
profile, missing/incompatible scope, duplicate placed identity, incomplete mode,
or invalid tier range fails closed with a typed diagnostic.

## Operation and restart identity

For one run and authored source instance, the adapter deterministically derives:

- one source operation ID;
- one commitment ID; and
- one restart participant ID.

The operation request includes the run ID, placed source ID, resolved profile ID,
and REW-001 content fingerprint. Repeated terminal callbacks submit the exact same
request. Reusing the operation ID after changing immutable content is diagnosed as
a conflicting resolved operation.

The adapter registers through the OBJ-001 restart registry. Restart phases may
replace attempt-local projection, but they do not clear the cached immutable
request or invent another operation. A room reload reconstructs the same IDs from
the same run/source inputs. Durable claim/applied state remains owned by RAP-001
and later persistence authorities, never by this component or a ScriptableObject.

## Strongbox range authoring

A tier range is data-driven. Each option supplies an integer authoring order, a
stable tier content ID, an outcome ID, and a positive weight. The inclusive
minimum/maximum orders must be valid, unique, and contiguous in the supplied
options. The resolved profile becomes one REW-001 exclusive group with a unique
grant identity per tier outcome. This supports arbitrary future tier counts
without an enum or an eleven-tier array.

## Validation checklist

Before a source package is integrated:

- profile and every nested ID are canonical;
- configured profiles contain at least one section;
- explicit no-drop profiles contain no entries;
- grant, roll, group, and outcome identities are unique;
- quantity ranges are positive and ordered;
- the placed object binds to one compatible OBJ-001 scope;
- the placed source identity is unique inside that scope;
- the selected override mode has exactly the data it needs;
- strongbox ranges are ordered, unique, and contiguous;
- repeated preview/submission returns the same request fingerprint; and
- restart registration is accepted or exact duplicate-no-change.

## Non-goals and later integration

This increment creates no production balance assets or prefabs. Existing enemy,
prop, mission, Stage 1, wallet, holdings, generation, pickup, and persistence files
remain untouched. `PROP-001`, `NORM-001`, and later integration owners may attach
this adapter to their owned serialized content. `GEN-001` consumes the resolved
profile, and `RAP-001`/source-claim authorities decide duplicate, claim, apply, and
persistence outcomes.

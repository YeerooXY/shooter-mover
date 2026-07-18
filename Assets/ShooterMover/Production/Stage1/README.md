# Stage 1 retained production presentation

This folder owns the retained Stage 1 scene presentation while it remains in the Assembly-CSharp boundary required by the accepted content-package dependency graph. The script GUID is preserved during extraction so existing scene and test references remain stable.

`Stage1ProductionPresentationHostV1` is the production lifecycle boundary in front of this retained implementation. The production composition root validates the Level Selection route and exact Bootstrap-owned authority bundle before enabling the retained presenter. Rejected startup explicitly disables it through the host, so the scene cannot silently fall back to prototype authority state.

`Stage1PlayerPresentationV1` is the first internal production subsystem. It now owns the complete accepted player construction recipe: the exact `PlayerMover` object, rigidbody and collider, movement input and tuning, `MovementActorLifecycle`, combat input, enemy-target adapter, void-hazard target, sprite projection, fallback gun mounts, boost trail, boost-state refresh, restart and owned runtime-resource disposal.

The subsystem supports a migration capture path for the retained presenter and a production construction path. Both paths converge on the same validated component surface. Construction is exactly once and rejects missing dependencies before creating a player. The subsystem deliberately does not own player health, combat-hit translation, equipment, inventory, enemy authority or mission-result authority.

The retained controller still calls its historical private construction, boost-refresh and restart blocks. Replacing those calls with `Stage1PlayerPresentationV1.Construct`, `RefreshBoostPresentation` and `Restart`, then deleting the duplicate controller fields and helpers, is now a mechanical source handoff rather than new architecture. It still requires a complete source-edit channel and Unity compilation proof.

Remaining responsibility extraction covers that final player handoff, rooms/environment, enemies/combat, HUD/camera and weapon presentation. The historical CLR namespace remains only inside the retained implementation until those responsibilities have production-owned replacements and Unity compilation proof.

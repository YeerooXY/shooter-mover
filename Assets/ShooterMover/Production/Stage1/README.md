# Stage 1 retained production presentation

This folder owns the retained Stage 1 scene presentation while it remains in the Assembly-CSharp boundary required by the accepted content-package dependency graph. The script GUID is preserved during extraction so existing scene and test references remain stable.

`Stage1ProductionPresentationHostV1` is the production lifecycle boundary in front of this retained implementation. The production composition root validates the Level Selection route and exact Bootstrap-owned authority bundle before enabling the retained presenter. Rejected startup explicitly disables it through the host, so the scene cannot silently fall back to prototype authority state.

`Stage1PlayerPresentationV1` is the first internal production boundary. It captures and validates the exact `PlayerMover` projection created by the retained presenter, including its rigidbody, collider, accepted movement lifecycle, combat input, enemy-target adapter, sprite renderer and boost trail. It creates no replacement player or movement actor. Its restart operation is ready to replace the matching legacy controller block once that controller can be edited and compiled from a complete Unity checkout.

The player boundary is intentionally staged: the retained controller still performs construction, boost refresh and restart today. Remaining responsibility extraction covers final player delegation, rooms/environment, enemies/combat, HUD/camera and weapon presentation. The historical CLR namespace remains only inside the retained implementation until those responsibilities have production-owned replacements and Unity compilation proof.

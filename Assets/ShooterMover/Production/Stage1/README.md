# Stage 1 retained production presentation

This folder owns the retained Stage 1 scene presentation while it remains in the Assembly-CSharp boundary required by the accepted content-package dependency graph. The script GUID is preserved during extraction so existing scene and test references remain stable.

`Stage1ProductionPresentationHostV1` is the production lifecycle boundary in front of this retained implementation. The production composition root validates the Level Selection route and exact Bootstrap-owned authority bundle before enabling the retained presenter. Rejected startup explicitly disables it through the host, so the scene cannot silently fall back to prototype authority state.

The next decomposition boundary is internal responsibility extraction: player/movement, rooms/environment, enemies/combat, HUD/camera, and weapon presentation. The historical CLR namespace remains only inside the retained implementation until those responsibilities have production-owned replacements and Unity compilation proof.

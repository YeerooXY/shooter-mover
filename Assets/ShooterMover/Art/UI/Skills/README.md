# SKILLUI-001 backplate

`skills_demo_screen.png.bytes` contains a base64-encoded PNG used only as passive presentation artwork by `SkillsSceneController`.

All visible values and every interactive control are code-owned overlays. Text or cards baked into the PNG are not read as data and are never used as hit targets. The PNG payload can be replaced with the final `skills_demo_screen.png` artwork without changing the XP/SKILL authority integration, allocation commands, route payload behavior, or tests.

The standalone scene uses actual merged XP-001 and SKILL-001 authority implementations for an authoring preview. Production Hub composition must inject the persistent authority instances and the HUB-owned return callback through `SkillsHubDestinationAdapterV1`.

# Canonical flow presentation backplates

These 640x360 image payloads are passive artwork retained for the dedicated production-flow scenes:

- `MainMenuBackground.png.bytes`
- `LevelSelectBackground.png.bytes`
- `SkillsBackground.png.bytes`
- `ResultsBackground.png.bytes`

The files contain base64-encoded PNG data. Each canonical scene has one scene-specific controller that decodes or projects its own artwork. The Main Menu no longer embeds Level Selection, Skills, Inventory, Shop, Crafting, Settings, or Results inside one `OnGUI` owner.

Text, icons, cards, and buttons baked into these images are presentation only. Selectable behavior comes from the code-owned hit regions and routing of the controller for the currently loaded scene.

The original File Library images were available to the initial implementation environment as visual descriptions/previews rather than transferable binary attachments. These compact backplates can still be replaced later without changing route, inventory, economy, skill, crafting, reward, or gameplay authority.

# MENU-001 presentation backplates

These four 640x360 image payloads are presentation-only backplates for the standalone MENU-001 scene:

- `MainMenuBackground.png.bytes`
- `LevelSelectBackground.png.bytes`
- `SkillsBackground.png.bytes`
- `ResultsBackground.png.bytes`

The files contain base64-encoded PNG data and are decoded by `MainMenuArtworkController`. Text, icons, cards, and buttons baked into these images are never used as controls. All selectable behavior comes from code-owned overlay hit regions.

The original File Library images were available to this implementation environment as visual descriptions/previews, not transferable binary attachments. The committed compact backplates reproduce their intended layout and can be replaced with the original 16:9 image payloads later without changing menu state, scene routing, hit regions, or gameplay authorities.

MENU-001 owns no wallet, inventory, XP, skill, purchase, crafting, or reward truth. The skills, results, and second-level content shown here remain navigation/presentation shells for their separate follow-up scene tasks.

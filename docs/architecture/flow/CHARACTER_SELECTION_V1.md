# CHARACTER_SELECTION_V1

## Purpose

CHAR-001 provides a reusable, presentation-only character and class selection flow between Main Menu and the Inventory / Loadout Hub.

It consumes the HUB-owned `PlayerRouteProfilePayloadV1` contract read-only. It does not create a character-stat, inventory, equipment, wallet, XP, reward, skill, shop, crafting, or gameplay authority.

## Stable content model

The V1 catalog contains two independent identity layers:

- `CharacterSelectionDefinitionV1`
  - stable character identity;
  - display name and description;
  - portrait and preview resource keys;
  - default loadout-profile identity;
  - optional stable visual, body, and armor variant metadata.
- `CharacterClassProfileDefinitionV1`
  - stable loadout-profile identity;
  - owning character identity;
  - Aggressive, Defensive, or Healer class metadata;
  - display and preview metadata.

The built-in vertical-slice content supplies two character identities and three profiles for each character. The profile identities are route metadata for future consumers; they do not grant stats, create equipment, or mutate holdings.

Catalog admission rejects:

- missing or empty character/profile collections;
- duplicate character identities;
- duplicate loadout-profile identities;
- profiles whose character identity is absent;
- missing default character;
- missing default profile;
- a default profile owned by another character;
- duplicate class entries for one character.

Catalog and definition fingerprints use canonical length-prefixed UTF-8 fields and SHA-256. Input collection order and Unity object instance IDs do not participate.

## Selection semantics

`CharacterSelectionServiceV1` receives:

1. one validated immutable character catalog; and
2. one valid incoming `PlayerRouteProfilePayloadV1`.

Highlight operations change only local draft selection. They never alter the incoming payload.

### Confirm

Confirm creates exactly one new immutable HUB payload with:

- the highlighted stable character identity;
- the highlighted stable loadout-profile identity;
- the same four ordered concrete equipment-instance identities from the incoming payload.

The service caches the terminal result. Repeated Confirm calls return the same result and payload object and do not create another route fact or payload.

### Back

Back returns the exact incoming payload object and targets `HubRouteV1.MainMenu`. Character and class highlights do not change that payload. Repeated Back calls return the same cached result.

The Unity controller has a local two-stage Back behavior:

- Class Choice → Character Choice: local presentation navigation only;
- Character Choice → caller: emits the immutable Back result.

## Supplied artwork

The exact source PNG blobs are copied read-only from:

- `source-assets/user-intake/menu_screens/character_choice_screen.png`
- `source-assets/user-intake/menu_screens/character_creation_choice_screen.png`
- `source-assets/user-intake/menu_screens/aggressive_class.png`
- `source-assets/user-intake/menu_screens/defensive_class.png`
- `source-assets/user-intake/menu_screens/healer_class.png`

They are stored as Unity `TextAsset` resources under `UI/CharacterSelect/Resources/CharacterSelect`. Runtime decoding creates transient textures for presentation only. Real code-owned buttons and keyboard/controller input handle interaction; baked artwork never acts as gameplay or route authority.

## Unity flow

`CharacterSelectControllerV1` supports:

- responsive character and class card layout;
- mouse/touch-compatible code-owned button regions;
- keyboard arrows, Enter/Space, Escape/Backspace;
- controller D-pad, South/accept, and East/back;
- an injected `ICharacterSelectionRouteSinkV1`;
- deterministic local state and terminal-result deduplication.

The standalone scene is:

`Assets/ShooterMover/Scenes/Flow/CharacterSelect/CharacterSelect.unity`

Production composition should inject the live incoming HUB payload and route sink. The scene's fallback payload exists only so the standalone scene can open without another scene owning composition.

## Authority boundary

CHAR-001 may:

- validate immutable selection content;
- highlight a character/profile locally;
- project the supplied artwork;
- construct a new immutable HUB route payload on Confirm;
- return the incoming immutable HUB payload on Back.

CHAR-001 may not:

- create or update character stats;
- grant or equip items;
- change inventory/holdings;
- change wallets, XP, skills, rewards, shops, crafting, or gameplay;
- edit the HUB route contract;
- derive identity from Unity instance ID, hierarchy, frame, clock, or random state.

## Verification

Focused commands:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode `
  -testFilter "ShooterMover.Tests.EditMode.Characters.Selection" `
  -testResults "artifacts/test-results/CHAR-001-EditMode.xml" `
  -logFile "artifacts/logs/CHAR-001-EditMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
New-Item -ItemType Directory -Force artifacts/test-results, artifacts/logs | Out-Null
& $Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode `
  -testFilter "ShooterMover.Tests.PlayMode.Flow.CharacterSelect" `
  -testResults "artifacts/test-results/CHAR-001-PlayMode.xml" `
  -logFile "artifacts/logs/CHAR-001-PlayMode.log" -quit
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

A passing Unity claim requires each named XML file to report a completed run with zero failed tests.

## Manual proof

1. Enter Character Select with a real incoming HUB payload.
2. Highlight both character identities.
3. Choose Aggressive, Defensive, and Healer profiles.
4. Confirm and verify the selected character/profile appears in the returned HUB payload.
5. Verify all four incoming equipment-instance identities are unchanged.
6. Repeat Confirm and verify no second route dispatch occurs.
7. Highlight another selection, Back, and verify the exact incoming payload is returned.
8. Resize between 16:9 and a non-16:9 Game view and verify all real controls remain usable.

## Known limitations

- Character-specific stats, gameplay spawn behavior, body assembly, and armor rendering are future consumers.
- The built-in catalog contains vertical-slice placeholder identities and descriptions, not final balance.
- Scene-to-scene composition remains owned by the wider flow bootstrap; CHAR-001 returns route results rather than editing HUB or Main Menu scenes.

## Rollback

Remove the CHAR-001-owned runtime, content, UI, scene, test, and documentation additions. No shared authority, HUB contract, inventory state, or serialized player data requires migration.

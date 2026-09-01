# Mira-NotePad

An in-game notepad for [MiraAPI](https://github.com/All-Of-Us-Mods/MiraAPI) and its Mods, with support for more heavy integration with other MiraAPI mods.

Toggle a notepad window from the HUD and jot stuff down mid-game. On its own it's just a clean little text box that colors role and modifier names as you type them. Install a supported mod alongside it and it does more, like auto-logging ability feedback or letting you tag guessed roles onto players.

## Features

### Notepad window
- Toggle with a HUD button (position configurable, top row or second row)
- Full text editing: arrow key navigation, Home/End, click-to-place cursor, held backspace/delete with repeat
- Text clears automatically on game start and when you return to the lobby
- Configurable text color (black, white, red, yellow, green, cyan, grey)

### Role and modifier coloring
Type a role or modifier name in the notepad and it gets auto-colored and (optionally) prefixed with its icon, matched against whatever roles/modifiers are actually registered by MiraAPI in your current lobby, custom roles included. Icons can be toggled off in settings if you just want the color. Works standalone, no integration needed.

## Mod Integrations

All integrations are soft dependencies, meaning nothing breaks if they're missing, you just don't get the extra features.

### [Town of Us Mira](https://github.com/AU-Avengers/TOU-Mira)
- **Auto role info**: ability feedback that TOU-Mira would normally just flash on screen (Lookout results, Cleric feedback, Forensic reports, Oracle confessions, Doomsayer/Trapper/Inquisitor feedback, etc.) gets automatically appended to your notepad, so you don't have to remember it yourself. Empty/no-op results get filtered out so you're not left with junk lines. Toggleable in settings.
- **Player Jotting**: a "Jot Role" button shows up on each player during meetings. Click it to guess a role for that player, it gets appended under their name for the rest of the meeting. Click it again to remove the guess. The button only shows up on players whose role you don't already know for a fact, so you can't jot someone who's confirmed teammate, revealed, or a player you already know because you're dead.

## Settings

A "Notepad" tab in the local settings menu:

| Setting | Options |
|---|---|
| Button Row | Top row / Second row |
| Text Color | Black, White, Red, Yellow, Green, Cyan, Grey |
| Auto-Add Role Info | On/off (requires TOU-Mira) |
| Show Role Icons | On/off |
| Show Modifier Icons | On/off |

## Requirements

- [MiraAPI](https://github.com/All-Of-Us-Mods/MiraAPI)
- [BepInEx](https://github.com/BepInEx/BepInEx) (IL2CPP, .NET 6)
- Town of Us Mira, optional, only needed for the integration features above

## Installing

Drop `NotePadMod.dll` into your `BepInEx/plugins` folder alongside MiraAPI (and TOU-Mira if you want the extra features). Grab the DLL from the [Releases](../../releases) page or a CI build artifact.

## Building

```
dotnet restore NotePadMod/NotePadMod.csproj
dotnet build NotePadMod/NotePadMod.csproj --configuration Release
```

Targets .NET 6, built against BepInEx.Unity.IL2CPP and MiraAPI. CI builds and publishes the DLL on pushes to main/master and on tagged releases.

## License

GPLv3

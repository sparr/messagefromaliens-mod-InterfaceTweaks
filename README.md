# Interface Tweaks

A [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin for [Message From Aliens](https://store.steampowered.com/app/1858720/Message_From_Aliens/) with small changes to the game's interface.

## Installing

Install BepInEx first, if you haven't already, as described under [Requirements](#requirements). Then unzip the release zip into the game folder, the one that holds `MessageFromAliens.exe`. In Steam, right-click the game and choose Manage, then Browse local files, to open it. The zip holds `BepInEx/plugins/InterfaceTweaks/`, so the mod lands in BepInEx's plugins folder. To update, unzip a newer release over it. To uninstall, delete `BepInEx/plugins/InterfaceTweaks/`.

## Tweaks

* **Exit confirmation**: The exit button in the home page's top-right menu opens a confirmation page instead of quitting immediately.
* **Right-aligned settings**: On the game's settings page, each row's choices are moved to the right edge of the row, so they line up in a column instead of starting one cell after the row's name.
* **Chapter numbers**: In the chapter menus, each chapter's name is preceded by its number, with a more descriptive prefix in the chapter's heading. These match the game's achievements and Discord spoiler threshold channels.
* **Quiz number values**: Once the game shows numbers' values in parentheses after them, numbers typed into a quiz's answer slots get their values too, on the label of each number's last glyph. A number is a run of adjacent slots holding digits, with an optional negative sign, repeat, or scientific form, read the way the game writes numbers.

## Configuration

`BepInEx/config/sparr.messagefromaliens.interfacetweaks.cfg`, created on first launch:

| Key | Default | Effect |
|---|---|---|
| `ExitConfirmation.Enabled` | `true` | Ask before the exit button quits the game. |
| `Settings.RightAlignChoices` | `true` | Line up settings rows' choices at the right edge. |
| `Chapters.Numbers` | `true` | Number the chapters in the chapter menus and chapter titles, as the chapter achievements do. |
| `Quizzes.NumberValues` | `true` | Show the values of numbers typed into quiz answers, once the game shows numbers' values. |

Each setting is tagged with its name in glyphs, and the mod names itself in glyphs, so Mod Settings can show them in the game.

## Requirements

- BepInEx 5.4.23.6 or later, `BepInEx_win_x64` build, unzipped into the game folder. Tested with Unity 6000.6.8f1.
- On Linux under Proton, set the Steam launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
- .NET SDK 8 or later to build.

## Building

```sh
dotnet build -c Release
```

The build copies `InterfaceTweaks.dll` to `<game>/BepInEx/plugins/InterfaceTweaks/`. Disable the copy with `-p:DeployToGame=false`.

The build looks for the game in `<SteamLibraryDir>/steamapps/common/Message From Aliens`. `SteamLibraryDir` comes from the first of these that is set:

1. `-p:SteamLibraryDir=/path/to/SteamLibrary` on the command line.
2. A `SteamLibraryDir` environment variable.
3. `GamePaths.props`, a copy of `GamePaths.props.example`. It is ignored by git.
4. Steam's default install location: `~/.local/share/Steam` on Linux, `C:\Program Files (x86)\Steam` on Windows.

For a game folder outside a Steam library, set `GameDir` to the game folder the same ways.

## Patches

| Method | Patch |
|---|---|
| `Draw.ToParentPage` | Prefix: when only the home page is on the page stack, where the game would quit, opens the confirmation page instead. |
| `Draw.ToPage` | Prefix: on the confirmation page, quits for the yes line and returns to the home page for the no line. |
| `Draw.CurrentPageIsMenu` | Postfix: true on the confirmation page, so its lines are drawn, highlighted, and clicked like a menu's. |
| `Global.GetInfo` | Prefix: supplies the confirmation page's lines and its explanation. |
| `Draw.Write` | Prefix: in the chapter menus and a chapter's title, adds the chapter's number before its name, and in the title the glyphs for its menu. Postfix: removes those glyphs from the drawn lines' link targets, and highlights the lines of chapters that contain the marked word, which the game's own check misses because of the added glyphs. |
| `Draw.Write` | Postfix: adds the values of numbers in the page's quiz answer slots to their labels. |
| `AlienCharacter.UpdateEarthText` | Postfix: adds the value again when the game rewrites the label of an answer slot that ends a number. |
| `Draw.Write` | Prefix: on the settings page and pages opened from it, fills each choice row's gap with empty cells up to the row's width of 12. |

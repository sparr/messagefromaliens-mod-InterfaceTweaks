# Interface Tweaks

A [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin for [Message From Aliens](https://store.steampowered.com/app/1858720/Message_From_Aliens/) with small changes to the game's interface.

## Installing

Install BepInEx first, if you haven't already, as described under [Requirements](#requirements). Then unzip the release zip into the game folder, the one that holds `MessageFromAliens.exe`. In Steam, right-click the game and choose Manage, then Browse local files, to open it. The zip holds `BepInEx/plugins/InterfaceTweaks/`, so the mod lands in BepInEx's plugins folder. To update, unzip a newer release over it. To uninstall, delete `BepInEx/plugins/InterfaceTweaks/`.

## Tweaks

* **Exit confirmation**: The exit button in the home page's top-right menu opens a confirmation page instead of quitting immediately.
* **Right-aligned settings**: On the game's settings page, each row's choices are moved to the right edge of the row, so they line up in a column instead of starting one cell after the row's name.
* **Chapter numbers**: In the chapter menus, each chapter's name is preceded by its number, with a more descriptive prefix in the chapter's heading. These match the game's achievements and Discord spoiler threshold channels.
* **Win pages**: When a chapter's wins reach certain counts, the game shows a page in the left panel, but no menu links to it, so after a restart it is gone. Each one you have unlocked is listed at the end of the menu that holds the chapter's last quiz, and opens on the right like the menu's other pages.
* **Quiz number values**: Once the game shows numbers' values in parentheses after them, numbers typed into a quiz's answer slots get their values too, on the label of each number's last glyph. A number is a run of adjacent slots holding digits, with an optional negative sign, repeat, or scientific form, read the way the game writes numbers.
* **Combined number labels**: Once the game shows numbers' values in parentheses after them, each number the game gives a value, and each number typed into a quiz's answer slots, gets one label centered under all of its glyphs in place of theirs. It holds the number in base 7 as its glyphs write it, with a point, a line drawn above repeating digits, and scientific form only where the glyphs use it, as in `1.2E-10`, whose exponent is also in base 7. The number's base 10 value follows in parentheses, as the game writes it, unless it reads the same, as in `10 (7)`. When a unit follows the number and the game shows that unit's value in human units, the unit joins the label too, as its translation and then that value, as in `315 (162) length unit (259 m)`. A unit after a number that follows a glyph translated as `and`, `or`, or `to` keeps its own label when the number before that glyph has no unit, since the unit belongs to both numbers, as in "1 to 2 year". A translation that lists alternatives separated by punctuation, such as `/` or `,`, counts if any of them is one of those words. While the mouse is over the number, its label, or its glyphs' labels, and while one of those labels is being edited, the glyphs' own labels show instead, so they can still be edited.
* **Page notes**: After the right panel's explain button opens a page's explanation in the left panel, the button is replaced by a notes button, which opens a notes page for the right panel's page in the left panel. The notes page is titled with the page's name and that glyph, and holds one English text field, bounded by the panel's dividers and the screen's edge, edited as Glyph Notes' field is: Enter starts a new line, and the text shrinks to fit. While the notes show, the explain button is back, so clicking it again returns to the explanation. Each page has its own notes, so each lesson and quiz has its own, even where they share an explanation. While the left panel shows a page's notes, opening another page on the right shows that page's notes in their place. Notes are saved in your save slot as entries whose names start with `interfacetweaks.pagenote:`, which the game ignores, and undo covers them.
* **Compact minesweeper**: On the minesweeper board, the board's glyphs have no translations under them, and its rows are drawn without gaps between them. The solution the game shows in the left panel after a wrong answer is drawn the same way.
* **Centered minesweeper**: The minesweeper board is moved 3.5 cells right, to the middle of the right panel. With the compact board, it is also moved 1 cell down, away from the line under the page's text.
* **Undo translations**: Left Ctrl+Z, by default (the `Undo.Shortcut` setting), undoes the latest translation change and returns to the screen it was made on: the right panel's page, the left panel's message and its page, and the dictionary's category and filter. Undo repeats back through earlier changes. Changes that other mods save in the game's dictionary are included too, except Mod Settings' record of the unlock messages it has shown. A change can't be undone once the right panel can't be drawn again as it was, as after a move in a game; pressing the shortcut then drops that change and keeps older ones. While the note editor has a different note open than when the change was made, or the note was edited since, undo reverts the translation without changing the screen. The shortcut does nothing while a label is being edited.

Each minesweeper tweak has a row of glyphs in the board page's bottom-right corner, its name and then yes or no, that turns it on and off.

## Configuration

`BepInEx/config/sparr.messagefromaliens.interfacetweaks.cfg`, created on first launch:

| Key | Default | Effect |
|---|---|---|
| `ExitConfirmation.Enabled` | `true` | Ask before the exit button quits the game. |
| `Settings.RightAlignChoices` | `true` | Line up settings rows' choices at the right edge. |
| `Chapters.Numbers` | `true` | Number the chapters in the chapter menus and chapter titles, as the chapter achievements do. |
| `Chapters.WinPages` | `true` | List the unlocked win pages at the end of each chapter's menu. |
| `Quizzes.NumberValues` | `true` | Show the values of numbers typed into quiz answers, once the game shows numbers' values. |
| `Numbers.CombinedLabels` | `true` | Give each number one label with its value in base 7 and base 10, and its unit, once the game shows numbers' values. |
| `Notes.PageNotes` | `true` | Replace the explain button with a notes button while the explanation shows, and keep a notes page for each page. |
| `Minesweeper.CompactBoard` | `true` | Hide the translations of the minesweeper board and its solution, and close the gaps between their rows. |
| `Minesweeper.CenterBoard` | `true` | Move the minesweeper board to the middle of the right panel, and with the compact board, 1 cell down. |
| `Undo.Enabled` | `true` | Keep a list of translation changes that the undo shortcut undoes. |
| `Undo.Shortcut` | `Z + LeftControl` | The undo shortcut, as BepInEx writes keyboard shortcuts. |

Each setting except `Undo.Shortcut` is tagged with its name in glyphs, and the mod names itself in glyphs, so Mod Settings can show them in the game. Mod Settings would show the shortcut as English key names in a text field, where a mistyped value would silently unbind undo.

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
| `Global.GetInfo` | Prefix: supplies the confirmation page's lines and its explanation. Postfix: adds the chapter's unlocked win pages to the end of the menu that holds its last quiz. |
| `Global.GetWin` | Postfix: true for an unlocked win page, so its menu line shows as done, the menu can still be won, and reading the page records no win. |
| `Global.SetWin` | Prefix: skips win pages, so they never add to a chapter's win count. |
| `Draw.Write` | Prefix: in the chapter menus and a chapter's title, adds the chapter's number before its name, and in the title the glyphs for its menu. Postfix: removes those glyphs from the drawn lines' link targets, and highlights the lines of chapters that contain the marked word, which the game's own check misses because of the added glyphs. |
| `Draw.Write` | Prefix: notes that the page's answer slots are being drawn. Postfix: adds the values of numbers in the page's quiz answer slots to their labels. |
| `AlienCharacter.UpdateEarthText` | Postfix: adds the value again when the game rewrites the label of the glyph that ends a number in the answer slots. |
| `Draw.UpdateText1` | Prefix: shows the labels that combined number labels hid, before the redraw reuses the glyphs. Postfix (late): finds the drawn numbers and their units, puts each one's combined label on the first glyph of its widest row, and hides its other glyphs' labels. |
| `Draw.Update` | Postfix: shows a number's own labels while the mouse is over it or one of them is being edited, and combines them again after, and draws the lines above repeating digits where TMP laid them out. |
| `AlienCharacter.UpdateEarthText` | Postfix (late): when the game rewrites the label of a glyph in a combined number, puts back the combined label or hides the label again. |
| `Draw.ResetTextPosition` | Postfix: after the game puts every label back under its glyph, which it does on most frames, widens each combined number label to span its row of the number. |
| `Draw.Write` | Prefix: on the settings page and pages opened from it, fills each choice row's gap with empty cells up to the row's width of 12. |
| `Draw.UpdateText1` | Prefix: returns the board's glyphs to their usual positions. Postfix: on the board and the left panel's solution page, hides the board's translations and moves its rows up to close the gaps when compact, and on the board, moves its glyphs and the cells' frames when centered and moves the frames' rows together when compact. |
| `Draw.Update` | Prefix (last), postfix (first), and finalizer: mark the game's own `Update` as running, and as done even if it throws, so the `Camera.ScreenToWorldPoint` patch applies only to the game's mouse tests. |
| `Camera.ScreenToWorldPoint(Vector3)` | Postfix: during the game's `Draw.Update` on the minesweeper board, maps the mouse's point on the compact or moved board back to the game's layout, so clicks and hovering find the cell drawn under the mouse. |
| `Draw.UpdateText1` | Postfix: on the minesweeper board, draws a toggle for each minesweeper tweak in the bottom-right corner. |
| `Draw.Update` | Prefix: a left click on a toggle flips its setting. |
| `HardwareCursor.Apply` | Prefix: gives the toggles the game's button cursor when the mouse is over them. |
| `Draw.UpdateText1` | Prefix: while the left panel shows another page's notes, replaces them with the current page's. Postfix: shows or hides the notes field for the redrawn left panel. |
| `Draw.Write` | Prefix: in the right menu, while the left panel shows the page's explanation, replaces the explain button with the notes button. |
| `Draw.Update` | Prefix: a left click on the notes button opens the page's notes. Postfix: places the notes field over the left panel's text area. |
| `Global.SaveGame` | Prefix: compares the dictionary with its copy from the last save, and pushes any changed translations onto the undo stack with the screen of the last redraw. |
| `Global.LoadGame` | Finalizer: clears the undo stack, also when the load throws after replacing the save. |
| `Draw.UpdateText1` | Prefix: notes which dictionary page was shown before the redraw changes it. Postfix (last): records the navigation state and the right panel's glyphs, as the screen of later changes. |
| `Draw.Update` | Prefix: on the undo shortcut, writes the latest change's old values back, restores its screen, and redraws. If the right panel's glyphs differ from the screen's, reverts the undo and drops that change. |

using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

// The mod's name in glyphs, as game word IDs, for Mod Settings and any other mod that lists mods.
[assembly: AssemblyMetadata("MessageFromAliens.Name", "window de small modify ness")]

namespace InterfaceTweaks
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // The English name stands in for the glyph name above where glyphs cannot be shown.
        public const string Guid = "sparr.messagefromaliens.interfacetweaks";
        public const string Name = "Interface Tweaks";
        public const string Version = "0.4.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;
        internal static ConfigEntry<bool> ConfirmExit;
        internal static ConfigEntry<bool> RightAlignSettings;
        internal static ConfigEntry<bool> NumberChapters;
        internal static ConfigEntry<bool> QuizNumberValues;
        internal static ConfigEntry<bool> CombinedNumbers;
        internal static ConfigEntry<bool> WordUnits;
        internal static ConfigEntry<bool> LoneUnits;
        internal static ConfigEntry<bool> WinPages;
        internal static ConfigEntry<bool> NotebookTyping;
        internal static ConfigEntry<bool> NoteImageButton;
        internal static ConfigEntry<bool> SearchButton;
        internal static ConfigEntry<bool> DictionaryFilter;
        internal static ConfigEntry<bool> CompactMinesweeper;
        internal static ConfigEntry<bool> CenterMinesweeper;
        internal static ConfigEntry<bool> IncrementalColumns;
        internal static ConfigEntry<bool> RaiseUnderlines;
        internal static ConfigEntry<bool> Hotkeys;
        internal static ConfigEntry<bool> SkipDuplicateHistory;
        internal static ConfigEntry<bool> UndoEnabled;
        internal static ConfigEntry<KeyboardShortcut> UndoShortcut;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            ConfirmExit = Config.Bind("ExitConfirmation", "Enabled", true,
                Described("Ask for confirmation before the home page's exit button quits the game.", "end"));
            RightAlignSettings = Config.Bind("Settings", "RightAlignChoices", true,
                Described("On the settings page and the pages under it, line up each row's choices at the right edge instead of one cell after its name.", "right word"));
            NumberChapters = Config.Bind("Chapters", "Numbers", true,
                Described("Put each chapter's number before its name in the chapter menus and the chapter's title, numbered as the game's chapter achievements are.", "science number"));
            QuizNumberValues = Config.Bind("Quizzes", "NumberValues", true,
                Described("Once the game shows numbers' values in parentheses, show them for numbers typed into quiz answers too.", "riddle number", gate: Gates.Values,
                    unlock: "translate number in riddle"));
            CombinedNumbers = Config.Bind("Numbers", "CombinedLabels", true,
                Described("Once the game shows numbers' values in parentheses, give each number one label under all of its glyphs: its value in base 7 as its glyphs write it, then in base 10, and the unit after it with its value in human units once the game shows those. The glyphs' own labels show while the mouse is over the number.", "connect number", gate: Gates.Values,
                    unlock: "connect number de translate"));
            WordUnits = Config.Bind("Numbers", "WordUnits", true,
                Described("Where the game's text writes a number and then a unit as a separate word, give the unit the value in human units the game gives units written with their number, shown once the game shows those.", "unit word", gate: Gates.Units,
                    unlock: "translate unit word"));
            LoneUnits = Config.Bind("Numbers", "LoneUnits", true,
                Described("Give a unit with no number before it the value of one of that unit in human units, as the game gives units written with their number, shown once the game shows those.", "alone unit", gate: Gates.Units,
                    unlock: "translate alone unit"));
            WinPages = Config.Bind("Chapters", "WinPages", false,
                Described("At the end of each chapter's menu, list the pages the game showed in the left panel when the chapter's wins reached certain counts, and at the end of a menu, the pages Mod Settings showed there when one of the menu's pages unlocked a mod's feature.", "science unlock", gate: Gates.WinPage,
                    unlock: "plural unlock de page in science"));
            NotebookTyping = Config.Bind("Notes", "TypeGlyphs", true,
                Described("While the note editor shows a note, type English to list the glyphs whose translations match, Up and Down to choose one, and Tab to insert it at the cursor.", "word draw", gate: Gates.NoteEditor,
                    unlock: "write word by translate", unlockPage: "home"));
            NoteImageButton = Config.Bind("Notes", "ImageButton", true,
                Described("In the note editor, show a button at the left end of the buttons at the top right that copies an image of the whole note, every page of it, to the clipboard.", "word picture", gate: Gates.NoteEditor,
                    unlock: "picture de write", unlockPage: "home"));
            SearchButton = Config.Bind("Dictionary", "SearchButton", true,
                Described("On the dictionary page, while the left panel shows a word's definition, show a button at the left end of the buttons at the top right that lists in the left panel every sentence you can see in the game so far that contains the word.", "see sentence with word", gate: Gates.Dictionary,
                    unlock: "see sentence with word"));
            DictionaryFilter = Config.Bind("Dictionary", "TypeFilter", true,
                Described("On the dictionary page, show a button at the left end of the buttons at the top right. Click it or press Ctrl+F, then type, to list only the categories and words whose translations contain the typed text, which shows in a box in place of the button's translation. Clicking it or Ctrl+F again empties the box, and Escape removes it.", "see word by translate", gate: Gates.Dictionary,
                    unlock: "see word by translate"));
            CompactMinesweeper = Config.Bind("Minesweeper", "CompactBoard", true,
                Described("On the minesweeper board and the solution shown after a wrong answer, hide the translations and close the gaps between rows. Also toggled by the glyphs in the board page's bottom-right corner.", "trap tohide", gate: MinesweeperLayout.BoardGate,
                    unlock: "small detrap in small game"));
            CenterMinesweeper = Config.Bind("Minesweeper", "CenterBoard", true,
                Described("Move the minesweeper board 3.5 cells right, to the middle of the right panel, and with the compact board, 1 cell down. Also toggled by the glyphs in the board page's bottom-right corner.", "trap center", gate: MinesweeperLayout.BoardGate,
                    unlock: "center detrap in small game"));
            IncrementalColumns = Config.Bind("Incremental", "FixedColumns", true,
                Described("In the incremental game, put each row's parts in fixed columns so they stay in place as the numbers grow: the money glyph and amount first, then the time from the seventh cell, and on each building's row its glyph and count first, then the price glyph in the fifth cell and the price after it.", "money position", gate: IncrementalLayout.Gate,
                    unlock: "same position in money enlarge"));
            RaiseUnderlines = Config.Bind("Underlines", "RaiseWhenLabelsHidden", true,
                Described("While translations are hidden, draw the lines under links and other underlined glyphs just below the glyphs instead of below the hidden translations.", "line up"));
            Hotkeys = Config.Bind("Keys", "Shortcuts", true,
                Described("Keyboard shortcuts for the game's own controls. In the number rule game, 1 to 7 enter digits. Page Up and Page Down go to a panel's previous and next page, both in the same panel: the panel last used when both panels have page buttons, so on its first or last page one of them does nothing. Tab and Shift+Tab move between translations through one panel's rows, then the other's.", "button"));
            SkipDuplicateHistory = Config.Bind("History", "SkipDuplicates", true,
                Described("Don't add a left panel history entry that is the same as the one it shows, as the game adds when a glyph is right-clicked twice.", "no again"));
            UndoEnabled = Config.Bind("Undo", "Enabled", true,
                Described("Keep a list of translation changes, and undo the latest with the undo shortcut, returning to the page it was made on. Changes made on a screen that can no longer be shown, such as before a game's turn, are dropped.", "anti translate"));
            UndoShortcut = Config.Bind("Undo", "Shortcut", new KeyboardShortcut(UnityEngine.KeyCode.Z, UnityEngine.KeyCode.LeftControl),
                "The key that undoes the latest translation change, when no label is being edited.");

            // Redraw the current page, so a change from Mod Settings shows at once.
            Config.SettingChanged += (_, _) => Global.manager?.UpdateText();

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        // Mod Settings shows a setting tagged with its name in glyphs, as game word IDs.
        // gate: a gate token for the part of the game the setting is for, as the game's menu
        // lines start with, so Mod Settings shows the setting only once it passes. unlock: the
        // feature's glyphs, from which Mod Settings writes the game's unlock sentences for the
        // gate, in the explanation of each chapter whose quizzes the gate counts and on its win
        // count page, or with unlockPage, in that page's explanation.
        private static ConfigDescription Described(string description, string glyphs, AcceptableValueBase values = null,
            string gate = null, string unlock = null, string unlockPage = null)
        {
            var tags = new Dictionary<string, string> { ["MessageFromAliens.Label"] = glyphs };
            if (gate != null)
            {
                tags[Gates.Key] = gate;
            }
            if (unlock != null)
            {
                tags[unlockPage == null ? "MessageFromAliens.Unlock" : "MessageFromAliens.Unlock." + unlockPage] = unlock;
            }
            return new ConfigDescription(description, values, tags);
        }
    }
}

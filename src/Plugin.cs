using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

// The mod's name in glyphs, as game word IDs, for Mod Settings and any other mod that lists mods.
[assembly: AssemblyMetadata("MessageFromAliens.Name", "button de small modify ness")]

namespace InterfaceTweaks
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // The English name stands in for the glyph name above where glyphs cannot be shown.
        public const string Guid = "sparr.messagefromaliens.interfacetweaks";
        public const string Name = "Interface Tweaks";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> ConfirmExit;
        internal static ConfigEntry<bool> RightAlignSettings;
        internal static ConfigEntry<bool> NumberChapters;
        internal static ConfigEntry<bool> QuizNumberValues;
        internal static ConfigEntry<bool> WinPages;

        private void Awake()
        {
            Log = Logger;

            ConfirmExit = Config.Bind("ExitConfirmation", "Enabled", true,
                Described("Ask for confirmation before the home page's exit button quits the game.", "whether end"));
            RightAlignSettings = Config.Bind("Settings", "RightAlignChoices", true,
                Described("On the settings page and the pages under it, line up each row's choices at the right edge instead of one cell after its name.", "right option"));
            NumberChapters = Config.Bind("Chapters", "Numbers", true,
                Described("Put each chapter's number before its name in the chapter menus and the chapter's title, numbered as the game's chapter achievements are.", "science number"));
            QuizNumberValues = Config.Bind("Quizzes", "NumberValues", true,
                Described("Once the game shows numbers' values in parentheses, show them for numbers typed into quiz answers too.", "riddle number"));
            WinPages = Config.Bind("Chapters", "WinPages", true,
                Described("At the end of each chapter's menu, list the pages the game showed in the left panel when the chapter's wins reached certain counts.", "science unlock"));

            // Redraw the current page, so a change from Mod Settings shows at once.
            Config.SettingChanged += (_, _) => Global.manager?.UpdateText();

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        // Mod Settings shows a setting tagged with its name in glyphs, as game word IDs.
        private static ConfigDescription Described(string description, string glyphs, AcceptableValueBase values = null) =>
            new ConfigDescription(description, values, new Dictionary<string, string> { ["MessageFromAliens.Label"] = glyphs });
    }
}

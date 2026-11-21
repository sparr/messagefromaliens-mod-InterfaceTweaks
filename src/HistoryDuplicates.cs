using System;
using HarmonyLib;

namespace InterfaceTweaks
{
    // Draw.PushLeftText skips a push whose entry is the left panel's current entry, but it
    // compares the entry joined with "&" and stores text it does not parse, as a word's
    // information is, joined with "&&". So a second right-click on the same glyph adds a second,
    // identical entry. This skips a push that would store the current entry again, as the game
    // does for other entries. Callers redraw after pushing, so the screen still shows the entry.
    internal static class HistoryDuplicates
    {
        internal static bool IsCurrent(Draw d, string title, string info, bool noparse) =>
            d.text_history != null && d.currenttext >= 0 && d.currenttext < d.text_history.Count
            && d.text_history[d.currenttext] == title + (noparse ? "&&" : "&") + info;

        // Runs after other mods' prefixes, which can change the text, so it compares the entry
        // that would be stored.
        [HarmonyPatch(typeof(Draw), nameof(Draw.PushLeftText))]
        private static class PushLeftTextPatch
        {
            [HarmonyPriority(Priority.Last)]
            [HarmonyAfter("sparr.messagefromaliens.calculator", "sparr.messagefromaliens.compoundtranslations")]
            private static bool Prefix(Draw __instance, string title, string info, bool noparse, bool __runOriginal)
            {
                try
                {
                    return __runOriginal && !(Plugin.SkipDuplicateHistory.Value && IsCurrent(__instance, title, info, noparse));
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"PushLeftText prefix failed: {e}");
                    return __runOriginal;
                }
            }
        }
    }
}

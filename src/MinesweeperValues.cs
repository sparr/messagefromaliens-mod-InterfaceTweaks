using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace InterfaceTweaks
{
    // The minesweeper board writes each cell worth 8 or more, other than a multiple of 7, as the
    // compound glyph n<value>. Global.StringToListString gives value tokens only to the number
    // forms it parses, so the game never adds these cells' values to their labels. Puts the value
    // token it gives a whole number before each such glyph, so the game labels it as it labels
    // other numbers.
    internal static class MinesweeperValues
    {
        private static readonly Regex Compound = new Regex(@"^n(\d+)$");

        [HarmonyPatch(typeof(Global), nameof(Global.StringToListString))]
        private static class StringToListStringPatch
        {
            private static void Postfix(List<string> __result, bool modify)
            {
                if (!modify || __result == null || Global.alien_text == null)
                {
                    return;
                }
                try
                {
                    for (var i = 0; i < __result.Count; i++)
                    {
                        var m = Compound.Match(__result[i]);
                        if (!m.Success || !Global.alien_text.ContainsKey(__result[i])
                            || (i > 0 && __result[i - 1].StartsWith("(")))
                        {
                            continue;
                        }
                        __result.Insert(i, "(" + m.Groups[1].Value + ")number");
                        i++;
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogError($"Minesweeper values failed: {e}");
                }
            }
        }
    }
}

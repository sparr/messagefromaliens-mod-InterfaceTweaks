using System.Reflection;
using HarmonyLib;

namespace InterfaceTweaks
{
    // What the tweaks that change the game's text share about how Draw.Write reads it.
    internal static class GameText
    {
        // The overload of Draw.Write that takes the text as one string, which the game calls for
        // every section of a page.
        internal static MethodBase WriteString() =>
            AccessTools.Method(typeof(Draw), nameof(Draw.Write), new[]
            {
                typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool),
                typeof(bool), typeof(float), typeof(string), typeof(float), typeof(bool), typeof(int), typeof(int),
            });
    }
}

using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace InterfaceTweaks
{
    // Whether Calculator's calculator is open. The panel type is looked up once, on first use,
    // in the assembly of the plugin with Calculator's GUID. First use is in a frame update, after
    // every plugin has loaded. AccessTools.TypeByName would instead scan every type of every
    // loaded assembly, and log a warning when Calculator is not installed.
    internal static class CalculatorState
    {
        internal const string CalculatorGuid = "sparr.messagefromaliens.calculator";

        private static bool resolved;
        private static FieldInfo open;

        internal static bool IsOpen()
        {
            if (!resolved)
            {
                resolved = true;
                if (Chainloader.PluginInfos.TryGetValue(CalculatorGuid, out var info) && info.Instance != null)
                {
                    var panel = info.Instance.GetType().Assembly.GetType("Calculator.Panel", false);
                    open = panel == null ? null : AccessTools.Field(panel, "open");
                }
            }
            return open != null && (bool)open.GetValue(null);
        }
    }
}

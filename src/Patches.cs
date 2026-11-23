using System.Collections.Generic;
using HarmonyLib;

namespace InterfaceTweaks
{
    // The patches of game methods that more than one tweak patches. Harmony rebuilds a method
    // once for each patch class, so each method has one class here, with a patch method for each
    // kind and priority, which calls each tweak's part in turn. Each part catches its own
    // exceptions, so one tweak's failure does not stop the others.

    [HarmonyPatch(typeof(Draw), "Update")]
    internal static class DrawUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Draw __instance)
        {
            Hotkeys.UpdatePrefix(__instance);
            MinesweeperToggles.UpdatePrefix(__instance);
            NotebookTyping.UpdatePrefix(__instance);
            NoteImage.UpdatePrefix(__instance);
            DictionarySearch.UpdatePrefix(__instance);
            DictionaryFilter.UpdatePrefix(__instance);
            UndoTranslations.UpdatePrefix(__instance);
        }

        // After every other mod's prefix and before every other mod's postfix, so only the game's
        // own Update sees the minesweeper board's mouse mapping.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        private static void PrefixLast() => MinesweeperLayout.GameUpdateStarts();

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void PostfixFirst() => MinesweeperLayout.GameUpdateEnds();

        [HarmonyPostfix]
        private static void Postfix(Draw __instance)
        {
            NotebookTyping.UpdatePostfix(__instance);
            NumberLabels.UpdatePostfix();
            DictionarySearch.UpdatePostfix(__instance);
            DictionaryFilter.UpdatePostfix(__instance);
        }

        [HarmonyFinalizer]
        private static void Finalizer() => MinesweeperLayout.GameUpdateEnds();

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            TabOrder.UpdateTranspiler(instructions);
    }

    [HarmonyPatch(typeof(Draw), nameof(Draw.UpdateText1))]
    internal static class DrawUpdateText1Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Draw __instance)
        {
            MinesweeperLayout.UpdateText1Prefix();
            NumberLabels.UpdateText1Prefix();
            UndoTranslations.UpdateText1Prefix(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(Draw __instance)
        {
            MinesweeperLayout.UpdateText1Postfix(__instance);
            MinesweeperToggles.UpdateText1Postfix(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void PostfixLow(Draw __instance) => NumberLabels.UpdateText1Postfix(__instance);

        // Last, so undo's fingerprint of the screen includes what other mods draw.
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void PostfixLast(Draw __instance) => UndoTranslations.UpdateText1Postfix(__instance);
    }

    [HarmonyPatch]
    internal static class DrawWritePatch
    {
        private static System.Reflection.MethodBase TargetMethod() => GameText.WriteString();

        [HarmonyPrefix]
        private static void Prefix(Draw __instance, ref string s, string sec, out Dictionary<string, string> __state)
        {
            ChapterNumbers.WritePrefix(__instance, ref s, sec, out __state);
            NoteImage.WritePrefix(__instance, ref s, sec);
            DictionarySearch.WritePrefix(__instance, ref s, sec);
            DictionaryFilter.WritePrefix(__instance, ref s, sec);
            QuizNumberValues.WritePrefix();
            IncrementalLayout.WritePrefix(__instance, ref s, sec);
            RightAlignSettings.WritePrefix(__instance, ref s, sec);
            UnitTokens.WritePrefix(ref s);
            RaisedUnderlines.WritePrefix(__instance);
        }

        [HarmonyPostfix]
        private static void Postfix(Draw __instance, string sec, List<AlienCharacter> __result, Dictionary<string, string> __state)
        {
            ChapterNumbers.WritePostfix(__instance, sec, __result, __state);
            QuizNumberValues.WritePostfix(__instance, sec);
        }

        // Runs also when the game's Write throws, so each prefix's record is removed.
        [HarmonyFinalizer]
        private static void Finalizer(Draw __instance) => RaisedUnderlines.WriteFinalizer(__instance);
    }

    [HarmonyPatch(typeof(AlienCharacter), nameof(AlienCharacter.UpdateEarthText))]
    internal static class UpdateEarthTextPatch
    {
        [HarmonyPostfix]
        private static void Postfix(AlienCharacter __instance) => QuizNumberValues.UpdateEarthTextPostfix(__instance);

        // After Quiz Number Values adds its value.
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void PostfixLow(AlienCharacter __instance) => NumberLabels.UpdateEarthTextPostfix(__instance);
    }

    [HarmonyPatch(typeof(Global), nameof(Global.GetInfo))]
    internal static class GetInfoPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(string category, string s, ref string __result) =>
            ExitConfirmation.GetInfoPrefix(category, s, ref __result);

        [HarmonyPostfix]
        private static void Postfix(string s, ref string __result) => WinPages.GetInfoPostfix(s, ref __result);
    }
}

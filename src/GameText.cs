using System.Reflection;
using HarmonyLib;

namespace InterfaceTweaks
{
    // What the tweaks share about the game's text and how Draw.Write reads it.
    internal static class GameText
    {
        // Line and page breaks.
        internal const string Newline = "newline";
        internal const string Newpage = "newpage";
        // The empty cell.
        internal const string Gap = ".";
        // The note editor's cursor.
        internal const string Cursor = "notebookcursor";
        // Yes and no, as the game's settings write them.
        internal const string Yes = "yes";
        internal const string No = "no";
        // Global.GetInfo's category for page explanations, and the suffix of an explanation's
        // name after its page's name.
        internal const string InfoCategory = "alieninfo";
        internal const string ExplanationSuffix = " de tutorial";
        // The kind of a value token, "(value)number", which Global.StringToListString inserts
        // for a number's glyph and which becomes that glyph's extra_info.
        internal const string ValueKind = "number";
        // Draw.Write's sections: the menus, the titles, and the right panel's body.
        internal const string LeftMenu = "leftmenu";
        internal const string RightMenu = "rightmenu";
        internal const string LeftTitle = "lefttitle";
        internal const string RightTitle = "righttitle";
        internal const string Body = "righttextdown";
        // The cells in a row of a panel.
        internal const int RowWidth = 12;

        // The overload of Draw.Write that takes the text as one string, which the game calls for
        // every section of a page.
        internal static MethodBase WriteString() =>
            AccessTools.Method(typeof(Draw), nameof(Draw.Write), new[]
            {
                typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool),
                typeof(bool), typeof(float), typeof(string), typeof(float), typeof(bool), typeof(int), typeof(int),
            });

        // Whether Draw.Write gives the token a cell of its own. Line and page breaks, gates, value
        // tokens, buttons, underline brackets, and the note editor's cursor take none; the empty
        // cell does.
        internal static bool TakesCell(string token) =>
            token != "" && token != Newline && token != Newpage && token != Cursor
            && token[0] != '(' && token[0] != '[' && token[0] != '*' && token[0] != '{' && token[0] != '}';
    }
}

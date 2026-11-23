namespace InterfaceTweaks
{
    // Gate tokens for Mod Settings, so each setting is listed once the game shows the part of the
    // game it is for. Global.GetWin passes "[<chapter>-<quiz>-<n>]" (<quiz> is the game's word
    // for quiz) once n of the chapter's quizzes are won, and a token without a count once that
    // page is won. Mod Settings also reads "|" in a token as "or".
    internal static class Gates
    {
        internal const string Key = "MessageFromAliens.Gate";

        // AlienCharacter.UpdateEarthText shows numbers' values once this many quizzes are won.
        internal const string Values = "[arithmetic-riddle-11]";

        // It shows each kind of unit's value in human units once its own chapter's quizzes are
        // won, so units show once any of these passes.
        internal const string Units = "[mechanics-riddle-5|electricity-riddle-3|thermology-riddle-2]";

        // The first page the game shows in the left panel for a chapter's win count.
        internal const string WinPage = "[arithmetic-riddle-3]";

        // The home menu's line for the dictionary.
        internal const string Dictionary = "[arithmetic-riddle-3]";

        // The home menu's line for the note editor.
        internal const string NoteEditor = "[last-riddle]";
    }
}

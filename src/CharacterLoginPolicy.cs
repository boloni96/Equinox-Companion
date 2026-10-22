namespace EquinoxCompanion;

public enum CharacterLoginMode { Unavailable, InGame, Title, CharacterSelect }
internal enum CharacterLoginAction { Wait, Proceed, Confirm, CheckList, IdentifyLogin }

public static class CharacterLoginPolicy
{
    public static string Label(bool loaded) => loaded ? "Switch to this character" : "Log in as this character";

    internal static CharacterLoginAction InitialAction(CharacterLoginMode mode,
        CharacterLoginRelationship relationship, bool currentLoginKnown) =>
        mode == CharacterLoginMode.Title && !currentLoginKnown && relationship == CharacterLoginRelationship.Unverified
            ? CharacterLoginAction.IdentifyLogin : Action(mode, relationship);

    // A title-screen list search is already a Lifestream action. An unknown
    // account must receive the same pre-action warning as an in-game switch.
    // Consent permits a bounded list check, never a blind title-screen login.
    internal static CharacterLoginAction Action(CharacterLoginMode mode, CharacterLoginRelationship relationship,
        bool confirmed = false, bool consentCurrent = false)
    {
        if (mode == CharacterLoginMode.Unavailable) return CharacterLoginAction.Wait;
        if (relationship != CharacterLoginRelationship.Unverified) return CharacterLoginAction.Proceed;
        if (!confirmed || !consentCurrent) return CharacterLoginAction.Confirm;
        return mode == CharacterLoginMode.Title ? CharacterLoginAction.CheckList : CharacterLoginAction.Proceed;
    }

    // An offline confirmation must not become permission to log out a character
    // that appeared while the dialog was open, or to switch after another logout.
    public static bool SameSource(ulong expected, bool loaded, ulong current) =>
        expected == 0 ? !loaded : loaded && current == expected;

    // Keep the three supported Lifestream entry points distinct. A rejected
    // login never falls through into a second command or a different character.
    public static bool Request(CharacterLoginMode mode, string name, string world,
        Action<string> switchCharacter, Func<string, string, bool> titleLogin,
        Func<string, string, bool> selectionLogin)
    {
        if (!CharacterTravelPolicy.ValidTarget(name, world)) return false;
        switch (mode)
        {
            case CharacterLoginMode.InGame:
                switchCharacter(name + "@" + world);
                return true;
            case CharacterLoginMode.Title:
                return titleLogin(name, world);
            case CharacterLoginMode.CharacterSelect:
                return selectionLogin(name, world);
            default:
                return false;
        }
    }
}


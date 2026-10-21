namespace EquinoxCompanion;

public enum CharacterLoginMode { Unavailable, InGame, Title, CharacterSelect }

public static class CharacterLoginPolicy
{
    public static string Label(bool loaded) => loaded ? "Switch to this character" : "Log in as this character";

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

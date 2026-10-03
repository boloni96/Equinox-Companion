namespace EquinoxCompanion;

// Notification memory belongs to a character login, not the plugin lifetime.
public sealed class GardenNoticeSession
{
    private ulong character;
    private readonly HashSet<string> shown = [];

    public void Login()
    {
        character = 0;
        shown.Clear();
    }

    public bool ObserveCharacter(ulong contentId)
    {
        // Temporary missing player data during zoning is not a new login.
        if (contentId == 0 || contentId == character) return false;
        character = contentId;
        shown.Clear();
        return true;
    }

    public bool Add(string key) => shown.Add(key);
}

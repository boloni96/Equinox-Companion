namespace EquinoxCompanion;
public static class KeyboundPolicy
{
    public static bool CanSubmit(string liveWord,string input,string selected)=>selected.Length>0&&liveWord==selected&&input==selected;
    public static bool IsPhrase(string text)=>text.Length is >0 and <=160&&text.Any(char.IsLetterOrDigit)&&!text.Any(char.IsControl)&&!text.Contains('?')&&!text.Contains('*');
    public static bool OwnsInput(string input,string sent)=>input==sent;
}

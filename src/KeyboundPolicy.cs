namespace EquinoxCompanion;
public static class KeyboundPolicy
{
    public const int SoftMinMs=80,SoftMaxMs=150,SoftSubmitMs=80;
    public static int TypingDelay(int min,int max,double fraction){min=Math.Clamp(min,10,1000);max=Math.Clamp(max,10,1000);var low=Math.Min(min,max);var high=Math.Max(min,max);return low+(int)((high-low)*Math.Clamp(fraction,0,1));}
    public static bool LiveWord(bool containerVisible,bool labelVisible,bool textVisible,bool obscured)=>containerVisible&&((labelVisible&&textVisible)||obscured);
    public static bool CanClear(bool inputActive,string actual,string owned)=>inputActive&&actual.Length>0&&actual==owned;
    public static bool LoopTerritory(uint territory)=>territory is 1359 or 1360;
    public static bool ShouldRecall(string input,string selected,string lastSubmitted,bool attempted)=>!attempted&&input.Length==0&&selected.Length>0&&selected==lastSubmitted;
    public static bool CanRetryInput(string actual,string lastObserved)=>actual.Length==0||actual==lastObserved;
    public static bool AbandonMissingTarget(bool targetLive,string actual,string observed,string expected)=>!targetLive&&(actual.Length==0||actual==observed||actual==expected);
    public static bool CanSubmit(string liveWord,string input,string selected)=>selected.Length>0&&liveWord==selected&&input==selected;
    public static bool IsPhrase(string text)=>text.Length is >0 and <=160&&text.Any(char.IsLetterOrDigit)&&!text.Any(char.IsControl)&&!text.Contains('?')&&!text.Contains('*');
    public static bool OwnsInput(string input,string sent)=>input==sent;
}

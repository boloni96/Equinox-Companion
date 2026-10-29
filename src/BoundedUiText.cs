namespace EquinoxCompanion;
// Bound both text length and measurement count regardless of imported error size.
internal static class BoundedUiText
{
    internal const int MaxMeasureCharacters=512;
    internal static string Prefix(string text,int count)
    {
        count=Math.Clamp(count,0,text.Length);
        if(count>0&&count<text.Length&&char.IsHighSurrogate(text[count-1])&&char.IsLowSurrogate(text[count]))count--;
        return text[..count];
    }
    internal static string Fit(string text,float width,Func<string,float> measure)
    {
        if(string.IsNullOrEmpty(text)||!float.IsFinite(width)||width<=0)return "";
        var shown=Prefix(text,MaxMeasureCharacters).Replace('\n',' ').Replace('\r',' ');
        var truncated=shown.Length<text.Length;
        if(!truncated&&measure(shown)<=width)return shown;
        const string tail="…";
        if(measure(tail)>width)return "";
        var low=0;var high=shown.Length;
        while(low<high){var mid=low+(high-low+1)/2;if(measure(Prefix(shown,mid)+tail)<=width)low=mid;else high=mid-1;}
        return Prefix(shown,low)+tail;
    }
}

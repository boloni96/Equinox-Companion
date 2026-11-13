namespace EquinoxCompanion;
public static class MapTravelPolicy
{
    // The live AreaMap stores one decimal digit, e.g. integer 11 + digit 3 = 11.3.
    // The SDK MouseCoords helper divides that digit by 100 and reports 11.03.
    public static float CursorCoordinate(ushort whole,ushort tenths)=>tenths<=9?whole+tenths/10f:float.NaN;
    public static string TooltipName(string text)=>text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()??"";
    public static bool CanSelectTown(string requested,string? actual,bool locked,bool unusable,bool current)=>!locked&&!unusable&&!current&&!string.IsNullOrWhiteSpace(actual)&&string.Equals(requested,actual,StringComparison.Ordinal);
    public static bool Hit(int markerX,int markerY,uint size,float hoverX,float hoverY)
    {
        if(size==0||!float.IsFinite(hoverX)||!float.IsFinite(hoverY))return false;
        var scale=size/100f;
        var x=markerX/2048f*41f/scale+1f;
        var y=markerY/2048f*41f/scale+1f;
        // Tooltip identity is checked separately; coordinates prevent same-name markers on another floor/map.
        return MathF.Max(MathF.Abs(x-hoverX),MathF.Abs(y-hoverY))<=.6f/scale+.1f;
    }
}

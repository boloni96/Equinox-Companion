namespace EquinoxCompanion;
public static class MapTravelPolicy
{
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

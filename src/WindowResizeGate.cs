namespace EquinoxCompanion;
internal sealed class WindowResizeGate
{
    private float width,height;
    private bool initialized;
    private long resumeAt;
    internal bool Active{get;private set;}
    internal static bool OnBorder(float x,float y,float width,float height) =>
        x>=-8&&y>=-8&&x<=width+8&&y<=height+8&&(x<=8||y<=8||x>=width-16||y>=height-16);
    internal bool Update(float nextWidth,float nextHeight,bool mouseDown,bool resizeCursor,long now)
    {
        if(!float.IsFinite(nextWidth)||!float.IsFinite(nextHeight)||nextWidth<=0||nextHeight<=0){Active=true;return true;}
        var changed=initialized&&(Math.Abs(width-nextWidth)>.5f||Math.Abs(height-nextHeight)>.5f);
        width=nextWidth;height=nextHeight;initialized=true;
        // Catch the initial click on the grip before the first size change too.
        if(mouseDown&&(resizeCursor||changed||Active))resumeAt=now+120;
        Active=now<resumeAt;
        return Active;
    }
}

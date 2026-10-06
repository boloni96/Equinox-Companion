using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private void DrawCofferAppearance()
    {
        if (!ImGui.CollapsingHeader("Coffer appearance")) return;
        ImGui.TextWrapped("Choose your own style for both maps. Red = unopened; green = opened. This preference stays on this PC.");
        for (var i=0;i<CofferStyles.Names.Length;i++)
        {
            ImGui.PushID(i);
            var selected=CofferStyles.Normalize(config.CofferStyle)==i;
            if(ImGui.Selectable((selected?"Selected: ":"")+CofferStyles.Names[i],selected,ImGuiSelectableFlags.None,new Vector2(180,30)))
            {
                RestoreCofferTints();config.CofferStyle=i;SaveConfiguration();
                Chat.Print("[Equinox] Coffer style saved: "+CofferStyles.Names[i]+".");
            }
            ImGui.SameLine();
            var at=ImGui.GetCursorScreenPos();var draw=ImGui.GetWindowDrawList();
            DrawCofferSymbol(draw,at+new Vector2(16,15),13,false,i);
            DrawCofferSymbol(draw,at+new Vector2(54,15),13,true,i);
            ImGui.Dummy(new Vector2(72,30));ImGui.PopID();
        }
    }
    private void DrawCofferSymbol(ImDrawListPtr draw,Vector2 center,float size,bool opened,int style)
    {
        style=CofferStyles.Normalize(style);
        var ink=opened?0xff66ee55u:0xff6666ffu;
        const uint dark=0xff201a17, shine=0xfff3eeee;
        Vector2 P(float x,float y)=>center+new Vector2(x,y)*size;
        void Line(float x,float y,float a,float b,uint color,float width=1.7f)=>draw.AddLine(P(x,y),P(a,b),color,width);
        void Box(float x,float y,float a,float b,uint fill,float round=0)=>draw.AddRectFilled(P(x,y),P(a,b),fill,round);
        if(style==0)
        {
            var icon=Textures.GetFromGameIcon(CofferIcon).GetWrapOrDefault();
            if(icon!=null)draw.AddImage(icon.Handle,center-new Vector2(size),center+new Vector2(size),Vector2.Zero,Vector2.One,opened?0xff2dff2du:0xff2d2dffu);
            return;
        }
        if(style==4) // Pixel chest: solid square pixels, no anti-aliased curves.
        {
            Box(-.9f,-.55f,.9f,.75f,dark);Box(-.75f,-.8f,.75f,-.4f,ink);
            Box(-.9f,-.55f,-.65f,.65f,ink);Box(.65f,-.55f,.9f,.65f,ink);
            Box(-.9f,.5f,.9f,.75f,ink);Box(-.9f,-.1f,.9f,.1f,ink);
            Box(-.15f,-.1f,.15f,.3f,shine);
            return;
        }
        var round=style==2?size*.38f:style==5?size*.12f:1;
        Box(-.9f,-.65f,.9f,.7f,dark,round);
        draw.AddRect(P(-.9f,-.65f),P(.9f,.7f),ink,round,ImDrawFlags.None,2);
        if(style==2) // Rounded arched lid.
        {
            draw.AddBezierCubic(P(-.88f,-.15f),P(-.85f,-1.1f),P(.85f,-1.1f),P(.88f,-.15f),ink,2);
            Line(-.85f,-.05f,.85f,-.05f,ink);
        }
        else Line(-.86f,-.12f,.86f,-.12f,ink);
        if(style is 1 or 3)
        {
            Line(-.5f,-.6f,-.5f,.64f,ink);Line(.5f,-.6f,.5f,.64f,ink);
            Line(-.8f,.55f,.8f,.55f,ink,1);
        }
        if(style==3) // Jewel and a small crown distinguish the royal chest.
        {
            draw.AddQuadFilled(P(0,-.35f),P(.23f,0),P(0,.35f),P(-.23f,0),shine);
            Line(-.5f,-.72f,-.65f,-1,ink);Line(-.65f,-1,-.2f,-.8f,ink);
            Line(-.2f,-.8f,0,-1.12f,ink);Line(0,-1.12f,.2f,-.8f,ink);
            Line(.2f,-.8f,.65f,-1,ink);Line(.65f,-1,.5f,-.72f,ink);
        }
        else Box(-.12f,-.22f,.12f,.25f,style==5?ink:shine,1);
    }
}

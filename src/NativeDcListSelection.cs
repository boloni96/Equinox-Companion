using System.Runtime.InteropServices;
namespace EquinoxCompanion;
public readonly record struct NativeDcListChoice(int EventParameter,int Category,int Item)
{
    public int PackedItem => (Category<<8)|Item;
}
public static class NativeDcListSelection
{
    // The DC menu uses four region slots, each containing four DC slots.
    // The native item key is region-relative, not the rendered row index.
    public static NativeDcListChoice? Find(uint nodeId,string expected,IReadOnlyList<string> names)
    {
        var size=nodeId==23?16:nodeId==24?8:0;
        if(size==0||names.Count!=size||string.IsNullOrWhiteSpace(expected))return null;
        var found=-1;
        for(var i=0;i<size;i++)if(names[i]==expected){if(found>=0)return null;found=i;}
        if(found<0)return null;
        return nodeId==23?new(1,found/4,found%4):new(2,0,found);
    }
}
// LobbyDKTWorldList's tree-click payload includes an indirect item key.
// Keep this addon-specific payload separate from ordinary list input.
[StructLayout(LayoutKind.Explicit,Size=0x40)]
public struct NativeDcTreeKey
{
    [FieldOffset(4)] public byte Selected;
    [FieldOffset(8)] public int Item;
}
[StructLayout(LayoutKind.Explicit,Size=0x40)]
public unsafe struct NativeDcTreeInput
{
    [FieldOffset(8)] public NativeDcTreeKey** Key;
    [FieldOffset(16)] public int Highlight;
    [FieldOffset(24)] public byte MouseButton;
}

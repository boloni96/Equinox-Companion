namespace EquinoxCompanion;

internal sealed record QuestCatalogueRow(uint Id,string Name,string Group);
internal enum EventRoutineAction { Quest, Farm, BuyAndUse, Verify, Next }
internal static class QuestToolsPolicy
{
    internal static bool EventCharacterReady(string current,string target,int serviceAccount)=>
        current.Length>0&&target.Length>0&&(current==target||serviceAccount is >=0 and <=15);
    internal const uint KeywardBound=5466;
    internal static uint ShortId(uint id)=>id&0xffff;
    internal static EventRoutineAction Next(bool questComplete,bool planKnown,long cost,long wallet,int usable,int held)
    {
        if(!questComplete)return EventRoutineAction.Quest;
        if(!planKnown||held>0)return EventRoutineAction.Verify;
        if(cost>0)return wallet>=cost?EventRoutineAction.BuyAndUse:EventRoutineAction.Farm;
        return usable>0?EventRoutineAction.BuyAndUse:EventRoutineAction.Next;
    }
    internal static bool RecordWindow(string name)=>!string.IsNullOrWhiteSpace(name)&&
        !name.StartsWith("_",StringComparison.Ordinal)&&
        !new[]{"Chat","Social","Friend","BlackList","LinkShell","FreeCompany","PartyMember","Config","InputString","InputSearch","Letter","Mail","Login","Title","CharaSelect"}.Any(x=>name.Contains(x,StringComparison.OrdinalIgnoreCase));
}

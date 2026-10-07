using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Queue<object> followTravelDiagnostics=new();
    private DateTimeOffset nextTravelMenuDiagnostic;
    private readonly Dictionary<string,DateTimeOffset> recentTravelCallbacks=new();
    private string lastTravelMenuDiagnostic="";
    private void RecordFollowTravel(string kind,object detail)
    {
        while(followTravelDiagnostics.Count>=120)followTravelDiagnostics.Dequeue();
        followTravelDiagnostics.Enqueue(new {at=DateTimeOffset.UtcNow,kind,detail});
    }
    private static readonly string[] TravelDiagnosticAddons=["TeleportHousingFriend","HousingSelectRoom","MansionSelectRoom","HousingSelectBlock","TelepotTown","SelectString","SelectYesno","Talk"];
    private unsafe object? ReadTravelMenuDiagnostic(AtkUnitBase* addon)
    {
        if(addon==null||!addon->IsVisible||addon->AtkValues==null||addon->AtkValuesCount>1024)return null;
        var values=new List<object>();
        for(var i=0;i<addon->AtkValuesCount;i++){
            var v=addon->AtkValues[i];var type=(int)v.Type&15;
            if(type is 8 or 10){var text=TravelMenuText(v.String.Value);if(!string.IsNullOrEmpty(text))values.Add(new {index=i,text=text[..Math.Min(300,text.Length)]});}
            else if(type is 3 or 5)values.Add(new {index=i,number=v.UInt});
        }
        return new {name=addon->NameString,valueCount=addon->AtkValuesCount,values,textNodes=TravelNodeText(&addon->UldManager)};
    }
    private unsafe void ObserveTravelMenuDiagnostics(DateTimeOffset now)
    {
        if(!config.EnableFollowThem||!followSession.Armed&&transportCapture==null||now<nextTravelMenuDiagnostic)return;
        nextTravelMenuDiagnostic=now.AddSeconds(1);
        var menus=new List<object>();
        foreach(var name in TravelDiagnosticAddons){var value=ReadTravelMenuDiagnostic((AtkUnitBase*)GardenGui.GetAddonByName(name).Address);if(value!=null)menus.Add(value);}
        var key=System.Text.Json.JsonSerializer.Serialize(menus);
        if(key==lastTravelMenuDiagnostic)return;lastTravelMenuDiagnostic=key;
        RecordFollowTravel("Menus changed",menus);
    }
    private unsafe void ObserveTravelCallbackDiagnostic(AtkUnitBase* addon,uint count,AtkValue* args)
    {
        if(!config.EnableFollowThem||!followSession.Armed&&transportCapture==null||addon==null||args==null||count>8||!TravelDiagnosticAddons.Contains(addon->NameString))return;
        var arguments=new List<object>();
        for(var i=0;i<count;i++)if(((int)args[i].Type&15) is 3 or 5)arguments.Add(new {index=i,number=args[i].Int});
        var callbackKey=addon->NameString+":"+usingSharedTravel+":"+System.Text.Json.JsonSerializer.Serialize(arguments);
        var now=DateTimeOffset.UtcNow;
        if(recentTravelCallbacks.TryGetValue(callbackKey,out var previous)&&now-previous<TimeSpan.FromSeconds(2))return;
        if(recentTravelCallbacks.Count>128)recentTravelCallbacks.Clear();
        recentTravelCallbacks[callbackKey]=now;
        RecordFollowTravel("Menu callback",new {addon=addon->NameString,automatic=usingSharedTravel,arguments,menu=ReadTravelMenuDiagnostic(addon)});
    }
}


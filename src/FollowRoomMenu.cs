using System.Security.Cryptography;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe string RoomMenuSignature(AtkUnitBase* addon)
    {
        if(addon==null||!addon->IsVisible||addon->AtkValues==null||addon->AtkValuesCount is <1 or >1024)return "";
        var text=new StringBuilder();
        for(var i=0;i<addon->AtkValuesCount;i++){
            var value=addon->AtkValues[i];
            if(((int)value.Type&15) is 8 or 10){var name=CopyMenuText(value.String.Value);if(name?.Length>500)return "";text.Append(i).Append(':').Append(name).Append('\n');}
        }
        return text.Length==0?"":Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
    private unsafe void CaptureFollowRoom(AtkUnitBase* addon,uint count,AtkValue* args)
    {
        if(!SharingTravel||usingSharedTravel||transportCapture is not {TravelKind:"door"} capture||addon==null||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectRoom").Address||args==null||count is <1 or >4||capture.Steps is not {Length:<8} steps)return;
        var signature=RoomMenuSignature(addon);if(signature.Length!=64)return;
        var values=new int[count];
        for(var i=0;i<count;i++){if(((int)args[i].Type&15) is not (3 or 5))return;values[i]=args[i].Int;}
        if(steps.LastOrDefault() is {Addon:"HousingSelectRoom"} prior&&prior.MenuSignature==signature&&prior.Arguments!.SequenceEqual(values))return;
        transportCapture=capture with {Steps=[..steps,new FollowMenuStep("Recorded private chamber selection",Addon:"HousingSelectRoom",Arguments:values,MenuSignature:signature)]};
    }
    private unsafe bool ReplayFollowRoom(FollowMenuStep step)
    {
        if(!FollowTransportPolicy.RoomStep(step))return false;
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectRoom").Address;
        if(RoomMenuSignature(addon)!=step.MenuSignature){TravelDiagnostic("Waiting for the same private-chambers list. If the list differs, choose the room manually.");return false;}
        var values=stackalloc AtkValue[step.Arguments!.Length];
        for(var i=0;i<step.Arguments.Length;i++){values[i].Type=AtkValueType.Int;values[i].Int=step.Arguments[i];}
        usingSharedTravel=true;try{addon->FireCallback((uint)step.Arguments.Length,values,true);}finally{usingSharedTravel=false;}
        return true;
    }
}

using System.Security.Cryptography;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string selectedRoomStep="";
    private DateTimeOffset selectedRoomAt;
    private unsafe string RoomMenuSignature(AtkUnitBase* addon)
    {
        if(addon==null||!addon->IsVisible||addon->AtkValues==null||addon->AtkValuesCount is <1 or >1024)return "";
        var text=new StringBuilder();
        for(var i=0;i<addon->AtkValuesCount;i++){
            var value=addon->AtkValues[i];
            if(((int)value.Type&15) is 8 or 10){var name=TravelMenuText(value.String.Value);if(name?.Length>500)return "";text.Append(i).Append(':').Append(name).Append('\n');}
        }
        return text.Length==0?"":Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
    private unsafe void CaptureFollowRoom(AtkUnitBase* addon,uint count,AtkValue* args)
    {
        if(!SharingTravel||usingSharedTravel||transportCapture is not {TravelKind:"door"} capture||addon==null||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectRoom").Address||args==null||count is <1 or >4||capture.Steps is not {Length:<8} steps)return;
        if(count==2&&((int)args[0].Type&15) is 3 or 5&&args[0].Int==0&&((int)args[1].Type&15) is 3 or 5&&args[1].Int is >=0 and <15){
            var offset=42+args[1].Int*12;
            if(addon->AtkValues!=null&&addon->AtkValuesCount>offset+4&&((int)addon->AtkValues[offset+3].Type&15) is 8 or 10&&((int)addon->AtkValues[offset+4].Type&15) is 8 or 10&&int.TryParse(TravelMenuText(addon->AtkValues[offset+3].String.Value),out var room)&&room is >=1 and <=512&&TravelMenuText(addon->AtkValues[offset+4].String.Value) is {Length:>0} owner){
                transportCapture=capture with {Steps=[..steps.Where(x=>FollowRoomTarget.Read(x)==null),new FollowRoomTarget(room,owner).Step()]};return;
            }
        }
        var signature=RoomMenuSignature(addon);if(signature.Length!=64)return;
        var values=new int[count];
        for(var i=0;i<count;i++){if(((int)args[i].Type&15) is not (3 or 5))return;values[i]=args[i].Int;}
        if(steps.LastOrDefault() is {Addon:"HousingSelectRoom"} prior&&prior.MenuSignature==signature&&prior.Arguments!.SequenceEqual(values))return;
        transportCapture=capture with {Steps=[..steps,new FollowMenuStep("Recorded private chamber selection",Addon:"HousingSelectRoom",Arguments:values,MenuSignature:signature)]};
    }
    private unsafe bool ReplayFollowRoom(FollowMenuStep step)
    {
        if(!FollowTransportPolicy.RoomStep(step))return false;
        if(FollowRoomTarget.Read(step) is {} target)return ReplayRoomTarget(step,target);
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectRoom").Address;
        if(RoomMenuSignature(addon)!=step.MenuSignature){TravelDiagnostic("Waiting for the same private-chambers list. If the list differs, choose the room manually.");return false;}
        var values=stackalloc AtkValue[step.Arguments!.Length];
        for(var i=0;i<step.Arguments.Length;i++){values[i].Type=AtkValueType.Int;values[i].Int=step.Arguments[i];}
        usingSharedTravel=true;try{addon->FireCallback((uint)step.Arguments.Length,values,true);}finally{usingSharedTravel=false;}
        return true;
    }
    private unsafe bool ReplayRoomTarget(FollowMenuStep step,FollowRoomTarget target)
    {
        var now=DateTimeOffset.UtcNow;var key=(pendingTransport?.Id??"")+step.MenuSignature;
        if(selectedRoomStep==key){
            var yes=(FFXIVClientStructs.FFXIV.Client.UI.AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
            if(yes!=null&&yes->IsVisible&&yes->PromptText!=null){
                var prompt=yes->PromptText->NodeText.ToString();
                if(!FollowRoomTarget.Confirmation(prompt,Player.CharacterName==target.Owner)){TravelDiagnostic("Private-chamber confirmation differs; waiting without accepting.");return false;}
                usingSharedTravel=true;try{SelectTravelChoice((AtkUnitBase*)yes,0);}finally{usingSharedTravel=false;}
                TravelDiagnostic("Requested private chamber "+target.Room+" belonging to "+target.Owner+".");return true;
            }
            if(now-selectedRoomAt<TimeSpan.FromSeconds(3))return false;
            // Do not repeatedly select a room while a transition is pending.
            TravelDiagnostic("Room selected; waiting for its confirmation or loading.");return false;
        }
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("HousingSelectRoom").Address;
        if(addon==null||!addon->IsVisible||!addon->IsReady||addon->AtkValues==null||addon->AtkValuesCount<54)return false;
        var matches=new List<int>();
        for(var row=0;row<15;row++){
            var offset=42+row*12;if(addon->AtkValuesCount<=offset+4)break;
            var number=addon->AtkValues[offset+3];var owner=addon->AtkValues[offset+4];
            if(((int)number.Type&15) is not (8 or 10)||((int)owner.Type&15) is not (8 or 10))continue;
            if(int.TryParse(TravelMenuText(number.String.Value),out var room)&&room==target.Room&&TravelMenuText(owner.String.Value)==target.Owner)matches.Add(row);
        }
        if(matches.Count!=1){TravelDiagnostic("Waiting for room "+target.Room+" / "+target.Owner+" in the private-chambers list. Select its page if needed.");return false;}
        var values=stackalloc AtkValue[2];values[0].Type=AtkValueType.Int;values[0].Int=0;values[1].Type=AtkValueType.Int;values[1].Int=matches[0];
        selectedRoomStep=key;selectedRoomAt=now;
        usingSharedTravel=true;try{addon->FireCallback(2,values,true);}finally{usingSharedTravel=false;}
        return false;
    }

}

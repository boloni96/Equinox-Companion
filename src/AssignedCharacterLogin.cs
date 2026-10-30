using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record AssignedLogin(SharedCharacter Target,ulong From,int ServiceAccount,string Pairing,string World,DateTimeOffset Deadline);
    private AssignedLogin? assignedLogin;
    private bool assignedDcSent,assignedServiceSent,assignedLoginSent;
    private DateTimeOffset assignedLoginNext,loginWorldNext;
    private readonly HashSet<ushort> loginWorldChecked=[];
    private ushort loginWorldPending,loginWorldSample;private DateTimeOffset loginWorldSentAt,loginWorldStableAt;
    private string assignedLoginError="";
    private bool identificationDcSent;
    private string LastLoginWorld(SharedCharacter target)
    {
        var observed=config.Discoveries.Where(e=>e.Actor.ContentId==target.ContentId).OrderByDescending(e=>e.At).FirstOrDefault();
        var presence=adventureBoards.Select(b=>b.Locations?.GetValueOrDefault(target.Id)).Where(x=>x!=null&&DateTimeOffset.TryParse(x.At,out _)).OrderByDescending(x=>DateTimeOffset.Parse(x!.At)).FirstOrDefault();
        if(presence!=null&&(observed==null||DateTimeOffset.Parse(presence.At)>observed.At))return presence.World;
        return !string.IsNullOrEmpty(observed?.Actor.CurrentWorldName)?observed.Actor.CurrentWorldName:target.World;
    }
    private bool StartAssignedCharacterSwitch(SharedCharacter target)
    {
        if(assignedLogin!=null)return assignedLogin.Target.Id==target.Id;
        var current=CurrentSharedCharacter();var index=CurrentLoginServiceAccount();
        if(current==null||index is <0 or >15||current.AccountId!=target.AccountId||!DeliveryCharacters().Any(x=>x.Character.Id==target.Id&&x.Person.Characters.Any(c=>c.Id==current.Id))){characterTravelStatus="Verify this client's current service account before automatic switching.";return false;}
        if(LifestreamBusy()||followSession.Armed||TraderRunning||HelperInCutscene()){characterTravelStatus="Finish the active operation before switching.";return false;}
        try{
            Pi.GetIpcSubscriber<object>("Lifestream.Logout").InvokeFunc();
            assignedLogin=new(target,Player.ContentId,index,config.PairingKey,LastLoginWorld(target),DateTimeOffset.UtcNow.AddMinutes(5));
            assignedDcSent=assignedServiceSent=assignedLoginSent=false;assignedLoginNext=default;loginWorldChecked.Clear();loginWorldNext=default;loginWorldPending=loginWorldSample=0;assignedLoginError="";
            characterSwitchFrom=Player.ContentId;characterSwitchTarget=target;characterSwitchUntil=assignedLogin.Deadline;
            characterTravelStatus=$"Switching using this client's verified service account {index+1}: {target.Name} @ {target.World}.";return true;
        }catch(Exception){characterTravelStatus="Lifestream logout is unavailable; no automatic login was requested.";return false;}
    }
    private static unsafe void LoginCallback(AtkUnitBase* addon,params int[] args)
    {
        var values=stackalloc AtkValue[args.Length];for(var i=0;i<args.Length;i++)values[i]=new(){Type=AtkValueType.Int,Int=args[i]};addon->FireCallback((uint)args.Length,values,true);
    }
    private unsafe bool SelectLoginDataCenter(string world,ref bool sent)
    {
        var map=SellerAddon("TitleDCWorldMap");if(map==null)return false;
        if(sent)return true;
        var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().FirstOrDefault(w=>w.Name.ToString()==world);
        if(row.RowId==0||row.DataCenter.RowId==0)throw new InvalidOperationException("The target data center could not be identified.");
        LoginCallback(map,17,(int)row.DataCenter.RowId);sent=true;return true;
    }
    private unsafe bool SelectVerifiedServiceAccount(int index)
    {
        var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null)return false;
        var text=menu->GetTextNodeById(2);var expected=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Lobby>().GetRowOrDefault(11)?.Text.ToString();
        if(text==null||string.IsNullOrWhiteSpace(expected)||TravelMenuText(text->NodeText.StringPtr)!=expected.Trim())throw new InvalidOperationException("An unrelated login dialog needs attention.");
        var list=menu->PopupMenu.PopupMenu.List;
        if(list==null||index<0||index>=list->ListLength||!list->IsItemInteractionEnabled||list->GetItemDisabledState(index))throw new InvalidOperationException("The verified service account is unavailable in this login.");
        list->DispatchItemEvent(index,AtkEventType.ListItemClick);return true;
    }
    // A ready empty world is a search result, not proof of the wrong account.
    private unsafe bool SelectNextKnownLoginWorld(string targetContent,string targetWorld,DateTimeOffset now)
    {
        if(now<loginWorldNext)return true;
        var lobby=AgentLobby.Instance();var addon=SellerAddon("_CharaSelectWorldServer");
        if(lobby==null||addon==null||lobby->TemporaryLocked||SellerAddon("_CharaSelectListMenu")==null)return true;
        var available=lobby->LobbyData.LobbyUIClient.CurrentDataCenterWorlds;if(available.LongCount is <=0 or >32)return true;
        if(loginWorldPending!=0){if(lobby->WorldId!=loginWorldPending){if(now-loginWorldSentAt>TimeSpan.FromSeconds(15))throw new InvalidOperationException("World list change timed out; no login was requested.");return true;}loginWorldPending=0;loginWorldSample=lobby->WorldId;loginWorldStableAt=now;return true;}
        if(loginWorldSample!=lobby->WorldId){loginWorldSample=lobby->WorldId;loginWorldStableAt=now;return true;}
        if(now-loginWorldStableAt<TimeSpan.FromSeconds(2))return true;
        var preferred=(ushort)0;
        foreach(var c in lobby->LobbyData.LobbyUIClient.CurrentDataCenterCharacters)if(c.ContentId.ToString()==targetContent&&c.DeletedFlag==0)preferred=c.CurrentWorldId;
        var known=DeliveryCharacters().GroupBy(x=>x.Character.World).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Person.Id+"/"+x.Character.AccountId).Distinct().Count());
        var worlds=new List<(ushort Id,int Index,string Name,int Priority)>();
        foreach(var w in available){var name=w.NameString;worlds.Add((w.Id,w.Index,name,w.Id==preferred?1000:name==targetWorld?500:known.GetValueOrDefault(name)*10));}
        loginWorldChecked.Add(lobby->WorldId);
        var next=worlds.Where(w=>!loginWorldChecked.Contains(w.Id)).OrderByDescending(w=>w.Priority).ThenBy(w=>w.Index).FirstOrDefault();
        if(next.Id==0)return false;
        LoginCallback(addon,25,0,next.Index);loginWorldPending=next.Id;loginWorldSentAt=now;loginWorldChecked.Add(next.Id);loginWorldNext=now.AddSeconds(2);
        characterTravelStatus="Checking "+next.Name+" on the selected service account…";return true;
    }
    private void CancelAssignedLogin(string reason)
    {
        if(assignedLogin==null)return;assignedLogin=null;assignedLoginError=reason;characterSwitchTarget=null;characterSwitchUntil=default;
        try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}
        characterTravelStatus=reason;
    }
    private unsafe void UpdateAssignedLogin(DateTimeOffset now)
    {
        if(assignedLogin is not {} run||now<assignedLoginNext)return;assignedLoginNext=now.AddMilliseconds(500);
        try{
            if(config.PairingKey!=run.Pairing||now>=run.Deadline)throw new InvalidOperationException("Assigned login expired or pairing changed.");
            if(Player.IsLoaded){if(Player.ContentId==run.From)return;if(Player.ContentId.ToString()!=run.Target.ContentId)throw new InvalidOperationException("An unexpected character logged in.");assignedLogin=null;characterSwitchUntil=default;characterSwitchTarget=null;return;}
            if(LifestreamBusy())return;
            if(SelectLoginDataCenter(run.World,ref assignedDcSent))return;
            if(SellerAddon("_TitleMenu")!=null&&!assignedDcSent){ClickLoginMenuButton("_TitleMenu",5);return;}
            if(SellerAddon("SelectString")!=null&&!assignedServiceSent){assignedServiceSent=SelectVerifiedServiceAccount(run.ServiceAccount);return;}
            if(SellerAddon("SelectOk")!=null)throw new InvalidOperationException("The game reported a login message. Review it before continuing.");
            var lobby=AgentLobby.Instance();if(lobby==null||lobby->TemporaryLocked||SellerAddon("_CharaSelectListMenu")==null)return;
            if(lobby->ServiceAccountIndex!=run.ServiceAccount)throw new InvalidOperationException("The selected service account differs from this client's reserved account.");
            if(assignedLoginSent)return;
            if(now<loginWorldNext)return;
            if(TryReadReadyLoginList(out var entries)&&entries.FirstOrDefault(e=>!e.Locked&&e.ContentId==run.Target.ContentId&&e.World==run.Target.World) is {} verified){
                if(!Pi.GetIpcSubscriber<string,string,bool>("Lifestream.InitiateLoginFromCharaSelectScreen").InvokeFunc(verified.Name,verified.World))throw new InvalidOperationException("Verified character login was refused.");assignedLoginSent=true;return;
            }
            if(!SelectNextKnownLoginWorld(run.Target.ContentId,run.Target.World,now))throw new InvalidOperationException("No matching character found on the selected account and data center. No alternate account was tried.");
        }catch(Exception e){CancelAssignedLogin(e.Message);}
    }
}

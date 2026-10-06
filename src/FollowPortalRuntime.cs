using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using NativeObject=FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly FollowPortalRelay portalRelay = new();
    private readonly FollowNoticeGate portalNotices = new();
    private unsafe delegate ulong FollowInteractDelegate(TargetSystem* system,NativeObject* obj,bool checkLineOfSight);
    private Hook<FollowInteractDelegate>? followPortalHook;
    private FollowPortalSignal? outgoingPortal,receivedPortal;
    private DateTimeOffset outgoingPortalAt,nextPortalPoll,receivedPortalAt,nextPortalTick;
    private Vector3 portalPreviousPosition;
    private string relayPairingKey="";
    private Task<string>? portalSendTask;
    private Task<(FollowPortalSignal[] Signals,string Status)>? portalReadTask;
    private string portalRelayStatus="Portal relay options are off.",lastLeaderEntity="",lastPortalSignalId="";
    private int relayGeneration,portalReadGeneration;
    private long followArmedAt;
    private bool relayInteracting,portalHookFailed;
    private Task<bool>? followLeaseTask,portalAudienceTask;
    private string followLeaseId="",followLeaseKey="",followLeaseName="",followLeaseIdentity="";
    private uint followLeaseWorld;
    private bool followLeaseDeleting;
    private DateTimeOffset nextFollowLease;
    private void UpdateFollowLease(DateTimeOffset now)
    {
        var loading=Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51];
        var desired=config.EnableFollowThem&&(config.FollowThem.UseSharedPortals||config.FollowThem.UseSharedTeleports)&&followSession.Armed&&(Player.IsLoaded||loading)&&config.PairingKey.Length==64;
        var identity=desired?config.PairingKey+"/"+config.FollowThem.TargetName+"/"+config.FollowThem.HomeWorld+"/"+followArmedAt:"";
        if(followLeaseTask is not null){if(!followLeaseTask.IsCompleted)return;var ok=followLeaseTask.GetAwaiter().GetResult();followLeaseTask=null;if(followLeaseDeleting){followLeaseId="";followLeaseDeleting=false;}else if(!ok)portalRelayStatus="Follow session unavailable — deploy Journal V7.11.76 and check pairing.";}
        if(followLeaseId.Length>0&&identity!=followLeaseIdentity){followLeaseDeleting=true;followLeaseTask=portalRelay.Session(followLeaseKey,followLeaseId,followLeaseName,followLeaseWorld,false);return;}
        if(!desired)return;
        if(followLeaseId.Length==0){followLeaseId=Guid.NewGuid().ToString("N");followLeaseKey=config.PairingKey;followLeaseName=config.FollowThem.TargetName;followLeaseWorld=config.FollowThem.HomeWorld;followLeaseIdentity=identity;nextFollowLease=default;}
        if(now<nextFollowLease)return;
        nextFollowLease=now.AddSeconds(15);followLeaseTask=portalRelay.Session(followLeaseKey,followLeaseId,followLeaseName,followLeaseWorld,true);
    }
    private unsafe ulong ObserveFollowPortalClick(TargetSystem* system,NativeObject* obj,bool checkLineOfSight)
    {
        try
        {
            if(config.EnableFollowThem&&config.FollowThem.SharePortalTransitions&&!relayInteracting&&obj!=null&&Player.IsLoaded)
            {
                outgoingPortal=null;
                var clicked=Objects.FirstOrDefault(o=>o.Address==(nint)obj);
                if(clicked!=null)CaptureTransportSource(clicked);
                var kind=PortalHandler(obj);
                if(transportCapture?.TravelKind!="door"&&clicked?.ObjectKind==ObjectKind.EventObj&&kind is 2 or 20 && Objects.LocalPlayer is {} self&&Vector3.Distance(self.Position,clicked.Position)<=4)
                {
                    transportCapture=null;
                    var map=AgentMap.Instance();var at=DateTimeOffset.UtcNow;var p=clicked.Position;
                    outgoingPortal=new(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,Player.CurrentWorld.RowId,self.GameObjectId.ToString(),Client.TerritoryType,map!=null?map->CurrentMapId:0,clicked.BaseId,kind,p.X,p.Y,p.Z,at.ToUnixTimeMilliseconds(),"",Approach:FollowTravelPosition.From(self.Position),DutyId:(uint)(FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance()==null?0:FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance()->CurrentContentFinderConditionId));
                    outgoingPortalAt=at; portalPreviousPosition=self.Position;
                    portalAudienceTask=config.PairingKey.Length==64?portalRelay.HasFollowers(config.PairingKey,Player.CharacterName,Player.HomeWorld.RowId):Task.FromResult(false);
                    portalRelayStatus="Portal click observed; waiting for a confirmed area transition.";
                }
            }
        }
        catch(Exception e){outgoingPortal=null;errorJournal.Record("portal-observe","Portal click could not be identified",exceptionType:e.GetType().Name);}
        return followPortalHook!.Original(system,obj,checkLineOfSight);
    }
    private static unsafe int PortalHandler(NativeObject* obj)
    {
        if(obj==null)return 0;
        var content=(int)obj->EventId.ContentId;
        if(content is 2 or 20)return content;
        return obj->EventHandler!=null?(int)obj->EventHandler->Info.EventId.ContentId:content;
    }
    private unsafe void UpdateFollowPortalRelay(DateTimeOffset now)
    {
        if(!config.EnableFollowThem&&followPortalHook==null&&portalSendTask==null&&portalReadTask==null&&followLeaseId.Length==0&&followLeaseTask==null)return;
        if(now<nextPortalTick)return;
        nextPortalTick=now.AddMilliseconds(100);
        try
        {
            ObserveTravelStationary(now);
            ObserveTravelMenuDiagnostics(now);
            UpdateFollowDutyLeave(now);
            UpdateFollowLease(now);
            UpdateFollowApproach(now);
            UpdateFollowTravel(now);
            if(relayPairingKey!=config.PairingKey){relayPairingKey=config.PairingKey;relayGeneration++;ResetTravelQueue();receivedPortal=null;outgoingPortal=null;pendingAethernet=null;pendingWard=null;pendingTransport=null;transportCapture=null;worldSource=null;CancelLifestreamTravel();CancelFollowApproach();pendingDutyLeave=null;}
            var sharing=config.EnableFollowThem&&config.FollowThem.SharePortalTransitions;
            if(sharing&&!portalHookFailed&&followPortalHook==null)
            {
                try { followPortalHook=Interop.HookFromAddress<FollowInteractDelegate>(TargetSystem.MemberFunctionPointers.InteractWithObject,ObserveFollowPortalClick);followPortalHook.Enable(); }
                catch(Exception e){portalHookFailed=true;portalRelayStatus="Portal click observer unavailable.";errorJournal.Record("portal-hook",portalRelayStatus,exceptionType:e.GetType().Name);}
            }
            if(followPortalHook!=null){if(sharing&&!followPortalHook.IsEnabled)followPortalHook.Enable();else if(!sharing&&followPortalHook.IsEnabled)followPortalHook.Disable();}
            if(!sharing)outgoingPortal=null;
            if(portalSendTask?.IsCompleted==true)
            {
                portalRelayStatus=portalSendTask.GetAwaiter().GetResult();portalSendTask=null;
                if(portalNotices.Changed(portalRelayStatus))FollowChatNotice("PORTAL — "+portalRelayStatus);
            }
            FlushOutgoingTravel();
            var loading=Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51];
            if(outgoingPortal is {} candidate)
            {
                if(now-outgoingPortalAt>TimeSpan.FromSeconds(120)||Player.IsLoaded&&(Player.CharacterName!=candidate.Name||Player.HomeWorld.RowId!=candidate.HomeWorld))
                    outgoingPortal=null;
                else if(Player.IsLoaded&&!loading&&(Client.TerritoryType!=candidate.Territory||(Objects.LocalPlayer is {} moved && Vector3.Distance(moved.Position,portalPreviousPosition)>12)))
                {
                    outgoingPortal=null;
                    if(config.PairingKey.Length==64&&portalAudienceTask is {} audience)
                        EnqueueOutgoingTravel(config.PairingKey,candidate with {SentAt=now.ToUnixTimeMilliseconds()},audience);
                    else portalRelayStatus="Portal relay needs pairing; transition not queued for replay.";
                }
                else
                {
                    var dialog=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
                    if(dialog!=null&&dialog->IsVisible&&dialog->PromptText!=null)
                    {
                        var prompt=dialog->PromptText->NodeText.ToString();
                        if(FollowPortalPolicy.IsConfirmationSupported(prompt))outgoingPortal=candidate with {Confirmation=prompt};
                    }
                    if(Objects.LocalPlayer is {} current)portalPreviousPosition=current.Position;
                }
            }
            var receiving=config.EnableFollowThem&&(config.FollowThem.UseSharedPortals||config.FollowThem.UseSharedTeleports)&&followSession.Armed;
            if(!receiving){receivedPortal=null;ResetTravelQueue();return;}
            if(portalReadTask?.IsCompleted==true)
            {
                var result=portalReadTask.GetAwaiter().GetResult();portalReadTask=null;
                if(portalReadGeneration==relayGeneration)
                {
                    if(result.Status!="Portal relay ready.")portalRelayStatus=result.Status;
                    if(portalNotices.Changed(result.Status) && result.Status!="Portal relay ready.")
                        FollowChatNotice("ERROR — "+result.Status);
                    foreach(var signal in result.Signals){EnqueueTravel(signal);portalReadCursor=Math.Max(portalReadCursor,signal.Sequence);}
                    if(result.Status!="Portal relay ready.")nextPortalPoll=now.AddSeconds(10);
                }
            }
            UpdateTravelQueue(now,loading);
            if(receivedPortal is {} pending)
            {
                if(now-receivedPortalAt>TimeSpan.FromSeconds(5)||loading||Client.TerritoryType!=pending.Territory){receivedPortal=null;return;}
                var dialog=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
                if(travelStepReady&&pending.Confirmation.Length>0&&dialog!=null&&dialog->IsVisible&&dialog->PromptText!=null&&dialog->PromptText->NodeText.ToString()==pending.Confirmation)
                {
                    receivedPortal=null;
                    dialog->FireCallbackInt(0);
                    portalRelayStatus="Accepted the same portal confirmation as your selected character.";
                    FollowChatNotice("PORTAL — " + portalRelayStatus);
                }
            }
            if(now<nextPortalPoll||portalReadTask!=null||lastLeaderSeen==default&&!config.FollowThem.MeetAtTeleports)return;
            if(config.PairingKey.Length!=64){portalRelayStatus="Pair both Companions with the same Journal key for portal relay.";return;}
            nextPortalPoll=now.AddSeconds(now-lastLeaderSeen>TimeSpan.FromSeconds(30)?3:1);portalReadGeneration=relayGeneration;
            portalReadTask=portalRelay.Read(config.PairingKey,config.FollowThem.TargetName,config.FollowThem.HomeWorld,followLeaseId,portalReadCursor);
        }
        catch(Exception e)
        {
            outgoingPortal=null;receivedPortal=null;nextPortalPoll=now.AddSeconds(10);
            portalRelayStatus="Portal relay paused; no portal action taken.";
            if (portalNotices.Changed(portalRelayStatus)) FollowChatNotice("ERROR — " + portalRelayStatus);
            errorJournal.Record("portal-relay",portalRelayStatus,exceptionType:e.GetType().Name);
        }
    }
    private async Task<string> SendPortalToAudience(string key,FollowPortalSignal signal,Task<bool> audience)
    {
        signal=NormalizeDutyDeparture(signal);
        if(!await audience)return "No active follower; portal transition not sent.";
        if(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-signal.SentAt>10000)return "Portal transition expired; not sent.";
        return await portalRelay.Send(key,signal with {SentAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
    }
    private unsafe void TryUseSharedPortal(FollowPortalSignal signal,DateTimeOffset now)
    {
        if(signal.TravelKind!="portal"){TryUseSharedTravel(signal,now);return;}
        if(!config.FollowThem.UseSharedPortals)return;
        var self=Objects.LocalPlayer;var map=AgentMap.Instance();
        if(self==null||map==null||!FollowPortalPolicy.CanUse(signal,now.ToUnixTimeMilliseconds(),followArmedAt,
            config.FollowThem.TargetName,config.FollowThem.HomeWorld,Player.CurrentWorld.RowId,Client.TerritoryType,map->CurrentMapId,
            lastLeaderEntity,(now-lastLeaderSeen).TotalSeconds,self.Position))return;
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.OccupiedInCutSceneEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.Unconscious])return;
        var dialog=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if(dialog!=null&&dialog->IsVisible)return;
        var position=new Vector3(signal.X,signal.Y,signal.Z);
        var target=Objects.FirstOrDefault(o=>o.ObjectKind==ObjectKind.EventObj&&o.BaseId==signal.BaseId&&o.IsTargetable&&Vector3.Distance(o.Position,position)<.75f);
        if(target==null||PortalHandler((NativeObject*)target.Address)!=signal.HandlerType||!FaceTravelTarget(signal,target,now))return;
        lastPortalSignalId=signal.Id;receivedPortal=signal;receivedPortalAt=now;
        try
        {
            relayInteracting=true;
            TargetSystem.Instance()->InteractWithObject((NativeObject*)target.Address,true);
            portalRelayStatus="Requested the portal shared by "+signal.Name+".";
            FollowChatNotice("PORTAL — " + portalRelayStatus);
        }
        finally {relayInteracting=false;}
    }
}

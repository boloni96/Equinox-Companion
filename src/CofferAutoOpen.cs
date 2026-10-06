using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NativeObject=FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeTreasure=FFXIVClientStructs.FFXIV.Client.Game.Object.Treasure;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly CofferAutoOpenPolicy cofferAutoOpen = new();
    private bool cofferAutoFault;
    private string cofferAutoStatus="Auto-open is off.";
    private void ResetCofferAutoOpen()
    {
        cofferAutoOpen.Clear();cofferAutoFault=false;
        cofferAutoStatus=config.AutoOpenCoffers?"Waiting for a nearby unopened coffer.":"Auto-open is off.";
    }
    private unsafe bool CofferInteractionReady()
    {
        if(!Player.IsLoaded || Objects.LocalPlayer is not {IsTargetable:true} || FollowTransitionBusy() ||
            Conditions[ConditionFlag.InCombat] || Conditions[ConditionFlag.Unconscious] ||
            Conditions[ConditionFlag.TradeOpen] || Conditions[ConditionFlag.Crafting] || Conditions[ConditionFlag.Gathering])return false;
        foreach(var name in new[]{"SelectYesno","SelectString","SelectIconString","Talk"})
        {
            var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
            if(addon!=null && addon->IsVisible)return false;
        }
        return true;
    }
    private unsafe void TryAutoOpenCoffer(DateTimeOffset now)
    {
        if(!config.EnableCofferMarkers || !config.AutoOpenCoffers || cofferAutoFault)return;
        if(!CofferInteractionReady()){cofferAutoStatus="Waiting: combat, loading or another interaction is active.";return;}
        var self=Objects.LocalPlayer!;
        foreach(var point in cofferMemory.Points.OrderBy(p=>Vector3.DistanceSquared(self.Position,p.Position)))
        {
            if(!cofferAutoOpen.CanAttempt(point,now) || !CofferAutoOpenPolicy.Eligible(true,true,true,true,point.Present,point.Opened,Vector3.Distance(self.Position,point.Position)))continue;
            var obj=Objects.FirstOrDefault(o=>o.GameObjectId==point.Id && o.BaseId==point.BaseId);
            if(obj==null || obj.Address==0 || obj.ObjectKind!=ObjectKind.Treasure)continue;
            var native=(NativeTreasure*)obj.Address;
            var opened=(native->Flags & NativeTreasure.TreasureFlags.Opened)!=0;
            if(!CofferAutoOpenPolicy.Eligible(config.AutoOpenCoffers,CofferInteractionReady(),true,obj.IsTargetable,true,opened,Vector3.Distance(self.Position,obj.Position)))continue;
            var system=TargetSystem.Instance();
            if(system==null || !cofferAutoOpen.Reserve(point,now))return;
            try
            {
                system->InteractWithObject((NativeObject*)obj.Address,true);
                cofferAutoStatus="Requested opening a nearby coffer; waiting for the game's confirmation.";
                // Do not mark it green here. Observation must confirm that the chest opened.
            }
            catch(Exception e)
            {
                cofferAutoFault=true;cofferAutoStatus="Auto-open paused after an error. Use Retry or open the chest manually.";
                Chat.PrintError("[Equinox] "+cofferAutoStatus);
                errorJournal.Record("coffer-auto-open",cofferAutoStatus,exceptionType:e.GetType().Name);
            }
            return;
        }
        cofferAutoStatus="Watching within 2.5 yalms. At most two attempts per coffer this visit; blocked coffers can be opened manually.";
    }
    private void DrawCofferAutoOpenSettings()
    {
        MessageToggle("Auto-open nearby treasure coffers",config.AutoOpenCoffers,v=>{config.AutoOpenCoffers=v;ResetCofferAutoOpen();});
        if(!config.AutoOpenCoffers)return;
        ImGui.TextWrapped("Opens ordinary treasure coffers within 2.5 yalms when out of combat and available. Does not move you, click portals or confirm menus. Local to this PC; independent of FollowThem. QuickLoot handles rolls separately. Turning off Treasure Coffer markers also stops auto-open.");
        ImGui.TextWrapped(cofferAutoStatus);
        if(cofferAutoFault && ImGui.Button("Retry coffer auto-open"))ResetCofferAutoOpen();
    }
}

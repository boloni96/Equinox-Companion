using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    [PluginService] internal static IGameGui GardenGui { get; private set; } = null!;
    private string? activeGardenSession;
    private GardenMenu? guidanceBed;
    private string gardenMarkerStatus="";
    private bool gardenMarkerError;
    private (string House,int Batch,int WrongBed,int NextBed)? gardenWrongBed;
    private bool FastGardenSync => plantingWindow?.IsOpen==true || activeGardenSession is not null;
    private static string GardenSessionKey(SharedGardenPlan p)=>$"{p.HouseId}:{p.Batch}:{p.At:O}";
    private void StopGardenSession() { activeGardenSession=null; guidanceBed=null; gardenMarkerStatus="";gardenWrongBed=null; }
    private void RememberGuidanceBed(GardenMenu menu)
    {
        // Only a numbered empty-bed menu establishes identity; selected guide tabs never do.
        guidanceBed=menu.NumberedLocation() is not null?menu:null;
    }
    private unsafe HashSet<string> MarkerNames(AgentHousingPlant* agent,string crop,string soil)
    {
        LoadGardenPictures();
        var names=new HashSet<string>(StringComparer.Ordinal);
        var english=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
        var local=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        // Resolve only items actually offered by the planting agent, never a crop's harvest item.
        for(var i=0;i<Math.Min((int)agent->SelectableItemCount,140);i++)
        {
            var item=agent->SelectableItems[i].ItemCache;if(item==null)continue;
            var id=item->Id;var name=english.GetRowOrDefault(id)?.Name.ToString()??"";
            if(name==soil || gardenTiming.TryGetValue(name,out var t)&&t.Crop==crop)
            { var translated=local.GetRowOrDefault(id)?.Name.ToString();if(!string.IsNullOrEmpty(translated))names.Add(translated); }
        }
        return names;
    }
    private unsafe void DrawPlantingMarkers()
    {
        if(!plantingWindow.IsOpen&&!minimizedLaunchers.Contains("Planting")){StopGardenSession();return;}
        if(!config.SyncEnabled||config.PairingKey.Length!=64||!Player.IsLoaded){StopGardenSession();return;}
        activeGardenSession=null;gardenMarkerError=false;gardenWrongBed=null;
        gardenMarkerStatus="Automatic guide · Open a numbered bed in game. A fresh batch starts at Bed 1.";
        var menu=guidanceBed;var now=DateTimeOffset.UtcNow;
        var current=Volatile.Read(ref capturedContext)?.CandidateAt(now);
        if(menu is null||!menu.Matches(now,current)||menu.NumberedLocation() is not {} location)return;
        var plans=GardenPlanSources();var house=SharedGardenLocation.Match(currentAddress,plans);
        var displayed=plans.FirstOrDefault(p=>p.HouseId==house&&p.Batch==plantingBatch);
        if(displayed is not null&&location.Patch!=(displayed.PhysicalPatch>0?displayed.PhysicalPatch:displayed.Batch))
        { gardenMarkerError=true;gardenMarkerStatus=$"Wrong batch: you opened Batch {location.Patch} in game, but the guide is showing Batch {displayed.Batch}. Open the matching batch in game or switch the guide tab. No items marked.";return; }
        var matches=plans.Where(p=>p.HouseId==house&&(p.PhysicalPatch>0?p.PhysicalPatch:p.Batch)==location.Patch).Take(2).ToArray();
        if(matches.Length!=1){gardenMarkerStatus="Waiting for an unambiguous saved plan for this physical batch.";return;}
        var source=matches[0];
        if(!GardenGuidance.ValidOrder(source)){gardenMarkerStatus="Save a fresh website plan starting at Bed 1 before using markers.";return;}
        activeGardenSession=GardenSessionKey(source);
        var plan=EffectiveGardenPlan(source);
        if(plan.CompletedAt is not null || plan.Beds.Any(b=>b.Crop.Length>0)&&plan.Beds.Where(b=>b.Crop.Length>0).All(b=>b.Status=="confirmed")){StopGardenSession();gardenMarkerStatus="Planting complete · confirmed by game actions.";return;}
        var next=GardenGuidance.Next(plan);
        if(next is null||next.Bed!=location.Bed){if(next is not null)gardenWrongBed=(source.HouseId,source.Batch,location.Bed,next.Bed);gardenMarkerError=next is not null;gardenMarkerStatus=next is null?"Waiting for the remaining planting steps.":$"Wrong bed: you opened Bed {location.Bed}; the plan expects Bed {next.Bed}. No items marked.";return;}
        if(menu.EmptyLocation() is null){gardenMarkerStatus=$"Bed {location.Bed} is occupied. Review its planned/actual crop before replacing anything.";return;}
        var soil=next.ReplantOrder>0&&next.Status!="replant"?next.StarterSoil:next.Soil;
        gardenMarkerStatus=$"Bed {next.Bed}: {next.Crop} · {soil}";
        try
        {
            var agent=AgentHousingPlant.Instance();
            if(agent==null||!agent->IsAgentActive())return;
            var names=MarkerNames(agent,next.Crop,soil);
            if(names.Count==0){gardenMarkerStatus+=" · Required item is not offered by this planting menu.";return;}
            var addon=(AtkUnitBase*)GardenGui.GetAddonByName("ContextIconMenu").Address;
            if(addon==null||!addon->IsVisible||agent->ContextAddonId!=addon->Id){gardenMarkerStatus+=" · Waiting for the planting item selector.";return;}
            var budget=600;var count=MarkGardenNodes(&addon->UldManager,names,ref budget,0);
            gardenMarkerStatus+=count>0?" · Required item outlined in green.":" · Required item not visible in this menu; check inventory/scroll.";
        }
        catch(Exception e){gardenMarkerStatus="Item marker unavailable; follow the bed guide.";errorJournal.Record("garden-marker","Could not read planting item menu",exceptionType:e.GetType().Name);}
    }
    private static unsafe int MarkGardenNodes(AtkUldManager* uld,HashSet<string> names,ref int budget,int depth)
    {
        if(uld==null||uld->NodeList==null||depth>8)return 0;
        var marked=0;
        for(var i=0;i<uld->NodeListCount&&budget-->0;i++)
        {
            var node=uld->NodeList[i];if(node==null||!node->IsVisible())continue;
            var visible=true;var parent=node->ParentNode;
            for(var n=0;parent!=null&&n<30;n++,parent=parent->ParentNode)if(!parent->IsVisible()){visible=false;break;}
            if(!visible)continue;
            if(node->Type==NodeType.Text)
            {
                var text=Dalamud.Game.Text.SeStringHandling.SeString.Parse(((AtkTextNode*)node)->NodeText.AsSpan()).TextValue;
                if(!names.Contains(text))continue;
                Bounds bounds;node->GetBounds(&bounds);
                if(bounds.Width<=0||bounds.Height<=0)continue;
                var offset=ImGui.GetMainViewport().Pos;
                ImGui.GetForegroundDrawList().AddRect(offset+new Vector2(bounds.Pos1.X-3,bounds.Pos1.Y-2),offset+new Vector2(bounds.Pos2.X+3,bounds.Pos2.Y+2),0xff65ee83,3,ImDrawFlags.None,2);
                marked++;
            }
            else if((int)node->Type>=1000)
            {
                var component=node->GetComponent();if(component!=null)marked+=MarkGardenNodes(&component->UldManager,names,ref budget,depth+1);
            }
        }
        return marked;
    }
}

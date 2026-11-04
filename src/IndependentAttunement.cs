using System.Numerics;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe bool IndependentCrystalUnlocked(string name)
    {
        var id=independentBook?.TravelId(name)??0;var state=UIState.Instance();
        return id!=0&&state!=null&&state->IsAetheryteUnlocked(id);
    }
    private Vector3? IndependentCrystalPosition(uint id)
    {
        var live=Objects.FirstOrDefault(o=>o.ObjectKind==Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte&&o.BaseId==id);
        if(live!=null)return live.Position;
        var row=DataManager.GetExcelSheet<Aetheryte>().GetRowOrDefault(id);
        if(!row.HasValue||row.Value.Territory.RowId!=Client.TerritoryType)return null;
        foreach(var location in row.Value.Level)if(location.RowId!=0){var p=location.Value;if(p.Territory.RowId==Client.TerritoryType)return new(p.X,p.Y,p.Z);}
        return null;
    }
    private unsafe bool UpdateIndependentAttunement(JsonElement step,string kind,DateTimeOffset now)
    {
        var name=IndependentStepPolicy.Text(step,kind=="AttuneAethernetShard"?"AethernetShard":"Aetheryte");
        var id=independentBook?.TravelId(name)??0;
        if(id==0){StopIndependentQuest("Attunement crystal is not mapped: "+name,true);return true;}
        if(IndependentCrystalUnlocked(name)){
            if(!HelperInCutscene()&&!QuestConversationVisible())NextIndependentStep(now);
            return true;
        }
        if(independentStepSent){
            questToolsStatus="Attuning to "+name+" · waiting for the game's unlock confirmation";
            if(now-independentActionAt>TimeSpan.FromSeconds(45))StopIndependentQuest("Attunement was not confirmed. Finish the crystal's prompt manually.",true);
            return true;
        }
        if(HelperInCutscene()||QuestConversationVisible()||VisibleFollowAddon("SelectYesno"))return true;
        if(!IndependentQuestTravel(step,IndependentStepPolicy.Number(step,"TerritoryId"),now))return true;
        var self=Objects.LocalPlayer;if(self==null)return true;
        var crystal=Objects.FirstOrDefault(o=>o.ObjectKind==Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Aetheryte&&o.BaseId==id&&o.IsTargetable);
        var position=crystal?.Position??IndependentCrystalPosition(id);
        if(position==null){StopIndependentQuest("No verified position for attunement: "+name,true);return true;}
        if(crystal==null||Vector3.Distance(self.Position,position.Value)>8){
            var offset=self.Position-position.Value;offset.Y=0;
            if(offset.LengthSquared()<.01f){StopIndependentQuest("Attunement crystal is on another floor.",true);return true;}
            var approach=position.Value+Vector3.Normalize(offset)*4;
            if((independentSearch!=null||independentPath!=null)&&Vector3.Distance(independentGoal,position.Value)<8)approach=independentGoal;
            if(!independentCollision.Ground(approach,out var floor)){StopIndependentQuest("No supported approach to "+name,true);return true;}
            IndependentWalk(floor,.75f,now);questToolsStatus="Approaching "+name+" · "+independentWalkStatus;return true;
        }
        var system=TargetSystem.Instance();if(system==null)return true;
        StopIndependentWalk();Targets.Target=crystal;independentActionAt=now;independentStepSent=true;
        helperReplaying=true;try{system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)crystal.Address,false);}finally{helperReplaying=false;}
        questToolsStatus="Attuning to "+name;return true;
    }
}

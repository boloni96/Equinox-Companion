using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string questRewardAttempt="";
    private unsafe bool PrepareQuestReward(AddonJournalResult* addon,uint questId,out string status){
        status="Waiting for quest rewards";
        if(addon==null||!addon->AtkUnitBase.IsReady||!addon->AtkUnitBase.IsVisible||!NativeEventTitle((AtkUnitBase*)addon,questId))return false;
        if(addon->CompleteButton!=null&&addon->CompleteButton->IsEnabled){questRewardAttempt="";return true;}
        var unit=(AtkUnitBase*)addon;
        // JournalResult optional reward slots: item IDs 82..86, icons +6, amounts +11.
        // Layout reference: NightmareXIV/TextAdvance ReaderJournalResult. Validate every field against Item before dispatch.
        if(unit->AtkValues==null||unit->AtkValuesCount<103||unit->AtkValuesCount>1024){status="Quest reward layout not recognized · choose manually";return false;}
        uint Number(int index){var v=unit->AtkValues[index];return ((int)v.Type&15) is 3 or 5?v.UInt:0;}
        var offers=new List<QuestRewardCandidate>();
        for(var i=0;i<5;i++){
            var encoded=Number(82+i);if(encoded==0)continue;
            var id=encoded%1000000;var amount=Number(93+i);var icon=Number(88+i);
            var row=DataManager.GetExcelSheet<Item>().GetRowOrDefault(id);
            if(row is not {} item||amount is 0 or >9999||icon!=item.Icon){status="Quest reward data not verified · choose manually";return false;}
            var facts=QuickLootReadFacts(id,0,false,false,false);
            var canUse=Objects.LocalPlayer is {} self&&item.LevelEquip<=self.Level;
            var upgrade=canUse&&facts.FitsJob==true&&facts.EquippedLevel is {} equipped?Math.Max(0,facts.ItemLevel-equipped):0;
            var value=(ulong)item.PriceLow*amount;
            offers.Add(new(i,id,amount,id==1||icon==26001,facts.UniqueOwned,upgrade,id==1?amount:value,item.Name.ToString()));
        }
        var chosen=QuestRewardPolicy.Choose(offers);
        if(chosen==null){status="No eligible optional reward · check inventory or choose manually";return false;}
        var key=questId+":"+unit->Id+":"+string.Join(",",offers.Select(x=>$"{x.ItemId}/{x.Amount}"));
        if(questRewardAttempt==key){status="Reward selection sent · waiting for Complete to become available";return false;}
        var canvas=addon->AtkComponentJournalCanvas268;
        if(canvas==null){status="Reward controls unavailable · waiting";return false;}
        var evt=FindQuestRewardEvent(&canvas->UldManager,(AtkEventListener*)canvas,(uint)(7+chosen.Index),0);
        if(evt==null){status="Reward click could not be verified · choose manually";return false;}
        var click=*evt;var data=new AtkEventData();questRewardAttempt=key;
        helperReplaying=true;try{click.Listener->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
        var reason=chosen.Gil?"gil":chosen.Upgrade>0?"current-job upgrade":"vendor value";
        status=$"Selected {chosen.Name} × {chosen.Amount} · {reason}";
        PrintEquinox("Quest reward · "+status);
        TraceKeyboundRun(independentQuestId!=0?independentQuestRun:keyboundAccountRun,"quest-reward-selected",status,"quest-rewards",false);
        return false;
    }
    private static unsafe AtkEvent* FindQuestRewardEvent(AtkUldManager* uld,AtkEventListener* canvas,uint parameter,int depth){
        if(uld==null||uld->NodeList==null||uld->NodeListCount>1024||depth>4)return null;
        for(var i=0;i<uld->NodeListCount;i++){
            var node=uld->NodeList[i];if(node==null||!node->IsVisible())continue;
            var evt=node->AtkEventManager.Event;
            for(var n=0;evt!=null&&n<32;n++,evt=evt->NextEvent)
                if(evt->Listener==canvas&&evt->Param==parameter&&evt->State.EventType==AtkEventType.MouseClick)return evt;
            if((int)node->Type>=1000){var component=((AtkComponentNode*)node)->Component;if(component!=null){var found=FindQuestRewardEvent(&component->UldManager,canvas,parameter,depth+1);if(found!=null)return found;}}
        }
        return null;
    }
}

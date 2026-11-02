using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint independentEquipJob;
    private uint[] independentEquipItems=[];
    private unsafe void UpdateIndependentEquipment(DateTimeOffset now)
    {
        var module=RecommendEquipModule.Instance();var manager=InventoryManager.Instance();
        if(module==null||manager==null||PlayerState.Instance()==null)return;
        if(independentEquipPhase==0){independentEquipJob=PlayerState.Instance()->CurrentClassJobId;independentEquipItems=[];module->SetupForClassJob(independentEquipJob);independentEquipPhase=1;independentActionAt=now;return;}
        if(PlayerState.Instance()->CurrentClassJobId!=independentEquipJob)throw new InvalidOperationException("Job changed while equipping recommended gear.");
        if(module->IsUpdating||now-independentActionAt<TimeSpan.FromMilliseconds(500))return;
        if(independentEquipPhase==1){var items=new List<uint>();foreach(var recommendation in module->RecommendedItems){var item=recommendation.Value;if(item!=null&&item->ItemId!=0)items.Add(item->ItemId);}independentEquipItems=items.ToArray();module->EquipRecommendedGear();independentEquipPhase=2;independentActionAt=now;return;}
        var equipped=manager->GetInventoryContainer(InventoryType.EquippedItems);if(equipped==null||!equipped->IsLoaded)return;
        foreach(var item in independentEquipItems.GroupBy(id=>id)){
            var count=0;for(var i=0;i<equipped->Size;i++)if(equipped->Items[i].ItemId==item.Key)count++;
            if(count<item.Count()){if(now-independentActionAt>TimeSpan.FromSeconds(10))StopIndependentQuest("Recommended equipment was requested but not confirmed; check equipment manually.",true);return;}
        }
        NextIndependentStep(now);
    }
}

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe void UpdateIndependentEquipment(DateTimeOffset now)
    {
        var module=RecommendEquipModule.Instance();var manager=InventoryManager.Instance();
        if(module==null||manager==null||PlayerState.Instance()==null)return;
        if(independentEquipPhase==0){module->SetupForClassJob(PlayerState.Instance()->CurrentClassJobId);independentEquipPhase=1;independentActionAt=now;return;}
        if(module->IsUpdating||now-independentActionAt<TimeSpan.FromMilliseconds(500))return;
        if(independentEquipPhase==1){module->EquipRecommendedGear();independentEquipPhase=2;independentActionAt=now;return;}
        var equipped=manager->GetInventoryContainer(InventoryType.EquippedItems);if(equipped==null||!equipped->IsLoaded)return;
        foreach(var recommendation in module->RecommendedItems){
            var item=recommendation.Value;if(item==null||item->ItemId==0)continue;
            var found=false;for(var i=0;i<equipped->Size;i++)if(equipped->Items[i].ItemId==item->ItemId){found=true;break;}
            if(!found){if(now-independentActionAt>TimeSpan.FromSeconds(10))StopIndependentQuest("Recommended equipment was requested but not confirmed; check equipment manually.",true);return;}
        }
        NextIndependentStep(now);
    }
}

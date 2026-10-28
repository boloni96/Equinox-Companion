using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private void DrawStorageChatOption()
    {
        var enabled=config.ShowStorageTransfersInChat;
        if(ImGui.Checkbox("Show transfers in chat",ref enabled)){config.ShowStorageTransfersInChat=enabled;SaveConfiguration();}
        MarketHelp("Report confirmed Organizer Entrust, Withdraw and internal stack merges. Includes item, quantity and retainer or FC chest. Off by default; attempted or skipped transfers are not reported.");
    }
    private string StorageRetainerName(ulong id)=>retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)?.GetValueOrDefault(id)??ReadSellerRetainers().FirstOrDefault(r=>r.Id==id)?.Name??"Retainer "+id;
    private void ReportStorageTransfer(string action,uint item,bool hq,int quantity,string location)
    {
        if(!config.ShowStorageTransfersInChat)return;
        // Logging must never interrupt an already-confirmed inventory operation.
        try{var name=retrievalCatalog?.Names.GetValueOrDefault(item)??item.ToString();PrintEquinox($"{action} {quantity:N0} × {name}{(hq?" (HQ)":"")} · {location}");}catch(Exception e){Log.Warning(e,"Storage chat report failed");}
    }
}

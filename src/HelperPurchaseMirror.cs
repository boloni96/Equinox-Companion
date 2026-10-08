using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly HashSet<string> purchaseMirrorFollowers=new();
    private string purchaseMirrorVendor="";
    private readonly Queue<HelperAction> purchaseQueue=new();
    private bool purchaseWaitingPreviousClose;
    private sealed record MirrorIntent(HelperPurchase Quote,HelperNpc Vendor,int Before,int[] Costs,string[] Sessions,DateTimeOffset Until);
    private MirrorIntent? purchaseMirrorIntent;
    private unsafe string CurrentPurchaseVendorKey()
    {
        var target=Targets.Target;var map=AgentMap.Instance();
        if(!Player.IsLoaded||target==null||map==null||PurchaseShop().Length==0)return "";
        return Player.CurrentWorld.RowId+"/"+Client.TerritoryType+"/"+map->CurrentMapId+"/"+target.GameObjectId;
    }
    private void ClearPurchaseMirroring(){purchaseMirrorFollowers.Clear();purchaseMirrorVendor="";purchaseMirrorIntent=null;}
    private void DisablePurchaseMirror(string session)
    {
        purchaseMirrorFollowers.Remove(session);
        if(purchaseMirrorIntent is {} intent)purchaseMirrorIntent=intent with {Sessions=intent.Sessions.Where(x=>x!=session).ToArray()};
    }
    private unsafe void DrawPurchaseMirrorToggle(HelperFollower follower)
    {
        var key=CurrentPurchaseVendorKey();var enabled=purchaseMirrorFollowers.Contains(follower.Id);
        ImGui.BeginDisabled(key.Length==0||!helperVendorPurchases||!follower.VendorPurchases||!HelperPolicy.Audience(follower,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        if(ImGui.Checkbox("Mirror all my purchases at this vendor",ref enabled)){
            if(enabled){purchaseMirrorVendor=key;purchaseMirrorFollowers.Add(follower.Id);}
            else DisablePurchaseMirror(follower.Id);
        }
        ImGui.EndDisabled();
        if(enabled)ImGui.TextWrapped("Buy normally: new selections are sent after your purchase completes, with the same quantity and cost. Closing this shop turns this off. Turning it off stops new requests; Stop the follower session to cancel queued purchases. Both clients need Companion 0.5.1.104.");
    }
    private unsafe void CapturePurchaseMirror()
    {
        purchaseMirrorIntent=null;
        if(purchaseQuote is not {} quote||purchaseVendor is not {} vendor||purchaseMirrorFollowers.Count==0)return;
        var inventory=InventoryManager.Instance();if(inventory==null)return;
        var sessions=helperFollowers.Where(f=>purchaseMirrorFollowers.Contains(f.Id)&&HelperPolicy.Audience(f,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())).Select(f=>f.Id).ToArray();
        if(sessions.Length>0)purchaseMirrorIntent=new(quote,vendor,inventory->GetInventoryItemCount(quote.ItemId),quote.Costs.Select(c=>inventory->GetInventoryItemCount(c.ItemId)).ToArray(),sessions,DateTimeOffset.UtcNow.AddSeconds(60));
    }
    private unsafe void ObservePurchaseMirroring(DateTimeOffset now)
    {
        if(purchaseMirrorIntent is {} intent){
            var inventory=InventoryManager.Instance();
            if(now>intent.Until||!Player.IsLoaded||intent.Vendor.World!=Player.CurrentWorld.RowId||intent.Vendor.Territory!=Client.TerritoryType)purchaseMirrorIntent=null;
            else if(inventory!=null){
                var prompt=PurchasePrompt();
                if(prompt.Length>0&&PurchaseShop()==intent.Quote.Shop)purchaseMirrorIntent=intent=intent with {Quote=intent.Quote with {Prompt=prompt}};
                if(HelperPurchasePolicy.Confirmed(intent.Quote,intent.Before,inventory->GetInventoryItemCount(intent.Quote.ItemId),intent.Costs,intent.Quote.Costs.Select(c=>inventory->GetInventoryItemCount(c.ItemId)).ToArray())){
                    purchaseMirrorIntent=null;
                    var recipients=intent.Sessions.Where(id=>!purchaseSent.Contains(id)&&helperFollowers.Any(f=>f.Id==id&&HelperPolicy.Audience(f,now.ToUnixTimeMilliseconds()))).ToArray();
                    if(recipients.Length>0){
                        if(helperOutgoing.Count>=32){ClearPurchaseMirroring();helperError="Purchase sharing queue is full; mirroring stopped. This purchase was not sent.";return;}
                        var action=new HelperAction(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,"vendorPurchase",now.ToUnixTimeMilliseconds(),intent.Vendor,Sessions:recipients,Purchase:intent.Quote);
                        helperOutgoing.Enqueue(action);foreach(var id in recipients)purchaseSent.Add(id);
                        RecordFollowTravel("Completed leader purchase mirrored",new {action.Id,purchase=intent.Quote,followers=recipients.Length});
                    }
                }
            }
        }
        // A captured last transaction can settle after closing; only new selections stop here.
        var key=CurrentPurchaseVendorKey();
        if(key.Length==0||key!=purchaseMirrorVendor){purchaseMirrorFollowers.Clear();purchaseMirrorVendor="";}
        foreach(var id in purchaseMirrorFollowers.ToArray())if(!helperFollowers.Any(f=>f.Id==id&&HelperPolicy.Audience(f,now.ToUnixTimeMilliseconds())))DisablePurchaseMirror(id);
    }
}

using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private void DrawMarketRetainerHover(MarketRetainer? retainer,int total,string name)
    {
        ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*30);
        ImGui.TextUnformatted(retainer==null?name:MarketRetainerLabel(retainer));
        if(retainer==null){
            ImGui.TextWrapped(total==0?"No items selling.":$"{total} selling · listings have not been recorded on this computer. Open this retainer's selling list or run Check prices.");
        }else{
            ImGui.TextDisabled($"{retainer.Character} · {retainer.World}");
            var rows=retainer.Stock.Select(x=>(Item:x,Decision:ReviewFor(retainer.Id,x.Slot))).ToArray();
            var updates=rows.Count(x=>x.Decision.NeedsAttention);
            var unknown=rows.Count(x=>x.Decision.Status==SellerStatus.Unknown);
            var skipped=rows.Count(x=>x.Decision.Status==SellerStatus.Skipped);
            var competitive=rows.Count(x=>x.Decision.Status==SellerStatus.Competitive&&!x.Decision.NeedsAttention);
            ImGui.TextWrapped($"{total} selling · {updates} to review · {competitive} competitive · {unknown} uncertain · {skipped} skipped");
            if(total!=rows.Length)ImGui.TextWrapped($"Game shows {total} selling; {rows.Length} listings are recorded. Reopen the selling list to refresh its contents.");
            var unresolved=rows.Where(x=>x.Decision.Status is SellerStatus.Unknown or SellerStatus.Skipped or SellerStatus.Checking||x.Decision.NeedsAttention)
                .OrderBy(x=>x.Decision.Status is SellerStatus.Unknown or SellerStatus.Skipped?0:1)
                .GroupBy(x=>(x.Decision.Status,x.Decision.Reason)).ToArray();
            foreach(var group in unresolved.Take(4)){
                ImGui.Separator();
                var names=string.Join(", ",group.Take(2).Select(x=>x.Item.Name+(x.Item.Hq?" HQ":"")));
                if(group.Count()>2)names+=$" +{group.Count()-2} more";
                ImGui.TextColored(SellerColor(group.First().Decision.VisualStatus),names);
                ImGui.TextWrapped(group.Key.Reason);
            }
            if(unresolved.Length>4)ImGui.TextWrapped($"{unresolved.Length-4} more reasons in Equinox Market → Selling.");
            if(unresolved.Length==0&&rows.Length>0)ImGui.TextWrapped("Recorded comparisons keep these prices under your current rules. This is a snapshot, not a guarantee that nobody has listed a cheaper item since.");
            var lastLive=retainer.Stock.Select(x=>sellerReviews.TryGetValue((retainer.Id,x.Slot),out var review)?review.At:default).DefaultIfEmpty().Max();
            if(lastLive!=default)ImGui.TextWrapped("Last live review: "+DisplayTime.Local(lastLive));
            ImGui.TextWrapped("Stock observed: "+DisplayTime.Local(retainer.At)+(retainer.Paired?" · paired record":" · this computer"));
            ImGui.TextWrapped("Last retainer visit: "+(retainer.LastVisit is {} visit?DisplayTime.Local(visit):"Not recorded"));
            if(MarketVisitDue(retainer))ImGui.TextWrapped("Visit due: a precautionary reminder, not a confirmed expiry.");
            ImGui.TextWrapped("Open Equinox Market → Selling for every item's reason. Check prices refreshes live comparisons without changing prices.");
        }
        ImGui.PopTextWrapPos();ImGui.EndTooltip();
    }
    private void MarketRetainerHover(MarketRetainer retainer)
    {
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))DrawMarketRetainerHover(retainer,retainer.Stock.Length,retainer.Name);
    }
    private unsafe void DrawMarketNativeHover()
    {
        if(!config.Seller.Enabled||!config.Seller.Highlights||!Player.IsLoaded||ImGui.GetIO().WantCaptureMouse)return;
        var stage=AtkStage.Instance();var collision=stage==null?null:stage->AtkCollisionManager;
        if(collision==null)return;
        var addon=SellerAddon("RetainerList");
        if(addon==null||collision->IntersectingAddon!=addon)return;
        var list=addon->GetComponentListById(27);var manager=RetainerManager.Instance();
        if(list==null||list->ItemRendererList==null||manager==null||!manager->IsReady)return;
        var viewport=ImGui.GetMainViewport().Pos;
        // Resolve nodes on this draw only; never retain native pointers across
        // refreshes. Only the hovered row builds its small reason summary.
        for(var i=0;i<Math.Min(10,list->ListLength);i++){
            var renderer=list->ItemRendererList[i].AtkComponentListItemRenderer;
            if(renderer==null||renderer->OwnerNode==null||!renderer->OwnerNode->IsVisible()||renderer->ListItemIndex<0||renderer->ListItemIndex>=Math.Min(10,(int)manager->GetRetainerCount()))continue;
            var text=renderer->GetTextNodeById(11);if(text==null||!text->IsVisible())continue;
            Bounds bounds;text->GetBounds(&bounds);if(bounds.Width<=0||bounds.Height<=0)continue;
            if(!ImGui.IsMouseHoveringRect(viewport+new Vector2(bounds.Pos1.X,bounds.Pos1.Y),viewport+new Vector2(bounds.Pos2.X,bounds.Pos2.Y),false))continue;
            var native=manager->GetRetainerBySortedIndex((uint)renderer->ListItemIndex);if(native==null)return;
            DrawMarketRetainerHover(MarketRetainerById(native->RetainerId),native->MarketItemCount,native->NameString);return;
        }
    }
}

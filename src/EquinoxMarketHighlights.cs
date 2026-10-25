using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record MarketNodeStyle(ByteColor Original,ByteColor Applied,byte[] OriginalText,string AppliedText);
    private readonly Dictionary<(string Addon,nint Address),Dictionary<nint,MarketNodeStyle>> marketHighlightNodes=[];
    private unsafe void RestoreMarketHighlights()=>PaintMarketHighlights(false);
    private unsafe void UpdateMarketHighlights()=>PaintMarketHighlights(config.Seller.Highlights);
    private unsafe void MarketHighlightFinalizing(Dalamud.Game.Addon.Lifecycle.AddonEvent e,Dalamud.Game.Addon.Lifecycle.AddonArgTypes.AddonArgs args)
    {
        // The native allocation is going away. Drop saved addresses without
        // dereferencing them or restoring text into a future recycled addon.
        foreach(var key in marketHighlightNodes.Keys.Where(x=>x.Address==args.Addon.Address).ToArray())marketHighlightNodes.Remove(key);
    }
    private unsafe void PaintMarketHighlights(bool enabled)
    {
        foreach(var name in new[]{"RetainerList","RetainerSellList"}){
            var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
            foreach(var old in marketHighlightNodes.Keys.Where(x=>x.Addon==name&&(addon==null||x.Address!=(nint)addon)).ToArray())marketHighlightNodes.Remove(old);
            if(addon==null||!addon->IsReady)continue;
            var key=(name,(nint)addon);if(!marketHighlightNodes.TryGetValue(key,out var saved))marketHighlightNodes[key]=saved=[];
            var list=addon->GetComponentListById(name=="RetainerList"?27u:11u);
            if(list==null||list->ItemRendererList==null)continue;
            var live=new HashSet<nint>();var seen=new HashSet<nint>();

            var rm=RetainerManager.Instance();var active=rm==null?0:rm->LastSelectedRetainerId;
            var stock=MarketRetainerById(active)?.Stock??[];
            for(var i=0;i<Math.Min(name=="RetainerList"?10:20,list->ListLength);i++){
                var row=list->ItemRendererList[i].AtkComponentListItemRenderer;if(row==null||!seen.Add((nint)row))continue;
                var index=row->ListItemIndex;
                if(name=="RetainerList"){
                    var text=row->GetTextNodeById(11);if(text==null)continue;
                    var native=rm!=null&&index>=0&&index<Math.Min(10,(int)rm->GetRetainerCount())?rm->GetRetainerBySortedIndex((uint)index):null;
                    var r=native==null?null:MarketRetainerById(native->RetainerId);
                    var states=r?.Stock.Select(x=>ReviewFor(r.Id,x.Slot)).ToArray()??[];
                    var needed=states.Count(x=>x.NeedsAttention);
                    var status=AggregateMarketStatus(states.Select(x=>x.VisualStatus).ToArray());
                    var total=native==null?r?.Stock.Length??0:native->MarketItemCount;
                    var replacement=enabled&&addon->IsVisible&&r!=null?$"{needed}/{total}"+(states.Any(x=>x.Status==SellerStatus.Unknown)||r.Stock.Length!=total?" ?":""):null;
                    PaintMarketText(text,replacement,status,saved,live);
                }else{
                    var text=row->GetTextNodeById(3);if(text==null)continue;
                    var slot=ReadMarketDisplaySlot(addon,index);
                    var item=stock.FirstOrDefault(x=>x.Slot==slot);
                    var status=item==null?SellerStatus.Unknown:ReviewFor(active,item.Slot).VisualStatus;
                    // Native item names carry embedded rarity colors. Use the
                    // plain text while highlighted so the status color is visible.
                    var plain=(TravelMenuText(text->NodeText.StringPtr)??"").Trim();
                    var match=item!=null&&plain.TrimEnd((char)Dalamud.Game.Text.SeIconChar.HighQuality).Trim()==item.Name;
                    PaintMarketText(text,enabled&&addon->IsVisible&&match?plain:null,status,saved,live);
                }
            }
            foreach(var gone in saved.Keys.Where(x=>!live.Contains(x)).ToArray())saved.Remove(gone);
            if(saved.Count==0)marketHighlightNodes.Remove(key);
        }
    }
    private static unsafe int ReadMarketDisplaySlot(AtkUnitBase* addon,int index)
    {
        var offset=15+13*index;
        if(addon==null||index is <0 or >=20||addon->AtkValues==null||offset>=addon->AtkValuesCount)return -1;
        var v=addon->AtkValues[offset];
        return v.Type is AtkValueType.Int or AtkValueType.UInt&&v.Int is >=0 and <20?v.Int:-1;
    }
    private static SellerStatus AggregateMarketStatus(SellerStatus[] states)=>states.Length==0?SellerStatus.Unknown:states.Contains(SellerStatus.Update)?SellerStatus.Update:states.Contains(SellerStatus.Checking)?SellerStatus.Checking:states.Contains(SellerStatus.History)?SellerStatus.History:states.All(x=>x==SellerStatus.Competitive)?SellerStatus.Competitive:states.Contains(SellerStatus.Skipped)?SellerStatus.Skipped:SellerStatus.Unknown;
    private static bool SameMarketColor(ByteColor a,ByteColor b)=>a.R==b.R&&a.G==b.G&&a.B==b.B&&a.A==b.A;
    private static unsafe void PaintMarketText(AtkTextNode* text,string? replacement,SellerStatus status,Dictionary<nint,MarketNodeStyle> saved,HashSet<nint> live)
    {
        var address=(nint)text;live.Add(address);
        if(saved.Remove(address,out var prior)){
            if(SameMarketColor(text->TextColor,prior.Applied))text->TextColor=prior.Original;
            if(text->NodeText.ToString()==prior.AppliedText)text->SetText(prior.OriginalText.AsSpan());
        }
        if(replacement==null||text->NodeText.BufUsed is 0 or >4096)return;
        var original=new ReadOnlySpan<byte>(text->NodeText.StringPtr,(int)text->NodeText.BufUsed).ToArray();
        var color=SellerColor(status);var applied=new ByteColor{R=(byte)(255*color.X),G=(byte)(255*color.Y),B=(byte)(255*color.Z),A=text->TextColor.A};
        saved[address]=new(text->TextColor,applied,original,replacement);text->SetText(replacement);text->TextColor=applied;
    }
}

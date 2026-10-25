using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<(string Addon,nint Address),Dictionary<nint,(ByteColor Original,ByteColor Applied)>> marketHighlightNodes=[];
    private unsafe void RestoreMarketHighlights()=>PaintMarketHighlights(false);
    private unsafe void UpdateMarketHighlights()=>PaintMarketHighlights(config.Seller.Highlights);
    private unsafe void PaintMarketHighlights(bool enabled)
    {
        foreach(var name in new[]{"RetainerList","RetainerSellList"}){
            var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
            // An addon may have been destroyed. Never dereference cached pointers.
            foreach(var old in marketHighlightNodes.Keys.Where(x=>x.Addon==name&&(addon==null||x.Address!=(nint)addon)).ToArray())marketHighlightNodes.Remove(old);
            if(addon==null||!addon->IsReady)continue;
            var key=(name,(nint)addon);if(!marketHighlightNodes.TryGetValue(key,out var saved))marketHighlightNodes[key]=saved=[];
            var labels=new Dictionary<string,SellerStatus>(StringComparer.Ordinal);
            if(enabled&&addon->IsVisible&&name=="RetainerList")foreach(var r in marketLedger.Retainers.Where(x=>x.CharacterId==Player.ContentId)){
                var statuses=r.Stock.Select(x=>ReviewFor(r.Id,x.Slot).Status).ToArray();
                labels[r.Name]=AggregateMarketStatus(statuses);
            }
            if(enabled&&addon->IsVisible&&name=="RetainerSellList"){
                var rm=RetainerManager.Instance();var id=rm==null?0:rm->LastSelectedRetainerId;
                var stock=marketLedger.Retainers.FirstOrDefault(x=>x.Id==id)?.Stock??[];
                foreach(var g in stock.GroupBy(x=>x.Name))labels[g.Key]=AggregateMarketStatus(g.Select(x=>ReviewFor(id,x.Slot).Status).ToArray());
            }
            var live=new HashSet<nint>();var visited=new HashSet<nint>();var budget=1024;
            VisitMarketNodes(&addon->UldManager,0,labels,saved,live,visited,ref budget);
            foreach(var gone in saved.Keys.Where(x=>!live.Contains(x)).ToArray())saved.Remove(gone);
            if(saved.Count==0)marketHighlightNodes.Remove(key);
        }
    }
    private static SellerStatus AggregateMarketStatus(SellerStatus[] states)=>states.Length==0?SellerStatus.Unknown:states.Contains(SellerStatus.Checking)?SellerStatus.Checking:states.Contains(SellerStatus.Update)?SellerStatus.Update:states.Contains(SellerStatus.History)?SellerStatus.History:states.All(x=>x==SellerStatus.Competitive)?SellerStatus.Competitive:states.Contains(SellerStatus.Skipped)?SellerStatus.Skipped:SellerStatus.Unknown;
    private static bool SameMarketColor(ByteColor a,ByteColor b)=>a.R==b.R&&a.G==b.G&&a.B==b.B&&a.A==b.A;
    private unsafe void VisitMarketNodes(AtkUldManager* manager,int depth,Dictionary<string,SellerStatus> labels,Dictionary<nint,(ByteColor Original,ByteColor Applied)> saved,HashSet<nint> live,HashSet<nint> visited,ref int budget)
    {
        if(manager==null||manager->NodeList==null||depth>4||manager->NodeListCount>512)return;
        for(var i=0;i<manager->NodeListCount&&budget-->0;i++){
            var node=manager->NodeList[i];if(node==null||!visited.Add((nint)node))continue;
            if(node->Type==NodeType.Text){
                var text=(AtkTextNode*)node;var address=(nint)text;live.Add(address);
                if(saved.Remove(address,out var prior)&&SameMarketColor(text->TextColor,prior.Applied))text->TextColor=prior.Original;
                var label=(TravelMenuText(text->NodeText.StringPtr)??"").Trim().TrimEnd((char)Dalamud.Game.Text.SeIconChar.HighQuality).Trim();
                if(!labels.TryGetValue(label,out var status)||status==SellerStatus.Unknown)continue;
                var color=SellerColor(status);var applied=new ByteColor{R=(byte)(255*color.X),G=(byte)(255*color.Y),B=(byte)(255*color.Z),A=text->TextColor.A};
                saved[address]=(text->TextColor,applied);text->TextColor=applied;
            }else if((int)node->Type>=1000){var component=node->GetComponent();if(component!=null)VisitMarketNodes(&component->UldManager,depth+1,labels,saved,live,visited,ref budget);}
        }
    }
}

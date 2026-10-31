using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly HashSet<uint> keyboundVisitedTabs=[];
    private string keyboundTabFingerprint="";
    private DateTimeOffset keyboundTabStableAt;
    private unsafe List<nint> KeyboundShopTabs(AtkUnitBase* addon)
    {
        var result=new List<nint>();
        if(addon==null||!addon->IsReady)return result;
        void Read(AtkUldManager* uld,int depth){
            if(uld==null||uld->NodeList==null||depth>6)return;
            for(var i=0;i<Math.Min((int)uld->NodeListCount,1024);i++){
                var node=uld->NodeList[i];if(node==null||!node->IsVisible()||(int)node->Type<1000)continue;
                var component=node->GetComponent();if(component==null)continue;
                if(component->GetComponentType()==ComponentType.RadioButton){
                    var radio=(AtkComponentRadioButton*)component;
                    var label=radio->ButtonTextNode==null?"":radio->ButtonTextNode->NodeText.ToString();
                    if(KeyboundBuyingPolicy.IsShopTab(label))result.Add((nint)radio);
                }
                Read(&component->UldManager,depth+1);
            }
        }
        Read(&addon->UldManager,0);
        result=result.Distinct().ToList();
        var labels=result.Select(p=>((AtkComponentRadioButton*)p)->ButtonTextNode->NodeText.ToString()).ToArray();
        if(!KeyboundBuyingPolicy.CompleteShopTabs(labels))throw new InvalidOperationException("Currency Exchange tabs could not be identified; nothing purchased.");
        if(result.Select(p=>((AtkComponentRadioButton*)p)->GroupId).Distinct().Count()!=1)throw new InvalidOperationException("Currency Exchange tab group differs; nothing purchased.");
        return result;
    }
    private unsafe bool KeyboundShopTabReady(string shop,DateTimeOffset now)
    {
        var addon=KeyboundAddon(shop);var tabs=KeyboundShopTabs(addon);
        var selected=tabs.Where(p=>((AtkComponentRadioButton*)p)->IsSelected).ToArray();
        if(selected.Length!=1)return false;
        var node=((AtkComponentRadioButton*)selected[0])->OwnerNode->AtkResNode.NodeId;
        if(!ShopUInt(addon,shop=="ShopExchangeCurrency"?4:3,out var count)||count>122)return false;
        var values=new List<uint>{node,count};
        for(var i=0;i<count;i++){
            if(!ShopUInt(addon,1066+i,out var item)||!ShopUInt(addon,1310+i,out var index))return false;
            values.Add(item);values.Add(index);
        }
        var fingerprint=string.Join(",",values);
        if(fingerprint!=keyboundTabFingerprint){keyboundTabFingerprint=fingerprint;keyboundTabStableAt=now;return false;}
        return now-keyboundTabStableAt>=TimeSpan.FromMilliseconds(700);
    }
    private unsafe bool KeyboundVisibleMissingRow(string shop)
    {
        var addon=KeyboundAddon(shop);if(addon==null||!ShopUInt(addon,shop=="ShopExchangeCurrency"?4:3,out var count)||count>122)return true;
        for(var i=0;i<count;i++)if(ShopUInt(addon,1066+i,out var item)&&keyboundBuyRemaining.Contains(item))return true;
        return false;
    }
    private unsafe void AdvanceKeyboundShopTab(string shop,DateTimeOffset now)
    {
        var tabs=KeyboundShopTabs(KeyboundAddon(shop));
        var selected=tabs.Single(p=>((AtkComponentRadioButton*)p)->IsSelected);
        keyboundVisitedTabs.Add(((AtkComponentRadioButton*)selected)->OwnerNode->AtkResNode.NodeId);
        var next=tabs.FirstOrDefault(p=>((AtkComponentRadioButton*)p)->IsEnabled&&!keyboundVisitedTabs.Contains(((AtkComponentRadioButton*)p)->OwnerNode->AtkResNode.NodeId));
        if(next==0)throw new InvalidOperationException($"{keyboundBuyRemaining.Count} reward(s) were not visible. Set the shop filter to Show All and turn off Only recently added items.");
        var radio=(AtkComponentRadioButton*)next;
        if(!radio->IsEnabled)throw new InvalidOperationException("The next shop tab is disabled.");
        keyboundTabFingerprint="";keyboundTabStableAt=default;
        helperReplaying=true;try{radio->SetActive();}finally{helperReplaying=false;}
        keyboundBuyStatus="Checking "+radio->ButtonTextNode->NodeText.ToString()+"…";
        keyboundBuyNext=now.AddSeconds(1);
    }
}

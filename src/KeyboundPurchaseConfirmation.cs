using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe bool FindKeyboundConfirmation(KeyboundOffer offer,string shop,uint index,out AtkUnitBase* dialog,out AtkComponentButton* button)
    {
        dialog=null;button=null;
        var agent=AgentShop.Instance();
        if(agent==null||!agent->IsAgentActive()||agent->SelectedItemIndex!=index)return false;
        if(shop=="ShopExchangeItem"){
            var d=(AddonShopExchangeItemDialog*)KeyboundAddon("ShopExchangeItemDialog");if(d==null)return false;
            if(agent->DialogAddonId!=d->Id||agent->SelectedItemStackSize!=1)return false;
            dialog=(AtkUnitBase*)d;button=d->ExchangeButton;return button!=null;
        }
        if(shop!="ShopExchangeCurrency")return false;
        // Single-item rewards use an icon + Yes/No prompt without a quantity
        // editor. Inspect the visible controls, never cast a node by list index.
        foreach(var name in new[]{"ShopExchangeCurrencyDialog","SelectYesno"}){
            var candidate=KeyboundAddon(name);if(candidate==null)continue;
            var texts=new List<string>();var yesButtons=new HashSet<nint>();var quantities=new List<int>();
            void Read(AtkUldManager* uld,int depth){
                if(uld==null||uld->NodeList==null||depth>6)return;
                for(var i=0;i<Math.Min((int)uld->NodeListCount,1024);i++){
                    var node=uld->NodeList[i];if(node==null||!node->IsVisible())continue;
                    if(node->Type==NodeType.Text){texts.Add(TravelMenuText(((AtkTextNode*)node)->NodeText.StringPtr)??"");continue;}
                    if((int)node->Type<1000)continue;
                    var component=node->GetComponent();if(component==null)continue;
                    var type=component->GetComponentType();
                    if(type==ComponentType.NumericInput)quantities.Add(((AtkComponentNumericInput*)component)->Value);
                    if(type==ComponentType.Button){
                        var b=(AtkComponentButton*)component;
                        if(b->OwnerNode!=null&&b->ButtonTextNode!=null&&KeyboundBuyingPolicy.MenuName(TravelMenuText(b->ButtonTextNode->NodeText.StringPtr)??"")=="yes")yesButtons.Add((nint)b);
                    }
                    Read(&component->UldManager,depth+1);
                }
            }
            Read(&candidate->UldManager,0);
            var localName=ExchangeItemName(offer.Item);
            if(yesButtons.Count!=1||quantities.Any(q=>q!=1)||!KeyboundBuyingPolicy.ConfirmationText(texts,localName,offer.Cost))continue;
            // The exact single-item prompt plus the still-live quoted row binds
            // this Yes button to our item and MGC price, not a generic dialog.
            dialog=candidate;button=(AtkComponentButton*)yesButtons.Single();return true;
        }
        return false;
    }
    private unsafe bool CanResumeKeyboundPurchase(KeyboundOffer offer,KeyboundPurchaseReceipt receipt,out string shop,out uint index)
    {
        shop="";index=0;
        if(receipt.Character!=Player.ContentId||!KeyboundIsTrader(Targets.Target)||receipt.Confirmed||receipt.Submitted)return false;
        var current=PurchaseShop();if(current.Length==0)return false;
        var matches=0;uint selected=0;
        for(var row=0;row<122;row++)if(KeyboundReadOffer(current,row,out var candidate)?.Item==offer.Item){matches++;selected=candidate;}
        if(matches!=1||!FindKeyboundConfirmation(offer,current,selected,out _,out _))return false;
        if(!KeyboundBuyingPolicy.Resumable(receipt,KeyboundWallet(),ArmoireInventory(offer.Item).Held,true))return false;
        shop=current;index=selected;return true;
    }
}

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed unsafe partial class RetrieverPort
    {
        private ChestMove? crystalPending;
        private ulong crystalRetainer;
        internal bool TransferCrystals(ulong id,ChestMove move)
        {
            if(!RetrieverActive(id)||!InventoryOpen)throw new InvalidOperationException("Selected retainer inventory changed.");
            var command=RetainerCrystalTransferPolicy.Command(move);
            var agent=AgentRetainer.Instance();
            if(agent==null||!agent->IsAgentActive())throw new InvalidOperationException("Retainer inventory is not active.");
            if(crystalPending==null){
                if(SellerAddon("ContextMenu")!=null||SellerAddon("InputNumeric")!=null)throw new InvalidOperationException("Close the existing item menu before transferring crystals.");
                crystalPending=move;crystalRetainer=id;
                // Crystals always open InputNumeric, even when transferring a single unit.
                agent->InventoryContextEvent.HandleCallback((uint)move.Source.Slot,(InventoryType)move.Source.Container,0,command);
                return false;
            }
            if(crystalRetainer!=id||crystalPending!=move)throw new InvalidOperationException("Crystal transfer target or quantities changed.");
            var numeric=SellerAddon("InputNumeric");if(numeric==null)return false;
            if(numeric->AtkValuesCount<=3)throw new InvalidOperationException("Crystal quantity dialog is not ready.");
            if(!RetainerCrystalTransferPolicy.AllowsQuantity(move.Quantity,numeric->AtkValues[3].UInt))throw new InvalidOperationException("Crystal capacity changed; refresh before transferring.");
            var value=new AtkValue{Type=AtkValueType.Int,Int=move.Quantity};numeric->FireCallback(1,&value,true);
            crystalPending=null;crystalRetainer=0;
            return true; // Caller confirms the observed inventory delta before advancing.
        }
        private void CancelCrystalTransfer()
        {
            if(crystalPending!=null&&RetrieverActive(crystalRetainer))CloseSellerAddon("InputNumeric");
            crystalPending=null;crystalRetainer=0;
        }
    }
}

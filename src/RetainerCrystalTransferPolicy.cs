namespace EquinoxCompanion;
internal static class RetainerCrystalTransferPolicy
{
    internal static ulong Command(ChestMove move)
    {
        var source=move.Source;var target=move.Destination;
        var withdraw=source.Container==12001&&target.Container==2001;
        var entrust=source.Container==2001&&target.Container==12001;
        if((!withdraw&&!entrust)||!CrystalStorage.IsItem(source.Item)||source.Hq||target.Hq||source.Item!=target.Item||source.Slot!=(int)source.Item-2||target.Slot!=(int)source.Item-2||move.Quantity<=0||move.Quantity>source.Quantity||move.Quantity>9999-target.Quantity)
            throw new InvalidOperationException("Crystal quantity, identity or dedicated capacity changed; refresh before transferring.");
        return withdraw?0ul:1ul; // AgentRetainer RetrieveFromRetainer / EntrustToRetainer.
    }
    internal static bool AllowsQuantity(int requested,uint maximum)=>requested>0&&requested<=9999&&(uint)requested<=maximum;
}

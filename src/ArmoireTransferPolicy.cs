namespace EquinoxCompanion;

public sealed record ArmoirePiece(int DresserSlot,uint SourceId,uint ItemId,uint CabinetId,int SetColumn,byte Dye1,byte Dye2);
public static class ArmoireTransferPolicy
{
    public static string Blocked(ArmoirePiece piece,bool stored,int held,int freeSlots)
    {
        if(piece.DresserSlot is <0 or >=800||piece.ItemId==0||piece.SourceId==0||piece.SetColumn is <-1 or >10)return "Invalid dresser entry";
        if(stored)return "Already in armoire";
        if(held>0)return "Another copy is in your inventory or armoury";
        if(piece.Dye1!=0||piece.Dye2!=0)return "Dyed item protected";
        if(freeSlots<2)return "Keep two inventory slots free";
        return "";
    }
    public static ushort RestoreMask(int column)=>column is >=0 and <=10?(ushort)(1<<column):throw new ArgumentOutOfRangeException(nameof(column));
    public static bool RestoreConfirmed(bool sourceStillPresent,int held)=>!sourceStillPresent&&held==1;
    public static bool StoreConfirmed(bool stored,int held)=>stored&&held==0;
}

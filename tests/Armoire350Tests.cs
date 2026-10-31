using EquinoxCompanion;
public static class Armoire350Tests
{
    public static void Run()
    {
        void Check(bool pass,string name){if(!pass)throw new Exception(name);Console.WriteLine("PASS armoire: "+name);}
        var piece=new ArmoirePiece(3,100,100,7,-1,0,0);
        string Block(ArmoirePiece p,bool stored=false,int held=0,int free=2)=>ArmoireTransferPolicy.Blocked(p,stored,held,free);
        Check(Block(piece)=="","eligible undyed piece can use staging space");
        Check(Block(piece with {Dye1=5}).Contains("Dyed"),"first dye channel is protected");
        Check(Block(piece with {Dye2=5}).Contains("Dyed"),"second dye channel is protected");
        Check(Block(piece,true).Length>0,"already stored copy is not withdrawn");
        Check(Block(piece,held:1).Length>0,"pre-existing bag or armoury copy cannot be consumed");
        Check(Block(piece,free:1).Length>0,"staging preserves spare space");
        Check(Block(piece with {DresserSlot=800}).Length>0,"invalid slot rejected");
        Check(Block(piece with {SetColumn=11}).Length>0,"invalid outfit column rejected");
        Check(ArmoireTransferPolicy.RestoreMask(0)==1&&ArmoireTransferPolicy.RestoreMask(8)==256&&ArmoireTransferPolicy.RestoreMask(10)==1024,"outfit mask targets exactly one piece across both bytes");
        Check(!ArmoireTransferPolicy.RestoreConfirmed(true,1),"arrival without dresser acknowledgement waits");
        Check(!ArmoireTransferPolicy.RestoreConfirmed(false,0),"dresser acknowledgement without arrival waits");
        Check(!ArmoireTransferPolicy.RestoreConfirmed(false,2),"ambiguous restored copies cannot be stored");
        Check(ArmoireTransferPolicy.RestoreConfirmed(false,1),"single restored copy acknowledged");
        Check(!ArmoireTransferPolicy.StoreConfirmed(true,1)&&!ArmoireTransferPolicy.StoreConfirmed(false,0),"partial storage acknowledgement cannot finish transfer");
        Check(ArmoireTransferPolicy.StoreConfirmed(true,0),"armoire receipt and inventory removal finish transfer");
    }
}

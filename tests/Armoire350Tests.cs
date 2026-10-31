using EquinoxCompanion;
public static class Armoire350Tests
{
    public static void Run()
    {
        void Check(bool pass,string name){if(!pass)throw new Exception(name);Console.WriteLine("PASS armoire: "+name);}
        Check(GearStoragePolicy.Destination(true,false,false,false,true,false)==GearStorageDestination.Armoire,"armoire is first choice for eligible undyed gear");
        Check(GearStoragePolicy.Destination(false,false,false,false,true,false)==GearStorageDestination.Dresser,"ineligible armoire gear routes to dresser");
        Check(GearStoragePolicy.Destination(true,false,false,true,true,false)==GearStorageDestination.Skip,"dyed source is filtered before withdrawal");
        Check(GearStoragePolicy.Destination(true,false,false,true,false,false)==GearStorageDestination.Dresser,"opting into dyed gear never strips it into armoire");
        Check(GearStoragePolicy.Destination(false,false,true,false,true,false)==GearStorageDestination.Skip,"existing dresser copy stays at source");
        Check(GearStoragePolicy.Destination(true,true,false,false,true,false)==GearStorageDestination.Skip,"existing armoire copy stays at source");
        Check(GearStoragePolicy.Destination(true,false,false,false,true,true)==GearStorageDestination.Skip,"saved exclusion blocks all destinations");
        Check(GearStoragePolicy.BatchCapacity(6,2)==4&&GearStoragePolicy.BatchCapacity(2,2)==0&&GearStoragePolicy.BatchCapacity(100,2)==20,"batches obey available staging space and bound work");
        Check(!GearStoragePolicy.SafeCopy(1,false,0,7,true,false,30000),"second source dye channel is checked before withdrawal");
        Check(!GearStoragePolicy.SafeCopy(1,true,0,0,true,false,30000),"equipped copy is not eligible");
        Check(!GearStoragePolicy.SafeCopy(1,false,0,0,true,true,30000),"materia remains protected");
        Check(!GearStoragePolicy.DeliveryConfirmed(false,true)&&!GearStoragePolicy.DeliveryConfirmed(true,false),"gear route requires both source and destination receipts");
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

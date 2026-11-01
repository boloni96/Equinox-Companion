using EquinoxCompanion;
using System.Text.Json;
public static class OrganizerStorage368Tests
{
    public static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS organizer storage: "+name);}
        Check(OrganizerStoragePolicy.Cosmetic(1,1,false),"level-one statless glamour matches");
        Check(!OrganizerStoragePolicy.Cosmetic(1,1,true),"starter gear with attribute bonus excluded");
        Check(!OrganizerStoragePolicy.Cosmetic(1,10,false),"higher item-level gear excluded");
        Check(!OrganizerStoragePolicy.Cosmetic(10,1,false),"higher equip-level gear excluded");
        Check(OrganizerStoragePolicy.ExtendWait(0)&&OrganizerStoragePolicy.ExtendWait(1)&&!OrganizerStoragePolicy.ExtendWait(2),"receipt recovery is bounded");
        var piece=new ArmoirePiece(3,12,12,8,-1,4,7);
        var snapshots=new Dictionary<ulong,OrganizerDresserSnapshot>{[1]=new(DateTimeOffset.UtcNow,[piece],[99]),[2]=new(DateTimeOffset.UtcNow,[],[],false)};
        var restored=JsonSerializer.Deserialize<Dictionary<ulong,OrganizerDresserSnapshot>>(JsonSerializer.Serialize(snapshots))!;
        Check(restored[1].Pieces.Single()==piece,"saved preview preserves slot and both dye channels");
        Check(restored[2].Pieces.Length==0&&!restored[2].CabinetKnown,"character-specific empty and unknown snapshots preserved");
        Check(ArmoireTransferPolicy.Blocked(restored[1].Pieces[0],false,0,4)=="Dyed item protected","cached dyed preview remains protected");
        Check(!ArmoireTransferPolicy.RestoreConfirmed(false,0)&&!ArmoireTransferPolicy.RestoreConfirmed(true,1),"partial receipt never authorizes next step");
        Check(!ArmoireTransferPolicy.StoreConfirmed(false,0)&&!ArmoireTransferPolicy.StoreConfirmed(true,1),"destination and inventory both required");
    }
}

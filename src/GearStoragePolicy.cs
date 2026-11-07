namespace EquinoxCompanion;

public enum GearStorageDestination { Skip, Armoire, Dresser }
public static class GearStoragePolicy
{
    public static GearStorageDestination Destination(bool eligibleForArmoire,bool inArmoire,bool inDresser,bool dyed,bool skipDyed,bool excluded)
    {
        if(excluded||inArmoire||inDresser||dyed&&skipDyed)return GearStorageDestination.Skip;
        // Never strip dye in the armoire. Opting into dyed gear can only use
        // individual dresser storage, which must preserve its dye channels.
        return eligibleForArmoire&&!dyed?GearStorageDestination.Armoire:GearStorageDestination.Dresser;
    }
    public static int BatchCapacity(int free,int reserve)=>Math.Clamp(free-Math.Max(1,reserve),0,20);
    public static bool SafeCopy(int quantity,bool equipped,byte dye1,byte dye2,bool skipDyed,bool materia,int condition)=>
        CopyRejection(quantity,equipped,dye1,dye2,skipDyed,materia,condition).Length==0;
    public static string CopyRejection(int quantity,bool equipped,byte dye1,byte dye2,bool skipDyed,bool materia,int condition)
    {
        if(quantity!=1)return "Expected one gear copy";
        if(equipped)return "Currently equipped";
        if(skipDyed&&(dye1!=0||dye2!=0))return "Dyed gear protected (Skip dyed gear enabled)";
        if(materia)return "Materia attached";
        if(condition<30000)return "Needs repair (condition below 100%)";
        return "";
    }
    public static bool DeliveryConfirmed(bool sourceGone,bool destinationPresent)=>sourceGone&&destinationPresent;
}

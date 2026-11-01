using EquinoxCompanion;
public static class GearSetProtection365Tests
{
    public static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS gear-set protection: "+name);}
        var sets=GearSetProtectionPolicy.Build([
            new("Paladin",true,[100u,1000200u,0u]),
            new("Crafter",true,[300u,100u]),
            new("Deleted",false,[400u])]);
        Check(sets[100].Contains("Paladin")&&sets[100].Contains("Crafter"),"shared gear lists every saved set");
        Check(sets.ContainsKey(200)&&!sets.ContainsKey(0)&&!sets.ContainsKey(400),"HQ normalized and empty/deleted sets excluded");
        Check(GearSetProtectionPolicy.Protected(true,true,1000300,sets),"inactive set equipment remains protected");
        Check(!GearSetProtectionPolicy.Protected(true,true,999,sets),"unrelated gear remains selectable");
        Check(!GearSetProtectionPolicy.Protected(false,false,100,sets),"disabled option releases protection");
        Check(GearSetProtectionPolicy.Protected(true,false,999,sets),"unavailable gear-set data blocks transfers");
        Check(!GearSetProtectionPolicy.Protected(true,false,0,sets),"empty slots remain available");
        var selected=new HashSet<uint>{100,999};
        selected.RemoveWhere(id=>GearSetProtectionPolicy.Protected(true,true,id,sets));
        Check(selected.SetEquals([999u]),"enabling protection removes prior protected selections");
        var updated=GearSetProtectionPolicy.Build([new("New set",true,[999u])]);
        Check(GearSetProtectionPolicy.Protected(true,true,999,updated),"newly saved set blocks previously allowed transfer");
    }
}

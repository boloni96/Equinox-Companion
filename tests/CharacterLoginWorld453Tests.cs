using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class CharacterLoginWorld453Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string home,string current,uint hd,uint cd,string expected){
            if(CharacterLoginWorldPolicy.Resolve(home,current,hd,cd)!=expected)throw new Exception($"Incorrect login list: {home} visiting {current}");
        }
        Check("Golem","Rafflesia",11,11,"Golem");
        Check("Raiden","Phoenix",7,7,"Raiden");
        Check("Rafflesia","Sophia",11,9,"Sophia");
        Check("Raiden","Raiden",7,7,"Raiden");
        Check("Golem","Unknown",11,0,"Golem");
        Check("Golem","",11,0,"Golem");
        Console.WriteLine("Same-DC World Visit and cross-DC character selection checks passed.");
    }
}

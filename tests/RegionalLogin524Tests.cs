using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class RegionalLogin524Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string home,string current,uint hd,uint cd,uint hr,uint cr,string expected,uint listDc)
        {
            if(CharacterLoginWorldPolicy.Resolve(home,current,hd,cd,hr,cr)!=expected||CharacterLoginWorldPolicy.ListDc(hd,cd,hr,cr)!=listDc)
                throw new Exception($"Wrong regional login list for {home} visiting {current}");
        }
        Check("Rafflesia","Coeurl",11,8,2,2,"Rafflesia",11);
        Check("Rafflesia","Famfrit",11,5,2,2,"Rafflesia",11);
        Check("Rafflesia","Gilgamesh",11,4,2,2,"Rafflesia",11);
        Check("Raiden","Cerberus",7,6,3,3,"Raiden",7);
        Check("Rafflesia","Sophia",11,9,2,4,"Sophia",9);
        Check("Raiden","Sophia",7,9,3,4,"Sophia",9);
        Check("Golem","Rafflesia",11,11,2,2,"Golem",11);
        Check("Rafflesia","Sophia",11,9,0,0,"Sophia",9);
        Check("Rafflesia","",11,0,2,0,"Rafflesia",11);
        Console.WriteLine("524 regional character-list selection and login-gate regressions passed.");
    }
}

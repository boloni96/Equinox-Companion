using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WorldVisit530Tests
{
    [ModuleInitializer] internal static void Run(){
        if(WorldVisitSelectionPolicy.Callback(["Ravana","Bismarck","Zurvan","Sophia","Sephirot"],"Sophia")!=5)throw new Exception("Materia five-world lists must remain supported");
        string[] materia=["Bismarck","Ravana","Sephirot","Sophia","Zurvan"];
        string[] japan=["Aegis","Atomos","Carbuncle","Garuda","Gungnir","Kujata","Tonberry","Typhon"];
        string[] europe=["Alpha","Lich","Odin","Phoenix","Raiden","Shiva","Twintania","Zodiark"];
        void CheckTable(string[] centers,string dc,string[] worlds,string target,int expected){
            string[] table=[..centers,"Rafflesia","Sophia",..worlds];
            if(WorldVisitSelectionPolicy.TableCallback(table,[..centers],dc,[..worlds],target)!=expected)
                throw new Exception("World Visit table selection: "+target);
            table[^1]="";
            if(WorldVisitSelectionPolicy.TableCallback(table,[..centers],dc,[..worlds],target)!=-1)
                throw new Exception("Incomplete destination table accepted");
        }
        CheckTable(["Materia"],"Materia",materia,"Sephirot",4);
        CheckTable(["Elemental","Gaia","Mana","Meteor"],"Elemental",japan,"Tonberry",8);
        CheckTable(["Chaos","Light"],"Light",europe,"Raiden",6);
        // Visible list from report 20261110-214244. WorldTranslate's old offset
        // read three extra labels and incorrectly issued [0,8] (Leviathan).
        string[] primal=["Behemoth","Excalibur","Exodus","Famfrit","Hyperion","Lamia","Leviathan","Ultros"];
        if(WorldVisitSelectionPolicy.Callback(primal,"Famfrit")!=5)throw new Exception("Famfrit visible row 3 requires callback 5, not Leviathan callback 8");
        if(WorldVisitSelectionPolicy.Callback(primal,"Leviathan")!=8)throw new Exception("Leviathan remains at visible row 6");
        if(WorldVisitSelectionPolicy.Callback(primal,"Ultros")!=9)throw new Exception("Last world must not be truncated by header labels");
        if(WorldVisitSelectionPolicy.Callback(primal,"Rafflesia")!=-1)throw new Exception("Home-world metadata must not become a destination in Primal's list");
    }
}

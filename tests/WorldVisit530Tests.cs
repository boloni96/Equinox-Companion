using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WorldVisit530Tests
{
    [ModuleInitializer] internal static void Run(){
        if(WorldVisitSelectionPolicy.Callback(["Ravana","Bismarck","Zurvan","Sophia","Sephirot"],"Sophia")!=5)throw new Exception("Materia five-world lists must remain supported");
        // Visible list from report 20261110-214244. WorldTranslate's old offset
        // read three extra labels and incorrectly issued [0,8] (Leviathan).
        string[] primal=["Behemoth","Excalibur","Exodus","Famfrit","Hyperion","Lamia","Leviathan","Ultros"];
        if(WorldVisitSelectionPolicy.Callback(primal,"Famfrit")!=5)throw new Exception("Famfrit visible row 3 requires callback 5, not Leviathan callback 8");
        if(WorldVisitSelectionPolicy.Callback(primal,"Leviathan")!=8)throw new Exception("Leviathan remains at visible row 6");
        if(WorldVisitSelectionPolicy.Callback(primal,"Ultros")!=9)throw new Exception("Last world must not be truncated by header labels");
        if(WorldVisitSelectionPolicy.Callback(primal,"Rafflesia")!=-1)throw new Exception("Home-world metadata must not become a destination in Primal's list");
    }
}

using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class WorldVisit529Tests
{
    [ModuleInitializer] internal static void Run(){
        string[] dynamis=["Cuchulainn","Golem","Halicarnassus","Kraken","Maduin","Marilith","Rafflesia","Seraph"];
        // The manual Rafflesia selection in the supplied report emitted [0,8].
        if(WorldVisitSelectionPolicy.Callback(dynamis,"Rafflesia")!=8)throw new Exception("World callback must match the observed native Rafflesia selection");
        if(WorldVisitSelectionPolicy.Callback(dynamis,"Coeurl")!=-1)throw new Exception("A stale data-center list must not select another world");
        dynamis[0]="Rafflesia";
        if(WorldVisitSelectionPolicy.Callback(dynamis,"Rafflesia")!=-1)throw new Exception("Duplicate world labels must fail closed");
        dynamis[0]="";
        if(WorldVisitSelectionPolicy.Callback(dynamis,"Rafflesia")!=8)throw new Exception("Empty slots must not shift world callback indices");
    }
}

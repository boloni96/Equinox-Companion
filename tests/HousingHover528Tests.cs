using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class HousingHover528Tests
{
    [ModuleInitializer] internal static void Run(){
        if(!NativeHousingPolicy.WardChoice("Go to specified ward.","Go to specified ward. (Review Tabs)"))throw new Exception("Ferry ward option annotation must not block ward travel");
        foreach(var wrong in new[]{"Travel to ward with private estate.","Travel to ward with apartment.","Leave residential district.","Nothing."})
            if(NativeHousingPolicy.WardChoice("Go to specified ward.",wrong))throw new Exception("Ward request selected a different ferry destination");
        if(!NativeHousingPolicy.WardChoice("Choisir un secteur.","Choisir un secteur."))throw new Exception("Localized exact ward labels must remain supported");
    }
}

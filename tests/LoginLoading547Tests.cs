using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class LoginLoading547Tests
{
    [ModuleInitializer] internal static void Run(){
        // After choosing Materia/service account, the native lobby briefly exposes zero.
        foreach(var state in new[]{(0u,0u,false,true),(86u,9u,true,true),(86u,9u,false,false),(86u,0u,false,true)})
            if(CharacterLoginWorldPolicy.DisplayedListReady(state.Item1,state.Item2,state.Item3,state.Item4))throw new Exception("Unready lobby must wait before world/character selection");
        if(!CharacterLoginWorldPolicy.DisplayedListReady(86,9,false,true))throw new Exception("Ready Sephirot list must allow searching for requested Sophia character");
        if(CharacterLoginWorldPolicy.Resolve("Sophia","Sophia",9,9,4,4)!="Sophia")throw new Exception("Loading another list must not change requested world");
        if(CharacterLoginWorldPolicy.Resolve("Rafflesia","Coeurl",11,8,2,2)!="Rafflesia")throw new Exception("Regional visit must use home list");
        if(CharacterLoginWorldPolicy.Resolve("Rafflesia","Sophia",11,9,2,4)!="Sophia")throw new Exception("Cross-region visit must retain visiting list");
    }
}

using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class TeleportNameTests
{
    [ModuleInitializer]internal static void Run()
    {
        TeleportName[] places=[new(62,"The Gold Saucer","The Gold Saucer","The Gold Saucer","The Gold Saucer",100),new(1,"Alpha Camp","Northern Map","Alpha Camp","Northern Map",200),new(2,"Beta Camp","Northern Map","Beta Camp","Northern Map",300),new(3,"Camp","Southern Map","Camp","Southern Map",400),new(4,"Étoile","Carte","Star Point","English Zone",500)];
        foreach(var q in new[]{"saucer","golden saucer","golden","sau","*sau*","  *GOLDEN SAUCER*  ","gold saucer"})
            if(TeleportNameSearch.Matches(places,q).Single().Id!=62)throw new Exception("Saucer partial/alias failed: "+q);
        if(TeleportNameSearch.Matches(places,"northern").Length!=2)throw new Exception("Map names must expose ambiguity, not pick the first aetheryte.");
        if(TeleportNameSearch.Matches(places,"camp").Single().Id!=3)throw new Exception("Exact destination must outrank substring matches.");
        if(TeleportNameSearch.Matches(places,"etoile").Single().Id!=4||TeleportNameSearch.Matches(places,"star").Single().Id!=4)throw new Exception("Localized and English names must match.");
        if(TeleportNameSearch.Matches(places,"#2").Single().Id!=2||TeleportNameSearch.Matches(places,"#999").Length!=0)throw new Exception("Explicit selection must stay within available destinations.");
        foreach(var q in new[]{"","***","???","missing"})if(TeleportNameSearch.Matches(places,q).Length!=0)throw new Exception("Empty/unmatched queries must not teleport.");
        if(TeleportNameSearch.Matches(places.Concat(places),"northern").Length!=2)throw new Exception("Duplicate rows must not create duplicate choices.");
        Console.WriteLine("PASS439 partial teleport names, maps, ambiguity, aliases, languages and explicit selection");
    }
}

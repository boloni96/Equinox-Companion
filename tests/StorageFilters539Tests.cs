using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class StorageFilters539Tests
{
    [ModuleInitializer] internal static void Run(){
        var categories=new HashSet<uint>{45,46};
        if(!InventoryBrowseSelection.Matches("Iron Ore",45,"ore",categories,false)||InventoryBrowseSelection.Matches("Iron Ore",50,"ore",categories,false)||InventoryBrowseSelection.Matches("Iron Ore",45,"wood",categories,false))throw new Exception("Storage category and search filters must intersect");
        if(InventoryBrowseSelection.Matches("Iron Ore",45,"",new HashSet<uint>(),false)||!InventoryBrowseSelection.Matches("Iron Ore",45,"",new HashSet<uint>(),true))throw new Exception("Cleared categories differ from reset filters");
        TeleportName[] places=[new(2,"Gridania","","","",0),new(8,"Limsa Lominsa","","","",0),new(9,"Ul'dah","","","",0),new(100,"Unrelated","","","",0)];
        foreach(var (alias,id) in new (string,uint)[]{("u",9),("ul",9),("g",2),("gr",2),("gri",2),("l",8),("li",8)}){
            var found=TeleportNameSearch.Matches(places," "+alias.ToUpperInvariant()+" ");
            if(found.Length!=1||found[0].Id!=id)throw new Exception("City alias must select only its main aetheryte");
            if(TeleportNameSearch.Matches(places.Where(p=>p.Id!=id),alias).Length!=0)throw new Exception("Locked city alias must not choose an unrelated teleport");
        }
    }
}

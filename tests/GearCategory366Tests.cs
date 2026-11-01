using EquinoxCompanion;
public static class GearCategory366Tests
{
    public static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS gear categories: "+name);}
        Check(GearDresserSelection.Matches(3200,3,123,3200,3,1000123),"dresser row matches exact armoury copy with HQ normalized");
        Check(!GearDresserSelection.Matches(3200,3,123,0,3,123),"same item in another container is not selected");
        Check(!GearDresserSelection.Matches(3200,3,123,3200,4,123),"another slot is not selected");
        Check(!GearDresserSelection.Matches(3200,3,123,3200,3,124),"changed item is not selected");
        Check(GearCategorySelection.Matches(35,true,new HashSet<uint>{112}),"outfit body piece matches Outfits");
        Check(!GearCategorySelection.Matches(35,false,new HashSet<uint>{112}),"ordinary body gear excluded by Outfits");
        Check(GearCategorySelection.Matches(35,true,new HashSet<uint>{35}),"outfit remains selectable by equipment slot");
        Check(GearCategorySelection.Matches(36,false,new HashSet<uint>{112,36}),"mixed filters form a union");
        Check(GearCategorySelection.Matches(35,true,new HashSet<uint>()),"empty storage filter allows all");
        Check(!GearCategorySelection.Matches(35,true,new HashSet<uint>(),false),"inventory clear all selects no categories");
    }
}

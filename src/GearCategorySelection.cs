namespace EquinoxCompanion;
internal static class GearCategorySelection
{
    // Organizer classification is separate from the game's physical equipment slot.
    internal static uint Category(uint original,uint equipLevel,bool wearable)=>wearable&&equipLevel==1?112u:original;
    internal static bool Matches(uint category,bool outfitPiece,IReadOnlySet<uint> selected,bool emptyMeansAll=true)=>
        selected.Count==0?emptyMeansAll:selected.Contains(category)||(outfitPiece&&selected.Contains(112));
}

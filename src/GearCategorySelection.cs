namespace EquinoxCompanion;
internal static class GearCategorySelection
{
    internal static bool Matches(uint category,bool outfitPiece,IReadOnlySet<uint> selected,bool emptyMeansAll=true)=>
        selected.Count==0?emptyMeansAll:selected.Contains(category)||(outfitPiece&&selected.Contains(112));
}

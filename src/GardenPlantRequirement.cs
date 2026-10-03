namespace EquinoxCompanion;

// One source for the current step's supplies and the temporary starter's next action.
public static class GardenPlantRequirement
{
    public static bool IsReplant(SharedGardenBed bed) => bed.ReplantOrder > 0 && bed.Status == "replant";
    public static bool SuppressTending(SharedGardenBed bed) => bed.ReplantOrder>0 && bed.Status is "starter" or "replant";
    public static bool RemoveStarter(SharedGardenBed bed) => IsReplant(bed) && bed.ActualCrop != "Empty";
    public static int Step(SharedGardenBed bed) => bed.ReplantOrder > 0 && bed.Status is "replant" or "confirmed" ? bed.ReplantOrder : bed.Order;
    public static string Soil(SharedGardenBed bed) => bed.ReplantOrder > 0 && bed.Status is not ("replant" or "confirmed") ? bed.StarterSoil : bed.Soil;
    public static string? ActionIcon(SharedGardenBed bed) => IsReplant(bed) ? RemoveStarter(bed) ? "remove-starter" : "replant" : null;
    public static bool? Required(uint itemId, IReadOnlyDictionary<uint,bool> items) => itemId != 0 && items.TryGetValue(itemId,out var correct) ? correct : null;
    public static bool PickerEntryMatches(int entries,int offered,int listLength,int index,int rendererIndex,uint cacheId,uint inventoryId,uint expectedIcon,uint displayedIcon) => entries>0&&entries<=140&&entries==offered&&entries==listLength&&index>=0&&index<entries&&rendererIndex==index&&cacheId!=0&&cacheId==inventoryId&&expectedIcon!=0&&expectedIcon==displayedIcon;
    // An icon is not an item identity. Only fall back when all offered items with this
    // artwork have the same meaning; shared seed icons must never produce false green.
    public static bool? IconMatch(uint iconId, IEnumerable<(uint Icon,bool Correct)> items)
    {
        var matches=items.Where(x=>iconId!=0&&x.Icon==iconId).Select(x=>x.Correct).Distinct().Take(2).ToArray();
        return matches.Length==1?matches[0]:null;
    }
}

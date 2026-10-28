namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<uint,bool> sellerOutfitQuality=[];
    private bool IsSellerLevelOneOutfit(uint id)
    {
        if(sellerOutfitQuality.TryGetValue(id,out var cached))return cached;
        var item=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id);
        if(item is not {} row)return false;
        if(row.EquipSlotCategory.RowId==0)return sellerOutfitQuality[id]=false;
        var slot=row.EquipSlotCategory.Value;
        return sellerOutfitQuality[id]=SellerPricing.IsLevelOneOutfit(row.LevelEquip,row.LevelItem.RowId,row.ClassJobCategory.RowId,row.EquipSlotCategory.RowId>0,slot.MainHand!=0||slot.OffHand!=0);
    }
}

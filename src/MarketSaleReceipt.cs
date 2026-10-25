using System.Text.RegularExpressions;
namespace EquinoxCompanion;

public sealed record MarketSaleReceipt(int ItemSlot,int GilSlot)
{
    public static MarketSaleReceipt? FromTemplate(uint id,string text)
    {
        if(id is not (745 or 748) || !text.Contains("has sold for",StringComparison.Ordinal) || !text.Contains("after fees",StringComparison.Ordinal))return null;
        var items=Regex.Matches(text,@"(?:ennoun\(Item,[^,]+,|sheet\(Item,)lnum(\d+)(?:[,\)])").Select(x=>int.Parse(x.Groups[1].Value)-1).Distinct().ToArray();
        var marker=text.LastIndexOf(" gil",StringComparison.Ordinal);if(marker<0)return null;
        var amounts=Regex.Matches(text[..marker],@"(?:num|digit|comma)\(lnum(\d+)\)").Select(x=>int.Parse(x.Groups[1].Value)-1).ToArray();
        if(items.Length!=1||amounts.Length==0||items[0] is <0 or >15||amounts[^1] is <0 or >15||amounts[^1]==items[0])return null;
        return new(items[0],amounts[^1]);
    }
    public (uint Item,bool Hq,long Net)? Read(int?[] values)
    {
        if(ItemSlot>=values.Length||GilSlot>=values.Length||values[ItemSlot] is not >0||values[GilSlot] is not >0)return null;
        var item=(uint)values[ItemSlot]!.Value;return(item%1_000_000,item>=1_000_000,values[GilSlot]!.Value);
    }
}

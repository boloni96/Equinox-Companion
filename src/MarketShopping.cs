using System.Globalization;
using System.Text.RegularExpressions;
namespace EquinoxCompanion;

public sealed record MarketShoppingRequest(string Name, int Quantity);
internal sealed record MarketImport(MarketShoppingRequest[] Items, string[] Errors);
internal sealed record MarketWorldPlan(uint WorldId, string World, string Region, long Available, long BuyQuantity, long Cost, bool Complete, MarketListing[] Purchases)
{
    internal MarketListing[] UnitPricePurchases { get; init; } = [];
    internal bool HasUnitPriceAlternative => !Purchases.SequenceEqual(UnitPricePurchases);
}
internal static class MarketShopping
{
    internal const int MaxItems = 100, MaxQuantity = 1_000_000;
    private static readonly Regex Line = new(@"^\s*([0-9]+(?:,[0-9]{3})*)\s*[x×]\s*(\S.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    // Updating a shortage twice must not double the purchase quantity.
    // Keep unrelated shopping entries; the latest shortage replaces a matching quantity.
    internal static MarketShoppingRequest[] WithRequirements(IEnumerable<MarketShoppingRequest> existing,IEnumerable<MarketShoppingRequest> requirements)
    {
        var result=existing.ToList();
        foreach(var item in requirements){
            if(string.IsNullOrWhiteSpace(item.Name)||item.Quantity is <1 or >MaxQuantity)throw new ArgumentException("Invalid missing-material quantity.");
            var index=result.FindIndex(e=>e.Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase));
            if(index<0)result.Add(item);else result[index]=item;
        }
        if(result.Count>MaxItems)throw new ArgumentException($"Where Is Cheap supports {MaxItems} items. Clear some shopping entries first.");
        return result.ToArray();
    }
    internal static MarketImport Parse(string text)
    {
        if(text.Length > 20000) return new([], ["The list is too long (maximum 20,000 characters)."]);
        var items = new List<MarketShoppingRequest>(); var errors = new List<string>(); var lineNumber = 0;
        foreach(var line in text.Replace("\r", "").Split('\n'))
        {
            lineNumber++; if(string.IsNullOrWhiteSpace(line)) continue;
            var match = Line.Match(line);
            if(!match.Success || !int.TryParse(match.Groups[1].Value.Replace(",", ""), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity is < 1 or > MaxQuantity || match.Groups[2].Value.Length > 200)
            { errors.Add($"Line {lineNumber}: use a positive quantity and item name, like 3x Ashen Alumen."); continue; }
            var name = match.Groups[2].Value.Trim();
            var at = items.FindIndex(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if(at >= 0)
            {
                if((long)items[at].Quantity + quantity > MaxQuantity) errors.Add($"Line {lineNumber}: combined quantity exceeds {MaxQuantity:N0}.");
                else items[at] = items[at] with { Quantity = items[at].Quantity + quantity };
            }
            else items.Add(new(name, quantity));
        }
        if(items.Count > MaxItems) errors.Add($"Import up to {MaxItems} different items at once.");
        if(items.Count == 0 && errors.Count == 0) errors.Add("Paste the remaining-ingredients list first.");
        return new(items.ToArray(), errors.ToArray());
    }
    internal static MarketWorldPlan[] Plans(IEnumerable<MarketListing> listings, int needed)
    {
        if(needed < 1) return [];
        var plans = new List<MarketWorldPlan>();
        foreach(var group in listings.Where(x => x.Quantity > 0 && x.Price > 0).GroupBy(x => x.WorldId))
        {
            var rows = group.OrderBy(x => x.Price).ThenBy(x => x.Total).ToArray();
            long available = 0, quantity = 0, cost = 0; var chosen = new List<MarketListing>();
            foreach(var row in rows)
            {
                available += row.Quantity;
                if(quantity >= needed) continue;
                if(cost > long.MaxValue - row.Total) continue;
                chosen.Add(row); quantity += row.Quantity; cost += row.Total;
            }
            var unitPricePurchases = chosen.ToArray();
            // Avoid a cheap-unit oversized stack when one sufficient stack costs less.
            var single = rows.Where(x => x.Quantity >= needed).OrderBy(x => x.Total).FirstOrDefault();
            if(single is not null && (quantity < needed || single.Total < cost))
            { chosen = [single]; quantity = single.Quantity; cost = single.Total; }
            plans.Add(new(group.Key, rows[0].World, rows[0].Region, available, quantity, cost, quantity >= needed, chosen.ToArray()) { UnitPricePurchases = unitPricePurchases });
        }
        return plans.OrderByDescending(x => x.Complete).ThenBy(x => x.Cost).ThenBy(x => x.World, StringComparer.Ordinal).ToArray();
    }
}

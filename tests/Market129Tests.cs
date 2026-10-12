using EquinoxCompanion;
using System.Text.Json;
internal static class Market129Tests
{
    internal static void Run()
    {
        void Check(bool ok, string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS 129 " + name); }
        var imported = MarketShopping.Parse("3x Ashen Alumen\r\n180x Megalania Skin");
        Check(imported.Errors.Length == 0 && imported.Items.SequenceEqual(new MarketShoppingRequest[] { new("Ashen Alumen",3), new("Megalania Skin",180) }), "Artisan example preserves names and quantities");
        var mixed = MarketShopping.Parse(" 1,000 × Ashen Alumen \n\n2X ashen alumen\n3x Megalania Skin");
        Check(mixed.Items.Length == 2 && mixed.Items[0].Quantity == 1002 && mixed.Errors.Length == 0, "duplicate names and copy formatting supported");
        foreach(var invalid in new[]{"", "-1x Item", "0x Item", "99999999999x Item", "3x ", "Item", "1,00x Item", new string('a',20001), "1000000x Item\n1x ITEM"})
            Check(MarketShopping.Parse(invalid).Errors.Length > 0, "invalid import rejected " + invalid[..Math.Min(20,invalid.Length)]);
        Check(MarketShopping.Parse(string.Join('\n',Enumerable.Range(1,101).Select(i=>$"1x Item {i}"))).Errors.Length == 1, "item limit enforced");
        var cfg = new Configuration { MarketShoppingItems = imported.Items.ToList() };
        var restored = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(cfg));
        Check(restored!.MarketShoppingItems.SequenceEqual(imported.Items), "shopping list survives configuration round trip");
        MarketListing Row(uint world, long price, int quantity) => new("Europe",world,"World "+world,false,price,quantity,"Retainer",1);
        var plans = MarketShopping.Plans(new[]{Row(1,2,99),Row(1,3,99),Row(2,1,10),Row(3,5,180)},180);
        Check(plans[0].WorldId == 1 && plans[0].BuyQuantity == 198 && plans[0].Cost == 495 && plans[0].Available == 198, "required quantity includes whole stacks and overbuy");
        Check(plans.Last().WorldId == 2 && !plans.Last().Complete, "partial cheap world ranks after complete worlds");
        Check(plans.All(p=>p.Purchases.All(x=>x.WorldId==p.WorldId)), "world plans never mix destinations");
        var oversized = MarketShopping.Plans(new[]{Row(1,1,999),Row(1,2,3)},3).Single();
        Check(oversized.HasUnitPriceAlternative && oversized.UnitPricePurchases.Single().Quantity == 999 && oversized.UnitPricePurchases.Single().Total == 999, "bulk low-unit-price option retained alongside lower total");
        Check(!plans[0].HasUnitPriceAlternative, "identical purchase options not duplicated");
        Check(oversized.Cost == 6 && oversized.BuyQuantity == 3, "cheaper sufficient single stack beats oversized cheap-unit stack");
        Check(MarketShopping.Plans([],3).Length == 0 && MarketShopping.Plans([Row(1,1,1)],0).Length == 0, "empty inputs make no plan");
        Check(MarketShopping.Plans([Row(1,0,5),Row(1,1,0)],2).Length == 0, "invalid listing quantity or price ignored");
        var large = MarketShopping.Plans([Row(1,long.MaxValue/2,1),Row(1,long.MaxValue/2,1),Row(1,long.MaxValue/2,1)],3).Single();
        Check(large.Cost > 0 && !large.Complete, "combined cost cannot overflow into misleading cheap result");
    }
}

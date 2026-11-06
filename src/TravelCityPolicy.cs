namespace EquinoxCompanion;
public static class TravelCityPolicy
{
    public static uint Preferred(int travelCity,int shoppingCity,bool shopping)=>shopping
        ?shoppingCity switch{1=>2u,2=>9u,_=>8u}
        :travelCity switch{1=>8u,2=>2u,3=>9u,_=>0u};
    public static uint Select(IEnumerable<uint> unlocked,uint preferred)
    {
        var cities=unlocked.Where(x=>x is 2 or 8 or 9).Distinct().ToArray();
        return preferred!=0&&cities.Contains(preferred)?preferred:cities.FirstOrDefault();
    }
}

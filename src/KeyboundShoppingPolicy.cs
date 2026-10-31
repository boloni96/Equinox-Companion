namespace EquinoxCompanion;
public sealed record KeyboundPrize(string Key,string Name,int Cost);
public static class KeyboundShoppingPolicy
{
    // Square Enix official event shop, verified 2026-10-31:
    // https://eu.finalfantasyxiv.com/lodestone/special/keybound_brawler/
    public static readonly KeyboundPrize[] Prizes=[
        new("brawler-top","Avalanche Brawler's Top",430),
        new("brawler-gloves","Avalanche Brawler's Halfgloves",260),
        new("brawler-bottoms","Avalanche Brawler's Bottoms",430),
        new("brawler-boots","Avalanche Brawler's Boots",260),
        new("brawler-earring","Avalanche Brawler's Earring",190),
        new("leader-glasses","Avalanche Leader's Sunglasses",190),
        new("leader-vest","Avalanche Leader's Vest",430),
        new("leader-gloves","Avalanche Leader's Halfgloves",260),
        new("leader-trousers","Avalanche Leader's Cargo Trousers",430),
        new("leader-boots","Avalanche Leader's Boots",260),
        new("leader-earring","Avalanche Leader's Earring",190),
        new("gatling","Avalanche Gatling Gun",430),
        new("scorpion","Magitek Scorpion Identification Key",430),
        new("barret","Wind-up Barret",260),
        new("gold-saucer-roll","Welcome to the Gold Saucer Orchestrion Roll",190),
        new("battles-roll","Let the Battles Begin! Orchestrion Roll",190),
        new("fight-roll","Fight On! Orchestrion Roll",190),
        new("framer","Keybound Brawler Framer's Kit",190),
        new("emote","Ballroom Etiquette - Intrepid Inputs",260),
        new("coffer","Keybound Coffer",910)
    ];
    public static string Normalize(string name)=>string.Join(" ",name.Trim().Replace('’','\'').Replace('–','-').Replace('—','-').ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries));
    public static string? PrizeKey(string name)=>Prizes.FirstOrDefault(p=>Normalize(p.Name)==Normalize(name))?.Key;
    public static Dictionary<string,int> WishlistPlan(IEnumerable<(string Name,bool Wanted,bool Obtained)> wishes,out int unmatched)
    {
        var result=new Dictionary<string,int>();var unknown=new HashSet<string>();
        var rows=wishes.ToArray();
        foreach(var row in rows.Where(x=>x.Wanted&&!x.Obtained)){
            var key=PrizeKey(row.Name);
            if(key is null){unknown.Add(Normalize(row.Name));continue;}
            if(rows.Any(x=>x.Obtained&&PrizeKey(x.Name)==key))continue;
            result[key]=1;
        }
        unmatched=unknown.Count;return result;
    }
    public static int Quantity(IReadOnlyDictionary<string,int>? plan,string key)=>plan is null?1:Math.Clamp(plan.GetValueOrDefault(key,0),0,999);
    public static long Total(IReadOnlyDictionary<string,int>? plan)=>Prizes.Sum(p=>(long)p.Cost*Quantity(plan,p.Key));
    public static long Remaining(long total,uint balance)=>Math.Max(0,total-balance);
    public static long? Rounds(long remaining,IReadOnlyCollection<uint> earnings)
    {
        if(remaining<=0)return 0;
        if(earnings.Count==0)return null;
        var average=earnings.Average(x=>(double)x);
        return average>0?(long)Math.Ceiling(remaining/average):null;
    }
}
// One observation per completed round; starting on results gives no baseline.
// Capacity-limited and unobserved rounds never skew the estimate.
public sealed class KeyboundRoundEarnings
{
    private bool inside,seenResults,capped;
    private uint? baseline;
    private uint earned;
    public uint? Observe(bool inRound,bool results,uint balance,uint? capacity)
    {
        if(inRound&&!inside){baseline=results?null:balance;earned=0;seenResults=false;capped=false;}
        if(inRound){
            seenResults|=results;
            capped|=capacity.HasValue&&balance>=capacity.Value;
            if(baseline.HasValue&&balance<baseline.Value)baseline=null;
            if(baseline.HasValue)earned=Math.Max(earned,balance-baseline.Value);
        }
        uint? completed=null;
        if(!inRound&&inside&&seenResults&&baseline.HasValue&&!capped)completed=earned;
        inside=inRound;
        return completed;
    }
}


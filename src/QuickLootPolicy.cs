using System.Text.Json;
namespace EquinoxCompanion;

public enum QuickLootRoll { Nothing, Need, Greed, Pass }
public sealed class QuickLootRule
{
    public uint Id { get; set; }
    public bool Enabled { get; set; } = true;
    public QuickLootRoll Roll { get; set; } = QuickLootRoll.Nothing;
}
public sealed class QuickLootCollectionFilter
{
    public bool Enabled { get; set; }
    public bool UntradeableOnly { get; set; }
}
public sealed class QuickLootSettings
{
    public bool Automatic { get; set; }
    public QuickLootRoll Mode { get; set; } = QuickLootRoll.Need;
    public float ManualMin { get; set; } = .5f;
    public float ManualMax { get; set; } = 1f;
    public float AutoMin { get; set; } = 1.5f;
    public float AutoMax { get; set; } = 3f;
    public bool ProtectWeekly { get; set; } = true;
    public bool KeepGlamour { get; set; } = true;
    public bool OtherJobs { get; set; }
    public bool MinimumLevel { get; set; }
    public int Level { get; set; }
    public bool BelowAverage { get; set; }
    public int BelowAverageBy { get; set; } = 30;
    public QuickLootRoll BelowAverageRoll { get; set; } = QuickLootRoll.Greed;
    public bool NotUpgrade { get; set; }
    public QuickLootRoll NotUpgradeRoll { get; set; } = QuickLootRoll.Greed;
    public bool MinimumSeals { get; set; }
    public int Seals { get; set; } = 1000;
    public bool PreventFailurePass { get; set; } = true;
    public bool Chat { get; set; } = true;
    public bool NormalToast { get; set; }
    public bool QuestToast { get; set; }
    public bool ErrorToast { get; set; } = true;
    public bool Diagnostics { get; set; }
    public bool ShowBar { get; set; } = true;
    public Dictionary<string,QuickLootCollectionFilter> Collections { get; set; } = [];
    public List<QuickLootRule> Items { get; set; } = [];
    public List<QuickLootRule> Duties { get; set; } = [];
    public static readonly string[] Categories = ["All unlockables","Mounts","Minions","Bardings","Cards","Emotes / hairstyles","Orchestrion rolls","Faded copies"];
}
public sealed record QuickLootFacts(uint ItemId,uint DutyId,bool Known,bool CanNeed,bool CanGreed,bool Weekly,
    bool UniqueOwned,bool Untradeable,bool Unlocked,string Category,bool Equipment,int ItemLevel,
    bool Glamour,bool? FitsJob,int AverageLevel,int? EquippedLevel,int? SealValue);
public sealed record QuickLootDecision(QuickLootRoll Roll,string Reason);
public static class QuickLootPolicy
{
    public static QuickLootDecision Decide(QuickLootSettings s,QuickLootFacts f,QuickLootRoll intent)
    {
        if(!f.Known)return new(QuickLootRoll.Nothing,"Item data unavailable; left for you");
        if(s.ProtectWeekly&&f.Weekly)return new(QuickLootRoll.Nothing,"Weekly loot protection; manual game roll required");
        var rule=s.Items.FirstOrDefault(r=>r.Enabled&&r.Id==f.ItemId);
        var reason="Item override";
        if(rule is null){rule=s.Duties.FirstOrDefault(r=>r.Enabled&&r.Id==f.DutyId);reason="Duty override";}
        if(rule is not null)return Limit(rule.Roll,f,reason);
        if(intent==QuickLootRoll.Nothing)return new(intent,"No rolling mode selected");
        if(intent==QuickLootRoll.Pass)return Limit(intent,f,"Pass requested");
        if(s.KeepGlamour&&f.Glamour)return Limit(intent,f,"Glamour protected from global filters");
        bool Filter(string key)=>s.Collections.TryGetValue(key,out var c)&&c.Enabled&&(!c.UntradeableOnly||f.Untradeable);
        if(f.Unlocked&&(Filter("All unlockables")||Filter(f.Category)))return Limit(QuickLootRoll.Pass,f,"Already unlocked on this character");
        if(s.OtherJobs&&f.FitsJob==false)return Limit(QuickLootRoll.Pass,f,"Item for another job");
        if(f.Equipment)
        {
            if(s.MinimumLevel&&f.ItemLevel<s.Level)return Limit(QuickLootRoll.Pass,f,"Below minimum item level");
            if(s.MinimumSeals&&f.SealValue is {} seals&&seals<s.Seals)return Limit(QuickLootRoll.Pass,f,"Below minimum expert-delivery seal value");
            if(f.CanNeed&&s.BelowAverage&&f.AverageLevel>0&&f.ItemLevel<f.AverageLevel-s.BelowAverageBy)return Limit(s.BelowAverageRoll,f,"Below current job average threshold");
            if(f.CanNeed&&s.NotUpgrade&&f.EquippedLevel is {} level&&f.ItemLevel<=level)return Limit(s.NotUpgradeRoll,f,"Not a higher item-level upgrade");
        }
        return Limit(intent,f,"Default rolling mode");
    }
    private static QuickLootDecision Limit(QuickLootRoll wanted,QuickLootFacts f,string reason)
    {
        if(wanted==QuickLootRoll.Nothing)return new(wanted,reason+"; do nothing");
        if(f.UniqueOwned)return new(QuickLootRoll.Pass,"Unique item already held by this character");
        var result=wanted==QuickLootRoll.Need&&!f.CanNeed?(f.CanGreed?QuickLootRoll.Greed:QuickLootRoll.Pass):wanted==QuickLootRoll.Greed&&!f.CanGreed?QuickLootRoll.Pass:wanted;
        return new(result,reason+(result!=wanted?"; limited by game roll permissions":""));
    }
    public static double Delay(QuickLootSettings s,bool automatic,double sample)
    {
        var min=automatic?s.AutoMin:s.ManualMin;var max=automatic?s.AutoMax:s.ManualMax;
        min=float.IsFinite(min)?Math.Clamp(min,.5f,60):1.5f;max=float.IsFinite(max)?Math.Clamp(max,min,60):min;
        return min+(max-min)*Math.Clamp(sample,0,1);
    }
    public static string ExportRules(IEnumerable<QuickLootRule> rules)=>JsonSerializer.Serialize(rules,new JsonSerializerOptions{WriteIndented=true});
    public static List<QuickLootRule> ImportRules(string json)
    {
        if(json.Length>256000)throw new FormatException("Rule list is too large.");
        var rows=JsonSerializer.Deserialize<List<QuickLootRule>>(json)??throw new FormatException("Expected a rule list.");
        if(rows.Count>2000||rows.Any(r=>r is null||r.Id==0||!Enum.IsDefined(r.Roll))||rows.Select(r=>r.Id).Distinct().Count()!=rows.Count)throw new FormatException("Use unique positive IDs and valid roll choices; maximum 2,000 rules.");
        return rows;
    }
}

using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    [PluginService] internal static ISigScanner QuickLootScanner { get; private set; } = null!;
    [PluginService] internal static IDtrBar QuickLootBar { get; private set; } = null!;
    [PluginService] internal static IToastGui QuickLootToasts { get; private set; } = null!;
    private unsafe delegate bool QuickLootNativeRoll(Loot* loot,RollResult result,uint slot);
    private QuickLootNativeRoll? quickLootNative;
    private bool quickLootNativeFailed;
    private IDtrBarEntry? quickLootBarEntry;
    private readonly HashSet<string> quickLootAttempted=[];
    private HashSet<string>? quickLootManualItems;
    private QuickLootRoll quickLootManualMode;
    private DateTimeOffset quickLootNextTick,quickLootNextRoll;
    private ulong quickLootCharacter;
    private uint quickLootTerritory;
    private string quickLootStatus="Automatic rolling is off.";
    private readonly Queue<string> quickLootHistory=[];
    private Dictionary<uint,uint[]>? quickLootFadedResults;
    private sealed record QuickLootOffer(uint Slot,string Key,uint ItemId,string Name,QuickLootFacts Facts);
    private sealed record QuickLootReceipt(QuickLootOffer Offer,QuickLootDecision Decision,DateTimeOffset At);
    private QuickLootReceipt? quickLootReceipt;
    private QuickLootSettings CurrentQuickLoot => config.QuickLoot;
    private void SaveQuickLoot(QuickLootSettings settings)
    {
        config.QuickLoot=settings;
        Pi.SavePluginConfig(config);
    }
    private void StopQuickLoot(string reason)
    {
        quickLootManualItems=null;quickLootReceipt=null;quickLootNextRoll=default;quickLootAttempted.Clear();quickLootStatus=reason;
    }
    private unsafe bool QuickLootUnlocked(uint id)
    {
        var item=ExdModule.GetItemRowById(id);var ui=UIState.Instance();
        return item!=null&&ui!=null&&ui->IsItemActionUnlocked(item)==1;
    }
    private unsafe QuickLootFacts QuickLootReadFacts(uint id,uint duty,bool need,bool greed,bool weekly)
    {
        var row=DataManager.GetExcelSheet<Item>().GetRowOrDefault(id);
        if(row is not {} item)return new(id,duty,false,need,greed,weekly,false,false,false,"",false,0,false,null,0,null,null);
        var inv=InventoryManager.Instance();var ui=UIState.Instance();
        var equipment=item.EquipSlotCategory.RowId!=0;
        var action=item.ItemAction.Value.Action.RowId;
        var category=action switch {1322=>"Mounts",853=>"Minions",1013=>"Bardings",3357=>"Cards",2633=>"Emotes / hairstyles",25183=>"Orchestrion rolls",_=>""};
        var unlocked=action!=0&&QuickLootUnlocked(id);
        if(item.FilterGroup==12&&item.ItemUICategory.RowId==94)
        {
            category="Faded copies";
            var settings=CurrentQuickLoot;
            if(settings.Collections.Any(c=>(c.Key=="All unlockables"||c.Key==category)&&c.Value.Enabled))
            {
                // One lazy catalogue index. Only constructed when this filter is used.
                quickLootFadedResults??=DataManager.GetExcelSheet<Recipe>()
                    .Where(r=>r.ItemResult.RowId!=0&&r.ItemResult.Value.ItemAction.Value.Action.RowId==25183)
                    .SelectMany(r=>r.Ingredient.Where(i=>i.RowId!=0).Select(i=>(Ingredient:i.RowId,Result:r.ItemResult.RowId)))
                    .GroupBy(x=>x.Ingredient).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Result).Distinct().ToArray());
                unlocked=quickLootFadedResults.TryGetValue(id,out var results)&&results.Length>0&&results.All(QuickLootUnlocked);
            }
        }
        bool? fits=null;
        if(equipment&&Player.ClassJob.RowId!=0)
        {
            var job=DataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(Player.ClassJob.RowId);
            if(job is {} j)
            {
                var property=typeof(ClassJobCategory).GetProperty(j.Abbreviation.ToString());
                if(property?.PropertyType==typeof(bool))fits=(bool?)property.GetValue(item.ClassJobCategory.Value);
            }
        }
        if(action==29153)fits=Player.ClassJob.RowId is 1 or 19;
        int? equipped=null;
        if(equipment&&inv!=null)
        {
            var bag=inv->GetInventoryContainer(InventoryType.EquippedItems);
            if(bag!=null)for(var i=0;i<bag->Size;i++)
            {
                var slot=bag->GetInventorySlot(i);
                // An empty compatible slot is an upgrade opportunity, especially
                // the second ring slot. Do not compare only the occupied ring.
                string[] slotNames=["MainHand","OffHand","Head","Body","Gloves","Waist","Legs","Feet","Ears","Neck","Wrists","FingerL","FingerR","SoulCrystal"];
                if(i<slotNames.Length&&typeof(EquipSlotCategory).GetProperty(slotNames[i])?.GetValue(item.EquipSlotCategory.Value) is {} allowed&&Convert.ToInt32(allowed)>0&&(slot==null||slot->ItemId==0)){equipped=0;continue;}
                if(slot==null||slot->ItemId==0)continue;
                var worn=DataManager.GetExcelSheet<Item>().GetRowOrDefault(slot->ItemId%1000000);
                if(worn is {} w&&w.EquipSlotCategory.RowId==item.EquipSlotCategory.RowId)equipped=Math.Min(equipped??int.MaxValue,(int)w.LevelItem.RowId);
            }
        }
        int? seals=null;
        if(equipment&&item.Rarity>1&&item.PriceLow>0&&item.ClassJobCategory.RowId>0)
        {var reward=DataManager.GetExcelSheet<GCSupplyDutyReward>().GetRowOrDefault(item.LevelItem.RowId);if(reward is {} r)seals=(int)r.SealsExpertDelivery;}
        return new(id,duty,true,need,greed,weekly,item.IsUnique&&inv!=null&&inv->GetInventoryItemCount(id)>0,item.IsUntradable,unlocked,category,equipment,(int)item.LevelItem.RowId,
            equipment&&item.LevelEquip==1&&item.LevelItem.RowId==1,fits,ui==null?0:ui->CurrentItemLevel,equipped,seals);
    }
    private unsafe List<QuickLootOffer> ReadQuickLootOffers()
    {
        var offers=new List<QuickLootOffer>();if(!Player.IsLoaded)return offers;
        var loot=Loot.Instance();var game=GameMain.Instance();if(loot==null||game==null)return offers;
        for(var i=0;i<loot->Items.Length;i++)
        {
            var x=loot->Items[i];
            if(x.ChestObjectId is 0 or 0xE0000000||x.ItemId==0||x.RollResult!=RollResult.UnAwarded||x.RollState is not (RollState.UpToNeed or RollState.UpToGreed or RollState.UpToPass)||x.LootMode is not (LootMode.Normal or LootMode.GreedOnly))continue;
            var id=x.ItemId%1000000;var name=DataManager.GetExcelSheet<Item>().GetRowOrDefault(id)?.Name.ToString()??$"Item {id}";
            var need=x.RollState==RollState.UpToNeed&&x.LootMode==LootMode.Normal;var greed=x.RollState is RollState.UpToNeed or RollState.UpToGreed;
            offers.Add(new((uint)i,$"{x.ChestObjectId}:{x.ChestItemIndex}:{x.ItemId}:{i}",id,name,QuickLootReadFacts(id,game->CurrentContentFinderConditionId,need,greed,x.WeeklyLootItem)));
        }
        return offers;
    }
    private void QuickLootReport(string text,bool error=false)
    {
        quickLootStatus=text;quickLootHistory.Enqueue(DateTimeOffset.Now.ToString("HH:mm:ss")+" · "+text);
        while(quickLootHistory.Count>80)quickLootHistory.Dequeue();
        var s=CurrentQuickLoot;
        if(s.Chat)Chat.Print("[QuickLoot] "+text);
        if(error&&s.ErrorToast)QuickLootToasts.ShowError(text);
        else if(!error){if(s.NormalToast)QuickLootToasts.ShowNormal(text);if(s.QuestToast)QuickLootToasts.ShowQuest(text);}
    }
    private bool QuickLootConflict()=>Pi.InstalledPlugins.Any(p=>p.IsLoaded&&p.InternalName=="LazyLoot");
    private unsafe void OnQuickLootCommand(string args)
    {
        if(!config.EnableQuickLoot){Chat.Print("[QuickLoot] Enable QuickLoot in Settings → Tracking first.");return;}
        var value=args.ToLowerInvariant();var s=CurrentQuickLoot;
        if(value is "need" or "greed" or "pass"){QueueQuickLoot(Enum.Parse<QuickLootRoll>(value,true));return;}
        if(value is "on" or "off" or "toggle")
        {s.Automatic=value=="on"||value=="toggle"&&!s.Automatic;StopQuickLoot(s.Automatic?"Automatic rolling enabled.":"Automatic rolling disabled.");SaveQuickLoot(s);return;}
        if(value.StartsWith("test ")&&uint.TryParse(value[5..],out var id)&&Player.IsLoaded)
        {var game=GameMain.Instance();var facts=QuickLootReadFacts(id,game==null?0u:game->CurrentContentFinderConditionId,true,true,false);var decision=QuickLootPolicy.Decide(s,facts,s.Mode);Chat.Print($"[QuickLoot] Preview {id}: {decision.Roll} · {decision.Reason}. Assumes normal roll permissions; no roll sent.");return;}
        visible=true;quickLootSelectTab=true;
    }
    private void QueueQuickLoot(QuickLootRoll roll)
    {
        if(!config.EnableQuickLoot)return;
        if(!Player.IsLoaded||QuickLootConflict()){quickLootStatus="Log in and disable LazyLoot before rolling with QuickLoot.";return;}
        StopQuickLoot("Preparing current loot.");quickLootManualMode=roll;
        quickLootManualItems=ReadQuickLootOffers().Select(x=>x.Key).ToHashSet();
        quickLootCharacter=Player.ContentId;quickLootTerritory=Client.TerritoryType;
        quickLootNextRoll=DateTimeOffset.UtcNow.AddSeconds(QuickLootPolicy.Delay(CurrentQuickLoot,false,Random.Shared.NextDouble()));
        quickLootStatus=$"Queued {quickLootManualItems.Count} current loot entries. Your filters still apply.";
    }
    private unsafe void UpdateQuickLoot(DateTimeOffset now)
    {
        if(!config.EnableQuickLoot)return;
        if(now<quickLootNextTick)return;quickLootNextTick=now.AddMilliseconds(250);
        if(!Player.IsLoaded||Player.ContentId==0){StopQuickLoot("Waiting for character.");if(quickLootBarEntry is not null)quickLootBarEntry.Shown=false;return;}
        if(quickLootCharacter!=Player.ContentId||quickLootTerritory!=Client.TerritoryType){StopQuickLoot("Character or area changed; old queue cleared.");quickLootCharacter=Player.ContentId;quickLootTerritory=Client.TerritoryType;}
        var s=CurrentQuickLoot;
        if(s.ShowBar)
        {
            quickLootBarEntry??=QuickLootBar.Get("Equinox QuickLoot");
            quickLootBarEntry.Shown=true;quickLootBarEntry.Text=new SeStringBuilder().AddText("QL: "+(s.Automatic?(QuickLootConflict()?"Paused":s.Mode.ToString()):"Off")).Build();
            quickLootBarEntry.Tooltip=new SeStringBuilder().AddText("QuickLoot · click to toggle automatic rolling. Settings: /equinox → QuickLoot").Build();
            quickLootBarEntry.OnClick=_=>{s.Automatic=!s.Automatic;StopQuickLoot(s.Automatic?"Automatic rolling enabled.":"Automatic rolling disabled.");SaveQuickLoot(s);};
        }
        else if(quickLootBarEntry is not null)quickLootBarEntry.Shown=false;
        if(!s.Automatic&&quickLootManualItems is null&&quickLootReceipt is null)return;
        if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||QuickLootConflict()){StopQuickLoot("Rolling paused: area transition or LazyLoot is enabled.");return;}
        try
        {
            var offers=ReadQuickLootOffers();var keys=offers.Select(x=>x.Key).ToHashSet();
            quickLootAttempted.RemoveWhere(k=>!keys.Contains(k));
            if(quickLootReceipt is {} receipt)
            {
                if(!keys.Contains(receipt.Offer.Key)){QuickLootReport($"{receipt.Decision.Roll} submitted for {receipt.Offer.Name}; entry is no longer pending.");quickLootReceipt=null;}
                else if(now-receipt.At>=TimeSpan.FromSeconds(6))
                {
                    QuickLootReport($"No roll acknowledgement for {receipt.Offer.Name}; left for manual review.",true);quickLootReceipt=null;
                    if(!s.PreventFailurePass&&receipt.Decision.Roll!=QuickLootRoll.Pass)
                    {var same=offers.FirstOrDefault(o=>o.Key==receipt.Offer.Key);if(same is not null&&!same.Facts.Weekly)SubmitQuickLoot(same,new(QuickLootRoll.Pass,"Explicit failure-pass setting"),now);}
                }
                return;
            }
            if(quickLootManualItems is not null)
            {quickLootManualItems.IntersectWith(keys);quickLootManualItems.ExceptWith(quickLootAttempted);if(quickLootManualItems.Count==0){quickLootManualItems=null;quickLootNextRoll=default;return;}}
            var automatic=quickLootManualItems is null;
            foreach(var offer in offers)
            {
                if(quickLootAttempted.Contains(offer.Key)||!automatic&&!quickLootManualItems!.Contains(offer.Key))continue;
                var choice=QuickLootPolicy.Decide(s,offer.Facts,automatic?s.Mode:quickLootManualMode);
                if(choice.Roll==QuickLootRoll.Nothing){if(!automatic)quickLootManualItems!.Remove(offer.Key);continue;}
                if(quickLootNextRoll==default){quickLootNextRoll=now.AddSeconds(QuickLootPolicy.Delay(s,automatic,Random.Shared.NextDouble()));return;}
                if(now<quickLootNextRoll)return;
                SubmitQuickLoot(offer,choice,now);
                quickLootNextRoll=now.AddSeconds(QuickLootPolicy.Delay(s,automatic,Random.Shared.NextDouble()));return;
            }
            quickLootNextRoll=default;
        }
        catch(Exception ex){StopQuickLoot("QuickLoot stopped after an error; check Diagnostics.");s.Automatic=false;SaveQuickLoot(s);errorJournal.Record("quickloot","Rolling stopped",exceptionType:ex.GetType().Name);QuickLootReport(quickLootStatus,true);}
    }
    private unsafe void SubmitQuickLoot(QuickLootOffer offer,QuickLootDecision choice,DateTimeOffset now)
    {
        if(quickLootNativeFailed){quickLootStatus="Native rolling unavailable; restart after a compatible update.";return;}
        if(quickLootNative is null)
        {
            // Native roll signature documented in LazyLoot's Roller; no hooks installed.
            if(!QuickLootScanner.TryScanText("41 83 F8 ?? 0F 83 ?? ?? ?? ?? 48 89 5C 24 08",out var address))
            {quickLootNativeFailed=true;CurrentQuickLoot.Automatic=false;SaveQuickLoot(CurrentQuickLoot);StopQuickLoot("Native rolling unavailable on this game build.");QuickLootReport(quickLootStatus,true);return;}
            quickLootNative=Marshal.GetDelegateForFunctionPointer<QuickLootNativeRoll>(address);
        }
        var loot=Loot.Instance();if(loot==null||offer.Slot>=loot->Items.Length)return;
        var x=loot->Items[(int)offer.Slot];var key=$"{x.ChestObjectId}:{x.ChestItemIndex}:{x.ItemId}:{offer.Slot}";
        if(key!=offer.Key||x.RollResult!=RollResult.UnAwarded)return;
        quickLootAttempted.Add(key);
        var result=choice.Roll switch {QuickLootRoll.Need=>RollResult.Needed,QuickLootRoll.Greed=>RollResult.Greeded,_=>RollResult.Passed};
        // The native bool is not a server acknowledgement. Track the observed
        // pending entry regardless, so a successful roll cannot lose feedback.
        var nativeResult=quickLootNative(loot,result,offer.Slot);
        quickLootReceipt=new(offer,choice,now);
        if(CurrentQuickLoot.Diagnostics)quickLootHistory.Enqueue($"{now:HH:mm:ss} · Native return: {nativeResult}; awaiting loot-state change.");
        if(CurrentQuickLoot.Diagnostics){quickLootHistory.Enqueue($"{now:HH:mm:ss} · {offer.Name}: {choice.Roll} · {choice.Reason}");while(quickLootHistory.Count>80)quickLootHistory.Dequeue();}
    }
}

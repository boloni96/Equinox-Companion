using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong suppliesCharacter;
    private DateTimeOffset suppliesReadyAt;
    private string? CurrentSubmarineFc(Actor actor)
    {
        var local=config.Discoveries.LastOrDefault(e=>e.Kind=="character.updated"&&e.Actor.ContentId==actor.ContentId)?.Character;
        if(local?.FcMember==false)return null;
        if(local?.FreeCompany is {} fc)return fc.Id;
        var matches=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>string.Equals(c.Name,actor.Name,StringComparison.OrdinalIgnoreCase)&&string.Equals(c.World,actor.HomeWorldName,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length==1&&matches[0].FcMember==true&&matches[0].FcId.Length>0?matches[0].FcId:null;
    }
    private unsafe void ObserveSubmarineSupplies(Actor actor,DateTimeOffset now)
    {
        if(suppliesCharacter!=Player.ContentId){suppliesCharacter=Player.ContentId;suppliesReadyAt=now.AddSeconds(10);return;}
        if(now<suppliesReadyAt)return;
        var fcId=CurrentSubmarineFc(actor);if(fcId is null)return;
        var manager=InventoryManager.Instance();if(manager==null)return;
        int space=0,capacity=0,tanks=0,repairs=0;
        // The four carried bags only: saddlebags, retainers and the FC chest are separate.
        for(var bag=0;bag<4;bag++)
        {
            var container=manager->GetInventoryContainer((InventoryType)bag);
            if(container==null||!container->IsLoaded||container->Items==null||container->Size!=35)return;
            capacity+=container->Size;
            for(var i=0;i<container->Size;i++)
            {
                var item=container->Items[i];if(item.ItemId==0){space++;continue;}
                if(item.ItemId==10155)tanks+=checked((int)item.Quantity);
                if(item.ItemId==10373)repairs+=checked((int)item.Quantity);
            }
        }
        var data=new SubmarineSupplies(fcId,tanks,repairs,space,capacity);
        var last=config.Discoveries.LastOrDefault(e=>e.Kind=="submarines.supplies"&&e.Actor.ContentId==actor.ContentId);
        // Confirm unchanged counts periodically only after actually reading loaded inventory.
        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"submarines.supplies",now,actor,null,Supplies:data),last is null||now-last.At>=TimeSpan.FromMinutes(5));
    }
    private Dictionary<string,SharedSubmarineSupplies> SubmarineSupplyRecords()
    {
        var chars=config.SharedRoster?.People.SelectMany(p=>p.Characters).ToArray()??[];
        var records=new List<SharedSubmarineSupplies>(config.SharedRoster?.SubmarineSupplies??[]);
        foreach(var e in config.Discoveries.Where(e=>e.Supplies is not null))
        {
            var matches=chars.Where(c=>string.Equals(c.Name,e.Actor.Name,StringComparison.OrdinalIgnoreCase)&&string.Equals(c.World,e.Actor.HomeWorldName,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if(matches.Length==1&&matches[0].FcMember!=false&&matches[0].FcId==e.Supplies!.FcId)records.Add(new(matches[0].Id,e.At,e.Supplies));
        }
        return records.Where(r=>chars.Any(c=>c.Id==r.CharacterId&&c.FcMember!=false&&c.FcId==r.Data.FcId)).GroupBy(r=>r.CharacterId).ToDictionary(g=>g.Key,g=>g.MaxBy(r=>r.ObservedAt)!);
    }
}

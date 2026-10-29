using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Dictionary<ulong,ChestStack[]> organizerVisitCache=[];
    private readonly HashSet<ulong> organizerReplans=[];
    private readonly Dictionary<uint,int> organizerEntrusted=[];
    private readonly HashSet<(uint,int)> organizerProtectedBagSlots=[];
    private int OrganizerStackLimit(uint item)=>(int)(DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item)?.StackSize??1);
    private static ChestStack[] EmptyOrganizerRetainer()=>Enumerable.Range(0,175).Select(i=>new ChestStack((uint)(10000+i/25),i%25,0,0,false,0)).Concat(Enumerable.Range(0,18).Select(i=>new ChestStack(12001,i,(uint)(i+2),0,false,9999))).ToArray();
    private unsafe Dictionary<ulong,ChestStack[]> RecordedOrganizerInventories(ulong[] owners,bool refreshStock=true)
    {
        if(refreshStock)RefreshRetrievalStock();var result=new Dictionary<ulong,ChestStack[]>();
        var manager=RetainerManager.Instance();if(manager==null||!manager->IsReady)return result;
        var counts=new Dictionary<ulong,int>();for(uint i=0;i<manager->GetRetainerCount();i++){var retainer=manager->GetRetainerBySortedIndex(i);if(retainer!=null)counts[retainer->RetainerId]=retainer->ItemCount;}
        var observations=config.Discoveries.Where(e=>e.Actor.ContentId==Player.ContentId.ToString()&&e.Storage?.Key.StartsWith("retainer:")==true).GroupBy(e=>e.Storage!.Key).ToDictionary(g=>g.Key,g=>g.MaxBy(x=>x.At)!);
        foreach(var owner in owners){
            var record=config.OrganizerInventories.Where(x=>x.Character==Player.ContentId&&x.Retainer==owner).MaxBy(x=>x.At);
            var types=Enumerable.Range(10000,7).Append(12001).ToArray();
            var rows=types.Select(t=>observations.GetValueOrDefault($"retainer:{owner}:{t}")).ToArray();
            ChestStack[]? slots=null;
            if(record!=null&&rows.All(x=>x==null||x.At<=record.At))slots=record.Slots.ToArray();
            else if(rows.All(x=>x?.Storage?.Stacks!=null)){
                var candidate=EmptyOrganizerRetainer();var valid=true;
                foreach(var (observation,index) in rows.Select((x,i)=>(x,i))){
                    var container=(uint)types[index];var used=new HashSet<int>();
                    foreach(var stack in observation!.Storage!.Stacks!){
                        var positions=stack.Positions;if(positions==null){valid=false;break;}
                        foreach(var pos in positions){var at=Array.FindIndex(candidate,x=>x.Container==container&&x.Slot==pos.Slot-1);if(at<0||!used.Add(at)||pos.Quantity<=0||pos.Quantity>OrganizerStackLimit(stack.ItemId)){valid=false;break;}candidate[at]=new(container,pos.Slot-1,stack.ItemId,pos.Quantity,stack.Hq,OrganizerStackLimit(stack.ItemId));}
                    }
                }
                if(valid)slots=candidate;
            }
            if(slots==null&&retrievalRawInventory.TryGetValue(owner,out var raw)){
                // Raw IPC records preserve each occupied stack; virtual positions are
                // used only for planning and rebound to verified native slots on arrival.
                var candidate=EmptyOrganizerRetainer();var valid=true;
                foreach(var row in raw.Where(x=>x.Length>3&&RetainerRetrieval.IsRetainerStorage(x[0])&&x[2]>0&&x[3]>0)){
                    if(row[2]>uint.MaxValue||row[3]>int.MaxValue){valid=false;break;}
                    var item=(uint)row[2];var limit=OrganizerStackLimit(item);var qty=(int)row[3];
                    var at=Array.FindIndex(candidate,x=>row[0]==12001?x.Container==12001&&x.Item==item&&x.Quantity==0:x.Container==(uint)row[0]&&x.Item==0);
                    if(at<0||qty>limit){valid=false;break;}candidate[at]=candidate[at] with{Item=item,Quantity=qty,Hq=row.Length>6&&(row[6]&1)!=0,Limit=limit};
                }
                if(valid)slots=candidate;
            }
            if(slots!=null&&StorageVisit.RecordedMatches(slots,counts.GetValueOrDefault(owner,-1)))result[owner]=slots;
        }
        return result;
    }
    private void RememberOrganizerInventory(ulong owner,ChestStack[] slots)
    {
        organizerVisitCache[owner]=slots.ToArray();organizeInventories[owner]=slots.ToArray();
    }
    private void SaveOrganizerInventories()
    {
        if(organizerVisitCache.Count==0)return;
        var character=organizePort?.Character??0;if(character==0)return;
        foreach(var (owner,slots) in organizerVisitCache){config.OrganizerInventories.RemoveAll(x=>x.Character==character&&x.Retainer==owner);config.OrganizerInventories.Add(new(character,owner,DateTimeOffset.UtcNow,slots));}
        organizerVisitCache.Clear();SaveConfiguration();
    }
    private void BindOrganizerVisit()
    {
        var slots=OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray();RememberOrganizerInventory(organizeActiveRetainer,slots);
        var visit=organizeMoves.Skip(organizeMoveIndex).TakeWhile(x=>x.Retainer==organizeActiveRetainer).ToArray();
        RoutedMove[] rebound;
        try{rebound=StorageVisit.Bind(visit,OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).Where(s=>!organizeBagsOnly||InventoryTradeFilterAllows(s,organizeBagTradeFilter)).ToArray(),slots,!organizeBagsOnly,organizerProtectedBagSlots);}
        catch(InvalidOperationException) when(organizeCarry==0&&organizeActiveRules.All(r=>r.From==StorageRouting.AllSources||r.From==StorageRouting.Bags)&&organizerReplans.Add(organizeActiveRetainer)){
            // No cargo in transit: safely regenerate only the remaining intent
            // using this refreshed inventory and the unchanged recorded others.
            BeginStorageCalculation();return;
        }
        organizeMoves=organizeMoves.Take(organizeMoveIndex).Concat(rebound).Concat(organizeMoves.Skip(organizeMoveIndex+visit.Length)).ToArray();
        if(rebound.Length>0)OrganizeStage(6);else OrganizeStage(8);
    }
}


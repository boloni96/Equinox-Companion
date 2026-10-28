using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<(int Version,ulong Character,StoragePreviewRow[] Rows,int Remaining)>? organizePreviewTask;
    private int organizePreviewVersion;
    private ulong organizePreviewOwner;
    private bool organizePreviewDirty=true,organizePreviewAll,organizePreviewBell;
    private DateTimeOffset organizePreviewAfter;
    private string organizePreviewMessage="Open the summoning bell to preview recorded retainer movements.";
    private Dictionary<ulong,string> organizePreviewNames=[];
    private void InvalidateOrganizePreview(){organizePreviewVersion++;organizePreview=[];organizePreviewDirty=true;organizePreviewAll=false;organizePreviewAfter=DateTimeOffset.UtcNow.AddMilliseconds(250);organizePreviewMessage="Preview needs updating…";}
    private void CheckOrganize(){InvalidateOrganizePreview();organizePreviewAfter=default;}
    private void CheckAllOrganize(){CheckOrganize();organizePreviewAll=true;}
    private unsafe void BeginOrganizePreview()
    {
        var retainers=ReadSellerRetainers();
        if(!Player.IsLoaded||retainers.Length==0){organizePreviewMessage="Open the summoning bell to preview this character’s retainers.";return;}
        var rules=organizePreviewAll?new StorageRoute[]{new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true)}:OrganizeRules();
        var owners=retainers.Select(x=>x.Id).Where(id=>rules.Any(r=>r.To==StorageAutoRouting.AllRetainers||r.To==id||r.From==StorageRouting.AllSources||r.From==id)).ToArray();
        organizePreviewNames=retainers.ToDictionary(x=>x.Id,x=>x.Name);
        if(config.UseRetainerAssignments.Contains(Player.ContentId)&&rules.Any(r=>r.To==StorageAutoRouting.AllRetainers)&&!StorageAssignments.HasSelections(config.RetainerAssignments.GetValueOrDefault(Player.ContentId)??[],config.RetrievalPresets)){organizePreviewMessage="Choose saved plans, items or categories in Retainer assignments, or turn off Use assignments.";return;}
        var cached=RecordedOrganizerInventories(owners,false);
        if(cached.Count<owners.Length){organizePreviewMessage=$"{cached.Count}/{owners.Length} inventories recorded. Missing: "+string.Join(", ",retainers.Where(x=>owners.Contains(x.Id)&&!cached.ContainsKey(x.Id)).Select(x=>x.Name))+". Open those inventories once; previews never summon retainers or move items.";return;}
        cached[StorageRouting.Bags]=OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).ToArray();
        if(config.UseRetainerAssignments.Contains(Player.ContentId))rules=StorageAssignments.Apply(rules,(config.RetainerAssignments.GetValueOrDefault(Player.ContentId)??[]).ToArray(),cached,OrganizeCategory,config.RetrievalPresets);
        var categories=cached.Values.SelectMany(x=>x).Where(x=>x.Item>0).Select(x=>x.Item).Distinct().ToDictionary(x=>x,OrganizeCategory);
        var plan=new StorageRoutingPlan("Preview",Player.ContentId,rules,organizeReserve);var version=organizePreviewVersion;var owner=Player.ContentId;
        organizePreviewMessage="Calculating movements and available space…";
        organizePreviewTask=Task.Run(()=>{var result=StorageRouting.Build(plan,cached,id=>categories.GetValueOrDefault(id),includeExistingBags:false);return(version,owner,StoragePreview.Describe(result.Moves),result.Remaining);});
    }
    private void DrawOrganizePreview()
    {
        var owner=Player.IsLoaded?Player.ContentId:0;var bell=RetainerToolsContext();
        if(owner!=organizePreviewOwner||bell&&!organizePreviewBell){organizePreviewOwner=owner;InvalidateOrganizePreview();}
        organizePreviewBell=bell;
        if(organizePreviewTask?.IsCompleted==true){
            var task=organizePreviewTask;organizePreviewTask=null;
            try{var result=task.GetAwaiter().GetResult();if(result.Version==organizePreviewVersion&&result.Character==owner){organizePreview=result.Rows;organizePreviewPage=0;organizePreviewMessage=organizePreview.Length==0?(result.Remaining>0?$"No safe movements available · {result.Remaining:N0} items blocked by space.":"No movements needed for this selection."):$"{organizePreview.Length} planned movements"+(result.Remaining>0?$" · {result.Remaining:N0} items blocked by space":"")+" · live slots checked before moving";}}
            catch(Exception e){organizePreviewMessage="Preview unavailable: "+e.Message;}
        }
        if(organizePreviewDirty&&organizePreviewTask==null&&!organizeRunning&&!retrievalRun.Running&&!chestRunning&&DateTimeOffset.UtcNow>=organizePreviewAfter){
            organizePreviewDirty=false;try{BeginOrganizePreview();}catch(Exception e){organizePreviewMessage="Preview unavailable: "+e.Message;}
        }
        ImGui.TextWrapped(organizePreviewMessage);
        if(organizePreview.Length==0)return;
        var pages=(organizePreview.Length+39)/40;organizePreviewPage=Math.Clamp(organizePreviewPage,0,pages-1);
        if(pages>1){ImGui.BeginDisabled(organizePreviewPage==0);if(ImGui.SmallButton("Previous page"))organizePreviewPage--;ImGui.EndDisabled();ImGui.SameLine();ImGui.TextUnformatted($"{organizePreviewPage+1}/{pages}");ImGui.SameLine();ImGui.BeginDisabled(organizePreviewPage+1>=pages);if(ImGui.SmallButton("Next page"))organizePreviewPage++;ImGui.EndDisabled();}
        if(ImGui.BeginChild("OrganizerPreview",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-10)),true)){
            foreach(var row in organizePreview.Skip(organizePreviewPage*40).Take(40)){
                DrawRetrievalIcon(row.Item);ImGui.TextUnformatted($"{retrievalCatalog?.Names.GetValueOrDefault(row.Item)??row.Item.ToString()}{(row.Hq?" HQ":"")} × {row.Quantity:N0}");
                ImGui.TextDisabled(row.Merge?$"Merge stacks · {organizePreviewNames.GetValueOrDefault(row.From,"Retainer")}":$"{organizePreviewNames.GetValueOrDefault(row.From,"Retainer")} → {organizePreviewNames.GetValueOrDefault(row.To,"Retainer")}");
            }
        }ImGui.EndChild();
    }
}

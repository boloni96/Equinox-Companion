namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<Dictionary<uint,string>>? ownedItemNamesTask;
    private Dictionary<uint,string>? ownedItemNames;
    private Task<ItemSearchIndex>? ownedItemIndexTask;
    private ItemSearchIndex? ownedItemIndex;
    private Task<(ItemSearchIndex Index,string Query,uint? Exact,(SharedItemLocation Location,StorageStack Stack)[] Rows)>? ownedItemQueryTask;
    private DateTimeOffset ownedItemQueryAfter,ownedItemIndexAfter;
    private string ownedItemRequestedQuery="";
    private uint? ownedItemRequestedExact;
    private int itemSearchPage;
    private string itemSearchLastDiscovery="";
    private readonly UiProjectionCache<(SharedItemLocation Location,StorageStack Stack)[]> itemSearchFiltered=new(1000);
    private readonly UiProjectionCache<HashSet<string>> itemSearchOwners=new(0);
    private string ownedItemSearchStatus="";
    private void BuildItemSearch()
    {
        var now=DateTimeOffset.UtcNow;var query=itemSearchQuery.Trim();
        if(query!=ownedItemRequestedQuery||itemExactId!=ownedItemRequestedExact){ownedItemRequestedQuery=query;ownedItemRequestedExact=itemExactId;ownedItemQueryAfter=now.AddMilliseconds(180);itemSearchMatches=[];itemSearchPage=0;itemSearchBuiltQuery="";}
        if(ownedItemNames==null&&ownedItemNamesTask==null){
            var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
            ownedItemNamesTask=Task.Run(()=>sheet.Where(x=>!x.Name.IsEmpty).ToDictionary(x=>x.RowId,x=>x.Name.ToString()));
        }
        if(ownedItemNamesTask?.IsCompleted==true){try{ownedItemNames=ownedItemNamesTask.GetAwaiter().GetResult();}catch(Exception e){ownedItemSearchStatus="Item names unavailable: "+e.Message;return;}finally{ownedItemNamesTask=null;}}
        if(ownedItemIndexTask?.IsCompleted==true){
            try{ownedItemIndex=ownedItemIndexTask.GetAwaiter().GetResult();itemSearchLocationCount=ownedItemIndex.LocationCount;itemSearchBuiltQuery="";ownedItemSearchStatus="";}
            catch(Exception e){ownedItemSearchStatus="Inventory index unavailable: "+e.Message;}
            ownedItemIndexTask=null;
        }
        if(ownedItemQueryTask?.IsCompleted==true){
            try{var result=ownedItemQueryTask.GetAwaiter().GetResult();if(result.Index==ownedItemIndex&&result.Query==query&&result.Exact==itemExactId){itemSearchMatches=result.Rows;itemSearchBuiltQuery=query;itemSearchPage=0;}}
            catch(Exception e){ownedItemSearchStatus="Search unavailable: "+e.Message;}
            ownedItemQueryTask=null;
        }
        var lastDiscovery=config.Discoveries.Count==0?"":config.Discoveries[^1].Id;
        if(ownedItemNames!=null&&ownedItemIndexTask==null&&now>=ownedItemIndexAfter&&(itemSearchRoster!=config.SharedRoster||itemSearchDiscoveryCount!=config.Discoveries.Count||itemSearchLastDiscovery!=lastDiscovery)){
            itemSearchRoster=config.SharedRoster;itemSearchDiscoveryCount=config.Discoveries.Count;itemSearchLastDiscovery=lastDiscovery;
            var roster=itemSearchRoster;var events=config.Discoveries.ToArray();var names=ownedItemNames;
            ownedItemIndexAfter=now.AddSeconds(1);ownedItemIndexTask=Task.Run(()=>ItemSearchIndex.Build(roster,events,names));
        }
        if(query.Length>0&&ownedItemIndex!=null&&ownedItemQueryTask==null&&itemSearchBuiltQuery!=query&&now>=ownedItemQueryAfter){
            var index=ownedItemIndex;var exact=itemExactId;ownedItemQueryTask=Task.Run(()=>(index,query,exact,index.Search(query,exact)));
        }
    }
}

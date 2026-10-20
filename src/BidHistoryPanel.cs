using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed class OlderBidHistory
    {
        public readonly Dictionary<string,SharedHousingBid> Bids=new();
        public string? Cursor;public bool Started;public int Total;public bool Expired;
    }
    private readonly Dictionary<string,OlderBidHistory> olderBidHistory=new();
    private string bidHistoryCharacter="",bidHistoryKey="",bidHistoryTaskKey="",bidHistoryTaskCharacter="",bidHistoryLoadStatus="";
    private Task<BidHistoryResult>? bidHistoryTask;
    private bool bidHistorySkipRecent;
    private static string HistoryBidKey(SharedHousingBid b)=>BidHistoryPolicy.Key(b);
    private void PollBidHistory()
    {
        if(bidHistoryKey!=config.PairingKey){bidHistoryKey=config.PairingKey;olderBidHistory.Clear();bidHistoryCharacter="";bidHistoryLoadStatus="";bidLastRoster=null;}
        if(bidHistoryTask?.IsCompleted!=true)return;
        var result=bidHistoryTask.IsCompletedSuccessfully?bidHistoryTask.Result:new BidHistoryResult(null,"History unavailable; try again.");
        if(bidHistoryTask.IsFaulted)_=bidHistoryTask.Exception;bidHistoryTask=null;
        if(bidHistoryTaskKey!=config.PairingKey)return;
        if(result.Page is {} page){
            if(!olderBidHistory.TryGetValue(page.CharacterId,out var state))olderBidHistory[page.CharacterId]=state=new();
            foreach(var bid in page.Bids)state.Bids[HistoryBidKey(bid)]=bid;
            state.Cursor=page.NextCursor;state.Started=true;state.Total=page.Total;state.Expired=false;bidLastRoster=null;
            bidHistoryLoadStatus=$"{state.Bids.Count} of {page.Total} server records loaded";
            // The first page usually overlaps the recent roster; one additional page makes "older" useful immediately.
            if(bidHistorySkipRecent&&!string.IsNullOrEmpty(state.Cursor)&&syncTask is null&&readySyncRecords==0){bidHistorySkipRecent=false;StartOlderBidPage(page.CharacterId,false);}
            else bidHistorySkipRecent=false;
        }else{
            bidHistoryLoadStatus=result.Error;
            if(result.Expired&&olderBidHistory.TryGetValue(bidHistoryTaskCharacter,out var state))state.Expired=true;
        }
    }
    private void StartOlderBidPage(string characterId,bool skipRecent)
    {
        if(bidHistoryTask is not null||syncTask is not null||readySyncRecords>0||config.PairingKey.Length!=64)return;
        olderBidHistory.TryGetValue(characterId,out var state);
        bidHistoryTaskKey=config.PairingKey;bidHistoryTaskCharacter=characterId;bidHistorySkipRecent=skipRecent;
        bidHistoryTask=sync.ReadBidHistory(bidHistoryTaskKey,characterId,state?.Cursor);bidHistoryLoadStatus="Loading history…";
    }
    private void DrawOlderBidHistoryControls()
    {
        PollBidHistory();
        var characters=(config.SharedRoster?.People??[]).Where(p=>bidHistoryPerson==""||p.Id==bidHistoryPerson).SelectMany(p=>p.Characters.Where(c=>bidHistoryAccount==""||p.Id+":"+c.AccountId==bidHistoryAccount).Select(c=>(Person:p,Character:c))).Where(x=>x.Character.HousingBidsTotal>0||x.Character.HousingBids?.Length>0).ToArray();
        if(characters.Length==0)return;
        if(!characters.Any(x=>x.Character.Id==bidHistoryCharacter))bidHistoryCharacter=characters.FirstOrDefault(x=>x.Character.HousingBidsTotal>40).Character?.Id??characters[0].Character.Id;
        var selected=characters.First(x=>x.Character.Id==bidHistoryCharacter);
        ImGui.SetNextItemWidth(220);
        if(ImGui.BeginCombo("##OlderHistoryCharacter",selected.Character.Name+" · "+selected.Character.World)){
            foreach(var x in characters)if(ImGui.Selectable(x.Character.Name+" · "+x.Character.World+"##"+x.Character.Id,bidHistoryCharacter==x.Character.Id)){bidHistoryCharacter=x.Character.Id;bidHistoryLoadStatus="";}
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Choose whose older bids to load. Loaded bids join the existing Person, account and result filters.");
        olderBidHistory.TryGetValue(bidHistoryCharacter,out var state);
        var complete=state?.Started==true&&string.IsNullOrEmpty(state.Cursor)&&!state.Expired;
        var canRead=(config.SharedRoster?.ProtocolVersion??0)>=24&&config.SyncEnabled;
        ImGui.SameLine();ImGui.BeginDisabled(!canRead||bidHistoryTask is not null||syncTask is not null||readySyncRecords>0||complete||state?.Expired==true);
        if(ImGui.SmallButton(complete?"All history loaded":"Load older history"))StartOlderBidPage(bidHistoryCharacter,state?.Started!=true);
        ImGui.EndDisabled();
        if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(!canRead?"Requires the matching Journal server update.":syncTask is not null||readySyncRecords>0?"Gameplay uploads have priority. Try when the upload finishes.":"Loads up to 40 older records at a time; current records remain visible.");
        ImGui.SameLine();ImGui.BeginDisabled(!canRead||bidHistoryTask is not null||syncTask is not null||readySyncRecords>0);if(ImGui.SmallButton("Refresh history")){olderBidHistory.Remove(bidHistoryCharacter);bidLastRoster=null;StartOlderBidPage(bidHistoryCharacter,true);}ImGui.EndDisabled();
        ImGui.SameLine();ImGui.TextDisabled(bidHistoryTask is not null?"Loading…":state is not null?$"{state.Bids.Count}/{state.Total} loaded":$"{selected.Character.HousingBids?.Length??0}/{selected.Character.HousingBidsTotal} recent");
        if(ImGui.IsItemHovered()&&bidHistoryLoadStatus.Length>0)ImGui.SetTooltip(bidHistoryLoadStatus);
        if(state?.Expired==true)ImGui.TextColored(Orange,"History snapshot expired; refresh to continue.");
    }
}

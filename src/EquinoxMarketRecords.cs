using Dalamud.Bindings.ImGui;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private void DrawMarketIncome()
    {
        var rows=FilteredMarketIncome();
        var totals=marketIncomeTotals.Get(rows,"",Environment.TickCount64,()=>(rows.Sum(x=>x.Net??0),rows.Any(x=>x.Net==null)));
        ImGui.TextDisabled($"{rows.Length} sales · {totals.Net:N0} gil confirmed net");
        MarketHelp(marketSharedStatus+"\n"+marketHistoryStatus+"\nThe game supplies up to 20 recent entries per retainer; previously recorded sales stay saved. Net is revenue after fees, not profit. Imported/inferred records without confirmed net are excluded from this total.");
        if(rows.Length==0){ImGui.TextDisabled("No sales match these filters.");return;}
        MarketPaging(rows.Length);
        if(!BeginMarketRecords("marketSales",4))return;
        ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,2.8f);
        ImGui.TableSetupColumn("Retainer",ImGuiTableColumnFlags.WidthStretch,1.7f);
        ImGui.TableSetupColumn("Qty",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*2.5f);
        ImGui.TableSetupColumn("Net gil",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*6);
        ImGui.TableHeadersRow();
        foreach(var x in rows.Skip(marketPage*80).Take(80)){
            ImGui.PushID(x.CharacterId+":"+x.Id);ImGui.TableNextRow();ImGui.TableNextColumn();
            var name=x.Item.Length>0?x.Item:SearchItemName(x.ItemId);
            var details=$"{DisplayTime.Local(x.At)}\n{x.Evidence}";
            MarketItemName(x.ItemId,name,x.Hq,details);
            ImGui.TableNextColumn();MarketRetainerCell(x.RetainerId,x.Retainer,x.Character,x.World);
            ImGui.TableNextColumn();ImGui.TextUnformatted(x.Quantity==0?"—":x.Quantity.ToString("N0"));MarketHelp(x.Quantity==0?"Quantity was not observed.":"Units sold in this transaction.");
            ImGui.TableNextColumn();MarketNumber("net",x.Net,(x.Net.HasValue?"Confirmed gil received after fees.":"Net gil was not confirmed.")+(x.Gross is {} gross?$"\nRecorded gross: {gross:N0} gil":"")+"\n"+details);
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawMarketSpending()
    {
        var cutoff=MarketCutoff;
        var rows=marketExpenseView.Get(MarketHistoryLedger.Expenses,MarketViewStamp,Environment.TickCount64,()=>(MarketHistoryLedger.Expenses??[]).Where(x=>x.At>=cutoff&&MatchesMarketItem(x.ItemId,SearchItemName(x.ItemId))&&(marketCharacter.Length==0||x.CharacterId.ToString()==marketCharacter)&&(marketWorldFilter.Length==0||x.World==marketWorldFilter)&&MatchesMarketOwner(x.CharacterId)).OrderByDescending(x=>x.At).ToArray());
        var spent=marketExpenseTotal.Get(rows,"",Environment.TickCount64,()=>rows.Sum(x=>x.Price*x.Quantity+x.Tax));
        ImGui.TextDisabled($"{rows.Length} purchases · {spent:N0} gil spent");
        MarketHelp("Confirmed market purchases, including recorded tax. Character purchases are not attributed to a retainer. Historical, vendor and gathered costs may be missing; these costs are not assigned automatically to crafts or resale stock.");
        if(rows.Length==0){ImGui.TextDisabled("No purchases match these filters.");return;}
        MarketPaging(rows.Length);
        if(!BeginMarketRecords("marketSpending",4))return;
        ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,2.8f);
        ImGui.TableSetupColumn("Character",ImGuiTableColumnFlags.WidthStretch,1.7f);
        ImGui.TableSetupColumn("Qty",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*2.5f);
        ImGui.TableSetupColumn("Total gil",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*6);
        ImGui.TableHeadersRow();
        foreach(var x in rows.Skip(marketPage*80).Take(80)){
            ImGui.PushID(x.CharacterId+":"+x.Id);ImGui.TableNextRow();ImGui.TableNextColumn();
            var details=$"{DisplayTime.Local(x.At)}\n{x.Quantity:N0} × {x.Price:N0} gil + {x.Tax:N0} gil tax";
            MarketItemName(x.ItemId,SearchItemName(x.ItemId),x.Hq,details);
            ImGui.TableNextColumn();if(ImGui.Selectable(x.Character+"##character"))ImGui.SetClipboardText(x.Character);
            var owner=MarketRetainers.FirstOrDefault(r=>r.CharacterId==x.CharacterId);
            MarketHelp(MarketIdentity(owner,x.Character,x.World)+"\nPurchased on "+x.World+"\nClick to copy the character name.");
            ImGui.TableNextColumn();ImGui.TextUnformatted(x.Quantity.ToString("N0"));
            ImGui.TableNextColumn();MarketNumber("total",x.Price*x.Quantity+x.Tax,details);
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawMarketChanges()
    {
        var cutoff=MarketCutoff;
        var rows=marketChangesView.Get(MarketHistoryLedger.Prices,MarketViewStamp,Environment.TickCount64,()=>MarketHistoryLedger.Prices.Reverse().Where(x=>x.At>=cutoff&&MatchesMarketItem(x.ItemId,x.Item)&&(MarketRetainerById(x.RetainerId) is {} r&&MatchesMarket(r))).ToArray());
        ImGui.TextDisabled($"{rows.Length} price checks · newest first");
        MarketHelp("Updated means the game confirmed a price change. Suggestions, skipped checks and kept prices are separate outcomes. Hover a result for its reason. Up to 2,000 retained checks are included in the full market backup.");
        if(rows.Length==0){ImGui.TextDisabled("No price checks match these filters.");return;}
        MarketPaging(rows.Length);
        var wide=ImGui.GetContentRegionAvail().X>=ImGui.GetFontSize()*38;
        if(!BeginMarketRecords("marketChanges"+(wide?"Wide":"Compact"),wide?5:4))return;
        ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,2.8f);
        ImGui.TableSetupColumn("Retainer",ImGuiTableColumnFlags.WidthStretch,1.7f);
        if(wide)ImGui.TableSetupColumn("Before",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*6);
        ImGui.TableSetupColumn("Price",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*6);
        ImGui.TableSetupColumn("Result",ImGuiTableColumnFlags.WidthFixed,ImGui.GetFontSize()*6);
        ImGui.TableHeadersRow();
        var index=marketPage*80;
        foreach(var x in rows.Skip(index).Take(80)){
            ImGui.PushID(index++);ImGui.TableNextRow();ImGui.TableNextColumn();
            var updated=x.Result=="Updated";
            var suggested=!updated&&x.Result!="Competitive"&&x.After.HasValue;
            var details=$"{DisplayTime.Local(x.At)}\nBefore: {x.Before:N0} gil"+(x.After is {} price&&x.Result!="Competitive"?$"\n{(updated?"Confirmed new price":"Suggested price")}: {price:N0} gil":"\nExisting price kept.")+"\n"+x.Reason;
            MarketItemName(x.ItemId,x.Item.Length>0?x.Item:SearchItemName(x.ItemId),x.Hq,details);
            ImGui.TableNextColumn();MarketRetainerCell(x.RetainerId,x.Retainer,"","");
            if(wide){ImGui.TableNextColumn();MarketNumber("before",x.Before,details);}
            ImGui.TableNextColumn();MarketNumber("price",x.After??x.Before,details);
            var label=updated?"Updated":suggested?"Suggested":x.Result=="Competitive"?"Kept":x.Result;
            var color=SellerColor(updated||x.Result=="Competitive"?SellerStatus.Competitive:suggested?SellerStatus.Update:SellerStatus.Skipped);
            ImGui.TableNextColumn();ImGui.TextColored(color,label);MarketHelp(details);
            ImGui.PopID();
        }
        ImGui.EndTable();
    }
}

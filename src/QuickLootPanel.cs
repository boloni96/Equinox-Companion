using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private bool quickLootSelectTab;
    private bool quickLootUiChanged;
    private string quickLootRuleSearch="",quickLootRuleQuery="",quickLootTransfer="",quickLootUiMessage="";
    private bool quickLootSearchDuties;
    private (uint Id,string Name)[] quickLootSearchResults=[];
    private int quickLootPreviewId;
    private string quickLootPreviewResult="Choose a current loot entry, or enter an item ID.";
    private DateTimeOffset quickLootPreviewAt;
    private List<QuickLootOffer> quickLootPreviewOffers=[];
    private void QCheck(string label,bool value,Action<bool> set)
    {if(ImGui.Checkbox(label,ref value)){set(value);quickLootUiChanged=true;}}
    private void QInt(string label,int value,int max,Action<int> set)
    {if(ImGui.InputInt(label,ref value)){set(Math.Clamp(value,0,max));quickLootUiChanged=true;}}
    private void QRoll(string label,QuickLootRoll value,Action<QuickLootRoll> set,bool filter=false)
    {
        if(!ImGui.BeginCombo(label,value.ToString()))return;
        foreach(var option in Enum.GetValues<QuickLootRoll>())
        {if(filter&&option is not (QuickLootRoll.Greed or QuickLootRoll.Pass))continue;if(ImGui.Selectable(option==QuickLootRoll.Nothing?"Do nothing":option.ToString(),option==value)){set(option);quickLootUiChanged=true;}}
        ImGui.EndCombo();
    }
    private static void QTabs(string id,params (string Name,System.Action Draw)[] tabs)
    {
        if(!ImGui.BeginTabBar(id))return;
        foreach(var tab in tabs)if(ImGui.BeginTabItem(tab.Name)){tab.Draw();ImGui.EndTabItem();}
        ImGui.EndTabBar();
    }
    private void DrawQuickLoot()
    {
        var s=CurrentQuickLoot;quickLootUiChanged=false;
        QCheck("Automatic rolling (FULF)",s.Automatic,v=>{s.Automatic=v;StopQuickLoot(v?"Automatic rolling enabled.":"Automatic rolling disabled.");});
        ImGui.SameLine();QCheck("Top bar (DTR)",s.ShowBar,v=>s.ShowBar=v);
        ImGui.TextWrapped("One configuration for all characters. Unlocks and equipped gear are checked on the character currently logged in.");
        if(QuickLootConflict())ImGui.TextWrapped("LazyLoot is loaded. QuickLoot rolling is paused to avoid two plugins rolling together.");
        ImGui.Separator();
        QTabs("QuickLootSections",
            ("Rolling",()=>QTabs("QuickLootRolling",("Current loot",DrawQuickLootCurrent),("Mode & timing",DrawQuickLootTiming))),
            ("Filters",()=>QTabs("QuickLootFilters",("Collections",DrawQuickLootCollections),("Equipment",DrawQuickLootGear),("Protection",DrawQuickLootProtection))),
            ("Rules",()=>QTabs("QuickLootRules",("Items",()=>DrawQuickLootRules(false)),("Duties",()=>DrawQuickLootRules(true)))),
            ("Feedback",()=>QTabs("QuickLootFeedback",("Messages",DrawQuickLootMessages),("Preview",DrawQuickLootPreview),("History",DrawQuickLootHistory))),
            ("About",DrawQuickLootAbout));
        ImGui.Separator();ImGui.TextWrapped(quickLootStatus);
        if(quickLootUiMessage.Length>0)ImGui.TextWrapped(quickLootUiMessage);
        if(quickLootUiChanged){SaveQuickLoot(s);quickLootUiMessage="Settings saved.";}
    }
    private void RefreshQuickLootPreview()
    {
        if(DateTimeOffset.UtcNow<quickLootPreviewAt)return;
        quickLootPreviewAt=DateTimeOffset.UtcNow.AddSeconds(1);
        try{quickLootPreviewOffers=ReadQuickLootOffers();}catch(Exception e){quickLootPreviewOffers=[];quickLootUiMessage="Loot preview unavailable: "+e.GetType().Name;}
    }
    private void DrawQuickLootCurrent()
    {
        ImGui.TextWrapped("One-time buttons apply your rules to loot currently available. Later drops are not added to that one-time queue.");
        foreach(var r in new[]{QuickLootRoll.Need,QuickLootRoll.Greed,QuickLootRoll.Pass})
        {if(ImGui.Button(r+" current loot"))QueueQuickLoot(r);ImGui.SameLine();}
        if(ImGui.Button("Stop")){CurrentQuickLoot.Automatic=false;StopQuickLoot("Rolling stopped.");quickLootUiChanged=true;}
        RefreshQuickLootPreview();
        if(quickLootPreviewOffers.Count==0)ImGui.TextWrapped("No eligible loot is waiting for a roll.");
        if(ImGui.BeginChild("QuickLootCurrentList",new Vector2(0,220),true))
        foreach(var offer in quickLootPreviewOffers)
        {var d=QuickLootPolicy.Decide(CurrentQuickLoot,offer.Facts,CurrentQuickLoot.Mode);ImGui.TextWrapped($"{offer.Name} · {d.Roll}\n{d.Reason}");ImGui.Separator();}
        ImGui.EndChild();
    }
    private void DrawQuickLootTiming()
    {
        var s=CurrentQuickLoot;QRoll("Automatic mode",s.Mode,v=>s.Mode=v);
        ImGui.TextWrapped("Need falls back to Greed, then Pass, when the game does not allow the stronger roll. Rules and weekly protection still apply.");
        void Delay(string name,float min,float max,Action<float,float> set)
        {if(ImGui.DragFloatRange2(name,ref min,ref max,.1f,.5f,60,"%.1f s","%.1f s")){min=Math.Clamp(min,.5f,60);max=Math.Clamp(max,min,60);set(min,max);quickLootUiChanged=true;}}
        Delay("One-time delay",s.ManualMin,s.ManualMax,(a,b)=>{s.ManualMin=a;s.ManualMax=b;});
        Delay("Automatic delay",s.AutoMin,s.AutoMax,(a,b)=>{s.AutoMin=a;s.AutoMax=b;});
        ImGui.TextWrapped("Delays apply before the first roll and between subsequent rolls. Changing character or area cancels the old queue.");
    }
    private void DrawQuickLootCollections()
    {
        ImGui.TextWrapped("Pass only when the current character has unlocked the collectible. Unknown unlock states are kept. Faded copies are skipped only if every identified orchestrion result is unlocked.");
        foreach(var name in QuickLootSettings.Categories)
        {
            var s=CurrentQuickLoot;s.Collections.TryGetValue(name,out var stored);var c=stored??new QuickLootCollectionFilter();
            ImGui.PushID(name);
            QCheck(name,c.Enabled,v=>{c.Enabled=v;s.Collections[name]=c;});ImGui.SameLine();
            QCheck("Untradeable only",c.UntradeableOnly,v=>{c.UntradeableOnly=v;s.Collections[name]=c;});ImGui.PopID();
        }
    }
    private void DrawQuickLootGear()
    {
        var s=CurrentQuickLoot;
        QCheck("Keep level-1 glamour despite global gear filters",s.KeepGlamour,v=>s.KeepGlamour=v);
        QCheck("Pass gear for other jobs",s.OtherJobs,v=>s.OtherJobs=v);
        QCheck("Minimum item level",s.MinimumLevel,v=>s.MinimumLevel=v);if(s.MinimumLevel)QInt("Item level",s.Level,9999,v=>s.Level=v);
        QCheck("Below current job average",s.BelowAverage,v=>s.BelowAverage=v);
        if(s.BelowAverage){QInt("Allowed item-level difference",s.BelowAverageBy,9999,v=>s.BelowAverageBy=v);QRoll("Below-average action",s.BelowAverageRoll,v=>s.BelowAverageRoll=v,true);}
        QCheck("Not an item-level upgrade",s.NotUpgrade,v=>s.NotUpgrade=v);if(s.NotUpgrade)QRoll("Non-upgrade action",s.NotUpgradeRoll,v=>s.NotUpgradeRoll=v,true);
        ImGui.TextWrapped("Upgrade checks compare equipped items in the matching slot; rings use the lower equipped item level. Item level alone does not evaluate secondary stats.");
        QCheck("Minimum Grand Company seals",s.MinimumSeals,v=>s.MinimumSeals=v);if(s.MinimumSeals)QInt("Expert-delivery seals",s.Seals,100000,v=>s.Seals=v);
    }
    private void DrawQuickLootProtection()
    {
        var s=CurrentQuickLoot;
        QCheck("Protect weekly-limited loot",s.ProtectWeekly,v=>s.ProtectWeekly=v);
        ImGui.TextWrapped("While protected, weekly-limited entries are left for manual rolling in the game's loot window, including when an item or duty override exists.");
        QCheck("Never automatically pass after a failed roll",s.PreventFailurePass,v=>s.PreventFailurePass=v);
        ImGui.TextWrapped("Enabled by default. If disabled, an unacknowledged Need/Greed may be followed by one Pass attempt on that same non-weekly entry. No endless retries.");
    }
    private void DrawQuickLootRules(bool duties)
    {
        var s=CurrentQuickLoot;var rules=duties?s.Duties:s.Items;
        ImGui.TextWrapped("Precedence: weekly protection → item override → duty override → global filters. Do nothing leaves the entry for you.");
        if(quickLootSearchDuties!=duties){quickLootSearchDuties=duties;quickLootRuleQuery="";quickLootSearchResults=[];}
        ImGui.InputText("Search name or ID",ref quickLootRuleSearch,100);
        if(quickLootRuleQuery!=quickLootRuleSearch)
        {
            quickLootRuleQuery=quickLootRuleSearch;var q=quickLootRuleSearch.Trim();uint.TryParse(q,out var id);
            quickLootSearchResults=q.Length==0?[]:duties?DataManager.GetExcelSheet<ContentFinderCondition>().Where(x=>x.RowId==id||q.Length>=2&&x.Name.ToString().Contains(q,StringComparison.OrdinalIgnoreCase)).Take(60).Select(x=>(x.RowId,x.Name.ToString())).ToArray():DataManager.GetExcelSheet<Item>().Where(x=>x.RowId==id||q.Length>=2&&x.Name.ToString().Contains(q,StringComparison.OrdinalIgnoreCase)).Take(60).Select(x=>(x.RowId,x.Name.ToString())).ToArray();
        }
        if(ImGui.BeginChild("QuickLootSearch"+duties,new Vector2(0,110),true))foreach(var row in quickLootSearchResults)
        {if(ImGui.Selectable($"Add {row.Name} ({row.Id})")){if(!rules.Any(r=>r.Id==row.Id)){rules.Add(new(){Id=row.Id});quickLootUiChanged=true;}else quickLootUiMessage="This rule already exists.";}}
        ImGui.EndChild();
        if(ImGui.BeginChild("QuickLootSavedRules"+duties,new Vector2(0,200),true))
        foreach(var rule in rules.ToArray())
        {
            ImGui.PushID(rule.Id.ToString());var name=duties?DataManager.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(rule.Id)?.Name.ToString():DataManager.GetExcelSheet<Item>().GetRowOrDefault(rule.Id)?.Name.ToString();
            QCheck($"{name??"Unknown"} ({rule.Id})",rule.Enabled,v=>rule.Enabled=v);QRoll("Action",rule.Roll,v=>rule.Roll=v);ImGui.SameLine();
            if(ImGui.SmallButton("Remove")){rules.Remove(rule);quickLootUiChanged=true;}ImGui.Separator();ImGui.PopID();
        }
        ImGui.EndChild();
        if(ImGui.Button("Copy rules")){ImGui.SetClipboardText(QuickLootPolicy.ExportRules(rules));quickLootUiMessage="Rules copied.";}
        ImGui.SameLine();if(ImGui.Button("Read clipboard")){quickLootTransfer=ImGui.GetClipboardText();quickLootUiMessage="Review pasted rules below, then Import / replace.";}
        if(ImGui.CollapsingHeader("Import / export JSON"))
        {
            ImGui.InputTextMultiline("##QuickLootTransfer",ref quickLootTransfer,256001,new Vector2(0,100));
            if(ImGui.Button("Import / replace this rule list"))try
            {
                var rows=QuickLootPolicy.ImportRules(quickLootTransfer);
                if(rows.Any(r=>duties?DataManager.GetExcelSheet<ContentFinderCondition>().GetRowOrDefault(r.Id) is null:DataManager.GetExcelSheet<Item>().GetRowOrDefault(r.Id) is null))throw new FormatException("One or more IDs are absent from the current game data.");
                if(duties)s.Duties=rows;else s.Items=rows;quickLootUiChanged=true;
            }
            catch(Exception e) when(e is JsonException or FormatException){quickLootUiMessage="Import failed: "+e.Message;}
        }
    }
    private void DrawQuickLootMessages()
    {
        var s=CurrentQuickLoot;
        QCheck("Chat results",s.Chat,v=>s.Chat=v);QCheck("Normal toasts",s.NormalToast,v=>s.NormalToast=v);
        QCheck("Quest toasts",s.QuestToast,v=>s.QuestToast=v);QCheck("Error toasts",s.ErrorToast,v=>s.ErrorToast=v);
        QCheck("Record decision reasons in history",s.Diagnostics,v=>s.Diagnostics=v);
    }
    private unsafe void DrawQuickLootPreview()
    {
        ImGui.TextWrapped("Preview makes no roll. Current-loot preview uses the game's actual permissions; an ID preview assumes Need/Greed are permitted and no weekly restriction.");
        ImGui.InputInt("Item ID",ref quickLootPreviewId);
        if(ImGui.Button("Explain item"))
        {
            if(!Player.IsLoaded||quickLootPreviewId<=0)quickLootPreviewResult="Log in and enter a positive item ID.";
            else {var game=FFXIVClientStructs.FFXIV.Client.Game.GameMain.Instance();var facts=QuickLootReadFacts((uint)quickLootPreviewId,game==null?0u:game->CurrentContentFinderConditionId,true,true,false);var d=QuickLootPolicy.Decide(CurrentQuickLoot,facts,CurrentQuickLoot.Mode);quickLootPreviewResult=$"{d.Roll} · {d.Reason}";}
        }
        ImGui.TextWrapped(quickLootPreviewResult);
    }
    private void DrawQuickLootHistory()
    {
        if(ImGui.Button("Clear session history"))quickLootHistory.Clear();
        if(ImGui.BeginChild("QuickLootHistory",new Vector2(0,260),true))foreach(var row in quickLootHistory.Reverse())ImGui.TextWrapped(row);
        ImGui.EndChild();
    }
    private static void DrawQuickLootAbout()
    {
        ImGui.TextWrapped("QuickLoot · Equinox Companion\n\nThank you to 53m1k0l0n, Gidedin, the LazyLoot contributors and PunishXIV for their work on configurable loot rolling, and for publishing their source and ideas. LazyLoot inspired this feature and provided a reference for the native rolling interface.\n\nQuickLoot's settings, decision engine and interface are implemented for Companion. This is an independent integration, not an official PunishXIV release or endorsement.");
        if(ImGui.Button("LazyLoot project & contributors"))Dalamud.Utility.Util.OpenLink("https://github.com/PunishXIV/LazyLoot");
        ImGui.TextWrapped("LazyLoot is published under GPL-3.0. Companion does not require LazyLoot to be installed. Automatic rolling stays paused if LazyLoot is loaded, to prevent conflicting rolls.");
    }
}

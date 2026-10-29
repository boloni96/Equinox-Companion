using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private GilTraderWindow gilTraderWindow = null!;
    private bool gilTraderCommand, gilReceive;
    private int gilTargetSource = 1;
    private string gilNameSearch = "";
    private int gilTotal = 1_000_000;
    private sealed class GilTraderWindow(Plugin p) : Window("Gil & Item Trader · Equinox###EquinoxGilTrader")
    {
        public override void OnOpen()
        {
            Size ??= new(460, 500); SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new() { MinimumSize = new(360, 300), MaximumSize = new(float.MaxValue) };
            RespectCloseHotkey = false; AllowPinning = true;
        }
        public override void Draw() => p.DrawWindowWithLauncher(p.DrawGilTrader);
        public override void OnClose()
        {
            if (!p.minimizedLaunchers.Contains("Gil Trader")) p.StopGilTrade("Gil & Item Trader closed.");
        }
        public override void PostDraw() => HandleNativeCollapse(this, () => p.MinimizeLauncher("Gil Trader"));
    }
    private void InitializeGilTrader()
    {
        gilTraderWindow = new(this); windows.AddWindow(gilTraderWindow);
        Chat.LogMessage += ObserveGilTradeCompletion;
        gilTraderCommand = Commands.AddHandler("/eqtrade", new CommandInfo((_, args) =>
        {
            if (args.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase)) StopGilTrade("Stopped by command.");
            else OpenGilTrader();
        }) { HelpMessage = "Gil & Item Trader: send or receive gil or selected bag items with a nearby player. /eqtrade stop stops the session." });
        if (!gilTraderCommand) Log.Warning("/eqtrade is already registered. Open Gil & Item Trader from the window launcher.");
    }
    private void DisposeGilTrader()
    {
        StopGilTrade("Companion unloaded.");
        Chat.LogMessage -= ObserveGilTradeCompletion;
        gilTradeCompletion.Clear();
        if (gilTraderCommand) Commands.RemoveHandler("/eqtrade");
    }
    private void OpenGilTrader() { if(!RequireOptionalTool("Gil Trader"))return; gilTraderWindow.IsOpen = true; minimizedLaunchers.Remove("Gil Trader"); }
    private void SetGilTraderEnabled(bool enabled)
    {
        config.EnableGilTrader = enabled;
        if (!enabled) StopGilTrade("Gil & Item Trader disabled.");
        HideDisabledOptionalWindows();
        SaveConfiguration();
    }
    private unsafe void DrawGilTrader()
    {
        if (!config.EnableGilTrader) return;
        ImGui.BeginDisabled(TraderRunning);
        var mode=gilItemsMode?1:0;ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##TraderMode",ref mode,"Gil\0Items\0")){gilItemsMode=mode==1;gilTradeNotice="";if(gilItemsMode)PrepareTradeItemShortcutLabels();}
        ImGui.SetNextItemWidth(-1);
        var sourceChoice=gilTargetSource==1?0:gilTargetSource==0?1:gilTargetSource;
        if(ImGui.Combo("##gil-target-source",ref sourceChoice,"Nearby players\0Party\0Friends\0Find by name\0"))gilTargetSource=sourceChoice==0?1:sourceChoice==1?0:sourceChoice;
        if (gilTargetSource == 3) { ImGui.SetNextItemWidth(-1); ImGui.InputTextWithHint("##gil-name-search", "Player name", ref gilNameSearch, 80); }
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##gil-partner", gilPartnerName.Length == 0 ? "Choose player" : gilPartnerName + " @ " + WorldName(gilPartnerWorld)))
        {
            var any = false;
            foreach (var member in GilTradeCandidates(gilTargetSource, gilNameSearch))
            {
                any = true;
                if (ImGui.Selectable(member.Name + " @ " + WorldName(member.World) + "##gil-target-" + member.World + ":" + member.Name,
                    gilPartnerName == member.Name && gilPartnerWorld == member.World))
                {
                    gilPartnerContent = member.ContentId; gilPartnerName = member.Name; gilPartnerWorld = member.World; gilPartnerRequiresParty = member.Party;
                    gilTradeNotice = "";
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Home world: " + WorldName(member.World) + "\nStand beside this player to trade. They do not need Equinox.");
            }
            if (!any) ImGui.TextDisabled(gilTargetSource == 0 ? "No party members. Choose Nearby players instead." : gilTargetSource == 2 ? "No cached friends. Open the game's Friends List, or choose Nearby players." : "No nearby players match. Stand beside them first.");
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(gilPartnerName.Length == 0 ? "Choose a player and check their home world before starting." : $"{gilPartnerName} · {WorldName(gilPartnerWorld)}\nStand nearby. Only this player is allowed for this session.");
        if (ImGui.RadioButton("Send##gil-mode-send", !gilReceive)) gilReceive = false;
        ImGui.SameLine(); if (ImGui.RadioButton("Receive##gil-mode-receive", gilReceive)) gilReceive = true;
        if (gilReceive)
        {
            ImGui.TextDisabled(gilItemsMode?"Receive items only · selected player":"Receive gil only · selected player");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Start receiving before they start sending. Your side must contain no gil or items. Stops after 60 seconds without progress, moving away or changing area. Party selection also stops if they leave the party. This permission is never saved.");
        }
        else if(gilItemsMode) DrawItemTradePicker();
        else
        {
            ImGui.SetNextItemWidth(Math.Min(190, Math.Max(100, ImGui.GetContentRegionAvail().X - 80)));
            ImGui.InputInt("Total gil", ref gilTotal, 0, 0); gilTotal = Math.Clamp(gilTotal, 0, (int)GilTradeRun.WalletLimit);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The total to send, divided into trades of at most 1,000,000 gil. The final trade sends only the remainder. The amount is checked against your character’s wallet.");
            ImGui.TextDisabled($"{gilTotal:N0} gil · {(gilTotal + (long)GilTradeRun.TradeLimit - 1) / GilTradeRun.TradeLimit:N0} trades");
        }
        if (ImGui.Button(gilReceive ? "Start receiving###gil-start" : gilItemsMode ? "Send items###gil-start" : "Send gil###gil-start")) StartGilTrade();
        ImGui.EndDisabled();
        ImGui.SameLine(); ImGui.BeginDisabled(!TraderRunning);
        if (ImGui.Button("Stop")) StopGilTrade("Stopped. Confirmed transfers are kept.");
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stops further actions and cancels our pending trade. Items or gil already transferred remain with the recipient. /eqtrade stop also works while minimized.");
        if(gilItemsMode) ImGui.TextUnformatted($"{itemTrade.Done:N0} items · {itemTrade.Trades} completed trades");
        else if (gilTrade.Running || gilTrade.Done > 0)
        {
            if (gilTrade.Sending) ImGui.ProgressBar(gilTrade.Total == 0 ? 0 : (float)gilTrade.Done / gilTrade.Total, new(-1, 0), $"{gilTrade.Done:N0} / {gilTrade.Total:N0} gil");
            else ImGui.TextUnformatted($"Received {gilTrade.Done:N0} gil · {gilTrade.Trades} trades");
        }
        ImGui.TextWrapped(gilTradeNotice.Length > 0 ? gilTradeNotice : gilItemsMode ? itemTrade.Status : gilTrade.Status);
        if (ImGui.CollapsingHeader("Details")) DrawGilTradeDiagnostics();
    }
    private void DrawGilTradeDiagnostics()
    {
        ImGui.TextWrapped("Items mode sends selected quantities in batches of five stacks; the receiver uses Items + Receive. Gil mode keeps its existing behavior. Both people can use /eqtrade. The receiver starts Receive; the sender selects Send and a total. Manual acceptance on the other client also works. Minimizing keeps this session running; Stop or closing with X ends it.");
        if (ImGui.SmallButton("Export diagnostics##gil")) Export();
        ImGui.SameLine(); if (ImGui.SmallButton("Open exports##gil")) OpenDiagnosticFolder(Path.Combine(Pi.GetPluginConfigDirectory(), "exports"));
        foreach (var line in gilTradeDiagnostics) ImGui.TextWrapped(line);
        if (gilTradeDiagnostics.Count > 0 && ImGui.SmallButton("Copy trade log")) ImGui.SetClipboardText(string.Join("\n", gilTradeDiagnostics));
    }
}

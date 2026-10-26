using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private GilTraderWindow gilTraderWindow = null!;
    private bool gilTraderCommand, gilReceive;
    private int gilTargetSource;
    private string gilNameSearch = "";
    private int gilTotal = 1_000_000;
    private sealed class GilTraderWindow(Plugin p) : Window("Gil Trader · Equinox###EquinoxGilTrader")
    {
        public override void OnOpen()
        {
            Size ??= new(390, 315); SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new() { MinimumSize = new(300, 260), MaximumSize = new(float.MaxValue) };
            RespectCloseHotkey = false; AllowPinning = true;
        }
        public override void Draw() => p.DrawWindowWithLauncher(p.DrawGilTrader);
        public override void OnClose()
        {
            if (!p.minimizedLaunchers.Contains("Gil Trader")) p.StopGilTrade("Gil Trader closed.");
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
        }) { HelpMessage = "Gil Trader: send or receive gil with a selected nearby player. /eqtrade stop stops the session." });
        if (!gilTraderCommand) Log.Warning("/eqtrade is already registered. Open Gil Trader from the window launcher.");
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
        if (!enabled) StopGilTrade("Gil Trader disabled.");
        HideDisabledOptionalWindows();
        SaveConfiguration();
    }
    private unsafe void DrawGilTrader()
    {
        if (!config.EnableGilTrader) return;
        ImGui.BeginDisabled(gilTrade.Running);
        ImGui.SetNextItemWidth(-1);
        ImGui.Combo("##gil-target-source", ref gilTargetSource, "Party\0Nearby players\0Friends\0Find by name\0");
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
        if (ImGui.RadioButton("Send gil##gil-mode-send", !gilReceive)) gilReceive = false;
        ImGui.SameLine(); if (ImGui.RadioButton("Receive##gil-mode-receive", gilReceive)) gilReceive = true;
        if (gilReceive)
        {
            ImGui.TextDisabled("Receive only from the selected player.");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Start receiving before they start sending. Your side must contain no gil or items. Stops after 60 seconds without progress, moving away or changing area. Party selection also stops if they leave the party. This permission is never saved.");
        }
        else
        {
            ImGui.SetNextItemWidth(Math.Min(190, Math.Max(100, ImGui.GetContentRegionAvail().X - 80)));
            ImGui.InputInt("Total gil", ref gilTotal, 0, 0); gilTotal = Math.Clamp(gilTotal, 0, (int)GilTradeRun.WalletLimit);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The total to send, divided into trades of at most 1,000,000 gil. The final trade sends only the remainder. The amount is checked against your character’s wallet.");
            ImGui.TextDisabled($"{gilTotal:N0} gil · {(gilTotal + (long)GilTradeRun.TradeLimit - 1) / GilTradeRun.TradeLimit:N0} trades");
        }
        if (ImGui.Button(gilReceive ? "Start receiving###gil-start" : "Send gil###gil-start")) StartGilTrade();
        ImGui.EndDisabled();
        ImGui.SameLine(); ImGui.BeginDisabled(!gilTrade.Running);
        if (ImGui.Button("Stop")) StopGilTrade("Stopped. Confirmed transfers are kept.");
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stops further actions and cancels our pending trade. Gil already transferred cannot be taken back. /eqtrade stop also works while minimized.");
        if (gilTrade.Running || gilTrade.Done > 0)
        {
            if (gilTrade.Sending) ImGui.ProgressBar(gilTrade.Total == 0 ? 0 : (float)gilTrade.Done / gilTrade.Total, new(-1, 0), $"{gilTrade.Done:N0} / {gilTrade.Total:N0} gil");
            else ImGui.TextUnformatted($"Received {gilTrade.Done:N0} gil · {gilTrade.Trades} trades");
        }
        ImGui.TextWrapped(gilTradeNotice.Length > 0 ? gilTradeNotice : gilTrade.Status);
        if (ImGui.CollapsingHeader("Details")) DrawGilTradeDiagnostics();
    }
    private void DrawGilTradeDiagnostics()
    {
        ImGui.TextWrapped("Both people can use /eqtrade. The receiver starts Receive; the sender selects Send and a total. Manual acceptance on the other client also works. Minimizing keeps this session running; Stop or closing with X ends it.");
        if (ImGui.SmallButton("Export diagnostics##gil")) Export();
        ImGui.SameLine(); if (ImGui.SmallButton("Open exports##gil")) OpenDiagnosticFolder(Path.Combine(Pi.GetPluginConfigDirectory(), "exports"));
        foreach (var line in gilTradeDiagnostics) ImGui.TextWrapped(line);
        if (gilTradeDiagnostics.Count > 0 && ImGui.SmallButton("Copy trade log")) ImGui.SetClipboardText(string.Join("\n", gilTradeDiagnostics));
    }
}

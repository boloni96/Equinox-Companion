using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private GilTraderWindow gilTraderWindow = null!;
    private bool gilTraderCommand, gilReceive;
    private int gilTotal = 1_000_000;
    private sealed class GilTraderWindow(Plugin p) : Window("Gil Trader · Equinox###EquinoxGilTrader")
    {
        public override void OnOpen()
        {
            Size ??= new(390, 255); SizeCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new() { MinimumSize = new(300, 220), MaximumSize = new(float.MaxValue) };
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
        gilTraderCommand = Commands.AddHandler("/eqtrade", new CommandInfo((_, args) =>
        {
            if (args.Trim().Equals("stop", StringComparison.OrdinalIgnoreCase)) StopGilTrade("Stopped by command.");
            else OpenGilTrader();
        }) { HelpMessage = "Gil Trader: send or receive gil with a nearby party member. /eqtrade stop stops the session." });
        if (!gilTraderCommand) Log.Warning("/eqtrade is already registered. Open Gil Trader from the window launcher.");
    }
    private void DisposeGilTrader()
    {
        StopGilTrade("Companion unloaded.");
        if (gilTraderCommand) Commands.RemoveHandler("/eqtrade");
    }
    private void OpenGilTrader() { gilTraderWindow.IsOpen = true; minimizedLaunchers.Remove("Gil Trader"); }
    private void SetGilTraderEnabled(bool enabled)
    {
        config.EnableGilTrader = enabled;
        if (!enabled) StopGilTrade("Gil Trader disabled.");
        SaveConfiguration();
    }
    private unsafe void DrawGilTrader()
    {
        if (!config.EnableGilTrader)
        {
            ImGui.TextWrapped("Trade gil with someone in your party. Each session starts only when you choose Send or Receive.");
            if (ImGui.Button("Enable Gil Trader")) SetGilTraderEnabled(true);
            return;
        }
        ImGui.BeginDisabled(gilTrade.Running);
        ImGui.SetNextItemWidth(-1);
        if (ImGui.BeginCombo("##gil-partner", gilPartnerName.Length == 0 ? "Choose party member" : gilPartnerName))
        {
            var any = false;
            foreach (var member in FollowParty)
            {
                if (member.ContentId == 0 || member.ContentId == Player.ContentId) continue;
                any = true;
                if (ImGui.Selectable(member.Name.TextValue + "##" + member.ContentId, gilPartnerContent == member.ContentId))
                {
                    gilPartnerContent = member.ContentId; gilPartnerName = member.Name.TextValue; gilPartnerWorld = member.World.RowId;
                    gilTradeNotice = "";
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(WorldName(member.World.RowId) ?? "Home world unavailable");
            }
            if (!any) ImGui.TextDisabled("Invite them to your party first.");
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(gilPartnerName.Length == 0 ? "Invite them using the game's party menu, then select them here." : $"{gilPartnerName} · {WorldName(gilPartnerWorld)}\nStand nearby. Only this party member is allowed for this session.");
        if (ImGui.RadioButton("Send gil", !gilReceive)) gilReceive = false;
        ImGui.SameLine(); if (ImGui.RadioButton("Receive", gilReceive)) gilReceive = true;
        if (gilReceive)
        {
            ImGui.TextDisabled("Receive only from the selected party member.");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Start receiving before they start sending. Your side must contain no gil or items. Stops after 60 seconds without progress, leaving the party, or changing area. This permission is never saved.");
        }
        else
        {
            ImGui.SetNextItemWidth(Math.Min(190, Math.Max(100, ImGui.GetContentRegionAvail().X - 80)));
            ImGui.InputInt("Total gil", ref gilTotal, 0, 0); gilTotal = Math.Clamp(gilTotal, 0, (int)GilTradeRun.WalletLimit);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The total to send, divided into trades of at most 1,000,000 gil. The final trade sends only the remainder. The amount is checked against your character’s wallet.");
            ImGui.TextDisabled($"{gilTotal:N0} gil · {(gilTotal + (long)GilTradeRun.TradeLimit - 1) / GilTradeRun.TradeLimit:N0} trades");
        }
        if (ImGui.Button(gilReceive ? "Start receiving" : "Send gil")) StartGilTrade();
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

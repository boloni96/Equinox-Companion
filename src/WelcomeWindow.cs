using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static string CompanionVersion => typeof(Plugin).Assembly.GetName().Version?.ToString()??"0";
    private readonly WindowSystem welcomeWindows = new("EquinoxCompanion");
    private WelcomeWindow welcomeWindow = null!;
    private void DismissWelcome()
    {
        welcomeWindow.IsOpen=false;
        var needsSave = config.WelcomeVersion != CompanionVersion || AppearancePolicy.NeedsChoice(config.Appearance);
        config.WelcomeVersion=CompanionVersion;
        // Closing the introduction never opts into a new theme.
        config.Appearance=AppearancePolicy.Dismiss(config.Appearance);
        if(needsSave)SaveConfiguration();
    }
    // Both choices take the same launch path; appearance never selects a different feature set.
    private void ChooseWelcomeAppearance(bool goddessBlue)
    {
        SetAppearance(goddessBlue);
        visible=true;
        mainWindow.IsOpen=true;
        DismissWelcome();
    }
    private sealed class WelcomeWindow : Window
    {
        private readonly Plugin plugin;
        public WelcomeWindow(Plugin plugin):base("Welcome to Equinox Companion###EquinoxWelcome",ImGuiWindowFlags.None)
        {
            this.plugin=plugin;RespectCloseHotkey=false;
            IsOpen=plugin.config.WelcomeVersion!=CompanionVersion || AppearancePolicy.NeedsChoice(plugin.config.Appearance);
            Size=new Vector2(500,350);SizeCondition=ImGuiCond.FirstUseEver;
            SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(360,280),MaximumSize=new Vector2(900,850)};
        }
        public override void OnClose(){if(!plugin.minimizedLaunchers.Contains("Welcome"))plugin.DismissWelcome();}
        public override void PostDraw()=>HandleNativeCollapse(this,()=>plugin.MinimizeLauncher("Welcome"));
        public override void Draw()=>plugin.DrawWindowWithLauncher(DrawContent);
        private void DrawContent()
        {
            var font=ImGui.GetFontSize();var iconSize=font*2;
            var at=ImGui.GetCursorScreenPos();
            plugin.GardenImage("assets/category-icons/characters.png",at,new Vector2(iconSize));
            ImGui.Dummy(new Vector2(iconSize));ImGui.SameLine();ImGui.BeginGroup();
            ImGui.TextColored(new Vector4(.57f,.79f,1,1),"Welcome to EQUINOX");
            ImGui.TextDisabled("Companion "+CompanionVersion);ImGui.EndGroup();
            ImGui.TextWrapped("Your people, properties and adventures, together.");
            ImGui.Separator();
            ImGui.TextWrapped("This is Goddess blue. Choose your window design to open Companion.");
            if(ImGui.Button("Keep Goddess blue"))plugin.ChooseWelcomeAppearance(true);
            SameLineIfFits(ButtonWidth("Use classic Dalamud"));
            if(ImGui.Button("Use classic Dalamud"))plugin.ChooseWelcomeAppearance(false);
            ImGui.TextWrapped("Both designs receive the same features and updates.");
            ImGui.TextDisabled(plugin.GoddessAppearance?"Saved choice: Goddess blue":"Saved choice: classic Dalamud");
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Your choice stays saved after restarting and updating. Closing this welcome keeps it. Change it any time in Settings → Appearance.");
            ImGui.Separator();
            ImGui.TextWrapped(plugin.config.PairingKey.Length==64
                ?"Your pairing and settings are ready. Continue with your shared Journal."
                :"First time? Open Settings → Connection and paste the pairing key from your Journal's Game connection.");
            if(ImGui.CollapsingHeader("Quick guide"))
            {
                ImGui.TextWrapped("Goddess blue applies to Companion, Adventure, Gardening, housing bids, Fashion Report and item tools. Click the arrow halfway down the menu divider to show icons only or icons with names. Your choice stays saved when resizing. Settings and other submenus keep their horizontal tabs, styled to match.");
                ImGui.TextWrapped("House shortcuts stay on the right of character controls and stack vertically in narrow windows. Adventure keeps portraits left, details in the middle and steps below.");
                ImGui.TextWrapped("Adventure shows overall activities done, with separate current-stop confirmation. Its world selector stays compact; Bring here and Cancel bring remain reachable. Window keybinds toggle open and closed.");
                ImGui.TextWrapped(plugin.config.PairingKey.Length==64?"Your saved pairing and settings are kept.":"Start in Settings → Connection: paste the pairing key from your Journal's Game connection.");
                ImGui.TextWrapped("/equinox · /adventure · /gardening · /fashionr · /bid · /eqtests · /eqtrade. Esc minimises a focused window to its familiar icon. Commands reopen windows; shortcuts toggle them.");
                if(ImGui.SmallButton("Open Companion")){plugin.visible=true;plugin.mainWindow.IsOpen=true;plugin.DismissWelcome();}
            }
        }
    }
}

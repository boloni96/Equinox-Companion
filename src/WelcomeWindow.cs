using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static string CompanionVersion => typeof(Plugin).Assembly.GetName().Version?.ToString()??"0";
    private readonly WindowSystem welcomeWindows = new("EquinoxCompanion");
    private WelcomeWindow welcomeWindow = null!;
    private bool welcomeFinished;
    private void DismissWelcome(bool keepSelection=false)
    {
        if(welcomeFinished)return;
        welcomeFinished=true;
        welcomeWindow.IsOpen=false;
        config.WelcomeVersion=CompanionVersion;
        if(!keepSelection)config.Appearance=AppearancePolicy.Dismiss(config.Appearance);
        SaveConfiguration();
    }
    // Both choices take the same launch path; appearance never selects a different feature set.
    private void ChooseWelcomeAppearance(bool goddessBlue)
    {
        SetAppearance(goddessBlue);
        visible=true;
        mainWindow.IsOpen=true;
        DismissWelcome(true);
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
        public override void OnOpen()=>plugin.welcomeFinished=false;
        public override void PostDraw()=>HandleNativeCollapse(this,()=>plugin.MinimizeLauncher("Welcome"));
        public override void Draw()=>plugin.DrawWindowWithLauncher(DrawContent);
        private void DrawContent()
        {
            var font=ImGui.GetFontSize();var iconSize=font*3;
            var at=ImGui.GetCursorScreenPos();
            var icon=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"icon.png")).GetWrapOrDefault();
            if(icon is not null)ImGui.GetWindowDrawList().AddImage(icon.Handle,at,at+new Vector2(iconSize));
            ImGui.Dummy(new Vector2(iconSize));ImGui.SameLine();ImGui.BeginGroup();
            ImGui.TextColored(new Vector4(.57f,.79f,1,1),"Welcome, Goddess");
            ImGui.SameLine();var heart=ImGui.GetCursorScreenPos();var blue=ImGui.GetColorU32(new Vector4(.32f,.68f,1,1));var draw=ImGui.GetWindowDrawList();
            draw.AddCircleFilled(heart+new Vector2(font*.27f,font*.32f),font*.25f,blue);draw.AddCircleFilled(heart+new Vector2(font*.67f,font*.32f),font*.25f,blue);
            draw.AddTriangleFilled(heart+new Vector2(.04f,.4f)*font,heart+new Vector2(.9f,.4f)*font,heart+new Vector2(.47f,.95f)*font,blue);ImGui.Dummy(new Vector2(font));
            ImGui.TextDisabled("Companion "+CompanionVersion);ImGui.EndGroup();
            ImGui.TextWrapped("Your Empire, with a little less to carry.");
            ImGui.Spacing();
            ImGui.TextWrapped("Equinox Companion was created to help you care for your Equinox Empire—from every little garden to every place you call home. A little less to remember, a little more time to enjoy the worlds you’ve made your own.");
            ImGui.TextColored(new Vector4(.57f,.79f,1,1),"Made with care, for you.");
            ImGui.Separator();
            if(ImGui.Button("Keep Goddess Blue"))plugin.ChooseWelcomeAppearance(true);
            SameLineIfFits(ButtonWidth("Use classic Dalamud"));
            if(ImGui.Button("Use classic Dalamud"))plugin.ChooseWelcomeAppearance(false);
            SameLineIfFits(ButtonWidth("Close"));if(ImGui.Button("Close"))plugin.DismissWelcome();
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Keep Goddess Blue and close this welcome without opening Companion. Both designs have the same features. Change the design in Settings → Appearance.");
            if(ImGui.CollapsingHeader("Quick guide"))
            {
                ImGui.TextWrapped("Goddess blue applies to Companion, Adventure, Gardening, housing bids, Fashion Report and item tools. Click the arrow halfway down the menu divider to show icons only or icons with names. Your choice stays saved when resizing. Settings and other submenus keep their horizontal tabs, styled to match.");
                ImGui.TextWrapped("House shortcuts stay on the right of character controls and stack vertically in narrow windows. Adventure keeps portraits left, details in the middle and steps below.");
                ImGui.TextWrapped("Adventure shows overall activities done, with separate current-stop confirmation. Its world selector stays compact; Bring here and Cancel bring remain reachable. Window keybinds toggle open and closed.");
                ImGui.TextWrapped(plugin.config.PairingKey.Length==64?"Your saved pairing and settings are kept.":"Start in Settings → Connection: paste the pairing key from your Journal's Game connection.");
                ImGui.TextWrapped("/equinox · /adventure · /gardening · /fashionr · /bid · /eqtests · /eqtrade. Esc minimises a focused window to its familiar icon. Commands reopen windows; shortcuts toggle them.");
            }
        }
    }
}

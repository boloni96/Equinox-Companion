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
    private sealed class WelcomeWindow : Window
    {
        private readonly Plugin plugin;
        public WelcomeWindow(Plugin plugin):base("Welcome to Equinox Companion###EquinoxWelcome",ImGuiWindowFlags.None)
        {
            this.plugin=plugin;RespectCloseHotkey=false;
            IsOpen=plugin.config.WelcomeVersion!=CompanionVersion || AppearancePolicy.NeedsChoice(plugin.config.Appearance);
            Size=new Vector2(540,440);SizeCondition=ImGuiCond.FirstUseEver;
            SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(380,300),MaximumSize=new Vector2(900,850)};
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
            ImGui.TextColored(new Vector4(.57f,.79f,1,1),"EQUINOX · A little more room to play");
            ImGui.TextDisabled("Companion "+CompanionVersion);ImGui.EndGroup();
            ImGui.TextWrapped("Meet Goddess blue: a tighter menu, compact windows and the same familiar tools.");
            ImGui.TextDisabled("Design preview · example progress");
            if(ImGui.BeginChild("DesignPreview",new Vector2(0,font*9.5f),true))
            {
                if(ImGui.BeginTable("PreviewColumns",2,ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Menu",ImGuiTableColumnFlags.WidthFixed,font*6.3f);
                    ImGui.TableSetupColumn("Content",ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(new Vector4(.57f,.79f,1,1),"EQUINOX");
                    ImGui.TextUnformatted("My Empire");ImGui.TextUnformatted("Your people");
                    ImGui.TextUnformatted("Gardening");ImGui.TextUnformatted("Submarines");
                    ImGui.TextUnformatted("Testing");ImGui.TextUnformatted("Settings");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("Adventure");
                    ImGui.ProgressBar(.3f,new Vector2(-1,0),"6 / 20 done");
                    ImGui.TextDisabled("This stop · 2 / 3 confirmed");
                    ImGui.Separator();ImGui.TextUnformatted("Gardening");
                    ImGui.TextUnformatted("Fashion Report");
                    ImGui.TextWrapped("Details when you need them.");
                    ImGui.EndTable();
                }
            }
            ImGui.EndChild();
            ImGui.TextWrapped("Choose for this installation. You can switch at any time in Settings → Appearance.");
            if(ImGui.Button("Use the new design")){plugin.SetAppearance(true);plugin.visible=true;plugin.mainWindow.IsOpen=true;plugin.DismissWelcome();}
            SameLineIfFits(ButtonWidth("Keep the current design"));
            if(ImGui.Button("Keep the current design")){plugin.SetAppearance(false);plugin.DismissWelcome();}
            ImGui.TextDisabled(plugin.GoddessAppearance?"Saved choice: Goddess blue":"Saved choice: current design");
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Your choice is remembered after restarting and updating. Closing this preview keeps the saved choice. Pairing and game settings are unchanged.");
            if(ImGui.CollapsingHeader("What's new & guide"))
            {
                ImGui.TextWrapped("Goddess blue applies to Companion, Adventure, Gardening, housing bids, Fashion Report and item tools. The new Companion menu keeps your people and accounts, with Testing built in. Narrow windows use menu icons with hover names.");
                ImGui.TextWrapped("Adventure shows overall activities done, with separate current-stop confirmation. Its world selector stays compact; Bring here and Cancel bring remain reachable. Window keybinds toggle open and closed.");
                ImGui.TextWrapped(plugin.config.PairingKey.Length==64?"Your saved pairing and settings are kept.":"Start in Settings → Connection: paste the pairing key from your Journal's Game connection.");
                ImGui.TextWrapped("/equinox · /adventure · /gardening · /fashionr · /bid · /eqtests. Esc minimises a focused window to its familiar icon. Commands reopen windows; shortcuts toggle them.");
                if(ImGui.SmallButton("Open Companion")){plugin.visible=true;plugin.mainWindow.IsOpen=true;plugin.DismissWelcome();}
            }
        }
    }
}
